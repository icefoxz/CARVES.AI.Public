using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform;

public sealed class OperatorActionabilityService
{
    public OperatorActionabilityAssessment Assess(
        RuntimeSessionState? session,
        int pendingApprovalCount,
        int reviewTaskCount,
        int blockedTaskCount,
        int activeIncidentCount,
        int nonBlockingNoiseCount,
        ProviderHealthActionabilityProjection providerHealth)
    {
        var healthyProviderCount = providerHealth.HealthyActionableProviderCount;
        var degradedProviderCount = providerHealth.DegradedActionableProviderCount;
        var unavailableProviderCount = providerHealth.UnavailableActionableProviderCount;
        var sessionActionability = session?.CurrentActionability ?? RuntimeActionability.None;
        var selectedIssue = providerHealth.Providers.FirstOrDefault(item => item.SelectionRole is "selected" or "fallback_selected");
        var preferredIssue = providerHealth.Providers.FirstOrDefault(item => item.SelectionRole == "preferred");

        if (pendingApprovalCount > 0)
        {
            return Build(
                OperatorActionabilityState.HumanRequired,
                OperatorActionabilityReason.PendingApproval,
                sessionActionability,
                "Pending permission requests require an explicit operator decision.",
                "approve, deny, or timeout pending worker permission requests",
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        if (reviewTaskCount > 0 || session?.Status == RuntimeSessionStatus.ReviewWait)
        {
            return Build(
                OperatorActionabilityState.HumanRequired,
                OperatorActionabilityReason.PendingReview,
                sessionActionability,
                "Review work is waiting for an operator decision before progress can continue.",
                "approve or reject pending review work",
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        if (blockedTaskCount > 0 || activeIncidentCount > 0)
        {
            return Build(
                OperatorActionabilityState.Blocked,
                OperatorActionabilityReason.ActiveBlocker,
                sessionActionability,
                "Active blockers are preventing autonomous progress.",
                "inspect active blockers and recovery recommendations",
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        if (session is not null && session.Status is RuntimeSessionStatus.Paused or RuntimeSessionStatus.Failed or RuntimeSessionStatus.Stopped)
        {
            return Build(
                OperatorActionabilityState.HumanRequired,
                session.Status == RuntimeSessionStatus.Stopped || session.Status == RuntimeSessionStatus.Failed
                    ? OperatorActionabilityReason.SessionTerminal
                    : OperatorActionabilityReason.SessionPause,
                sessionActionability,
                "The current session is paused or terminal and requires an operator decision before work resumes.",
                RuntimeActionabilitySemantics.DescribeNextAction(session),
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        if (unavailableProviderCount > 0 && healthyProviderCount == 0 && degradedProviderCount == 0)
        {
            return Build(
                OperatorActionabilityState.HumanRequired,
                OperatorActionabilityReason.ProviderUnavailable,
                sessionActionability,
                "All worker backends are unavailable; autonomous execution cannot continue safely.",
                "repair or reroute worker backends before resuming autonomous execution",
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        if (providerHealth.FallbackInUse && preferredIssue is not null)
        {
            return Build(
                OperatorActionabilityState.AutonomousDegraded,
                OperatorActionabilityReason.ProviderUnavailable,
                sessionActionability,
                "The preferred execution lane is unavailable, but a fallback backend remains usable for autonomous work.",
                "continue on the fallback backend and repair the preferred lane if failures continue",
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        if (selectedIssue is not null && selectedIssue.ActionabilityImpact == "degraded_execution_lane")
        {
            return Build(
                OperatorActionabilityState.AutonomousDegraded,
                OperatorActionabilityReason.ProviderDegraded,
                sessionActionability,
                "The selected execution lane is degraded, but autonomous work can continue.",
                "monitor the selected execution lane and reroute only if failures continue",
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        if (providerHealth.OptionalIssueCount > 0 || providerHealth.DisabledIssueCount > 0)
        {
            return Build(
                OperatorActionabilityState.AdvisoryOnly,
                OperatorActionabilityReason.ProviderOptionalResidue,
                sessionActionability,
                "Optional or disabled backends are unhealthy, but the active execution lane remains usable.",
                "no immediate action; configure or repair optional backends only if you plan to use them",
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        if (nonBlockingNoiseCount > 0)
        {
            return Build(
                OperatorActionabilityState.AdvisoryOnly,
                OperatorActionabilityReason.NonBlockingNoise,
                sessionActionability,
                "Only non-blocking runtime residue remains; no immediate operator intervention is required.",
                "use audit runtime-noise for cleanup planning",
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        if (session is null)
        {
            return Build(
                OperatorActionabilityState.AdvisoryOnly,
                OperatorActionabilityReason.NoSession,
                sessionActionability,
                "No active runtime session is attached.",
                "start a runtime session or inspect the current repo state",
                healthyProviderCount,
                degradedProviderCount,
                unavailableProviderCount,
                providerHealth);
        }

        return Build(
            OperatorActionabilityState.AdvisoryOnly,
            OperatorActionabilityReason.AutonomousExecution,
            sessionActionability,
            "No immediate operator intervention is required; runtime execution remains autonomous.",
            $"no immediate operator action; {RuntimeActionabilitySemantics.DescribeNextAction(session)}",
            healthyProviderCount,
            degradedProviderCount,
            unavailableProviderCount,
            providerHealth);
    }

    public static string DescribeState(OperatorActionabilityState state)
    {
        return state switch
        {
            OperatorActionabilityState.AutonomousDegraded => "autonomous_degraded",
            OperatorActionabilityState.HumanRequired => "human_required",
            OperatorActionabilityState.Blocked => "blocked",
            _ => "advisory_only",
        };
    }

    public static string DescribeReason(OperatorActionabilityReason reason)
    {
        return reason switch
        {
            OperatorActionabilityReason.PendingApproval => "pending_approval",
            OperatorActionabilityReason.PendingReview => "pending_review",
            OperatorActionabilityReason.ActiveBlocker => "active_blocker",
            OperatorActionabilityReason.ProviderUnavailable => "provider_unavailable",
            OperatorActionabilityReason.ProviderDegraded => "provider_degraded",
            OperatorActionabilityReason.SessionPause => "session_pause",
            OperatorActionabilityReason.SessionTerminal => "session_terminal",
            OperatorActionabilityReason.NonBlockingNoise => "non_blocking_noise",
            OperatorActionabilityReason.AutonomousExecution => "autonomous_execution",
            OperatorActionabilityReason.NoSession => "no_session",
            OperatorActionabilityReason.ProviderOptionalResidue => "provider_optional_residue",
            _ => "none",
        };
    }

    private static OperatorActionabilityAssessment Build(
        OperatorActionabilityState state,
        OperatorActionabilityReason reason,
        RuntimeActionability sessionActionability,
        string summary,
        string recommendedNextAction,
        int healthyProviderCount,
        int degradedProviderCount,
        int unavailableProviderCount,
        ProviderHealthActionabilityProjection providerHealth)
    {
        return new OperatorActionabilityAssessment
        {
            State = state,
            Reason = reason,
            SessionActionability = sessionActionability,
            Summary = summary,
            RecommendedNextAction = recommendedNextAction,
            HealthyProviderCount = healthyProviderCount,
            DegradedProviderCount = degradedProviderCount,
            UnavailableProviderCount = unavailableProviderCount,
            ActionableProviderIssueCount = providerHealth.Providers.Count(item => item.ActionabilityRelevant),
            OptionalProviderIssueCount = providerHealth.OptionalIssueCount,
            DisabledProviderCount = providerHealth.DisabledIssueCount,
            SelectedBackendId = providerHealth.SelectedBackendId,
            PreferredBackendId = providerHealth.PreferredBackendId,
            FallbackInUse = providerHealth.FallbackInUse,
        };
    }
}
