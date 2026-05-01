using System.Diagnostics;
using System.Text;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ActorSessionService
{
    private readonly IActorSessionRepository repository;
    private readonly OperatorOsEventStreamService eventStreamService;
    private readonly WorkerSupervisorStateService? workerSupervisorStateService;

    public ActorSessionService(
        IActorSessionRepository repository,
        OperatorOsEventStreamService eventStreamService,
        WorkerSupervisorStateService? workerSupervisorStateService = null)
    {
        this.repository = repository;
        this.eventStreamService = eventStreamService;
        this.workerSupervisorStateService = workerSupervisorStateService;
    }

    public ActorSessionRecord Ensure(
        ActorSessionKind kind,
        string actorIdentity,
        string repoId,
        string reason,
        string? actorSessionId = null,
        string? runtimeSessionId = null,
        string? operationClass = null,
        string? operation = null,
        string? providerProfile = null,
        string? capabilityProfile = null,
        string? sessionScope = null,
        string? budgetProfile = null,
        string? scheduleBinding = null,
        string? lastContextReceipt = null,
        bool? leaseEligible = null,
        string? healthPosture = null,
        int? processId = null,
        DateTimeOffset? processStartedAt = null,
        ActorSessionRegistrationMode? registrationMode = null,
        string? workerInstanceId = null,
        string? supervisorLaunchTokenId = null)
    {
        var resolvedProcessStartedAt = processStartedAt ?? ResolveProcessStartedAt(processId);
        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var stableId = string.IsNullOrWhiteSpace(actorSessionId)
            ? BuildStableId(kind, actorIdentity, repoId)
            : actorSessionId;
        var session = entries.FirstOrDefault(item => string.Equals(item.ActorSessionId, stableId, StringComparison.Ordinal));
        var created = false;
        if (session is null)
        {
            session = new ActorSessionRecord
            {
                ActorSessionId = stableId,
                Kind = kind,
                ActorIdentity = actorIdentity,
                RepoId = repoId,
                RuntimeSessionId = runtimeSessionId,
                LastOperationClass = operationClass,
                LastOperation = operation,
                State = ActorSessionState.Active,
                LastReason = reason,
            };
            session.RecordRegistrationMetadata(
                providerProfile,
                capabilityProfile,
                sessionScope,
                budgetProfile,
                scheduleBinding,
                lastContextReceipt,
                leaseEligible,
                healthPosture,
                processId,
                resolvedProcessStartedAt,
                registrationMode,
                workerInstanceId,
                supervisorLaunchTokenId);
            entries.Add(session);
            created = true;
        }
        else
        {
            session.Touch(ActorSessionState.Active, reason, runtimeSessionId, operationClass, operation);
            session.RecordRegistrationMetadata(
                providerProfile,
                capabilityProfile,
                sessionScope,
                budgetProfile,
                scheduleBinding,
                lastContextReceipt,
                leaseEligible,
                healthPosture,
                processId,
                resolvedProcessStartedAt,
                registrationMode,
                workerInstanceId,
                supervisorLaunchTokenId);
        }

        repository.Save(new ActorSessionSnapshot
        {
            Entries = entries.OrderBy(item => item.ActorSessionId, StringComparer.Ordinal).ToArray(),
        });
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = created ? OperatorOsEventKind.ActorSessionStarted : OperatorOsEventKind.ActorSessionUpdated,
            RepoId = repoId,
            ActorSessionId = session.ActorSessionId,
            ActorKind = session.Kind,
            ActorIdentity = session.ActorIdentity,
            ReferenceId = session.ActorSessionId,
            ReasonCode = created ? "actor_session_started" : "actor_session_updated",
            Summary = reason,
        });
        return session;
    }

    public ActorSessionRegistrationResult Register(
        ActorSessionKind kind,
        string actorIdentity,
        string repoId,
        string reason,
        bool dryRun,
        string? actorSessionId = null,
        string? runtimeSessionId = null,
        string? providerProfile = null,
        string? capabilityProfile = null,
        string? sessionScope = null,
        string? budgetProfile = null,
        string? scheduleBinding = null,
        string? lastContextReceipt = null,
        string? healthPosture = null,
        int? processId = null,
        ActorSessionRegistrationMode registrationMode = ActorSessionRegistrationMode.Manual,
        string? workerInstanceId = null,
        string? launchToken = null)
    {
        var operation = registrationMode == ActorSessionRegistrationMode.Supervised
            ? "register_supervised_worker"
            : ResolveRegistrationOperation(kind);
        var leaseEligible = kind == ActorSessionKind.Worker;
        var processStartedAt = ResolveProcessStartedAt(processId);
        var supervisorLaunchTokenId = (string?)null;
        var resolvedWorkerInstanceId = (string?)null;
        var supervisorRecord = (WorkerSupervisorInstanceRecord?)null;
        if (registrationMode == ActorSessionRegistrationMode.Supervised)
        {
            ValidateSupervisedRegistration(
                kind,
                actorIdentity,
                repoId,
                actorSessionId,
                processId,
                processStartedAt,
                workerInstanceId,
                launchToken,
                out supervisorRecord);
            resolvedWorkerInstanceId = supervisorRecord.WorkerInstanceId;
            supervisorLaunchTokenId = supervisorRecord.LaunchTokenId;
            providerProfile = string.IsNullOrWhiteSpace(providerProfile)
                ? supervisorRecord.ProviderProfile
                : providerProfile;
            capabilityProfile = string.IsNullOrWhiteSpace(capabilityProfile)
                ? supervisorRecord.CapabilityProfile
                : capabilityProfile;
            scheduleBinding = string.IsNullOrWhiteSpace(scheduleBinding)
                ? supervisorRecord.ScheduleBinding
                : scheduleBinding;
            if (!string.IsNullOrWhiteSpace(supervisorRecord.ActorSessionId))
            {
                actorSessionId = supervisorRecord.ActorSessionId;
            }
        }
        else
        {
            ValidateManualRegistration(workerInstanceId, launchToken);
        }

        var resolvedActorSessionId = string.IsNullOrWhiteSpace(actorSessionId)
            ? BuildStableActorSessionId(kind, actorIdentity, repoId)
            : actorSessionId;
        var roleSwitchCandidates = FindSameIdentityDifferentRole(kind, actorIdentity, repoId);
        var relatedActorSessionIds = roleSwitchCandidates
            .Select(item => item.ActorSessionId)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var roleSwitchEvidence = BuildRoleSwitchEvidence(kind, actorIdentity, relatedActorSessionIds);
        var scheduleBindingState = ResolveScheduleBindingState(scheduleBinding);
        var callbackWakeAuthority = ResolveCallbackWakeAuthority(kind, scheduleBinding);
        var scheduleReplacementPolicy = ResolveScheduleReplacementPolicy(scheduleBinding);
        var session = dryRun
            ? new ActorSessionRecord
            {
                ActorSessionId = resolvedActorSessionId,
                Kind = kind,
                ActorIdentity = actorIdentity,
                RepoId = repoId,
                RuntimeSessionId = runtimeSessionId,
                State = ActorSessionState.Active,
                LastOperationClass = "actor_session_registration",
                LastOperation = operation,
                LastReason = reason,
                ProcessId = processId,
                ProcessStartedAt = processStartedAt,
                RegistrationMode = registrationMode,
                WorkerInstanceId = resolvedWorkerInstanceId,
                SupervisorLaunchTokenId = supervisorLaunchTokenId,
            }
            : Ensure(
                kind,
                actorIdentity,
                repoId,
                reason,
                actorSessionId,
                runtimeSessionId,
                operationClass: "actor_session_registration",
                operation: operation,
                providerProfile: providerProfile,
                capabilityProfile: capabilityProfile,
                sessionScope: sessionScope,
                budgetProfile: budgetProfile,
                scheduleBinding: scheduleBinding,
                lastContextReceipt: lastContextReceipt,
                leaseEligible: leaseEligible,
                healthPosture: healthPosture,
                processId: processId,
                processStartedAt: processStartedAt,
                registrationMode: registrationMode,
                workerInstanceId: resolvedWorkerInstanceId,
                supervisorLaunchTokenId: supervisorLaunchTokenId);

        if (dryRun)
        {
            session.RecordRegistrationMetadata(
                providerProfile,
                capabilityProfile,
                sessionScope,
                budgetProfile,
                scheduleBinding,
                lastContextReceipt,
                leaseEligible,
                healthPosture,
                processId,
                processStartedAt,
                registrationMode,
                resolvedWorkerInstanceId,
                supervisorLaunchTokenId);
        }
        else if (relatedActorSessionIds.Length > 0)
        {
            eventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.ActorSessionUpdated,
                RepoId = session.RepoId,
                ActorSessionId = session.ActorSessionId,
                ActorKind = session.Kind,
                ActorIdentity = session.ActorIdentity,
                ReferenceId = session.ActorSessionId,
                ReasonCode = "same_agent_role_switch_evidence",
                Summary = roleSwitchEvidence,
                ExcerptTail = string.Join(",", relatedActorSessionIds),
            });
        }

        if (!dryRun && registrationMode == ActorSessionRegistrationMode.Supervised)
        {
            var boundSupervisorRecord = workerSupervisorStateService!.BindActorSession(
                resolvedWorkerInstanceId!,
                session.ActorSessionId,
                processId!.Value,
                processStartedAt!.Value,
                reason);
            eventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.ActorSessionUpdated,
                RepoId = session.RepoId,
                ActorSessionId = session.ActorSessionId,
                ActorKind = session.Kind,
                ActorIdentity = session.ActorIdentity,
                ReferenceId = resolvedWorkerInstanceId,
                ReasonCode = "supervised_actor_session_registered",
                Summary = $"Supervisor worker instance {resolvedWorkerInstanceId} bound to actor session {session.ActorSessionId}.",
            });
            eventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.WorkerSupervisorRunning,
                RepoId = boundSupervisorRecord.RepoId,
                ActorSessionId = session.ActorSessionId,
                ActorKind = ActorSessionKind.Worker,
                ActorIdentity = boundSupervisorRecord.WorkerIdentity,
                ReferenceId = boundSupervisorRecord.WorkerInstanceId,
                ReasonCode = "worker_supervisor_running",
                Summary = $"Worker supervisor instance {boundSupervisorRecord.WorkerInstanceId} is running as actor session {session.ActorSessionId}.",
            });
        }

        return new ActorSessionRegistrationResult(
            "actor-session-registration.v1",
            dryRun,
            dryRun ? "actor_session_registration_dry_run" : "actor_session_registered_or_refreshed",
            session.ActorSessionId,
            session.Kind,
            session.ActorIdentity,
            session.RepoId,
            session.State,
            session.ProviderProfile,
            session.CapabilityProfile,
            session.SessionScope,
            session.BudgetProfile,
            session.ScheduleBinding,
            session.LastContextReceipt,
            session.LeaseEligible,
            session.HealthPosture,
            session.ProcessId,
            session.ProcessStartedAt,
            session.RegistrationMode,
            session.WorkerInstanceId,
            session.SupervisorLaunchTokenId,
            registrationMode == ActorSessionRegistrationMode.Supervised,
            "actor_session_registration",
            operation,
            ResolveRoleBoundary(kind),
            kind == ActorSessionKind.Planner,
            kind == ActorSessionKind.Worker,
            true,
            false,
            false,
            false,
            relatedActorSessionIds.Length > 0,
            relatedActorSessionIds,
            roleSwitchEvidence,
            scheduleBindingState,
            callbackWakeAuthority,
            scheduleReplacementPolicy,
            false,
            false,
            reason,
            DateTimeOffset.UtcNow);
    }

    public WorkerSupervisorLaunchRegistrationResult RegisterWorkerSupervisorLaunch(
        string repoId,
        string workerIdentity,
        string reason,
        bool dryRun,
        string? workerInstanceId = null,
        string? hostSessionId = null,
        string? actorSessionId = null,
        string? providerProfile = null,
        string? capabilityProfile = null,
        string? scheduleBinding = null)
    {
        if (workerSupervisorStateService is null)
        {
            throw new InvalidOperationException("Worker supervisor launch registration requires worker supervisor state service.");
        }

        var registeredAt = DateTimeOffset.UtcNow;
        var resolvedWorkerInstanceId = string.IsNullOrWhiteSpace(workerInstanceId)
            ? dryRun ? "<generated-on-submit>" : null
            : workerInstanceId.Trim();
        if (dryRun)
        {
            return BuildWorkerSupervisorLaunchRegistrationResult(
                dryRun,
                "worker_supervisor_launch_dry_run",
                resolvedWorkerInstanceId ?? "<generated-on-submit>",
                repoId,
                workerIdentity,
                launchTokenId: null,
                launchToken: null,
                launchTokenExpiresAt: null,
                hostSessionId,
                actorSessionId,
                providerProfile,
                capabilityProfile,
                scheduleBinding,
                reason,
                registeredAt);
        }

        var launch = workerSupervisorStateService.RegisterPendingLaunch(
            repoId,
            workerIdentity,
            reason,
            resolvedWorkerInstanceId,
            hostSessionId,
            actorSessionId,
            providerProfile,
            capabilityProfile,
            scheduleBinding);
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.WorkerSupervisorLaunchRequested,
            RepoId = launch.RepoId,
            ActorSessionId = actorSessionId,
            ActorKind = ActorSessionKind.Worker,
            ActorIdentity = launch.WorkerIdentity,
            ReferenceId = launch.WorkerInstanceId,
            ReasonCode = "worker_supervisor_launch_requested",
            Summary = $"Worker supervisor instance {launch.WorkerInstanceId} launch requested for worker {launch.WorkerIdentity}; token id {launch.LaunchTokenId}.",
        });

        return BuildWorkerSupervisorLaunchRegistrationResult(
            dryRun,
            "worker_supervisor_launch_registered",
            launch.WorkerInstanceId,
            launch.RepoId,
            launch.WorkerIdentity,
            launch.LaunchTokenId,
            launch.LaunchToken,
            launch.LaunchTokenExpiresAt,
            hostSessionId,
            actorSessionId,
            providerProfile,
            capabilityProfile,
            scheduleBinding,
            reason,
            registeredAt);
    }

    public IReadOnlyList<WorkerSupervisorInstanceRecord> ListWorkerSupervisorInstances(string? repoId = null)
    {
        if (workerSupervisorStateService is null)
        {
            return Array.Empty<WorkerSupervisorInstanceRecord>();
        }

        return workerSupervisorStateService.List(repoId);
    }

    public IReadOnlyList<WorkerSupervisorArchivedInstanceRecord> ListWorkerSupervisorArchivedInstances(string? repoId = null)
    {
        if (workerSupervisorStateService is null)
        {
            return Array.Empty<WorkerSupervisorArchivedInstanceRecord>();
        }

        return workerSupervisorStateService.ListArchived(repoId);
    }

    public WorkerSupervisorInstanceRecord RecordWorkerSupervisorHostProcessHandle(
        string workerInstanceId,
        string hostProcessHandleId,
        string hostProcessHandleOwnerSessionId,
        string reason,
        string? stdoutLogPath = null,
        string? stderrLogPath = null)
    {
        if (workerSupervisorStateService is null)
        {
            throw new InvalidOperationException("Worker supervisor process handle recording requires worker supervisor state service.");
        }

        var updated = workerSupervisorStateService.RecordHostProcessHandle(
            workerInstanceId,
            hostProcessHandleId,
            hostProcessHandleOwnerSessionId,
            reason,
            stdoutLogPath,
            stderrLogPath);
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.WorkerSupervisorHostProcessHandleAcquired,
            RepoId = updated.RepoId,
            ActorSessionId = updated.ActorSessionId,
            ActorKind = ActorSessionKind.Worker,
            ActorIdentity = updated.WorkerIdentity,
            ReferenceId = updated.WorkerInstanceId,
            ReasonCode = "worker_supervisor_host_process_handle_acquired",
            Summary = $"Worker supervisor instance {updated.WorkerInstanceId} acquired a Host process handle owned by Host session {updated.HostProcessHandleOwnerSessionId}.",
        });

        return updated;
    }

    public WorkerSupervisorArchiveResult ArchiveWorkerSupervisorInstance(
        string workerInstanceId,
        bool dryRun,
        string reason)
    {
        if (workerSupervisorStateService is null)
        {
            throw new InvalidOperationException("Worker supervisor archive requires worker supervisor state service.");
        }

        var archivedAt = DateTimeOffset.UtcNow;
        var archiveOperation = workerSupervisorStateService.Archive(workerInstanceId, reason, dryRun);
        var record = archiveOperation.Record;
        var tombstone = archiveOperation.Tombstone;
        if (!dryRun)
        {
            eventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.WorkerSupervisorArchived,
                RepoId = record.RepoId,
                ActorSessionId = record.ActorSessionId,
                ActorKind = ActorSessionKind.Worker,
                ActorIdentity = record.WorkerIdentity,
                ReferenceId = record.WorkerInstanceId,
                ReasonCode = "worker_supervisor_archived",
                Summary = $"Worker supervisor instance {record.WorkerInstanceId} archived from {record.State} state with tombstone {tombstone?.ArchiveId ?? "(none)"}.",
            });
        }

        return new WorkerSupervisorArchiveResult(
            "worker-supervisor-archive.v1",
            dryRun,
            dryRun ? "worker_supervisor_archive_dry_run" : "worker_supervisor_archived",
            !dryRun,
            record.WorkerInstanceId,
            record.RepoId,
            record.WorkerIdentity,
            record.State,
            record.ActorSessionId,
            record.ScheduleBinding,
            tombstone?.ArchiveId,
            tombstone is not null,
            dryRun
                ? "Re-run without --dry-run to remove this supervisor live-state entry."
                : "Supervisor live-state entry removed and archived as a sanitized tombstone. Issue a new worker supervisor launch before reusing the worker instance id.",
            dryRun
                ? $"api worker-supervisor-archive --worker-instance-id {FormatCommandArgument(record.WorkerInstanceId)} --reason {FormatCommandArgument(reason)}"
                : $"api worker-supervisor-launch --repo-id {FormatCommandArgument(record.RepoId)} --identity {FormatCommandArgument(record.WorkerIdentity)} --worker-instance-id {FormatCommandArgument(record.WorkerInstanceId)}",
            false,
            false,
            false,
            false,
            reason,
            archivedAt);
    }

    public WorkerSupervisorInstanceRecord? MarkBoundWorkerSupervisorLost(
        string actorSessionId,
        bool dryRun,
        string reason)
    {
        if (workerSupervisorStateService is null)
        {
            return null;
        }

        var existing = workerSupervisorStateService.TryGetByActorSessionId(actorSessionId);
        if (existing is null)
        {
            return null;
        }

        if (dryRun)
        {
            return existing;
        }

        var updated = workerSupervisorStateService.MarkActorSessionLost(actorSessionId, reason);
        if (updated is not null)
        {
            eventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.WorkerSupervisorLost,
                RepoId = updated.RepoId,
                ActorSessionId = actorSessionId,
                ActorKind = ActorSessionKind.Worker,
                ActorIdentity = updated.WorkerIdentity,
                ReferenceId = updated.WorkerInstanceId,
                ReasonCode = "worker_supervisor_lost",
                Summary = $"Worker supervisor instance {updated.WorkerInstanceId} marked Lost because bound actor session {actorSessionId} was reconciled as non-live.",
            });
            eventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.ActorSessionLivenessReconciled,
                RepoId = updated.RepoId,
                ActorSessionId = actorSessionId,
                ActorKind = ActorSessionKind.Worker,
                ActorIdentity = updated.WorkerIdentity,
                ReferenceId = updated.WorkerInstanceId,
                ReasonCode = "worker_supervisor_instance_lost",
                Summary = $"Worker supervisor instance {updated.WorkerInstanceId} marked Lost because bound actor session {actorSessionId} was reconciled as non-live.",
            });
        }

        return updated;
    }

    public ActorSessionRecord MarkState(
        string actorSessionId,
        ActorSessionState state,
        string reason,
        string? runtimeSessionId = null,
        string? taskId = null,
        string? runId = null,
        string? permissionRequestId = null,
        OwnershipScope? ownershipScope = null,
        string? ownershipTargetId = null,
        string? operationClass = null,
        string? operation = null)
    {
        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var session = entries.FirstOrDefault(item => string.Equals(item.ActorSessionId, actorSessionId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Actor session '{actorSessionId}' was not found.");
        session.Touch(state, reason, runtimeSessionId, operationClass, operation, taskId, runId, permissionRequestId);
        if (ownershipScope is not null && !string.IsNullOrWhiteSpace(ownershipTargetId))
        {
            session.RecordOwnership(ownershipScope.Value, ownershipTargetId, reason);
        }

        repository.Save(new ActorSessionSnapshot
        {
            Entries = entries.OrderBy(item => item.ActorSessionId, StringComparer.Ordinal).ToArray(),
        });
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.ActorSessionUpdated,
            RepoId = session.RepoId,
            ActorSessionId = session.ActorSessionId,
            ActorKind = session.Kind,
            ActorIdentity = session.ActorIdentity,
            TaskId = taskId,
            RunId = runId,
            PermissionRequestId = permissionRequestId,
            OwnershipScope = ownershipScope,
            OwnershipTargetId = ownershipTargetId,
            ReferenceId = session.ActorSessionId,
            ReasonCode = "actor_session_state_changed",
            Summary = reason,
        });
        return session;
    }

    public IReadOnlyList<ActorSessionRecord> List(ActorSessionKind? kind = null)
    {
        return repository.Load().Entries
            .Where(item => kind is null || item.Kind == kind.Value)
            .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();
    }

    public ActorSessionFallbackPolicyResult BuildFallbackPolicy(string repoId)
    {
        var registeredSessions = repository.Load().Entries
            .Where(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal))
            .Where(item => item.State != ActorSessionState.Stopped)
            .ToArray();
        var liveness = ActorSessionLivenessRules.Classify(registeredSessions);
        var sessions = liveness.LiveSessions;
        var nonLiveSessions = liveness.ClosedSessions.Concat(liveness.StaleSessions).ToArray();
        var nonLiveStopCommands = ActorSessionLivenessRules.BuildStopCommands(nonLiveSessions, "actor-thread-not-live");
        var explicitPlannerRegistered = sessions.Any(item => item.Kind == ActorSessionKind.Planner);
        var explicitWorkerRegistered = sessions.Any(item => item.Kind == ActorSessionKind.Worker);
        var missingActorRoles = BuildMissingActorRoles(explicitPlannerRegistered, explicitWorkerRegistered);
        var fallbackAllowed = missingActorRoles.Length > 0;
        var fallbackMode = fallbackAllowed
            ? "same_agent_split_role_fallback_allowed_with_evidence"
            : "explicit_actor_sessions_ready";
        var recommendedNextAction = nonLiveStopCommands.Length > 0
            ? $"Stop or refresh non-live actor session(s) before relying on explicit role binding. Next command: {nonLiveStopCommands[0]}"
            : fallbackAllowed
            ? $"Register missing actor session role(s): {string.Join(", ", missingActorRoles)}; until then, use same-agent split-role fallback only with Host-mediated role-switch evidence."
            : "Use explicit PlannerSession and WorkerSession bindings; same-agent fallback is not needed.";

        return new ActorSessionFallbackPolicyResult(
            "actor-session-fallback-policy.v1",
            repoId,
            explicitPlannerRegistered,
            explicitWorkerRegistered,
            fallbackAllowed,
            fallbackMode,
            missingActorRoles,
            [
                "same_agent_role_switch_evidence",
                "explicit_actor_registration_or_context_receipt",
                "host_routed_task_run_or_result_ingestion",
                "fallback_run_packet"
            ],
            true,
            [
                "role_switch_receipt",
                "context_receipt",
                "execution_claim",
                "review_bundle"
            ],
            [
                "operator_session_is_interaction_surface_not_operator_shell_truth",
                "planner_session_is_decision_only_no_implementation",
                "worker_session_is_execution_only_host_dispatch_required",
                "schedule_binding_is_callback_projection_only",
                "no_second_task_queue",
                "no_second_execution_truth_root",
                "review_writeback_required_before_completion"
            ],
            recommendedNextAction,
            registeredSessions.Length,
            sessions.Count,
            nonLiveSessions.Length,
            liveness.StaleSessions.Count,
            liveness.ClosedSessions.Count,
            (int)liveness.FreshnessWindow.TotalSeconds,
            liveness.CheckedAt,
            liveness.StaleSessions.Select(item => item.ActorSessionId).ToArray(),
            liveness.ClosedSessions.Select(item => item.ActorSessionId).ToArray(),
            nonLiveStopCommands,
            "stale_or_closed_sessions_do_not_satisfy_role_binding",
            false,
            false,
            false,
            DateTimeOffset.UtcNow);
    }

    public ActorSessionRepairResult RepairStaleCurrentWork(bool dryRun, string reason)
    {
        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var repairs = new List<ActorSessionRepairEntry>();

        foreach (var session in entries.Where(IsStaleCurrentWorkResidue))
        {
            var repairedState = ResolveRepairedState(session.State);
            repairs.Add(new ActorSessionRepairEntry(
                session.ActorSessionId,
                session.Kind,
                session.ActorIdentity,
                session.RepoId,
                session.State,
                repairedState,
                session.CurrentTaskId,
                session.CurrentRunId,
                "invalid_current_task_id"));

            if (!dryRun)
            {
                session.ClearCurrentWork(repairedState, reason);
            }
        }

        if (!dryRun && repairs.Count > 0)
        {
            repository.Save(new ActorSessionSnapshot
            {
                Entries = entries.OrderBy(item => item.ActorSessionId, StringComparer.Ordinal).ToArray(),
            });

            foreach (var repair in repairs)
            {
                eventStreamService.Append(new OperatorOsEventRecord
                {
                    EventKind = OperatorOsEventKind.ActorSessionUpdated,
                    RepoId = repair.RepoId,
                    ActorSessionId = repair.ActorSessionId,
                    ActorKind = repair.Kind,
                    ActorIdentity = repair.ActorIdentity,
                    TaskId = repair.PreviousCurrentTaskId,
                    RunId = repair.PreviousCurrentRunId,
                    ReferenceId = repair.ActorSessionId,
                    ReasonCode = "actor_session_stale_current_work_repaired",
                    Summary = reason,
                });
            }
        }

        return new ActorSessionRepairResult(
            "actor-session-repair.v1",
            dryRun,
            repairs.Count,
            repairs,
            reason,
            DateTimeOffset.UtcNow);
    }

    public ActorSessionClearResult Clear(
        string? actorSessionId,
        string? repoId,
        ActorSessionKind? kind,
        bool dryRun,
        string reason)
    {
        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var matched = entries
            .Where(item => string.IsNullOrWhiteSpace(actorSessionId) || string.Equals(item.ActorSessionId, actorSessionId, StringComparison.Ordinal))
            .Where(item => string.IsNullOrWhiteSpace(repoId) || string.Equals(item.RepoId, repoId, StringComparison.Ordinal))
            .Where(item => kind is null || item.Kind == kind.Value)
            .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();

        if (!dryRun && matched.Length > 0)
        {
            var matchedIds = matched.Select(item => item.ActorSessionId).ToHashSet(StringComparer.Ordinal);
            repository.Save(new ActorSessionSnapshot
            {
                Entries = entries
                    .Where(item => !matchedIds.Contains(item.ActorSessionId))
                    .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
                    .ToArray(),
            });

            foreach (var session in matched)
            {
                eventStreamService.Append(new OperatorOsEventRecord
                {
                    EventKind = OperatorOsEventKind.ActorSessionUpdated,
                    RepoId = session.RepoId,
                    ActorSessionId = session.ActorSessionId,
                    ActorKind = session.Kind,
                    ActorIdentity = session.ActorIdentity,
                    ReferenceId = session.ActorSessionId,
                    ReasonCode = "actor_session_cache_cleared",
                    Summary = reason,
                });
            }
        }

        return new ActorSessionClearResult(
            "actor-session-clear.v1",
            dryRun,
            matched.Length,
            matched
                .Select(item => new ActorSessionClearEntry(
                    item.ActorSessionId,
                    item.Kind,
                    item.ActorIdentity,
                    item.RepoId,
                    item.State))
                .ToArray(),
            reason,
            DateTimeOffset.UtcNow);
    }

    public ActorSessionStopResult Stop(string actorSessionId, bool dryRun, string reason)
    {
        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var session = entries.FirstOrDefault(item => string.Equals(item.ActorSessionId, actorSessionId, StringComparison.Ordinal));
        if (session is null)
        {
            return new ActorSessionStopResult(
                "actor-session-stop.v1",
                dryRun,
                actorSessionId,
                Found: false,
                Stopped: false,
                Kind: null,
                ActorIdentity: null,
                RepoId: null,
                PreviousState: null,
                CurrentState: null,
                WorkerSupervisorStopRequired: false,
                WorkerSupervisorStopSynchronized: false,
                WorkerSupervisorSynchronizationStatus: "not_applicable",
                WorkerSupervisorInstanceId: null,
                WorkerSupervisorPreviousState: null,
                WorkerSupervisorCurrentState: null,
                Reason: reason,
                StoppedAt: DateTimeOffset.UtcNow);
        }

        var previousState = session.State;
        var supervisedWorkerStopRequired = session.Kind == ActorSessionKind.Worker
            && session.RegistrationMode == ActorSessionRegistrationMode.Supervised;
        var supervisorBeforeStop = supervisedWorkerStopRequired && workerSupervisorStateService is not null
            ? workerSupervisorStateService.TryGetByActorSessionId(session.ActorSessionId)
            : null;
        var supervisorWorkerInstanceId = supervisorBeforeStop?.WorkerInstanceId ?? session.WorkerInstanceId;
        var supervisorPreviousState = supervisorBeforeStop?.State;
        var supervisorAfterStop = (WorkerSupervisorInstanceRecord?)null;
        if (!dryRun)
        {
            if (supervisedWorkerStopRequired && workerSupervisorStateService is not null)
            {
                supervisorAfterStop = workerSupervisorStateService.MarkActorSessionStopped(
                    session.ActorSessionId,
                    reason);
            }

            session.ClearCurrentWork(ActorSessionState.Stopped, reason);
            repository.Save(new ActorSessionSnapshot
            {
                Entries = entries.OrderBy(item => item.ActorSessionId, StringComparer.Ordinal).ToArray(),
            });
            eventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.ActorSessionUpdated,
                RepoId = session.RepoId,
                ActorSessionId = session.ActorSessionId,
                ActorKind = session.Kind,
                ActorIdentity = session.ActorIdentity,
                ReferenceId = session.ActorSessionId,
                ReasonCode = "actor_session_stopped",
                Summary = reason,
            });
            if (supervisorAfterStop is not null)
            {
                eventStreamService.Append(new OperatorOsEventRecord
                {
                    EventKind = OperatorOsEventKind.WorkerSupervisorStopped,
                    RepoId = supervisorAfterStop.RepoId,
                    ActorSessionId = session.ActorSessionId,
                    ActorKind = ActorSessionKind.Worker,
                    ActorIdentity = supervisorAfterStop.WorkerIdentity,
                    ReferenceId = supervisorAfterStop.WorkerInstanceId,
                    ReasonCode = "worker_supervisor_stopped",
                    Summary = $"Worker supervisor instance {supervisorAfterStop.WorkerInstanceId} marked Stopped because bound actor session {session.ActorSessionId} was explicitly stopped.",
                });
            }
        }

        var supervisorCurrentState = dryRun
            ? supervisorPreviousState
            : supervisorAfterStop?.State ?? supervisorPreviousState;
        return new ActorSessionStopResult(
            "actor-session-stop.v1",
            dryRun,
            session.ActorSessionId,
            Found: true,
            Stopped: !dryRun,
            Kind: session.Kind,
            ActorIdentity: session.ActorIdentity,
            RepoId: session.RepoId,
            PreviousState: previousState,
            CurrentState: dryRun ? previousState : ActorSessionState.Stopped,
            WorkerSupervisorStopRequired: supervisedWorkerStopRequired,
            WorkerSupervisorStopSynchronized: !dryRun && supervisorAfterStop is not null,
            WorkerSupervisorSynchronizationStatus: ResolveWorkerSupervisorStopSynchronizationStatus(
                supervisedWorkerStopRequired,
                workerSupervisorStateService is not null,
                dryRun,
                supervisorBeforeStop,
                supervisorAfterStop),
            WorkerSupervisorInstanceId: supervisorAfterStop?.WorkerInstanceId
                ?? supervisorWorkerInstanceId,
            WorkerSupervisorPreviousState: supervisorPreviousState,
            WorkerSupervisorCurrentState: supervisorCurrentState,
            Reason: reason,
            StoppedAt: DateTimeOffset.UtcNow);
    }

    private static string ResolveWorkerSupervisorStopSynchronizationStatus(
        bool supervisedWorkerStopRequired,
        bool workerSupervisorServiceAvailable,
        bool dryRun,
        WorkerSupervisorInstanceRecord? supervisorBeforeStop,
        WorkerSupervisorInstanceRecord? supervisorAfterStop)
    {
        if (!supervisedWorkerStopRequired)
        {
            return "not_required";
        }

        if (!workerSupervisorServiceAvailable)
        {
            return "supervisor_service_unavailable";
        }

        if (supervisorBeforeStop is null)
        {
            return "supervisor_record_missing";
        }

        if (dryRun)
        {
            return "dry_run_supervisor_stop_pending";
        }

        return supervisorAfterStop is null
            ? "supervisor_record_missing"
            : "supervisor_stopped_before_actor_session_stop";
    }

    public ActorSessionHeartbeatResult Heartbeat(
        string actorSessionId,
        bool dryRun,
        string reason,
        string? healthPosture = null,
        string? lastContextReceipt = null)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var session = entries.FirstOrDefault(item => string.Equals(item.ActorSessionId, actorSessionId, StringComparison.Ordinal));
        if (session is null)
        {
            return new ActorSessionHeartbeatResult(
                "actor-session-heartbeat.v1",
                dryRun,
                actorSessionId,
                Found: false,
                HeartbeatAccepted: false,
                Updated: false,
                Kind: null,
                ActorIdentity: null,
                RepoId: null,
                PreviousState: null,
                CurrentState: null,
                PreviousHealthPosture: null,
                CurrentHealthPosture: null,
                PreviousContextReceipt: null,
                CurrentContextReceipt: null,
                PreviousLastSeenAt: null,
                CurrentLastSeenAt: null,
                LivenessStatus: "not_found",
                Reason: reason,
                GrantsExecutionAuthority: false,
                GrantsTruthWriteAuthority: false,
                CreatesTaskQueue: false,
                CheckedAt: checkedAt);
        }

        var previousState = session.State;
        var previousHealthPosture = session.HealthPosture;
        var previousContextReceipt = session.LastContextReceipt;
        var previousLastSeenAt = session.LastSeenAt;
        var heartbeatAccepted = session.State != ActorSessionState.Stopped;
        if (!dryRun && heartbeatAccepted)
        {
            session.Touch(
                session.State,
                reason,
                operationClass: "actor_session_liveness",
                operation: "heartbeat");
            session.RecordRegistrationMetadata(
                lastContextReceipt: string.IsNullOrWhiteSpace(lastContextReceipt) ? null : lastContextReceipt.Trim(),
                healthPosture: string.IsNullOrWhiteSpace(healthPosture) ? null : healthPosture.Trim());
            repository.Save(new ActorSessionSnapshot
            {
                Entries = entries.OrderBy(item => item.ActorSessionId, StringComparer.Ordinal).ToArray(),
            });
            eventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.ActorSessionUpdated,
                RepoId = session.RepoId,
                ActorSessionId = session.ActorSessionId,
                ActorKind = session.Kind,
                ActorIdentity = session.ActorIdentity,
                ReferenceId = session.ActorSessionId,
                ReasonCode = "actor_session_heartbeat",
                Summary = reason,
            });
        }

        return new ActorSessionHeartbeatResult(
            "actor-session-heartbeat.v1",
            dryRun,
            session.ActorSessionId,
            Found: true,
            HeartbeatAccepted: heartbeatAccepted,
            Updated: heartbeatAccepted && !dryRun,
            Kind: session.Kind,
            ActorIdentity: session.ActorIdentity,
            RepoId: session.RepoId,
            PreviousState: previousState,
            CurrentState: session.State,
            PreviousHealthPosture: previousHealthPosture,
            CurrentHealthPosture: session.HealthPosture,
            PreviousContextReceipt: previousContextReceipt,
            CurrentContextReceipt: session.LastContextReceipt,
            PreviousLastSeenAt: previousLastSeenAt,
            CurrentLastSeenAt: session.LastSeenAt,
            LivenessStatus: ResolveLivenessStatus(session, checkedAt),
            Reason: heartbeatAccepted ? reason : "Stopped actor session cannot be refreshed by heartbeat; register or start explicitly.",
            GrantsExecutionAuthority: false,
            GrantsTruthWriteAuthority: false,
            CreatesTaskQueue: false,
            CheckedAt: checkedAt);
    }

    public ActorSessionRecord? TryGet(string actorSessionId)
    {
        return repository.Load().Entries.FirstOrDefault(item => string.Equals(item.ActorSessionId, actorSessionId, StringComparison.Ordinal));
    }

    public void ClearOwnership(string actorSessionId, string reason)
    {
        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var session = entries.FirstOrDefault(item => string.Equals(item.ActorSessionId, actorSessionId, StringComparison.Ordinal));
        if (session is null)
        {
            return;
        }

        session.ClearOwnership(reason);
        repository.Save(new ActorSessionSnapshot
        {
            Entries = entries.OrderBy(item => item.ActorSessionId, StringComparer.Ordinal).ToArray(),
        });
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.ActorSessionUpdated,
            RepoId = session.RepoId,
            ActorSessionId = session.ActorSessionId,
            ActorKind = session.Kind,
            ActorIdentity = session.ActorIdentity,
            ReferenceId = session.ActorSessionId,
            ReasonCode = "actor_session_ownership_cleared",
            Summary = reason,
        });
    }

    public static string BuildStableActorSessionId(ActorSessionKind kind, string actorIdentity, string repoId)
    {
        return BuildStableId(kind, actorIdentity, repoId);
    }

    private static string BuildStableId(ActorSessionKind kind, string actorIdentity, string repoId)
    {
        return $"actor-{ToSlug(kind.ToString())}-{ToSlug(repoId)}-{ToSlug(actorIdentity)}";
    }

    private static string ResolveRegistrationOperation(ActorSessionKind kind)
    {
        return kind switch
        {
            ActorSessionKind.Operator => "register_operator_session",
            ActorSessionKind.Planner => "register_planner_session",
            ActorSessionKind.Worker => "register_worker_session",
            ActorSessionKind.Agent => "register_agent_session",
            _ => "register_actor_session",
        };
    }

    private static string ResolveRoleBoundary(ActorSessionKind kind)
    {
        return kind switch
        {
            ActorSessionKind.Operator => "operator_governance_shell_no_task_queue",
            ActorSessionKind.Planner => "planner_decision_only_no_implementation",
            ActorSessionKind.Worker => "worker_execution_only_host_dispatch_required",
            ActorSessionKind.Agent => "agent_attached_unbound_until_role_binding",
            _ => "actor_session_unclassified",
        };
    }

    private static WorkerSupervisorLaunchRegistrationResult BuildWorkerSupervisorLaunchRegistrationResult(
        bool dryRun,
        string outcome,
        string workerInstanceId,
        string repoId,
        string workerIdentity,
        string? launchTokenId,
        string? launchToken,
        DateTimeOffset? launchTokenExpiresAt,
        string? hostSessionId,
        string? actorSessionId,
        string? providerProfile,
        string? capabilityProfile,
        string? scheduleBinding,
        string reason,
        DateTimeOffset registeredAt)
    {
        var apiRegistrationCommand = BuildSupervisedActorRegistrationCommand(
            workerIdentity,
            repoId,
            actorSessionId,
            workerInstanceId,
            launchToken ?? "<launch-token>",
            providerProfile,
            capabilityProfile,
            scheduleBinding,
            jsonApi: true);
        var textRegistrationCommand = BuildSupervisedActorRegistrationCommand(
            workerIdentity,
            repoId,
            actorSessionId,
            workerInstanceId,
            launchToken ?? "<launch-token>",
            providerProfile,
            capabilityProfile,
            scheduleBinding,
            jsonApi: false);

        return new WorkerSupervisorLaunchRegistrationResult(
            "worker-supervisor-launch-registration.v1",
            dryRun,
            outcome,
            workerInstanceId,
            repoId,
            workerIdentity,
            launchTokenId,
            launchToken,
            launchTokenExpiresAt,
            launchToken is not null,
            launchToken is null ? "not_issued_in_dry_run" : "returned_once_not_persisted",
            hostSessionId,
            actorSessionId,
            providerProfile,
            capabilityProfile,
            scheduleBinding,
            apiRegistrationCommand,
            textRegistrationCommand,
            "registration_only_launch_token",
            "host_registered_launch_token_process_not_spawned",
            false,
            false,
            false,
            false,
            true,
            "supervised_actor_registration_process_id_and_start_time",
            false,
            false,
            false,
            false,
            reason,
            registeredAt);
    }

    private static string BuildSupervisedActorRegistrationCommand(
        string workerIdentity,
        string repoId,
        string? actorSessionId,
        string workerInstanceId,
        string launchToken,
        string? providerProfile,
        string? capabilityProfile,
        string? scheduleBinding,
        bool jsonApi)
    {
        var command = new List<string>
        {
            jsonApi ? "api" : "actor",
            jsonApi ? "actor-session-register" : "register",
            "--kind",
            "worker",
            "--identity",
            FormatCommandArgument(workerIdentity),
            "--repo-id",
            FormatCommandArgument(repoId),
            "--registration-mode",
            "supervised",
            "--worker-instance-id",
            FormatCommandArgument(workerInstanceId),
            "--launch-token",
            FormatCommandArgument(launchToken),
            "--process-id",
            "<pid>",
            "--context-receipt",
            "<context-receipt>",
            "--health",
            "healthy",
        };
        if (!string.IsNullOrWhiteSpace(actorSessionId))
        {
            command.Add("--actor-session-id");
            command.Add(FormatCommandArgument(actorSessionId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(providerProfile))
        {
            command.Add("--provider-profile");
            command.Add(FormatCommandArgument(providerProfile.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(capabilityProfile))
        {
            command.Add("--capability-profile");
            command.Add(FormatCommandArgument(capabilityProfile.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(scheduleBinding))
        {
            command.Add("--schedule-binding");
            command.Add(FormatCommandArgument(scheduleBinding.Trim()));
        }

        return string.Join(' ', command);
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

    private void ValidateSupervisedRegistration(
        ActorSessionKind kind,
        string actorIdentity,
        string repoId,
        string? actorSessionId,
        int? processId,
        DateTimeOffset? processStartedAt,
        string? workerInstanceId,
        string? launchToken,
        out WorkerSupervisorInstanceRecord supervisorRecord)
    {
        if (workerSupervisorStateService is null)
        {
            throw new InvalidOperationException("Supervised actor session registration requires worker supervisor state service.");
        }

        if (kind != ActorSessionKind.Worker)
        {
            throw new InvalidOperationException("Supervised actor session registration is only valid for worker sessions.");
        }

        if (string.IsNullOrWhiteSpace(workerInstanceId))
        {
            throw new InvalidOperationException("Supervised actor session registration requires --worker-instance-id.");
        }

        if (string.IsNullOrWhiteSpace(launchToken))
        {
            throw new InvalidOperationException("Supervised actor session registration requires --launch-token.");
        }

        if (processId is not int id || id <= 0 || processStartedAt is null)
        {
            throw new InvalidOperationException("Supervised actor session registration requires a live --process-id with resolvable process start time.");
        }

        supervisorRecord = workerSupervisorStateService.TryGet(workerInstanceId.Trim())
            ?? throw new InvalidOperationException($"Worker supervisor instance '{workerInstanceId.Trim()}' was not found.");
        if (!string.Equals(supervisorRecord.RepoId, repoId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Worker supervisor instance '{supervisorRecord.WorkerInstanceId}' belongs to repo '{supervisorRecord.RepoId}', not '{repoId}'.");
        }

        if (!string.Equals(supervisorRecord.WorkerIdentity, actorIdentity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Worker supervisor instance '{supervisorRecord.WorkerInstanceId}' belongs to identity '{supervisorRecord.WorkerIdentity}', not '{actorIdentity}'.");
        }

        if (!string.IsNullOrWhiteSpace(supervisorRecord.ActorSessionId)
            && !string.IsNullOrWhiteSpace(actorSessionId)
            && !string.Equals(supervisorRecord.ActorSessionId, actorSessionId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Worker supervisor instance '{supervisorRecord.WorkerInstanceId}' is already bound to actor session '{supervisorRecord.ActorSessionId}'.");
        }

        if (supervisorRecord.State is not WorkerSupervisorInstanceState.Requested
            and not WorkerSupervisorInstanceState.Starting)
        {
            throw new InvalidOperationException($"Worker supervisor instance '{supervisorRecord.WorkerInstanceId}' is not registrable because it is {supervisorRecord.State}.");
        }

        if (!workerSupervisorStateService.VerifyLaunchToken(supervisorRecord, launchToken.Trim()))
        {
            throw new InvalidOperationException("Supervised actor session registration rejected because launch token verification failed.");
        }
    }

    private static void ValidateManualRegistration(string? workerInstanceId, string? launchToken)
    {
        if (!string.IsNullOrWhiteSpace(workerInstanceId) || !string.IsNullOrWhiteSpace(launchToken))
        {
            throw new InvalidOperationException("Worker supervisor launch fields require --registration-mode supervised.");
        }
    }

    private IReadOnlyList<ActorSessionRecord> FindSameIdentityDifferentRole(
        ActorSessionKind kind,
        string actorIdentity,
        string repoId)
    {
        return repository.Load().Entries
            .Where(item => item.Kind != kind)
            .Where(item => string.Equals(item.ActorIdentity, actorIdentity, StringComparison.Ordinal))
            .Where(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal))
            .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildRoleSwitchEvidence(
        ActorSessionKind kind,
        string actorIdentity,
        IReadOnlyList<string> relatedActorSessionIds)
    {
        return relatedActorSessionIds.Count == 0
            ? "No same-agent role switch was detected for this registration."
            : $"Same-agent role switch evidence: actor identity '{actorIdentity}' registered as {kind} while related role session(s) already exist: {string.Join(", ", relatedActorSessionIds)}. This is transitional Host-mediated role binding evidence, not permission for a role to cross its authority boundary.";
    }

    private static string ResolveScheduleBindingState(string? scheduleBinding)
    {
        return string.IsNullOrWhiteSpace(scheduleBinding)
            ? "unbound_manual_or_fallback_wake"
            : "registered_projection_only";
    }

    private static string ResolveCallbackWakeAuthority(ActorSessionKind kind, string? scheduleBinding)
    {
        if (string.IsNullOrWhiteSpace(scheduleBinding))
        {
            return kind switch
            {
                ActorSessionKind.Planner => "host_manual_or_scheduler_wake_required_for_planner_reentry",
                ActorSessionKind.Worker => "host_dispatch_required_before_worker_invocation",
                _ => "operator_or_host_wake_required",
            };
        }

        return kind switch
        {
            ActorSessionKind.Planner => "host_mediated_planner_reentry_only",
            ActorSessionKind.Worker => "host_mediated_worker_callback_projection_only",
            _ => "host_mediated_callback_projection_only",
        };
    }

    private static string ResolveScheduleReplacementPolicy(string? scheduleBinding)
    {
        return string.IsNullOrWhiteSpace(scheduleBinding)
            ? "register_or_refresh_actor_session_with_schedule_binding_to_attach_callback"
            : "replace_by_reregistering_actor_session_schedule_binding";
    }

    private static string[] BuildMissingActorRoles(bool explicitPlannerRegistered, bool explicitWorkerRegistered)
    {
        var roles = new List<string>();
        if (!explicitPlannerRegistered)
        {
            roles.Add("planner");
        }

        if (!explicitWorkerRegistered)
        {
            roles.Add("worker");
        }

        return roles.ToArray();
    }

    private static bool IsStaleCurrentWorkResidue(ActorSessionRecord session)
    {
        if (string.IsNullOrWhiteSpace(session.CurrentTaskId))
        {
            return false;
        }

        var currentTaskId = session.CurrentTaskId.Trim();
        var looksLikeCliOption = currentTaskId.StartsWith("-", StringComparison.Ordinal);
        var looksLikeHelpToken = string.Equals(currentTaskId, "help", StringComparison.OrdinalIgnoreCase);
        if (!looksLikeCliOption && !looksLikeHelpToken)
        {
            return false;
        }

        return string.Equals(session.LastOperationClass, "delegation", StringComparison.OrdinalIgnoreCase)
               || string.Equals(session.LastOperation, "run_task", StringComparison.OrdinalIgnoreCase);
    }

    private static ActorSessionState ResolveRepairedState(ActorSessionState state)
    {
        return state is ActorSessionState.Blocked or ActorSessionState.Waiting
            ? ActorSessionState.Idle
            : state;
    }

    private static string ResolveLivenessStatus(ActorSessionRecord session, DateTimeOffset checkedAt)
    {
        return ActorSessionLivenessRules.ResolveStatus(session, checkedAt);
    }

    private static DateTimeOffset? ResolveProcessStartedAt(int? processId)
    {
        if (processId is not int id || id <= 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(id);
            if (process.HasExited)
            {
                return null;
            }

            return NormalizeProcessStartTime(process.StartTime);
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset NormalizeProcessStartTime(DateTime startTime)
    {
        var localStartTime = startTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(startTime, DateTimeKind.Local)
            : startTime;
        return new DateTimeOffset(localStartTime).ToUniversalTime();
    }

    private static string ToSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        return builder.ToString().Trim('-');
    }
}

public sealed record ActorSessionRepairResult(
    string SchemaVersion,
    bool DryRun,
    int RepairedCount,
    IReadOnlyList<ActorSessionRepairEntry> Repairs,
    string Reason,
    DateTimeOffset RepairedAt);

public sealed record ActorSessionRepairEntry(
    string ActorSessionId,
    ActorSessionKind Kind,
    string ActorIdentity,
    string RepoId,
    ActorSessionState PreviousState,
    ActorSessionState RepairedState,
    string? PreviousCurrentTaskId,
    string? PreviousCurrentRunId,
    string ReasonCode);

public sealed record ActorSessionClearResult(
    string SchemaVersion,
    bool DryRun,
    int ClearedCount,
    IReadOnlyList<ActorSessionClearEntry> ClearedSessions,
    string Reason,
    DateTimeOffset ClearedAt);

public sealed record ActorSessionClearEntry(
    string ActorSessionId,
    ActorSessionKind Kind,
    string ActorIdentity,
    string RepoId,
    ActorSessionState State);

public sealed record ActorSessionStopResult(
    string SchemaVersion,
    bool DryRun,
    string ActorSessionId,
    bool Found,
    bool Stopped,
    ActorSessionKind? Kind,
    string? ActorIdentity,
    string? RepoId,
    ActorSessionState? PreviousState,
    ActorSessionState? CurrentState,
    bool WorkerSupervisorStopRequired,
    bool WorkerSupervisorStopSynchronized,
    string WorkerSupervisorSynchronizationStatus,
    string? WorkerSupervisorInstanceId,
    WorkerSupervisorInstanceState? WorkerSupervisorPreviousState,
    WorkerSupervisorInstanceState? WorkerSupervisorCurrentState,
    string Reason,
    DateTimeOffset StoppedAt);

public sealed record ActorSessionHeartbeatResult(
    string SchemaVersion,
    bool DryRun,
    string ActorSessionId,
    bool Found,
    bool HeartbeatAccepted,
    bool Updated,
    ActorSessionKind? Kind,
    string? ActorIdentity,
    string? RepoId,
    ActorSessionState? PreviousState,
    ActorSessionState? CurrentState,
    string? PreviousHealthPosture,
    string? CurrentHealthPosture,
    string? PreviousContextReceipt,
    string? CurrentContextReceipt,
    DateTimeOffset? PreviousLastSeenAt,
    DateTimeOffset? CurrentLastSeenAt,
    string LivenessStatus,
    string Reason,
    bool GrantsExecutionAuthority,
    bool GrantsTruthWriteAuthority,
    bool CreatesTaskQueue,
    DateTimeOffset CheckedAt);

public sealed record ActorSessionFallbackPolicyResult(
    string SchemaVersion,
    string RepoId,
    bool ExplicitPlannerRegistered,
    bool ExplicitWorkerRegistered,
    bool FallbackAllowed,
    string FallbackMode,
    IReadOnlyList<string> MissingActorRoles,
    IReadOnlyList<string> RequiredEvidence,
    bool FallbackRunPacketRequired,
    IReadOnlyList<string> RequiredRunPacketFields,
    IReadOnlyList<string> Boundaries,
    string RecommendedNextAction,
    int RegisteredActorCount,
    int LiveActorSessionCount,
    int NonLiveActorSessionCount,
    int StaleActorSessionCount,
    int ClosedActorSessionCount,
    int ActorSessionFreshnessWindowSeconds,
    DateTimeOffset ActorSessionLivenessCheckedAt,
    IReadOnlyList<string> StaleActorSessionIds,
    IReadOnlyList<string> ClosedActorSessionIds,
    IReadOnlyList<string> NonLiveActorStopCommands,
    string NonLiveActorSessionPolicy,
    bool GrantsExecutionAuthority,
    bool GrantsTruthWriteAuthority,
    bool CreatesTaskQueue,
    DateTimeOffset ProjectedAt);

public sealed record ActorSessionRegistrationResult(
    string SchemaVersion,
    bool DryRun,
    string Outcome,
    string ActorSessionId,
    ActorSessionKind Kind,
    string ActorIdentity,
    string RepoId,
    ActorSessionState State,
    string? ProviderProfile,
    string? CapabilityProfile,
    string? SessionScope,
    string? BudgetProfile,
    string? ScheduleBinding,
    string? LastContextReceipt,
    bool LeaseEligible,
    string? HealthPosture,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAt,
    ActorSessionRegistrationMode RegistrationMode,
    string? WorkerInstanceId,
    string? SupervisorLaunchTokenId,
    bool SupervisorLaunchVerified,
    string OperationClass,
    string Operation,
    string RoleBoundary,
    bool DecisionAuthority,
    bool ImplementationAuthority,
    bool HostDispatchRequired,
    bool TaskTruthWriteAllowed,
    bool StartsRun,
    bool IssuesLease,
    bool SameAgentRoleSwitchDetected,
    IReadOnlyList<string> RelatedActorSessionIds,
    string RoleSwitchEvidence,
    string ScheduleBindingState,
    string CallbackWakeAuthority,
    string ScheduleReplacementPolicy,
    bool ScheduleBindingGrantsExecutionAuthority,
    bool ScheduleBindingGrantsTruthWriteAuthority,
    string Reason,
    DateTimeOffset RegisteredAt);

public sealed record WorkerSupervisorLaunchRegistrationResult(
    string SchemaVersion,
    bool DryRun,
    string Outcome,
    string WorkerInstanceId,
    string RepoId,
    string WorkerIdentity,
    string? LaunchTokenId,
    string? LaunchToken,
    DateTimeOffset? LaunchTokenExpiresAt,
    bool LaunchTokenReturned,
    string LaunchTokenVisibility,
    string? HostSessionId,
    string? ActorSessionId,
    string? ProviderProfile,
    string? CapabilityProfile,
    string? ScheduleBinding,
    string ApiRegistrationCommand,
    string TextRegistrationCommand,
    string SupervisorCapabilityMode,
    string ProcessOwnershipStatus,
    bool HostProcessSpawned,
    bool HostProcessHandleHeld,
    bool StdIoCaptured,
    bool ImmediateDeathDetectionReady,
    bool RequiresWorkerProcessRegistration,
    string ProcessProofSource,
    bool StartsRun,
    bool IssuesLease,
    bool GrantsExecutionAuthority,
    bool GrantsTruthWriteAuthority,
    string Reason,
    DateTimeOffset RegisteredAt);

public sealed record WorkerSupervisorArchiveResult(
    string SchemaVersion,
    bool DryRun,
    string Outcome,
    bool Archived,
    string WorkerInstanceId,
    string RepoId,
    string WorkerIdentity,
    WorkerSupervisorInstanceState PreviousState,
    string? ActorSessionId,
    string? ScheduleBinding,
    string? ArchiveId,
    bool TombstoneProjected,
    string NextAction,
    string NextCommand,
    bool StartsRun,
    bool IssuesLease,
    bool GrantsExecutionAuthority,
    bool GrantsTruthWriteAuthority,
    string Reason,
    DateTimeOffset ArchivedAt);
