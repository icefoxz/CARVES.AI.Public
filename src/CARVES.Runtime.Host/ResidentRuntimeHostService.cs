using System.Text;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Host;

public sealed class ResidentRuntimeHostService
{
    private readonly RuntimeServices services;

    public ResidentRuntimeHostService(RuntimeServices services)
    {
        this.services = services;
    }

    public Carves.Runtime.Application.ControlPlane.OperatorCommandResult Run(bool dryRun, int cycles, int intervalMilliseconds, int slots)
    {
        if (cycles < 0)
        {
            return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Failure("Usage: host [--dry-run] [--cycles <count>] [--interval-ms <milliseconds>] [--slots <count>]");
        }

        if (intervalMilliseconds < 0 || slots <= 0)
        {
            return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Failure("Usage: host [--dry-run] [--cycles <count>] [--interval-ms <milliseconds>] [--slots <count>]");
        }

        var lines = new List<string>();
        var managedRepoIds = new HashSet<string>(StringComparer.Ordinal);
        var reportedNonLiveActorSessionIds = new HashSet<string>(StringComparer.Ordinal);
        var exitReason = "Resident runtime host exited cleanly.";
        var stopRequested = false;

        var currentRepoId = EnsureCurrentRepoRegistered();
        managedRepoIds.Add(currentRepoId);

        ConsoleCancelEventHandler? cancelHandler = null;
        cancelHandler = (_, args) =>
        {
            args.Cancel = true;
            stopRequested = true;
            exitReason = "Resident runtime host received Ctrl+C and paused managed runtimes.";
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            lines.AddRange(BuildStartupSummary(dryRun, slots));

            var remainingCycles = cycles == 0 ? int.MaxValue : cycles;
            for (var index = 0; index < remainingCycles && !stopRequested; index++)
            {
                lines.AddRange(ReconcileActorSessionLiveness(index + 1, dryRun, reportedNonLiveActorSessionIds));
                var decision = services.PlatformSchedulerService.Plan(slots);
                lines.Add($"Host cycle {index + 1}: {decision.Reason}");

                foreach (var repoId in decision.SelectedRepoIds)
                {
                    managedRepoIds.Add(repoId);
                    lines.AddRange(RunRepoCycle(repoId, dryRun));
                }

                if (decision.SelectedRepoIds.Count == 0)
                {
                    lines.Add("No repo was advanced because the platform scheduler did not select a repo for this cycle.");
                    foreach (var candidate in decision.Candidates.Where(candidate => !candidate.Selected).Take(10))
                    {
                        lines.Add($"[{candidate.RepoId}] Scheduler blocked runtime advancement: {candidate.Reason}");
                    }
                }

                if (cycles != 0 && index + 1 >= cycles)
                {
                    exitReason = $"Resident runtime host reached cycle cap ({cycles}) and paused managed runtimes.";
                    break;
                }

                if (intervalMilliseconds > 0)
                {
                    BlockingHostWait.Delay(TimeSpan.FromMilliseconds(intervalMilliseconds));
                }
            }
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            foreach (var repoId in managedRepoIds)
            {
                PauseRepoOnExit(repoId, exitReason, lines);
            }
        }

        lines.Add(exitReason);
        return new Carves.Runtime.Application.ControlPlane.OperatorCommandResult(0, lines);
    }

    private IReadOnlyList<string> BuildStartupSummary(bool dryRun, int slots)
    {
        var platformStatus = services.OperatorApiService.GetPlatformStatus();
        var lines = new List<string>
        {
            "Runtime Host started",
            $"Repos: {platformStatus.RegisteredRepoCount}",
            $"Workers: {platformStatus.WorkerNodeCount}",
            $"Providers: {platformStatus.ProviderCount}",
            $"Slots per cycle: {slots}",
            $"Dry run: {dryRun}",
            "Session loop running...",
        };
        return lines;
    }

