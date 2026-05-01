namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAlphaExternalUseReadinessSurface
{
    public string SchemaVersion { get; init; } = "runtime-alpha-external-use-readiness.v1";

    public string SurfaceId { get; init; } = "runtime-alpha-external-use-readiness";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string PreviousPhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.PreviousDocumentPath;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string AlphaVersion { get; init; } = RuntimeAlphaVersion.Current;

    public string CommandEntry { get; init; } = "carves pilot readiness";

    public string JsonCommandEntry { get; init; } = "carves pilot readiness --json";

    public string AliasCommandEntry { get; init; } = "carves pilot alpha";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-alpha-external-use-readiness";

    public string ApiCommandEntry { get; init; } = "carves api runtime-alpha-external-use-readiness";

    public bool AlphaExternalUseReady { get; init; }

    public bool FrozenLocalDistReady { get; init; }

    public bool ExternalConsumerResourcePackReady { get; init; }

    public bool GovernedAgentHandoffReady { get; init; }

    public bool ProductizedPilotGuideReady { get; init; }

    public bool SessionGatewayPrivateAlphaReady { get; init; }

    public bool SessionGatewayRepeatabilityReady { get; init; }

    public bool ProductPilotProofRequiredPerTarget { get; init; } = true;

    public string CandidateDistRoot { get; init; } = string.Empty;

    public string DistManifestSourceCommit { get; init; } = string.Empty;

    public string SourceGitHead { get; init; } = string.Empty;

    public bool SourceGitWorktreeClean { get; init; }

    public bool DistManifestMatchesSourceHead { get; init; }

    public IReadOnlyList<RuntimeAlphaExternalUseReadinessCheckSurface> ReadinessChecks { get; init; } = [];

    public IReadOnlyList<string> MinimumOperatorReadbacks { get; init; } = [];

    public IReadOnlyList<string> ExternalTargetStartCommands { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAlphaExternalUseReadinessCheckSurface
{
    public string CheckId { get; init; } = string.Empty;

    public string SurfaceId { get; init; } = string.Empty;

    public string Posture { get; init; } = string.Empty;

    public bool Ready { get; init; }

    public bool BlocksAlphaUse { get; init; } = true;

    public string Summary { get; init; } = string.Empty;
}
