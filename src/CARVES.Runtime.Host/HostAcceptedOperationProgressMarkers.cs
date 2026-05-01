namespace Carves.Runtime.Host;

internal static class HostAcceptedOperationProgressMarkers
{
    public const string Accepted = "accepted";
    public const string Dispatching = "dispatching";
    public const string Running = "running";
    public const string Writeback = "writeback";
    public const string Completed = "completed";
    public const string Failed = "failed";

    public static int ResolveOrdinal(string? marker)
    {
        return marker?.Trim().ToLowerInvariant() switch
        {
            Dispatching => 1,
            Running => 2,
            Writeback => 3,
            Completed => 4,
            Failed => 4,
            _ => 0,
        };
    }
}
