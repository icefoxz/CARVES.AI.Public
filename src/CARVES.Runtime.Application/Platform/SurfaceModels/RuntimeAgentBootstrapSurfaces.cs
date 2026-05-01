using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentBootstrapPacketSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-bootstrap-packet.v1";

    public string SurfaceId { get; init; } = "runtime-agent-bootstrap-packet";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public AgentBootstrapPacket Packet { get; init; } = new();
}

public sealed class AgentBootstrapPacket
{
    public string PacketId { get; init; } = "agent-bootstrap-packet";

    public int PolicyVersion { get; init; }

    public string StartupMode { get; init; } = string.Empty;

    public string InitializationHeading { get; init; } = string.Empty;

    public string SourcesHeading { get; init; } = string.Empty;

    public string[] EntryOrder { get; init; } = [];

    public string[] CurrentCardMemoryRefs { get; init; } = [];

    public string[] ReportFields { get; init; } = [];

    public string[] SourceFields { get; init; } = [];

    public AgentBootstrapRepoPosture RepoPosture { get; init; } = new();

    public AgentBootstrapHostSnapshot HostSnapshot { get; init; } = new();

    public string PostureBasis { get; init; } = "unknown";

    public string[] HostRoutedActions { get; init; } = [];

    public string[] StartupInspectCommands { get; init; } = [];

    public string[] WarmResumeInspectCommands { get; init; } = [];

    public string[] OptionalDeepReadCommands { get; init; } = [];

    public string[] NotYetProven { get; init; } = [];

    public AgentBootstrapHotPathContext HotPathContext { get; init; } = new();
}

public sealed class AgentBootstrapHotPathContext
{
    public string Summary { get; init; } = string.Empty;

    public string RecommendedStartupRoute { get; init; } = "bootstrap_packet_first";

    public string GovernanceBoundary { get; init; } = "compact_context_does_not_replace_initialization_report";

    public string CurrentTaskId { get; init; } = "N/A";

    public string[] DefaultInspectCommands { get; init; } = [];

    public string[] TaskOverlayCommands { get; init; } = [];

    public string[] BoundedNextCommands { get; init; } = [];

    public string[] FullGovernanceReadTriggers { get; init; } = [];

    public AgentBootstrapTaskSummary[] ActiveTasks { get; init; } = [];

    public AgentMarkdownReadPolicy MarkdownReadPolicy { get; init; } = new();
}

public sealed class AgentMarkdownReadPolicy
{
    public string PolicyId { get; init; } = "markdown-read-frequency-reduction";

    public string Summary { get; init; } = string.Empty;

    public string DefaultPostInitializationMode { get; init; } = "machine_surface_first";

    public string WarmResumeMode { get; init; } = "receipt_validated_machine_surface_first";

    public string GovernanceBoundary { get; init; } = "classification_reduces_read_frequency_only";

    public string[] RequiredInitialSources { get; init; } = [];

    public string[] NeverReplacedSources { get; init; } = [];

    public string[] PostInitializationHotPathSurfaces { get; init; } = [];

    public string[] DeferredAfterInitializationSources { get; init; } = [];

    public string[] EscalationTriggers { get; init; } = [];

    public AgentMarkdownReadTier[] ReadTiers { get; init; } = [];
}

public sealed class AgentMarkdownReadTier
{
    public string TierId { get; init; } = string.Empty;

    public string DefaultAction { get; init; } = string.Empty;

    public string ReadWhen { get; init; } = string.Empty;

    public string[] Sources { get; init; } = [];

