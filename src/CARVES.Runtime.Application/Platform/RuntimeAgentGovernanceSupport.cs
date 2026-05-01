using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform;

internal static class RuntimeAgentGovernanceSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static AgentGovernanceKernelPolicy LoadPolicy(string repoRoot, ControlPlanePaths paths)
    {
        return new RuntimeAgentGovernanceKernelService(repoRoot, paths).LoadPolicy();
    }

    public static RuntimeSessionState? LoadSession(ControlPlanePaths paths)
    {
        if (!File.Exists(paths.RuntimeSessionFile))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimeSessionState>(File.ReadAllText(paths.RuntimeSessionFile), JsonOptions);
    }

    public static AgentBootstrapHostSnapshot LoadHostSnapshot(ControlPlanePaths paths)
    {
        if (!File.Exists(paths.PlatformHostSnapshotFile))
        {
            return new AgentBootstrapHostSnapshot();
        }

        using var document = JsonDocument.Parse(File.ReadAllText(paths.PlatformHostSnapshotFile));
        var root = document.RootElement;
        return new AgentBootstrapHostSnapshot
        {
            State = NormalizeHostSnapshotState(root),
            SessionStatus = TryGetString(root, "session_status") ?? "unknown",
            HostControlState = TryGetString(root, "host_control_state") ?? "unknown",
            RecordedAt = TryGetDateTimeOffset(root, "recorded_at"),
        };
    }

    public static string ToRepoRelative(string repoRoot, string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string NormalizeHostSnapshotState(JsonElement root)
    {
        var state = TryGetString(root, "state");
        var hostControlState = TryGetString(root, "host_control_state");
        if (string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase))
        {
            return "stopped";
        }

        if (string.Equals(state, "live", StringComparison.OrdinalIgnoreCase)
            || string.Equals(hostControlState, "running", StringComparison.OrdinalIgnoreCase))
        {
            return "Live/running";
        }

        return state ?? "unknown";
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(property.GetString(), out var value) ? value : null;
    }
}