    private IReadOnlyList<string> ReconcileActorSessionLiveness(int cycleNumber, bool dryRun, ISet<string> reportedNonLiveActorSessionIds)
    {
        var sessions = services.OperatorApiService.GetActorSessions()
            .Where(item => item.State != ActorSessionState.Stopped)
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ThenBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();
        var liveness = ActorSessionLivenessRules.Classify(sessions);
        var nonLiveSessions = liveness.ClosedSessions
            .Concat(liveness.StaleSessions)
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ThenBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();
        var nonLiveSessionIds = nonLiveSessions
            .Select(item => item.ActorSessionId)
            .ToHashSet(StringComparer.Ordinal);
        var identityUnverifiedScheduleBoundWorkers = sessions
            .Where(item => item.Kind == ActorSessionKind.Worker)
            .Where(item => !string.IsNullOrWhiteSpace(item.ScheduleBinding))
            .Where(item => !nonLiveSessionIds.Contains(item.ActorSessionId))
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_identity_unverified")
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ThenBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();
        var heartbeatOnlyScheduleBoundWorkers = sessions
            .Where(item => item.Kind == ActorSessionKind.Worker)
            .Where(item => !string.IsNullOrWhiteSpace(item.ScheduleBinding))
            .Where(item => !nonLiveSessionIds.Contains(item.ActorSessionId))
            .Where(item => ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "heartbeat_only")
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ThenBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();

        if (nonLiveSessions.Length == 0
            && identityUnverifiedScheduleBoundWorkers.Length == 0
            && heartbeatOnlyScheduleBoundWorkers.Length == 0)
        {
            return Array.Empty<string>();
        }

        var staleIds = liveness.StaleSessions.Select(item => item.ActorSessionId).ToHashSet(StringComparer.Ordinal);
        var closedIds = liveness.ClosedSessions.Select(item => item.ActorSessionId).ToHashSet(StringComparer.Ordinal);
        var lines = new List<string>
        {
            $"Actor session liveness reconciliation: live={liveness.LiveSessions.Count} stale={liveness.StaleSessions.Count} closed={liveness.ClosedSessions.Count} non_live={nonLiveSessions.Length}; process_identity_unverified_schedule_bound={identityUnverifiedScheduleBoundWorkers.Length}; heartbeat_only_schedule_bound={heartbeatOnlyScheduleBoundWorkers.Length}; freshness_window_seconds={(int)liveness.FreshnessWindow.TotalSeconds}; checked_at={liveness.CheckedAt:O}",
        };

        foreach (var session in nonLiveSessions)
        {
            StopReconciledActorSession(
                session,
                cycleNumber,
                dryRun,
                reportedNonLiveActorSessionIds,
                lines,
                liveness.CheckedAt,
                ResolveActorSessionLivenessStatus(session, staleIds, closedIds),
                ActorSessionLivenessRules.ResolveNonLiveReason(session, liveness.CheckedAt),
                "Non-live actor session detected",
                "Host liveness reconciliation stopped a non-live actor session.",
                "actor_session_liveness_reconciled");
        }

        foreach (var session in identityUnverifiedScheduleBoundWorkers)
        {
            StopReconciledActorSession(
                session,
                cycleNumber,
                dryRun,
                reportedNonLiveActorSessionIds,
                lines,
                liveness.CheckedAt,
                "identity_unverified",
                "process_identity_unverified",
                "Schedule-bound actor session lacks process identity proof",
                "Host process identity reconciliation stopped an unverified schedule-bound actor session.",
                "actor_session_process_identity_reconciled");
        }

        foreach (var session in heartbeatOnlyScheduleBoundWorkers)
        {
            StopReconciledActorSession(
                session,
                cycleNumber,
                dryRun,
                reportedNonLiveActorSessionIds,
                lines,
                liveness.CheckedAt,
                "heartbeat_only",
                "heartbeat_only",
                "Schedule-bound actor session lacks process tracking proof",
                "Host process tracking reconciliation stopped a heartbeat-only schedule-bound actor session.",
                "actor_session_process_tracking_reconciled");
        }

        var cleanupSessionCount = nonLiveSessions.Length + identityUnverifiedScheduleBoundWorkers.Length + heartbeatOnlyScheduleBoundWorkers.Length;
        if (cleanupSessionCount > 10)
        {
            lines.Add($"Actor session liveness reconciliation truncated: rendered=10; hidden={cleanupSessionCount - 10}; use actor sessions --kind worker --repo-id <repo-id> to narrow cleanup.");
        }

        return lines;
    }

