using System.Globalization;
using System.Text.Json;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Planning;

public sealed partial class PlannerEmergenceService
{
    public TaskNode CaptureResult(TaskNode task, ResultEnvelope envelope, FailureReport? failure)
    {
        var run = ResolveRun(task, envelope.ExecutionRunId);
        var reports = executionRunReportService.ListReports(task.TaskId);
        var latestReport = reports.LastOrDefault();
        var changeSurface = PersistChangeSurface(task, run?.RunId, latestReport, envelope);
        var pattern = executionPatternService.Analyze(task.TaskId, reports);
        var signal = PersistSignal(task, run?.RunId, failure, pattern, PlanningSignalKind.FailureContext);
        var memory = AppendExecutionMemory(
            task,
            eventKind: string.Equals(envelope.Status, "success", StringComparison.OrdinalIgnoreCase) ? "result_success" : "result_failure",
            run?.RunId,
            failure?.Id,
            task.PlannerReview.Verdict.ToString(),
            task.Status.ToString(),
            BuildResultSummary(task, envelope, failure, pattern),
            CollectArtifactPaths(
                changeSurfacePath: GetChangeSurfacePath(task.TaskId, changeSurface.SurfaceId),
                signalPath: signal is null ? null : GetSignalPath(task.TaskId, signal.SignalId),
                resultPath: GetResultPath(task.TaskId)));
        RecordPlanningEvidence(
            task,
            BuildResultSummary(task, envelope, failure, pattern),
            memory.ArtifactPaths,
            run?.RunId);

        if (!ShouldCreateReplanEntryForResult(task, envelope, failure))
        {
            return ApplyMetadata(task, null, signal, memory, null);
        }

        var incident = PersistIncident(
            task,
            PlannerReplanTrigger.TaskFailed,
            run?.RunId,
            failure?.Id,
            failure?.Failure.Type,
            failure?.Failure.Message ?? envelope.Failure.Message ?? envelope.Result.StopReason,
            envelope.Validation.CommandsRun,
            changeSurface.ModulesTouched);
        var suggestions = PersistSuggestedTasks(task, incident, changeSurface, signal, pattern);
        var replan = PersistReplanEntry(
            task,
            PlannerReplanTrigger.TaskFailed,
            task.PlannerReview.Reason,
            run?.RunId,
            failure?.Id,
            incident.IncidentId,
            changeSurface.SurfaceId,
            signal?.SignalId,
            suggestions);
        return ApplyMetadata(task, replan, signal, memory, suggestions);
    }

    public TaskNode CaptureBoundaryStop(TaskNode task, ExecutionBoundaryViolation violation, ExecutionBoundaryReplanRequest replan)
    {
        var reports = executionRunReportService.ListReports(task.TaskId);
        var pattern = executionPatternService.Analyze(task.TaskId, reports);
        var signal = PersistSignal(task, violation.RunId, null, pattern, PlanningSignalKind.ExecutionPattern);
        var memory = AppendExecutionMemory(
            task,
            "boundary_stop",
            violation.RunId,
            null,
            task.PlannerReview.Verdict.ToString(),
            task.Status.ToString(),
            $"Boundary stopped execution for {task.TaskId}: {violation.Reason}.",
            CollectArtifactPaths(
                boundaryPath: GetBoundaryViolationPath(task.TaskId),
                replanPath: GetBoundaryReplanPath(task.TaskId),
                signalPath: signal is null ? null : GetSignalPath(task.TaskId, signal.SignalId)));
        RecordPlanningEvidence(
            task,
            $"Boundary stopped execution for {task.TaskId}: {violation.Reason}.",
            memory.ArtifactPaths,
            violation.RunId);
        return ApplyMetadata(task, null, signal, memory, null);
    }

