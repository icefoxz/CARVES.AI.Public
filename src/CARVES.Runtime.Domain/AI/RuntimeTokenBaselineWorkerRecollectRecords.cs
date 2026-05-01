namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenBaselineWorkerRecollectResult
{
    public string SchemaVersion { get; init; } = "runtime-token-baseline-worker-recollect-result.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset RecollectedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortJsonArtifactPath { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public RuntimeTokenBaselineCohortFreeze Cohort { get; init; } = new();

    public int RequestedTaskCount { get; init; }

    public int RecollectedTaskCount { get; init; }

    public int AttributionRecordCount { get; init; }

    public int DirectToLlmRouteEdgeCount { get; init; }

    public IReadOnlyList<string> TaskIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AttributionIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenBaselineWorkerRecollectTaskRecord> Tasks { get; init; } = Array.Empty<RuntimeTokenBaselineWorkerRecollectTaskRecord>();

    public RuntimeTokenBaselineAttemptedTaskCohort AttemptedTaskCohort { get; init; } = new();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTokenBaselineWorkerRecollectTaskRecord
{
    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string RequestId { get; init; } = string.Empty;

    public string AttributionId { get; init; } = string.Empty;

    public string PacketArtifactPath { get; init; } = string.Empty;

    public string ContextPackArtifactPath { get; init; } = string.Empty;

    public string Consumer { get; init; } = string.Empty;

    public string TokenAccountingSource { get; init; } = "local_estimate";

    public DateTimeOffset RecordedAtUtc { get; init; }
}

public sealed record RuntimeTokenBaselineAttemptedTaskCohort
{
    public string SelectionMode { get; init; } = "frozen_worker_recollect_task_set";

    public bool CoversFrozenReplayTaskSet { get; init; }

    public int AttemptedTaskCount { get; init; }

    public int SuccessfulAttemptedTaskCount { get; init; }

    public int FailedAttemptedTaskCount { get; init; }

    public int IncompleteAttemptedTaskCount { get; init; }

    public IReadOnlyList<string> AttemptedTaskIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RuntimeTokenBaselineAttemptedTaskRecord> Tasks { get; init; } = Array.Empty<RuntimeTokenBaselineAttemptedTaskRecord>();
}

public sealed record RuntimeTokenBaselineAttemptedTaskRecord
{
    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string WorkerBackend { get; init; } = string.Empty;

    public string TaskStatus { get; init; } = string.Empty;

    public string LatestRunStatus { get; init; } = string.Empty;

    public bool Attempted { get; init; }

    public bool SuccessfulAttempted { get; init; }

    public bool ReviewAdmissionAccepted { get; init; }

    public bool ConstraintViolationObserved { get; init; }

    public double RetryCount { get; init; }

    public double RepairCount { get; init; }
}
