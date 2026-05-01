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
    private static bool ShouldCreateReplanEntryForResult(TaskNode task, ResultEnvelope envelope, FailureReport? failure)
    {
        if (failure?.Attribution.Layer is FailureAttributionLayer.Environment or FailureAttributionLayer.Provider)
        {
            return false;
        }

        return failure is not null
               || task.Status == DomainTaskStatus.Review && !task.PlannerReview.AcceptanceMet
               || string.Equals(envelope.Status, "blocked", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveChangedPaths(TaskNode task, ExecutionRunReport? latestReport, ResultEnvelope? envelope)
    {
        if (latestReport is not null && latestReport.ModulesTouched.Count > 0)
        {
            return latestReport.ModulesTouched;
        }

        if (envelope is not null)
        {
            return envelope.Changes.FilesModified
                .Concat(envelope.Changes.FilesAdded)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return task.Scope;
    }

    private static string NormalizeModule(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var slashIndex = normalized.LastIndexOf('/');
        return (slashIndex <= 0 ? normalized : normalized[..slashIndex]).ToLowerInvariant();
    }

    private static PlanningSignalSeverity MapSeverity(ExecutionPattern pattern, FailureReport? failure)
    {
        if (pattern.Type != ExecutionPatternType.HealthyProgress)
        {
            return pattern.Severity switch
            {
                ExecutionPatternSeverity.High => PlanningSignalSeverity.High,
                ExecutionPatternSeverity.Medium => PlanningSignalSeverity.Medium,
                _ => PlanningSignalSeverity.Low,
            };
        }

        return failure?.Failure.Type switch
        {
            FailureType.TestRegression or FailureType.ContractViolation => PlanningSignalSeverity.High,
            FailureType.BuildFailure or FailureType.ReviewRejected => PlanningSignalSeverity.Medium,
            _ => PlanningSignalSeverity.Low,
        };
    }

    private static string DescribePlanningSuggestion(ExecutionPattern pattern, FailureReport? failure)
    {
        if (pattern.Type != ExecutionPatternType.HealthyProgress)
        {
            return pattern.Suggestion switch
            {
                ExecutionPatternSuggestion.ChangeReplanStrategy => "Change replan strategy before the next execution attempt.",
                ExecutionPatternSuggestion.NarrowScope => "Narrow scope before generating another execution task.",
                ExecutionPatternSuggestion.ManualReview => "Escalate to planner or operator review before continuing.",
                ExecutionPatternSuggestion.PauseAndReview => "Pause execution and inspect why repeated runs are not converging.",
                _ => "Continue within the current execution budget.",
            };
        }

        return failure?.Failure.Type switch
        {
            FailureType.TestRegression => "Generate a focused validation repair task before continuing.",
            FailureType.ReviewRejected => "Create a follow-up task that addresses the rejected review gap.",
            _ => "Create a smaller follow-up task before retrying execution.",
        };
    }

    private static string BuildResultSummary(TaskNode task, ResultEnvelope envelope, FailureReport? failure, ExecutionPattern pattern)
    {
        if (string.Equals(envelope.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            return $"Execution result for {task.TaskId} completed successfully.";
        }

        if (failure is not null)
        {
            return $"{failure.Failure.Type}: {failure.Failure.Message}";
        }

        return pattern.Type == ExecutionPatternType.HealthyProgress
            ? $"Execution result for {task.TaskId} ended with status {envelope.Status}."
            : pattern.Summary;
    }

    private static string BuildSuggestedTitle(TaskNode task, StructuredIncidentRecord incident, ExecutionPattern pattern)
    {
        return pattern.Type switch
        {
            ExecutionPatternType.ScopeDrift => $"Narrow scope for {task.TaskId}",
            ExecutionPatternType.BoundaryLoop or ExecutionPatternType.ReplanLoop => $"Change replan approach for {task.TaskId}",
            _ when incident.FailureType is FailureType.TestRegression => $"Repair validation gap for {task.TaskId}",
            _ => $"Follow up on {task.TaskId}",
        };
    }

    private static string BuildSuggestedDescription(TaskNode task, StructuredIncidentRecord incident, ExecutionPattern pattern)
    {
        if (pattern.Type is ExecutionPatternType.ScopeDrift or ExecutionPatternType.BoundaryLoop or ExecutionPatternType.ReplanLoop)
        {
            return $"Reduce the execution surface for {task.TaskId} before another delegated run.";
        }

        return incident.FailureType switch
        {
            FailureType.TestRegression => $"Create a focused repair step that restores validation for the last {task.TaskId} execution.",
            FailureType.ReviewRejected => $"Address the rejected review outcome for {task.TaskId} with a smaller follow-up task.",
            _ => $"Investigate the gap recorded for {task.TaskId} and prepare a bounded follow-up task.",
        };
    }

    private static IReadOnlyList<string> ResolveSuggestedScope(TaskNode task, PatchChangeSurface changeSurface)
    {
        if (changeSurface.ModulesTouched.Count > 0)
        {
            return changeSurface.ModulesTouched.Take(3).ToArray();
        }

        return task.Scope.Take(3).ToArray();
    }

    private static IReadOnlyList<string> BuildSuggestedAcceptance(TaskNode task, StructuredIncidentRecord incident, ExecutionPattern pattern)
    {
        if (pattern.Type is ExecutionPatternType.BoundaryLoop or ExecutionPatternType.ReplanLoop)
        {
            return
            [
                "the next execution attempt is smaller than the previous loop",
                "the follow-up task stays within a narrower scope",
            ];
        }

        return incident.FailureType switch
        {
            FailureType.TestRegression =>
            [
                "the failing validation path is isolated",
                "the next delegated run has a clear acceptance target",
            ],
            _ =>
            [
                $"the follow-up task addresses the recorded gap for {task.TaskId}",
                "the follow-up scope is bounded and reviewable",
            ],
        };
    }

    private static IReadOnlyList<string> BuildSuggestedConstraints(PatchChangeSurface changeSurface, ExecutionPattern pattern)
    {
        var constraints = new List<string> { "host-routed suggested task insertion only" };
        if (pattern.Type is ExecutionPatternType.ScopeDrift or ExecutionPatternType.BoundaryLoop)
        {
            constraints.Add("narrow scope before re-dispatch");
        }

        if (changeSurface.RiskLevel == ChangeSurfaceRiskLevel.High)
        {
            constraints.Add("prefer smaller bounded change sets");
        }

        return constraints;
    }

    private static (PlannerSuggestionGuardVerdict Verdict, string Reason) EvaluateGuard(
        TaskNode task,
        IReadOnlyList<string> scope,
        IReadOnlyList<string> acceptance)
    {
        if (scope.Count == 0 || acceptance.Count == 0)
        {
            return (PlannerSuggestionGuardVerdict.Reject, "missing_scope_or_acceptance");
        }

        if (scope.Any(IsProtectedPath))
        {
            return (PlannerSuggestionGuardVerdict.Reject, "protected_control_plane_scope");
        }

        var maxScope = Math.Max(4, Math.Max(1, task.Scope.Count) * 2);
        if (scope.Count > maxScope)
        {
            return (PlannerSuggestionGuardVerdict.Reject, "scope_explosion");
        }

        var overlapsSource = task.Scope.Count == 0
            || scope.Any(item => task.Scope.Any(source => item.Contains(source, StringComparison.OrdinalIgnoreCase) || source.Contains(item, StringComparison.OrdinalIgnoreCase)));
        return overlapsSource
            ? (PlannerSuggestionGuardVerdict.Pass, "within_source_scope")
            : (PlannerSuggestionGuardVerdict.Warn, "scope_not_proven_by_source");
    }

    private static bool IsProtectedPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith(".ai/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(".carves-platform/", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> CollectArtifactPaths(
        string? replanPath = null,
        string? incidentPath = null,
        string? changeSurfacePath = null,
        string? signalPath = null,
        string? reviewPath = null,
        string? resultPath = null,
        string? boundaryPath = null)
    {
        return new[]
        {
            replanPath,
            incidentPath,
            changeSurfacePath,
            signalPath,
            reviewPath,
            resultPath,
            boundaryPath,
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Cast<string>()
        .ToArray();
    }

    private static string CreateSequentialId(string prefix, string taskId, string root)
    {
        Directory.CreateDirectory(root);
        var next = Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly).Count() + 1;
        return $"{prefix}-{taskId}-{next:000}";
    }

    private static IReadOnlyList<T> ListRecords<T>(string root)
    {
        if (!Directory.Exists(root))
        {
            return Array.Empty<T>();
        }

        return Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions))
            .Where(item => item is not null)
            .Cast<T>()
            .ToArray();
    }

    private string GetResultPath(string taskId) => Path.Combine(paths.AiRoot, "execution", taskId, "result.json");
    private string GetBoundaryViolationPath(string taskId) => Path.Combine(paths.AiRoot, "runtime", "boundary", "violations", $"{taskId}.json");
    private string GetBoundaryReplanPath(string taskId) => Path.Combine(paths.AiRoot, "runtime", "boundary", "replans", $"{taskId}.json");
    private string GetReviewArtifactPath(string taskId) => Path.Combine(paths.ReviewArtifactsRoot, $"{taskId}.json");
    private string GetReplanRoot(string taskId) => Path.Combine(paths.RuntimeRoot, "planning", "replans", taskId);
    private string GetIncidentRoot(string taskId) => Path.Combine(paths.RuntimeRoot, "planning", "incidents", taskId);
    private string GetChangeSurfaceRoot(string taskId) => Path.Combine(paths.RuntimeRoot, "planning", "change-surfaces", taskId);
    private string GetSuggestedTaskRoot(string taskId) => Path.Combine(paths.RuntimeRoot, "planning", "suggested-tasks", taskId);
    private string GetSignalRoot(string taskId) => Path.Combine(paths.RuntimeRoot, "planning", "signals", taskId);
    private string GetExecutionMemoryRoot(string taskId) => Path.Combine(paths.AiRoot, "memory", "execution", taskId);
    private string SuggestionsRoot => Path.Combine(paths.RuntimeRoot, "planning", "suggested-tasks");
    private string SignalsRoot => Path.Combine(paths.RuntimeRoot, "planning", "signals");
    private string ExecutionMemoryRoot => Path.Combine(paths.AiRoot, "memory", "execution");
    private string GetReplanEntryPath(string taskId, string entryId) => Path.Combine(GetReplanRoot(taskId), $"{entryId}.json");
    private string GetIncidentPath(string taskId, string incidentId) => Path.Combine(GetIncidentRoot(taskId), $"{incidentId}.json");
    private string GetChangeSurfacePath(string taskId, string surfaceId) => Path.Combine(GetChangeSurfaceRoot(taskId), $"{surfaceId}.json");
    private string GetSuggestedTaskPath(string taskId, string suggestionId) => Path.Combine(GetSuggestedTaskRoot(taskId), $"{suggestionId}.json");
    private string GetSignalPath(string taskId, string signalId) => Path.Combine(GetSignalRoot(taskId), $"{signalId}.json");
    private string GetExecutionMemoryPath(string taskId, string memoryId) => Path.Combine(GetExecutionMemoryRoot(taskId), $"{memoryId}.json");

    private static bool IsTrue(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value)
               && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static TaskNode Clone(TaskNode task, IReadOnlyDictionary<string, string> metadata)
    {
        return new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource,
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies,
            Scope = task.Scope,
            Acceptance = task.Acceptance,
            Constraints = task.Constraints,
            AcceptanceContract = task.AcceptanceContract,
            Validation = task.Validation,
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities,
            Metadata = metadata,
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind,
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction,
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore,
            PlannerReview = task.PlannerReview,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
