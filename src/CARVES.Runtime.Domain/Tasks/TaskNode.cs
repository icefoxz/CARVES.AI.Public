using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Domain.Tasks;

public sealed class TaskNode
{
    private TaskStatus status = TaskStatus.Pending;

    public string TaskId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public TaskStatus Status
    {
        get => status;
        init => status = value;
    }

    public TaskType TaskType { get; init; } = TaskType.Execution;

    public string Priority { get; init; } = "P1";

    public string Source { get; init; } = "HUMAN";

    public string? CardId { get; init; }

    public TaskProposalSource ProposalSource { get; init; } = TaskProposalSource.None;

    public string? ProposalReason { get; init; }

    public double? ProposalConfidence { get; init; }

    public string? ProposalPriorityHint { get; init; }

    public string? BaseCommit { get; init; }

    public string? ResultCommit { get; set; }

    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Scope { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Acceptance { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    public AcceptanceContract? AcceptanceContract { get; init; }

    public ValidationPlan Validation { get; init; } = new();

    public int RetryCount { get; set; }

    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public string? LastWorkerRunId { get; set; }

    public string? LastWorkerBackend { get; set; }

    public WorkerFailureKind LastWorkerFailureKind { get; set; } = WorkerFailureKind.None;

    public bool LastWorkerRetryable { get; set; }

    public string? LastWorkerSummary { get; set; }

    public string? LastWorkerDetailRef { get; set; }

    public string? LastProviderDetailRef { get; set; }

    public WorkerRecoveryAction LastRecoveryAction { get; set; } = WorkerRecoveryAction.None;

    public string? LastRecoveryReason { get; set; }

    public DateTimeOffset? RetryNotBefore { get; set; }

    public PlannerReview PlannerReview { get; set; } = new();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool CanDispatchToWorkerPool => TaskType.CanDispatchToWorkerPool();

    public bool CanExecuteInWorker => TaskType.CanExecuteInWorker();

    public bool RequiresReviewBoundary => TaskType.RequiresReviewBoundary();

    public bool IsReady(IReadOnlySet<string> completedTaskIds)
    {
        return Status == TaskStatus.Pending
            && Dependencies.All(completedTaskIds.Contains)
            && (RetryNotBefore is null || RetryNotBefore <= DateTimeOffset.UtcNow);
    }

    public void SetStatus(TaskStatus status)
    {
        TaskStatusTransitionPolicy.EnsureCanTransition(TaskId, Status, status, "task-node-set-status");
        this.status = status;
        Touch();
    }

    public void IncrementRetryCount()
    {
        RetryCount += 1;
        Touch();
    }

    public void SetResultCommit(string? resultCommit)
    {
        ResultCommit = resultCommit;
        Touch();
    }

    public void SetPlannerReview(PlannerReview review)
    {
        PlannerReview = review;
        Touch();
    }

    public void RecordWorkerOutcome(WorkerExecutionResult result, DateTimeOffset? retryNotBefore = null)
    {
        LastWorkerRunId = result.RunId;
        LastWorkerBackend = result.BackendId;
        LastWorkerFailureKind = result.FailureKind;
        LastWorkerRetryable = result.Retryable;
        LastWorkerSummary = result.Summary;
        RetryNotBefore = retryNotBefore;
        Touch();
    }

    public void ClearRetryBackoff()
    {
        RetryNotBefore = null;
        Touch();
    }

    public void RecordRecovery(WorkerRecoveryDecision decision)
    {
        LastRecoveryAction = decision.Action;
        LastRecoveryReason = decision.Reason;
        Touch();
    }

    public void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
