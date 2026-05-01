using System.Text.Json;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RoutingValidationService
{
    private ValidationRouteResolution ResolveRoute(
        ModelQualificationMatrix matrix,
        RoutingValidationTaskDefinition validationTask,
        RoutingValidationMode mode,
        WorkerSelectionDecision? selection,
        RuntimeRoutingRuleMatch? routingProfileMatch)
    {
        if (selection is not null)
        {
            var selectionLane = ResolveLane(matrix, selection);
            if (selectionLane is not null)
            {
                return new ValidationRouteResolution(
                    selectionLane,
                    selection.RouteSource,
                    selection.Candidates.Any(candidate => string.Equals(candidate.RouteDisposition, "fallback", StringComparison.Ordinal)),
                    selection.UsedFallback || mode == RoutingValidationMode.ForcedFallback,
                    selection.PreferredRouteEligibility,
                    selection.PreferredIneligibilityReason,
                    selection.SelectedBecause.ToArray(),
                    selection.AppliedRoutingRuleId,
                    selection.SelectedRoutingProfileId,
                    selection.Candidates.Select(ToSnapshot).ToArray());
            }
        }

        if (routingProfileMatch is null)
        {
            return ValidationRouteResolution.None;
        }

        var targetRoute = mode == RoutingValidationMode.ForcedFallback
            ? routingProfileMatch.FallbackRoutes.FirstOrDefault()
            : routingProfileMatch.PreferredRoute;
        if (targetRoute is null)
        {
            return new ValidationRouteResolution(
                null,
                mode == RoutingValidationMode.ForcedFallback ? "active_profile_fallback" : "active_profile_preferred",
                routingProfileMatch.FallbackRoutes.Count > 0,
                mode == RoutingValidationMode.ForcedFallback,
                mode == RoutingValidationMode.ForcedFallback ? RouteEligibilityStatus.TemporarilyIneligible : RouteEligibilityStatus.Unsupported,
                mode == RoutingValidationMode.ForcedFallback
                    ? "forced fallback requested for validation, but no fallback route was configured"
                    : "active routing profile did not yield an executable preferred route",
                mode == RoutingValidationMode.ForcedFallback ? ["fallback_route_selected"] : ["preferred_route_eligible"],
                routingProfileMatch.Rule.RuleId,
                null,
                BuildCandidateSnapshots(matrix, routingProfileMatch, selectedLaneId: null, mode));
        }

        var lane = ResolveLane(matrix, targetRoute);
        return new ValidationRouteResolution(
            lane,
            mode == RoutingValidationMode.ForcedFallback ? "active_profile_fallback" : "active_profile_preferred",
            routingProfileMatch.FallbackRoutes.Count > 0,
            mode == RoutingValidationMode.ForcedFallback,
            mode == RoutingValidationMode.ForcedFallback ? RouteEligibilityStatus.TemporarilyIneligible : RouteEligibilityStatus.Eligible,
            mode == RoutingValidationMode.ForcedFallback ? "forced fallback requested for validation" : null,
            mode == RoutingValidationMode.ForcedFallback ? ["fallback_route_selected"] : ["preferred_route_eligible"],
            routingProfileMatch.Rule.RuleId,
            targetRoute.RoutingProfileId,
            BuildCandidateSnapshots(matrix, routingProfileMatch, lane?.LaneId, mode));
    }

    private RoutingValidationCandidateSnapshot[] BuildCandidateSnapshots(
        ModelQualificationMatrix matrix,
        RuntimeRoutingRuleMatch routingProfileMatch,
        string? selectedLaneId,
        RoutingValidationMode mode)
    {
        var latestRun = currentModelQualificationService.LoadLatestRun();
        var routeCandidates = new List<(RuntimeRoutingRoute Route, string Disposition)>
        {
            (routingProfileMatch.PreferredRoute, "preferred"),
        };
        routeCandidates.AddRange(routingProfileMatch.FallbackRoutes.Select(route => (route, "fallback")));

        return routeCandidates
            .Select(item =>
            {
                var lane = ResolveLane(matrix, item.Route);
                var laneResults = lane is null || latestRun is null
                    ? []
                    : latestRun.Results.Where(result => string.Equals(result.LaneId, lane.LaneId, StringComparison.Ordinal)).ToArray();
                var recentLatency = laneResults.Where(result => result.LatencyMs.HasValue).Select(result => result.LatencyMs!.Value).DefaultIfEmpty().LastOrDefault();
                var recentFailures = laneResults.Count(result => !result.Success);
                var anySuccess = laneResults.Any(result => result.Success);
                var eligibility = item.Disposition == "preferred" && mode == RoutingValidationMode.ForcedFallback
                    ? RouteEligibilityStatus.TemporarilyIneligible
                    : lane is null
                        ? RouteEligibilityStatus.Unsupported
                        : anySuccess || laneResults.Length == 0
                            ? RouteEligibilityStatus.Eligible
                            : RouteEligibilityStatus.TemporarilyIneligible;
                return new RoutingValidationCandidateSnapshot
                {
                    BackendId = item.Route.BackendId ?? lane?.BackendId ?? "(none)",
                    ProviderId = item.Route.ProviderId,
                    RoutingProfileId = item.Route.RoutingProfileId,
                    RoutingRuleId = routingProfileMatch.Rule.RuleId,
                    RouteDisposition = item.Disposition,
                    Eligibility = eligibility,
                    Selected = lane is not null && string.Equals(lane.LaneId, selectedLaneId, StringComparison.Ordinal),
                    Signals = new RouteSelectionSignals
                    {
                        RouteHealth = recentFailures > 0 && !anySuccess ? "Unavailable" : "Healthy",
                        QuotaState = RouteQuotaState.Unknown,
                        TokenBudgetFit = true,
                        RecentLatencyMs = recentLatency == 0 ? null : recentLatency,
                        RecentFailureCount = recentFailures,
                    },
                    Reason = item.Disposition == "preferred" && mode == RoutingValidationMode.ForcedFallback
                        ? "preferred route withheld because forced fallback mode was requested"
                        : lane is null
                            ? "no qualification lane matched this route"
                            : $"matched {item.Disposition} route via rule '{routingProfileMatch.Rule.RuleId}'",
                };
            })
            .ToArray();
    }

    private static RoutingValidationCandidateSnapshot ToSnapshot(WorkerSelectionCandidate candidate)
    {
        return new RoutingValidationCandidateSnapshot
        {
            BackendId = candidate.BackendId,
            ProviderId = candidate.ProviderId,
            RoutingProfileId = candidate.RoutingProfileId,
            RoutingRuleId = candidate.RoutingRuleId,
            RouteDisposition = candidate.RouteDisposition,
            Eligibility = candidate.Eligibility,
            Selected = candidate.Selected,
            Signals = candidate.Signals,
            Reason = candidate.Reason,
        };
    }

    private static ModelQualificationLane? ResolveLane(ModelQualificationMatrix matrix, RuntimeRoutingRoute route)
    {
        return matrix.Lanes.FirstOrDefault(item =>
                   string.Equals(item.ProviderId, route.ProviderId, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(item.BackendId, route.BackendId, StringComparison.OrdinalIgnoreCase)
                   && (string.IsNullOrWhiteSpace(route.RequestFamily)
                       || string.Equals(item.RequestFamily, route.RequestFamily, StringComparison.OrdinalIgnoreCase))
                   && string.Equals(item.Model, route.Model, StringComparison.Ordinal))
               ?? matrix.Lanes.FirstOrDefault(item =>
                   string.Equals(item.ProviderId, route.ProviderId, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(item.BackendId, route.BackendId, StringComparison.OrdinalIgnoreCase)
                   && (string.IsNullOrWhiteSpace(route.RequestFamily)
                       || string.Equals(item.RequestFamily, route.RequestFamily, StringComparison.OrdinalIgnoreCase))
                   && string.Equals(item.RoutingProfileId, route.RoutingProfileId, StringComparison.Ordinal));
    }

    private static double CalculateOutcomeRate(
        IReadOnlyList<RoutingValidationTrace> traces,
        Func<RoutingValidationTrace, RoutingValidationExecutionOutcome> selector)
    {
        var applicable = traces.Where(trace => selector(trace) != RoutingValidationExecutionOutcome.NotRun).ToArray();
        if (applicable.Length == 0)
        {
            return 0;
        }

        return applicable.Count(trace => selector(trace) == RoutingValidationExecutionOutcome.Passed) / (double)applicable.Length;
    }

    private static RoutingValidationRouteBreakdown[] BuildRouteBreakdown(IReadOnlyList<RoutingValidationTrace> traces)
    {
        return traces
            .GroupBy(trace => new
            {
                trace.TaskType,
                ProviderId = trace.SelectedProvider ?? "(none)",
                BackendId = trace.SelectedBackend ?? "(none)",
                trace.SelectedLane,
                trace.SelectedModel,
            })
            .OrderBy(group => group.Key.TaskType, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ProviderId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.BackendId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.SelectedLane, StringComparer.Ordinal)
            .Select(group =>
            {
                var items = group.ToArray();
                return new RoutingValidationRouteBreakdown
                {
                    TaskFamily = group.Key.TaskType,
                    ProviderId = group.Key.ProviderId,
                    BackendId = group.Key.BackendId,
                    SelectedLane = group.Key.SelectedLane,
                    SelectedModel = group.Key.SelectedModel,
                    Samples = items.Length,
                    SuccessRate = items.Count(item => item.TaskSucceeded) / (double)items.Length,
                    PatchAcceptanceRate = items.Count(item => item.PatchAccepted) / (double)items.Length,
                    AverageRetryCount = items.Average(item => item.RetryCount),
                    AverageLatencyMs = items.Average(item => (double)item.LatencyMs),
                };
            })
            .ToArray();
    }

    private static ValidationExecutionOutcomes DetermineExecutionOutcomes(
        RoutingValidationTaskDefinition validationTask,
        WorkerExecutionResult result,
        string output,
        bool schemaValid)
    {
        if (!IsVerySmallCodeTask(validationTask.TaskType))
        {
            return ValidationExecutionOutcomes.NotRun;
        }

        if (!result.Succeeded)
        {
            return new ValidationExecutionOutcomes(
                RoutingValidationExecutionOutcome.Failed,
                RoutingValidationExecutionOutcome.Failed,
                RoutingValidationExecutionOutcome.Rejected);
        }

        if (!schemaValid)
        {
            return new ValidationExecutionOutcomes(
                RoutingValidationExecutionOutcome.Failed,
                RoutingValidationExecutionOutcome.Failed,
                RoutingValidationExecutionOutcome.Failed);
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            var filesTouched = document.RootElement.TryGetProperty("files_touched", out var filesElement) && filesElement.ValueKind == JsonValueKind.Array
                ? filesElement.EnumerateArray().Count()
                : 0;
            var validationCommands = document.RootElement.TryGetProperty("validation_commands", out var commandsElement) && commandsElement.ValueKind == JsonValueKind.Array
                ? commandsElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
                : [];

            return new ValidationExecutionOutcomes(
                validationCommands.Any(command => command.Contains("build", StringComparison.OrdinalIgnoreCase))
                    ? RoutingValidationExecutionOutcome.Passed
                    : RoutingValidationExecutionOutcome.Failed,
                validationCommands.Any(command => command.Contains("test", StringComparison.OrdinalIgnoreCase))
                    ? RoutingValidationExecutionOutcome.Passed
                    : RoutingValidationExecutionOutcome.Failed,
                filesTouched is > 0 and <= 2
                    ? RoutingValidationExecutionOutcome.Passed
                    : RoutingValidationExecutionOutcome.Failed);
        }
        catch (JsonException)
        {
            return new ValidationExecutionOutcomes(
                RoutingValidationExecutionOutcome.Failed,
                RoutingValidationExecutionOutcome.Failed,
                RoutingValidationExecutionOutcome.Failed);
        }
    }

    private static bool IsVerySmallCodeTask(string taskType)
    {
        return string.Equals(taskType, "very-small-code-task", StringComparison.Ordinal)
            || taskType.StartsWith("code.small.", StringComparison.Ordinal);
    }
}
