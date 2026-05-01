using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeValidationLabProofHandoffSurface
{
    public string SchemaVersion { get; init; } = "runtime-validationlab-proof-handoff.v1";

    public string SurfaceId { get; init; } = "runtime-validationlab-proof-handoff";

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string ArtifactCatalogSchemaVersion { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public bool ControlledModeDefault { get; init; }

    public IReadOnlyList<string> ValidationLabFollowOnLanes { get; init; } = [];

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<RuntimeValidationLabProofLaneSurface> Lanes { get; init; } = [];
}

public sealed class RuntimeValidationLabProofLaneSurface
{
    public string LaneId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string HandoffMode { get; init; } = "read_only_projection";

    public string RuntimeAuthoritySummary { get; init; } = string.Empty;

    public string ValidationLabAuthoritySummary { get; init; } = string.Empty;

    public IReadOnlyList<string> RuntimeCommands { get; init; } = [];

    public IReadOnlyList<RuntimeValidationLabProofFamilySurface> RuntimeTruthFamilies { get; init; } = [];

    public IReadOnlyList<string> RuntimeEvidencePaths { get; init; } = [];

    public IReadOnlyList<string> ValidationLabOwnedAssets { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];

    public RuntimeValidationLabProofDisciplineSurface Discipline { get; init; } = new();
}

public sealed class RuntimeValidationLabProofFamilySurface
{
    public string FamilyId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public RuntimeArtifactClass ArtifactClass { get; init; } = RuntimeArtifactClass.DerivedTruth;

    public RuntimeExportPackagingMode PackagingMode { get; init; } = RuntimeExportPackagingMode.Full;

    public IReadOnlyList<string> Roots { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

public sealed class RuntimeValidationLabProofDisciplineSurface
{
    public bool IsValid { get; init; } = true;

    public int FullFamilyCount { get; init; }

    public int ManifestOnlyFamilyCount { get; init; }

    public int PointerOnlyFamilyCount { get; init; }

    public IReadOnlyList<string> FullFamilyIds { get; init; } = [];

    public IReadOnlyList<string> ManifestOnlyFamilyIds { get; init; } = [];

    public IReadOnlyList<string> PointerOnlyFamilyIds { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
