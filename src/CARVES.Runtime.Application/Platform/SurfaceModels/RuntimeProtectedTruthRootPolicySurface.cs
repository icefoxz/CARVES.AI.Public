namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeProtectedTruthRootPolicySurface
{
    public string SchemaVersion { get; init; } = "runtime-protected-truth-root-policy.v1";

    public string SurfaceId { get; init; } = "runtime-protected-truth-root-policy";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyDocumentPath { get; init; } = string.Empty;

    public string ProjectBoundaryPath { get; init; } = ".ai/PROJECT_BOUNDARY.md";

    public string OverallPosture { get; init; } = string.Empty;

    public string EnforcementAnchor { get; init; } = "mode_e_review_preflight_and_review_writeback";

    public string BaselinePortability { get; init; } = "vendor_agnostic_runtime_owned";

    public IReadOnlyList<RuntimeProtectedTruthRootSurface> ProtectedRoots { get; init; } = [];

    public IReadOnlyList<RuntimeProtectedTruthRootSurface> DeniedRoots { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeProtectedTruthRootSurface
{
    public string Root { get; init; } = string.Empty;

    public string Classification { get; init; } = string.Empty;

    public string AllowedMutationChannel { get; init; } = string.Empty;

    public string UnauthorizedMutationOutcome { get; init; } = string.Empty;

    public string RemediationAction { get; init; } = string.Empty;

    public IReadOnlyList<string> Examples { get; init; } = [];
}

public sealed class RuntimeProtectedPathViolationSurface
{
    public string Path { get; init; } = string.Empty;

    public string ProtectedClassification { get; init; } = string.Empty;

    public string RemediationAction { get; init; } = string.Empty;
}
