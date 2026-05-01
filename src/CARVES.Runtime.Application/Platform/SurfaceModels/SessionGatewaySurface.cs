using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Domain.Platform;
using System.Text.Json.Nodes;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class SessionGatewaySessionCreateRequest
{
    public string? SessionId { get; init; }

    public string? ActorIdentity { get; init; }

    public string? RequestedMode { get; init; }

    public string? ClientRepoRoot { get; init; }
}

public sealed class SessionGatewayMessageRequest
{
    public string? MessageId { get; init; }

    public string UserText { get; init; } = string.Empty;

    public string? ConversationContext { get; init; }

    public string? RequestedMode { get; init; }

    public string? TargetCardId { get; init; }

    public string? TargetTaskGraphId { get; init; }

    public string? TargetTaskId { get; init; }

    public string? PlanHash { get; init; }

    public string? TaskGraphHash { get; init; }

    public string? AcceptanceContractHash { get; init; }

    public IReadOnlyList<string> ExternalModuleReceiptPaths { get; init; } = [];

    public IReadOnlyList<string> RequiredExternalModuleIds { get; init; } = [];

    public JsonObject? ClientCapabilities { get; init; }

    public string? ClientRepoRoot { get; init; }
}

public sealed class SessionGatewayOperationMutationRequest
{
    public string Reason { get; init; } = string.Empty;

    public string? ClientRepoRoot { get; init; }
}

public sealed class SessionGatewaySessionSurface
{
    public string SchemaVersion { get; init; } = "session-gateway-v1.session.v1";

    public string SessionId { get; init; } = string.Empty;

    public string ActorSessionId { get; init; } = string.Empty;

    public string ActorIdentity { get; init; } = string.Empty;

    public string RepoId { get; init; } = string.Empty;

    public string BrokerMode { get; init; } = "strict_broker";

    public string SessionAuthority { get; init; } = "runtime_embedded";

    public string State { get; init; } = string.Empty;

    public string? RuntimeSessionId { get; init; }

    public string? LastOperationClass { get; init; }

    public string? LastOperation { get; init; }

    public string? LastReason { get; init; }

    public string? LatestClassifiedIntent { get; init; }

    public int EventCount { get; init; }
}

public sealed class SessionGatewayTurnSurface
{
    public string SchemaVersion { get; init; } = "session-gateway-v1.turn.v1";

    public bool Accepted { get; init; } = true;

    public string SessionId { get; init; } = string.Empty;

    public string TurnId { get; init; } = string.Empty;

    public string MessageId { get; init; } = string.Empty;

    public string ClassifiedIntent { get; init; } = string.Empty;

    public string? OperationId { get; init; }

    public string NextProjectionHint { get; init; } = string.Empty;

    public string BrokerMode { get; init; } = "strict_broker";

    public string RouteAuthority { get; init; } = "runtime_control_kernel";

    public SessionGatewayIntentEnvelopeSurface IntentEnvelope { get; init; } = new();

    public SessionGatewayTurnPostureSurface TurnPosture { get; init; } = new();

    public SessionGatewayWorkOrderDryRunSurface WorkOrderDryRun { get; init; } = new();
}

public sealed class SessionGatewayTurnPostureSurface
{
    public const string CurrentSchemaVersion = "runtime-turn-posture.session-gateway.v6";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;

