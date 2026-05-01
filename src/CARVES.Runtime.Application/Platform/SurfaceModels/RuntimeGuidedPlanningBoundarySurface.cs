namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeGuidedPlanningBoundarySurface
{
    public string SchemaVersion { get; init; } = "runtime-guided-planning-boundary.v1";

    public string SurfaceId { get; init; } = "runtime-guided-planning-boundary";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string WorkbenchBoundaryPath { get; init; } = string.Empty;

    public string SessionGatewayPath { get; init; } = string.Empty;

    public string FirstRunPacketPath { get; init; } = string.Empty;

    public string QuickstartGuidePath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string InteractiveShellOwner { get; init; } = "carves_operator";

    public string FocusField { get; init; } = "focus_card_id";

    public string FocusEffect { get; init; } = "scope_next_clarification_turn_only";

    public string PreferredInteractiveProjection { get; init; } = "downstream_web_graph_canvas";

    public IReadOnlyList<string> AuxiliaryGraphProjections { get; init; } = [];

    public IReadOnlyList<string> OfficialLifecycle { get; init; } = [];

    public IReadOnlyList<string> PlanningPostures { get; init; } = [];

    public IReadOnlyList<RuntimeGuidedPlanningObjectSurface> PlanningObjects { get; init; } = [];

    public IReadOnlyList<string> AllowedProjectionSurfaces { get; init; } = [];

    public IReadOnlyList<string> WritebackCommands { get; init; } = [];

    public IReadOnlyList<string> BlockedClaims { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeGuidedPlanningObjectSurface
{
    public string ObjectId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string TruthRole { get; init; } = string.Empty;

    public string WritebackEligibility { get; init; } = string.Empty;

    public string ProjectionUse { get; init; } = string.Empty;
}