    public string[] PreferredSurfaces { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class AgentBootstrapTaskSummary
{
    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = "CARD-UNKNOWN";

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = "unknown";

    public string Priority { get; init; } = string.Empty;

    public string TaskType { get; init; } = "unknown";

    public string Summary { get; init; } = string.Empty;

    public string InspectCommand { get; init; } = string.Empty;

    public string OverlayCommand { get; init; } = string.Empty;
}

public sealed class AgentBootstrapRepoPosture
{
    public string SessionStatus { get; init; } = "unknown";

    public string LoopMode { get; init; } = "unknown";

    public string CurrentActionability { get; init; } = "unknown";

    public string PlannerState { get; init; } = "unknown";

    public string CurrentTaskId { get; init; } = "N/A";
}

public sealed class AgentBootstrapHostSnapshot
{
    public string State { get; init; } = "not_checked";

    public string SessionStatus { get; init; } = "unknown";

    public string HostControlState { get; init; } = "unknown";

    public DateTimeOffset? RecordedAt { get; init; }
}

public sealed class RuntimeAgentBootstrapReceiptSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-bootstrap-receipt.v1";

    public string SurfaceId { get; init; } = "runtime-agent-bootstrap-receipt";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public AgentBootstrapReceipt Receipt { get; init; } = new();
}

public sealed class AgentBootstrapReceipt
{
    public string ReceiptId { get; init; } = "agent-bootstrap-receipt";

    public int PolicyVersion { get; init; }

    public string PacketSurfaceId { get; init; } = string.Empty;

    public string TaskOverlaySurfaceId { get; init; } = string.Empty;

    public string RepoRoot { get; init; } = string.Empty;

    public string CurrentTaskId { get; init; } = "N/A";

    public string[] CurrentCardMemoryRefs { get; init; } = [];

    public AgentBootstrapRepoPosture RepoPosture { get; init; } = new();

    public AgentBootstrapHostSnapshot HostSnapshot { get; init; } = new();

    public string PostureBasis { get; init; } = "unknown";

    public DateTimeOffset VerifiedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ResumeDecision { get; init; } = "cold_init_required";

    public string ResumeReason { get; init; } = string.Empty;

    public string ComparisonStatus { get; init; } = "not_provided";

    public string ComparedReceiptPath { get; init; } = "N/A";

    public string[] RequiredReceiptFields { get; init; } = [];

    public string[] ValidationInspectCommands { get; init; } = [];

    public string[] WarmResumeChecks { get; init; } = [];

    public string[] InvalidationReasons { get; init; } = [];

    public string[] ColdInitTriggers { get; init; } = [];

    public AgentBootstrapHotPathContext HotPathContext { get; init; } = new();

    public AgentBootstrapWorkingContext WorkingContext { get; init; } = new();

    public AgentBootstrapResumeGuidance ResumeGuidance { get; init; } = new();
}

public sealed class AgentBootstrapWorkingContext
{
    public string Summary { get; init; } = string.Empty;

    public string DefaultEntryMode { get; init; } = "bootstrap_receipt_then_task_overlay";

    public string GovernanceBoundary { get; init; } = "working_context_is_projection_not_truth";

    public AgentBootstrapTaskSummary[] ActiveTasks { get; init; } = [];

    public AgentRecentExecutionSummary[] RecentExecutions { get; init; } = [];

    public string SafetyPosture { get; init; } = "not_evaluated";

    public string BackendPosture { get; init; } = "inspect runtime-agent-model-profile-routing";

    public string[] RecommendedNextCommands { get; init; } = [];

    public string[] EscalationTriggers { get; init; } = [];

    public string[] UnavailableChecks { get; init; } = [];
}

public sealed class AgentRecentExecutionSummary
{
    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = "N/A";

    public string Backend { get; init; } = "N/A";

    public string Status { get; init; } = "unknown";

    public string FailureKind { get; init; } = "none";

    public bool Retryable { get; init; }

    public string Summary { get; init; } = "N/A";

    public string DetailRef { get; init; } = "N/A";

    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class AgentBootstrapResumeGuidance
{
    public string Guidance { get; init; } = string.Empty;

    public bool MachineSurfaceFirst { get; init; }

    public string[] SkipActions { get; init; } = [];

    public string[] RequiredActions { get; init; } = [];

    public string[] EscalationTriggers { get; init; } = [];
}

public sealed class RuntimeAgentTaskBootstrapOverlaySurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-task-overlay.v1";

