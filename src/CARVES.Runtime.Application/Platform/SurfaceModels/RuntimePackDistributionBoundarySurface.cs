using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackDistributionBoundarySurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-distribution-boundary.v1";

    public string SurfaceId { get; init; } = "runtime-pack-distribution-boundary";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public RuntimePackDistributionCurrentTruth CurrentTruth { get; init; } = new();

    public IReadOnlyList<RuntimePackBoundaryCapability> LocalCapabilities { get; init; } = [];

    public IReadOnlyList<RuntimePackBoundaryCapability> ClosedFutureCapabilities { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimePackDistributionCurrentTruth
{
    public bool HasCurrentAdmission { get; init; }

    public bool HasCurrentSelection { get; init; }

    public int SelectionHistoryCount { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimePackBoundaryCapability
{
    public string CapabilityId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> TruthRefs { get; init; } = [];
}
