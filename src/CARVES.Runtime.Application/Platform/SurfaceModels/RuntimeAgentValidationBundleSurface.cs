namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentValidationBundleSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-validation-bundle.v1";

    public string SurfaceId { get; init; } = "runtime-agent-validation-bundle";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string GuidePath { get; init; } = string.Empty;

    public string WorkmapPath { get; init; } = string.Empty;

    public string ArchitecturePath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string ValidationOwnership { get; init; } = "runtime_owned_v1_validation_bundle";

    public IReadOnlyList<RuntimeAgentValidationBundleLaneSurface> Lanes { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentValidationBundleLaneSurface
{
    public string LaneId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> RuntimeSurfaceRefs { get; init; } = [];

    public IReadOnlyList<string> StableEvidencePaths { get; init; } = [];

    public IReadOnlyList<string> TestFileRefs { get; init; } = [];

    public IReadOnlyList<string> ValidationCommands { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}