    private void StopReconciledActorSession(
        ActorSessionRecord session,
        int cycleNumber,
        bool dryRun,
        ISet<string> reportedActorSessionIds,
        List<string> lines,
        DateTimeOffset checkedAt,
        string status,
        string reason,
        string linePrefix,
        string stopReason,
        string reasonCode)
    {
        var stopCommand = BuildActorSessionStopCommand(session.ActorSessionId);
        var cleanupAction = dryRun
            ? "dry_run_stop_projected"
            : "stopped";
        if (lines.Count <= 10)
        {
            lines.Add($"[{session.RepoId}] {linePrefix}: {session.ActorSessionId} [{session.Kind}] liveness={status}; reason={reason}; cleanup_action={cleanupAction}; next_action={stopCommand}");
        }

        if (!reportedActorSessionIds.Add(session.ActorSessionId))
        {
            return;
        }

        var stopResult = services.ActorSessionService.Stop(
            session.ActorSessionId,
            dryRun,
            stopReason);
        var supervisorResult = services.ActorSessionService.MarkBoundWorkerSupervisorLost(
            session.ActorSessionId,
            dryRun,
            $"Host liveness reconciliation marked supervised worker lost because actor session {session.ActorSessionId} was {status}: {reason}.");
        if (supervisorResult is not null && lines.Count <= 10)
        {
            var supervisorAction = dryRun
                ? "dry_run_lost_projected"
                : "marked_lost";
            lines.Add($"[{session.RepoId}] Bound worker supervisor reconciled: worker_instance={supervisorResult.WorkerInstanceId}; actor_session={session.ActorSessionId}; state={supervisorResult.State}; cleanup_action={supervisorAction}");
        }
        if (dryRun)
        {
            return;
        }

        services.OperatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.ActorSessionLivenessReconciled,
            RepoId = session.RepoId,
            ActorSessionId = session.ActorSessionId,
            ActorKind = session.Kind,
            ActorIdentity = session.ActorIdentity,
            ReferenceId = session.ActorSessionId,
            ReasonCode = reasonCode,
            Summary = $"Host cycle {cycleNumber} detected {status} actor session {session.ActorSessionId} because {reason}. Cleanup action: {cleanupAction}; stopped={stopResult.Stopped}; supervisor_instance={supervisorResult?.WorkerInstanceId ?? "(none)"}; next command: {stopCommand}",
            OccurredAt = checkedAt,
        });
    }

    private static string ResolveActorSessionLivenessStatus(
        ActorSessionRecord session,
        IReadOnlySet<string> staleIds,
        IReadOnlySet<string> closedIds)
    {
        if (closedIds.Contains(session.ActorSessionId))
        {
            return "closed";
        }

        return staleIds.Contains(session.ActorSessionId)
            ? "stale"
            : "unknown";
    }

    private static string BuildActorSessionStopCommand(string actorSessionId)
    {
        return $"api actor-session-stop --actor-session-id {actorSessionId} --reason actor-thread-not-live";
    }

    private string EnsureCurrentRepoRegistered()
    {
        var existing = services.RepoRegistryService.List()
            .FirstOrDefault(descriptor => string.Equals(Path.GetFullPath(descriptor.RepoPath), services.Paths.RepoRoot, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing.RepoId;
        }

        var descriptor = services.RepoRegistryService.Register(services.Paths.RepoRoot, services.SystemConfig.RepoName, "default", "balanced");
        return descriptor.RepoId;
    }

    private IReadOnlyList<string> RunRepoCycle(string repoId, bool dryRun, string? preface = null)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(preface))
        {
            lines.Add(preface);
        }

        var descriptor = services.RepoRegistryService.Inspect(repoId);
        var instance = services.RuntimeInstanceManager.Inspect(repoId);
        if (instance.Status == RuntimeInstanceStatus.Paused)
        {
            lines.Add($"[{repoId}] Skipped paused runtime instance; explicit resume is required before host scheduling can advance it.");
            return lines;
        }
        else if (instance.Status == RuntimeInstanceStatus.Stopped)
        {
            lines.Add($"[{repoId}] Skipped stopped runtime instance; explicit runtime start is required before host scheduling can advance it.");
            return lines;
        }
        else if (instance.ActiveSessionId is null)
        {
            services.RuntimeInstanceManager.Start(repoId, dryRun);
            lines.Add($"[{repoId}] Started runtime instance.");
        }

        var repoServices = RuntimeComposition.Create(descriptor.RepoPath);
        var loop = repoServices.DevLoopService.RunContinuousLoop(dryRun, 1);
        lines.Add($"[{repoId}] {loop.Message}");
        UpdateRuntimeInstanceStatus(repoId, loop.Session);
        return lines;
    }

    private void UpdateRuntimeInstanceStatus(string repoId, RuntimeSessionState? session)
    {
        var instance = services.RuntimeInstanceManager.Inspect(repoId);
        if (session is null)
        {
            return;
        }

        instance.ActiveSessionId = session.SessionId;
        instance.Status = session.Status switch
        {
            RuntimeSessionStatus.Paused => RuntimeInstanceStatus.Paused,
            RuntimeSessionStatus.Stopped or RuntimeSessionStatus.Failed => RuntimeInstanceStatus.Stopped,
            _ => RuntimeInstanceStatus.Running,
        };
        services.RuntimeInstanceManager.Update(instance);
    }

    private void PauseRepoOnExit(string repoId, string exitReason, ICollection<string> lines)
    {
        try
        {
            var descriptor = services.RepoRegistryService.Inspect(repoId);
            var repoServices = RuntimeComposition.Create(descriptor.RepoPath);
            var session = repoServices.DevLoopService.GetSession();
            if (session is null || session.Status is RuntimeSessionStatus.Paused or RuntimeSessionStatus.Stopped or RuntimeSessionStatus.Failed)
            {
                return;
            }

            services.RuntimeInstanceManager.Pause(repoId, exitReason);
            lines.Add($"[{repoId}] Paused runtime instance for safe host exit.");
        }
        catch
        {
        }
    }
}
