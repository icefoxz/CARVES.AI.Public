using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Orchestration;

public sealed partial class DevLoopService
{
    private readonly string attachedRepoRoot;
    private readonly TaskGraphService taskGraphService;
    private readonly PlannerWorkerCycle plannerWorkerCycle;
    private readonly WorkerBroker workerBroker;
    private readonly PlannerHostService plannerHostService;
    private readonly PlannerWakeBridgeService plannerWakeBridgeService;
    private readonly IRuntimeSessionRepository sessionRepository;
    private readonly IMarkdownSyncService markdownSyncService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly FailureReportService failureReportService;
    private readonly RuntimeFailurePolicy runtimeFailurePolicy;
    private readonly RecoveryPolicyEngine recoveryPolicyEngine;
    private readonly WorktreeRuntimeService worktreeRuntimeService;
    private readonly RuntimeIncidentTimelineService incidentTimelineService;
    private readonly ActorSessionService actorSessionService;
    private readonly FormalPlanningExecutionGateService formalPlanningExecutionGateService;

    public DevLoopService(
        string attachedRepoRoot,
        TaskGraphService taskGraphService,
        PlannerWorkerCycle plannerWorkerCycle,
        WorkerBroker workerBroker,
        PlannerHostService plannerHostService,
        PlannerWakeBridgeService plannerWakeBridgeService,
        IRuntimeSessionRepository sessionRepository,
        IMarkdownSyncService markdownSyncService,
        IRuntimeArtifactRepository artifactRepository,
        FailureReportService failureReportService,
        RuntimeFailurePolicy runtimeFailurePolicy,
        RecoveryPolicyEngine recoveryPolicyEngine,
        WorktreeRuntimeService worktreeRuntimeService,
        RuntimeIncidentTimelineService incidentTimelineService,
        ActorSessionService actorSessionService,
        FormalPlanningExecutionGateService? formalPlanningExecutionGateService = null)
    {
        this.attachedRepoRoot = attachedRepoRoot;
        this.taskGraphService = taskGraphService;
        this.plannerWorkerCycle = plannerWorkerCycle;
        this.workerBroker = workerBroker;
        this.plannerHostService = plannerHostService;
        this.plannerWakeBridgeService = plannerWakeBridgeService;
        this.sessionRepository = sessionRepository;
        this.markdownSyncService = markdownSyncService;
        this.artifactRepository = artifactRepository;
        this.failureReportService = failureReportService;
        this.runtimeFailurePolicy = runtimeFailurePolicy;
        this.recoveryPolicyEngine = recoveryPolicyEngine;
        this.worktreeRuntimeService = worktreeRuntimeService;
        this.incidentTimelineService = incidentTimelineService;
        this.actorSessionService = actorSessionService;
        this.formalPlanningExecutionGateService = formalPlanningExecutionGateService ?? new FormalPlanningExecutionGateService();
    }

    public RuntimeSessionState StartSession(bool dryRun)
    {
        var session = RuntimeSessionState.Start(attachedRepoRoot, dryRun);
        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
        return session;
    }

    public RuntimeSessionState? GetSession()
    {
        return sessionRepository.Load();
    }

    public RuntimeSessionState PauseSession(string reason)
    {
        var session = RequireSession();
        session.Pause(reason);
        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
        return session;
    }

    public RuntimeSessionState ResumeSession(string reason)
    {
        var session = RequireSession();
        session.Resume(reason);
        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
        return session;
    }

    public RuntimeSessionState StopSession(string reason)
    {
        var session = RequireSession();
        session.Stop(reason);
        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
        return session;
    }

    public RuntimeSessionState? ResolveReviewDecision(string taskId, string reason)
    {
        var session = sessionRepository.Load();
        if (session is null)
        {
            return null;
        }

        if (session.ReviewPendingTaskIds.Contains(taskId, StringComparer.Ordinal))
        {
            session.ResolveReviewPending(taskId, reason);
            sessionRepository.Save(session);
        }

        return session;
    }

    public RuntimeSessionState MarkReviewPending(string taskId, string reason)
    {
        var session = sessionRepository.Load() ?? RuntimeSessionState.Start(attachedRepoRoot, dryRun: false);
        session.MarkReviewWait(taskId, reason);
        sessionRepository.Save(session);
        return session;
    }

    public RuntimeSessionState? ReconcileReviewBoundary()
    {
        var session = sessionRepository.Load();
        if (session is null || session.ReviewPendingTaskIds.Count == 0)
        {
            return session;
        }

        var graph = taskGraphService.Load();
        var resolvedAny = false;
        foreach (var taskId in session.ReviewPendingTaskIds.ToArray())
        {
            if (!graph.Tasks.TryGetValue(taskId, out var task) || task.Status != DomainTaskStatus.Review)
            {
                session.ResolveReviewPending(taskId, $"Reconciled stale review boundary for {taskId}.");
                resolvedAny = true;
            }
        }

        if (resolvedAny)
        {
            sessionRepository.Save(session);
        }

        return session;
    }

    public CycleResult RunOnce(bool dryRun)
    {
        return Tick(dryRun);
    }

    public CycleResult Tick(bool dryRun)
    {
        return Tick(dryRun, RuntimeLoopMode.ManualTick);
    }

    public ContinuousLoopResult RunContinuousLoop(bool dryRun, int maxIterations)
    {
        if (maxIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations), "Continuous loop iteration count must be positive.");
        }

        var session = GetSession() ?? StartSession(dryRun);
        var iterations = new List<CycleResult>();

        for (var iterationIndex = 0; iterationIndex < maxIterations; iterationIndex++)
        {
            if (session.Status is RuntimeSessionStatus.Paused or RuntimeSessionStatus.ReviewWait or RuntimeSessionStatus.ApprovalWait or RuntimeSessionStatus.Failed or RuntimeSessionStatus.Stopped)
            {
                break;
            }

            var cycle = Tick(dryRun, RuntimeLoopMode.ContinuousLoop);
            iterations.Add(cycle);
            session = cycle.Session ?? session;

            if (session.Status is RuntimeSessionStatus.Paused or RuntimeSessionStatus.ReviewWait or RuntimeSessionStatus.ApprovalWait or RuntimeSessionStatus.Failed or RuntimeSessionStatus.Stopped)
            {
                break;
            }

            if (cycle.PlannerReentry is not null || cycle.ScheduleDecision is not { ShouldDispatch: true })
            {
                break;
            }
        }

        if (iterations.Count == maxIterations && session.Status is RuntimeSessionStatus.Idle or RuntimeSessionStatus.Scheduling or RuntimeSessionStatus.Executing)
        {
            session.MarkIdle($"Continuous loop reached iteration cap ({maxIterations}).", RuntimeActionability.WorkerActionable);
            sessionRepository.Save(session);
            markdownSyncService.Sync(taskGraphService.Load(), session: session);
        }

        var finalReason = session.WaitingReason ?? session.StopReason ?? session.LoopReason;
        return new ContinuousLoopResult
        {
            Iterations = iterations,
            Session = session,
            MaxIterations = maxIterations,
            Message = finalReason,
        };
    }
}
