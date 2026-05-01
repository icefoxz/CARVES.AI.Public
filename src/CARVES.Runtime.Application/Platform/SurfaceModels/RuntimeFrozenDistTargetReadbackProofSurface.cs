namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeFrozenDistTargetReadbackProofSurface
{
    public string SchemaVersion { get; init; } = "runtime-frozen-dist-target-readback-proof.v1";

    public string SurfaceId { get; init; } = "runtime-frozen-dist-target-readback-proof";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string PreviousPhaseDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot target-proof";

    public string JsonCommandEntry { get; init; } = "carves pilot target-proof --json";

    public string AliasCommandEntry { get; init; } = "carves pilot external-proof";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-frozen-dist-target-readback-proof";

    public string ApiCommandEntry { get; init; } = "carves api runtime-frozen-dist-target-readback-proof";

    public bool FrozenDistTargetReadbackProofComplete { get; init; }

    public string CliInvocationPosture { get; init; } = string.Empty;

    public bool CliInvocationContractComplete { get; init; }

    public string CliActivationPosture { get; init; } = string.Empty;

    public bool CliActivationPlanComplete { get; init; }

    public string TargetAgentBootstrapPosture { get; init; } = string.Empty;

    public bool TargetAgentBootstrapReady { get; init; }

    public string LocalDistFreshnessSmokePosture { get; init; } = string.Empty;

    public bool LocalDistFreshnessSmokeReady { get; init; }

    public string LocalDistFreshnessSmokeSourceCommit { get; init; } = string.Empty;

    public string TargetDistBindingPlanPosture { get; init; } = string.Empty;

    public bool TargetDistBindingPlanComplete { get; init; }

    public bool TargetBoundToLocalDist { get; init; }

    public string TargetDistRecommendedBindingMode { get; init; } = string.Empty;

    public string LocalDistHandoffPosture { get; init; } = string.Empty;

    public bool StableExternalConsumptionReady { get; init; }

    public string RuntimeRootKind { get; init; } = string.Empty;

    public string RuntimeDistManifestVersion { get; init; } = string.Empty;

    public string RuntimeDistManifestSourceCommit { get; init; } = string.Empty;

    public bool RuntimeInitialized { get; init; }

    public bool GitRepositoryDetected { get; init; }

    public bool TargetGitWorktreeClean { get; init; }

    public IReadOnlyList<string> RequiredSourceReadbackCommands { get; init; } = [];

    public IReadOnlyList<string> RequiredTargetReadbackCommands { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
