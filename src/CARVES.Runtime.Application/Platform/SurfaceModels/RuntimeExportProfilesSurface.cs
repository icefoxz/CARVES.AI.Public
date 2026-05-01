using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public enum RuntimeExportPackagingMode
{
    Full,
    ManifestOnly,
    PointerOnly,
}

public sealed class RuntimeExportProfilePolicy
{
    public string SchemaVersion { get; init; } = "runtime-export-profiles-policy.v1";

    public string PolicyId { get; init; } = "runtime-export-profiles";

    public IReadOnlyList<RuntimeExportProfilePolicyProfile> Profiles { get; init; } = [];
}

public sealed class RuntimeExportProfilePolicyProfile
{
    public string ProfileId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<RuntimeExportProfileFamilyRule> FamilyRules { get; init; } = [];

    public IReadOnlyList<string> IncludedPathRoots { get; init; } = [];

    public IReadOnlyList<string> ExcludedFamilyIds { get; init; } = [];

    public IReadOnlyList<string> ExcludedPathRoots { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed class RuntimeExportProfileFamilyRule
{
    public string FamilyId { get; init; } = string.Empty;

    public RuntimeExportPackagingMode PackagingMode { get; init; } = RuntimeExportPackagingMode.Full;

    public string Reason { get; init; } = string.Empty;
}

public sealed class RuntimeExportProfilesSurface
{
    public string SchemaVersion { get; init; } = "runtime-export-profiles.v1";

    public string SurfaceId { get; init; } = "runtime-export-profiles";

    public string PolicyFile { get; init; } = string.Empty;

    public string ArtifactCatalogSchemaVersion { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<RuntimeExportProfileSurfaceProfile> Profiles { get; init; } = [];
}

public sealed class RuntimeExportProfileSurfaceProfile
{
    public string ProfileId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<RuntimeExportProfileResolvedFamily> IncludedFamilies { get; init; } = [];

    public IReadOnlyList<RuntimeExportProfileExcludedFamily> ExcludedFamilies { get; init; } = [];

    public IReadOnlyList<string> IncludedPathRoots { get; init; } = [];

    public IReadOnlyList<string> ExcludedPathRoots { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public RuntimeExportProfileDisciplineSurface Discipline { get; init; } = new();
}

public sealed class RuntimeExportProfileResolvedFamily
{
    public string FamilyId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public RuntimeArtifactClass ArtifactClass { get; init; } = RuntimeArtifactClass.DerivedTruth;

    public RuntimeExportPackagingMode PackagingMode { get; init; } = RuntimeExportPackagingMode.Full;

    public IReadOnlyList<string> Roots { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

public sealed class RuntimeExportProfileExcludedFamily
{
    public string FamilyId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}

public sealed class RuntimeExportProfileDisciplineSurface
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