    public IReadOnlyDictionary<string, string> ProjectionFields { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public SessionGatewayTurnPostureObservationSurface Observation { get; init; } = new();

    public string GuidanceStateStatus { get; init; } = "none";

    public bool ShouldGuide { get; init; }

    public string? DirectionSummary { get; init; }

    public string? Question { get; init; }

    public string? RecommendedNextAction { get; init; }

    public string? SynthesizedResponse { get; init; }

    public RuntimeGuidanceCandidate? GuidanceCandidate { get; init; }

    public RuntimeGuidanceIntentDraftCandidate? IntentDraftCandidate { get; init; }

    public RuntimeTurnAwarenessPolicy? AwarenessPolicy { get; init; }

    public RuntimeTurnLanguageResolution? LanguageResolution { get; init; }

    public string AwarenessPolicyStatus { get; init; } = RuntimeTurnAwarenessPolicyStatuses.NotCompiled;

    public IReadOnlyList<string> AwarenessPolicyIssues { get; init; } = [];

    public RuntimeGuidanceFieldCollectionDiagnostics? FieldDiagnostics { get; init; }

    public RuntimeGuidanceAdmissionClassifierResult? AdmissionSignals { get; init; }

    public bool PromptSuppressed { get; init; }

    public string? SuppressionKey { get; init; }

    public string IntentLaneGuard { get; init; } = "not_required";

    public string? IntentLaneGuardReason { get; init; }

    public string StyleProfileStatus { get; init; } = "runtime_default";

    public string StyleProfileSource { get; init; } = "runtime_default";

    public string? StyleProfileRevisionHash { get; init; }

    public IReadOnlyList<string> StyleProfileIssues { get; init; } = [];

    public string AwarenessProfileStatus { get; init; } = "runtime_default";

    public string AwarenessProfileSource { get; init; } = "runtime_default";

    public string? AwarenessProfileRevisionHash { get; init; }

    public IReadOnlyList<string> AwarenessProfileIssues { get; init; } = [];

    public bool ShouldLoadPostureRegistryProse { get; init; }

    public bool ShouldLoadCustomPersonalityNotes { get; init; }
}

public sealed class SessionGatewayTurnPostureObservationSurface
{
    public string SchemaVersion { get; init; } = "runtime-turn-posture.observation.v1";

    public string EventKind { get; init; } = "turn_posture_observed";

    public string DecisionPath { get; init; } = "ordinary_no_op";

    public string ProfileStatus { get; init; } = "runtime_default";

    public string GuardStatus { get; init; } = "not_required";

    public string GuidanceStateStatus { get; init; } = "none";

    public bool PromptSuppressed { get; init; }

    public bool ShouldGuide { get; init; }

    public int ProjectionFieldCount { get; init; }

    public IReadOnlyList<string> IssueCodes { get; init; } = [];

    public IReadOnlyList<string> EvidenceCodes { get; init; } = [];
}

public sealed class SessionGatewayIntentEnvelopeSurface
{
    public string SchemaVersion { get; init; } = "carves.intent_envelope.v0.98-rc";

    public string PrimaryIntent { get; init; } = string.Empty;

    public string CandidateLevel { get; init; } = "L0_CHAT";

    public double Confidence { get; init; }

    public IReadOnlyList<SessionGatewayObjectFocusSurface> ObjectFocus { get; init; } = [];

    public IReadOnlyList<string> ActionTokens { get; init; } = [];

    public bool RequiresWorkOrder { get; init; }

    public bool MayExecute { get; init; }
}

public sealed class SessionGatewayObjectFocusSurface
{
    public string Source { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Id { get; init; } = string.Empty;

    public double Confidence { get; init; }
}

public sealed class SessionGatewayWorkOrderDryRunSurface
{
    public string SchemaVersion { get; init; } = "carves.work_order_dry_run.v0.98-rc";

    public string? WorkOrderId { get; init; }

    public string ReceiptKind { get; init; } = "not_required";

    public string AdmissionState { get; init; } = "not_required";

    public string UserVisibleLevel { get; init; } = "L0_CHAT";

    public string TerminalState { get; init; } = "none";

    public bool RequiresWorkOrder { get; init; }

    public bool MayExecute { get; init; }

    public bool LeaseIssued { get; init; }

    public IReadOnlyList<SessionGatewayObjectFocusSurface> BoundObjects { get; init; } = [];

    public IReadOnlyList<SessionGatewayBoundArtifactSurface> BoundArtifacts { get; init; } = [];

    public SessionGatewaySubmitSemanticsSurface SubmitSemantics { get; init; } = new();

    public SessionGatewayOperationRegistrySurface OperationRegistry { get; init; } = new();

    public SessionGatewayCapabilityLeaseSurface CapabilityLease { get; init; } = new();

    public SessionGatewayResourceLeaseSurface ResourceLease { get; init; } = new();

    public SessionGatewayExternalModuleAdapterRegistrySurface ExternalModuleAdapters { get; init; } = new();

    public SessionGatewayExternalModuleReceiptEvaluationSurface ExternalModuleReceiptEvaluation { get; init; } = new();

    public SessionGatewayPrivilegedWorkOrderSurface PrivilegedWorkOrder { get; init; } = new();

    public SessionGatewayExecutionTransactionDryRunSurface TransactionDryRun { get; init; } = new();

    public IReadOnlyList<string> StopReasons { get; init; } = [];

