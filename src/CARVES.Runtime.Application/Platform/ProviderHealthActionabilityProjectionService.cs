using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ProviderHealthActionabilityProjectionService
{
    public ProviderHealthActionabilityProjection Build(
        IReadOnlyList<ProviderHealthRecord> providerHealth,
        WorkerSelectionDecision selection,
        string? preferredBackendId)
    {
        var classified = providerHealth
            .Select(record => Classify(record, selection, preferredBackendId))
            .ToArray();

        var issueSummaries = classified
            .Where(item => item.Record.State != WorkerBackendHealthState.Healthy)
            .OrderByDescending(item => item.Summary.ActionabilityRelevant)
            .ThenByDescending(item => item.Record.ConsecutiveFailureCount)
            .ThenBy(item => item.Record.BackendId, StringComparer.Ordinal)
            .Select(item => item.Summary)
            .ToArray();

        return new ProviderHealthActionabilityProjection
        {
            SelectedBackendId = selection.SelectedBackendId,
            PreferredBackendId = preferredBackendId,
            FallbackInUse = selection.UsedFallback,
            HealthyActionableProviderCount = classified.Count(item => item.ActionabilityRelevant && item.Record.State == WorkerBackendHealthState.Healthy),
            DegradedActionableProviderCount = classified.Count(item => item.ActionabilityRelevant && item.Record.State == WorkerBackendHealthState.Degraded),
            UnavailableActionableProviderCount = classified.Count(item => item.ActionabilityRelevant && item.Record.State is WorkerBackendHealthState.Unavailable or WorkerBackendHealthState.Disabled),
            OptionalIssueCount = classified.Count(item =>
                !item.ActionabilityRelevant
                && item.Record.State != WorkerBackendHealthState.Healthy
                && item.Record.State != WorkerBackendHealthState.Disabled),
            DisabledIssueCount = classified.Count(item =>
                !item.ActionabilityRelevant
                && item.Record.State == WorkerBackendHealthState.Disabled),
            Providers = issueSummaries,
        };
    }

    private static ClassifiedProviderHealth Classify(
        ProviderHealthRecord record,
        WorkerSelectionDecision selection,
        string? preferredBackendId)
    {
        var isSelected = !string.IsNullOrWhiteSpace(selection.SelectedBackendId)
            && string.Equals(selection.SelectedBackendId, record.BackendId, StringComparison.Ordinal);
        var isPreferred = !string.IsNullOrWhiteSpace(preferredBackendId)
            && string.Equals(preferredBackendId, record.BackendId, StringComparison.Ordinal);

        var selectionRole = DetermineSelectionRole(record, selection, isSelected, isPreferred);
        var actionabilityRelevant = isSelected || isPreferred;
        var impact = DetermineImpact(record.State, selectionRole, actionabilityRelevant);
        var recommendedNextAction = DetermineNextAction(record.State, selectionRole, actionabilityRelevant, selection.UsedFallback);

        return new ClassifiedProviderHealth(
            record,
            actionabilityRelevant,
            new OperationalProviderHealthSummary
            {
                BackendId = record.BackendId,
                ProviderId = record.ProviderId,
                State = record.State.ToString(),
                LatencyMs = record.LatencyMs,
                ConsecutiveFailureCount = record.ConsecutiveFailureCount,
                Summary = record.Summary,
                RecommendedNextAction = recommendedNextAction,
                SelectionRole = selectionRole,
                ActionabilityImpact = impact,
                ActionabilityRelevant = actionabilityRelevant,
            });
    }

    private static string DetermineSelectionRole(
        ProviderHealthRecord record,
        WorkerSelectionDecision selection,
        bool isSelected,
        bool isPreferred)
    {
        if (isSelected)
        {
            return selection.UsedFallback ? "fallback_selected" : "selected";
        }

        if (isPreferred)
        {
            return "preferred";
        }

        if (record.State == WorkerBackendHealthState.Disabled)
        {
            return "disabled";
        }

        return "optional";
    }

    private static string DetermineImpact(WorkerBackendHealthState state, string selectionRole, bool actionabilityRelevant)
    {
        if (actionabilityRelevant)
        {
            return state == WorkerBackendHealthState.Degraded
                ? "degraded_execution_lane"
                : "execution_lane_unavailable";
        }

        return selectionRole == "disabled"
            ? "disabled_placeholder"
            : "optional_residue";
    }

    private static string DetermineNextAction(
        WorkerBackendHealthState state,
        string selectionRole,
        bool actionabilityRelevant,
        bool fallbackInUse)
    {
        if (actionabilityRelevant)
        {
            if (selectionRole == "preferred" && fallbackInUse)
            {
                return "continue on the fallback backend and repair the preferred lane if failures continue";
            }

            return state == WorkerBackendHealthState.Degraded
                ? "monitor the selected execution lane and reroute only if failures continue"
                : "repair or reroute the required execution lane before autonomous execution continues";
        }

        return selectionRole == "disabled"
            ? "no immediate action unless this backend becomes part of the execution lane"
            : "no immediate action unless you intend to route work through this backend";
    }

    private sealed record ClassifiedProviderHealth(
        ProviderHealthRecord Record,
        bool ActionabilityRelevant,
        OperationalProviderHealthSummary Summary);
}
