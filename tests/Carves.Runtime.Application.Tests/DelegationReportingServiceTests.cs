using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class DelegationReportingServiceTests
{
    [Fact]
    public void BuildDelegationReport_DowngradesProjectionNoiseFromDefaultPreview()
    {
        var activeTask = new TaskNode
        {
            TaskId = "T-ACTIVE",
            Title = "Active blocker",
            Status = DomainTaskStatus.Blocked,
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        var completedTask = new TaskNode
        {
            TaskId = "T-DONE",
            Title = "Completed task",
            Status = DomainTaskStatus.Completed,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
        };
        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([activeTask, completedTask])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var eventRepository = new InMemoryOperatorOsEventRepository();
        var eventStreamService = new OperatorOsEventStreamService(eventRepository);
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.DelegationCompleted,
            TaskId = activeTask.TaskId,
            ActorKind = ActorSessionKind.Operator,
            ActorIdentity = "operator",
            ReasonCode = "delegation_failed",
            Summary = "Active delegated task failed and remains blocked.",
            OccurredAt = DateTimeOffset.UtcNow.AddHours(-1),
        });
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.DelegationCompleted,
            TaskId = completedTask.TaskId,
            ActorKind = ActorSessionKind.Operator,
            ActorIdentity = "operator",
            ReasonCode = "delegation_completed",
            Summary = "Completed task left recent residue.",
            OccurredAt = DateTimeOffset.UtcNow.AddHours(-2),
        });
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.DelegationRequested,
            TaskId = null,
            ActorKind = ActorSessionKind.Operator,
            ActorIdentity = "operator",
            ReasonCode = "delegation_requested",
            Summary = "Legacy request without current task linkage.",
            OccurredAt = DateTimeOffset.UtcNow.AddDays(-10),
        });

        var runtimeNoiseAuditService = new RuntimeNoiseAuditService(taskGraphService, () => Array.Empty<RuntimeIncidentRecord>());
        var report = new DelegationReportingService(
            runtimeNoiseAuditService,
            eventStreamService,
            null!,
            new WorkerOperationalPolicyService(WorkerOperationalPolicy.CreateDefault()))
            .BuildDelegationReport(hours: 24 * 14);

        Assert.Equal(1, report.ActionableEventCount);
        Assert.Equal(1, report.ProjectionNoiseCount);
        Assert.Equal(1, report.LegacyDebtCount);
        Assert.Single(report.RecentEvents);
        Assert.Equal(activeTask.TaskId, report.RecentEvents[0].TaskId);
        Assert.Equal(RuntimeNoiseAuditClassification.ActiveBlocker, report.RecentEvents[0].Classification);
        Assert.Single(report.Actors);
        Assert.Equal("Operator:operator", report.Actors[0].Actor);
        Assert.Single(report.Outcomes);
        Assert.Equal("failed", report.Outcomes[0].Outcome);
    }
}
