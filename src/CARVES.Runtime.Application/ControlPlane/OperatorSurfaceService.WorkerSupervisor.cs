using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private static readonly OperatorOsEventKind[] WorkerSupervisorEventKinds =
    [
        OperatorOsEventKind.WorkerSupervisorLaunchRequested,
        OperatorOsEventKind.WorkerSupervisorRunning,
        OperatorOsEventKind.WorkerSupervisorLost,
        OperatorOsEventKind.WorkerSupervisorArchived,
        OperatorOsEventKind.WorkerSupervisorStopped,
        OperatorOsEventKind.WorkerSupervisorHostProcessHandleAcquired,
    ];

    public OperatorCommandResult WorkerSupervisorLaunch(
        string? repoId,
        string? workerIdentity,
        string? workerInstanceId,
        string? actorSessionId,
        string? providerProfile,
        string? capabilityProfile,
        string? scheduleBinding,
        bool dryRun,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(workerIdentity))
        {
            return OperatorCommandResult.Failure("Usage: worker supervisor-launch --identity <id> [--repo-id <id>] [--worker-instance-id <id>] [--actor-session-id <id>] [--provider-profile <id>] [--capability-profile <id>] [--schedule-binding <id>] [--reason <text>] [--dry-run]");
        }

        WorkerSupervisorLaunchRegistrationResult result;
        try
        {
            result = RegisterWorkerSupervisorLaunchCore(
                repoId,
                workerIdentity,
                workerInstanceId,
                actorSessionId,
                providerProfile,
                capabilityProfile,
                scheduleBinding,
                dryRun,
                reason);
        }
        catch (InvalidOperationException exception)
        {
            return OperatorCommandResult.Failure(exception.Message);
        }

        var lines = new List<string>
        {
            dryRun ? "Worker supervisor launch dry run" : "Worker supervisor launch registered",
            $"Worker instance: {result.WorkerInstanceId}",
            $"Repo: {result.RepoId}",
            $"Worker identity: {result.WorkerIdentity}",
            $"Actor session: {result.ActorSessionId ?? "(default stable id)"}",
            $"Launch token id: {result.LaunchTokenId ?? "(none)"}",
            $"Launch token: {result.LaunchToken ?? "(not issued in dry run)"}",
            $"Launch token visibility: {result.LaunchTokenVisibility}",
            $"Launch token expires at: {result.LaunchTokenExpiresAt?.ToString("O") ?? "(none)"}",
            $"Provider profile: {result.ProviderProfile ?? "(none)"}",
            $"Capability profile: {result.CapabilityProfile ?? "(none)"}",
            $"Schedule binding: {result.ScheduleBinding ?? "(none)"}",
            $"Supervisor capability mode: {result.SupervisorCapabilityMode}",
            $"Process ownership status: {result.ProcessOwnershipStatus}",
            $"Host process spawned: {result.HostProcessSpawned}",
            $"Host process handle held: {result.HostProcessHandleHeld}",
            $"Immediate death detection ready: {result.ImmediateDeathDetectionReady}",
            $"Process proof source: {result.ProcessProofSource}",
            $"Registration command: {result.TextRegistrationCommand}",
            $"API registration command: {result.ApiRegistrationCommand}",
            $"Starts run: {result.StartsRun}",
            $"Issues lease: {result.IssuesLease}",
            $"Grants execution authority: {result.GrantsExecutionAuthority}",
            $"Grants truth write authority: {result.GrantsTruthWriteAuthority}",
            $"Reason: {result.Reason}",
        };

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult WorkerSupervisorInstances(string? repoId = null)
    {
        var surface = BuildWorkerSupervisorInstancesSurface(repoId);
        var lines = new List<string>
        {
            $"Worker supervisor instances: total={surface.TotalCount}; repo={surface.RepoFilter ?? "(any)"}; archived={surface.ArchivedCount}; checked_at={surface.ProjectedAt:O}",
            $"Supervisor readiness: posture={surface.SupervisorReadinessPosture}; host_process_supervision_ready={surface.HostProcessSupervisionReady}; immediate_death_detection_ready={surface.ImmediateDeathDetectionReady}; gap_count={surface.SupervisorAutomationGapCount}",
        };
        if (surface.SupervisorAutomationGaps.Count > 0)
        {
            lines.Add($"Supervisor automation gaps: {string.Join(", ", surface.SupervisorAutomationGaps)}");
        }

        if (surface.Entries.Count == 0)
        {
            lines.Add("(no active supervisor instances)");
        }
        else
        {
            foreach (var entry in surface.Entries)
            {
                lines.Add($"- {entry.WorkerInstanceId} [{entry.State}] identity={entry.WorkerIdentity} repo={entry.RepoId}");
                lines.Add($"  actor_session: {entry.ActorSessionId ?? "(none)"}");
                lines.Add($"  host_session: {entry.HostSessionId ?? "(none)"}");
                lines.Add($"  process: id={entry.ProcessId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}; started_at={entry.ProcessStartedAt?.ToString("O") ?? "(none)"}");
                lines.Add($"  host_process_handle: id={entry.HostProcessHandleId ?? "(none)"}; owner_session={entry.HostProcessHandleOwnerSessionId ?? "(none)"}; acquired_at={entry.HostProcessHandleAcquiredAt?.ToString("O") ?? "(none)"}");
                lines.Add($"  launch_token: id={entry.LaunchTokenId}; expires_at={entry.LaunchTokenExpiresAt:O}; expired={entry.LaunchTokenExpired}; raw_token_projected={entry.RawLaunchTokenProjected}");
                lines.Add($"  binding: provider={entry.ProviderProfile ?? "(none)"}; capability={entry.CapabilityProfile ?? "(none)"}; schedule={entry.ScheduleBinding ?? "(none)"}");
                lines.Add($"  supervisor_mode: capability={entry.SupervisorCapabilityMode}; process_ownership={entry.ProcessOwnershipStatus}; host_process_spawned={entry.HostProcessSpawned}; host_handle_held={entry.HostProcessHandleHeld}; immediate_death_detection={entry.ImmediateDeathDetectionReady}");
                lines.Add($"  process_proof: source={entry.ProcessProofSource}; stdio_captured={entry.StdIoCaptured}; stdout={entry.HostProcessStdoutLogPath ?? "(none)"}; stderr={entry.HostProcessStderrLogPath ?? "(none)"}; worker_registration_required={entry.RequiresWorkerProcessRegistration}");
                lines.Add($"  automation_gaps: count={entry.SupervisorAutomationGapCount}; gaps={(entry.SupervisorAutomationGaps.Count == 0 ? "(none)" : string.Join(", ", entry.SupervisorAutomationGaps))}");
                lines.Add($"  lifecycle: registration_allowed={entry.RegistrationAllowed}; recovery_required={entry.RecoveryRequired}; status={entry.LifecycleStatus}");
                lines.Add($"  next_action: {entry.NextAction}");
                lines.Add($"  next_command: {entry.NextCommand}");
                lines.Add($"  lifecycle_events: count={entry.LifecycleEventCount}; last={entry.LastLifecycleEventKind?.ToString() ?? "(none)"}; reason={entry.LastLifecycleReasonCode ?? "(none)"}; at={entry.LastLifecycleEventAt?.ToString("O") ?? "(none)"}");
                lines.Add($"  lifecycle_events_command: {entry.LifecycleEventsCommand}");
                lines.Add($"  last_reason: {entry.LastReason}");
            }
        }

        if (surface.ArchivedEntries.Count > 0)
        {
            lines.Add($"Archived supervisor tombstones: count={surface.ArchivedCount}");
            foreach (var entry in surface.ArchivedEntries)
            {
                lines.Add($"- archive {entry.ArchiveId}: worker_instance={entry.WorkerInstanceId}; previous_state={entry.PreviousState}; repo={entry.RepoId}; archived_at={entry.ArchivedAt:O}");
                lines.Add($"  actor_session={entry.ActorSessionId ?? "(none)"}; schedule={entry.ScheduleBinding ?? "(none)"}; reason={entry.Reason}");
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult WorkerSupervisorEvents(
        string? repoId = null,
        string? workerInstanceId = null,
        string? actorSessionId = null)
    {
        var surface = BuildWorkerSupervisorEventsSurface(repoId, workerInstanceId, actorSessionId);
        var lines = new List<string>
        {
            $"Worker supervisor events: total={surface.EventCount}; rendered={surface.DisplayedEventCount}; hidden={surface.EventCount - surface.DisplayedEventCount}; repo={surface.RepoFilter ?? "(any)"}; worker_instance={surface.WorkerInstanceFilter ?? "(any)"}; actor_session={surface.ActorSessionFilter ?? "(any)"}; checked_at={surface.ProjectedAt:O}",
            $"Included event kinds: {string.Join(", ", surface.IncludedEventKinds)}",
        };

        if (surface.Truncated)
        {
            lines.Add($"Worker supervisor event output truncated: rendered={surface.DisplayedEventCount}; hidden={surface.EventCount - surface.DisplayedEventCount}");
        }

        if (surface.Events.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var item in surface.Events)
        {
            lines.Add($"- {item.EventKind} [{item.OccurredAt:O}] worker_instance={item.WorkerInstanceId ?? "(none)"} repo={item.RepoId ?? "(none)"} actor_session={item.ActorSessionId ?? "(none)"}");
            lines.Add($"  actor: {item.ActorKind?.ToString() ?? "(none)"}:{item.ActorIdentity ?? "(none)"}");
            lines.Add($"  reason: {item.ReasonCode ?? "(none)"}");
            lines.Add($"  summary: {item.Summary}");
            if (!string.IsNullOrWhiteSpace(item.DetailRef))
            {
                lines.Add($"  detail_ref: {item.DetailRef}");
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult WorkerSupervisorArchive(
        string? workerInstanceId,
        bool dryRun,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(workerInstanceId))
        {
            return OperatorCommandResult.Failure("Usage: worker supervisor-archive --worker-instance-id <id> [--reason <text>] [--dry-run]");
        }

        WorkerSupervisorArchiveResult result;
        try
        {
            result = ArchiveWorkerSupervisorCore(workerInstanceId, dryRun, reason);
        }
        catch (InvalidOperationException exception)
        {
            return OperatorCommandResult.Failure(exception.Message);
        }

        var lines = new List<string>
        {
            dryRun ? "Worker supervisor archive dry run" : "Worker supervisor archived",
            $"Archived: {result.Archived}",
            $"Worker instance: {result.WorkerInstanceId}",
            $"Repo: {result.RepoId}",
            $"Worker identity: {result.WorkerIdentity}",
            $"Previous state: {result.PreviousState}",
            $"Actor session: {result.ActorSessionId ?? "(none)"}",
            $"Schedule binding: {result.ScheduleBinding ?? "(none)"}",
            $"Archive tombstone: id={result.ArchiveId ?? "(none)"}; projected={result.TombstoneProjected}",
            $"Next action: {result.NextAction}",
            $"Next command: {result.NextCommand}",
            $"Starts run: {result.StartsRun}",
            $"Issues lease: {result.IssuesLease}",
            $"Grants execution authority: {result.GrantsExecutionAuthority}",
            $"Grants truth write authority: {result.GrantsTruthWriteAuthority}",
            $"Reason: {result.Reason}",
        };

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult ApiWorkerSupervisorLaunch(
        string? repoId,
        string? workerIdentity,
        string? workerInstanceId,
        string? actorSessionId,
        string? providerProfile,
        string? capabilityProfile,
        string? scheduleBinding,
        bool dryRun,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(workerIdentity))
        {
            return OperatorCommandResult.Failure("Usage: api worker-supervisor-launch --identity <id> [--repo-id <id>] [--worker-instance-id <id>] [--actor-session-id <id>] [--provider-profile <id>] [--capability-profile <id>] [--schedule-binding <id>] [--reason <text>] [--dry-run]");
        }

        try
        {
            return OperatorCommandResult.Success(operatorApiService.ToJson(RegisterWorkerSupervisorLaunchCore(
                repoId,
                workerIdentity,
                workerInstanceId,
                actorSessionId,
                providerProfile,
                capabilityProfile,
                scheduleBinding,
                dryRun,
                reason)));
        }
        catch (InvalidOperationException exception)
        {
            return OperatorCommandResult.Failure(exception.Message);
        }
    }

    public OperatorCommandResult ApiWorkerSupervisorInstances(string? repoId = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(BuildWorkerSupervisorInstancesSurface(repoId)));
    }

    public OperatorCommandResult ApiWorkerSupervisorEvents(
        string? repoId = null,
        string? workerInstanceId = null,
        string? actorSessionId = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(BuildWorkerSupervisorEventsSurface(repoId, workerInstanceId, actorSessionId)));
    }

    public OperatorCommandResult ApiWorkerSupervisorArchive(
        string? workerInstanceId,
        bool dryRun,
        string? reason)
    {
        if (string.IsNullOrWhiteSpace(workerInstanceId))
        {
            return OperatorCommandResult.Failure("Usage: api worker-supervisor-archive --worker-instance-id <id> [--reason <text>] [--dry-run]");
        }

        try
        {
            return OperatorCommandResult.Success(operatorApiService.ToJson(ArchiveWorkerSupervisorCore(workerInstanceId, dryRun, reason)));
        }
        catch (InvalidOperationException exception)
        {
            return OperatorCommandResult.Failure(exception.Message);
        }
    }

    private WorkerSupervisorLaunchRegistrationResult RegisterWorkerSupervisorLaunchCore(
        string? repoId,
        string workerIdentity,
        string? workerInstanceId,
        string? actorSessionId,
        string? providerProfile,
        string? capabilityProfile,
        string? scheduleBinding,
        bool dryRun,
        string? reason)
    {
        var resolvedRepoId = string.IsNullOrWhiteSpace(repoId)
            ? repoRegistryService.List().FirstOrDefault()?.RepoId ?? Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : repoId.Trim();
        var resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? "Registered a Host-owned supervised worker launch request."
            : reason.Trim();

        return actorSessionService.RegisterWorkerSupervisorLaunch(
            resolvedRepoId,
            workerIdentity.Trim(),
            resolvedReason,
            dryRun,
            string.IsNullOrWhiteSpace(workerInstanceId) ? null : workerInstanceId.Trim(),
            devLoopService.GetSession()?.SessionId,
            string.IsNullOrWhiteSpace(actorSessionId) ? null : actorSessionId.Trim(),
            string.IsNullOrWhiteSpace(providerProfile) ? null : providerProfile.Trim(),
            string.IsNullOrWhiteSpace(capabilityProfile) ? null : capabilityProfile.Trim(),
            string.IsNullOrWhiteSpace(scheduleBinding) ? null : scheduleBinding.Trim());
    }

    private WorkerSupervisorArchiveResult ArchiveWorkerSupervisorCore(
        string workerInstanceId,
        bool dryRun,
        string? reason)
    {
        var resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? "Archived worker supervisor live-state entry by operator request."
            : reason.Trim();
        return actorSessionService.ArchiveWorkerSupervisorInstance(
            workerInstanceId.Trim(),
            dryRun,
            resolvedReason);
    }

    private WorkerSupervisorInstancesSurface BuildWorkerSupervisorInstancesSurface(string? repoId)
    {
        var resolvedRepoId = string.IsNullOrWhiteSpace(repoId) ? null : repoId.Trim();
        var supervisorEvents = QueryWorkerSupervisorEvents(resolvedRepoId, workerInstanceId: null, actorSessionId: null);
        var entries = actorSessionService.ListWorkerSupervisorInstances(resolvedRepoId)
            .Select(record => ToWorkerSupervisorInstanceSurface(
                record,
                supervisorEvents
                    .Where(item => string.Equals(item.ReferenceId, record.WorkerInstanceId, StringComparison.Ordinal))
                    .ToArray()))
            .ToArray();
        var archivedEntries = actorSessionService.ListWorkerSupervisorArchivedInstances(resolvedRepoId)
            .Select(ToWorkerSupervisorArchivedInstanceSurface)
            .ToArray();
        var automationGaps = ResolveSupervisorAutomationGaps(entries);
        var hostProcessSupervisionReady = entries.Length > 0 && automationGaps.Length == 0;
        var immediateDeathDetectionReady = entries.Length > 0 && entries.All(item => item.ImmediateDeathDetectionReady);
        return new WorkerSupervisorInstancesSurface(
            "worker-supervisor-instances.v1",
            resolvedRepoId,
            entries.Length,
            archivedEntries.Length,
            ResolveSupervisorReadinessPosture(entries, automationGaps),
            hostProcessSupervisionReady,
            immediateDeathDetectionReady,
            automationGaps.Length,
            automationGaps,
            entries,
            archivedEntries,
            DateTimeOffset.UtcNow);
    }

    private WorkerSupervisorEventsSurface BuildWorkerSupervisorEventsSurface(
        string? repoId,
        string? workerInstanceId,
        string? actorSessionId)
    {
        const int maxDisplayedEvents = 100;
        var resolvedRepoId = string.IsNullOrWhiteSpace(repoId) ? null : repoId.Trim();
        var resolvedWorkerInstanceId = string.IsNullOrWhiteSpace(workerInstanceId) ? null : workerInstanceId.Trim();
        var resolvedActorSessionId = string.IsNullOrWhiteSpace(actorSessionId) ? null : actorSessionId.Trim();
        var matchingEvents = QueryWorkerSupervisorEvents(resolvedRepoId, resolvedWorkerInstanceId, resolvedActorSessionId);
        var displayedEvents = matchingEvents
            .Take(maxDisplayedEvents)
            .Select(ToWorkerSupervisorEventSurface)
            .ToArray();

        return new WorkerSupervisorEventsSurface(
            "worker-supervisor-events.v1",
            resolvedRepoId,
            resolvedWorkerInstanceId,
            resolvedActorSessionId,
            matchingEvents.Length,
            displayedEvents.Length,
            matchingEvents.Length > displayedEvents.Length,
            maxDisplayedEvents,
            "operator_os_events_filtered_by_worker_supervisor_lifecycle",
            WorkerSupervisorEventKinds,
            displayedEvents,
            DateTimeOffset.UtcNow);
    }

    private OperatorOsEventRecord[] QueryWorkerSupervisorEvents(
        string? repoId,
        string? workerInstanceId,
        string? actorSessionId)
    {
        return operatorApiService.GetOperatorOsEvents(actorSessionId: actorSessionId)
            .Where(item => WorkerSupervisorEventKinds.Contains(item.EventKind))
            .Where(item => string.IsNullOrWhiteSpace(repoId)
                           || string.Equals(item.RepoId, repoId, StringComparison.Ordinal))
            .Where(item => string.IsNullOrWhiteSpace(workerInstanceId)
                           || string.Equals(item.ReferenceId, workerInstanceId, StringComparison.Ordinal))
            .OrderByDescending(item => item.OccurredAt)
            .ToArray();
    }

    private static WorkerSupervisorInstanceSurface ToWorkerSupervisorInstanceSurface(
        WorkerSupervisorInstanceRecord record,
        IReadOnlyList<OperatorOsEventRecord> lifecycleEvents)
    {
        var hostProcessHandleHeld = HasSupervisorHostProcessHandleProof(record);
        var stdIoCaptured = !string.IsNullOrWhiteSpace(record.HostProcessStdoutLogPath)
                            && !string.IsNullOrWhiteSpace(record.HostProcessStderrLogPath);
        var automationGaps = ResolveSupervisorAutomationGaps(
            hostProcessSpawned: hostProcessHandleHeld,
            hostProcessHandleHeld: hostProcessHandleHeld,
            stdIoCaptured: stdIoCaptured,
            immediateDeathDetectionReady: hostProcessHandleHeld);
        var launchTokenExpired = IsSupervisorLaunchTokenExpired(record);

        return new WorkerSupervisorInstanceSurface(
            record.WorkerInstanceId,
            record.RepoId,
            record.WorkerIdentity,
            record.OwnershipMode,
            record.State,
            record.HostSessionId,
            record.ActorSessionId,
            record.ProviderProfile,
            record.CapabilityProfile,
            record.ScheduleBinding,
            record.ProcessId,
            record.ProcessStartedAt,
            record.HostProcessHandleId,
            record.HostProcessHandleOwnerSessionId,
            record.HostProcessHandleAcquiredAt,
            record.HostProcessStdoutLogPath,
            record.HostProcessStderrLogPath,
            record.LaunchTokenId,
            record.LaunchTokenIssuedAt,
            record.LaunchTokenExpiresAt,
            !string.IsNullOrWhiteSpace(record.LaunchTokenHash),
            launchTokenExpired,
            false,
            "raw_token_never_projected_from_state",
            IsSupervisorRegistrationAllowed(record.State, launchTokenExpired),
            IsSupervisorRecoveryRequired(record.State, launchTokenExpired),
            ResolveSupervisorLifecycleStatus(record.State, launchTokenExpired),
            ResolveSupervisorNextAction(record, launchTokenExpired),
            ResolveSupervisorNextCommand(record, launchTokenExpired),
            BuildWorkerSupervisorEventsCommand(record),
            lifecycleEvents.Count,
            lifecycleEvents.FirstOrDefault()?.EventKind,
            lifecycleEvents.FirstOrDefault()?.ReasonCode,
            lifecycleEvents.FirstOrDefault()?.OccurredAt,
            hostProcessHandleHeld ? "host_owned_process_handle" : "registration_only_launch_token",
            ResolveSupervisorProcessOwnershipStatus(record),
            hostProcessHandleHeld,
            hostProcessHandleHeld,
            stdIoCaptured,
            hostProcessHandleHeld,
            IsSupervisorRegistrationAllowed(record.State, launchTokenExpired),
            ResolveSupervisorProcessProofSource(record),
            automationGaps.Length,
            automationGaps,
            record.LastHeartbeatAt,
            record.LastReason,
            record.CreatedAt,
            record.UpdatedAt);
    }

    private static WorkerSupervisorArchivedInstanceSurface ToWorkerSupervisorArchivedInstanceSurface(WorkerSupervisorArchivedInstanceRecord record)
    {
        return new WorkerSupervisorArchivedInstanceSurface(
            record.ArchiveId,
            record.WorkerInstanceId,
            record.RepoId,
            record.WorkerIdentity,
            record.PreviousState,
            record.ActorSessionId,
            record.ScheduleBinding,
            record.Reason,
            record.ArchivedAt,
            record.OriginalCreatedAt,
            record.OriginalUpdatedAt);
    }

    private static WorkerSupervisorEventSurface ToWorkerSupervisorEventSurface(OperatorOsEventRecord record)
    {
        return new WorkerSupervisorEventSurface(
            record.EventKind,
            record.RepoId,
            record.ActorSessionId,
            record.ActorKind,
            record.ActorIdentity,
            record.ReferenceId,
            record.ReasonCode,
            record.Summary,
            record.DetailRef,
            record.OccurredAt);
    }

    private static bool IsSupervisorLaunchTokenExpired(WorkerSupervisorInstanceRecord record)
    {
        return record.State is (WorkerSupervisorInstanceState.Requested or WorkerSupervisorInstanceState.Starting)
            && DateTimeOffset.UtcNow > record.LaunchTokenExpiresAt;
    }

    private static bool IsSupervisorRegistrationAllowed(
        WorkerSupervisorInstanceState state,
        bool launchTokenExpired)
    {
        return !launchTokenExpired
            && state is (WorkerSupervisorInstanceState.Requested
                or WorkerSupervisorInstanceState.Starting);
    }

    private static bool IsSupervisorRecoveryRequired(
        WorkerSupervisorInstanceState state,
        bool launchTokenExpired)
    {
        return launchTokenExpired
            || state is (WorkerSupervisorInstanceState.Lost
                or WorkerSupervisorInstanceState.Failed
                or WorkerSupervisorInstanceState.Stopped);
    }

    private static string ResolveSupervisorLifecycleStatus(
        WorkerSupervisorInstanceState state,
        bool launchTokenExpired)
    {
        if (launchTokenExpired)
        {
            return "launch_token_expired_relaunch_required";
        }

        return state switch
        {
            WorkerSupervisorInstanceState.Requested => "pending_supervised_actor_registration",
            WorkerSupervisorInstanceState.Starting => "worker_process_starting",
            WorkerSupervisorInstanceState.Running => "supervised_worker_running",
            WorkerSupervisorInstanceState.Stopping => "worker_process_stopping",
            WorkerSupervisorInstanceState.Stopped => "supervised_worker_stopped_relaunch_required",
            WorkerSupervisorInstanceState.Lost => "supervised_worker_lost_relaunch_required",
            WorkerSupervisorInstanceState.Failed => "supervised_worker_failed_relaunch_required",
            _ => "unknown",
        };
    }

    private static string ResolveSupervisorNextAction(
        WorkerSupervisorInstanceRecord record,
        bool launchTokenExpired)
    {
        if (launchTokenExpired)
        {
            return "Do not use this expired launch token. Issue a new worker supervisor launch before registering a replacement worker process.";
        }

        return record.State switch
        {
            WorkerSupervisorInstanceState.Requested or WorkerSupervisorInstanceState.Starting =>
                "Complete supervised actor registration from the live worker process using the one-time launch token that was returned when the launch was created.",
            WorkerSupervisorInstanceState.Running =>
                "Keep the worker process alive and refresh the bound actor session heartbeat/context receipt. Do not reuse the launch token after registration.",
            WorkerSupervisorInstanceState.Lost or WorkerSupervisorInstanceState.Failed or WorkerSupervisorInstanceState.Stopped =>
                "Do not reuse this supervisor instance or its old launch token. Issue a new worker supervisor launch and register the replacement worker process.",
            WorkerSupervisorInstanceState.Stopping =>
                "Wait for shutdown to finish, then issue a new worker supervisor launch if this worker should be replaced.",
            _ => "Inspect worker supervisor state before scheduling.",
        };
    }

    private static string ResolveSupervisorNextCommand(
        WorkerSupervisorInstanceRecord record,
        bool launchTokenExpired)
    {
        if (launchTokenExpired)
        {
            return $"api worker-supervisor-launch --repo-id {FormatCommandArgument(record.RepoId)} --identity {FormatCommandArgument(record.WorkerIdentity)} --worker-instance-id <new-worker-instance-id>";
        }

        return record.State switch
        {
            WorkerSupervisorInstanceState.Requested or WorkerSupervisorInstanceState.Starting =>
                $"api actor-session-register --kind worker --identity {FormatCommandArgument(record.WorkerIdentity)} --repo-id {FormatCommandArgument(record.RepoId)} --registration-mode supervised --worker-instance-id {FormatCommandArgument(record.WorkerInstanceId)} --launch-token <returned-launch-token> --process-id <pid> --context-receipt <context-receipt>",
            WorkerSupervisorInstanceState.Running =>
                string.IsNullOrWhiteSpace(record.ActorSessionId)
                    ? "api actor-sessions --kind worker"
                    : $"api actor-session-heartbeat --actor-session-id {FormatCommandArgument(record.ActorSessionId)} --context-receipt <context-receipt> --health healthy",
            WorkerSupervisorInstanceState.Lost or WorkerSupervisorInstanceState.Failed or WorkerSupervisorInstanceState.Stopped =>
                $"api worker-supervisor-launch --repo-id {FormatCommandArgument(record.RepoId)} --identity {FormatCommandArgument(record.WorkerIdentity)} --worker-instance-id <new-worker-instance-id>",
            WorkerSupervisorInstanceState.Stopping =>
                $"api worker-supervisor-instances --repo-id {FormatCommandArgument(record.RepoId)}",
            _ => "api worker-supervisor-instances",
        };
    }

    private static string BuildWorkerSupervisorEventsCommand(WorkerSupervisorInstanceRecord record)
    {
        return $"api worker-supervisor-events --repo-id {FormatCommandArgument(record.RepoId)} --worker-instance-id {FormatCommandArgument(record.WorkerInstanceId)}";
    }

    private static string ResolveSupervisorProcessOwnershipStatus(WorkerSupervisorInstanceRecord record)
    {
        if (HasSupervisorHostProcessHandleProof(record))
        {
            return "host_owned_process_handle_ready";
        }

        return record.State switch
        {
            WorkerSupervisorInstanceState.Requested or WorkerSupervisorInstanceState.Starting =>
                "host_registered_launch_token_process_not_spawned",
            WorkerSupervisorInstanceState.Running when record.ProcessId.HasValue && record.ProcessStartedAt.HasValue =>
                "worker_process_registered_with_process_identity_proof_no_host_handle",
            WorkerSupervisorInstanceState.Running =>
                "worker_process_registered_without_complete_process_identity_proof",
            WorkerSupervisorInstanceState.Lost =>
                "worker_process_lost_no_host_handle",
            WorkerSupervisorInstanceState.Failed =>
                "worker_process_failed_no_host_handle",
            WorkerSupervisorInstanceState.Stopped =>
                "worker_process_stopped_no_host_handle",
            WorkerSupervisorInstanceState.Stopping =>
                "worker_process_stopping_no_host_handle",
            _ => "unknown",
        };
    }

    private static string ResolveSupervisorProcessProofSource(WorkerSupervisorInstanceRecord record)
    {
        if (HasSupervisorHostProcessHandleProof(record))
        {
            return "host_process_handle_plus_actor_session_process_id_and_start_time";
        }

        return record.ProcessId.HasValue && record.ProcessStartedAt.HasValue
            ? "actor_session_process_id_and_start_time"
            : "none_until_supervised_actor_registration";
    }

    private static string ResolveSupervisorReadinessPosture(
        IReadOnlyList<WorkerSupervisorInstanceSurface> entries,
        IReadOnlyList<string> automationGaps)
    {
        if (entries.Count == 0)
        {
            return "no_supervisor_instances";
        }

        return automationGaps.Count == 0
            ? "host_process_supervision_ready"
            : "registration_ready_process_supervision_not_ready";
    }

    private static string[] ResolveSupervisorAutomationGaps(IReadOnlyList<WorkerSupervisorInstanceSurface> entries)
    {
        if (entries.Count == 0)
        {
            return ["no_supervisor_instances_registered"];
        }

        return entries
            .SelectMany(item => item.SupervisorAutomationGaps)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ResolveSupervisorAutomationGaps(
        bool hostProcessSpawned,
        bool hostProcessHandleHeld,
        bool stdIoCaptured,
        bool immediateDeathDetectionReady)
    {
        var gaps = new List<string>();
        if (!hostProcessSpawned)
        {
            gaps.Add("host_process_spawn_not_implemented");
        }

        if (!hostProcessHandleHeld)
        {
            gaps.Add("host_process_handle_not_held");
        }

        if (!stdIoCaptured)
        {
            gaps.Add("stdio_capture_not_available");
        }

        if (!immediateDeathDetectionReady)
        {
            gaps.Add("immediate_death_detection_not_available");
        }

        return gaps.ToArray();
    }

    private static bool HasSupervisorHostProcessHandleProof(WorkerSupervisorInstanceRecord record)
    {
        return record.OwnershipMode is WorkerSupervisorOwnershipMode.HostOwned
               && record.State is WorkerSupervisorInstanceState.Running
               && record.ProcessId.HasValue
               && record.ProcessStartedAt.HasValue
               && !string.IsNullOrWhiteSpace(record.ActorSessionId)
               && !string.IsNullOrWhiteSpace(record.HostProcessHandleId)
               && !string.IsNullOrWhiteSpace(record.HostProcessHandleOwnerSessionId)
               && record.HostProcessHandleAcquiredAt is not null
               && (string.IsNullOrWhiteSpace(record.HostSessionId)
                   || string.Equals(record.HostSessionId, record.HostProcessHandleOwnerSessionId, StringComparison.Ordinal));
    }

    private static string FormatCommandArgument(string value)
    {
        if (value.Length > 0 && value.All(IsSimpleCommandArgumentCharacter))
        {
            return value;
        }

        return $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }

    private static bool IsSimpleCommandArgumentCharacter(char character)
    {
        return char.IsLetterOrDigit(character)
               || character is '-' or '_' or '.' or ':' or '/' or '=' or '<' or '>';
    }

    private sealed record WorkerSupervisorInstancesSurface(
        string SchemaVersion,
        string? RepoFilter,
        int TotalCount,
        int ArchivedCount,
        string SupervisorReadinessPosture,
        bool HostProcessSupervisionReady,
        bool ImmediateDeathDetectionReady,
        int SupervisorAutomationGapCount,
        IReadOnlyList<string> SupervisorAutomationGaps,
        IReadOnlyList<WorkerSupervisorInstanceSurface> Entries,
        IReadOnlyList<WorkerSupervisorArchivedInstanceSurface> ArchivedEntries,
        DateTimeOffset ProjectedAt);

    private sealed record WorkerSupervisorInstanceSurface(
        string WorkerInstanceId,
        string RepoId,
        string WorkerIdentity,
        WorkerSupervisorOwnershipMode OwnershipMode,
        WorkerSupervisorInstanceState State,
        string? HostSessionId,
        string? ActorSessionId,
        string? ProviderProfile,
        string? CapabilityProfile,
        string? ScheduleBinding,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAt,
        string? HostProcessHandleId,
        string? HostProcessHandleOwnerSessionId,
        DateTimeOffset? HostProcessHandleAcquiredAt,
        string? HostProcessStdoutLogPath,
        string? HostProcessStderrLogPath,
        string LaunchTokenId,
        DateTimeOffset LaunchTokenIssuedAt,
        DateTimeOffset LaunchTokenExpiresAt,
        bool LaunchTokenHashPresent,
        bool LaunchTokenExpired,
        bool RawLaunchTokenProjected,
        string LaunchTokenVisibility,
        bool RegistrationAllowed,
        bool RecoveryRequired,
        string LifecycleStatus,
        string NextAction,
        string NextCommand,
        string LifecycleEventsCommand,
        int LifecycleEventCount,
        OperatorOsEventKind? LastLifecycleEventKind,
        string? LastLifecycleReasonCode,
        DateTimeOffset? LastLifecycleEventAt,
        string SupervisorCapabilityMode,
        string ProcessOwnershipStatus,
        bool HostProcessSpawned,
        bool HostProcessHandleHeld,
        bool StdIoCaptured,
        bool ImmediateDeathDetectionReady,
        bool RequiresWorkerProcessRegistration,
        string ProcessProofSource,
        int SupervisorAutomationGapCount,
        IReadOnlyList<string> SupervisorAutomationGaps,
        DateTimeOffset? LastHeartbeatAt,
        string LastReason,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record WorkerSupervisorArchivedInstanceSurface(
        string ArchiveId,
        string WorkerInstanceId,
        string RepoId,
        string WorkerIdentity,
        WorkerSupervisorInstanceState PreviousState,
        string? ActorSessionId,
        string? ScheduleBinding,
        string Reason,
        DateTimeOffset ArchivedAt,
        DateTimeOffset OriginalCreatedAt,
        DateTimeOffset OriginalUpdatedAt);

    private sealed record WorkerSupervisorEventsSurface(
        string SchemaVersion,
        string? RepoFilter,
        string? WorkerInstanceFilter,
        string? ActorSessionFilter,
        int EventCount,
        int DisplayedEventCount,
        bool Truncated,
        int MaxDisplayedEvents,
        string EventSource,
        IReadOnlyList<OperatorOsEventKind> IncludedEventKinds,
        IReadOnlyList<WorkerSupervisorEventSurface> Events,
        DateTimeOffset ProjectedAt);

    private sealed record WorkerSupervisorEventSurface(
        OperatorOsEventKind EventKind,
        string? RepoId,
        string? ActorSessionId,
        ActorSessionKind? ActorKind,
        string? ActorIdentity,
        string? WorkerInstanceId,
        string? ReasonCode,
        string Summary,
        string? DetailRef,
        DateTimeOffset OccurredAt);
}
