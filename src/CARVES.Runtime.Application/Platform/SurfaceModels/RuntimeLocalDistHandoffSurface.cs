namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeLocalDistHandoffSurface
{
    public string SchemaVersion { get; init; } = "runtime-local-dist-handoff.v1";

    public string SurfaceId { get; init; } = "runtime-local-dist-handoff";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string LocalDistGuideDocumentPath { get; init; } = string.Empty;

    public string CliDistributionGuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot dist";

    public string JsonCommandEntry { get; init; } = "carves pilot dist --json";

    public string RuntimeRootKind { get; init; } = string.Empty;

    public bool StableExternalConsumptionReady { get; init; }

    public bool ExternalTargetBoundToRuntimeRoot { get; init; }

    public bool RuntimeRootMatchesRepoRoot { get; init; }

    public bool RuntimeRootHasManifest { get; init; }

    public bool RuntimeRootHasVersion { get; init; }

    public bool RuntimeRootHasWrapper { get; init; }

    public bool RuntimeRootHasGitDirectory { get; init; }

    public bool RuntimeRootHasSolution { get; init; }

    public string ManifestSchemaVersion { get; init; } = string.Empty;

    public string ManifestVersion { get; init; } = string.Empty;

    public string ManifestSourceCommit { get; init; } = string.Empty;

    public string ManifestOutputPath { get; init; } = string.Empty;

    public string VersionFileValue { get; init; } = string.Empty;

    public IReadOnlyList<string> RequiredSmokeCommands { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
