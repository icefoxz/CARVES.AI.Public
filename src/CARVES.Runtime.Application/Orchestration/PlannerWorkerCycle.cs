using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Orchestration;

public sealed class PlannerWorkerCycle
{
    private readonly TaskGraphService taskGraphService;
    private readonly WorkerRequestFactory workerRequestFactory;
    private readonly WorkerService workerService;
    private readonly PlannerReviewService plannerReviewService;
    private readonly TaskTransitionPolicy transitionPolicy;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly PlannerReviewArtifactFactory reviewArtifactFactory;
    private readonly FormalPlanningExecutionGateService formalPlanningExecutionGateService;
    private readonly ModeExecutionEntryGateService modeExecutionEntryGateService;

    public PlannerWorkerCycle(
        TaskGraphService taskGraphService,
        WorkerRequestFactory workerRequestFactory,
        WorkerService workerService,
        PlannerReviewService plannerReviewService,
        TaskTransitionPolicy transitionPolicy,
        IRuntimeArtifactRepository artifactRepository,
        PlannerReviewArtifactFactory reviewArtifactFactory,
        FormalPlanningExecutionGateService? formalPlanningExecutionGateService = null)
    {
        this.taskGraphService = taskGraphService;
        this.workerRequestFactory = workerRequestFactory;
        this.workerService = workerService;
        this.plannerReviewService = plannerReviewService;
        this.transitionPolicy = transitionPolicy;
        this.artifactRepository = artifactRepository;
        this.reviewArtifactFactory = reviewArtifactFactory;
        this.formalPlanningExecutionGateService = formalPlanningExecutionGateService ?? new FormalPlanningExecutionGateService();
        this.modeExecutionEntryGateService = new ModeExecutionEntryGateService(this.formalPlanningExecutionGateService);
    }

    public CycleResult Run(TaskNode nextTask, bool dryRun, WorkerSelectionOptions? selectionOptions = null)
    {
        var prepared = Prepare(nextTask, dryRun, selectionOptions);
        var report = Execute(prepared);
        return Complete(prepared, report);
    }

    public PreparedWorkerCycle Prepare(TaskNode nextTask, bool dryRun, WorkerSelectionOptions? selectionOptions = null)
    {
        modeExecutionEntryGateService.EnsureReadyForExecution(nextTask);
        var task = taskGraphService.MarkStatus(nextTask.TaskId, DomainTaskStatus.Running);
        var request = workerRequestFactory.Create(task, dryRun, selectionOptions);
        return new PreparedWorkerCycle(task, request);
    }

    public TaskRunReport Execute(PreparedWorkerCycle prepared)
    {
        return workerService.Execute(prepared.Request);
    }

    public CycleResult Complete(PreparedWorkerCycle prepared, TaskRunReport report)
    {
        var task = prepared.Task;
        var request = prepared.Request;
        var review = plannerReviewService.Review(task, report);
        var transition = transitionPolicy.Decide(task, report, review);

        task.SetPlannerReview(review);
        DateTimeOffset? retryNotBefore = transition.IncrementRetry
            ? DateTimeOffset.UtcNow.Add(new WorkerFailureClassifier().ResolveBackoff(task, report.WorkerExecution))
            : null;
        task.RecordWorkerOutcome(report.WorkerExecution, retryNotBefore);
        task.LastWorkerDetailRef = PromptSafeArtifactProjectionFactory.GetWorkerExecutionDetailRef(task.TaskId);
        task.LastProviderDetailRef = PromptSafeArtifactProjectionFactory.GetProviderArtifactDetailRef(task.TaskId);
        task.Touch();
        if (report.ResultCommit is not null)
        {
            task.SetResultCommit(report.ResultCommit);
        }

        if (transition.IncrementRetry)
        {
            task.IncrementRetryCount();
        }
        else
        {
            task.ClearRetryBackoff();
        }

        task.SetStatus(transition.NextStatus);
        if (transition.NextStatus == DomainTaskStatus.Review)
        {
            task = ProjectAcceptanceContract(task, AcceptanceContractLifecycleStatus.HumanReview);
        }

        taskGraphService.ReplaceTask(task);
        artifactRepository.SavePlannerReviewArtifact(reviewArtifactFactory.Create(task, report, review, transition));
        return new CycleResult
        {
            Tasks = [task],
            Requests = [request],
            Reports = [report],
            Reviews = [review],
            Transitions = [transition],
            Message = $"Processed {task.TaskId} with verdict {review.Verdict}.",
        };
    }

    private static TaskNode ProjectAcceptanceContract(TaskNode task, AcceptanceContractLifecycleStatus status)
    {
        var projectedContract = status == AcceptanceContractLifecycleStatus.HumanReview
            ? AcceptanceContractStatusProjector.MoveToHumanReview(task.AcceptanceContract)
            : task.AcceptanceContract;
        if (ReferenceEquals(projectedContract, task.AcceptanceContract))
        {
            return task;
        }

        return new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource,
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies,
            Scope = task.Scope,
            Acceptance = task.Acceptance,
            Constraints = task.Constraints,
            AcceptanceContract = projectedContract,
            Validation = task.Validation,
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities,
            Metadata = task.Metadata,
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind,
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction,
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore,
            PlannerReview = task.PlannerReview,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}

public sealed record PreparedWorkerCycle(TaskNode Task, WorkerRequest Request);