    public string SurfaceId { get; init; } = "runtime-agent-task-overlay";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public AgentTaskBootstrapOverlay Overlay { get; init; } = new();
}

public sealed class AgentTaskBootstrapOverlay
{
    public string OverlayId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string TaskStatus { get; init; } = string.Empty;

    public string[] ScopeFiles { get; init; } = [];

    public string[] ProtectedRoots { get; init; } = [];

    public string[] AllowedActions { get; init; } = [];

    public string[] RequiredVerification { get; init; } = [];

    public string[] StableEvidenceSurfaces { get; init; } = [];

    public string[] StopConditions { get; init; } = [];

    public string[] MemoryBundleRefs { get; init; } = [];

    public string[] Acceptance { get; init; } = [];

    public string[] Constraints { get; init; } = [];

    public AgentTaskBootstrapAcceptanceContractSummary AcceptanceContract { get; init; } = new();

    public AgentTaskScopeFileContext[] ScopeFileContexts { get; init; } = [];

    public AgentTaskSafetyBoundaryContext SafetyContext { get; init; } = new();

    public string[] EditableRoots { get; init; } = [];

    public string[] ReadOnlyRoots { get; init; } = [];

    public string[] TruthRoots { get; init; } = [];

    public string[] RepoMirrorRoots { get; init; } = [];

    public AgentTaskBootstrapValidationContext ValidationContext { get; init; } = new();

    public AgentTaskBootstrapWorkerSummary LastWorker { get; init; } = new();

    public AgentTaskBootstrapPlannerReviewSummary PlannerReview { get; init; } = new();

    public AgentTaskMarkdownReadGuidance MarkdownReadGuidance { get; init; } = new();
}

public sealed class AgentTaskBootstrapAcceptanceContractSummary
{
    public string BindingState { get; init; } = "missing";

    public string ContractId { get; init; } = "N/A";

    public string Status { get; init; } = "missing";

    public string Goal { get; init; } = "N/A";

    public string BusinessValue { get; init; } = "N/A";

    public string[] EvidenceRequired { get; init; } = [];

    public string[] MustNot { get; init; } = [];

    public string[] ArchitectureConstraints { get; init; } = [];

    public bool HumanReviewRequired { get; init; }

    public bool ProvisionalAllowed { get; init; }
}

public sealed class AgentTaskScopeFileContext
{
    public string Path { get; init; } = string.Empty;

    public bool Exists { get; init; }

    public string GitStatus { get; init; } = "not_checked";

    public string BoundaryClass { get; init; } = "scope";

    public string Source { get; init; } = "task_overlay";
}

public sealed class AgentTaskSafetyBoundaryContext
{
    public string Summary { get; init; } = string.Empty;

    public string LayerSummary { get; init; } = string.Empty;

    public AgentTaskSafetyLayerContext[] Layers { get; init; } = [];

    public string[] NonClaims { get; init; } = [];

    public string[] EditableRoots { get; init; } = [];

    public string[] ReadOnlyRoots { get; init; } = [];

    public string[] TruthRoots { get; init; } = [];

    public string[] RepoMirrorRoots { get; init; } = [];

    public string[] ProtectedRoots { get; init; } = [];

    public int MaxFilesChanged { get; init; }

    public int MaxLinesChanged { get; init; }

    public int MaxShellCommands { get; init; }

    public string[] RequiredValidation { get; init; } = [];

    public string[] WorkerAllowedActions { get; init; } = [];

    public string[] PlannerOnlyActions { get; init; } = [];

    public string[] StopConditions { get; init; } = [];
}

public sealed class AgentTaskSafetyLayerContext
{
    public string LayerId { get; init; } = string.Empty;

    public string Phase { get; init; } = string.Empty;

    public string Authority { get; init; } = string.Empty;

    public string EnforcementTiming { get; init; } = string.Empty;

    public string[] Enforces { get; init; } = [];

