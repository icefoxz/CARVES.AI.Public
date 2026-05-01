namespace Carves.Runtime.Host;

internal sealed record HostRehydrationSummary(
    bool Rehydrated,
    int PendingApprovalCount,
    int ActorSessionCount,
    int RecentIncidentCount,
    int RecentOperatorEventCount,
    int StaleMarkerCount,
    int PausedRuntimeCount,
    IReadOnlyList<string> CleanupActions,
    string Summary);
