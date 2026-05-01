using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.ControlPlane;
using Carves.Runtime.Infrastructure.Git;
using Carves.Runtime.Infrastructure.Persistence;
using Carves.Runtime.Infrastructure.Processes;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Infrastructure.Platform;

public sealed class LocalRepoRuntimeGateway : IRepoRuntimeGateway
{
    public RepoRuntimeGatewayMode GatewayMode => RepoRuntimeGatewayMode.Local;

    public RepoRuntimeGatewayHealth GetHealth(RepoDescriptor descriptor)
    {
        var repoExists = Directory.Exists(descriptor.RepoPath);
        var aiExists = Directory.Exists(Path.Combine(descriptor.RepoPath, ".ai"));
        var reachable = repoExists && aiExists;
        return new RepoRuntimeGatewayHealth(
            GatewayMode,
            reachable ? RepoRuntimeGatewayHealthState.Healthy : RepoRuntimeGatewayHealthState.Unreachable,
            reachable,
            reachable ? "Repo-local runtime gateway is healthy." : "Repo path or .ai control plane is missing.",
            DateTimeOffset.UtcNow);
    }

    public RepoRuntimeCommandResult Execute(RepoRuntimeCommandRequest request)
    {
        var descriptor = new RepoDescriptor
        {
            RepoId = request.RepoId ?? Path.GetFileName(request.RepoPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            RepoPath = request.RepoPath,
            Stage = RuntimeStageReader.TryRead(request.RepoPath) ?? "Unknown",
        };

        return request.Operation switch
        {
            RepoRuntimeGatewayOperation.LoadSummary => CommandResult(LoadSummary(descriptor), "Summary loaded."),
            RepoRuntimeGatewayOperation.StartSession => CommandResult(Start(descriptor, request.DryRun), "Session started."),
            RepoRuntimeGatewayOperation.ResumeSession => CommandResult(Resume(descriptor, request.Reason ?? "Resumed through gateway."), request.Reason ?? "Resumed through gateway."),
            RepoRuntimeGatewayOperation.PauseSession => CommandResult(Pause(descriptor, request.Reason ?? "Paused through gateway."), request.Reason ?? "Paused through gateway."),
            RepoRuntimeGatewayOperation.StopSession => CommandResult(Stop(descriptor, request.Reason ?? "Stopped through gateway."), request.Reason ?? "Stopped through gateway."),
            RepoRuntimeGatewayOperation.RecoverLease => RecoverLease(request),
            _ => new RepoRuntimeCommandResult(false, GatewayMode, RepoRuntimeGatewayHealthState.Degraded, $"Unsupported gateway operation '{request.Operation}'.", null, request.TaskId, null),
        };
    }

    public RepoRuntimeSummary LoadSummary(RepoDescriptor descriptor)
    {
        var paths = ControlPlanePaths.FromRepoRoot(descriptor.RepoPath);
        var taskGraph = new JsonTaskGraphRepository(paths).Load();
        var session = new JsonRuntimeSessionRepository(paths).Load();
        var opportunities = new JsonOpportunityRepository(paths).Load();
        return new RepoRuntimeSummary(
            descriptor.RepoId,
            descriptor.RepoPath,
            RuntimeStageReader.TryRead(descriptor.RepoPath) ?? descriptor.Stage,
            session?.Status,
            session?.CurrentActionability ?? RuntimeActionability.None,
            opportunities.Items.Count(item => item.Status == OpportunityStatus.Open),
            taskGraph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Pending),
            taskGraph.Tasks.Values.Count(task => task.Status == DomainTaskStatus.Review),
            session?.StopReason,
            session?.WaitingReason,
            session?.SessionId,
            DateTimeOffset.UtcNow);
    }

    public RuntimeSessionState Start(RepoDescriptor descriptor, bool dryRun)
    {
        var paths = ControlPlanePaths.FromRepoRoot(descriptor.RepoPath);
        var sessionRepository = new JsonRuntimeSessionRepository(paths);
        var session = RuntimeSessionState.Start(descriptor.RepoPath, dryRun);
        sessionRepository.Save(session);
        Sync(paths, session);
        return session;
    }

