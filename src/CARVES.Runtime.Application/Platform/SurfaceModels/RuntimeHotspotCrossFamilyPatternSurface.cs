using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeHotspotCrossFamilyPatternSurface
{
    public string SchemaVersion { get; init; } = "runtime-hotspot-cross-family-patterns.v1";

    public string SurfaceId { get; init; } = "runtime-hotspot-cross-family-patterns";

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string SourceSurfaceId { get; init; } = "runtime-hotspot-backlog-drain";

    public string SourceBoundaryDocumentPath { get; init; } = string.Empty;

    public string BacklogPath { get; init; } = string.Empty;

    public string QueueIndexPath { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public DateTimeOffset? QueueSnapshotGeneratedAt { get; init; }

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public RuntimeHotspotCrossFamilyPatternCountsSurface Counts { get; init; } = new();

    public IReadOnlyList<RuntimeHotspotCrossFamilyPatternEntrySurface> Patterns { get; init; } = [];

    public IReadOnlyList<RuntimeHotspotCrossFamilyBoundaryCategorySurface> BoundaryCategories { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeHotspotCrossFamilyPatternCountsSurface
{
    public int QueueFamilyCount { get; init; }

    public int ContinuedQueueCount { get; init; }

    public int PatternCount { get; init; }

    public int ResidualPatternCount { get; init; }

    public int RepeatedBacklogKindPatternCount { get; init; }

    public int ValidationOverlapPatternCount { get; init; }

    public int BoundaryCategoryCount { get; init; }

    public int SharedBoundaryCategoryCount { get; init; }
}

public sealed class RuntimeHotspotCrossFamilyPatternEntrySurface
{
    public string PatternId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string PatternType { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public int QueueCount { get; init; }

    public IReadOnlyList<string> QueueIds { get; init; } = [];

    public IReadOnlyList<string> SupportingTaskIds { get; init; } = [];

    public IReadOnlyList<string> PreviousTaskIds { get; init; } = [];

    public IReadOnlyList<string> BacklogItemIds { get; init; } = [];

    public IReadOnlyList<string> BacklogKinds { get; init; } = [];

    public IReadOnlyList<string> ValidationSurfaces { get; init; } = [];

    public IReadOnlyList<string> RepresentativePaths { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeHotspotCrossFamilyBoundaryCategorySurface
{
    public string CategoryId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public int QueueCount { get; init; }

    public IReadOnlyList<string> QueueIds { get; init; } = [];

    public IReadOnlyList<string> MatchingKeywords { get; init; } = [];

    public IReadOnlyList<string> RepresentativePaths { get; init; } = [];

    public IReadOnlyList<string> SupportingStatements { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
