namespace Carves.Runtime.Application.Refactoring;

public sealed record RefactoringFinding(
    string FindingId,
    string Kind,
    string Path,
    string Reason,
    string Severity,
    IReadOnlyDictionary<string, int> Metrics);

public sealed class RefactoringBacklogSnapshot
{
    public int Version { get; init; } = 1;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<RefactoringBacklogItem> Items { get; init; } = Array.Empty<RefactoringBacklogItem>();
}

public sealed class RefactoringHotspotQueueSnapshot
{
    public int Version { get; init; } = 1;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public int SelectionWindow { get; init; } = 20;

    public int MaxItemsPerQueue { get; init; } = 4;

    public IReadOnlyList<RefactoringHotspotQueue> Queues { get; init; } = Array.Empty<RefactoringHotspotQueue>();
}

public sealed class RefactoringHotspotQueue
{
    public string QueueId { get; init; } = string.Empty;

    public string FamilyId { get; init; } = string.Empty;

    public int QueuePass { get; init; } = 1;

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string PlanningTaskId { get; init; } = string.Empty;

    public string? SuggestedTaskId { get; init; }

    public string? PreviousSuggestedTaskId { get; init; }

    public string ProofTarget { get; init; } = string.Empty;

    public IReadOnlyList<string> ScopeRoots { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PreservationConstraints { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ValidationSurface { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> BacklogItemIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> HotspotPaths { get; init; } = Array.Empty<string>();
}

public sealed class RefactoringBacklogItem
{
    public string ItemId { get; init; } = string.Empty;

    public string Fingerprint { get; init; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string Severity { get; set; } = "warning";

    public string Priority { get; set; } = "P3";

    public RefactoringBacklogStatus Status { get; set; } = RefactoringBacklogStatus.Open;

    public string? SuggestedTaskId { get; set; }

    public DateTimeOffset FirstDetectedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastDetectedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ResolvedAt { get; set; }

    public IReadOnlyDictionary<string, int> Metrics { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);

    public void MarkObserved(RefactoringFinding finding, string priority, DateTimeOffset observedAt)
    {
        Kind = finding.Kind;
        Path = finding.Path;
        Reason = finding.Reason;
        Severity = finding.Severity;
        Priority = priority;
        Metrics = finding.Metrics;
        LastDetectedAt = observedAt;

        if (Status == RefactoringBacklogStatus.Resolved)
        {
            Status = RefactoringBacklogStatus.Open;
            SuggestedTaskId = null;
            ResolvedAt = null;
        }
    }

    public void MarkSuggested(string taskId, DateTimeOffset observedAt)
    {
        SuggestedTaskId = taskId;
        Status = RefactoringBacklogStatus.Suggested;
        LastDetectedAt = observedAt;
        ResolvedAt = null;
    }

    public void MarkResolved(DateTimeOffset observedAt)
    {
        Status = RefactoringBacklogStatus.Resolved;
        LastDetectedAt = observedAt;
        ResolvedAt = observedAt;
    }
}

public enum RefactoringBacklogStatus
{
    Open,
    Suggested,
    Resolved,
    Suppressed,
}

public sealed record RefactoringTaskMaterializationResult(
    IReadOnlyList<string> DeferredBacklogItemIds,
    IReadOnlyList<string> SuggestedTaskIds,
    bool DeferredForHigherPriorityWork,
    IReadOnlyList<string> QueueIds,
    IReadOnlyList<string> QueuePaths);