    public RuntimeSessionState Resume(RepoDescriptor descriptor, string reason)
    {
        var paths = ControlPlanePaths.FromRepoRoot(descriptor.RepoPath);
        var sessionRepository = new JsonRuntimeSessionRepository(paths);
        var session = sessionRepository.Load() ?? RuntimeSessionState.Start(descriptor.RepoPath, false);
        session.Resume(reason);
        sessionRepository.Save(session);
        Sync(paths, session);
        return session;
    }

    public RuntimeSessionState Pause(RepoDescriptor descriptor, string reason)
    {
        var paths = ControlPlanePaths.FromRepoRoot(descriptor.RepoPath);
        var sessionRepository = new JsonRuntimeSessionRepository(paths);
        var session = sessionRepository.Load() ?? RuntimeSessionState.Start(descriptor.RepoPath, false);
        session.Pause(reason);
        sessionRepository.Save(session);
        Sync(paths, session);
        return session;
    }

    public RuntimeSessionState Stop(RepoDescriptor descriptor, string reason)
    {
        var paths = ControlPlanePaths.FromRepoRoot(descriptor.RepoPath);
        var sessionRepository = new JsonRuntimeSessionRepository(paths);
        var session = sessionRepository.Load() ?? RuntimeSessionState.Start(descriptor.RepoPath, false);
        session.Stop(reason);
        sessionRepository.Save(session);
        Sync(paths, session);
        return session;
    }

    public RuntimeSessionState? LoadSession(RepoDescriptor descriptor)
    {
        return new JsonRuntimeSessionRepository(ControlPlanePaths.FromRepoRoot(descriptor.RepoPath)).Load();
    }

    public OpportunitySnapshot LoadOpportunities(RepoDescriptor descriptor)
    {
        return new JsonOpportunityRepository(ControlPlanePaths.FromRepoRoot(descriptor.RepoPath)).Load();
    }

    public DomainTaskGraph LoadTaskGraph(RepoDescriptor descriptor)
    {
        return new JsonTaskGraphRepository(ControlPlanePaths.FromRepoRoot(descriptor.RepoPath)).Load();
    }

    private static void Sync(ControlPlanePaths paths, RuntimeSessionState session)
    {
        var markdownSync = new MarkdownSyncService(paths, new MarkdownProjector(), new ControlPlaneLockService(paths.RepoRoot));
        var taskGraph = new JsonTaskGraphRepository(paths).Load();
        markdownSync.Sync(taskGraph, session: session);
    }

    private static RepoRuntimeCommandResult CommandResult(RepoRuntimeSummary summary, string reason)
    {
        return new RepoRuntimeCommandResult(true, RepoRuntimeGatewayMode.Local, RepoRuntimeGatewayHealthState.Healthy, reason, summary.ActiveSessionId, null, summary.SessionStatus?.ToString());
    }

    private static RepoRuntimeCommandResult CommandResult(RuntimeSessionState session, string reason)
    {
        return new RepoRuntimeCommandResult(true, RepoRuntimeGatewayMode.Local, RepoRuntimeGatewayHealthState.Healthy, reason, session.SessionId, null, session.Status.ToString());
    }

