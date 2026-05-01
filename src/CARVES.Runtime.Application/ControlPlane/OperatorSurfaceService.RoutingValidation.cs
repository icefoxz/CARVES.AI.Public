using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectValidationSuite()
    {
        return OperatorSurfaceFormatter.RoutingValidationSuite(
            routingValidationService.LoadOrCreateCatalog(),
            routingValidationService.LoadLatestSummary());
    }

    public OperatorCommandResult InspectValidationTrace(string traceId)
    {
        return OperatorSurfaceFormatter.RoutingValidationTrace(routingValidationService.LoadTrace(traceId));
    }

    public OperatorCommandResult InspectValidationSummary(string? runId = null)
    {
        return OperatorSurfaceFormatter.RoutingValidationSummary(
            runId is null ? routingValidationService.LoadLatestSummary() : routingValidationService.SummarizeLatest(runId));
    }

    public OperatorCommandResult InspectValidationHistory(int? limit = null)
    {
        return OperatorSurfaceFormatter.RoutingValidationHistory(routingValidationService.LoadHistory(limit));
    }

    public OperatorCommandResult ApiValidationSuite()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(new
        {
            catalog = routingValidationService.LoadOrCreateCatalog(),
            latest_summary = routingValidationService.LoadLatestSummary(),
        }));
    }

    public OperatorCommandResult ApiValidationTrace(string traceId)
    {
        var trace = routingValidationService.LoadTrace(traceId);
        return OperatorCommandResult.Success(operatorApiService.ToJson(new
        {
            trace?.SchemaVersion,
            trace?.TraceId,
            trace?.RunId,
            trace?.TaskId,
            trace?.TaskType,
            trace?.RoutingIntent,
            trace?.ModuleId,
            execution_mode = trace?.ExecutionMode.ToString(),
            trace?.RoutingProfileId,
            trace?.RouteSource,
            trace?.SelectedProvider,
            trace?.SelectedLane,
            trace?.SelectedBackend,
            trace?.SelectedModel,
            trace?.SelectedRoutingProfileId,
            trace?.AppliedRoutingRuleId,
            trace?.CodexThreadId,
            codex_thread_continuity = trace?.CodexThreadContinuity.ToString(),
            trace?.FallbackConfigured,
            trace?.FallbackTriggered,
            preferred_route_eligibility = trace?.PreferredRouteEligibility?.ToString(),
            trace?.PreferredIneligibilityReason,
            trace?.SelectedBecause,
            trace?.StartedAt,
            trace?.EndedAt,
            trace?.LatencyMs,
            trace?.RequestSucceeded,
            trace?.TaskSucceeded,
            trace?.SchemaValid,
            build_outcome = trace?.BuildOutcome.ToString(),
            test_outcome = trace?.TestOutcome.ToString(),
            safety_outcome = trace?.SafetyOutcome.ToString(),
            trace?.RetryCount,
            trace?.PatchAccepted,
            trace?.PromptTokens,
            trace?.CompletionTokens,
            trace?.EstimatedCostUsd,
            trace?.FailureCategory,
            trace?.ReasonCodes,
            trace?.ArtifactPaths,
            candidates = trace?.Candidates.Select(candidate => new
            {
                candidate.BackendId,
                candidate.ProviderId,
                candidate.RoutingProfileId,
                candidate.RoutingRuleId,
                candidate.RouteDisposition,
                eligibility = candidate.Eligibility.ToString(),
                candidate.Selected,
                signals = new
                {
                    candidate.Signals.RouteHealth,
                    quota_state = candidate.Signals.QuotaState.ToString(),
                    candidate.Signals.TokenBudgetFit,
                    candidate.Signals.RecentLatencyMs,
                    candidate.Signals.RecentFailureCount,
                },
                candidate.Reason,
            }).ToArray(),
        }));
    }

    public OperatorCommandResult ApiValidationSummary(string? runId = null)
    {
        var summary = runId is null ? routingValidationService.LoadLatestSummary() : routingValidationService.SummarizeLatest(runId);
        return OperatorCommandResult.Success(operatorApiService.ToJson(summary));
    }

    public OperatorCommandResult ApiValidationHistory(int? limit = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(routingValidationService.LoadHistory(limit)));
    }

    public OperatorCommandResult RunValidationTask(string taskId, RoutingValidationMode mode, string? repoId = null)
    {
        var trace = routingValidationService.RunTask(taskId, mode, repoId);
        return OperatorSurfaceFormatter.RoutingValidationTrace(trace);
    }

    public OperatorCommandResult RunValidationSuite(RoutingValidationMode mode, int? limit = null, string? repoId = null)
    {
        var summary = routingValidationService.RunSuite(mode, limit, repoId);
        return OperatorSurfaceFormatter.RoutingValidationSummary(summary);
    }
}
