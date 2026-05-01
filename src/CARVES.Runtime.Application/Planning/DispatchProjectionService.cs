using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Planning;

public sealed class DispatchProjectionService
{
    private readonly Carves.Runtime.Application.TaskGraph.TaskScheduler scheduler;
    private readonly FormalPlanningExecutionGateService formalPlanningExecutionGateService;
    private readonly ModeExecutionEntryGateService modeExecutionEntryGateService;

    public DispatchProjectionService(
        Carves.Runtime.Application.TaskGraph.TaskScheduler? scheduler = null,
        FormalPlanningExecutionGateService? formalPlanningExecutionGateService = null)
    {
        this.formalPlanningExecutionGateService = formalPlanningExecutionGateService ?? new FormalPlanningExecutionGateService();
        this.modeExecutionEntryGateService = new ModeExecutionEntryGateService(this.formalPlanningExecutionGateService);
        this.scheduler = scheduler ?? new Carves.Runtime.Application.TaskGraph.TaskScheduler(formalPlanningExecutionGateService: this.formalPlanningExecutionGateService);
    }

    public DispatchProjection Build(DomainTaskGraph graph, RuntimeSessionState? session, int maxWorkers)
    {
        var effectiveSession = session ?? RuntimeSessionState.Start(string.Empty, dryRun: false);
        var workerPool = new WorkerPoolSnapshot(effectiveSession.ActiveWorkerCount, Math.Max(0, maxWorkers), effectiveSession.ActiveTaskIds);
        var decision = scheduler.Decide(graph, effectiveSession, workerPool);
        var state = decision.ShouldDispatch ? "dispatchable" : "dispatch_blocked";
        var idleReason = decision.ShouldDispatch ? "READY_TASK_AVAILABLE" : ClassifyIdleReason(graph, effectiveSession, workerPool, decision);
        var readyEntryGates = graph.ReadyTasks()
            .Select(task => new ReadyTaskEntryGate(task, modeExecutionEntryGateService.Evaluate(task)))
            .ToArray();
        var firstBlocked = readyEntryGates.FirstOrDefault(item => item.Gate.BlocksExecution);
        var acceptanceContractGapCount = readyEntryGates.Count(item => item.Gate.AcceptanceContractGap);
        var planRequiredBlockCount = readyEntryGates.Count(item => item.Gate.PlanRequired);
        var workspaceRequiredBlockCount = readyEntryGates.Count(item => item.Gate.WorkspaceRequired);
        return new DispatchProjection(
            state,
            decision.Reason,
            idleReason,
            decision.Tasks.FirstOrDefault()?.TaskId,
            readyEntryGates.Count(item => !item.Gate.BlocksExecution),
            effectiveSession.ActiveWorkerCount,
            workerPool.MaxWorkers,
            AutoContinueOnApprove: true,
            AcceptanceContractGapCount: acceptanceContractGapCount,
            PlanRequiredBlockCount: planRequiredBlockCount,
            WorkspaceRequiredBlockCount: workspaceRequiredBlockCount,
            FirstBlockedTaskId: firstBlocked?.Task.TaskId,
            FirstBlockingCheckId: firstBlocked?.Gate.FirstBlockingCheckId,
            FirstBlockingCheckSummary: firstBlocked?.Gate.FirstBlockingCheckSummary,
            FirstBlockingCheckRequiredAction: firstBlocked?.Gate.FirstBlockingCheckRequiredAction,
            FirstBlockingCheckRequiredCommand: firstBlocked?.Gate.FirstBlockingCheckRequiredCommand,
            RecommendedNextAction: firstBlocked?.Gate.RecommendedNextAction,
            RecommendedNextCommand: firstBlocked?.Gate.RecommendedNextCommand);
    }

