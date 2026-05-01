namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeTargetAgentBootstrapPackSurface
{
    public string SchemaVersion { get; init; } = "runtime-target-agent-bootstrap-pack.v1";

    public string SurfaceId { get; init; } = "runtime-target-agent-bootstrap-pack";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string GuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves agent bootstrap";

    public string MaterializeCommandEntry { get; init; } = "carves agent bootstrap --write";

    public string TargetAgentBootstrapPath { get; init; } = ".ai/AGENT_BOOTSTRAP.md";

    public string RootAgentsPath { get; init; } = "AGENTS.md";

    public string ProjectLocalLauncherPath { get; init; } = ".carves/carves";

    public string AgentStartMarkdownPath { get; init; } = ".carves/AGENT_START.md";

    public string AgentStartJsonPath { get; init; } = ".carves/agent-start.json";

    public string VisibleAgentStartPath { get; init; } = "CARVES_START.md";

    public bool RuntimeInitialized { get; init; }

    public bool TargetAgentBootstrapExists { get; init; }

    public bool RootAgentsExists { get; init; }

    public bool RootAgentsContainsCarvesEntry { get; init; }

    public string RootAgentsIntegrationPosture { get; init; } = string.Empty;

    public string RootAgentsSuggestedPatch { get; init; } = string.Empty;

    public bool ProjectLocalLauncherExists { get; init; }

    public bool AgentStartMarkdownExists { get; init; }

    public bool AgentStartJsonExists { get; init; }

    public bool VisibleAgentStartExists { get; init; }

    public bool CanMaterialize { get; init; }

    public bool WriteRequested { get; init; }

    public IReadOnlyList<string> MissingFiles { get; init; } = [];

    public IReadOnlyList<string> MaterializedFiles { get; init; } = [];

    public IReadOnlyList<string> SkippedFiles { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
