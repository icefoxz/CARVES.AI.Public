using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Tasks;

public sealed class PlannerReviewArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string TaskId { get; init; } = string.Empty;

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public PlannerReview Review { get; init; } = new();

    public TaskStatus ResultingStatus { get; init; } = TaskStatus.Pending;

    public string TransitionReason { get; init; } = string.Empty;

    public string PlannerComment { get; init; } = string.Empty;

    public string PatchSummary { get; init; } = string.Empty;

    public string? ResultCommit { get; init; }

    public bool ValidationPassed { get; init; }

    public IReadOnlyList<string> ValidationEvidence { get; init; } = Array.Empty<string>();

    public SafetyOutcome SafetyOutcome { get; init; } = SafetyOutcome.Allow;

    public IReadOnlyList<string> SafetyIssues { get; init; } = Array.Empty<string>();

    public ReviewDecisionStatus DecisionStatus { get; init; } = ReviewDecisionStatus.NeedsAttention;

    public string? DecisionReason { get; init; }

    public DateTimeOffset? DecisionAt { get; init; }

    public ReviewDecisionDebt? DecisionDebt { get; init; }

    public ReviewWritebackRecord Writeback { get; init; } = new();

    public ReviewRealityProjection RealityProjection { get; init; } = new();

    public ReviewClosureBundle ClosureBundle { get; init; } = new();
}

public sealed class ReviewRealityProjection
{
    public SolidityClass SolidityClass { get; init; } = SolidityClass.Ghost;

    public string PromotionResult { get; init; } = "not_proven";

    public string PlannedScope { get; init; } = string.Empty;

    public string VerifiedOutcome { get; init; } = string.Empty;

    public RealityProofTarget? ProofTarget { get; init; }
}

public sealed class ReviewClosureBundle
{
    public string SchemaVersion { get; init; } = "review-closure-bundle.v1";

    public string CandidateResultSource { get; init; } = "worker";

    public string WorkerResultVerdict { get; init; } = "review_pending";

    public string AcceptedPatchSource { get; init; } = "none";

    public string CompletionMode { get; init; } = "worker_review_pending";

    public string ReviewerDecision { get; init; } = "pending_review";

    public ReviewClosureValidationSummary Validation { get; init; } = new();

    public ReviewClosureCompletionClaimSummary CompletionClaim { get; init; } = new();

    public ReviewClosureHostValidationSummary HostValidation { get; init; } = new();

    public ReviewClosureFallbackRunPacketSummary FallbackRunPacket { get; init; } = new();

    public ReviewClosureContractMatrix ContractMatrix { get; init; } = new();

    public ReviewClosureDecision ClosureDecision { get; init; } = new();

    public string WritebackRecommendation { get; init; } = "review_required_before_writeback";
}

public sealed class ReviewClosureCompletionClaimSummary
{
    public string Status { get; init; } = "not_recorded";

    public bool Required { get; init; }

    public string? PacketId { get; init; }

    public string? SourceExecutionPacketId { get; init; }

    public bool ClaimIsTruth { get; init; }

    public bool HostValidationRequired { get; init; } = true;

    public string PacketValidationStatus { get; init; } = "not_evaluated";

