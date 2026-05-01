namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase2NonInferiorityCohortFreezeResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase2-non-inferiority-cohort-freeze.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string CandidateMarkdownArtifactPath { get; init; } = string.Empty;

    public string CandidateJsonArtifactPath { get; init; } = string.Empty;

    public string RequestKindSliceProofMarkdownArtifactPath { get; init; } = string.Empty;

    public string RequestKindSliceProofJsonArtifactPath { get; init; } = string.Empty;

    public string RollbackPlanMarkdownArtifactPath { get; init; } = string.Empty;

    public string RollbackPlanJsonArtifactPath { get; init; } = string.Empty;

    public string WorkerRecollectMarkdownArtifactPath { get; init; } = string.Empty;

    public string WorkerRecollectJsonArtifactPath { get; init; } = string.Empty;

    public string TrustMarkdownArtifactPath { get; init; } = string.Empty;

    public string TrustJsonArtifactPath { get; init; } = string.Empty;

    public string TrustLineClassification { get; init; } = string.Empty;

    public string TargetSurface { get; init; } = string.Empty;

    public string CandidateStrategy { get; init; } = string.Empty;

    public bool NonInferiorityCohortFrozen { get; init; }

    public IReadOnlyList<string> TaskIds { get; init; } = Array.Empty<string>();

    public RuntimeTokenBaselineAttemptedTaskCohort AttemptedTaskCohort { get; init; } = new();

    public IReadOnlyList<RuntimeTokenPhase2RequestKindMixEntry> RequestKindMix { get; init; } = Array.Empty<RuntimeTokenPhase2RequestKindMixEntry>();

    public string Provider { get; init; } = string.Empty;

    public string ProviderApiVersion { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Tokenizer { get; init; } = string.Empty;

    public string TokenAccountingSourcePolicy { get; init; } = string.Empty;

    public string ContextWindowView { get; init; } = string.Empty;

    public string BillableCostView { get; init; } = string.Empty;

    public IReadOnlyList<string> ToolAvailability { get; init; } = Array.Empty<string>();

    public string RetrievalSnapshot { get; init; } = string.Empty;

    public IReadOnlyList<string> SuccessCriteria { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> HardFailConditions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenPhase2NonInferiorityThreshold> MetricThresholds { get; init; } = Array.Empty<RuntimeTokenPhase2NonInferiorityThreshold>();

    public string LowBaseCountRule { get; init; } = string.Empty;

    public string ManualReviewProtocol { get; init; } = string.Empty;

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenPhase2RequestKindMixEntry
{
    public string RequestKind { get; init; } = string.Empty;

    public int RequestCount { get; init; }

    public double RequestRatio { get; init; }
}

public sealed record RuntimeTokenPhase2NonInferiorityThreshold
{
    public string MetricId { get; init; } = string.Empty;

    public string ThresholdKind { get; init; } = string.Empty;

    public string Comparator { get; init; } = string.Empty;

    public double ThresholdValue { get; init; }

    public string Units { get; init; } = string.Empty;
}