    private static RepoRuntimeCommandResult RecoverLease(RepoRuntimeCommandRequest request)
    {
        var paths = ControlPlanePaths.FromRepoRoot(request.RepoPath);
        var lockService = new ControlPlaneLockService(paths.RepoRoot);
        var sessionRepository = new JsonRuntimeSessionRepository(paths);
        var session = sessionRepository.Load();
        if (session is null)
        {
            return new RepoRuntimeCommandResult(false, RepoRuntimeGatewayMode.Local, RepoRuntimeGatewayHealthState.Degraded, "No runtime session exists for lease recovery.", null, request.TaskId, null);
        }

        if (!string.IsNullOrWhiteSpace(request.SessionId) && !string.Equals(session.SessionId, request.SessionId, StringComparison.Ordinal))
        {
            return new RepoRuntimeCommandResult(false, RepoRuntimeGatewayMode.Local, RepoRuntimeGatewayHealthState.Degraded, "Session mismatch prevented lease recovery.", session.SessionId, request.TaskId, session.Status.ToString());
        }

        var leaseRepository = new JsonWorkerLeaseRepository(paths, lockService);
        var taskGraphService = new Carves.Runtime.Application.TaskGraph.TaskGraphService(new JsonTaskGraphRepository(paths, lockService), new Carves.Runtime.Application.TaskGraph.TaskScheduler(), lockService);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths, lockService);
        var worktreeRuntimeService = new WorktreeRuntimeService(
            paths.RepoRoot,
            new GitClient(new ProcessRunner()),
            new JsonWorktreeRuntimeRepository(paths, lockService));
        var lifecycleService = new DelegatedWorkerLifecycleReconciliationService(
            paths,
            taskGraphService,
            sessionRepository,
            leaseRepository,
            worktreeRuntimeService,
            artifactRepository,
            new JsonDelegatedRunLifecycleRepository(paths, lockService),
            new JsonDelegatedRunRecoveryLedgerRepository(paths, lockService));

        if (!string.IsNullOrWhiteSpace(request.TaskId))
        {
            var lease = leaseRepository.Load()
                .Where(item => string.Equals(item.TaskId, request.TaskId, StringComparison.Ordinal))
                .OrderByDescending(item => item.AcquiredAt)
                .ThenByDescending(item => item.ExpiresAt)
                .FirstOrDefault();
            if (lease is not null)
            {
                lifecycleService.ReconcileLeaseRecovery(lease, request.Reason ?? "Lease recovery requested.", nameof(LocalRepoRuntimeGateway));
                session = sessionRepository.Load() ?? session;
            }
            else
            {
                var recoveryReason = request.Reason ?? "Lease expired and tracking was lost during local gateway recovery.";
                var graph = taskGraphService.Load();
                if (graph.Tasks.TryGetValue(request.TaskId, out var task))
                {
                    var decision = new WorkerRecoveryDecision
                    {
                        Action = WorkerRecoveryAction.EscalateToOperator,
                        ReasonCode = "delegated_run_tracking_lost",
                        Reason = $"Delegated worker lease expired and tracking was lost for {task.TaskId}. {recoveryReason}".Trim(),
                        Actionability = RuntimeActionability.HumanActionable,
                    };
                    task.SetStatus(DomainTaskStatus.Blocked);
                    task.SetPlannerReview(new PlannerReview
                    {
                        Verdict = PlannerVerdict.HumanDecisionRequired,
                        Reason = decision.Reason,
                        AcceptanceMet = false,
                        BoundaryPreserved = true,
                        ScopeDriftDetected = true,
                    });
                    task.RecordRecovery(decision);
                    taskGraphService.ReplaceTask(task);
                    session.ReleaseWorker(task.TaskId);
                    session.RecordRecoveryDecision(task.TaskId, decision);
                    session.MarkIdle(decision.Reason, RuntimeActionability.HumanActionable);
                }
                else
                {
                    session.ReleaseWorker(request.TaskId);
                    session.MarkIdle(recoveryReason, RuntimeActionability.HumanActionable);
                }

                sessionRepository.Save(session);
            }
        }
        else
        {
            session.MarkIdle(request.Reason ?? "Lease recovered through local gateway.");
            sessionRepository.Save(session);
        }

        Sync(paths, session);
        return new RepoRuntimeCommandResult(true, RepoRuntimeGatewayMode.Local, RepoRuntimeGatewayHealthState.Healthy, request.Reason ?? "Lease recovered.", session.SessionId, request.TaskId, session.Status.ToString());
    }
}
