using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Domain.Platform;

public enum ExecutionRunHistoricalExceptionCategory
{
    ApprovedWithoutValidation,
    SafetyBlockedReviewOverride,
    SubstrateFailureReviewOverride,
    HistoricalReviewStateMismatch,
}

public sealed class ExecutionRunHistoricalExceptionEntry
{
    public string TaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public DomainTaskStatus TaskStatus { get; init; } = DomainTaskStatus.Pending;

    public string LatestRunId { get; init; } = string.Empty;

    public string LatestRunStatus { get; init; } = string.Empty;

    public ReviewDecisionStatus ReviewDecisionStatus { get; init; } = ReviewDecisionStatus.NeedsAttention;

    public DomainTaskStatus ReviewResultingStatus { get; init; } = DomainTaskStatus.Pending;

    public bool ValidationPassed { get; init; }

    public SafetyOutcome SafetyOutcome { get; init; } = SafetyOutcome.Allow;

    public ExecutionRunHistoricalExceptionCategory[] Categories { get; init; } = [];

    public bool AutoReconcileEligible { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;

    public string? ReviewArtifactRef { get; init; }

    public string? RunArtifactRef { get; init; }
}

public sealed class ExecutionRunHistoricalExceptionReport
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string ReportId { get; init; } = $"execution-run-historical-exceptions-{Guid.NewGuid():N}";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public ExecutionRunHistoricalExceptionEntry[] Entries { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}