    public TaskNode CaptureReviewOutcome(TaskNode task, PlannerVerdict verdict, string reason)
    {
        var memory = AppendExecutionMemory(
            task,
            verdict switch
            {
                PlannerVerdict.Complete => "review_approved",
                PlannerVerdict.Blocked => "review_blocked",
                PlannerVerdict.Superseded => "review_superseded",
                _ => "review_recorded",
            },
            task.Metadata.GetValueOrDefault("execution_run_latest_id"),
            task.Metadata.GetValueOrDefault("last_failure_id"),
            verdict.ToString(),
            task.Status.ToString(),
            $"Review recorded for {task.TaskId}: {verdict}.",
            CollectArtifactPaths(reviewPath: GetReviewArtifactPath(task.TaskId)));
        RecordPlanningEvidence(
            task,
            $"Review recorded for {task.TaskId}: {verdict}.",
            memory.ArtifactPaths,
            task.Metadata.GetValueOrDefault("execution_run_latest_id"));

        if (verdict is not (PlannerVerdict.PauseForReview or PlannerVerdict.HumanDecisionRequired or PlannerVerdict.SplitTask))
        {
            return ApplyMetadata(task, null, null, memory, null);
        }

        var reports = executionRunReportService.ListReports(task.TaskId);
        var latestReport = reports.LastOrDefault();
        var changeSurface = PersistChangeSurface(task, latestReport?.RunId, latestReport, envelope: null);
        var pattern = executionPatternService.Analyze(task.TaskId, reports);
        var signal = PersistSignal(task, latestReport?.RunId, null, pattern, PlanningSignalKind.ReviewGap);
        var incident = PersistIncident(
            task,
            PlannerReplanTrigger.AcceptanceUnmet,
            latestReport?.RunId,
            task.Metadata.GetValueOrDefault("last_failure_id"),
            null,
            reason,
            Array.Empty<string>(),
            changeSurface.ModulesTouched);
        var suggestions = PersistSuggestedTasks(task, incident, changeSurface, signal, pattern);
        var replan = PersistReplanEntry(
            task,
            PlannerReplanTrigger.AcceptanceUnmet,
            reason,
            latestReport?.RunId,
            task.Metadata.GetValueOrDefault("last_failure_id"),
            incident.IncidentId,
            changeSurface.SurfaceId,
            signal?.SignalId,
            suggestions);
        return ApplyMetadata(task, replan, signal, memory, suggestions);
    }

    public TaskNode CaptureReviewRejection(TaskNode task, string reason)
    {
        var reports = executionRunReportService.ListReports(task.TaskId);
        var latestReport = reports.LastOrDefault();
        var changeSurface = PersistChangeSurface(task, latestReport?.RunId, latestReport, envelope: null);
        var pattern = executionPatternService.Analyze(task.TaskId, reports);
        var signal = PersistSignal(task, latestReport?.RunId, null, pattern, PlanningSignalKind.ReviewGap);
        var incident = PersistIncident(
            task,
            PlannerReplanTrigger.ReviewRejected,
            latestReport?.RunId,
            task.Metadata.GetValueOrDefault("last_failure_id"),
            FailureType.ReviewRejected,
            reason,
            Array.Empty<string>(),
            changeSurface.ModulesTouched);
        var suggestions = PersistSuggestedTasks(task, incident, changeSurface, signal, pattern);
        var replan = PersistReplanEntry(
            task,
            PlannerReplanTrigger.ReviewRejected,
            reason,
            latestReport?.RunId,
            task.Metadata.GetValueOrDefault("last_failure_id"),
            incident.IncidentId,
            changeSurface.SurfaceId,
            signal?.SignalId,
            suggestions);
        var memory = AppendExecutionMemory(
            task,
            "review_rejected",
            latestReport?.RunId,
            task.Metadata.GetValueOrDefault("last_failure_id"),
            PlannerVerdict.Continue.ToString(),
            task.Status.ToString(),
            $"Review rejected {task.TaskId} and returned it to planning.",
            CollectArtifactPaths(
                reviewPath: GetReviewArtifactPath(task.TaskId),
                replanPath: GetReplanEntryPath(task.TaskId, replan.EntryId),
                incidentPath: GetIncidentPath(task.TaskId, incident.IncidentId)));
        RecordPlanningEvidence(
            task,
            $"Review rejected {task.TaskId} and returned it to planning.",
            memory.ArtifactPaths,
            latestReport?.RunId);
        return ApplyMetadata(task, replan, signal, memory, suggestions);
    }

