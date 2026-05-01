using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeSessionGatewayInternalBetaExitContractSurface
{
    public string SchemaVersion { get; init; } = "runtime-session-gateway-internal-beta-exit-contract.v1";

    public string SurfaceId { get; init; } = "runtime-session-gateway-internal-beta-exit-contract";

    public string ExitContractPath { get; init; } = string.Empty;

    public string ReleaseSurfacePath { get; init; } = string.Empty;

    public string InternalBetaGatePath { get; init; } = string.Empty;

    public string FirstRunPacketPath { get; init; } = string.Empty;

    public string OperatorProofContractPath { get; init; } = string.Empty;

    public string AlphaSetupPath { get; init; } = string.Empty;

    public string AlphaQuickstartPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string InternalBetaGatePosture { get; init; } = string.Empty;

    public string FirstRunPacketPosture { get; init; } = string.Empty;

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string ContractOwnership { get; init; } = "runtime_owned_internal_beta_exit_contract";

    public SessionGatewayOperatorProofContractSurface OperatorProofContract { get; init; } = new();

    public IReadOnlyList<RuntimeSessionGatewayInternalBetaEvidenceSampleSurface> Samples { get; init; } = [];

    public IReadOnlyList<string> RepresentativeEvidenceBasis { get; init; } = [];

    public IReadOnlyList<string> ExitCriteria { get; init; } = [];

    public IReadOnlyList<string> BlockedClaims { get; init; } = [];

    public IReadOnlyList<string> EntryCommands { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeSessionGatewayInternalBetaEvidenceSampleSurface
{
    public string SampleId { get; init; } = string.Empty;

    public string RepoId { get; init; } = string.Empty;

    public string RepoPath { get; init; } = string.Empty;

    public string SampleClass { get; init; } = string.Empty;

    public string EvidenceWeight { get; init; } = string.Empty;

    public string Verdict { get; init; } = string.Empty;

    public string PacketPath { get; init; } = string.Empty;

    public string EvidencePath { get; init; } = string.Empty;

    public bool CountsAsRepresentativeEvidence { get; init; }

    public string BootstrapBehaviorJudgment { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}