    public string? EffectLedgerPath { get; init; }

    public string? EffectLedgerReplayState { get; init; }

    public string? EffectLedgerTerminalState { get; init; }

    public IReadOnlyList<string> EffectLedgerStopReasons { get; init; } = [];

    public string NextRequiredAction { get; init; } = "none";

    public string Summary { get; init; } = string.Empty;
}

public sealed class SessionGatewayBoundArtifactSurface
{
    public string Kind { get; init; } = string.Empty;

    public string? Hash { get; init; }

    public bool Required { get; init; }

    public string Status { get; init; } = string.Empty;
}

public sealed class SessionGatewaySubmitSemanticsSurface
{
    public IReadOnlyList<string> SubmitMeans { get; init; } = [];

    public IReadOnlyList<string> DoesNotMean { get; init; } = [];
}

public sealed class SessionGatewayOperationRegistrySurface
{
    public string SchemaVersion { get; init; } = "carves.operation_registry.v0.98-rc";

    public string RegistryVersion { get; init; } = string.Empty;

    public IReadOnlyList<SessionGatewayOperationDefinitionSurface> Operations { get; init; } = [];
}

public sealed class SessionGatewayOperationDefinitionSurface
{
    public string OperationId { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string CapabilityRequired { get; init; } = string.Empty;

    public IReadOnlyList<string> PreconditionResolvers { get; init; } = [];

    public IReadOnlyList<string> DeclaredEffects { get; init; } = [];

    public IReadOnlyList<string> WriteTargets { get; init; } = [];

    public string FailurePolicy { get; init; } = string.Empty;

    public string LedgerEventSchema { get; init; } = string.Empty;
}

public sealed class SessionGatewayExecutionTransactionDryRunSurface
{
    public string SchemaVersion { get; init; } = "carves.execution_transaction_dry_run.v0.98-rc";

    public string? TransactionId { get; init; }

    public string? TransactionHash { get; init; }

    public string CompilerVersion { get; init; } = string.Empty;

    public string RegistryVersion { get; init; } = string.Empty;

    public string VerificationState { get; init; } = "not_required";

    public IReadOnlyList<SessionGatewayTransactionStepSurface> Steps { get; init; } = [];

    public IReadOnlyList<string> VerificationErrors { get; init; } = [];

    public SessionGatewayTransactionVerificationReportSurface VerificationReport { get; init; } = new();
}

public sealed class SessionGatewayTransactionStepSurface
{
    public string StepId { get; init; } = string.Empty;

    public string OperationId { get; init; } = string.Empty;

    public string OperationVersion { get; init; } = string.Empty;

    public string CapabilityRequired { get; init; } = string.Empty;

    public IReadOnlyList<string> PreconditionResolvers { get; init; } = [];

    public string PreconditionState { get; init; } = "unresolved";

    public IReadOnlyList<string> DeclaredEffects { get; init; } = [];

    public IReadOnlyList<string> WritesDeclared { get; init; } = [];

    public string FailurePolicy { get; init; } = string.Empty;

    public string LedgerEventSchema { get; init; } = string.Empty;
}

public sealed class SessionGatewayTransactionVerificationReportSurface
{
    public string SchemaVersion { get; init; } = "carves.transaction_verification_report.v0.98-rc";

    public bool OperationCoverage { get; init; }

    public bool CapabilityCoverage { get; init; }

    public bool PreconditionCoverage { get; init; }

    public bool DeclaredEffectCoverage { get; init; }

    public bool WriteTargetCoverage { get; init; }

    public bool FailurePolicyBinding { get; init; }

    public bool LedgerEventBinding { get; init; }

    public bool DeterministicHash { get; init; }

    public int StepCount { get; init; }

    public int ErrorCount { get; init; }
}

public sealed class SessionGatewayTransactionVerificationRequest
{
    public IReadOnlyList<string> CapabilityIds { get; init; } = [];

    public IReadOnlyList<SessionGatewayTransactionStepSurface> Steps { get; init; } = [];
}

public sealed class SessionGatewayCapabilityLeaseSurface
{
    public string SchemaVersion { get; init; } = "carves.capability_lease.v0.98-rc";

    public string? LeaseId { get; init; }

    public string? WorkOrderId { get; init; }

    public string? ParentLeaseId { get; init; }

    public string IssuedBy { get; init; } = string.Empty;

