using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Tests;

public sealed class PlannerWakeBridgeServiceTests
{
    [Fact]
    public void QueueAndConsume_PersistSignalsAndEmitOperatorOsEvents()
    {
        using var workspace = new TemporaryWorkspace();
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: false);
        sessionRepository.Save(session);
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph()), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var service = new PlannerWakeBridgeService(
            workspace.RootPath,
            sessionRepository,
            new NoOpMarkdownSyncService(),
            taskGraphService,
            eventStream);

        var queued = service.Queue(
            session,
            PlannerWakeReason.TaskFailed,
            PlannerWakeSourceKind.WorkerOutcome,
            "Task T-ASYNC-FAIL failed and needs planner follow-up.",
            "T-ASYNC-FAIL: failed via codex_cli",
            "T-ASYNC-FAIL",
            "RUN-ASYNC-FAIL",
            persist: true);

        Assert.Single(session.PendingPlannerWakeSignals);
        Assert.True(service.TryConsume(session, out var consumed, persist: true));
        Assert.Equal(queued.SignalId, consumed!.SignalId);
        Assert.Empty(session.PendingPlannerWakeSignals);

        var events = eventStream.Load();
        Assert.Contains(events, item => item.EventKind == OperatorOsEventKind.PlannerWakeQueued && item.ReferenceId == queued.SignalId);
        Assert.Contains(events, item => item.EventKind == OperatorOsEventKind.PlannerWakeConsumed && item.ReferenceId == queued.SignalId);
    }
}
