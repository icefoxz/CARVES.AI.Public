using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeBetaProgramStatusSurface
{
    public string SchemaVersion { get; init; } = "runtime-beta-program-status.v1";

    public string SurfaceId { get; init; } = "runtime-beta-program-status";

    public string ProgramStatusDocPath { get; init; } = string.Empty;

    public string RoutingMapPath { get; init; } = string.Empty;

    public string RedundancySweepDocPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string ContractOwnership { get; init; } = "runtime_owned_beta_program_status";

    public string LiveEntryCommand { get; init; } = string.Empty;

    public int ConsolidatedSurfaceCount { get; init; }

    public IReadOnlyList<string> ConsolidatedFromSurfaceIds { get; init; } = [];

    public IReadOnlyList<string> RuntimeOwnedProgramBoundaries { get; init; } = [];

    public IReadOnlyList<RuntimeBetaProgramPhaseSurface> Phases { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeBetaProgramPhaseSurface
{
    public string PhaseId { get; init; } = string.Empty;

    public string PhaseTitle { get; init; } = string.Empty;

    public string SupportingDocPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CrossRepoState { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportingReferencePaths { get; init; } = [];

    public IReadOnlyList<string> RuntimeOwnedAreas { get; init; } = [];

    public IReadOnlyList<string> OperatorOwnedFollowOn { get; init; } = [];

    public IReadOnlyList<string> CloudOwnedFollowOn { get; init; } = [];

    public IReadOnlyList<string> BlockedClaims { get; init; } = [];

    public IReadOnlyList<string> QueryEntryCommands { get; init; } = [];
}
