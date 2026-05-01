using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeRemoteWorkerQualificationSurface
{
    public string SchemaVersion { get; init; } = "runtime-remote-worker-qualification.v1";

    public string SurfaceId { get; init; } = "runtime-remote-worker-qualification";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public RuntimeRemoteWorkerQualificationPolicy[] CurrentPolicies { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed record RuntimeRemoteWorkerQualificationPolicy
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string PolicyId { get; init; } = string.Empty;

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProviderId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string RoutingProfileId { get; init; } = string.Empty;

    public string ProtocolFamily { get; init; } = string.Empty;

    public RuntimeRemoteWorkerLaneQualification[] Lanes { get; init; } = [];

    public string[] ChecksPassed { get; init; } = [];
}

public sealed record RuntimeRemoteWorkerLaneQualification
{
    public string LaneId { get; init; } = string.Empty;

    public string RoutingIntent { get; init; } = string.Empty;

    public bool Allowed { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string[] Constraints { get; init; } = [];
}

public sealed record RuntimeRemoteWorkerLaneDecision(
    bool Allowed,
    string ReasonCode,
    string Summary,
    string? ProviderId,
    string? BackendId,
    string? RoutingIntent,
    string? LaneId,
    IReadOnlyList<string> Constraints)
{
    public static RuntimeRemoteWorkerLaneDecision NotApplicable(string providerId, string backendId)
    {
        return new RuntimeRemoteWorkerLaneDecision(
            Allowed: true,
            ReasonCode: "not_applicable",
            Summary: $"Remote worker qualification does not apply to backend '{backendId}'.",
            ProviderId: providerId,
            BackendId: backendId,
            RoutingIntent: null,
            LaneId: null,
            Constraints: Array.Empty<string>());
    }
}
