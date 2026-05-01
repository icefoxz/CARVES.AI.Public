namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase3PostRolloutEvidenceResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase3-post-rollout-evidence.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset CollectedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string MainPathReplacementReviewMarkdownArtifactPath { get; init; } = string.Empty;

    public string MainPathReplacementReviewJsonArtifactPath { get; init; } = string.Empty;

    public string ReplacementScopeFreezeMarkdownArtifactPath { get; init; } = string.Empty;

    public string ReplacementScopeFreezeJsonArtifactPath { get; init; } = string.Empty;

    public string WorkerRecollectMarkdownArtifactPath { get; init; } = string.Empty;

    public string WorkerRecollectJsonArtifactPath { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = string.Empty;

    public string ObservationMode { get; init; } = "limited_main_path_default_replay";

    public string EvidenceStatus { get; init; } = "incomplete_post_rollout_evidence";

    public RuntimeTokenPhase2ExecutionTruthScope ExecutionTruthScope { get; init; } = new();

    public RuntimeTokenBaselineAttemptedTaskCohort AttemptedTaskCohort { get; init; } = new();

    public RuntimeTokenPhase3PostRolloutScope RolloutScope { get; init; } = new();

    public RuntimeTokenPhase3PostRolloutTokenEvidence TokenEvidence { get; init; } = new();

    public RuntimeTokenPhase3PostRolloutBehaviorEvidence BehaviorEvidence { get; init; } = new();

    public RuntimeTokenPhase3PostRolloutSafetyEvidence Safety { get; init; } = new();

    public bool LimitedMainPathImplementationObserved { get; init; }

    public bool PostRolloutTokenEvidenceObserved { get; init; }

    public bool PostRolloutBehaviorEvidenceObserved { get; init; }

    public IReadOnlyList<RuntimeTokenPhase3PostRolloutSample> Samples { get; init; } = Array.Empty<RuntimeTokenPhase3PostRolloutSample>();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NextRequiredActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase3PostRolloutScope
{
    public string RequestKind { get; init; } = string.Empty;

    public string Surface { get; init; } = string.Empty;

    public string ExecutionMode { get; init; } = "no_provider_agent_mediated";

    public string WorkerBackend { get; init; } = "null_worker";

    public bool DefaultEnabled { get; init; }

    public bool FullRollout { get; init; }

    public string AllowlistMode { get; init; } = "frozen_scope";
}

public sealed record RuntimeTokenPhase3PostRolloutTokenEvidence
{
    public int BaselineRequestCount { get; init; }

    public int CandidateDefaultRequestCount { get; init; }

    public int FallbackRequestCount { get; init; }

    public double PostRolloutWholeRequestReductionP95 { get; init; }

    public double BaselineTotalTokensPerSuccessfulTask { get; init; }

    public double CandidateTotalTokensPerSuccessfulTask { get; init; }

    public double DeltaTotalTokensPerSuccessfulTask { get; init; }

    public double RelativeChangeTotalTokensPerSuccessfulTask { get; init; }

    public double BaselineContextWindowInputTokensP95 { get; init; }

    public double CandidateContextWindowInputTokensP95 { get; init; }

    public double DeltaContextWindowInputTokensP95 { get; init; }

    public double BaselineBillableInputTokensUncachedP95 { get; init; }

    public double CandidateBillableInputTokensUncachedP95 { get; init; }

    public double DeltaBillableInputTokensUncachedP95 { get; init; }
}

public sealed record RuntimeTokenPhase3PostRolloutBehaviorEvidence
{
    public int AttemptedTaskCount { get; init; }

    public int SuccessfulAttemptedTaskCount { get; init; }

    public int FailedAttemptedTaskCount { get; init; }

    public int IncompleteAttemptedTaskCount { get; init; }

    public double? BaselineTaskSuccessRate { get; init; }

    public double? CandidateTaskSuccessRate { get; init; }

    public double? TaskSuccessRateDeltaPercentagePoints { get; init; }

    public double? BaselineReviewAdmissionRate { get; init; }

    public double? CandidateReviewAdmissionRate { get; init; }

    public double? ReviewAdmissionRateDeltaPercentagePoints { get; init; }

    public double? BaselineConstraintViolationRate { get; init; }

    public double? CandidateConstraintViolationRate { get; init; }

    public double? ConstraintViolationRateDeltaPercentagePoints { get; init; }

    public double? BaselineRetryCountPerTask { get; init; }

    public double? CandidateRetryCountPerTask { get; init; }

    public double? RetryCountPerTaskRelativeDelta { get; init; }

    public double? BaselineRepairCountPerTask { get; init; }

    public double? CandidateRepairCountPerTask { get; init; }

    public double? RepairCountPerTaskRelativeDelta { get; init; }

    public bool Observed { get; init; }

    public IReadOnlyList<string> UnavailableMetrics { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase3PostRolloutSafetyEvidence
{
    public int HardFailCount { get; init; }

    public bool RollbackTriggered { get; init; }

    public bool KillSwitchUsed { get; init; }

    public IReadOnlyList<string> HardFailConditionsTriggered { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase3PostRolloutSample
{
    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string BaselineRequestId { get; init; } = string.Empty;

    public string CandidateRequestId { get; init; } = string.Empty;

    public string BaselineDecisionMode { get; init; } = string.Empty;

    public string CandidateDecisionMode { get; init; } = string.Empty;

    public string CandidateDecisionReason { get; init; } = string.Empty;

    public bool CandidateDefaultApplied { get; init; }

    public int BaselineWholeRequestTokens { get; init; }

    public int CandidateWholeRequestTokens { get; init; }

    public double WholeRequestReductionRatio { get; init; }
}
