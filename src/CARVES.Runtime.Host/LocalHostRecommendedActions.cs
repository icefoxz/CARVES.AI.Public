namespace Carves.Runtime.Host;

internal static class LocalHostRecommendedActions
{
    public const string None = "none";
    public const string EnsureHost = "ensure_host";
    public const string ReconcileStaleHost = "reconcile_stale_host";
    public const string RestartForSurfaceRegistry = "restart_for_surface_registry";
    public const string WaitForStartup = "wait_for_startup";
    public const string InspectHostStatus = "inspect_host_status";
    public const string ManualTerminateConflictingProcess = "manual_terminate_conflicting_process";
}
