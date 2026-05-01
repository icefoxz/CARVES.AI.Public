using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackPolicyPreviewSurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-policy-preview.v1";

    public string SurfaceId { get; init; } = "runtime-pack-policy-preview";

    public string Summary { get; init; } = string.Empty;

    public RuntimePackPolicyPreviewArtifact? CurrentPreview { get; init; }

    public string[] Notes { get; init; } = [];
}

public sealed record RuntimePackPolicyPreviewArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string PreviewId { get; init; } = $"packpolpreview-{Guid.NewGuid():N}";

    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string InputPath { get; init; } = string.Empty;

    public string PackageId { get; init; } = string.Empty;

    public string RuntimeStandardVersion { get; init; } = string.Empty;

    public RuntimePackAdmissionPolicyArtifact CurrentAdmissionPolicy { get; init; } = RuntimePackAdmissionPolicyArtifact.CreateDefault(string.Empty);

    public RuntimePackAdmissionPolicyArtifact IncomingAdmissionPolicy { get; init; } = RuntimePackAdmissionPolicyArtifact.CreateDefault(string.Empty);

    public RuntimePackSwitchPolicyArtifact CurrentSwitchPolicy { get; init; } = RuntimePackSwitchPolicyArtifact.CreateDefault();

    public RuntimePackSwitchPolicyArtifact IncomingSwitchPolicy { get; init; } = RuntimePackSwitchPolicyArtifact.CreateDefault();

    public IReadOnlyList<RuntimePackPolicyPreviewDiffEntry> Differences { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string[] ChecksPassed { get; init; } = [];
}

public sealed record RuntimePackPolicyPreviewDiffEntry
{
    public string DiffCode { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string? CurrentValue { get; init; }

    public string? IncomingValue { get; init; }
}
