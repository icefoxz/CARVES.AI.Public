using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Tests;

public sealed class ResourceLeaseServiceTests
{
    [Fact]
    public void TryAcquire_AllowsNonConflictingChildWorkOrdersInSameTaskGraph()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ResourceLeaseService(workspace.Paths);
        var now = DateTimeOffset.Parse("2026-04-20T09:00:00Z");

        var first = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-001",
            TaskGraphId = "TG-P7",
            TaskId = "TASK-P7-A",
            DeclaredWriteSet = new ResourceWriteSet
            {
                Paths = ["src/Core/A.cs"],
                Modules = ["Core.A"],
                TruthOperations = ["task_status_to_review:TASK-P7-A"],
                TargetBranches = ["feature/p7-a"],
            },
            Now = now,
        });
        var second = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-002",
            TaskGraphId = "TG-P7",
            TaskId = "TASK-P7-B",
            DeclaredWriteSet = new ResourceWriteSet
            {
                Paths = ["src/Core/B.cs"],
                Modules = ["Core.B"],
                TruthOperations = ["task_status_to_review:TASK-P7-B"],
                TargetBranches = ["feature/p7-b"],
            },
            Now = now.AddSeconds(1),
        });

        Assert.True(first.Acquired);
        Assert.True(second.Acquired);
        Assert.Equal(ResourceLeaseStatus.Active, first.Lease.Status);
        Assert.Equal(ResourceLeaseStatus.Active, second.Lease.Status);
        Assert.Contains("TASK-P7-A", first.Lease.DeclaredWriteSet.TaskIds);
        Assert.Contains("TASK-P7-B", second.Lease.DeclaredWriteSet.TaskIds);
        Assert.Empty(second.Conflicts);
        Assert.Equal(2, service.LoadActive(now.AddSeconds(2)).Count);
    }

    [Fact]
    public void TryAcquire_BlocksSameTaskPathModuleTruthOperationAndTargetBranchConflicts()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ResourceLeaseService(workspace.Paths);
        var now = DateTimeOffset.Parse("2026-04-20T09:00:00Z");
        var first = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-BLOCKING",
            TaskGraphId = "TG-P7",
            TaskId = "TASK-P7-SAME",
            DeclaredWriteSet = new ResourceWriteSet
            {
                Paths = ["src/Feature"],
                Modules = ["Feature.Parser"],
                TruthOperations = ["task_status_to_review:TASK-P7-SAME"],
                TargetBranches = ["feature/p7"],
            },
            Now = now,
        });

        var blocked = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-BLOCKED",
            TaskGraphId = "TG-P7",
            TaskId = "TASK-P7-SAME",
            DeclaredWriteSet = new ResourceWriteSet
            {
                Paths = ["src/Feature/Parser.cs"],
                Modules = ["Feature.Parser"],
                TruthOperations = ["task_status_to_review:TASK-P7-SAME"],
                TargetBranches = ["feature/p7"],
            },
            Now = now.AddSeconds(1),
        });

        Assert.True(first.Acquired);
        Assert.False(blocked.Acquired);
        Assert.Equal(ResourceLeaseStatus.Stopped, blocked.Lease.Status);
        Assert.Contains(ResourceLeaseService.ConflictStopReason, blocked.Lease.StopReasons);
        Assert.Contains(first.Lease.LeaseId, blocked.Lease.BlockingLeaseIds);
        Assert.Contains(blocked.Conflicts, conflict => conflict.Kind == ResourceLeaseConflictKind.SameTask);
        Assert.Contains(blocked.Conflicts, conflict => conflict.Kind == ResourceLeaseConflictKind.SamePathWrite);
        Assert.Contains(blocked.Conflicts, conflict => conflict.Kind == ResourceLeaseConflictKind.SameModule);
        Assert.Contains(blocked.Conflicts, conflict => conflict.Kind == ResourceLeaseConflictKind.SameTruthOperation);
        Assert.Contains(blocked.Conflicts, conflict => conflict.Kind == ResourceLeaseConflictKind.SameTargetBranch);
    }

    [Fact]
    public void ProjectAcquire_ReturnsProjectionWithoutPersistingOrBlockingLaterAcquire()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ResourceLeaseService(workspace.Paths);
        var now = DateTimeOffset.Parse("2026-04-20T09:00:00Z");
        var declaredWriteSet = new ResourceWriteSet
        {
            Paths = ["src/DryRun"],
            Modules = ["Runtime.DryRun"],
            TruthOperations = ["task_status_to_review:TASK-P7-DRY"],
            TargetBranches = ["feature/p7-dry"],
        };

        var projection = service.ProjectAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-DRY",
            TaskId = "TASK-P7-DRY",
            DeclaredWriteSet = declaredWriteSet,
            Now = now,
        });
        var active = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-ACTIVE",
            TaskId = "TASK-P7-ACTIVE",
            DeclaredWriteSet = declaredWriteSet,
            Now = now.AddSeconds(1),
        });
        var blockedProjection = service.ProjectAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-BLOCKED-PROJECTION",
            TaskId = "TASK-P7-BLOCKED-PROJECTION",
            DeclaredWriteSet = new ResourceWriteSet
            {
                Paths = ["src/DryRun/Feature.cs"],
            },
            Now = now.AddSeconds(2),
        });

        Assert.False(projection.Acquired);
        Assert.False(projection.Queued);
        Assert.Equal(ResourceLeaseStatus.Projected, projection.Lease.Status);
        Assert.Equal("projected", projection.Lease.ConflictResolution);
        Assert.True(active.Acquired);
        Assert.Equal(ResourceLeaseStatus.Active, active.Lease.Status);
        Assert.False(blockedProjection.Acquired);
        Assert.Equal(ResourceLeaseStatus.Stopped, blockedProjection.Lease.Status);
        Assert.Contains(ResourceLeaseService.ConflictStopReason, blockedProjection.Lease.StopReasons);
        Assert.Contains(active.Lease.LeaseId, blockedProjection.Lease.BlockingLeaseIds);
        var persisted = Assert.Single(service.LoadSnapshot().Leases);
        Assert.Equal(active.Lease.LeaseId, persisted.LeaseId);
    }

    [Fact]
    public void TryAcquire_QueuesSameTruthOperationWhenSerializationPolicyAllowsIt()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ResourceLeaseService(workspace.Paths);
        var now = DateTimeOffset.Parse("2026-04-20T09:00:00Z");
        _ = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-TRUTH-A",
            TaskGraphId = "TG-P7",
            TaskId = "TASK-P7-TRUTH-A",
            DeclaredWriteSet = new ResourceWriteSet
            {
                TruthOperations = ["refresh_authoritative_projection:TG-P7"],
            },
            Now = now,
        });

        var defaultBlocked = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-TRUTH-BLOCKED",
            TaskGraphId = "TG-P7",
            TaskId = "TASK-P7-TRUTH-B",
            DeclaredWriteSet = new ResourceWriteSet
            {
                TruthOperations = ["refresh_authoritative_projection:TG-P7"],
            },
            Now = now.AddSeconds(1),
        });
        var serialized = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-TRUTH-QUEUED",
            TaskGraphId = "TG-P7",
            TaskId = "TASK-P7-TRUTH-C",
            DeclaredWriteSet = new ResourceWriteSet
            {
                TruthOperations = ["refresh_authoritative_projection:TG-P7"],
            },
            ConflictPolicy = ResourceLeaseConflictPolicy.SerializeTruthOperations,
            Now = now.AddSeconds(2),
        });

        Assert.False(defaultBlocked.Acquired);
        Assert.Equal(ResourceLeaseStatus.Stopped, defaultBlocked.Lease.Status);
        Assert.True(serialized.Queued);
        Assert.Equal(ResourceLeaseStatus.Queued, serialized.Lease.Status);
        Assert.Equal("serialized_truth_operation_queue", serialized.Lease.ConflictResolution);
        Assert.All(serialized.Conflicts, conflict => Assert.Equal(ResourceLeaseConflictKind.SameTruthOperation, conflict.Kind));
    }

    [Fact]
    public void ReconcileActualWriteSet_StopsOrEscalatesWhenActualWritesExceedDeclaredScope()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ResourceLeaseService(workspace.Paths);
        var now = DateTimeOffset.Parse("2026-04-20T09:00:00Z");
        var within = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-WITHIN",
            TaskId = "TASK-P7-WITHIN",
            DeclaredWriteSet = new ResourceWriteSet
            {
                Paths = ["src/Feature"],
                Modules = ["Feature"],
                TruthOperations = ["task_status_to_review:TASK-P7-WITHIN"],
                TargetBranches = ["feature/p7-within"],
            },
            Now = now,
        });
        var stopped = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-STOP",
            TaskId = "TASK-P7-STOP",
            DeclaredWriteSet = new ResourceWriteSet
            {
                Paths = ["tests/FeatureTests.cs"],
                Modules = ["Feature.Tests"],
                TruthOperations = ["task_status_to_review:TASK-P7-STOP"],
                TargetBranches = ["feature/p7-stop"],
            },
            Now = now.AddSeconds(1),
        });
        var escalation = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-ESCALATE",
            TaskId = "TASK-P7-ESCALATE",
            DeclaredWriteSet = new ResourceWriteSet
            {
                Paths = ["docs/p7-note.md"],
                Modules = ["Docs.P7"],
                TruthOperations = ["task_status_to_review:TASK-P7-ESCALATE"],
                TargetBranches = ["feature/p7-escalate"],
            },
            Now = now.AddSeconds(2),
        });

        var withinResult = service.ReconcileActualWriteSet(
            within.Lease.LeaseId,
            new ResourceWriteSet
            {
                Paths = ["src/Feature/Parser.cs"],
                Modules = ["Feature"],
                TruthOperations = ["task_status_to_review:TASK-P7-WITHIN"],
                TargetBranches = ["feature/p7-within"],
            });
        var stopResult = service.ReconcileActualWriteSet(
            stopped.Lease.LeaseId,
            new ResourceWriteSet
            {
                Paths = ["tests"],
                Modules = ["Feature.Tests"],
                TruthOperations = ["task_status_to_review:TASK-P7-STOP"],
                TargetBranches = ["feature/p7-stop"],
            });
        var escalationResult = service.ReconcileActualWriteSet(
            escalation.Lease.LeaseId,
            new ResourceWriteSet
            {
                Paths = ["docs/p7-note.md"],
                Modules = ["Docs.P7", "Runtime.ControlPlane"],
                TruthOperations = ["task_status_to_review:TASK-P7-ESCALATE"],
                TargetBranches = ["feature/p7-escalate"],
            },
            ResourceLeaseActualWriteSetPolicy.RequestEscalation);

        Assert.True(withinResult.WithinDeclaredWriteSet);
        Assert.Equal(ResourceLeaseStatus.Active, withinResult.Lease!.Status);
        Assert.False(stopResult.WithinDeclaredWriteSet);
        Assert.Contains(ResourceLeaseService.ActualWriteSetEscalationStopReason, stopResult.StopReasons);
        Assert.Equal(ResourceLeaseStatus.Stopped, stopResult.Lease!.Status);
        Assert.Contains(stopResult.EscalationReasons, reason => reason.Contains("Actual path 'tests'", StringComparison.Ordinal));
        Assert.False(escalationResult.WithinDeclaredWriteSet);
        Assert.True(escalationResult.EscalationRequired);
        Assert.Equal(ResourceLeaseStatus.EscalationRequired, escalationResult.Lease!.Status);
        Assert.Contains(escalationResult.EscalationReasons, reason => reason.Contains("Runtime.ControlPlane", StringComparison.Ordinal));
    }

    [Fact]
    public void ReleaseAndRecoverStale_CloseResourceLeasesWithoutTruthWriteback()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ResourceLeaseService(workspace.Paths);
        var now = DateTimeOffset.Parse("2026-04-20T09:00:00Z");
        var released = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-RELEASE",
            TaskId = "TASK-P7-RELEASE",
            DeclaredWriteSet = new ResourceWriteSet { Paths = ["src/Release.cs"] },
            Now = now,
            ValidUntil = now.AddMinutes(10),
        });
        var stale = service.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P7-STALE",
            TaskId = "TASK-P7-STALE",
            DeclaredWriteSet = new ResourceWriteSet { Paths = ["src/Stale.cs"] },
            Now = now,
            ValidUntil = now.AddSeconds(1),
        });

        var release = service.Release(released.Lease.LeaseId, "completed_without_truth_writeback");
        var recovery = service.RecoverStale(now.AddSeconds(2));
        var snapshot = service.LoadSnapshot();

        Assert.True(release.Released);
        Assert.Contains(stale.Lease.LeaseId, recovery.RecoveredLeaseIds);
        Assert.Contains(snapshot.Leases, lease => lease.LeaseId == released.Lease.LeaseId
            && lease.Status == ResourceLeaseStatus.Released
            && lease.ReleaseReason == "completed_without_truth_writeback");
        Assert.Contains(snapshot.Leases, lease => lease.LeaseId == stale.Lease.LeaseId
            && lease.Status == ResourceLeaseStatus.Expired
            && lease.ReleaseReason == ResourceLeaseService.StaleRecoveredReason);
    }
}