    public string[] NonClaims { get; init; } = [];
}

public sealed class AgentTaskMarkdownReadGuidance
{
    public string Summary { get; init; } = string.Empty;

    public string DefaultReadMode { get; init; } = "task_overlay_first_after_initialization";

    public string GovernanceBoundary { get; init; } = "task_refs_are_targeted_reads_not_new_truth";

    public string[] TaskScopedMarkdownRefs { get; init; } = [];

    public string[] RequiredBeforeEditRefs { get; init; } = [];

    public string[] ReplacementSurfaces { get; init; } = [];

    public string[] EscalationTriggers { get; init; } = [];
}

public sealed class AgentTaskBootstrapValidationContext
{
    public string[] Commands { get; init; } = [];

    public string[] Checks { get; init; } = [];

    public string[] ExpectedEvidence { get; init; } = [];

    public string[] RequiredVerification { get; init; } = [];
}

public sealed class AgentTaskBootstrapWorkerSummary
{
    public string RunId { get; init; } = "N/A";

    public string Backend { get; init; } = "N/A";

    public string FailureKind { get; init; } = "none";

    public bool Retryable { get; init; }

    public string Summary { get; init; } = "N/A";

    public string DetailRef { get; init; } = "N/A";

    public string ProviderDetailRef { get; init; } = "N/A";

    public string RecoveryAction { get; init; } = "none";

    public string RecoveryReason { get; init; } = "N/A";
}

public sealed class AgentTaskBootstrapPlannerReviewSummary
{
    public string Verdict { get; init; } = "continue";

    public string DecisionStatus { get; init; } = "needs_attention";

    public string Reason { get; init; } = string.Empty;

    public bool AcceptanceMet { get; init; }

    public bool BoundaryPreserved { get; init; } = true;

    public bool ScopeDriftDetected { get; init; }

    public string[] FollowUpSuggestions { get; init; } = [];
}

public sealed class RuntimeAgentModelProfileRoutingSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-model-profile-routing.v1";

    public string SurfaceId { get; init; } = "runtime-agent-model-profile-routing";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public AgentModelProfileRoutingSnapshot Routing { get; init; } = new();
}

public sealed class AgentModelProfileRoutingSnapshot
{
    public string Summary { get; init; } = string.Empty;

    public AgentGovernanceModelProfile[] Profiles { get; init; } = [];

    public AgentModelProfileLaneMatch[] AvailableLanes { get; init; } = [];
}

public sealed class AgentModelProfileLaneMatch
{
    public string LaneId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string MatchedProfileId { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

public sealed class RuntimeAgentLoopStallGuardSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-loop-stall-guard.v1";

    public string SurfaceId { get; init; } = "runtime-agent-loop-stall-guard";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public AgentLoopStallGuardEvaluation Guard { get; init; } = new();
}

public sealed class AgentLoopStallGuardEvaluation
{
    public string TaskId { get; init; } = string.Empty;

    public int DetectorWindow { get; init; }

    public string DetectionLineage { get; init; } = string.Empty;

    public ExecutionPattern Pattern { get; init; } = new();

    public AgentLoopStallProfileOutcome[] ProfileOutcomes { get; init; } = [];
}

public sealed class AgentLoopStallProfileOutcome
{
    public string ProfileId { get; init; } = string.Empty;

    public string ForcedAction { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

public sealed class RuntimeWeakModelExecutionLaneSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-weak-model-lane.v1";

    public string SurfaceId { get; init; } = "runtime-agent-weak-model-lane";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public AgentWeakModelExecutionLaneSnapshot LaneSnapshot { get; init; } = new();
}

public sealed class AgentWeakModelExecutionLaneSnapshot
{
    public string Summary { get; init; } = string.Empty;

    public AgentGovernanceWeakExecutionLane[] Lanes { get; init; } = [];

    public AgentModelProfileLaneMatch[] MatchedQualifiedLanes { get; init; } = [];
}
