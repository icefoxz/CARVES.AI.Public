using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Domain.Platform;

public enum RoutingValidationMode
{
    Baseline,
    Routing,
    ForcedFallback,
}

public enum RouteEligibilityStatus
{
    Eligible,
    TemporarilyIneligible,
    Exhausted,
    Unsupported,
}

public enum RouteQuotaState
{
    Unknown,
    Healthy,
    Exhausted,
}

public sealed class RouteSelectionSignals
{
    public string RouteHealth { get; init; } = "unknown";

    public RouteQuotaState QuotaState { get; init; } = RouteQuotaState.Unknown;

    public bool TokenBudgetFit { get; init; } = true;

    public long? RecentLatencyMs { get; init; }

    public int RecentFailureCount { get; init; }
}

public sealed class RoutingValidationCatalog
{
    public string SchemaVersion { get; init; } = "routing-validation-catalog.v1";

    public string CatalogId { get; init; } = "current-connected-routing-validation";

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public RoutingValidationTaskDefinition[] Tasks { get; init; } = [];
}

public sealed class RoutingValidationTaskDefinition
{
    public string TaskId { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public string RoutingIntent { get; init; } = string.Empty;

    public string? ModuleId { get; init; }

    public string Prompt { get; init; } = string.Empty;

    public ModelQualificationExpectedFormat ExpectedFormat { get; init; } = ModelQualificationExpectedFormat.Text;

    public string[] RequiredJsonFields { get; init; } = [];

    public string RiskLevel { get; init; } = "low";

    public string BaselineLaneId { get; init; } = string.Empty;

    public string? Summary { get; init; }
}

public sealed class RoutingValidationTrace
{
    public string SchemaVersion { get; init; } = "routing-validation-trace.v1";

    public string TraceId { get; init; } = $"val-trace-{Guid.NewGuid():N}";

    public string RunId { get; init; } = $"val-run-{Guid.NewGuid():N}";

    public string TaskId { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public string RoutingIntent { get; init; } = string.Empty;

    public string? ModuleId { get; init; }

    public RoutingValidationMode ExecutionMode { get; init; }

    public string? RoutingProfileId { get; init; }

    public string RouteSource { get; init; } = string.Empty;

    public string? SelectedProvider { get; init; }

    public string? SelectedLane { get; init; }

    public string? SelectedBackend { get; init; }

    public string? SelectedModel { get; init; }

    public string? SelectedRoutingProfileId { get; init; }

    public string? AppliedRoutingRuleId { get; init; }

    public string? CodexThreadId { get; init; }

    public WorkerThreadContinuity CodexThreadContinuity { get; init; } = WorkerThreadContinuity.None;

    public bool FallbackConfigured { get; init; }

    public bool FallbackTriggered { get; init; }

    public RouteEligibilityStatus? PreferredRouteEligibility { get; init; }

    public string? PreferredIneligibilityReason { get; init; }

    public string[] SelectedBecause { get; init; } = [];

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset EndedAt { get; init; } = DateTimeOffset.UtcNow;

    public long LatencyMs { get; init; }

    public bool RequestSucceeded { get; init; }

    public bool TaskSucceeded { get; init; }

    public bool SchemaValid { get; init; }

    public RoutingValidationExecutionOutcome BuildOutcome { get; init; } = RoutingValidationExecutionOutcome.NotRun;

    public RoutingValidationExecutionOutcome TestOutcome { get; init; } = RoutingValidationExecutionOutcome.NotRun;

    public RoutingValidationExecutionOutcome SafetyOutcome { get; init; } = RoutingValidationExecutionOutcome.NotRun;

    public int RetryCount { get; init; }

    public bool PatchAccepted { get; init; }

    public int? PromptTokens { get; init; }

    public int? CompletionTokens { get; init; }

    public decimal? EstimatedCostUsd { get; init; }

    public string? FailureCategory { get; init; }

    public string[] ReasonCodes { get; init; } = [];

    public string[] ArtifactPaths { get; init; } = [];

    public RoutingValidationCandidateSnapshot[] Candidates { get; init; } = [];
}

public sealed class RoutingValidationCandidateSnapshot
{
    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string? RoutingProfileId { get; init; }

    public string? RoutingRuleId { get; init; }

    public string RouteDisposition { get; init; } = "none";

    public RouteEligibilityStatus Eligibility { get; init; } = RouteEligibilityStatus.Unsupported;

    public bool Selected { get; init; }

    public RouteSelectionSignals Signals { get; init; } = new();

    public string Reason { get; init; } = string.Empty;
}

public sealed class RoutingValidationSummary
{
    public string SchemaVersion { get; init; } = "routing-validation-summary.v1";

    public string SummaryId { get; init; } = $"val-summary-{Guid.NewGuid():N}";

    public string RunId { get; init; } = string.Empty;

    public string? RoutingProfileId { get; init; }

    public RoutingValidationMode ExecutionMode { get; init; } = RoutingValidationMode.Routing;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public int Tasks { get; init; }

    public double SuccessRate { get; init; }

    public double SchemaValidityRate { get; init; }

    public double FallbackRate { get; init; }

    public double BuildPassRate { get; init; }

    public double TestPassRate { get; init; }

    public double SafetyPassRate { get; init; }

    public double AverageLatencyMs { get; init; }

    public decimal TotalEstimatedCostUsd { get; init; }

