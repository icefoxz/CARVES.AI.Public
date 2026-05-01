namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeVendorNativeAccelerationSurface
{
    public string SchemaVersion { get; init; } = "runtime-vendor-native-acceleration.v1";

    public string SurfaceId { get; init; } = "runtime-vendor-native-acceleration";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string CodexGovernanceDocumentPath { get; init; } = string.Empty;

    public string ClaudeQualificationDocumentPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CurrentMode { get; init; } = string.Empty;

    public string PlanningCouplingPosture { get; init; } = string.Empty;

    public string FormalPlanningPosture { get; init; } = string.Empty;

    public string? PlanHandle { get; init; }

    public string? PlanningCardId { get; init; }

    public string ManagedWorkspacePosture { get; init; } = string.Empty;

    public string PortableFoundationSummary { get; init; } = string.Empty;

    public string CodexReinforcementState { get; init; } = string.Empty;

    public string CodexReinforcementSummary { get; init; } = string.Empty;

    public IReadOnlyList<string> CodexGovernanceAssets { get; init; } = [];

    public string ClaudeReinforcementState { get; init; } = string.Empty;

    public string ClaudeReinforcementSummary { get; init; } = string.Empty;

    public IReadOnlyList<string> ClaudeQualifiedRoutingIntents { get; init; } = [];

    public IReadOnlyList<string> ClaudeClosedRoutingIntents { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
