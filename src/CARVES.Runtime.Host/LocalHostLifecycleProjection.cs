namespace Carves.Runtime.Host;

internal sealed record LocalHostLifecycleProjection(
    string State,
    string Reason,
    string ActionKind,
    string Action,
    bool Ready,
    bool BlocksAutomation)
{
    public static LocalHostLifecycleProjection FromReadiness(
        string readiness,
        bool ready,
        string actionKind,
        string action)
    {
        var state = ready
            ? "ready"
            : readiness switch
            {
                "not_running" => "recoverable",
                "host_start_in_progress" => "waiting",
                "host_session_conflict" => "blocked",
                "surface_registry_stale" => "blocked",
                "incompatible" => "blocked",
                _ => "blocked",
            };

        return new LocalHostLifecycleProjection(
            state,
            readiness,
            actionKind,
            action,
            Ready: ready,
            BlocksAutomation: !ready);
    }
}
