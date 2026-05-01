using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed record LocalHostSurfaceHonestyProjection(
    string HostReadiness,
    string OperationalState,
    bool HostRunning,
    bool ConflictPresent,
    bool SafeToStartNewHost,
    bool PointerRepairApplied,
    string RecommendedActionKind,
    string RecommendedAction,
    LocalHostLifecycleProjection Lifecycle,
    string SummaryMessage,
    string SnapshotState,
    string? BaseUrl,
    int? ProcessId);

internal static class LocalHostSurfaceHonesty
{
    public static LocalHostSurfaceHonestyProjection Describe(HostDiscoveryResult discovery)
    {
        var pointerRepairApplied = discovery.HostRunning
            && discovery.Summary is not null
            && discovery.Message.Contains("Repaired active host descriptor", StringComparison.Ordinal);
        if (discovery.HostRunning && discovery.Summary is not null)
        {
            var commandSurfaceCompatibility = HostCommandSurfaceCatalog.Evaluate(discovery.Summary);
            if (!commandSurfaceCompatibility.Compatible)
            {
                return new LocalHostSurfaceHonestyProjection(
                    HostReadiness: "surface_registry_stale",
                    OperationalState: "host_restart_required",
                    HostRunning: true,
                    ConflictPresent: false,
                    SafeToStartNewHost: false,
                    PointerRepairApplied: pointerRepairApplied,
                    RecommendedActionKind: LocalHostRecommendedActions.RestartForSurfaceRegistry,
                    RecommendedAction: HostCommandSurfaceCatalog.RestartAction,
                    Lifecycle: LocalHostLifecycleProjection.FromReadiness(
                        "surface_registry_stale",
                        ready: false,
                        LocalHostRecommendedActions.RestartForSurfaceRegistry,
                        HostCommandSurfaceCatalog.RestartAction),
                    SummaryMessage: $"Resident host is running but its command surface is stale: {commandSurfaceCompatibility.Reason}.",
                    SnapshotState: discovery.Snapshot?.State.ToString().ToLowerInvariant() ?? "none",
                    BaseUrl: discovery.Summary.BaseUrl,
                    ProcessId: discovery.Descriptor?.ProcessId);
            }

            return new LocalHostSurfaceHonestyProjection(
                HostReadiness: pointerRepairApplied ? "healthy_with_pointer_repair" : "connected",
                OperationalState: pointerRepairApplied ? "healthy_with_pointer_repair" : "healthy",
                HostRunning: true,
                ConflictPresent: false,
                SafeToStartNewHost: false,
                PointerRepairApplied: pointerRepairApplied,
                RecommendedActionKind: LocalHostRecommendedActions.None,
                RecommendedAction: "host ready",
                Lifecycle: LocalHostLifecycleProjection.FromReadiness(
                    pointerRepairApplied ? "healthy_with_pointer_repair" : "connected",
                    ready: true,
                    LocalHostRecommendedActions.None,
                    "host ready"),
                SummaryMessage: discovery.Message,
                SnapshotState: discovery.Snapshot?.State.ToString().ToLowerInvariant() ?? "none",
                BaseUrl: discovery.Summary.BaseUrl,
                ProcessId: discovery.Descriptor?.ProcessId);
        }

        if (discovery.Descriptor is not null)
        {
            return new LocalHostSurfaceHonestyProjection(
                HostReadiness: "host_session_conflict",
                OperationalState: "host_session_conflict",
                HostRunning: false,
                ConflictPresent: true,
                SafeToStartNewHost: false,
                PointerRepairApplied: false,
                RecommendedActionKind: LocalHostRecommendedActions.ReconcileStaleHost,
                RecommendedAction: "carves host reconcile --replace-stale --json",
                Lifecycle: LocalHostLifecycleProjection.FromReadiness(
                    "host_session_conflict",
                    ready: false,
                    LocalHostRecommendedActions.ReconcileStaleHost,
                    "carves host reconcile --replace-stale --json"),
                SummaryMessage: discovery.Message,
                SnapshotState: discovery.Snapshot?.State.ToString().ToLowerInvariant() ?? "none",
                BaseUrl: discovery.Descriptor.BaseUrl,
                ProcessId: discovery.Descriptor.ProcessId);
        }

        var operationalState = discovery.Snapshot?.State switch
        {
            HostRuntimeSnapshotState.Stopped => "stopped_snapshot",
            HostRuntimeSnapshotState.Stale => "stale_snapshot",
            HostRuntimeSnapshotState.Live => "live_snapshot_without_responder",
            _ => "not_running",
        };

        return new LocalHostSurfaceHonestyProjection(
            HostReadiness: "not_running",
            OperationalState: operationalState,
            HostRunning: false,
            ConflictPresent: false,
            SafeToStartNewHost: true,
            PointerRepairApplied: false,
            RecommendedActionKind: LocalHostRecommendedActions.EnsureHost,
            RecommendedAction: "carves host ensure --json",
            Lifecycle: LocalHostLifecycleProjection.FromReadiness(
                "not_running",
                ready: false,
                LocalHostRecommendedActions.EnsureHost,
                "carves host ensure --json"),
            SummaryMessage: discovery.Message,
            SnapshotState: discovery.Snapshot?.State.ToString().ToLowerInvariant() ?? "none",
            BaseUrl: discovery.Snapshot?.BaseUrl,
            ProcessId: discovery.Snapshot?.ProcessId);
    }
}
