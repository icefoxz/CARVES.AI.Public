using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RepoTruthProjectionService
{
    private static readonly TimeSpan ProjectionStaleAfter = TimeSpan.FromMinutes(15);

    private readonly IRepoRuntimeGateway runtimeGateway;

    public RepoTruthProjectionService(IRepoRuntimeGateway runtimeGateway)
    {
        this.runtimeGateway = runtimeGateway;
    }

    public RuntimeInstance Refresh(RepoDescriptor descriptor, RuntimeInstance instance)
    {
        var now = DateTimeOffset.UtcNow;
        var health = runtimeGateway.GetHealth(descriptor);
        instance.GatewayMode = health.Mode;
        instance.GatewayHealth = health.State;
        instance.GatewayReason = health.Reason;

        if (!health.Reachable)
        {
            instance.Projection = new RepoRuntimeProjection
            {
                TruthSource = ProjectionTruthSource.PlatformProjection,
                Freshness = instance.Projection.ProjectedAt.HasValue && now - instance.Projection.ProjectedAt.Value > ProjectionStaleAfter
                    ? ProjectionFreshness.Stale
                    : ProjectionFreshness.Unavailable,
                LastReconciliationOutcome = ProjectionReconciliationOutcome.MarkedUnavailable,
                RefreshRule = instance.Projection.RefreshRule,
                FreshnessReason = health.Reason,
                SummaryFingerprint = instance.Projection.SummaryFingerprint,
                ProjectedAt = instance.Projection.ProjectedAt,
                RepoObservedAt = instance.Projection.RepoObservedAt,
                LastReconciledAt = now,
                Stage = instance.Projection.Stage,
                SessionStatus = instance.Projection.SessionStatus,
                Actionability = instance.Projection.Actionability,
                OpenOpportunityCount = instance.Projection.OpenOpportunityCount,
                OpenTaskCount = instance.Projection.OpenTaskCount,
                ReviewTaskCount = instance.Projection.ReviewTaskCount,
                StopReason = instance.Projection.StopReason,
                WaitingReason = instance.Projection.WaitingReason,
                ActiveSessionId = instance.Projection.ActiveSessionId,
            };
            instance.Stage = string.IsNullOrWhiteSpace(instance.Stage) ? descriptor.Stage : instance.Stage;
            return instance;
        }

        var summary = runtimeGateway.LoadSummary(descriptor);
        var fingerprint = BuildFingerprint(summary);
        var driftDetected = !string.IsNullOrWhiteSpace(instance.Projection.SummaryFingerprint)
            && !string.Equals(instance.Projection.SummaryFingerprint, fingerprint, StringComparison.Ordinal);
        instance.ActiveSessionId = summary.ActiveSessionId;
        instance.Stage = summary.Stage;
        instance.Projection = new RepoRuntimeProjection
        {
            TruthSource = ProjectionTruthSource.PlatformProjection,
            Freshness = ProjectionFreshness.Fresh,
            LastReconciliationOutcome = driftDetected
                ? ProjectionReconciliationOutcome.ReconciledDrift
                : ProjectionReconciliationOutcome.RefreshedFromRepoTruth,
            RefreshRule = "refresh_on_list_or_inspect_and_reconcile_from_repo_truth",
            FreshnessReason = driftDetected
                ? "Platform summary drift was detected and reconciled from repo truth."
                : "Platform summary refreshed from repo truth.",
            SummaryFingerprint = fingerprint,
            ProjectedAt = now,
            RepoObservedAt = summary.ObservedAt,
            LastReconciledAt = now,
            Stage = summary.Stage,
            SessionStatus = summary.SessionStatus,
            Actionability = summary.Actionability,
            OpenOpportunityCount = summary.OpenOpportunityCount,
            OpenTaskCount = summary.OpenTaskCount,
            ReviewTaskCount = summary.ReviewTaskCount,
            StopReason = summary.StopReason,
            WaitingReason = summary.WaitingReason,
            ActiveSessionId = summary.ActiveSessionId,
        };
        return instance;
    }

    private static string BuildFingerprint(RepoRuntimeSummary summary)
    {
        return string.Join(
            "|",
            summary.Stage,
            summary.SessionStatus?.ToString() ?? "none",
            summary.Actionability,
            summary.OpenOpportunityCount,
            summary.OpenTaskCount,
            summary.ReviewTaskCount,
            summary.StopReason ?? string.Empty,
            summary.WaitingReason ?? string.Empty,
            summary.ActiveSessionId ?? string.Empty);
    }
}
