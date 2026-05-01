using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackAdmissionSurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-admission.v1";

    public string SurfaceId { get; init; } = "runtime-pack-admission";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string RuntimeStandardVersion { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public RuntimePackAdmissionArtifact? CurrentAdmission { get; init; }

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimePackAdmissionArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public string RuntimeStandardVersion { get; init; } = string.Empty;

    public string PackArtifactPath { get; init; } = string.Empty;

    public string RuntimePackAttributionPath { get; init; } = string.Empty;

    public string ArtifactRef { get; init; } = string.Empty;

    public RuntimePackAdmissionProfileSelection ExecutionProfiles { get; init; } = new();

    public RuntimePackAdmissionSource Source { get; init; } = new();

    public string Summary { get; init; } = string.Empty;

    public string[] ChecksPassed { get; init; } = [];
}

public sealed class RuntimePackAdmissionProfileSelection
{
    public string PolicyPreset { get; init; } = string.Empty;

    public string GatePreset { get; init; } = string.Empty;

    public string ValidatorProfile { get; init; } = string.Empty;

    public string EnvironmentProfile { get; init; } = string.Empty;

    public string RoutingProfile { get; init; } = string.Empty;
}

public sealed class RuntimePackAdmissionSource
{
    public string AssignmentMode { get; init; } = string.Empty;

    public string? AssignmentRef { get; init; }
}
