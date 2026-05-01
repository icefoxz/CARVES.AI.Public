using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeControlledGovernanceProofSurface
{
    public string SchemaVersion { get; init; } = "runtime-controlled-governance-proof.v1";

    public string SurfaceId { get; init; } = "runtime-controlled-governance-proof";

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string HandoffBoundaryDocumentPath { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public bool ControlledModeDefault { get; init; }

    public bool ProducerCannotSelfApprove { get; init; }

    public bool ReviewerCannotApproveSameTask { get; init; }

    public IReadOnlyList<string> ValidationLabFollowOnLanes { get; init; } = [];

    public IReadOnlyList<string> ControlledModeInvariants { get; init; } = [];

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<RuntimeControlledGovernanceProofLaneSurface> Lanes { get; init; } = [];
}

public sealed class RuntimeControlledGovernanceProofLaneSurface
{
    public string LaneId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string TruthLevel { get; init; } = "implemented";

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> SourceHandoffLaneIds { get; init; } = [];

    public string RuntimeAuthoritySummary { get; init; } = string.Empty;

    public string IntegrationSummary { get; init; } = string.Empty;

    public IReadOnlyList<string> GoverningCommands { get; init; } = [];

    public IReadOnlyList<RuntimeValidationLabProofFamilySurface> RuntimeTruthFamilies { get; init; } = [];

    public IReadOnlyList<string> RuntimeEvidencePaths { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