    public string DescribeIdleReason(string idleReason)
    {
        return idleReason switch
        {
            "NO_READY_TASK" => "no dispatchable task",
            "WAITING_APPROVAL" => "waiting for approval or review",
            "WORKER_UNAVAILABLE" => "worker unavailable",
            "BLOCKED_BY_DEPENDENCY" => "blocked by dependency",
            "FAILED_NEEDS_REPLAN" => "failed and needs replan",
            "MISSING_ACCEPTANCE_CONTRACT" => "acceptance contract missing",
            "PLAN_REQUIRED" => "formal planning required",
            "WORKSPACE_REQUIRED" => "managed workspace required",
            "SESSION_PAUSED" => "session paused",
            "SESSION_STOPPED" => "session stopped",
            "WORKER_POOL_AT_CAPACITY" => "worker pool at capacity",
            "READY_TASK_AVAILABLE" => "dispatchable task available",
            nameof(TaskScheduleIdleReason.NoReadyExecutionTask) => "no dispatchable task",
            nameof(TaskScheduleIdleReason.ReviewBoundary) => "waiting for approval or review",
            nameof(TaskScheduleIdleReason.WorkerPoolAtCapacity) => "worker pool at capacity",
            nameof(TaskScheduleIdleReason.SessionPaused) => "session paused",
            nameof(TaskScheduleIdleReason.SessionStopped) => "session stopped",
            nameof(TaskScheduleIdleReason.AllCandidatesBlocked) => "all dependency-ready candidates blocked by governance",
            _ => idleReason,
        };
    }

    private static string ClassifyIdleReason(
        DomainTaskGraph graph,
        RuntimeSessionState session,
        WorkerPoolSnapshot workerPool,
        TaskScheduleDecision decision)
    {
        if (decision.IdleReason == TaskScheduleIdleReason.SessionPaused)
        {
            return "SESSION_PAUSED";
        }

        if (decision.IdleReason == TaskScheduleIdleReason.SessionStopped)
        {
            return "SESSION_STOPPED";
        }

        if (workerPool.MaxWorkers == 0)
        {
            return "WORKER_UNAVAILABLE";
        }

        if (session.PendingPermissionRequestIds.Count > 0
            || graph.Tasks.Values.Any(task => task.Status is DomainTaskStatus.ApprovalWait or DomainTaskStatus.Review))
        {
            return "WAITING_APPROVAL";
        }

        if (decision.BlockedTasks.Any(block =>
                block.Kind == TaskScheduleBlockKind.Governance
                && block.Reason.Contains("acceptance contract", StringComparison.OrdinalIgnoreCase)))
        {
            return "MISSING_ACCEPTANCE_CONTRACT";
        }

        if (decision.BlockedTasks.Any(block =>
                block.Kind == TaskScheduleBlockKind.Governance
                && block.Reason.Contains("scope", StringComparison.OrdinalIgnoreCase)))
        {
            return "MISSING_TASK_SCOPE";
        }

        if (decision.BlockedTasks.Any(block =>
                block.Kind == TaskScheduleBlockKind.Governance
                && block.Reason.Contains("managed workspace lease", StringComparison.OrdinalIgnoreCase)))
        {
            return "WORKSPACE_REQUIRED";
        }

        if (decision.BlockedTasks.Any(block =>
                block.Kind == TaskScheduleBlockKind.Governance
                && (block.Reason.Contains("formal planning", StringComparison.OrdinalIgnoreCase)
                    || block.Reason.Contains("plan handle", StringComparison.OrdinalIgnoreCase))))
        {
            return "PLAN_REQUIRED";
        }

        if (graph.Tasks.Values.Any(IsReplanRequired))
        {
            return "FAILED_NEEDS_REPLAN";
        }

        var completedTaskIds = graph.CompletedTaskIds();
        var hasPendingDependencyWait = graph.Tasks.Values.Any(task =>
            task.Status == DomainTaskStatus.Pending
            && task.Dependencies.Any(dependency => !completedTaskIds.Contains(dependency)));
        if (hasPendingDependencyWait)
        {
            return "BLOCKED_BY_DEPENDENCY";
        }

        if (decision.IdleReason == TaskScheduleIdleReason.WorkerPoolAtCapacity)
        {
            return "WORKER_POOL_AT_CAPACITY";
        }

        return "NO_READY_TASK";
    }

    private static bool IsReplanRequired(TaskNode task)
    {
        if (task.Status == DomainTaskStatus.Failed)
        {
            return true;
        }

        return task.Metadata.TryGetValue("planner_replan_required", out var plannerRequired)
               && bool.TryParse(plannerRequired, out var parsed)
               && parsed;
    }

    private sealed record ReadyTaskEntryGate(TaskNode Task, ModeExecutionEntryGateProjection Gate);
}
