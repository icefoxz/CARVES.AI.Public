using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RoutingValidationService
{
    public RoutingValidationHistory LoadHistory(int? limit = null)
    {
        var batches = repository.LoadSummaries(limit)
            .OrderByDescending(summary => summary.GeneratedAt)
            .ToArray();
        return new RoutingValidationHistory
        {
            BatchCount = batches.Length,
            LatestRunId = batches.FirstOrDefault()?.RunId,
            Batches = batches,
        };
    }

    public RoutingValidationTrace RunTask(string taskId, RoutingValidationMode mode, string? repoId = null, string? runId = null)
    {
        var catalog = LoadOrCreateCatalog();
        var validationTask = catalog.Tasks.FirstOrDefault(item => string.Equals(item.TaskId, taskId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Validation task '{taskId}' was not found.");
        var matrix = currentModelQualificationService.LoadOrCreateMatrix();
        var resolvedRunId = runId ?? $"val-run-{Guid.NewGuid():N}";
        var activeProfile = runtimeRoutingProfileService.LoadActive();
        var activeProfileId = activeProfile?.ProfileId;

        WorkerSelectionDecision? selection = null;
        ValidationRouteResolution routeResolution;
        if (mode == RoutingValidationMode.Baseline)
        {
            var lane = matrix.Lanes.FirstOrDefault(item => string.Equals(item.LaneId, validationTask.BaselineLaneId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Baseline lane '{validationTask.BaselineLaneId}' was not found in the current qualification matrix.");
            routeResolution = new ValidationRouteResolution(
                lane,
                "validation_baseline",
                false,
                false,
                null,
                null,
                ["baseline_fixed_lane"],
                null,
                lane.RoutingProfileId,
                []);
        }
        else
        {
            var routingProfileMatch = runtimeRoutingProfileService.Resolve(validationTask.RoutingIntent, validationTask.ModuleId);
            selection = workerSelectionPolicyService.Evaluate(
                BuildTaskNode(validationTask),
                repoId,
                allowFallback: true,
                mode == RoutingValidationMode.ForcedFallback
                    ? new WorkerSelectionOptions { ForceFallbackOnly = true }
                    : null);
            routeResolution = ResolveRoute(matrix, validationTask, mode, selection, routingProfileMatch);
            if (routeResolution.Lane is null)
            {
                var failedTrace = BuildSelectionFailureTrace(validationTask, mode, resolvedRunId, activeProfileId, selection, routeResolution);
                repository.SaveTrace(failedTrace);
                SaveLatestSummary(resolvedRunId);
                return failedTrace;
            }
        }

        var qualificationCase = new ModelQualificationCase
        {
            CaseId = validationTask.TaskId,
            RoutingIntent = validationTask.RoutingIntent,
            ModuleId = validationTask.ModuleId,
            Prompt = validationTask.Prompt,
            ExpectedFormat = validationTask.ExpectedFormat,
            RequiredJsonFields = validationTask.RequiredJsonFields,
            Summary = validationTask.Summary,
        };
        var result = laneExecutor.Execute(routeResolution.Lane!, qualificationCase, 1);
        var output = result.ResponsePreview ?? result.Rationale ?? result.Summary ?? string.Empty;
        var schemaValid = EvaluateFormat(validationTask, output);
        var executionOutcomes = DetermineExecutionOutcomes(validationTask, result, output, schemaValid);
        var trace = new RoutingValidationTrace
        {
            RunId = resolvedRunId,
            TaskId = validationTask.TaskId,
            TaskType = validationTask.TaskType,
            RoutingIntent = validationTask.RoutingIntent,
            ModuleId = validationTask.ModuleId,
            ExecutionMode = mode,
            RoutingProfileId = activeProfileId,
            RouteSource = routeResolution.RouteSource,
            SelectedProvider = routeResolution.Lane!.ProviderId,
            SelectedLane = routeResolution.Lane.LaneId,
            SelectedBackend = routeResolution.Lane.BackendId,
            SelectedModel = routeResolution.Lane.Model,
            SelectedRoutingProfileId = routeResolution.SelectedRoutingProfileId ?? routeResolution.Lane.RoutingProfileId,
            AppliedRoutingRuleId = routeResolution.AppliedRoutingRuleId,
            CodexThreadId = result.ThreadId,
            CodexThreadContinuity = result.ThreadContinuity,
            FallbackConfigured = routeResolution.FallbackConfigured,
            FallbackTriggered = routeResolution.FallbackTriggered,
            PreferredRouteEligibility = routeResolution.PreferredRouteEligibility,
            PreferredIneligibilityReason = routeResolution.PreferredIneligibilityReason,
            SelectedBecause = routeResolution.SelectedBecause,
            StartedAt = result.StartedAt,
            EndedAt = result.CompletedAt,
            LatencyMs = result.ProviderLatencyMs ?? (long)Math.Max(0, (result.CompletedAt - result.StartedAt).TotalMilliseconds),
            RequestSucceeded = result.Succeeded,
            TaskSucceeded = result.Succeeded,
            SchemaValid = schemaValid,
            BuildOutcome = executionOutcomes.BuildOutcome,
            TestOutcome = executionOutcomes.TestOutcome,
            SafetyOutcome = executionOutcomes.SafetyOutcome,
            RetryCount = 0,
            PatchAccepted = executionOutcomes.SafetyOutcome == RoutingValidationExecutionOutcome.Passed && result.Succeeded,
            PromptTokens = result.InputTokens,
            CompletionTokens = result.OutputTokens,
            EstimatedCostUsd = EstimateCostUsd(routeResolution.Lane, result.InputTokens, result.OutputTokens),
            FailureCategory = result.Succeeded ? null : WorkerFailureSemantics.Classify(result).ReasonCode,
            ReasonCodes = BuildReasonCodes(selection, result),
            ArtifactPaths = [],
            Candidates = routeResolution.Candidates,
        };

        repository.SaveTrace(trace);
        SaveLatestSummary(resolvedRunId);
        return trace;
    }

    public RoutingValidationSummary RunSuite(RoutingValidationMode mode, int? limit = null, string? repoId = null)
    {
        var catalog = LoadOrCreateCatalog();
        var runId = $"val-run-{Guid.NewGuid():N}";
        foreach (var task in catalog.Tasks.Take(limit ?? catalog.Tasks.Length))
        {
            RunTask(task.TaskId, mode, repoId, runId);
        }

        return SaveLatestSummary(runId);
    }

    public RoutingValidationSummary SummarizeLatest(string? runId = null)
    {
        var resolvedRunId = runId
            ?? repository.LoadLatestSummary()?.RunId
            ?? repository.LoadTraces().LastOrDefault()?.RunId
            ?? throw new InvalidOperationException("No validation traces exist.");
        return SaveLatestSummary(resolvedRunId);
    }

    private RoutingValidationSummary SaveLatestSummary(string runId)
    {
        var traces = repository.LoadTraces(runId);
        var summary = new RoutingValidationSummary
        {
            RunId = runId,
            RoutingProfileId = traces.Select(item => item.RoutingProfileId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            ExecutionMode = traces.Select(item => item.ExecutionMode).Distinct().FirstOrDefault(),
            Tasks = traces.Count,
            SuccessRate = traces.Count == 0 ? 0 : traces.Count(item => item.TaskSucceeded) / (double)traces.Count,
            SchemaValidityRate = traces.Count == 0 ? 0 : traces.Count(item => item.SchemaValid) / (double)traces.Count,
            FallbackRate = traces.Count == 0 ? 0 : traces.Count(item => item.FallbackTriggered) / (double)traces.Count,
            BuildPassRate = CalculateOutcomeRate(traces, static item => item.BuildOutcome),
            TestPassRate = CalculateOutcomeRate(traces, static item => item.TestOutcome),
            SafetyPassRate = CalculateOutcomeRate(traces, static item => item.SafetyOutcome),
            AverageLatencyMs = traces.Count == 0 ? 0 : traces.Average(item => item.LatencyMs),
            TotalEstimatedCostUsd = traces.Sum(item => item.EstimatedCostUsd ?? 0m),
            RouteBreakdown = BuildRouteBreakdown(traces),
        };
        repository.SaveSummary(summary);
        repository.SaveLatestSummary(summary);
        return summary;
    }
}