    public string IssuerAuthority { get; init; } = string.Empty;

    public string RequestedBy { get; init; } = string.Empty;

    public string LeaseState { get; init; } = "not_required";

    public DateTimeOffset? IssuedAt { get; init; }

    public DateTimeOffset? ValidUntil { get; init; }

    public bool Revocable { get; init; }

    public bool ExecutionEnabled { get; init; }

    public bool CapabilitySubsetOfParent { get; init; } = true;

    public IReadOnlyList<string> CapabilityIds { get; init; } = [];

    public SessionGatewayCapabilityGrantSurface Read { get; init; } = new();

    public SessionGatewayCapabilityGrantSurface Execute { get; init; } = new();

    public SessionGatewayCapabilityGrantSurface GitOperations { get; init; } = new();

    public SessionGatewayCapabilityGrantSurface TruthOperations { get; init; } = new();

    public SessionGatewayCapabilityGrantSurface ExternalAdapters { get; init; } = new();

    public IReadOnlyList<string> StopReasons { get; init; } = [];
}

public sealed class SessionGatewayCapabilityGrantSurface
{
    public IReadOnlyList<string> Allow { get; init; } = [];

    public IReadOnlyList<string> Deny { get; init; } = [];
}

public sealed class SessionGatewayResourceLeaseSurface
{
    public string SchemaVersion { get; init; } = "carves.resource_lease_surface.v0.98-rc.p7";

    public string? LeaseId { get; init; }

    public string? WorkOrderId { get; init; }

    public string? ParentWorkOrderId { get; init; }

    public string? TaskGraphId { get; init; }

    public string? TaskId { get; init; }

    public string LeaseState { get; init; } = "not_required";

    public string ConflictPolicy { get; init; } = "stop";

    public string ConflictResolution { get; init; } = "not_required";

    public bool CanRunInParallel { get; init; }

    public bool DeclaredWriteSetWithinLease { get; init; }

    public DateTimeOffset? AcquiredAt { get; init; }

    public DateTimeOffset? ValidUntil { get; init; }

    public SessionGatewayResourceWriteSetSurface DeclaredWriteSet { get; init; } = new();

    public SessionGatewayResourceWriteSetSurface ActualWriteSet { get; init; } = new();

    public IReadOnlyList<string> ConflictReasons { get; init; } = [];

    public IReadOnlyList<string> StopReasons { get; init; } = [];

    public IReadOnlyList<string> BlockingLeaseIds { get; init; } = [];
}

public sealed class SessionGatewayResourceWriteSetSurface
{
    public IReadOnlyList<string> TaskIds { get; init; } = [];

    public IReadOnlyList<string> Paths { get; init; } = [];

    public IReadOnlyList<string> Modules { get; init; } = [];

    public IReadOnlyList<string> TruthOperations { get; init; } = [];

    public IReadOnlyList<string> TargetBranches { get; init; } = [];
}

public sealed class SessionGatewayExternalModuleAdapterRegistrySurface
{
    public string SchemaVersion { get; init; } = "carves.external_module_adapter_registry_surface.v0.98-rc.p8";

    public string RegistryVersion { get; init; } = string.Empty;

    public IReadOnlyList<SessionGatewayExternalModuleAdapterSurface> Modules { get; init; } = [];
}

public sealed class SessionGatewayExternalModuleAdapterSurface
{
    public string ModuleId { get; init; } = string.Empty;

    public string ModuleOwner { get; init; } = string.Empty;

    public string CapabilityId { get; init; } = string.Empty;

    public string InputContractSchema { get; init; } = string.Empty;

    public string OutputArtifactKind { get; init; } = string.Empty;

    public string VerdictSchema { get; init; } = string.Empty;

    public string DefaultTrustLevel { get; init; } = string.Empty;

    public string ReceiptReplayRule { get; init; } = string.Empty;

    public string GovernanceBoundary { get; init; } = string.Empty;
}

public sealed class SessionGatewayExternalModuleReceiptEvaluationSurface
{
    public string SchemaVersion { get; init; } = "carves.external_module_receipt_evaluation_surface.v1";

    public string EvaluationState { get; init; } = "not_required";

    public string TransactionDisposition { get; init; } = "not_required";

    public IReadOnlyList<string> CitedReceiptHashes { get; init; } = [];

    public IReadOnlyList<string> StopReasons { get; init; } = [];

