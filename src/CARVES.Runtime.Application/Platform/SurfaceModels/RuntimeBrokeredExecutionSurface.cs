namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeBrokeredExecutionSurface
{
    public string SchemaVersion { get; init; } = "runtime-brokered-execution.v1";

    public string SurfaceId { get; init; } = "runtime-brokered-execution";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string TaskStatus { get; init; } = string.Empty;

    public string ModeEProfileId { get; init; } = "mode_e_brokered_execution";

    public string BrokeredExecutionState { get; init; } = string.Empty;

    public string BrokeredExecutionDocumentPath { get; init; } = "docs/runtime/runtime-mode-e-brokered-execution.md";

    public string ExecutionPacketSurfaceId { get; init; } = "execution-packet";

    public string PacketEnforcementSurfaceId { get; init; } = "packet-enforcement";

    public string ResultReturnChannel { get; init; } = string.Empty;

    public string WorkerArtifactChannel { get; init; } = ".ai/artifacts/worker-executions/";

    public string ResultReturnPayloadStatus { get; init; } = "missing";

    public string ResultReturnOfficialTruthState { get; init; } = "returned_material_not_approved_truth";

    public string PacketPath { get; init; } = string.Empty;

    public bool PacketPersisted { get; init; }

    public string PacketEnforcementPath { get; init; } = string.Empty;

    public string PacketEnforcementVerdict { get; init; } = string.Empty;

    public string PlannerIntent { get; init; } = string.Empty;

    public string WorkspaceOwnershipPolicy { get; init; } = "no_unrestricted_repo_or_workspace_ownership";

    public string OfficialTruthIngressPolicy { get; init; } = "planner_review_and_host_writeback_only";

    public string ResultReturnPolicy { get; init; } = "worker_returns_result_envelope_patch_evidence_or_replan_request";

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public IReadOnlyList<RuntimeBrokeredExecutionCheckSurface> BrokeredChecks { get; init; } = [];

    public IReadOnlyList<string> InspectCommands { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool IsValid { get; init; } = true;

    public ExecutionPacketSurfaceSnapshot ExecutionPacket { get; init; } = new();

    public PacketEnforcementSurfaceSnapshot PacketEnforcement { get; init; } = new();

    public RuntimeBrokeredResultReturnSurface ResultReturn { get; init; } = new();

    public RuntimeBrokeredReviewPreflightSurface ReviewPreflight { get; init; } = new();

    public RuntimeWorkspaceMutationAuditSurface? MutationAudit { get; init; }
}

public sealed class RuntimeBrokeredResultReturnSurface
{
    public string SchemaVersion { get; init; } = "runtime-brokered-result-return.v1";

    public string Channel { get; init; } = string.Empty;

    public string ExpectedPayloadShape { get; init; } = "result-envelope.v1";

    public bool PayloadPresent { get; init; }

    public bool PayloadReadable { get; init; }

    public bool PayloadValid { get; init; }

    public bool PayloadMalformed { get; init; }

    public string PayloadStatus { get; init; } = "missing";

    public string? PayloadTaskId { get; init; }

    public string? PayloadExecutionRunId { get; init; }

    public string? PayloadResultStatus { get; init; }

    public string OfficialTruthState { get; init; } = "returned_material_not_approved_truth";

    public string WritebackPolicy { get; init; } = "planner_review_and_host_writeback_required";

    public IReadOnlyList<string> ExpectedEvidence { get; init; } = [];

    public IReadOnlyList<string> MissingEvidence { get; init; } = [];

    public IReadOnlyList<string> PayloadIssues { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeBrokeredExecutionCheckSurface
{
    public string CheckId { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RequiredAction { get; init; } = string.Empty;
}

public sealed class RuntimeBrokeredReviewPreflightSurface
{
    public string SchemaVersion { get; init; } = "runtime-brokered-review-preflight.v1";

    public string Status { get; init; } = "not_applicable";

    public bool Applies { get; init; }

    public bool CanProceedToReviewApproval { get; init; } = true;

    public bool CanProceedToProvisionalApproval { get; init; } = true;

    public string Summary { get; init; } = "Mode E review preflight does not apply.";

    public string PacketScopeStatus { get; init; } = "not_applicable";

    public string PacketEnforcementVerdict { get; init; } = "not_applicable";

    public IReadOnlyList<string> PacketScopeMismatchFiles { get; init; } = [];

    public IReadOnlyList<string> PacketScopeReasonCodes { get; init; } = [];

    public string AcceptanceEvidenceStatus { get; init; } = "not_applicable";

    public IReadOnlyList<string> MissingAcceptanceEvidence { get; init; } = [];

    public string PathPolicyStatus { get; init; } = "not_applicable";

    public IReadOnlyList<string> ProtectedPathViolations { get; init; } = [];

    public IReadOnlyList<RuntimeProtectedPathViolationSurface> ProtectedPathViolationDetails { get; init; } = [];

    public IReadOnlyList<string> HostOnlyPathViolations { get; init; } = [];

    public IReadOnlyList<string> DeniedPathViolations { get; init; } = [];

    public string MutationAuditStatus { get; init; } = "not_applicable";

    public int MutationAuditChangedPathCount { get; init; }

    public IReadOnlyList<string> MutationAuditViolationPaths { get; init; } = [];

    public string? MutationAuditFirstViolationPath { get; init; }

    public string? MutationAuditFirstViolationClass { get; init; }

    public string MutationAuditRecommendedAction { get; init; } = "none";

    public IReadOnlyList<RuntimeBrokeredReviewPreflightBlockerSurface> Blockers { get; init; } = [];
}

public sealed class RuntimeBrokeredReviewPreflightBlockerSurface
{
    public string BlockerId { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string RequiredAction { get; init; } = string.Empty;
}
