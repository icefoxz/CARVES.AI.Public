using System.Text.Json.Nodes;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static FriendlyHostProjection ResolveFriendlyHostProjection(string repoRoot, string? requiredCapability = null)
    {
        var arguments = new List<string> { "host", "status", "--json" };
        if (!string.IsNullOrWhiteSpace(requiredCapability))
        {
            arguments.Add("--require-capability");
            arguments.Add(requiredCapability);
        }

        var hostStatus = HostProgramInvoker.Invoke(repoRoot, arguments.ToArray());
        if (TryReadHostSurfaceProjection(hostStatus, out var projection))
        {
            return projection;
        }

        var hostReadiness = hostStatus.ExitCode == 0 ? "connected" : "not_running";
        return new FriendlyHostProjection(
            HostReadiness: hostReadiness,
            HostOperationalState: hostReadiness == "connected" ? "healthy" : "not_running",
            ConflictPresent: false,
            SafeToStartNewHost: !string.Equals(hostReadiness, "connected", StringComparison.Ordinal),
            RecommendedActionKind: hostReadiness == "connected" ? "none" : "ensure_host",
            RecommendedAction: hostReadiness == "connected" ? "host ready" : "carves host ensure --json",
            LifecycleState: hostReadiness == "connected" ? "ready" : "recoverable",
            LifecycleReason: hostReadiness,
            Message: string.IsNullOrWhiteSpace(hostStatus.CombinedOutput)
                ? (hostReadiness == "connected" ? "Resident host is running." : "No resident host is running.")
                : hostStatus.CombinedOutput.Trim());
    }

    private static bool TryReadHostSurfaceProjection(
        HostProgramInvoker.HostInvocationResult hostStatus,
        out FriendlyHostProjection projection)
    {
        projection = null!;
        var json = string.IsNullOrWhiteSpace(hostStatus.StandardOutput)
            ? hostStatus.StandardError
            : hostStatus.StandardOutput;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var node = JsonNode.Parse(json)?.AsObject();
            if (node is null)
            {
                return false;
            }

            var lifecycle = node["lifecycle"]?.AsObject();
            projection = new FriendlyHostProjection(
                HostReadiness: node["host_readiness"]?.GetValue<string>() ?? "not_running",
                HostOperationalState: node["host_operational_state"]?.GetValue<string>() ?? "not_running",
                ConflictPresent: node["conflict_present"]?.GetValue<bool>() ?? false,
                SafeToStartNewHost: node["safe_to_start_new_host"]?.GetValue<bool>() ?? false,
                RecommendedActionKind: node["recommended_action_kind"]?.GetValue<string>() ?? string.Empty,
                RecommendedAction: node["recommended_action"]?.GetValue<string>() ?? string.Empty,
                LifecycleState: lifecycle?["state"]?.GetValue<string>() ?? string.Empty,
                LifecycleReason: lifecycle?["reason"]?.GetValue<string>() ?? string.Empty,
                Message: node["message"]?.GetValue<string>() ?? string.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeFriendlyHostLabel(FriendlyHostProjection projection)
    {
        if (projection.HostRunning)
        {
            return "connected";
        }

        return projection.ConflictPresent ? "session conflict" : "not running";
    }

    private static string ResolveFriendlyHostNextAction(FriendlyHostProjection projection, string fallback)
    {
        return string.IsNullOrWhiteSpace(projection.RecommendedAction) ? fallback : projection.RecommendedAction;
    }

    private static int RenderFriendlyHostTransportFailure(FriendlyHostProjection projection, int exitCode)
    {
        Console.Error.WriteLine(projection.ConflictPresent ? "Host session conflict." : "Host not running.");
        if (!string.IsNullOrWhiteSpace(projection.Message))
        {
            Console.Error.WriteLine(projection.Message);
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("Next:");
        Console.Error.WriteLine($"  {ResolveFriendlyHostNextAction(projection, "carves host ensure --json")}");
        return exitCode;
    }

    private sealed record FriendlyHostProjection(
        string HostReadiness,
        string HostOperationalState,
        bool ConflictPresent,
        bool SafeToStartNewHost,
        string RecommendedActionKind,
        string RecommendedAction,
        string LifecycleState,
        string LifecycleReason,
        string Message)
    {
        public bool HostRunning =>
            string.Equals(HostReadiness, "connected", StringComparison.Ordinal)
            || string.Equals(HostReadiness, "healthy_with_pointer_repair", StringComparison.Ordinal);
    }
}
