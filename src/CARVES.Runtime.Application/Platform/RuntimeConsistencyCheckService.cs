using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeConsistencyCheckService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeSessionRepository sessionRepository;
    private readonly IWorkerLeaseRepository workerLeaseRepository;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly IDelegatedRunLifecycleRepository lifecycleRepository;

    public RuntimeConsistencyCheckService(
        string repoRoot,
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        IRuntimeSessionRepository sessionRepository,
        IWorkerLeaseRepository workerLeaseRepository,
        IRuntimeArtifactRepository artifactRepository,
        IDelegatedRunLifecycleRepository lifecycleRepository)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.sessionRepository = sessionRepository;
        this.workerLeaseRepository = workerLeaseRepository;
        this.artifactRepository = artifactRepository;
        this.lifecycleRepository = lifecycleRepository;
    }

    public RuntimeConsistencyReport Run(RuntimeConsistencyHostSnapshot? hostSnapshot = null)
    {
        var graph = taskGraphService.Load();
        var session = sessionRepository.Load();
        var leases = workerLeaseRepository.Load();
        var permissionArtifacts = artifactRepository.LoadWorkerPermissionArtifacts()
            .ToDictionary(item => item.TaskId, StringComparer.Ordinal);
        var lifecycleRecords = lifecycleRepository.Load().Records
            .ToDictionary(item => item.TaskId, StringComparer.Ordinal);
        var findings = new List<RuntimeConsistencyFinding>();
        var latestLeaseByTaskId = leases
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
            var workerExecution = artifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId);
            permissionArtifacts.TryGetValue(task.TaskId, out var permissionArtifact);
            latestLeaseByTaskId.TryGetValue(task.TaskId, out var latestLease);
            lifecycleRecords.TryGetValue(task.TaskId, out var lifecycle);

            EvaluateWorkerExecutionTruth(task, workerExecution, findings);
            EvaluateLeaseTruth(task, latestLease, workerExecution, lifecycle, findings);
            EvaluatePermissionTruth(task, permissionArtifact, findings);
        }

        EvaluateSessionTruth(graph, session, leases, findings);
        EvaluateHostTruth(session, hostSnapshot, findings);

        return new RuntimeConsistencyReport
        {
            RepoRoot = repoRoot,
            CheckedAt = DateTimeOffset.UtcNow,
            SessionStatus = session?.Status.ToString() ?? "none",
            ActiveLeaseCount = leases.Count(item => item.Status == WorkerLeaseStatus.Active),
            RunningTaskCount = graph.Tasks.Values.Count(item => item.Status == DomainTaskStatus.Running),
            PendingApprovalCount = permissionArtifacts.Values.SelectMany(item => item.Requests).Count(item => item.State == WorkerPermissionState.Pending),
            HostSnapshot = hostSnapshot,
            Findings = findings
                .OrderByDescending(item => item.Severity)
                .ThenBy(item => item.TaskId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(item => item.Category, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private void EvaluateWorkerExecutionTruth(
        TaskNode task,
        WorkerExecutionArtifact? workerExecution,
        ICollection<RuntimeConsistencyFinding> findings)
    {
        if (!string.IsNullOrWhiteSpace(task.LastWorkerRunId) && workerExecution is null)
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "worker_execution_missing",
                Severity = RuntimeConsistencySeverity.Warning,
                TaskId = task.TaskId,
                RunId = task.LastWorkerRunId,
                Summary = $"Task {task.TaskId} references worker run {task.LastWorkerRunId}, but no execution artifact was found.",
                LikelyCause = "Task truth was updated, but the worker execution artifact was lost or never persisted.",
                RecommendedAction = $"Inspect {task.TaskId} and regenerate worker truth before retrying delegated execution.",
                RepoTruthAnchor = TaskNodePath(task.TaskId),
                PlatformTruthAnchor = WorkerExecutionArtifactPath(task.TaskId),
            });
            return;
        }

        if (workerExecution is null)
        {
            return;
        }

        var runId = workerExecution.Result.RunId;
        if (string.IsNullOrWhiteSpace(task.LastWorkerRunId))
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "worker_run_anchor_missing",
                Severity = RuntimeConsistencySeverity.Warning,
                TaskId = task.TaskId,
                RunId = runId,
                Summary = $"Task {task.TaskId} has a worker execution artifact, but task truth has no run anchor.",
                LikelyCause = "The worker artifact was persisted, but the task node was not updated with the matching run metadata.",
                RecommendedAction = $"Inspect {task.TaskId} and reconcile the worker run anchor before relying on delegated result summaries.",
                RepoTruthAnchor = TaskNodePath(task.TaskId),
                PlatformTruthAnchor = WorkerExecutionArtifactPath(task.TaskId),
            });
            return;
        }

        if (!string.Equals(task.LastWorkerRunId, runId, StringComparison.Ordinal))
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "worker_run_anchor_mismatch",
                Severity = RuntimeConsistencySeverity.Error,
                TaskId = task.TaskId,
                RunId = runId,
                Summary = $"Task {task.TaskId} points at worker run {task.LastWorkerRunId}, but the latest execution artifact reports {runId}.",
                LikelyCause = "A delegated worker run completed, but task truth and worker artifact truth diverged across retries or recovery.",
                RecommendedAction = $"Reconcile {task.TaskId} so the task node, worker artifact, and any incident records all reference the same invocation chain.",
                RepoTruthAnchor = TaskNodePath(task.TaskId),
                PlatformTruthAnchor = WorkerExecutionArtifactPath(task.TaskId),
            });
        }
    }

    private void EvaluateLeaseTruth(
        TaskNode task,
        WorkerLeaseRecord? latestLease,
        WorkerExecutionArtifact? workerExecution,
        DelegatedRunLifecycleRecord? lifecycle,
        ICollection<RuntimeConsistencyFinding> findings)
    {
        if (latestLease is null)
        {
            if (task.Status == DomainTaskStatus.Running)
            {
                findings.Add(new RuntimeConsistencyFinding
                {
                    Category = "running_task_without_lease",
                    Severity = RuntimeConsistencySeverity.Error,
                    TaskId = task.TaskId,
                    Summary = $"Task {task.TaskId} is marked running, but no worker lease exists.",
                    LikelyCause = "Delegated execution started without a surviving lease record, or the lease was dropped before task truth was reconciled.",
                    RecommendedAction = $"Inspect {task.TaskId}, terminate any orphaned worker process, and reconcile task state before rerunning.",
                    RepoTruthAnchor = TaskNodePath(task.TaskId),
                    PlatformTruthAnchor = paths.PlatformWorkerLeasesLiveStateFile,
                });
            }

            return;
        }

        if (latestLease.Status == WorkerLeaseStatus.Active && task.Status != DomainTaskStatus.Running)
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "active_lease_task_mismatch",
                Severity = RuntimeConsistencySeverity.Error,
                TaskId = task.TaskId,
                LeaseId = latestLease.LeaseId,
                RunId = task.LastWorkerRunId,
                Summary = $"Worker lease {latestLease.LeaseId} is active for {task.TaskId}, but the task status is {task.Status}.",
                LikelyCause = "The worker lease is still live while task truth already moved to a non-running state.",
                RecommendedAction = $"Inspect {task.TaskId}, reconcile the live worker lease, and only then accept the current task state.",
                RepoTruthAnchor = TaskNodePath(task.TaskId),
                PlatformTruthAnchor = paths.PlatformWorkerLeasesLiveStateFile,
            });
        }

        if (latestLease.Status != WorkerLeaseStatus.Active && task.Status == DomainTaskStatus.Running)
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "running_task_lost_lease",
                Severity = RuntimeConsistencySeverity.Error,
                TaskId = task.TaskId,
                LeaseId = latestLease.LeaseId,
                RunId = task.LastWorkerRunId,
                Summary = $"Task {task.TaskId} is still marked running, but its latest lease is {latestLease.Status}.",
                LikelyCause = "The delegated worker stopped heartbeating or the lease was released without updating task truth.",
                RecommendedAction = $"Treat {task.TaskId} as a reconciliation candidate before dispatching more worker runs.",
                RepoTruthAnchor = TaskNodePath(task.TaskId),
                PlatformTruthAnchor = paths.PlatformWorkerLeasesLiveStateFile,
            });
        }

        if (latestLease.Status == WorkerLeaseStatus.Expired && task.Status == DomainTaskStatus.Pending)
        {
            if (lifecycle?.State == DelegatedRunLifecycleState.Retryable)
            {
                return;
            }

            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "expired_run_collapsed_to_pending",
                Severity = RuntimeConsistencySeverity.Warning,
                TaskId = task.TaskId,
                LeaseId = latestLease.LeaseId,
                RunId = workerExecution?.Result.RunId ?? task.LastWorkerRunId,
                Summary = $"Task {task.TaskId} returned to pending after lease {latestLease.LeaseId} expired.",
                LikelyCause = "A delegated run started and then lost tracking, but task truth fell back to pending instead of preserving an explicit reconciled state.",
                RecommendedAction = $"Classify {task.TaskId} as reconciliating, stalled, or blocked before the next delegated run.",
                RepoTruthAnchor = TaskNodePath(task.TaskId),
                PlatformTruthAnchor = paths.PlatformWorkerLeasesLiveStateFile,
            });
        }
    }

    private void EvaluatePermissionTruth(
        TaskNode task,
        WorkerPermissionArtifact? permissionArtifact,
        ICollection<RuntimeConsistencyFinding> findings)
    {
        if (permissionArtifact is null)
        {
            return;
        }

        var pendingRequests = permissionArtifact.Requests.Where(item => item.State == WorkerPermissionState.Pending).ToArray();
        var deniedRequests = permissionArtifact.Requests.Where(item => item.State is WorkerPermissionState.Denied or WorkerPermissionState.TimedOut).ToArray();
        var allowedRequests = permissionArtifact.Requests.Where(item => item.State == WorkerPermissionState.Allowed).ToArray();

        if (pendingRequests.Length > 0 && task.Status != DomainTaskStatus.ApprovalWait)
        {
            var pending = pendingRequests[0];
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "pending_permission_task_mismatch",
                Severity = RuntimeConsistencySeverity.Error,
                TaskId = task.TaskId,
                RunId = permissionArtifact.RunId,
                PermissionRequestId = pending.PermissionRequestId,
                Summary = $"Task {task.TaskId} has pending permission requests, but task truth is {task.Status}.",
                LikelyCause = "Permission approval state was persisted, but the task node was not moved into approval_wait.",
                RecommendedAction = $"Reconcile {task.TaskId} and session approval state before delegating more worker activity.",
                RepoTruthAnchor = TaskNodePath(task.TaskId),
                PlatformTruthAnchor = WorkerPermissionArtifactPath(task.TaskId),
            });
        }

        if (deniedRequests.Length > 0 && task.Status != DomainTaskStatus.Blocked)
        {
            var denied = deniedRequests[0];
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "denied_permission_task_mismatch",
                Severity = RuntimeConsistencySeverity.Warning,
                TaskId = task.TaskId,
                RunId = permissionArtifact.RunId,
                PermissionRequestId = denied.PermissionRequestId,
                Summary = $"Task {task.TaskId} has denied/timed-out permission decisions, but task truth is {task.Status}.",
                LikelyCause = "Permission denial was recorded, but the execution consequence did not propagate back into task state.",
                RecommendedAction = $"Inspect {task.TaskId} and align blocked state with the recorded permission outcome.",
                RepoTruthAnchor = TaskNodePath(task.TaskId),
                PlatformTruthAnchor = WorkerPermissionArtifactPath(task.TaskId),
            });
        }

        if (allowedRequests.Length > 0 && task.Status == DomainTaskStatus.ApprovalWait)
        {
            var allowed = allowedRequests[0];
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "allowed_permission_still_waiting",
                Severity = RuntimeConsistencySeverity.Warning,
                TaskId = task.TaskId,
                RunId = permissionArtifact.RunId,
                PermissionRequestId = allowed.PermissionRequestId,
                Summary = $"Task {task.TaskId} is still waiting for approval even though a permission decision was already allowed.",
                LikelyCause = "Approval truth was recorded, but task/session state was not released back to dispatchable execution.",
                RecommendedAction = $"Reconcile {task.TaskId} before retrying the delegated worker run.",
                RepoTruthAnchor = TaskNodePath(task.TaskId),
                PlatformTruthAnchor = WorkerPermissionArtifactPath(task.TaskId),
            });
        }
    }

    private void EvaluateSessionTruth(
        Carves.Runtime.Domain.Tasks.TaskGraph graph,
        Carves.Runtime.Domain.Runtime.RuntimeSessionState? session,
        IReadOnlyList<WorkerLeaseRecord> leases,
        ICollection<RuntimeConsistencyFinding> findings)
    {
        if (session is null)
        {
            return;
        }

        var activeLeases = leases.Where(item => item.Status == WorkerLeaseStatus.Active).ToArray();
        var runningTaskIds = graph.Tasks.Values
            .Where(item => item.Status == DomainTaskStatus.Running)
            .Select(item => item.TaskId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var sessionTaskIds = session.ActiveTaskIds
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var pendingPermissionIds = artifactRepository.LoadWorkerPermissionArtifacts()
            .SelectMany(item => item.Requests)
            .Where(item => item.State == WorkerPermissionState.Pending)
            .Select(item => item.PermissionRequestId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        if (session.ActiveWorkerCount != activeLeases.Length)
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "session_active_worker_mismatch",
                Severity = RuntimeConsistencySeverity.Error,
                Summary = $"Session reports {session.ActiveWorkerCount} active workers, but lease truth reports {activeLeases.Length}.",
                LikelyCause = "Session summary and worker lease truth drifted apart during delegated execution or recovery.",
                RecommendedAction = "Reconcile session worker counters from lease truth before trusting operator summaries.",
                RepoTruthAnchor = paths.RuntimeSessionFile,
                PlatformTruthAnchor = paths.PlatformWorkerLeasesLiveStateFile,
            });
        }

        if (!sessionTaskIds.SequenceEqual(runningTaskIds, StringComparer.Ordinal))
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "session_active_task_mismatch",
                Severity = RuntimeConsistencySeverity.Warning,
                Summary = $"Session active tasks [{string.Join(", ", sessionTaskIds)}] do not match running tasks [{string.Join(", ", runningTaskIds)}].",
                LikelyCause = "Session truth and task graph truth drifted during worker lifecycle updates.",
                RecommendedAction = "Reconcile active task projections before using status or dashboard summaries for operator decisions.",
                RepoTruthAnchor = paths.RuntimeSessionFile,
                PlatformTruthAnchor = paths.TaskGraphFile,
            });
        }

        if (session.Status == Carves.Runtime.Domain.Runtime.RuntimeSessionStatus.Idle
            && (activeLeases.Length > 0 || runningTaskIds.Length > 0))
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "idle_session_with_live_execution",
                Severity = RuntimeConsistencySeverity.Error,
                Summary = "Session is idle while worker leases or running tasks still exist.",
                LikelyCause = "Runtime session state was released before delegated worker truth fully converged.",
                RecommendedAction = "Inspect the delegated run lifecycle and reconcile leases, task truth, and session summary before resuming work.",
                RepoTruthAnchor = paths.RuntimeSessionFile,
                PlatformTruthAnchor = paths.PlatformWorkerLeasesLiveStateFile,
            });
        }

        if (session.Status == Carves.Runtime.Domain.Runtime.RuntimeSessionStatus.ApprovalWait
            && pendingPermissionIds.Length == 0)
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "approval_wait_without_pending_requests",
                Severity = RuntimeConsistencySeverity.Warning,
                Summary = "Session is in approval_wait, but no pending permission requests remain in artifact truth.",
                LikelyCause = "Permission decisions were resolved without fully reconciling session state.",
                RecommendedAction = "Refresh session approval state from worker permission truth before delegating additional runs.",
                RepoTruthAnchor = paths.RuntimeSessionFile,
                PlatformTruthAnchor = paths.WorkerPermissionArtifactsRoot,
            });
        }

        if (!session.PendingPermissionRequestIds.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(pendingPermissionIds, StringComparer.Ordinal))
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "session_permission_queue_mismatch",
                Severity = RuntimeConsistencySeverity.Warning,
                Summary = $"Session pending approval ids [{string.Join(", ", session.PendingPermissionRequestIds)}] do not match permission artifact truth [{string.Join(", ", pendingPermissionIds)}].",
                LikelyCause = "Permission queue state drifted between session persistence and worker permission artifacts.",
                RecommendedAction = "Reconcile session approval queues from permission artifact truth.",
                RepoTruthAnchor = paths.RuntimeSessionFile,
                PlatformTruthAnchor = paths.WorkerPermissionArtifactsRoot,
            });
        }
    }

    private void EvaluateHostTruth(
        Carves.Runtime.Domain.Runtime.RuntimeSessionState? session,
        RuntimeConsistencyHostSnapshot? hostSnapshot,
        ICollection<RuntimeConsistencyFinding> findings)
    {
        if (hostSnapshot is null)
        {
            return;
        }

        if (hostSnapshot.DescriptorExists && !hostSnapshot.LiveHostRunning)
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "stale_host_descriptor",
                Severity = RuntimeConsistencySeverity.Warning,
                Summary = "Host discovery descriptor exists, but no live host responded.",
                LikelyCause = "Cold host truth still points at an old host process or a previously failed resident host.",
                RecommendedAction = "Clean stale host descriptors or restart the resident host before relying on live/cold host status.",
                RepoTruthAnchor = paths.RuntimeSessionFile,
                PlatformTruthAnchor = hostSnapshot.DescriptorPath,
            });
        }

        if (string.Equals(hostSnapshot.SnapshotState, "Stale", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "stale_host_snapshot",
                Severity = RuntimeConsistencySeverity.Warning,
                Summary = "Persisted host snapshot is stale and should not be treated as live runtime truth.",
                LikelyCause = "Resident host shutdown or crash left a persisted host snapshot behind without a matching live host.",
                RecommendedAction = "Restart the resident host or reconcile host snapshot truth before relying on cold operator summaries.",
                RepoTruthAnchor = paths.RuntimeSessionFile,
                PlatformTruthAnchor = hostSnapshot.SnapshotPath ?? hostSnapshot.DescriptorPath,
            });
        }

        if (hostSnapshot.LiveHostRunning && !hostSnapshot.DescriptorExists)
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "live_host_without_descriptor",
                Severity = RuntimeConsistencySeverity.Error,
                Summary = "A live host is serving requests, but no discovery descriptor exists.",
                LikelyCause = "Resident host state is live, but cold discovery truth was not persisted or was deleted.",
                RecommendedAction = "Rehydrate the host descriptor before depending on thin-client routing.",
                RepoTruthAnchor = paths.RuntimeSessionFile,
                PlatformTruthAnchor = hostSnapshot.DescriptorPath,
            });
        }

        if (!hostSnapshot.LiveHostRunning
            && session is not null
            && session.Status == Carves.Runtime.Domain.Runtime.RuntimeSessionStatus.Executing)
        {
            findings.Add(new RuntimeConsistencyFinding
            {
                Category = "executing_session_without_live_host",
                Severity = RuntimeConsistencySeverity.Error,
                Summary = "Session truth says executing, but no live host responded during verification.",
                LikelyCause = "Session truth was not rehydrated after host shutdown or delegated execution lost its live host anchor.",
                RecommendedAction = "Reconcile session truth against the actual host lifecycle before dispatching more work.",
                RepoTruthAnchor = paths.RuntimeSessionFile,
                PlatformTruthAnchor = hostSnapshot.SnapshotPath ?? hostSnapshot.DescriptorPath,
            });
        }
    }

    private string TaskNodePath(string taskId)
    {
        return Path.Combine(paths.TaskNodesRoot, $"{taskId}.json");
    }

    private string WorkerExecutionArtifactPath(string taskId)
    {
        return Path.Combine(paths.WorkerExecutionArtifactsRoot, $"{taskId}.json");
    }

    private string WorkerPermissionArtifactPath(string taskId)
    {
        return Path.Combine(paths.WorkerPermissionArtifactsRoot, $"{taskId}.json");
    }
}