    public SuggestedTaskInsertionResult ApproveSuggestedTask(string suggestionId, string reason)
    {
        var loaded = TryLoadSuggestionById(suggestionId);
        if (loaded.Record is null || loaded.Path is null)
        {
            return new SuggestedTaskInsertionResult(false, $"Suggested task '{suggestionId}' was not found.");
        }

        var suggestion = loaded.Record;
        if (suggestion.Status != PlannerSuggestionStatus.Draft)
        {
            return new SuggestedTaskInsertionResult(false, $"Suggested task '{suggestionId}' is already {suggestion.Status.ToString().ToLowerInvariant()}.", suggestion, suggestion.InsertedTaskId);
        }

        if (suggestion.GuardVerdict == PlannerSuggestionGuardVerdict.Reject)
        {
            return new SuggestedTaskInsertionResult(false, $"Suggested task '{suggestionId}' is blocked by the soft guard: {suggestion.GuardReason}.", suggestion);
        }

        var graph = taskGraphService.Load();
        if (graph.Tasks.ContainsKey(suggestion.ProposedTaskId))
        {
            return new SuggestedTaskInsertionResult(false, $"Suggested task target '{suggestion.ProposedTaskId}' already exists in the task graph.", suggestion);
        }

        var sourceTask = taskGraphService.GetTask(suggestion.SourceTaskId);
        var insertedTask = new TaskNode
        {
            TaskId = suggestion.ProposedTaskId,
            Title = suggestion.Title,
            Description = suggestion.Description,
            Status = DomainTaskStatus.Suggested,
            TaskType = sourceTask.TaskType,
            Priority = sourceTask.Priority,
            Source = "PLANNER_EMERGENCE",
            CardId = suggestion.CardId ?? sourceTask.CardId,
            ProposalSource = TaskProposalSource.SuggestedTask,
            ProposalReason = suggestion.Reason,
            Dependencies = suggestion.Dependencies,
            Scope = suggestion.Scope,
            Acceptance = suggestion.Acceptance,
            Constraints = suggestion.Constraints,
            AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                suggestion.ProposedTaskId,
                suggestion.Title,
                suggestion.Description,
                suggestion.CardId ?? sourceTask.CardId,
                suggestion.Acceptance,
                suggestion.Constraints,
                validation: null,
                sourceContract: sourceTask.AcceptanceContract),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["suggested_task_id"] = suggestion.SuggestionId,
                ["suggested_task_source_id"] = suggestion.SourceTaskId,
                ["suggested_task_guard_verdict"] = suggestion.GuardVerdict.ToString(),
                ["suggested_task_guard_reason"] = suggestion.GuardReason,
                ["suggested_task_incident_id"] = suggestion.IncidentId ?? string.Empty,
                ["suggested_task_signal_id"] = suggestion.PlanningSignalId ?? string.Empty,
            },
        };

        taskGraphService.AddTasks([insertedTask]);
        var updated = suggestion with
        {
            Status = PlannerSuggestionStatus.Inserted,
            InsertedTaskId = insertedTask.TaskId,
            InsertedAtUtc = DateTimeOffset.UtcNow,
            ApprovalReason = reason,
        };
        SaveSuggestion(updated);
        AppendExecutionMemory(
            sourceTask,
            "suggested_task_inserted",
            sourceTask.Metadata.GetValueOrDefault("execution_run_latest_id"),
            sourceTask.Metadata.GetValueOrDefault("last_failure_id"),
            sourceTask.PlannerReview.Verdict.ToString(),
            insertedTask.Status.ToString(),
            $"Inserted suggested task {insertedTask.TaskId} from {suggestion.SuggestionId}.",
            [loaded.Path]);
        return new SuggestedTaskInsertionResult(true, $"Inserted suggested task {insertedTask.TaskId} in SUGGESTED state.", updated, insertedTask.TaskId);
    }

    public PlannerReplanEntry? TryGetLatestReplan(string taskId)
    {
        return ListRecords<PlannerReplanEntry>(GetReplanRoot(taskId))
            .OrderBy(item => item.CreatedAtUtc)
            .LastOrDefault();
    }

    public IReadOnlyList<SuggestedTaskRecord> ListSuggestedTasks(string taskId)
    {
        return ListRecords<SuggestedTaskRecord>(GetSuggestedTaskRoot(taskId))
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.SuggestionId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<ExecutionMemoryRecord> ListExecutionMemory(string taskId, int take = 10)
    {
        return ListRecords<ExecutionMemoryRecord>(GetExecutionMemoryRoot(taskId))
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.MemoryId, StringComparer.Ordinal)
            .Take(Math.Max(1, take))
            .ToArray();
    }

    public IReadOnlyList<PlanningSignalRecord> ListSignals(string taskId)
    {
        return ListRecords<PlanningSignalRecord>(GetSignalRoot(taskId))
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.SignalId, StringComparer.Ordinal)
            .ToArray();
    }

    public PlannerEmergenceProjection BuildProjection()
    {
        var graph = taskGraphService.Load();
        return new PlannerEmergenceProjection
        {
            ReplanRequiredTaskCount = graph.Tasks.Values.Count(task => IsTrue(task.Metadata, "planner_replan_required")),
            DraftSuggestedTaskCount = Directory.Exists(SuggestionsRoot)
                ? Directory.EnumerateFiles(SuggestionsRoot, "*.json", SearchOption.AllDirectories)
                    .Select(path => JsonSerializer.Deserialize<SuggestedTaskRecord>(File.ReadAllText(path), JsonOptions))
                    .Where(item => item is not null && item.Status == PlannerSuggestionStatus.Draft)
                    .Count()
                : 0,
            PlanningSignalCount = Directory.Exists(SignalsRoot)
                ? Directory.EnumerateFiles(SignalsRoot, "*.json", SearchOption.AllDirectories).Count()
                : 0,
            ExecutionMemoryRecordCount = Directory.Exists(ExecutionMemoryRoot)
                ? Directory.EnumerateFiles(ExecutionMemoryRoot, "*.json", SearchOption.AllDirectories).Count()
                : 0,
        };
    }
}
