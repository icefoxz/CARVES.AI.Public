namespace Carves.Runtime.Application.Workers;

public sealed record WorkerExecutionAuditQuery
{
    public string RequestedQuery { get; init; } = "recent";

    public string EffectiveQuery { get; init; } = "recent limit:10";

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

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(TaskId)
        || !string.IsNullOrWhiteSpace(RunId)
        || !string.IsNullOrWhiteSpace(Status)
        || !string.IsNullOrWhiteSpace(EventType)
        || !string.IsNullOrWhiteSpace(BackendId)
        || !string.IsNullOrWhiteSpace(ProviderId)
        || SafetyAllowed is not null;
}

public sealed record WorkerExecutionAuditQueryResult
{
    public WorkerExecutionAuditQuery Query { get; init; } = new();

    public WorkerExecutionAuditSummary Summary { get; init; } = new();

    public IReadOnlyList<WorkerExecutionAuditEntry> Entries { get; init; } = [];

    public string QueryMode { get; init; } = "indexed";
}
