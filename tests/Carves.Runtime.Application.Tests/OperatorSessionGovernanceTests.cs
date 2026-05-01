using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class OperatorSessionGovernanceTests
{
    [Fact]
    public void ActorSessionService_PersistsStableOperatorAndAgentSessions()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var service = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);

        var operatorSession = service.Ensure(ActorSessionKind.Operator, "operator", "repo", "Operator attached.");
        var operatorSessionAgain = service.Ensure(ActorSessionKind.Operator, "operator", "repo", "Operator attached again.");
        var agentSession = service.Ensure(ActorSessionKind.Agent, "agent-cli", "repo", "Agent attached.");

        Assert.Equal(operatorSession.ActorSessionId, operatorSessionAgain.ActorSessionId);
        Assert.NotEqual(operatorSession.ActorSessionId, agentSession.ActorSessionId);
        Assert.Equal(2, service.List().Count);
        Assert.Contains(eventStream.Load(eventKind: OperatorOsEventKind.ActorSessionStarted), item => item.ActorIdentity == "operator");
        Assert.Contains(eventStream.Load(eventKind: OperatorOsEventKind.ActorSessionUpdated), item => item.ActorIdentity == "operator");
    }

    [Fact]
    public void ActorSessionService_RecordsSameAgentRoleSwitchEvidence()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var service = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);

        var planner = service.Register(
            ActorSessionKind.Planner,
            "same-agent",
            "repo",
            "Register planner identity.",
            dryRun: false);
        var worker = service.Register(
            ActorSessionKind.Worker,
            "same-agent",
            "repo",
            "Register worker identity.",
            dryRun: false);

        Assert.False(planner.SameAgentRoleSwitchDetected);
        Assert.Empty(planner.RelatedActorSessionIds);
        Assert.True(worker.SameAgentRoleSwitchDetected);
        Assert.Contains(planner.ActorSessionId, worker.RelatedActorSessionIds);
        Assert.Contains("Same-agent role switch evidence", worker.RoleSwitchEvidence, StringComparison.Ordinal);
        Assert.Contains("not permission for a role to cross its authority boundary", worker.RoleSwitchEvidence, StringComparison.Ordinal);
        Assert.Contains(eventStream.Load(eventKind: OperatorOsEventKind.ActorSessionUpdated), item =>
            item.ReasonCode == "same_agent_role_switch_evidence"
            && item.ActorKind == ActorSessionKind.Worker
            && item.ActorIdentity == "same-agent"
            && item.Summary.Contains("Same-agent role switch evidence", StringComparison.Ordinal)
            && item.Summary.Contains(planner.ActorSessionId, StringComparison.Ordinal));
    }

    [Fact]
    public void ActorSessionService_ProjectsScheduleBindingAsCallbackOnlyAuthority()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var service = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);

        var worker = service.Register(
            ActorSessionKind.Worker,
            "codex-cli-worker",
            "repo",
            "Register worker callback binding.",
            dryRun: false,
            scheduleBinding: "worker-evidence-ping",
            lastContextReceipt: "ctx-receipt-001");
        var planner = service.Register(
            ActorSessionKind.Planner,
            "planner-host",
            "repo",
            "Register planner without schedule binding.",
            dryRun: true);

        Assert.Equal("registered_projection_only", worker.ScheduleBindingState);
        Assert.Equal("host_mediated_worker_callback_projection_only", worker.CallbackWakeAuthority);
        Assert.Equal("replace_by_reregistering_actor_session_schedule_binding", worker.ScheduleReplacementPolicy);
        Assert.False(worker.ScheduleBindingGrantsExecutionAuthority);
        Assert.False(worker.ScheduleBindingGrantsTruthWriteAuthority);
        Assert.Equal("worker-evidence-ping", worker.ScheduleBinding);
        Assert.Equal("ctx-receipt-001", worker.LastContextReceipt);

        Assert.Equal("unbound_manual_or_fallback_wake", planner.ScheduleBindingState);
        Assert.Equal("host_manual_or_scheduler_wake_required_for_planner_reentry", planner.CallbackWakeAuthority);
        Assert.Equal("register_or_refresh_actor_session_with_schedule_binding_to_attach_callback", planner.ScheduleReplacementPolicy);
        Assert.False(planner.ScheduleBindingGrantsExecutionAuthority);
        Assert.False(planner.ScheduleBindingGrantsTruthWriteAuthority);
    }

    [Fact]
    public void ActorSessionService_DoesNotStopSupervisedActorWhenSupervisorStopFails()
    {
        var actorRepository = new InMemoryActorSessionRepository();
        var eventRepository = new InMemoryOperatorOsEventRepository();
        var eventStream = new OperatorOsEventStreamService(eventRepository);
        actorRepository.Save(new ActorSessionSnapshot
        {
            Entries =
            [
                new ActorSessionRecord
                {
                    ActorSessionId = "actor-worker-r2",
                    Kind = ActorSessionKind.Worker,
                    ActorIdentity = "phase-r2-supervised-worker",
                    RepoId = "repo-r2",
                    State = ActorSessionState.Active,
                    RegistrationMode = ActorSessionRegistrationMode.Supervised,
                    WorkerInstanceId = "worker-instance-r2",
                    SupervisorLaunchTokenId = "wlt-r2",
                    LastReason = "phase R2 active supervised worker",
                },
            ],
        });
        var supervisorRepository = new FailingWorkerSupervisorStateRepository(new WorkerSupervisorStateSnapshot
        {
            Entries =
            [
                new WorkerSupervisorInstanceRecord
                {
                    WorkerInstanceId = "worker-instance-r2",
                    RepoId = "repo-r2",
                    WorkerIdentity = "phase-r2-supervised-worker",
                    State = WorkerSupervisorInstanceState.Running,
                    ActorSessionId = "actor-worker-r2",
                    LaunchTokenId = "wlt-r2",
                    LaunchTokenHash = "sha256:r2",
                    LastReason = "phase R2 running supervisor",
                },
            ],
        });
        var service = new ActorSessionService(
            actorRepository,
            eventStream,
            new WorkerSupervisorStateService(supervisorRepository));

        var failure = Assert.Throws<InvalidOperationException>(() => service.Stop(
            "actor-worker-r2",
            dryRun: false,
            "phase R2 stop should fail before actor state changes"));

        var actorRecord = Assert.Single(actorRepository.Load().Entries);
        var supervisorRecord = Assert.Single(supervisorRepository.Load().Entries);
        Assert.Contains("simulated worker supervisor save failure", failure.Message, StringComparison.Ordinal);
        Assert.Equal(ActorSessionState.Active, actorRecord.State);
        Assert.Equal(WorkerSupervisorInstanceState.Running, supervisorRecord.State);
        Assert.DoesNotContain(
            eventStream.Load(eventKind: OperatorOsEventKind.ActorSessionUpdated),
            item => item.ReasonCode == "actor_session_stopped");
    }

    [Fact]
    public void ActorSessionService_RecordsSupervisorHandleAcquisitionAsOsEvent()
    {
        using var workspace = new TemporaryWorkspace();
        var actorRepository = new JsonActorSessionRepository(workspace.Paths);
        var eventRepository = new JsonOperatorOsEventRepository(workspace.Paths);
        var supervisorRepository = new JsonWorkerSupervisorStateRepository(workspace.Paths);
        var eventStream = new OperatorOsEventStreamService(eventRepository, workspace.Paths);
        var service = new ActorSessionService(
            actorRepository,
            eventStream,
            new WorkerSupervisorStateService(supervisorRepository));

        var launch = service.RegisterWorkerSupervisorLaunch(
            "repo-supervisor-r6",
            "codex-worker-supervised",
            "phase R6 pending supervised worker launch",
            dryRun: false,
            workerInstanceId: "worker-instance-r6",
            hostSessionId: "host-session-r6",
            actorSessionId: "actor-worker-r6",
            providerProfile: "codex-cli",
            capabilityProfile: "external-codex-worker",
            scheduleBinding: "worker-evidence-ping");
        var registration = service.Register(
            ActorSessionKind.Worker,
            "codex-worker-supervised",
            "repo-supervisor-r6",
            "phase R6 supervised worker registration",
            dryRun: false,
            actorSessionId: "actor-worker-r6",
            lastContextReceipt: "phase-r6-context",
            healthPosture: "healthy",
            processId: Environment.ProcessId,
            registrationMode: ActorSessionRegistrationMode.Supervised,
            workerInstanceId: launch.WorkerInstanceId,
            launchToken: launch.LaunchToken);

        var updated = service.RecordWorkerSupervisorHostProcessHandle(
            launch.WorkerInstanceId,
            "host-process-handle-r6",
            "host-session-r6",
            "phase R6 host process handle acquired",
            stdoutLogPath: "logs/worker-r6.stdout.log",
            stderrLogPath: "logs/worker-r6.stderr.log");
        var evt = Assert.Single(eventStream.Load(eventKind: OperatorOsEventKind.WorkerSupervisorHostProcessHandleAcquired));

        Assert.Equal("actor-worker-r6", registration.ActorSessionId);
        Assert.Equal("host-process-handle-r6", updated.HostProcessHandleId);
        Assert.Equal("host-session-r6", updated.HostProcessHandleOwnerSessionId);
        Assert.NotNull(updated.HostProcessHandleAcquiredAt);
        Assert.Equal("logs/worker-r6.stdout.log", updated.HostProcessStdoutLogPath);
        Assert.Equal("logs/worker-r6.stderr.log", updated.HostProcessStderrLogPath);
        Assert.Equal("repo-supervisor-r6", evt.RepoId);
        Assert.Equal("actor-worker-r6", evt.ActorSessionId);
        Assert.Equal(ActorSessionKind.Worker, evt.ActorKind);
        Assert.Equal("codex-worker-supervised", evt.ActorIdentity);
        Assert.Equal("worker-instance-r6", evt.ReferenceId);
        Assert.Equal("worker_supervisor_host_process_handle_acquired", evt.ReasonCode);
        Assert.Contains("acquired a Host process handle", evt.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("host-process-handle-r6", evt.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorSessionLiveness_RequiresProcessStartIdentityForProcessAlive()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var service = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);

        var worker = service.Register(
            ActorSessionKind.Worker,
            "process-tracked-worker",
            "repo",
            "Register process tracked worker.",
            dryRun: false,
            scheduleBinding: "worker-evidence-ping",
            lastContextReceipt: "ctx-receipt-001",
            processId: Environment.ProcessId);
        var session = service.List().Single(item => item.ActorSessionId == worker.ActorSessionId);

        Assert.Equal(Environment.ProcessId, worker.ProcessId);
        Assert.NotNull(worker.ProcessStartedAt);
        Assert.Equal(worker.ProcessStartedAt, session.ProcessStartedAt);
        Assert.Equal("process_alive", ActorSessionLivenessRules.ResolveProcessTrackingStatus(session));

        session.ProcessStartedAt = DateTimeOffset.UnixEpoch;
        Assert.Equal("process_mismatch", ActorSessionLivenessRules.ResolveProcessTrackingStatus(session));
        Assert.True(ActorSessionLivenessRules.IsClosed(session));

        session.ProcessStartedAt = null;
        Assert.Equal("process_identity_unverified", ActorSessionLivenessRules.ResolveProcessTrackingStatus(session));
        Assert.False(ActorSessionLivenessRules.IsClosed(session));
    }

    [Fact]
    public void ActorSessionService_ProjectsSameAgentFallbackPolicyWithoutAuthority()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var service = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);

        var fallback = service.BuildFallbackPolicy("repo");

        Assert.False(fallback.ExplicitPlannerRegistered);
        Assert.False(fallback.ExplicitWorkerRegistered);
        Assert.True(fallback.FallbackAllowed);
        Assert.Equal("same_agent_split_role_fallback_allowed_with_evidence", fallback.FallbackMode);
        Assert.Contains("planner", fallback.MissingActorRoles);
        Assert.Contains("worker", fallback.MissingActorRoles);
        Assert.Contains("same_agent_role_switch_evidence", fallback.RequiredEvidence);
        Assert.Contains("fallback_run_packet", fallback.RequiredEvidence);
        Assert.True(fallback.FallbackRunPacketRequired);
        Assert.Equal(["role_switch_receipt", "context_receipt", "execution_claim", "review_bundle"], fallback.RequiredRunPacketFields);
        Assert.Contains("no_second_task_queue", fallback.Boundaries);
        Assert.False(fallback.GrantsExecutionAuthority);
        Assert.False(fallback.GrantsTruthWriteAuthority);
        Assert.False(fallback.CreatesTaskQueue);

        service.Register(ActorSessionKind.Planner, "planner-host", "repo", "Register explicit planner.", dryRun: false);
        service.Register(ActorSessionKind.Worker, "codex-cli-worker", "repo", "Register explicit worker.", dryRun: false);

        var ready = service.BuildFallbackPolicy("repo");

        Assert.True(ready.ExplicitPlannerRegistered);
        Assert.True(ready.ExplicitWorkerRegistered);
        Assert.False(ready.FallbackAllowed);
        Assert.Equal("explicit_actor_sessions_ready", ready.FallbackMode);
        Assert.Empty(ready.MissingActorRoles);
        Assert.False(ready.GrantsExecutionAuthority);
        Assert.False(ready.GrantsTruthWriteAuthority);
        Assert.False(ready.CreatesTaskQueue);
    }

    [Fact]
    public void ActorSessionService_FallbackPolicyPrioritizesNonLiveCleanupEvenWhenRolesAreReady()
    {
        var repository = new InMemoryActorSessionRepository();
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var service = new ActorSessionService(repository, eventStream);

        service.Register(ActorSessionKind.Planner, "planner-host", "repo", "Register explicit planner.", dryRun: false);
        service.Register(ActorSessionKind.Worker, "codex-cli-worker", "repo", "Register explicit worker.", dryRun: false);
        service.Register(
            ActorSessionKind.Worker,
            "stale-codex-cli-worker",
            "repo",
            "Register stale worker residue.",
            dryRun: false,
            actorSessionId: "actor-worker-stale-extra",
            scheduleBinding: "worker-evidence-ping",
            healthPosture: "healthy");
        var snapshot = repository.Load();
        var staleWorker = snapshot.Entries.Single(item => item.ActorSessionId == "actor-worker-stale-extra");
        staleWorker.LastSeenAt = DateTimeOffset.UtcNow.Subtract(ActorSessionLivenessRules.FreshnessWindow).AddMinutes(-1);
        staleWorker.UpdatedAt = staleWorker.LastSeenAt;
        repository.Save(snapshot);

        var readyWithResidue = service.BuildFallbackPolicy("repo");

        Assert.True(readyWithResidue.ExplicitPlannerRegistered);
        Assert.True(readyWithResidue.ExplicitWorkerRegistered);
        Assert.False(readyWithResidue.FallbackAllowed);
        Assert.Equal("explicit_actor_sessions_ready", readyWithResidue.FallbackMode);
        Assert.Equal(1, readyWithResidue.StaleActorSessionCount);
        Assert.Equal(["actor-worker-stale-extra"], readyWithResidue.StaleActorSessionIds);
        Assert.Equal(["api actor-session-stop --actor-session-id actor-worker-stale-extra --reason actor-thread-not-live"], readyWithResidue.NonLiveActorStopCommands);
        Assert.Contains("Stop or refresh non-live actor session", readyWithResidue.RecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains("actor-worker-stale-extra", readyWithResidue.RecommendedNextAction, StringComparison.Ordinal);
        Assert.DoesNotContain("same-agent fallback is not needed", readyWithResidue.RecommendedNextAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SessionOwnershipService_RestrictsWorkerInterruptionToOperator()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessions = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);
        var ownership = new SessionOwnershipService(new InMemoryOwnershipRepository(), actorSessions, eventStream);
        var operatorSession = actorSessions.Ensure(ActorSessionKind.Operator, "operator", "repo", "Operator ready.");
        var agentSession = actorSessions.Ensure(ActorSessionKind.Agent, "agent-cli", "repo", "Agent ready.");

        var granted = ownership.Claim(operatorSession, OwnershipScope.WorkerInterruption, "lease-1", "Interrupt worker lease.");
        var denied = ownership.Claim(agentSession, OwnershipScope.WorkerInterruption, "lease-2", "Agent tried to interrupt worker lease.");

        Assert.True(granted.Allowed);
        Assert.Equal(OwnershipDecisionOutcome.Granted, granted.Outcome);
        Assert.False(denied.Allowed);
        Assert.Equal(OwnershipDecisionOutcome.Denied, denied.Outcome);
        Assert.Equal("ownership_scope_not_permitted", denied.ReasonCode);
    }

    [Fact]
    public void SessionOwnershipService_ReportsCurrentlyOccupiedOwnerOnTaskMutationConflict()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessions = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);
        var ownership = new SessionOwnershipService(new InMemoryOwnershipRepository(), actorSessions, eventStream);
        var operatorSession = actorSessions.Ensure(ActorSessionKind.Operator, "operator", "repo", "Operator ready.");
        var agentSession = actorSessions.Ensure(ActorSessionKind.Agent, "agent-cli", "repo", "Agent ready.");

        var granted = ownership.Claim(operatorSession, OwnershipScope.TaskMutation, "task-1", "Operator claimed task.");
        var denied = ownership.Claim(agentSession, OwnershipScope.TaskMutation, "task-1", "Agent tried to claim the same task.");

        Assert.True(granted.Allowed);
        Assert.False(denied.Allowed);
        Assert.Equal(OwnershipDecisionOutcome.Deferred, denied.Outcome);
        Assert.Equal("ownership_conflict_deferred", denied.ReasonCode);
        Assert.Contains("currently occupied", denied.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(operatorSession.ActorIdentity, denied.ExistingOwnerIdentity);
    }

    [Fact]
    public void SessionOwnershipService_DeniesPlannerTaskMutationOwnership()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessions = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);
        var ownership = new SessionOwnershipService(new InMemoryOwnershipRepository(), actorSessions, eventStream);
        var plannerSession = actorSessions.Ensure(ActorSessionKind.Planner, "planner-host", "repo", "Planner active.");

        var denied = ownership.Claim(plannerSession, OwnershipScope.TaskMutation, "task-1", "Planner tried to execute a task.");

        Assert.False(denied.Allowed);
        Assert.Equal(OwnershipDecisionOutcome.Denied, denied.Outcome);
        Assert.Equal("ownership_scope_not_permitted", denied.ReasonCode);
        Assert.Contains("Planner session", denied.Summary, StringComparison.Ordinal);
        Assert.Contains("may not own TaskMutation", denied.Summary, StringComparison.Ordinal);
        Assert.Empty(ownership.List(OwnershipScope.TaskMutation));
    }

    [Fact]
    public void SessionOwnershipService_DeniesWorkerApprovalAndRuntimeControlOwnership()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessions = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);
        var ownership = new SessionOwnershipService(new InMemoryOwnershipRepository(), actorSessions, eventStream);
        var workerSession = actorSessions.Ensure(ActorSessionKind.Worker, "codex-cli-worker", "repo", "Worker attached.");

        var approvalDenied = ownership.Claim(workerSession, OwnershipScope.ApprovalDecision, "task-1", "Worker tried to approve review.");
        var runtimeDenied = ownership.Claim(workerSession, OwnershipScope.RuntimeControl, "worker-selection-policy", "Worker tried to mutate runtime policy.");
        var taskMutationGranted = ownership.Claim(workerSession, OwnershipScope.TaskMutation, "task-1", "Worker claimed execution mutation.");

        Assert.False(approvalDenied.Allowed);
        Assert.Equal(OwnershipDecisionOutcome.Denied, approvalDenied.Outcome);
        Assert.Equal("ownership_scope_not_permitted", approvalDenied.ReasonCode);
        Assert.Contains("Worker session", approvalDenied.Summary, StringComparison.Ordinal);
        Assert.Contains("may not own ApprovalDecision", approvalDenied.Summary, StringComparison.Ordinal);

        Assert.False(runtimeDenied.Allowed);
        Assert.Equal(OwnershipDecisionOutcome.Denied, runtimeDenied.Outcome);
        Assert.Equal("ownership_scope_not_permitted", runtimeDenied.ReasonCode);

        Assert.True(taskMutationGranted.Allowed);
        Assert.Equal(OwnershipDecisionOutcome.Granted, taskMutationGranted.Outcome);
    }

    [Fact]
    public void ActorReviewWritebackPolicyService_DeniesWorkerReviewAndWritebackOperations()
    {
        var policy = new ActorReviewWritebackPolicyService();
        var operations = new[] { "review_task_complete", "approve_review", "reject_review", "reopen_review" };

        foreach (var operation in operations)
        {
            var decision = policy.Evaluate(ActorSessionKind.Worker, "codex-cli-worker", operation, "T-1");

            Assert.False(decision.Allowed);
            Assert.Equal("worker_review_writeback_denied", decision.ReasonCode);
            Assert.Contains("execution-only", decision.Summary, StringComparison.Ordinal);
            Assert.Contains(operation, decision.Summary, StringComparison.Ordinal);
            Assert.Contains("submit_execution_result", decision.AllowedWorkerOperations);
            Assert.Contains("submit_completion_claim", decision.AllowedWorkerOperations);
        }
    }

    [Fact]
    public void ConcurrentActorArbitrationService_EscalatesPlannerVsOperatorConflict()
    {
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessions = new ActorSessionService(new InMemoryActorSessionRepository(), eventStream);
        var ownership = new SessionOwnershipService(new InMemoryOwnershipRepository(), actorSessions, eventStream);
        var arbitration = new ConcurrentActorArbitrationService(
            ownership,
            new RuntimeIncidentTimelineService(new InMemoryRuntimeIncidentTimelineRepository(), eventStream),
            eventStream);
        var plannerSession = actorSessions.Ensure(ActorSessionKind.Planner, "planner-host", "repo", "Planner active.");
        var operatorSession = actorSessions.Ensure(ActorSessionKind.Operator, "operator", "repo", "Operator active.");

        var plannerClaim = ownership.Claim(plannerSession, OwnershipScope.PlannerControl, "planner", "Planner controls planner lifecycle.");
        var conflict = arbitration.Resolve(operatorSession, OwnershipScope.PlannerControl, "planner", "Operator attempted conflicting planner control.");

        Assert.True(plannerClaim.Allowed);
        Assert.Equal(ActorArbitrationOutcome.Escalated, conflict.Outcome);
        Assert.Equal(plannerSession.ActorSessionId, conflict.CurrentOwnerActorSessionId);
        Assert.Contains("currently occupied", conflict.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("poll", conflict.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(eventStream.Load(eventKind: OperatorOsEventKind.ArbitrationResolved), item => item.ReferenceId == conflict.ArbitrationId);
    }

    [Fact]
    public void OperatorOsEventStreamService_PersistsMixedTaskPermissionAndIncidentEvents()
    {
        var service = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());

        service.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.TaskStarted,
            RepoId = "repo",
            ActorSessionId = "actor-worker",
            ActorKind = ActorSessionKind.Worker,
            ActorIdentity = "codex-sdk",
            TaskId = "T-1",
            Summary = "Task started.",
            ReasonCode = "task_started",
        });
        service.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.PermissionRequested,
            RepoId = "repo",
            ActorSessionId = "actor-worker",
            ActorKind = ActorSessionKind.Worker,
            ActorIdentity = "codex-sdk",
            TaskId = "T-1",
            PermissionRequestId = "perm-1",
            Summary = "Permission requested.",
            ReasonCode = "permission_requested",
        });
        service.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.IncidentDetected,
            RepoId = "repo",
            ActorSessionId = "actor-operator",
            ActorKind = ActorSessionKind.Operator,
            ActorIdentity = "operator",
            TaskId = "T-1",
            Summary = "Incident detected.",
            ReasonCode = "incident_detected",
        });

        var taskEvents = service.Load(taskId: "T-1");
        var permissionEvents = service.Load(eventKind: OperatorOsEventKind.PermissionRequested);

        Assert.Equal(3, taskEvents.Count);
        Assert.Single(permissionEvents);
        Assert.Equal("perm-1", permissionEvents[0].PermissionRequestId);
    }
}