    public IReadOnlyList<string> PacketValidationBlockers { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PresentFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ContractItemsSatisfied { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredContractItems { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingContractItems { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TestsRun { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EvidencePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DisallowedChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ForbiddenVocabularyHits { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> KnownLimitations { get; init; } = Array.Empty<string>();

    public string NextRecommendation { get; init; } = string.Empty;

    public string? RawClaimHash { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    public static ReviewClosureCompletionClaimSummary FromWorkerClaim(WorkerCompletionClaim claim)
    {
        return new ReviewClosureCompletionClaimSummary
        {
            Status = claim.Status,
            Required = claim.Required,
            PacketId = claim.PacketId,
            SourceExecutionPacketId = claim.SourceExecutionPacketId,
            ClaimIsTruth = claim.ClaimIsTruth,
            HostValidationRequired = claim.HostValidationRequired,
            PacketValidationStatus = claim.PacketValidationStatus,
            PacketValidationBlockers = claim.PacketValidationBlockers,
            PresentFields = claim.PresentFields,
            MissingFields = claim.MissingFields,
            ChangedFiles = claim.ChangedFiles,
            ContractItemsSatisfied = claim.ContractItemsSatisfied,
            RequiredContractItems = claim.RequiredContractItems,
            MissingContractItems = claim.MissingContractItems,
            TestsRun = claim.TestsRun,
            EvidencePaths = claim.EvidencePaths,
            DisallowedChangedFiles = claim.DisallowedChangedFiles,
            ForbiddenVocabularyHits = claim.ForbiddenVocabularyHits,
            KnownLimitations = claim.KnownLimitations,
            NextRecommendation = claim.NextRecommendation,
            RawClaimHash = claim.RawClaimHash,
            Notes = claim.Notes,
        };
    }
}

public sealed class ReviewClosureHostValidationSummary
{
    public string SchemaVersion { get; init; } = "review-closure-host-validation.v1";

    public string Status { get; init; } = "not_evaluated";

    public bool Required { get; init; }

    public string ReasonCode { get; init; } = "not_evaluated";

    public string Message { get; init; } = "Host validation has not been evaluated for this review bundle.";

    public string? WorkerPacketId { get; init; }

    public string? SourceExecutionPacketId { get; init; }

    public string CompletionClaimStatus { get; init; } = "not_recorded";

    public string CompletionClaimPacketValidationStatus { get; init; } = "not_evaluated";

    public bool CompletionClaimHostValidationRequired { get; init; } = true;

    public bool CompletionClaimClaimsTruthAuthority { get; init; }

    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EvidenceRefs { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed class ReviewClosureFallbackRunPacketSummary
{
    public string SchemaVersion { get; init; } = "review-closure-fallback-run-packet.v1";

    public bool Required { get; init; }

    public string Status { get; init; } = "not_required";

    public bool StrictlyRequired { get; init; }

    public bool ClosureBlockerWhenIncomplete { get; init; }

    public IReadOnlyList<string> RequiredReceipts { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PresentReceipts { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingReceipts { get; init; } = Array.Empty<string>();

    public string? RoleSwitchReceiptRef { get; init; }

    public string? ContextReceiptRef { get; init; }

    public string? ExecutionClaimRef { get; init; }

    public string? ReviewBundleRef { get; init; }

    public bool GrantsExecutionAuthority { get; init; }

    public bool GrantsTruthWriteAuthority { get; init; }

    public bool CreatesTaskQueue { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed class ReviewClosureValidationSummary
{
    public string RequiredGateStatus { get; init; } = "not_evaluated";

    public string AdvisoryGateStatus { get; init; } = "not_recorded";

    public string KnownRedBaselineStatus { get; init; } = "not_recorded";

    public int EvidenceCount { get; init; }

    public ReviewClosureValidationProfile Profile { get; init; } = new();
}

public sealed class ReviewClosureValidationProfile
{
    public string ProfileId { get; init; } = "task_validation_profile_v1";

    public string Status { get; init; } = "not_evaluated";

    public string TouchedScope { get; init; } = "not_recorded";

    public string FailureAttribution { get; init; } = "not_evaluated";

    public IReadOnlyList<ReviewClosureValidationGate> RequiredGates { get; init; } = Array.Empty<ReviewClosureValidationGate>();

    public IReadOnlyList<ReviewClosureValidationGate> AdvisoryGates { get; init; } = Array.Empty<ReviewClosureValidationGate>();

    public IReadOnlyList<string> KnownRedBaseline { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed class ReviewClosureValidationGate
{
    public string GateId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = "not_evaluated";

    public bool Blocking { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
}

public sealed class ReviewClosureDecision
{
    public string SchemaVersion { get; init; } = "review-closure-decision.v1";

    public string Status { get; init; } = "writeback_blocked";

    public string Decision { get; init; } = "block_writeback";

    public bool WritebackAllowed { get; init; }

    public string ResultSource { get; init; } = "worker";

    public string AcceptedPatchSource { get; init; } = "none";

    public string WorkerResultVerdict { get; init; } = "review_pending";

    public string ReviewerDecision { get; init; } = "pending_review";

    public string RequiredGateStatus { get; init; } = "not_evaluated";

    public string ContractMatrixStatus { get; init; } = "not_evaluated";

    public string SafetyStatus { get; init; } = "not_evaluated";

    public string HostValidationStatus { get; init; } = "not_evaluated";

    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed class ReviewClosureContractMatrix
{
    public string ProfileId { get; init; } = "generic_review_closure_v1";

    public string Status { get; init; } = "not_evaluated";

    public IReadOnlyList<ReviewClosureContractCheck> Checks { get; init; } = Array.Empty<ReviewClosureContractCheck>();

    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed class ReviewClosureContractCheck
{
    public string CheckId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = "not_evaluated";

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
}
