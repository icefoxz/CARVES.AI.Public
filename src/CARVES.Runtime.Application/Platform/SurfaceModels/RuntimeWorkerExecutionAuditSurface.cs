namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeWorkerExecutionAuditSurface
{
    public string SchemaVersion { get; init; } = "runtime-worker-execution-audit.v1";

    public string SurfaceId { get; init; } = "runtime-worker-execution-audit";

    public string Summary { get; init; } = string.Empty;

    public string StoragePath { get; init; } = string.Empty;

    public bool ReadModelConfigured { get; init; }

    public bool StorageExists { get; init; }

    public bool Available { get; init; }

    public string AvailabilityStatus { get; init; } = string.Empty;

    public RuntimeWorkerExecutionAuditQuerySurface Query { get; init; } = new();

    public RuntimeWorkerExecutionAuditSummarySurface Counts { get; init; } = new();

    public RuntimeWorkerExecutionAuditSummarySurface QueryCounts { get; init; } = new();

    public IReadOnlyList<RuntimeWorkerExecutionAuditEntrySurface> RecentEntries { get; init; } = [];

    public IReadOnlyList<string> SupportedQueryFields { get; init; } = [];

    public IReadOnlyList<string> QueryExamples { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed class RuntimeWorkerExecutionAuditQuerySurface
{
    public string RequestedQuery { get; init; } = "recent";

    public string EffectiveQuery { get; init; } = "recent limit:10";

    public string QueryMode { get; init; } = "indexed";

    public bool Filtered { get; init; }

    public int Limit { get; init; } = 10;

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public string? Status { get; init; }

    public string? EventType { get; init; }

    public string? BackendId { get; init; }

    public string? ProviderId { get; init; }

    public bool? SafetyAllowed { get; init; }

    public IReadOnlyList<string> UnsupportedTerms { get; init; } = [];

    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
}

public sealed class RuntimeWorkerExecutionAuditSummarySurface
{
    public int TotalExecutions { get; init; }

    public int SucceededExecutions { get; init; }

    public int FailedExecutions { get; init; }

    public int BlockedExecutions { get; init; }

    public int SkippedExecutions { get; init; }

    public int ApprovalWaitExecutions { get; init; }

    public int SafetyBlockedExecutions { get; init; }

    public int PermissionRequestCount { get; init; }

    public int ChangedFilesCount { get; init; }

    public DateTimeOffset? LatestOccurrenceUtc { get; init; }

    public string? LatestTaskId { get; init; }
}

public sealed class RuntimeWorkerExecutionAuditEntrySurface
{
    public long? SequenceId { get; init; }

    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string AdapterId { get; init; } = string.Empty;

    public string? ProtocolFamily { get; init; }

    public string Status { get; init; } = string.Empty;

    public string FailureKind { get; init; } = string.Empty;

    public string FailureLayer { get; init; } = string.Empty;

    public int ChangedFilesCount { get; init; }

    public int ObservedChangedFilesCount { get; init; }

    public int PermissionRequestCount { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public long? ProviderLatencyMs { get; init; }

    public string SafetyOutcome { get; init; } = string.Empty;

    public bool SafetyAllowed { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;
}
