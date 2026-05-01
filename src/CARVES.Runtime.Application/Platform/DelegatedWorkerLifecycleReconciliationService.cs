using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class DelegatedWorkerLifecycleReconciliationService
{
    private readonly ControlPlane.ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeSessionRepository sessionRepository;
    private readonly IWorkerLeaseRepository workerLeaseRepository;
    private readonly WorktreeRuntimeService worktreeRuntimeService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly IDelegatedRunLifecycleRepository lifecycleRepository;
    private readonly IDelegatedRunRecoveryLedgerRepository recoveryLedgerRepository;

    public DelegatedWorkerLifecycleReconciliationService(
        ControlPlane.ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        IRuntimeSessionRepository sessionRepository,
        IWorkerLeaseRepository workerLeaseRepository,
        WorktreeRuntimeService worktreeRuntimeService,
        IRuntimeArtifactRepository artifactRepository,
        IDelegatedRunLifecycleRepository lifecycleRepository,
        IDelegatedRunRecoveryLedgerRepository recoveryLedgerRepository)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.sessionRepository = sessionRepository;
        this.workerLeaseRepository = workerLeaseRepository;
        this.worktreeRuntimeService = worktreeRuntimeService;
        this.artifactRepository = artifactRepository;
        this.lifecycleRepository = lifecycleRepository;
        this.recoveryLedgerRepository = recoveryLedgerRepository;
    }

    public DelegatedRunLifecycleSnapshot Capture(bool persist = true)
    {
        var graph = taskGraphService.Load();
        var session = sessionRepository.Load();
        var latestLeases = workerLeaseRepository.Load()
            .GroupBy(item => item.TaskId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.AcquiredAt)
                    .ThenByDescending(item => item.ExpiresAt)
                    .First(),
                StringComparer.Ordinal);
        var latestWorktrees = graph.ListTasks()
            .Select(task => new
            {
                TaskId = task.TaskId,
                Record = worktreeRuntimeService.Load(task.TaskId).FirstOrDefault(),
            })
            .Where(item => item.Record is not null)
            .ToDictionary(item => item.TaskId, item => item.Record!, StringComparer.Ordinal);

        var records = new List<DelegatedRunLifecycleRecord>();
        foreach (var task in graph.ListTasks())
        {
            latestLeases.TryGetValue(task.TaskId, out var lease);
            latestWorktrees.TryGetValue(task.TaskId, out var worktree);
            var execution = artifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId);
            if (!IsLifecycleRelevant(task, lease, execution, worktree))
            {
                continue;
            }

            records.Add(BuildRecord(task, session, lease, execution, worktree));
        }

        var snapshot = new DelegatedRunLifecycleSnapshot
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Records = records
                .OrderBy(record => record.TaskId, StringComparer.Ordinal)
                .ToArray(),
        };

        if (persist)
        {
            lifecycleRepository.Save(snapshot);
        }

        return snapshot;
    }

    public DelegatedRunLifecycleRecord? TryGet(string taskId)
    {
        var snapshot = lifecycleRepository.Load();
        return snapshot.Records.FirstOrDefault(item => string.Equals(item.TaskId, taskId, StringComparison.Ordinal))
            ?? Capture(persist: true).Records.FirstOrDefault(item => string.Equals(item.TaskId, taskId, StringComparison.Ordinal));
    }

    public DelegatedRunLifecycleSnapshot ReconcileKnownDrift()
    {
        var graph = taskGraphService.Load();
        var latestLeases = workerLeaseRepository.Load()
            .GroupBy(item => item.TaskId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.AcquiredAt)
                    .ThenByDescending(item => item.ExpiresAt)
                    .First(),
                StringComparer.Ordinal);

        foreach (var task in graph.ListTasks())
        {
            if (!latestLeases.TryGetValue(task.TaskId, out var lease))
            {
                continue;
            }

            var execution = artifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId);
            var worktree = worktreeRuntimeService.Load(task.TaskId).FirstOrDefault();
            var record = BuildRecord(task, sessionRepository.Load(), lease, execution, worktree);
            if (lease.Status != WorkerLeaseStatus.Expired)
            {
                continue;
            }

            if (task.Status == DomainTaskStatus.Pending && record.State != DelegatedRunLifecycleState.Retryable)
            {
                ReconcileLeaseRecovery(lease, "Reconciled previously expired delegated run drift.");
            }
        }

        return Capture(persist: true);
    }

    public HostRestartRehydrationReport RehydrateAfterHostRestart(string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var actions = new List<string>();
        var leases = workerLeaseRepository.Load().ToList();
        var invalidatedLeases = 0;
        var reconciledTasks = new HashSet<string>(StringComparer.Ordinal);

        foreach (var lease in leases
                     .Where(item => item.Status == WorkerLeaseStatus.Active)
                     .OrderByDescending(item => item.AcquiredAt)
                     .ThenByDescending(item => item.ExpiresAt)
                     .ToArray())
        {
            lease.Complete(WorkerLeaseStatus.Expired, "Resident host restart invalidated the active delegated worker lease.", now);
            invalidatedLeases += 1;
            actions.Add($"Invalidated active delegated lease {lease.LeaseId} for task {lease.TaskId} during host rehydration.");
        }

        if (invalidatedLeases > 0)
        {
            workerLeaseRepository.Save(leases);
        }

        foreach (var lease in leases
                     .Where(item => item.Status == WorkerLeaseStatus.Expired)
                     .OrderByDescending(item => item.AcquiredAt)
                     .ThenByDescending(item => item.ExpiresAt))
        {
            var record = TryGet(lease.TaskId) ?? Capture(persist: true).Records.FirstOrDefault(item => string.Equals(item.TaskId, lease.TaskId, StringComparison.Ordinal));
            if (record is null
                || record.State is DelegatedRunLifecycleState.None or DelegatedRunLifecycleState.Completed)
            {
                continue;
            }

            record = ReconcileLeaseRecovery(lease, reason);
            reconciledTasks.Add(lease.TaskId);
            actions.Add($"Reconciled task {lease.TaskId} to {record.State} during host restart.");
        }

        return new HostRestartRehydrationReport
        {
            ExecutedAt = now,
            InvalidatedLeaseCount = invalidatedLeases,
            ReconciledTaskCount = reconciledTasks.Count,
            Actions = actions,
            Summary = invalidatedLeases == 0 && reconciledTasks.Count == 0
                ? "No delegated worker leases required restart rehydration."
                : $"Invalidated {invalidatedLeases} active lease(s) and reconciled {reconciledTasks.Count} task(s) during host restart.",
        };
    }

    public DelegatedRunLifecycleRecord ReconcileLeaseRecovery(WorkerLeaseRecord lease, string reason, string actorIdentity = nameof(DelegatedWorkerLifecycleReconciliationService))
    {
        var session = sessionRepository.Load();
        var graph = taskGraphService.Load();
        if (!graph.Tasks.TryGetValue(lease.TaskId, out var task))
        {
            return Capture(persist: true).Records.First(record => string.Equals(record.TaskId, lease.TaskId, StringComparison.Ordinal));
        }

        var execution = artifactRepository.TryLoadWorkerExecutionArtifact(lease.TaskId);
        var worktree = worktreeRuntimeService.Load(lease.TaskId).FirstOrDefault();
        var record = BuildRecord(task, session, lease, execution, worktree);
        var decision = BuildRecoveryDecision(record, reason);
        var beforeStatus = task.Status;

        if (TaskStatusTransitionPolicy.IsFinalized(beforeStatus))
        {
            return record;
        }

        if (record.State is DelegatedRunLifecycleState.Orphaned
            or DelegatedRunLifecycleState.Stalled
            or DelegatedRunLifecycleState.ManualReviewRequired)
        {
            if (worktree is not null
                && worktree.State != WorktreeRuntimeState.Quarantined
                && Directory.Exists(worktree.WorktreePath))
            {
                worktreeRuntimeService.QuarantineAndRequestRebuild(task.TaskId, worktree.WorktreePath, decision.Reason);
            }

            task.SetStatus(DomainTaskStatus.Blocked);
            task.SetPlannerReview(new PlannerReview
            {
                Verdict = PlannerVerdict.HumanDecisionRequired,
                Reason = decision.Reason,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = true,
            });
        }
        else if (record.State == DelegatedRunLifecycleState.Retryable)
        {
            task.SetStatus(DomainTaskStatus.Pending);
            task.SetPlannerReview(new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = decision.Reason,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
            });
        }
        else if (record.State == DelegatedRunLifecycleState.Quarantined)
        {
            task.SetStatus(DomainTaskStatus.Blocked);
            task.SetPlannerReview(new PlannerReview
            {
                Verdict = PlannerVerdict.HumanDecisionRequired,
                Reason = decision.Reason,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = true,
            });
        }
        else
        {
            task.SetStatus(DomainTaskStatus.Blocked);
            task.SetPlannerReview(new PlannerReview
            {
                Verdict = PlannerVerdict.HumanDecisionRequired,
                Reason = decision.Reason,
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = true,
            });
        }

        task.RecordRecovery(decision);
        taskGraphService.ReplaceTask(task);

        if (session is not null)
        {
            var preserveExistingBoundary = ShouldPreserveExistingBoundary(session, task.TaskId);
            session.ReleaseWorker(task.TaskId);
            session.RecordRecoveryDecision(task.TaskId, decision, updateLoopState: !preserveExistingBoundary);
            ReconcileSessionState(session, decision, task.TaskId, preserveExistingBoundary);
            sessionRepository.Save(session);
        }

        AppendRecoveryLedgerEntry(task, lease, record, beforeStatus, decision, actorIdentity);

        return Capture(persist: true).Records.First(record => string.Equals(record.TaskId, task.TaskId, StringComparison.Ordinal));
    }

    public DelegatedRunRecoveryLedgerEntry? TryGetLatestRecovery(string taskId)
    {
        return recoveryLedgerRepository.Load().Entries
            .Where(entry => string.Equals(entry.TaskId, taskId, StringComparison.Ordinal))
            .OrderByDescending(entry => entry.RecordedAt)
            .ThenByDescending(entry => entry.RecoveryEntryId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsLifecycleRelevant(
        TaskNode task,
        WorkerLeaseRecord? lease,
        WorkerExecutionArtifact? execution,
        WorktreeRuntimeRecord? worktree)
    {
        return lease is not null
            || execution is not null
            || worktree is not null
            || !string.IsNullOrWhiteSpace(task.LastWorkerRunId)
            || task.Status is DomainTaskStatus.Running or DomainTaskStatus.Blocked or DomainTaskStatus.ApprovalWait;
    }

    private DelegatedRunLifecycleRecord BuildRecord(
        TaskNode task,
        RuntimeSessionState? session,
        WorkerLeaseRecord? lease,
        WorkerExecutionArtifact? execution,
        WorktreeRuntimeRecord? worktree)
    {
        var state = DetermineState(task, lease, execution, worktree);
        var (reasonCode, summary, nextAction, recoveryAction, retryable, requiresOperatorAction) = DescribeState(state, task, lease, execution, worktree);
        var latestRecovery = TryGetLatestRecovery(task.TaskId);

        return new DelegatedRunLifecycleRecord
        {
            TaskId = task.TaskId,
            CardId = task.CardId,
            SessionId = session?.SessionId,
            LeaseId = lease?.LeaseId,
            LeaseStatus = lease?.Status,
            RunId = execution?.Result.RunId ?? task.LastWorkerRunId,
            BackendId = execution?.Result.BackendId ?? task.LastWorkerBackend,
            ProviderId = execution?.Result.ProviderId,
            WorktreePath = worktree?.WorktreePath,
            WorktreeState = worktree?.State,
            TaskStatus = task.Status,
            State = state,
            RecoveryAction = recoveryAction,
            Retryable = retryable,
            RequiresOperatorAction = requiresOperatorAction,
            ReasonCode = reasonCode,
            Summary = summary,
            RecommendedNextAction = nextAction,
            LatestRecoveryEntryId = latestRecovery?.RecoveryEntryId,
            LatestRecoveryActorIdentity = latestRecovery?.ActorIdentity,
            LatestRecoveryRecordedAt = latestRecovery?.RecordedAt,
            RepoTruthAnchor = Path.Combine(paths.TaskNodesRoot, $"{task.TaskId}.json"),
            PlatformTruthAnchor = paths.PlatformDelegatedRunLifecycleLiveStateFile,
            ObservedAt = DateTimeOffset.UtcNow,
        };
    }

    private void AppendRecoveryLedgerEntry(
        TaskNode task,
        WorkerLeaseRecord lease,
        DelegatedRunLifecycleRecord record,
        DomainTaskStatus beforeStatus,
        WorkerRecoveryDecision decision,
        string actorIdentity)
    {
        var snapshot = recoveryLedgerRepository.Load();
        var entries = snapshot.Entries.ToList();
        entries.Add(new DelegatedRunRecoveryLedgerEntry
        {
            TaskId = task.TaskId,
            CardId = task.CardId,
            RunId = record.RunId,
            LeaseId = lease.LeaseId,
            LeaseStatus = lease.Status,
            RecoveryAction = decision.Action,
            RecoveryReason = decision.Reason,
            ActorIdentity = actorIdentity,
            PolicyIdentity = nameof(DelegatedWorkerLifecycleReconciliationService),
            LifecycleState = record.State.ToString(),
            TaskStatusBefore = beforeStatus,
            TaskStatusAfter = task.Status,
            RecommendedNextAction = decision.Reason,
            RecordedAt = DateTimeOffset.UtcNow,
        });
        recoveryLedgerRepository.Save(new DelegatedRunRecoveryLedgerSnapshot
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Entries = entries
                .OrderBy(entry => entry.RecordedAt)
                .ThenBy(entry => entry.RecoveryEntryId, StringComparer.Ordinal)
                .ToArray(),
        });
    }

    private static DelegatedRunLifecycleState DetermineState(
        TaskNode task,
        WorkerLeaseRecord? lease,
        WorkerExecutionArtifact? execution,
        WorktreeRuntimeRecord? worktree)
    {
        if (TaskStatusTransitionPolicy.IsFinalized(task.Status))
        {
            return DelegatedRunLifecycleState.Completed;
        }

        if (worktree?.State == WorktreeRuntimeState.Quarantined)
        {
            return DelegatedRunLifecycleState.Quarantined;
        }

        if (execution?.Result.Succeeded == true)
        {
            return DelegatedRunLifecycleState.Completed;
        }

        if (lease?.Status == WorkerLeaseStatus.Active && task.Status == DomainTaskStatus.Running)
        {
            return DelegatedRunLifecycleState.Running;
        }

        if (lease?.Status == WorkerLeaseStatus.Active)
        {
            return DelegatedRunLifecycleState.Stalled;
        }

        if (lease?.Status == WorkerLeaseStatus.Quarantined)
        {
            return DelegatedRunLifecycleState.Quarantined;
        }

        if (lease?.Status == WorkerLeaseStatus.Expired)
        {
            if (execution?.Result.Retryable == true)
            {
                return DelegatedRunLifecycleState.Retryable;
            }

            if (worktree is not null && Directory.Exists(worktree.WorktreePath))
            {
                return DelegatedRunLifecycleState.Orphaned;
            }

            if (!string.IsNullOrWhiteSpace(task.LastWorkerRunId) || execution is not null)
            {
                return DelegatedRunLifecycleState.ManualReviewRequired;
            }

            return DelegatedRunLifecycleState.Expired;
        }

        if (task.Status == DomainTaskStatus.Blocked)
        {
            return DelegatedRunLifecycleState.Blocked;
        }

        return DelegatedRunLifecycleState.None;
    }

    private static (string ReasonCode, string Summary, string NextAction, WorkerRecoveryAction RecoveryAction, bool Retryable, bool RequiresOperatorAction) DescribeState(
        DelegatedRunLifecycleState state,
        TaskNode task,
        WorkerLeaseRecord? lease,
        WorkerExecutionArtifact? execution,
        WorktreeRuntimeRecord? worktree)
    {
        return state switch
        {
            DelegatedRunLifecycleState.Running => (
                "delegated_run_running",
                $"Delegated worker run for {task.TaskId} is active under lease {lease?.LeaseId}.",
                "observe current delegated execution or inspect worker output",
                WorkerRecoveryAction.None,
                false,
                false),
            DelegatedRunLifecycleState.Stalled => (
                "delegated_run_stalled",
                $"Delegated worker lease {lease?.LeaseId} is active, but task truth is {task.Status}.",
                "reconcile lease/task truth before dispatching more work",
                WorkerRecoveryAction.EscalateToOperator,
                false,
                true),
            DelegatedRunLifecycleState.Expired => (
                "delegated_run_expired",
                $"Delegated worker lease {lease?.LeaseId} expired without enough evidence to cleanly retry or complete {task.TaskId}.",
                "inspect runtime drift and choose whether to rerun or block the task",
                WorkerRecoveryAction.BlockTask,
                false,
                true),
            DelegatedRunLifecycleState.Orphaned => (
                "delegated_run_orphaned",
                $"Delegated worker lease {lease?.LeaseId} expired while the worktree '{worktree?.WorktreePath}' still exists for {task.TaskId}.",
                "quarantine the worktree and require manual review before rerunning",
                WorkerRecoveryAction.EscalateToOperator,
                false,
                true),
            DelegatedRunLifecycleState.Completed => (
                "delegated_run_completed",
                $"Delegated worker execution for {task.TaskId} has converged to a completed state.",
                "observe downstream tasks or review the completed result",
                WorkerRecoveryAction.None,
                false,
                false),
            DelegatedRunLifecycleState.Quarantined => (
                "delegated_run_quarantined",
                $"Delegated worker worktree for {task.TaskId} is quarantined and requires explicit recovery planning.",
                "inspect quarantine evidence and decide whether to rebuild or retry",
                WorkerRecoveryAction.RebuildWorktree,
                false,
                true),
            DelegatedRunLifecycleState.Retryable => (
                "delegated_run_retryable",
                $"Delegated worker run for {task.TaskId} expired after a retryable worker outcome.",
                "task may be rerun after reviewing the retry guidance",
                WorkerRecoveryAction.Retry,
                true,
                false),
            DelegatedRunLifecycleState.ManualReviewRequired => (
                "delegated_run_manual_review_required",
                $"Delegated worker run for {task.TaskId} lost tracking and requires manual review before continuing.",
                "inspect runtime drift, worker evidence, and decide whether to rerun or block",
                WorkerRecoveryAction.EscalateToOperator,
                false,
                true),
            DelegatedRunLifecycleState.Blocked => (
                "delegated_run_blocked",
                $"Delegated worker lifecycle for {task.TaskId} is blocked: {task.LastRecoveryReason ?? task.LastWorkerSummary ?? task.PlannerReview.Reason ?? "operator action required"}.",
                "inspect blocked reason and choose the next recovery action",
                WorkerRecoveryAction.BlockTask,
                false,
                true),
            _ => (
                "delegated_run_none",
                $"No delegated worker lifecycle is currently tracked for {task.TaskId}.",
                "observe current task truth",
                WorkerRecoveryAction.None,
                false,
                false),
        };
    }

    private static WorkerRecoveryDecision BuildRecoveryDecision(DelegatedRunLifecycleRecord record, string reason)
    {
        return record.State switch
        {
            DelegatedRunLifecycleState.Retryable => new WorkerRecoveryDecision
            {
                Action = WorkerRecoveryAction.Retry,
                ReasonCode = record.ReasonCode,
                Reason = $"{record.Summary} {reason}".Trim(),
                Actionability = RuntimeActionability.WorkerActionable,
                AutoApplied = true,
            },
            DelegatedRunLifecycleState.Quarantined => new WorkerRecoveryDecision
            {
                Action = WorkerRecoveryAction.RebuildWorktree,
                ReasonCode = record.ReasonCode,
                Reason = $"{record.Summary} {reason}".Trim(),
                Actionability = RuntimeActionability.HumanActionable,
            },
            _ => new WorkerRecoveryDecision
            {
                Action = record.RecoveryAction == WorkerRecoveryAction.None ? WorkerRecoveryAction.EscalateToOperator : record.RecoveryAction,
                ReasonCode = record.ReasonCode,
                Reason = $"{record.Summary} {reason}".Trim(),
                Actionability = RuntimeActionability.HumanActionable,
            },
        };
    }

    private static bool ShouldPreserveExistingBoundary(RuntimeSessionState session, string recoveredTaskId)
    {
        if (session.PendingPermissionRequestIds.Count > 0)
        {
            return true;
        }

        if (session.ReviewPendingTaskIds.Count > 0
            && !session.ReviewPendingTaskIds.Contains(recoveredTaskId, StringComparer.Ordinal))
        {
            return true;
        }

        if (session.ActiveTaskIds.Count > 0
            && !session.ActiveTaskIds.Contains(recoveredTaskId, StringComparer.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static void ReconcileSessionState(RuntimeSessionState session, WorkerRecoveryDecision decision, string recoveredTaskId, bool preserveExistingBoundary)
    {
        if (session.PendingPermissionRequestIds.Count > 0)
        {
            if (!preserveExistingBoundary)
            {
                session.MarkApprovalWait(
                    session.LastTaskId ?? session.CurrentTaskId ?? string.Empty,
                    session.PendingPermissionRequestIds,
                    session.LastPermissionSummary ?? decision.Reason);
            }

            return;
        }

        if (session.ReviewPendingTaskIds.Count > 0)
        {
            if (!preserveExistingBoundary)
            {
                var reviewTaskId = session.ReviewPendingTaskIds.Contains(recoveredTaskId, StringComparer.Ordinal)
                    ? recoveredTaskId
                    : session.ReviewPendingTaskIds[0];
                session.MarkReviewWait(reviewTaskId, decision.Reason);
            }

            return;
        }

        if (session.ActiveTaskIds.Count > 0)
        {
            if (!preserveExistingBoundary)
            {
                session.MarkExecuting(session.ActiveTaskIds, decision.Reason);
            }

            return;
        }

        session.MarkIdle(decision.Reason, decision.Actionability);
    }
}
