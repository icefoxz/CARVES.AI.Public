using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackSwitchPolicySurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-switch-policy.v1";

    public string SurfaceId { get; init; } = "runtime-pack-switch-policy";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public RuntimePackSelectionArtifact? CurrentSelection { get; init; }

    public RuntimePackSwitchPolicyArtifact CurrentPolicy { get; init; } = RuntimePackSwitchPolicyArtifact.CreateDefault();

    public string[] Notes { get; init; } = [];
}

public sealed record RuntimePackSwitchPolicyArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string PolicyId { get; init; } = string.Empty;

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyMode { get; init; } = "manual_local_selection";

    public bool PinActive { get; init; }

    public string? PinnedSelectionId { get; init; }

    public string? PackId { get; init; }

    public string? PackVersion { get; init; }

    public string? Channel { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] ChecksPassed { get; init; } = [];

    public bool BlocksSelectionChange(string packId, string packVersion, string channel)
    {
        if (!PinActive)
        {
            return false;
        }

        return !string.Equals(PackId, packId, StringComparison.Ordinal)
               || !string.Equals(PackVersion, packVersion, StringComparison.Ordinal)
               || !string.Equals(Channel, channel, StringComparison.Ordinal);
    }

    public static RuntimePackSwitchPolicyArtifact CreateDefault()
    {
        return new RuntimePackSwitchPolicyArtifact
        {
            PolicyId = "packpolicy-default",
            PolicyMode = "manual_local_selection",
            PinActive = false,
            Reason = "No explicit local pin is active.",
            Summary = "Runtime-local pack switch policy is unpinned; assign and rollback remain bounded only by current admitted identity and local history.",
            ChecksPassed =
            [
                "policy remains local-runtime scoped",
                "registry and rollout stay closed"
            ],
        };
    }
}
