using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackagingProofFederationMaturitySurface
{
    public string SchemaVersion { get; init; } = "runtime-packaging-proof-federation-maturity.v1";

    public string SurfaceId { get; init; } = "runtime-packaging-proof-federation-maturity";

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string ExportProfilePolicyPath { get; init; } = string.Empty;

    public string ControlledGovernanceBoundaryPath { get; init; } = string.Empty;

    public string ValidationLabHandoffBoundaryPath { get; init; } = string.Empty;

    public string PackDistributionSurfaceId { get; init; } = "runtime-pack-distribution-boundary";

    public string Summary { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<RuntimePackagingMaturityProfileSurface> PackagingProfiles { get; init; } = [];

    public IReadOnlyList<RuntimePackagingMaturityProofLaneSurface> ProofLanes { get; init; } = [];

    public IReadOnlyList<RuntimeBoundedFederationLaneSurface> FederationLanes { get; init; } = [];

    public IReadOnlyList<RuntimePackBoundaryCapability> ClosedCapabilities { get; init; } = [];
}

public sealed class RuntimePackagingMaturityProfileSurface
{
    public string ProfileId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool DisciplineValid { get; init; } = true;

    public IReadOnlyList<string> FullFamilyIds { get; init; } = [];

    public IReadOnlyList<string> ManifestOnlyFamilyIds { get; init; } = [];

    public IReadOnlyList<string> PointerOnlyFamilyIds { get; init; } = [];

    public IReadOnlyList<string> IncludedPathRoots { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed class RuntimePackagingMaturityProofLaneSurface
{
    public string LaneId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string TruthLevel { get; init; } = "implemented";

    public IReadOnlyList<string> SourceHandoffLaneIds { get; init; } = [];

    public IReadOnlyList<string> RuntimeEvidencePaths { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeBoundedFederationLaneSurface
{
    public string LaneId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> TruthRefs { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
