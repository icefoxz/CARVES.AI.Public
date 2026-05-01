using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeNoiseAuditServiceTests
{
    [Fact]
    public void Build_ClassifiesBlockedTasksAndIncidents()
    {
        var activeTask = new TaskNode
        {
            TaskId = "T-ACTIVE",
            Title = "Recent blocked task",
            Status = DomainTaskStatus.Blocked,
            PlannerReview = new PlannerReview { Reason = "Recent blocker." },
            UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        var legacyTask = new TaskNode
        {
            TaskId = "T-LEGACY",
            Title = "Old blocked task",
            Status = DomainTaskStatus.Blocked,
            PlannerReview = new PlannerReview { Reason = "Old blocker." },
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-10),
        };
        var completedTask = new TaskNode
        {
            TaskId = "T-DONE",
            Title = "Completed",
            Status = DomainTaskStatus.Completed,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([activeTask, legacyTask, completedTask])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var incidents = new[]
        {
            new RuntimeIncidentRecord
            {
                IncidentId = "INC-PROJECTION",
                IncidentType = RuntimeIncidentType.WorkerFailed,
                TaskId = completedTask.TaskId,
                Summary = "Completed task left a recent incident.",
                OccurredAt = DateTimeOffset.UtcNow.AddHours(-2),
            },
        };
        var service = new RuntimeNoiseAuditService(taskGraphService, () => incidents);

        var report = service.Build();

        Assert.Equal(RuntimeNoiseStartGateVerdict.Blocked, report.StartGate);
        Assert.Contains(report.BlockedTasks, item => item.TaskId == activeTask.TaskId && item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker);
        Assert.Contains(report.BlockedTasks, item => item.TaskId == legacyTask.TaskId && item.Classification == RuntimeNoiseAuditClassification.LegacyDebt);
        Assert.Contains(report.Incidents, item => item.IncidentId == "INC-PROJECTION" && item.Classification == RuntimeNoiseAuditClassification.ProjectionNoise);
    }

    [Fact]
    public void Build_IgnoresSupersededTasksWhenComputingBlockedQueue()
    {
        var supersededTask = new TaskNode
        {
            TaskId = "T-SUPERSEDED",
            Title = "Former blocker",
            Status = DomainTaskStatus.Superseded,
            PlannerReview = new PlannerReview { Reason = "Superseded by corrected card lineage." },
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        var blockedTask = new TaskNode
        {
            TaskId = "T-BLOCKED",
            Title = "Current blocker",
            Status = DomainTaskStatus.Blocked,
            PlannerReview = new PlannerReview { Reason = "Still blocked." },
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([supersededTask, blockedTask])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = new RuntimeNoiseAuditService(taskGraphService, () => Array.Empty<RuntimeIncidentRecord>());

        var report = service.Build();

        Assert.DoesNotContain(report.BlockedTasks, item => item.TaskId == supersededTask.TaskId);
        Assert.Contains(report.BlockedTasks, item => item.TaskId == blockedTask.TaskId && item.Classification == RuntimeNoiseAuditClassification.ActiveBlocker);
    }
}
