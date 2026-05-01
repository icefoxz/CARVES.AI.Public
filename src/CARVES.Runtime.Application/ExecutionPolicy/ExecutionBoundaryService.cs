using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Planning;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ExecutionPolicy;

public sealed class ExecutionBoundaryService
{
    private readonly ExecutionBudgetFactory budgetFactory;
    private readonly ExecutionPathClassifier pathClassifier;
    private readonly ManagedWorkspacePathPolicyService? managedWorkspacePathPolicyService;

    public ExecutionBoundaryService(
        ExecutionBudgetFactory budgetFactory,
        ExecutionPathClassifier pathClassifier,
        ManagedWorkspacePathPolicyService? managedWorkspacePathPolicyService = null)
    {
        this.budgetFactory = budgetFactory;
        this.pathClassifier = pathClassifier;
        this.managedWorkspacePathPolicyService = managedWorkspacePathPolicyService;
    }

    public ExecutionBoundaryAssessment Assess(TaskNode task, ResultEnvelope result)
    {
        var confidence = budgetFactory.AssessConfidence(task);
        var budget = budgetFactory.Create(task);
        var observedPaths = result.Changes.FilesModified.Concat(result.Changes.FilesAdded).Distinct(StringComparer.Ordinal).ToArray();
        var priorFailures = ParseInt(task.Metadata, "failure_count");
        var retryCount = result.Telemetry.RetryCount > 0 ? result.Telemetry.RetryCount : task.RetryCount + 1;
        var failureCount = result.Telemetry.FailureCount > 0
            ? result.Telemetry.FailureCount
            : priorFailures + (!string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
        var failureDensity = retryCount <= 0 ? 0 : (double)failureCount / retryCount;
        var telemetry = result.Telemetry with
        {
            FilesChanged = observedPaths.Length,
            LinesChanged = result.Changes.LinesChanged,
            RetryCount = retryCount,
            FailureCount = failureCount,
            FailureDensity = failureDensity,
        };
        telemetry = telemetry with
        {
            ObservedPaths = result.Telemetry.ObservedPaths.Count == 0 ? observedPaths : result.Telemetry.ObservedPaths,
            ChangeKinds = result.Telemetry.ChangeKinds.Count == 0 ? pathClassifier.ClassifyMany(observedPaths) : result.Telemetry.ChangeKinds,
        };
        var managedWorkspacePathPolicy = managedWorkspacePathPolicyService?.Evaluate(task, telemetry.ObservedPaths)
            ?? new ManagedWorkspacePathPolicyAssessment();

        var reasons = new List<string>();
        var score = 0;
        var touchedFiles = telemetry.ObservedPaths.Count;
        ExecutionBoundaryStopReason? stopReason = null;
        var stopDetail = string.Empty;
        if (touchedFiles > budget.MaxFiles)
        {
            score += 2;
            reasons.Add($"Touched files exceeded budget ({touchedFiles} > {budget.MaxFiles}).");
            stopReason ??= ExecutionBoundaryStopReason.SizeExceeded;
            stopDetail = $"{touchedFiles} files exceeded the budget of {budget.MaxFiles}.";
        }

        if (result.Changes.LinesChanged > budget.MaxLinesChanged)
        {
            score += 2;
            reasons.Add($"Changed lines exceeded budget ({result.Changes.LinesChanged} > {budget.MaxLinesChanged}).");
            stopReason ??= ExecutionBoundaryStopReason.SizeExceeded;
            stopDetail = $"{result.Changes.LinesChanged} changed lines exceeded the budget of {budget.MaxLinesChanged}.";
        }

        if (telemetry.RetryCount > budget.MaxRetries)
        {
            score += 2;
            reasons.Add($"Retry count exceeded budget ({telemetry.RetryCount} > {budget.MaxRetries}).");
            stopReason ??= ExecutionBoundaryStopReason.RetryExceeded;
            stopDetail = $"{telemetry.RetryCount} retries exceeded the budget of {budget.MaxRetries}.";
        }

        if (telemetry.RetryCount >= 2 && telemetry.FailureDensity > budget.MaxFailureDensity)
        {
            score += 2;
            reasons.Add($"Failure density exceeded budget ({telemetry.FailureDensity:0.00} > {budget.MaxFailureDensity:0.00}).");
            stopReason ??= ExecutionBoundaryStopReason.UnstableExecution;
            stopDetail = $"{telemetry.FailureDensity:0.00} failure density exceeded the budget of {budget.MaxFailureDensity:0.00}.";
        }

        if (telemetry.DurationSeconds > budget.MaxDurationMinutes * 60)
        {
            score += 1;
            reasons.Add($"Execution duration exceeded budget ({telemetry.DurationSeconds}s > {budget.MaxDurationMinutes * 60}s).");
            stopReason ??= ExecutionBoundaryStopReason.Timeout;
            stopDetail = $"{telemetry.DurationSeconds}s exceeded the duration budget of {budget.MaxDurationMinutes * 60}s.";
        }

        if (managedWorkspacePathPolicy.DenyCount > 0)
        {
            score += 2;
            reasons.Add(managedWorkspacePathPolicy.Summary);
            stopReason = ExecutionBoundaryStopReason.ManagedWorkspaceDeniedPath;
            stopDetail = managedWorkspacePathPolicy.Summary;
        }

        if (managedWorkspacePathPolicy.HostOnlyCount > 0)
        {
            score += 2;
            reasons.Add(managedWorkspacePathPolicy.Summary);
            stopReason = ExecutionBoundaryStopReason.ManagedWorkspaceHostOnlyPath;
            stopDetail = managedWorkspacePathPolicy.Summary;
        }

        if (HasScopeViolation(task.Scope, telemetry.ObservedPaths))
        {
            score += 2;
            reasons.Add("Observed paths drifted outside the declared task scope.");
            stopReason ??= ExecutionBoundaryStopReason.ScopeViolation;
            stopDetail = "Observed file changes were outside the declared task scope.";
        }

        if (managedWorkspacePathPolicy.ScopeEscapeCount > 0)
        {
            score += 2;
            reasons.Add(managedWorkspacePathPolicy.Summary);
            stopReason ??= ExecutionBoundaryStopReason.ScopeViolation;
            stopDetail = managedWorkspacePathPolicy.Summary;
        }

        if (telemetry.ChangeKinds.Contains(ExecutionChangeKind.ControlPlaneState)
            || telemetry.ChangeKinds.Contains(ExecutionChangeKind.Contracts))
        {
            score += 1;
            reasons.Add("Protected control-plane or contract files were touched.");
        }

        if (string.Equals(result.Validation.Build, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Validation.Tests, "failed", StringComparison.OrdinalIgnoreCase))
        {
            score += 1;
            reasons.Add("Validation failed during execution.");
        }

        var normalizedRisk = (double)touchedFiles / Math.Max(1, budget.MaxFiles)
                             + (double)telemetry.RetryCount / Math.Max(1, budget.MaxRetries)
                             + telemetry.FailureDensity;
        if ((telemetry.RetryCount >= 2 || telemetry.FailureCount >= 2) && normalizedRisk > 1.0)
        {
            reasons.Add($"Normalized boundary risk exceeded threshold ({normalizedRisk:0.00} > 1.00).");
            score += 1;
            stopReason ??= ExecutionBoundaryStopReason.RiskExceeded;
            stopDetail = $"Normalized risk {normalizedRisk:0.00} exceeded the enforcement threshold.";
        }

        var risk = score switch
        {
            <= 1 => ExecutionRiskLevel.Low,
            <= 3 => ExecutionRiskLevel.Medium,
            <= 5 => ExecutionRiskLevel.High,
            _ => ExecutionRiskLevel.Critical,
        };
        var shouldStop = stopReason is not null;
        var decision = shouldStop
            ? ExecutionBoundaryDecision.Stop
            : risk switch
            {
                ExecutionRiskLevel.High => ExecutionBoundaryDecision.Review,
                ExecutionRiskLevel.Critical => ExecutionBoundaryDecision.Block,
                _ => ExecutionBoundaryDecision.Allow,
            };
        if (reasons.Count == 0)
        {
            reasons.Add("Execution stayed within the declared budget.");
        }

        telemetry = telemetry with
        {
            BudgetExceeded = score > 0 || telemetry.BudgetExceeded,
            Summary = string.IsNullOrWhiteSpace(telemetry.Summary)
                ? string.Join(' ', reasons)
                : telemetry.Summary,
        };

        return new ExecutionBoundaryAssessment
        {
            Budget = budget,
            Confidence = confidence,
            Telemetry = telemetry,
            RiskLevel = risk,
            RiskScore = score,
            Decision = decision,
            ShouldStop = shouldStop,
            StopReason = stopReason,
            StopDetail = stopDetail,
            ManagedWorkspacePathPolicy = managedWorkspacePathPolicy,
            Reasons = reasons,
        };
    }

    public ExecutionBoundaryViolation CreateViolation(string taskId, ExecutionBoundaryAssessment assessment, ExecutionRun? run = null)
    {
        if (!assessment.ShouldStop || assessment.StopReason is null)
        {
            throw new InvalidOperationException("Cannot create a boundary violation when the assessment did not stop execution.");
        }

        return new ExecutionBoundaryViolation
        {
            TaskId = taskId,
            RunId = run?.RunId,
            StoppedAtStep = run is null ? 0 : Math.Clamp(run.CurrentStepIndex + 1, 0, Math.Max(0, run.Steps.Count)),
            TotalSteps = run?.Steps.Count ?? 0,
            Reason = assessment.StopReason.Value,
            Detail = assessment.StopDetail,
            RiskLevel = assessment.RiskLevel,
            RiskScore = assessment.RiskScore,
            Budget = assessment.Budget,
            Telemetry = assessment.Telemetry,
            Confidence = assessment.Confidence,
        };
    }

    private static int ParseInt(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed)
            ? parsed
            : 0;
    }

    private static bool HasScopeViolation(IReadOnlyList<string> declaredScope, IReadOnlyList<string> observedPaths)
    {
        if (declaredScope.Count == 0 || observedPaths.Count == 0)
        {
            return false;
        }

        return observedPaths.Any(path => declaredScope.All(scope => !PathMatchesScope(scope, path)));
    }

    private static bool PathMatchesScope(string scope, string observedPath)
    {
        var normalizedScope = scope.Replace('\\', '/').Trim();
        var normalizedPath = observedPath.Replace('\\', '/').Trim();
        if (string.Equals(normalizedScope, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedScope.EndsWith("/**", StringComparison.Ordinal))
        {
            var prefix = normalizedScope[..^3].TrimEnd('/');
            return normalizedPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedPath, prefix, StringComparison.OrdinalIgnoreCase);
        }

        var root = normalizedScope.TrimEnd('/');
        return normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }
}
