using System.Globalization;
using System.Text.Json;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed partial class PlannerEmergenceService
{
    private TaskNode ApplyMetadata(
        TaskNode task,
        PlannerReplanEntry? replan,
        PlanningSignalRecord? signal,
        ExecutionMemoryRecord memory,
        IReadOnlyList<SuggestedTaskRecord>? suggestions)
    {
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["planner_last_memory_id"] = memory.MemoryId,
            ["planner_last_memory_event"] = memory.EventKind,
        };

        if (signal is null)
        {
            metadata.Remove("planner_signal_id");
            metadata.Remove("planner_signal_kind");
            metadata.Remove("planner_signal_summary");
        }
        else
        {
            metadata["planner_signal_id"] = signal.SignalId;
            metadata["planner_signal_kind"] = signal.Kind.ToString();
            metadata["planner_signal_summary"] = signal.Summary;
        }

        if (replan is null)
        {
            metadata.Remove("planner_replan_required");
            metadata.Remove("planner_replan_entry_id");
            metadata.Remove("planner_entry_reason");
            metadata.Remove("planner_suggested_task_count");
            metadata.Remove("planner_suggested_task_ids");
        }
        else
        {
            metadata["planner_replan_required"] = "true";
            metadata["planner_replan_entry_id"] = replan.EntryId;
            metadata["planner_entry_reason"] = replan.Trigger.ToString();
            metadata["planner_suggested_task_count"] = (suggestions?.Count ?? 0).ToString(CultureInfo.InvariantCulture);
            metadata["planner_suggested_task_ids"] = suggestions is null ? string.Empty : string.Join(',', suggestions.Select(item => item.SuggestionId));
        }

        return Clone(task, metadata);
    }

    private PlannerReplanEntry PersistReplanEntry(
        TaskNode task,
        PlannerReplanTrigger trigger,
        string reason,
        string? runId,
        string? failureId,
        string? incidentId,
        string? changeSurfaceId,
        string? signalId,
        IReadOnlyList<SuggestedTaskRecord> suggestions)
    {
        var entry = new PlannerReplanEntry
        {
            EntryId = CreateSequentialId("REPLAN", task.TaskId, GetReplanRoot(task.TaskId)),
            TaskId = task.TaskId,
            Trigger = trigger,
            Reason = reason,
            RunId = runId,
            FailureId = failureId,
            IncidentId = incidentId,
            ChangeSurfaceId = changeSurfaceId,
            PlanningSignalId = signalId,
            SuggestedTaskIds = suggestions.Select(item => item.SuggestionId).ToArray(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        WriteJson(GetReplanEntryPath(task.TaskId, entry.EntryId), entry);
        return entry;
    }

    private StructuredIncidentRecord PersistIncident(
        TaskNode task,
        PlannerReplanTrigger trigger,
        string? runId,
        string? failureId,
        FailureType? failureType,
        string summary,
        IReadOnlyList<string> logs,
        IReadOnlyList<string> suspectedArea)
    {
        var incident = new StructuredIncidentRecord
        {
            IncidentId = CreateSequentialId("INC", task.TaskId, GetIncidentRoot(task.TaskId)),
            TaskId = task.TaskId,
            RunId = runId,
            FailureId = failureId,
            FailureType = failureType,
            Trigger = trigger,
            Summary = summary,
            Logs = logs,
            Context = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["task_status"] = task.Status.ToString(),
                ["planner_verdict"] = task.PlannerReview.Verdict.ToString(),
                ["planner_reason"] = task.PlannerReview.Reason,
            },
            SuspectedArea = suspectedArea.Count == 0 ? task.Scope : suspectedArea,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        WriteJson(GetIncidentPath(task.TaskId, incident.IncidentId), incident);
        return incident;
    }

    private PatchChangeSurface PersistChangeSurface(
        TaskNode task,
        string? runId,
        ExecutionRunReport? latestReport,
        ResultEnvelope? envelope)
    {
        var changedPaths = ResolveChangedPaths(task, latestReport, envelope);
        var modules = changedPaths
            .Select(NormalizeModule)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var filesChanged = envelope?.Changes.FilesModified.Concat(envelope.Changes.FilesAdded).Distinct(StringComparer.Ordinal).Count()
            ?? latestReport?.FilesChanged
            ?? changedPaths.Count;
        var linesChanged = envelope?.Changes.LinesChanged ?? 0;
        var risk = filesChanged >= 6 || linesChanged >= 200 || modules.Length >= 3
            ? ChangeSurfaceRiskLevel.High
            : filesChanged >= 3 || linesChanged >= 80
                ? ChangeSurfaceRiskLevel.Medium
                : ChangeSurfaceRiskLevel.Low;
        var surface = new PatchChangeSurface
        {
            SurfaceId = CreateSequentialId("SURF", task.TaskId, GetChangeSurfaceRoot(task.TaskId)),
            TaskId = task.TaskId,
            RunId = runId,
            FilesChanged = filesChanged,
            LinesChanged = linesChanged,
            ModulesTouched = modules,
            RiskLevel = risk,
            Summary = $"{filesChanged} files changed across {Math.Max(1, modules.Length)} modules; risk {risk.ToString().ToLowerInvariant()}.",
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };
        WriteJson(GetChangeSurfacePath(task.TaskId, surface.SurfaceId), surface);
        return surface;
    }

    private PlanningSignalRecord? PersistSignal(
        TaskNode task,
        string? runId,
        FailureReport? failure,
        ExecutionPattern pattern,
        PlanningSignalKind fallbackKind)
    {
        var shouldEmit = failure is not null || pattern.Type != ExecutionPatternType.HealthyProgress || fallbackKind == PlanningSignalKind.ReviewGap;
        if (!shouldEmit)
        {
            return null;
        }

        var signal = new PlanningSignalRecord
        {
            SignalId = CreateSequentialId("SIG", task.TaskId, GetSignalRoot(task.TaskId)),
            TaskId = task.TaskId,
            Kind = pattern.Type == ExecutionPatternType.HealthyProgress ? fallbackKind : PlanningSignalKind.ExecutionPattern,
            Severity = MapSeverity(pattern, failure),
            Summary = pattern.Type == ExecutionPatternType.HealthyProgress
                ? failure?.Failure.Message ?? task.PlannerReview.Reason
                : pattern.Summary,
            RecommendedAction = DescribePlanningSuggestion(pattern, failure),
            PatternType = pattern.Type == ExecutionPatternType.HealthyProgress ? null : pattern.Type,
            FailureId = failure?.Id,
            RunIds = pattern.Evidence.Select(item => item.RunId).Append(runId ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        WriteJson(GetSignalPath(task.TaskId, signal.SignalId), signal);
        return signal;
    }

    private IReadOnlyList<SuggestedTaskRecord> PersistSuggestedTasks(
        TaskNode task,
        StructuredIncidentRecord incident,
        PatchChangeSurface changeSurface,
        PlanningSignalRecord? signal,
        ExecutionPattern pattern)
    {
        var proposedTaskId = $"{task.TaskId}-S{ListSuggestedTasks(task.TaskId).Count + 1:00}";
        var scope = ResolveSuggestedScope(task, changeSurface);
        var acceptance = BuildSuggestedAcceptance(task, incident, pattern);
        var constraints = BuildSuggestedConstraints(changeSurface, pattern);
        var guard = EvaluateGuard(task, scope, acceptance);
        var suggestion = new SuggestedTaskRecord
        {
            SuggestionId = CreateSequentialId("SUG", task.TaskId, GetSuggestedTaskRoot(task.TaskId)),
            SourceTaskId = task.TaskId,
            ProposedTaskId = proposedTaskId,
            CardId = task.CardId,
            Status = guard.Verdict == PlannerSuggestionGuardVerdict.Reject ? PlannerSuggestionStatus.Rejected : PlannerSuggestionStatus.Draft,
            GuardVerdict = guard.Verdict,
            GuardReason = guard.Reason,
            Title = BuildSuggestedTitle(task, incident, pattern),
            Description = BuildSuggestedDescription(task, incident, pattern),
            Reason = incident.Summary,
            Target = pattern.Type switch
            {
                ExecutionPatternType.ScopeDrift or ExecutionPatternType.BoundaryLoop or ExecutionPatternType.ReplanLoop => "narrow_scope",
                _ => "follow_up",
            },
            Dependencies = task.Dependencies,
            Scope = scope,
            Acceptance = acceptance,
            Constraints = constraints,
            IncidentId = incident.IncidentId,
            PlanningSignalId = signal?.SignalId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        SaveSuggestion(suggestion);
        return [suggestion];
    }

    private ExecutionMemoryRecord AppendExecutionMemory(
        TaskNode task,
        string eventKind,
        string? runId,
        string? failureId,
        string? reviewVerdict,
        string status,
        string summary,
        IReadOnlyList<string> artifactPaths)
    {
        var memory = new ExecutionMemoryRecord
        {
            MemoryId = CreateSequentialId("MEM", task.TaskId, GetExecutionMemoryRoot(task.TaskId)),
            TaskId = task.TaskId,
            EventKind = eventKind,
            RunId = runId,
            FailureId = failureId,
            ReviewVerdict = reviewVerdict,
            Status = status,
            Summary = summary,
            ArtifactPaths = artifactPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray(),
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };
        WriteJson(GetExecutionMemoryPath(task.TaskId, memory.MemoryId), memory);
        return memory;
    }

    private void RecordPlanningEvidence(
        TaskNode task,
        string summary,
        IReadOnlyList<string> artifactPaths,
        string? runId)
    {
        evidenceStoreService.RecordPlanning(
            task,
            producer: nameof(PlannerEmergenceService),
            summary: summary,
            artifactPaths: artifactPaths,
            runId: runId,
            sourceEvidenceIds: ResolvePlanningSourceEvidenceIds(task.TaskId, runId));
    }

    private (SuggestedTaskRecord? Record, string? Path) TryLoadSuggestionById(string suggestionId)
    {
        if (!Directory.Exists(SuggestionsRoot))
        {
            return default;
        }

        var path = Directory.EnumerateFiles(SuggestionsRoot, $"{suggestionId}.json", SearchOption.AllDirectories).FirstOrDefault();
        return path is null
            ? default
            : (JsonSerializer.Deserialize<SuggestedTaskRecord>(File.ReadAllText(path), JsonOptions), path);
    }

    private void SaveSuggestion(SuggestedTaskRecord suggestion)
    {
        WriteJson(GetSuggestedTaskPath(suggestion.SourceTaskId, suggestion.SuggestionId), suggestion);
    }

    private ExecutionRun? ResolveRun(TaskNode task, string? runId)
    {
        if (!string.IsNullOrWhiteSpace(runId))
        {
            return executionRunService.TryLoad(runId);
        }

        return executionRunService.ListRuns(task.TaskId).LastOrDefault();
    }

    private void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private IReadOnlyList<string> ResolvePlanningSourceEvidenceIds(string taskId, string? runId)
    {
        var sourceIds = new List<string>();
        var runEvidence = evidenceStoreService.TryGetLatest(taskId, Carves.Runtime.Domain.Evidence.RuntimeEvidenceKind.ExecutionRun, runId);
        if (runEvidence is not null)
        {
            sourceIds.Add(runEvidence.EvidenceId);
        }

        var contextEvidence = evidenceStoreService.TryGetLatest(taskId, Carves.Runtime.Domain.Evidence.RuntimeEvidenceKind.ContextPack);
        if (contextEvidence is not null)
        {
            sourceIds.Add(contextEvidence.EvidenceId);
        }

        return sourceIds.Distinct(StringComparer.Ordinal).ToArray();
    }
}