    public IReadOnlyList<SessionGatewayExternalModuleReceiptReplaySurface> Receipts { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}

public sealed class SessionGatewayExternalModuleReceiptReplaySurface
{
    public string ModuleId { get; init; } = string.Empty;

    public string ModuleOwner { get; init; } = string.Empty;

    public string Verdict { get; init; } = string.Empty;

    public string ReplayState { get; init; } = string.Empty;

    public string TransactionDisposition { get; init; } = string.Empty;

    public string ReceiptRelativePath { get; init; } = string.Empty;

    public string? ReceiptHash { get; init; }

    public string? OutputArtifactHash { get; init; }

    public IReadOnlyList<string> StopReasons { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}

public sealed class SessionGatewayPrivilegedWorkOrderSurface
{
    public string SchemaVersion { get; init; } = "carves.privileged_work_order_surface.v0.98-rc.p9";

    public string Mode { get; init; } = "not_required";

    public string ConfirmationState { get; init; } = "not_required";

    public string? OperationId { get; init; }

    public string? OperationHash { get; init; }

    public string? TargetKind { get; init; }

    public string? TargetId { get; init; }

    public string? TargetHash { get; init; }

    public string? RequiredRole { get; init; }

    public IReadOnlyList<string> ActorRoles { get; init; } = [];

    public bool SecondConfirmationRequired { get; init; }

    public bool NaturalLanguageConfirmationAccepted { get; init; }

    public bool IrreversibilityAcknowledged { get; init; }

    public string IrreversibilityNotice { get; init; } = string.Empty;

    public string? ExpectedCertificateId { get; init; }

    public string? CertificateId { get; init; }

    public string? CertificatePath { get; init; }

    public string? CertificateHash { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public IReadOnlyList<string> StopReasons { get; init; } = [];
}

public sealed class SessionGatewayCapabilityLeaseVerificationRequest
{
    public string? WorkOrderId { get; init; }

    public string? ParentLeaseId { get; init; }

    public IReadOnlyList<string> ParentCapabilityIds { get; init; } = [];

    public string Issuer { get; init; } = string.Empty;

    public string RequestedBy { get; init; } = string.Empty;

    public DateTimeOffset? ValidUntil { get; init; }

    public DateTimeOffset? Now { get; init; }

    public bool Revoked { get; init; }

    public IReadOnlyList<string> CapabilityIds { get; init; } = [];
}

public sealed class SessionGatewayEventsSurface
{
    public string SchemaVersion { get; init; } = "session-gateway-v1.events.v1";

    public string SessionId { get; init; } = string.Empty;

    public IReadOnlyList<SessionGatewayEventSurface> Events { get; init; } = [];
}

public sealed class SessionGatewayEventSurface
{
    public string EventId { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public DateTimeOffset RecordedAt { get; init; }

    public string SessionId { get; init; } = string.Empty;

    public string? TurnId { get; init; }

    public string? MessageId { get; init; }

    public string? OperationId { get; init; }

    public string Stage { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public JsonObject Projection { get; init; } = new();
}

public sealed record SessionGatewayOperationBindingSurface
{
    public string OperationId { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public string? TurnId { get; init; }

    public string? MessageId { get; init; }

    public string? TaskId { get; init; }

    public string ClassifiedIntent { get; init; } = string.Empty;

    public string? RequestedAction { get; init; }

    public DateTimeOffset AcceptedAt { get; init; }
}

public sealed class SessionGatewayOperationSurface
{
    public string SchemaVersion { get; init; } = "session-gateway-v1.operation.v1";

    public bool Accepted { get; init; } = true;

    public string OperationId { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public string? TurnId { get; init; }

    public string? MessageId { get; init; }

    public string? TaskId { get; init; }

    public string? RequestedAction { get; init; }

    public string BrokerMode { get; init; } = "strict_broker";

    public string RouteAuthority { get; init; } = "runtime_control_kernel";

    public string OperationState { get; init; } = string.Empty;

    public bool Completed { get; init; }

    public int? ExitCode { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = [];

    public DateTimeOffset AcceptedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? ProgressMarker { get; init; }

    public int? ProgressOrdinal { get; init; }

    public DateTimeOffset? ProgressAt { get; init; }

    public SessionGatewayOperatorProofContractSurface OperatorProofContract { get; init; } = new();
}