internal sealed class FailingWorkerSupervisorStateRepository : IWorkerSupervisorStateRepository
{
    private readonly WorkerSupervisorStateSnapshot snapshot;

    public FailingWorkerSupervisorStateRepository(WorkerSupervisorStateSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }

    public WorkerSupervisorStateSnapshot Load()
    {
        return new WorkerSupervisorStateSnapshot
        {
            Entries = snapshot.Entries.Select(Clone).ToArray(),
            UpdatedAt = snapshot.UpdatedAt,
        };
    }

    public void Save(WorkerSupervisorStateSnapshot snapshot)
    {
        throw new InvalidOperationException("simulated worker supervisor save failure");
    }

    private static WorkerSupervisorInstanceRecord Clone(WorkerSupervisorInstanceRecord record)
    {
        return new WorkerSupervisorInstanceRecord
        {
            WorkerInstanceId = record.WorkerInstanceId,
            RepoId = record.RepoId,
            WorkerIdentity = record.WorkerIdentity,
            OwnershipMode = record.OwnershipMode,
            State = record.State,
            HostSessionId = record.HostSessionId,
            ActorSessionId = record.ActorSessionId,
            ProviderProfile = record.ProviderProfile,
            CapabilityProfile = record.CapabilityProfile,
            ScheduleBinding = record.ScheduleBinding,
            ProcessId = record.ProcessId,
            ProcessStartedAt = record.ProcessStartedAt,
            LaunchTokenId = record.LaunchTokenId,
            LaunchTokenHash = record.LaunchTokenHash,
            LaunchTokenIssuedAt = record.LaunchTokenIssuedAt,
            LaunchTokenExpiresAt = record.LaunchTokenExpiresAt,
            LastHeartbeatAt = record.LastHeartbeatAt,
            LastReason = record.LastReason,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
        };
    }
}
