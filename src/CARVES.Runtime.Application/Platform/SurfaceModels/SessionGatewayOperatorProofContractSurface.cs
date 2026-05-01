using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public static class SessionGatewayProofSources
{
    public const string SyntheticFixture = "synthetic_fixture";
    public const string RepoLocalProof = "repo_local_proof";
    public const string OperatorRunProof = "operator_run_proof";
    public const string ExternalUserProof = "external_user_proof";
}

public static class SessionGatewayOperatorWaitStates
{
    public const string WaitingOperatorSetup = "WAITING_OPERATOR_SETUP";
    public const string WaitingOperatorRun = "WAITING_OPERATOR_RUN";
    public const string WaitingOperatorEvidence = "WAITING_OPERATOR_EVIDENCE";
    public const string WaitingOperatorVerdict = "WAITING_OPERATOR_VERDICT";
}

public static class SessionGatewayOperatorContractEvents
{
    public const string OperatorActionRequired = "operator_action_required";
    public const string OperatorProjectRequired = "operator_project_required";
    public const string OperatorEvidenceRequired = "operator_evidence_required";
    public const string RealWorldProofMissing = "real_world_proof_missing";
}

public sealed class SessionGatewayStageExitContractSurface
{
    public string StageId { get; init; } = string.Empty;

    public string BlockingState { get; init; } = string.Empty;

    public IReadOnlyList<string> RequiredEventKinds { get; init; } = [];

    public IReadOnlyList<string> AcceptedProofSources { get; init; } = [];

    public IReadOnlyList<string> OperatorMustDo { get; init; } = [];

    public IReadOnlyList<string> AiMayDo { get; init; } = [];

    public IReadOnlyList<string> RequiredEvidence { get; init; } = [];

    public IReadOnlyList<string> NonPassingEvidence { get; init; } = [];

    public string MissingProofSummary { get; init; } = string.Empty;
}

public sealed class SessionGatewayOperatorProofContractSurface
{
    public string CurrentProofSource { get; init; } = string.Empty;

    public string CurrentOperatorState { get; init; } = string.Empty;

    public bool OperatorActionRequired { get; init; }

    public bool RealWorldProofMissing { get; init; }

    public string BlockingSummary { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportedProofSources { get; init; } = [];

    public IReadOnlyList<string> BlockingEventKinds { get; init; } = [];

    public IReadOnlyList<string> SharedRequiredEvidence { get; init; } = [];

    public IReadOnlyList<SessionGatewayStageExitContractSurface> StageExitContracts { get; init; } = [];
}
