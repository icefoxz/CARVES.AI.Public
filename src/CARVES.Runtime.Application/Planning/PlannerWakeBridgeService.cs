using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerWakeBridgeService
{
    private readonly string repoRoot;
    private readonly IRuntimeSessionRepository sessionRepository;
    private readonly IMarkdownSyncService markdownSyncService;
    private readonly TaskGraphService taskGraphService;
    private readonly OperatorOsEventStreamService operatorOsEventStreamService;

    public PlannerWakeBridgeService(
        string repoRoot,
        IRuntimeSessionRepository sessionRepository,
        IMarkdownSyncService markdownSyncService,
        TaskGraphService taskGraphService,
        OperatorOsEventStreamService operatorOsEventStreamService)
    {
        this.repoRoot = repoRoot;
        this.sessionRepository = sessionRepository;
        this.markdownSyncService = markdownSyncService;
        this.taskGraphService = taskGraphService;
        this.operatorOsEventStreamService = operatorOsEventStreamService;
    }

    public PlannerWakeSignal Queue(
        RuntimeSessionState session,
        PlannerWakeReason wakeReason,
        PlannerWakeSourceKind sourceKind,
        string detail,
        string summary,
        string? taskId = null,
        string? runId = null,
        bool persist = false)
    {
        var signal = session.EnqueuePlannerWake(wakeReason, sourceKind, detail, summary, taskId, runId);
        AppendEvent(
            OperatorOsEventKind.PlannerWakeQueued,
            signal.SignalId,
            wakeReason.ToString().ToLowerInvariant(),
            summary,
            taskId,
            runId);

        if (persist)
        {
            Persist(session);
        }

        return signal;
    }

    public bool TryConsume(RuntimeSessionState session, out PlannerWakeSignal? signal, bool persist = false)
    {
        if (!session.TryConsumePlannerWake(out signal))
        {
            return false;
        }

        AppendEvent(
            OperatorOsEventKind.PlannerWakeConsumed,
            signal!.SignalId,
            signal.WakeReason.ToString().ToLowerInvariant(),
            signal.Summary,
            signal.TaskId,
            signal.RunId);

        if (persist)
        {
            Persist(session);
        }

        return true;
    }

    public void RecordLeaseAcquired(RuntimeSessionState session)
    {
        AppendEvent(
            OperatorOsEventKind.PlannerLeaseAcquired,
            session.PlannerLeaseId,
            session.PlannerLeaseMode.ToString().ToLowerInvariant(),
            session.PlannerLeaseReason ?? "Planner lease acquired.",
            taskId: null,
            runId: session.LastWorkerRunId);
    }

    public void RecordLeaseReleased(RuntimeSessionState session)
    {
        AppendEvent(
            OperatorOsEventKind.PlannerLeaseReleased,
            session.PlannerLeaseId,
            session.PlannerLeaseMode.ToString().ToLowerInvariant(),
            session.PlannerLeaseReason ?? "Planner lease released.",
            taskId: null,
            runId: session.LastWorkerRunId);
    }

    private void Persist(RuntimeSessionState session)
    {
        sessionRepository.Save(session);
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
    }

    private void AppendEvent(
        OperatorOsEventKind eventKind,
        string? referenceId,
        string reasonCode,
        string summary,
        string? taskId,
        string? runId)
    {
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = eventKind,
            RepoId = ResolveRepoId(),
            TaskId = taskId,
            RunId = runId,
            ReasonCode = reasonCode,
            Summary = summary,
            ReferenceId = referenceId,
        });
    }

    private string ResolveRepoId()
    {
        return Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