    public RoutingValidationRouteBreakdown[] RouteBreakdown { get; init; } = [];
}

public enum RoutingValidationExecutionOutcome
{
    NotRun,
    Passed,
    Failed,
    Rejected,
}

public sealed class RoutingValidationRouteBreakdown
{
    public string TaskFamily { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string? SelectedLane { get; init; }

    public string? SelectedModel { get; init; }

    public int Samples { get; init; }

    public double SuccessRate { get; init; }

    public double PatchAcceptanceRate { get; init; }

    public double AverageRetryCount { get; init; }

    public double AverageLatencyMs { get; init; }
}

public sealed class RoutingValidationHistory
{
    public string SchemaVersion { get; init; } = "routing-validation-history.v1";

    public string HistoryId { get; init; } = $"val-history-{Guid.NewGuid():N}";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public int BatchCount { get; init; }

    public string? LatestRunId { get; init; }

    public RoutingValidationSummary[] Batches { get; init; } = [];
}

public sealed class ValidationCoverageMatrix
{
    public string SchemaVersion { get; init; } = "validation-coverage-matrix.v1";

    public string MatrixId { get; init; } = $"validation-coverage-{Guid.NewGuid():N}";

    public string CandidateId { get; init; } = string.Empty;

    public string ProfileId { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public int ValidationBatchCount { get; init; }

    public ValidationCoverageFamily[] Families { get; init; } = [];

    public ValidationCoverageGap[] MissingEvidence { get; init; } = [];
}

public sealed class ValidationCoverageFamily
{
    public string TaskFamily { get; init; } = string.Empty;

    public string RoutingIntent { get; init; } = string.Empty;

    public string? ModuleId { get; init; }

    public string[] TaskIds { get; init; } = [];

    public string PreferredLaneId { get; init; } = string.Empty;

    public string[] FallbackLaneIds { get; init; } = [];

    public int BaselineTraceCount { get; init; }

    public int RoutingTraceCount { get; init; }

    public int FallbackTraceCount { get; init; }

    public bool BaselineCovered { get; init; }

    public bool RoutingCovered { get; init; }

    public bool FallbackRequired { get; init; }

    public bool FallbackCovered { get; init; }

    public ValidationCoverageGap[] MissingEvidence { get; init; } = [];
}

public sealed class ValidationCoverageGap
{
    public string TaskFamily { get; init; } = string.Empty;

    public string RoutingIntent { get; init; } = string.Empty;

    public string? ModuleId { get; init; }

    public RoutingValidationMode RequiredMode { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public string[] TaskIds { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}

public sealed class RoutingPromotionDecision
{
    public string SchemaVersion { get; init; } = "routing-promotion-decision.v1";

    public string DecisionId { get; init; } = $"routing-promotion-{Guid.NewGuid():N}";

    public string CandidateId { get; init; } = string.Empty;

    public string ProfileId { get; init; } = string.Empty;

    public string SourceRunId { get; init; } = string.Empty;

    public bool Eligible { get; init; }

    public bool MultiBatchEvidence { get; init; }

    public int EvidenceBatchCount { get; init; }

    public int BaselineComparisonCount { get; init; }

    public int RoutingEvidenceCount { get; init; }

    public int FallbackEvidenceCount { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string[] ReasonCodes { get; init; } = [];

    public RoutingPromotionIntentDecision[] Intents { get; init; } = [];
}

public sealed class RoutingCandidateReadiness
{
    public string SchemaVersion { get; init; } = "routing-candidate-readiness.v1";

    public string ReadinessId { get; init; } = $"routing-readiness-{Guid.NewGuid():N}";

    public string CandidateId { get; init; } = string.Empty;

    public string ProfileId { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Status { get; init; } = "not_ready";

    public bool PromotionEligible { get; init; }

    public int ValidationBatchCount { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string[] CoveredTaskFamilies { get; init; } = [];

    public ValidationCoverageGap[] MissingEvidence { get; init; } = [];

    public string[] BlockingReasons { get; init; } = [];

    public string[] RecommendedNextActions { get; init; } = [];

    public RoutingCandidateReadinessFamily[] Families { get; init; } = [];
}

public sealed class RoutingCandidateReadinessFamily
{
    public string TaskFamily { get; init; } = string.Empty;

    public string RoutingIntent { get; init; } = string.Empty;

    public string? ModuleId { get; init; }

    public string Status { get; init; } = "not_ready";

    public bool BaselineCovered { get; init; }

    public bool RoutingCovered { get; init; }

    public bool FallbackRequired { get; init; }

    public bool FallbackCovered { get; init; }

    public bool MultiBatchCovered { get; init; }

    public ValidationCoverageGap[] MissingEvidence { get; init; } = [];

    public string[] BlockingReasons { get; init; } = [];

    public string[] RecommendedNextActions { get; init; } = [];
}

public sealed class RoutingPromotionIntentDecision
{
    public string RoutingIntent { get; init; } = string.Empty;

    public string? ModuleId { get; init; }

    public string PreferredLaneId { get; init; } = string.Empty;

    public string[] FallbackLaneIds { get; init; } = [];

    public string? BaselineTraceId { get; init; }

    public string? RoutingTraceId { get; init; }

    public string? FallbackTraceId { get; init; }

    public bool Eligible { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string[] ReasonCodes { get; init; } = [];
}
