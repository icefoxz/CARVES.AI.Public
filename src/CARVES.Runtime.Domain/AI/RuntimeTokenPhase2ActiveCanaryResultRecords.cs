namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase2ActiveCanaryResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase2-active-canary-result.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string ExecutionApprovalMarkdownArtifactPath { get; init; } = string.Empty;

    public string ExecutionApprovalJsonArtifactPath { get; init; } = string.Empty;

    public string BaselineEvidenceMarkdownArtifactPath { get; init; } = string.Empty;

    public string BaselineEvidenceJsonArtifactPath { get; init; } = string.Empty;

    public string NonInferiorityCohortMarkdownArtifactPath { get; init; } = string.Empty;

    public string NonInferiorityCohortJsonArtifactPath { get; init; } = string.Empty;

    public string WorkerRecollectMarkdownArtifactPath { get; init; } = string.Empty;

    public string WorkerRecollectJsonArtifactPath { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = string.Empty;

    public string ObservationMode { get; init; } = "controlled_worker_request_path_replay";

    public RuntimeTokenPhase2ActiveCanaryScope CanaryScope { get; init; } = new();

    public RuntimeTokenPhase2ExecutionTruthScope ExecutionTruthScope { get; init; } = new();

    public RuntimeTokenBaselineAttemptedTaskCohort AttemptedTaskCohort { get; init; } = new();

    public RuntimeTokenPhase2ActiveCanaryTokenMetrics TokenMetrics { get; init; } = new();

    public RuntimeTokenPhase2ActiveCanaryNonInferiorityResult NonInferiority { get; init; } = new();

    public RuntimeTokenPhase2ActiveCanarySafetyResult Safety { get; init; } = new();

    public string Decision { get; init; } = "inconclusive";

    public IReadOnlyList<RuntimeTokenPhase2ActiveCanarySample> Samples { get; init; } = Array.Empty<RuntimeTokenPhase2ActiveCanarySample>();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NextRequiredActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase2ExecutionTruthScope
{
    public string ExecutionMode { get; init; } = "no_provider_agent_mediated";

    public string WorkerBackend { get; init; } = "null_worker";

    public bool ProviderSdkExecutionRequired { get; init; }

    public string ProviderModelBehaviorClaim { get; init; } = "not_claimed";

    public string BehavioralNonInferiorityScope { get; init; } = "current_runtime_mode_only";

    public string ProviderBilledCostClaim { get; init; } = "not_applicable";
}

public sealed record RuntimeTokenPhase2ActiveCanaryScope
{
    public IReadOnlyList<string> RequestKinds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> SurfaceAllowlist { get; init; } = Array.Empty<string>();

    public bool DefaultEnabled { get; init; }

    public string AllowlistMode { get; init; } = "explicit";
}

public sealed record RuntimeTokenPhase2ActiveCanaryTokenMetrics
{
    public int BaselineRequestCount { get; init; }

    public int CandidateRequestCount { get; init; }

    public double TargetSurfaceReductionRatioP95 { get; init; }

    public double TargetSurfaceShareP95 { get; init; }

    public double ExpectedWholeRequestReductionP95 { get; init; }

    public double ObservedWholeRequestReductionP95 { get; init; }

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

public sealed record RuntimeTokenPhase2ActiveCanaryNonInferiorityResult
{
    public double? TaskSuccessRateDeltaPercentagePoints { get; init; }

    public double? ReviewAdmissionRateDeltaPercentagePoints { get; init; }

    public double? ConstraintViolationRateDeltaPercentagePoints { get; init; }

    public double? RetryCountPerTaskRelativeDelta { get; init; }

    public double? RepairCountPerTaskRelativeDelta { get; init; }

    public double ProviderContextCapHitRateDeltaPercentagePoints { get; init; }

    public double InternalPromptBudgetCapHitRateDeltaPercentagePoints { get; init; }

    public double SectionBudgetCapHitRateDeltaPercentagePoints { get; init; }

    public double TrimLoopCapHitRateDeltaPercentagePoints { get; init; }

    public bool SampleSizeSufficient { get; init; }

    public bool ManualReviewRequired { get; init; }

    public bool Passed { get; init; }

    public IReadOnlyList<string> UnavailableMetrics { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenPhase2ActiveCanaryThresholdEvaluation> ThresholdEvaluations { get; init; } = Array.Empty<RuntimeTokenPhase2ActiveCanaryThresholdEvaluation>();
}

public sealed record RuntimeTokenPhase2ActiveCanaryThresholdEvaluation
{
    public string MetricId { get; init; } = string.Empty;

    public string ThresholdKind { get; init; } = string.Empty;

    public string Comparator { get; init; } = string.Empty;

    public double ThresholdValue { get; init; }

    public string Units { get; init; } = string.Empty;

    public double? BaselineValue { get; init; }

    public double? CandidateValue { get; init; }

    public double? DeltaValue { get; init; }

    public bool Evaluated { get; init; }

    public bool Passed { get; init; }

    public string? Reason { get; init; }
}

public sealed record RuntimeTokenPhase2ActiveCanarySafetyResult
{
    public int HardFailCount { get; init; }

    public bool RollbackTriggered { get; init; }

    public bool ManualReviewRequired { get; init; }

    public IReadOnlyList<string> HardFailConditionsTriggered { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase2ActiveCanarySample
{
    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string BaselineRequestId { get; init; } = string.Empty;

    public string CandidateRequestId { get; init; } = string.Empty;

    public string BaselineDecisionReason { get; init; } = string.Empty;

    public string CandidateDecisionReason { get; init; } = string.Empty;

    public bool CandidateApplied { get; init; }

    public int BaselineInstructionsTokens { get; init; }

    public int CandidateInstructionsTokens { get; init; }

    public int BaselineWholeRequestTokens { get; init; }

    public int CandidateWholeRequestTokens { get; init; }

    public double TargetSurfaceReductionRatio { get; init; }

    public double WholeRequestReductionRatio { get; init; }
}
