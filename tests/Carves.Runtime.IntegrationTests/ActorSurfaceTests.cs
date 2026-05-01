using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;
using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class ActorSurfaceTests
{
    [Fact]
    public void HostRoutedActorSessionsAndEventsExposeMultiActorTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "planner", "wake", "explicit_human_wake", "integration wake");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "status");

            var sessions = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions");
            var events = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "os-events");
            var actorInspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "actor", "sessions");

            Assert.Equal(0, sessions.ExitCode);
            Assert.Contains("\"kind\": \"operator\"", sessions.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"kind\": \"agent\"", sessions.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"kind\": \"planner\"", sessions.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, events.ExitCode);
            Assert.Contains("ownership_claimed", events.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("arbitration_resolved", events.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, actorInspect.ExitCode);
            Assert.Contains("Actor sessions:", actorInspect.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorSessions_TextSurfaceShowsTruncationAndRepoFilter()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteManyActorSessionsForTruncation(sandbox.RootPath);

        var truncated = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "actor",
            "sessions",
            "--kind",
            "worker",
            "--repo-id",
            "repo-truncation");
        var filteredText = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "actor",
            "sessions",
            "--repo-id",
            "repo-other");
        var filteredApi = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "api",
            "actor-sessions",
            "--repo-id",
            "repo-other");

        Assert.Equal(0, truncated.ExitCode);
        Assert.Contains("Actor sessions: total=52; rendered=50; hidden=2", truncated.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Actor session filters: kind=Worker; repo=repo-truncation", truncated.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Actor session liveness: live=51 stale=0 closed=1 non_live=1", truncated.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Actor session process tracking: process_alive=0 process_missing=0 process_mismatch=0 process_identity_unverified=0 heartbeat_only=52", truncated.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Actor session output truncated: rendered=50; hidden=2", truncated.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Hidden non-live actor sessions: 1", truncated.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("actor-worker-truncation-999-closed", truncated.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, filteredText.ExitCode);
        Assert.Contains("Actor sessions: total=1; rendered=1; hidden=0", filteredText.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("repo=repo-other", filteredText.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("actor-worker-other-001", filteredText.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("actor-worker-truncation-000", filteredText.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, filteredApi.ExitCode);
        Assert.Contains("\"repo_id\": \"repo-other\"", filteredApi.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"actor_session_id\": \"actor-worker-other-001\"", filteredApi.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("actor-worker-truncation-000", filteredApi.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void HostCycle_StopsNonLiveActorSessionDuringReconciliation()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteHostLivenessActorSessionResidue(sandbox.RootPath);

        var host = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "--cycles", "1", "--interval-ms", "0", "--slots", "1");
        var sessions = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "actor",
            "sessions",
            "--kind",
            "worker",
            "--repo-id",
            "repo-reconcile");
        var events = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "api",
            "os-events",
            "--actor-session-id",
            "actor-worker-reconcile-stale");

        Assert.Equal(0, host.ExitCode);
        Assert.Contains("Actor session liveness reconciliation:", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stale=1", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("non_live=1", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("actor-worker-reconcile-stale", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=heartbeat_stale", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cleanup_action=stopped", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(
            "next_action=api actor-session-stop --actor-session-id actor-worker-reconcile-stale --reason actor-thread-not-live",
            host.StandardOutput,
            StringComparison.Ordinal);

        Assert.Equal(0, sessions.ExitCode);
        Assert.Contains("actor-worker-reconcile-stale [Worker/Stopped]", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("liveness: closed", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=state_stopped", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("last_reason: Host liveness reconciliation stopped a non-live actor session.", sessions.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, events.ExitCode);
        Assert.Contains("\"event_kind\": \"actor_session_liveness_reconciled\"", events.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"reason_code\": \"actor_session_liveness_reconciled\"", events.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"actor_session_id\": \"actor-worker-reconcile-stale\"", events.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Cleanup action: stopped", events.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"reason_code\": \"actor_session_stopped\"", events.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void HostCycle_DryRunReportsNonLiveActorSessionWithoutStopping()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteHostLivenessActorSessionResidue(sandbox.RootPath);

        var host = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "--dry-run", "--cycles", "1", "--interval-ms", "0", "--slots", "1");
        var sessions = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "actor",
            "sessions",
            "--kind",
            "worker",
            "--repo-id",
            "repo-reconcile");

        Assert.Equal(0, host.ExitCode);
        Assert.Contains("Actor session liveness reconciliation:", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cleanup_action=dry_run_stop_projected", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=heartbeat_stale", host.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, sessions.ExitCode);
        Assert.Contains("actor-worker-reconcile-stale [Worker/Active]", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("liveness: stale", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=heartbeat_stale", sessions.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void HostCycle_StopsDeadProcessActorSessionImmediately()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteHostDeadProcessActorSessionResidue(sandbox.RootPath);

        var host = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "--cycles", "1", "--interval-ms", "0", "--slots", "1");
        var sessions = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "actor",
            "sessions",
            "--kind",
            "worker",
            "--repo-id",
            "repo-process-reconcile");

        Assert.Equal(0, host.ExitCode);
        Assert.Contains("Actor session liveness reconciliation:", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("closed=1", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stale=0", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("actor-worker-reconcile-dead-process", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("liveness=closed", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=process_missing", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cleanup_action=stopped", host.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, sessions.ExitCode);
        Assert.Contains("actor-worker-reconcile-dead-process [Worker/Stopped]", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("process_id: 2147483647", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Actor session process tracking: process_alive=0 process_missing=1 process_mismatch=0 process_identity_unverified=0 heartbeat_only=0", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("process_tracking: process_missing", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("liveness: closed", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=state_stopped", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("actor-worker-reconcile-dead-process", sessions.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void HostCycle_MarksBoundSupervisedWorkerLostDuringReconciliation()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var launch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-s5",
                "--identity",
                "phase-s5-supervised-worker",
                "--worker-instance-id",
                "worker-instance-s5",
                "--actor-session-id",
                "actor-worker-s5",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--schedule-binding",
                "worker-evidence-ping",
                "--reason",
                "phase S5 issue supervised launch token");
            var launchRoot = ParseFirstJsonObject(launch.StandardOutput);
            var launchToken = launchRoot["launch_token"]!.GetValue<string>();
            var registration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-s5-supervised-worker",
                "--repo-id",
                "repo-supervisor-s5",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-s5",
                "--launch-token",
                launchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-s5-context",
                "--health",
                "healthy",
                "--reason",
                "phase S5 supervised worker registration");
            RewriteActorSessionProcessStartedAt(
                sandbox.RootPath,
                "actor-worker-s5",
                "2000-01-01T00:00:00.0000000+00:00");

            var host = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "--cycles", "1", "--interval-ms", "0", "--slots", "1");
            var reregisterLost = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-s5-supervised-worker",
                "--repo-id",
                "repo-supervisor-s5",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-s5",
                "--launch-token",
                launchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-s5-reregister-context",
                "--health",
                "healthy",
                "--reason",
                "phase S6 lost supervisor must not be reregistered with old token");
            var instances = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-s5");
            var sessions = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "actor",
                "sessions",
                "--kind",
                "worker",
                "--repo-id",
                "repo-supervisor-s5");
            var events = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "os-events",
                "--actor-session-id",
                "actor-worker-s5");

            Assert.Equal(0, launch.ExitCode);
            Assert.Equal(0, registration.ExitCode);
            Assert.Equal(0, host.ExitCode);
            Assert.Contains("Actor session liveness reconciliation:", host.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("actor-worker-s5", host.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("reason=process_mismatch", host.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Bound worker supervisor reconciled: worker_instance=worker-instance-s5", host.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("cleanup_action=marked_lost", host.StandardOutput, StringComparison.Ordinal);
            Assert.NotEqual(0, reregisterLost.ExitCode);
            Assert.Contains(
                "Worker supervisor instance 'worker-instance-s5' is not registrable because it is Lost.",
                reregisterLost.CombinedOutput,
                StringComparison.Ordinal);

            Assert.Equal(0, instances.ExitCode);
            var instancesRoot = ParseFirstJsonObject(instances.StandardOutput);
            Assert.Equal("registration_ready_process_supervision_not_ready", instancesRoot["supervisor_readiness_posture"]!.GetValue<string>());
            Assert.False(instancesRoot["host_process_supervision_ready"]!.GetValue<bool>());
            Assert.False(instancesRoot["immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.Equal(4, instancesRoot["supervisor_automation_gap_count"]!.GetValue<int>());
            var instanceSurfaceGaps = instancesRoot["supervisor_automation_gaps"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .ToArray();
            Assert.Contains("host_process_spawn_not_implemented", instanceSurfaceGaps);
            Assert.Contains("host_process_handle_not_held", instanceSurfaceGaps);
            Assert.Contains("stdio_capture_not_available", instanceSurfaceGaps);
            Assert.Contains("immediate_death_detection_not_available", instanceSurfaceGaps);
            var entry = Assert.IsType<JsonObject>(Assert.Single(instancesRoot["entries"]!.AsArray()));
            Assert.Equal("worker-instance-s5", entry["worker_instance_id"]!.GetValue<string>());
            Assert.Equal("lost", entry["state"]!.GetValue<string>());
            Assert.Equal("actor-worker-s5", entry["actor_session_id"]!.GetValue<string>());
            Assert.False(entry["registration_allowed"]!.GetValue<bool>());
            Assert.True(entry["recovery_required"]!.GetValue<bool>());
            Assert.Equal("supervised_worker_lost_relaunch_required", entry["lifecycle_status"]!.GetValue<string>());
            Assert.Equal("registration_only_launch_token", entry["supervisor_capability_mode"]!.GetValue<string>());
            Assert.Equal("worker_process_lost_no_host_handle", entry["process_ownership_status"]!.GetValue<string>());
            Assert.False(entry["host_process_spawned"]!.GetValue<bool>());
            Assert.False(entry["host_process_handle_held"]!.GetValue<bool>());
            Assert.False(entry["immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.False(entry["requires_worker_process_registration"]!.GetValue<bool>());
            Assert.Equal("actor_session_process_id_and_start_time", entry["process_proof_source"]!.GetValue<string>());
            Assert.Equal(4, entry["supervisor_automation_gap_count"]!.GetValue<int>());
            var entryAutomationGaps = entry["supervisor_automation_gaps"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .ToArray();
            Assert.Contains("host_process_spawn_not_implemented", entryAutomationGaps);
            Assert.Contains("host_process_handle_not_held", entryAutomationGaps);
            Assert.Contains("stdio_capture_not_available", entryAutomationGaps);
            Assert.Contains("immediate_death_detection_not_available", entryAutomationGaps);
            Assert.Contains("Do not reuse this supervisor instance", entry["next_action"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("api worker-supervisor-launch", entry["next_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("--worker-instance-id <new-worker-instance-id>", entry["next_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("Host liveness reconciliation marked supervised worker lost", entry["last_reason"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, instances.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, sessions.ExitCode);
            Assert.Contains("actor-worker-s5 [Worker/Stopped]", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("process_tracking: process_mismatch", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("liveness: closed", sessions.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, events.ExitCode);
            Assert.Contains("\"reason_code\": \"worker_supervisor_instance_lost\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"event_kind\": \"worker_supervisor_lost\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"reason_code\": \"worker_supervisor_lost\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("worker-instance-s5", events.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostCycle_StopsProcessIdentityUnverifiedScheduleWorker()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteProcessIdentityUnverifiedScheduleWorkerSession(sandbox.RootPath);

        var host = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "--cycles", "1", "--interval-ms", "0", "--slots", "1");
        var sessions = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "actor",
            "sessions",
            "--kind",
            "worker",
            "--repo-id",
            "repo-worker-automation-identity-unverified");
        var events = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "api",
            "os-events",
            "--actor-session-id",
            "actor-worker-process-identity-unverified");

        Assert.Equal(0, host.ExitCode);
        Assert.Contains("Actor session liveness reconciliation:", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("process_identity_unverified_schedule_bound=1", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Schedule-bound actor session lacks process identity proof", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("actor-worker-process-identity-unverified", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("liveness=identity_unverified", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=process_identity_unverified", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cleanup_action=stopped", host.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, sessions.ExitCode);
        Assert.Contains("actor-worker-process-identity-unverified [Worker/Stopped]", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains($"process_id: {Environment.ProcessId}", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("process_started_at: (none)", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("process_tracking: process_identity_unverified", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("liveness: closed", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=state_stopped", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("last_reason: Host process identity reconciliation stopped an unverified schedule-bound actor session.", sessions.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, events.ExitCode);
        Assert.Contains("\"reason_code\": \"actor_session_process_identity_reconciled\"", events.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Cleanup action: stopped", events.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"reason_code\": \"actor_session_stopped\"", events.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void HostCycle_StopsHeartbeatOnlyScheduleWorker()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteHeartbeatOnlyScheduleWorkerSession(sandbox.RootPath);

        var host = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "--cycles", "1", "--interval-ms", "0", "--slots", "1");
        var sessions = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "actor",
            "sessions",
            "--kind",
            "worker",
            "--repo-id",
            "repo-worker-automation-heartbeat-reconcile");
        var events = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "api",
            "os-events",
            "--actor-session-id",
            "actor-worker-heartbeat-only-reconcile");

        Assert.Equal(0, host.ExitCode);
        Assert.Contains("Actor session liveness reconciliation:", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("heartbeat_only_schedule_bound=1", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Schedule-bound actor session lacks process tracking proof", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("actor-worker-heartbeat-only-reconcile", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("liveness=heartbeat_only", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=heartbeat_only", host.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cleanup_action=stopped", host.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, sessions.ExitCode);
        Assert.Contains("actor-worker-heartbeat-only-reconcile [Worker/Stopped]", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("process_id: (none)", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("process_started_at: (none)", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("process_tracking: heartbeat_only", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("liveness: closed", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=state_stopped", sessions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("last_reason: Host process tracking reconciliation stopped a heartbeat-only schedule-bound actor session.", sessions.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, events.ExitCode);
        Assert.Contains("\"reason_code\": \"actor_session_process_tracking_reconciled\"", events.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Cleanup action: stopped", events.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"reason_code\": \"actor_session_stopped\"", events.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentGateway_CanQueryActorAndEventSurfaces()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase11-codex-cli-worker",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--scope",
                "host-dispatch-only",
                "--schedule-binding",
                "worker-evidence-ping",
                "--context-receipt",
                "phase11-context-receipt",
                "--health",
                "healthy",
                "--reason",
                "phase11-worker-projection");

            var actors = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "actors");
            var fallbackPolicy = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "actor-fallback-policy");
            var roleReadiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "actor-role-readiness");
            var automationReadiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "worker-automation-readiness");
            var automationReadinessApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "worker-automation-readiness");
            var gatewayTrace = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "trace");
            var gatewayTraceJson = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "trace", "--json");
            var gatewayTraceWatch = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "trace", "--watch", "--iterations", "1", "--interval-ms", "0");
            var actorGatewayTrace = ProgramHarness.Run("--repo-root", sandbox.RootPath, "actor", "agent-trace");
            var gatewayTraceApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "agent-gateway-trace");
            var events = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "events");

            Assert.Equal(0, actors.ExitCode);
            Assert.Contains("\"outcome\": \"query_actors\"", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"actor_sessions\"", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("phase11-codex-cli-worker", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"provider_profile\": \"codex-cli\"", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"capability_profile\": \"external-codex-worker\"", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"session_scope\": \"host-dispatch-only\"", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"schedule_binding\": \"worker-evidence-ping\"", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"last_context_receipt\": \"phase11-context-receipt\"", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"lease_eligible\": true", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"health_posture\": \"healthy\"", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"role_boundary\": \"worker_execution_only_host_dispatch_required\"", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"decision_authority\": false", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"implementation_authority\": true", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"host_dispatch_required\": true", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"task_truth_write_allowed\": false", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"starts_run\": false", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"issues_lease\": false", actors.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, fallbackPolicy.ExitCode);
            Assert.Contains("\"outcome\": \"query_actor_fallback_policy\"", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"actor_session_fallback_policy\"", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"fallback_mode\": \"same_agent_split_role_fallback_allowed_with_evidence\"", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"grants_execution_authority\": false", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"grants_truth_write_authority\": false", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"creates_task_queue\": false", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, roleReadiness.ExitCode);
            Assert.Contains("\"outcome\": \"query_actor_role_readiness\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"actor_role_binding_readiness\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"readiness_type\": \"role_binding_registration_readiness\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"readiness_claim\": \"registered_actor_roles_only\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"readiness_scope\": \"repo_registered_actor_sessions\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"not_worker_automation_readiness\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"automation_readiness_status\": \"not_evaluated\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"host_health_checked\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"dispatchable_task_checked\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_runtime_availability_checked\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"review_gate_checked\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_planner_registered\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_worker_registered\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"fallback_allowed\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"fallback_mode\": \"same_agent_split_role_fallback_allowed_with_evidence\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"missing_actor_roles\": [", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"planner\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("phase11-codex-cli-worker", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"schedule_bound_worker_session_ids\": [", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"process_tracked_schedule_bound_worker_session_ids\": []", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"process_unverified_schedule_bound_worker_session_ids\": [", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"schedule_bound_worker_process_tracking_ready\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"context_receipted_worker_session_ids\": [", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_role_binding_ready\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_role_binding_ready_policy\": \"planner_and_worker_actor_sessions_registered_and_live; does_not_prove_worker_automation_dispatch_readiness\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_dispatch_ready\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_dispatch_ready_deprecated\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_dispatch_ready_replaced_by\": \"explicit_role_binding_ready\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_dispatch_ready_semantics\": \"deprecated_alias_for_role_binding_only_not_worker_automation_dispatch\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_callback_registration_present\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_callback_projection_ready\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_callback_projection_policy\": \"schedule_bound_worker_requires_process_alive_before_callback_projection_is_ready\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Re-register schedule-bound worker session(s) with process-id and process start proof", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"grants_execution_authority\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"grants_truth_write_authority\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"creates_task_queue\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"agent_gateway_query_only\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("This readiness surface only reports role-binding registration posture", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, automationReadiness.ExitCode);
            Assert.Contains("\"outcome\": \"query_worker_automation_readiness\"", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"worker_automation_readiness\"", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"readiness_type\": \"worker_automation_runtime_readiness\"", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"readiness_claim\": \"host_dispatch_runtime_readiness_only\"", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"not_role_binding_readiness\": true", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"role_binding_surface_ref\": \"agent query actor-role-readiness\"", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"automation_can_run_now\": false", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"run_eligibility\": {", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"worker_automation_run_eligibility\"", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"decision_basis\": \"host_health_and_dispatchable_task_and_worker_runtime_and_review_gate\"", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"host_ready\": true", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"dispatchable_task_ready\": false", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"run_not_started\": true", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"lease_not_issued\": true", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"truth_writeback_not_allowed\": true", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"role_binding_readiness\": {", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"status\": \"not_evaluated\"", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"host_health\": {", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"dispatchable_task\": {", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_runtime\": {", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"review_gate\": {", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"checked\": true", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"dispatch_not_available\"", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"grants_execution_authority\": false", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"grants_truth_write_authority\": false", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"creates_task_queue\": false", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"starts_run\": false", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"issues_lease\": false", automationReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, automationReadinessApi.ExitCode);
            Assert.Contains("\"kind\": \"worker_automation_readiness\"", automationReadinessApi.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, gatewayTrace.ExitCode);
            Assert.Contains("Agent gateway trace:", gatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Requests:", gatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Responses:", gatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Request [", gatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Response [", gatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("request_id=", gatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("agent_gateway_request_received", gatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("agent_gateway_response_accepted", gatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("query:actor-fallback-policy", gatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, gatewayTraceJson.ExitCode);
            Assert.Contains("\"kind\": \"agent_gateway_trace\"", gatewayTraceJson.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"event_kind\": \"AgentGatewayRequestReceived\"", gatewayTraceJson.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, gatewayTraceWatch.ExitCode);
            Assert.Contains("CARVES Agent Gateway Trace Watch 1/1", gatewayTraceWatch.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Agent gateway trace:", gatewayTraceWatch.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Requests:", gatewayTraceWatch.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Responses:", gatewayTraceWatch.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("request_id=", gatewayTraceWatch.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("query:actor-fallback-policy", gatewayTraceWatch.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, actorGatewayTrace.ExitCode);
            Assert.Contains("Agent gateway trace:", actorGatewayTrace.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, gatewayTraceApi.ExitCode);
            Assert.Contains("\"kind\": \"agent_gateway_trace\"", gatewayTraceApi.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, events.ExitCode);
            Assert.Contains("\"outcome\": \"query_events\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"operator_os_events\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("AgentGatewayRequestReceived", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("AgentGatewayResponseReturned", events.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_ProjectsRunEligibilityBlockerMatrix()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-WORKER-ELIGIBLE", scope: ["README.md"]);
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-WORKER-REVIEW-BLOCK", scope: ["README.md"]);
        File.Delete(Path.Combine(
            sandbox.RootPath,
            ".ai",
            "artifacts",
            "worker-executions",
            "T-INTEGRATION-WORKER-REVIEW-BLOCK.json"));

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-null");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-null", "default");

            var readiness = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-null");

            Assert.True(readiness.ExitCode == 0, readiness.CombinedOutput);
            Assert.Contains("\"kind\": \"worker_automation_readiness\"", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"run_eligibility\": {", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"decision\": \"blocked\"", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"host_ready\": true", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"review_gate_clear\": false", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"review_gate_blocked\"", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"family\": \"review_gate\"", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"blocking\": true", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"sdk_api_worker_boundary\": \"closed_until_separate_governed_activation\"", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"run_not_started\": true", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"lease_not_issued\": true", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"truth_writeback_not_allowed\": true", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"role_binding_readiness\": {", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Role-binding readiness is intentionally separate", readiness.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_ReportsSelectionFailureAsRuntimeBlocker()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-NULL-BLOCK";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        WriteNullWorkerPolicy(Path.Combine(sandbox.RootPath, ".carves-platform", "policies", "worker-selection.policy.json"));

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var readiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "worker-automation-readiness");

            Assert.True(readiness.ExitCode == 0, readiness.CombinedOutput);
            var root = JsonNode.Parse(readiness.StandardOutput)!.AsObject();
            Assert.Equal("blocked", root["status"]!.GetValue<string>());
            Assert.False(root["automation_can_run_now"]!.GetValue<bool>());

            var eligibility = root["run_eligibility"]!.AsObject();
            Assert.Equal("blocked", eligibility["decision"]!.GetValue<string>());
            Assert.True(eligibility["dispatchable_task_ready"]!.GetValue<bool>());
            Assert.False(eligibility["worker_runtime_ready"]!.GetValue<bool>());
            Assert.True(eligibility["review_gate_clear"]!.GetValue<bool>());
            Assert.Equal(taskId, eligibility["next_task_id"]!.GetValue<string>());
            Assert.Null(eligibility["selected_backend_id"]);
            Assert.False(eligibility["selected_backend_external_app_cli"]!.GetValue<bool>());
            Assert.Equal("selection_blocked", eligibility["worker_runtime_status"]!.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(eligibility["selection_error"]!.GetValue<string>()));

            var workerRuntime = root["worker_runtime"]!.AsObject();
            Assert.False(workerRuntime["available"]!.GetValue<bool>());
            Assert.Equal("selection_blocked", workerRuntime["status"]!.GetValue<string>());
            Assert.Null(workerRuntime["selected_backend_id"]);
            Assert.False(workerRuntime["selected_backend_external_app_cli"]!.GetValue<bool>());
            Assert.False(string.IsNullOrWhiteSpace(workerRuntime["selection_error"]!.GetValue<string>()));
            Assert.Equal("closed_until_separate_governed_activation", workerRuntime["sdk_api_worker_boundary"]!.GetValue<string>());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_DefaultRoleModeHardGateBlocksAutomation()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-ROLE-GATE";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliProbeScript();

        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-role-gate");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-role-gate", "codex-worker-local-cli");

            var readiness = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-role-gate",
                "--refresh-worker-health");
            var tick = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-role-gate",
                "--refresh-worker-health",
                "--dispatch");

            Assert.Equal(0, readiness.ExitCode);
            var root = JsonNode.Parse(readiness.StandardOutput)!.AsObject();
            Assert.Equal("blocked", root["status"]!.GetValue<string>());
            Assert.False(root["automation_can_run_now"]!.GetValue<bool>());

            var roleGate = root["role_mode_gate"]!.AsObject();
            Assert.False(roleGate["allowed"]!.GetValue<bool>());
            Assert.Equal("role_mode_disabled", roleGate["outcome"]!.GetValue<string>());
            Assert.Equal("disabled", roleGate["role_mode"]!.GetValue<string>());
            Assert.True(roleGate["main_thread_direct_mode"]!.GetValue<bool>());

            var eligibility = root["run_eligibility"]!.AsObject();
            Assert.Equal("blocked", eligibility["decision"]!.GetValue<string>());
            Assert.False(eligibility["role_mode_allowed"]!.GetValue<bool>());
            Assert.Equal("role_mode_disabled", eligibility["role_mode_outcome"]!.GetValue<string>());
            Assert.True(eligibility["dispatchable_task_ready"]!.GetValue<bool>());
            Assert.True(eligibility["worker_runtime_ready"]!.GetValue<bool>());
            Assert.True(eligibility["review_gate_clear"]!.GetValue<bool>());
            Assert.Equal("policy inspect", eligibility["next_command"]!.GetValue<string>());

            var blocker = Assert.IsType<JsonObject>(Assert.Single(root["blockers"]!.AsArray()));
            Assert.Equal("role_mode", blocker["family"]!.GetValue<string>());
            Assert.Equal("role_mode_disabled", blocker["code"]!.GetValue<string>());
            Assert.True(blocker["main_thread_direct_mode"]!.GetValue<bool>());
            Assert.True(blocker["role_automation_frozen"]!.GetValue<bool>());

            Assert.Equal(0, tick.ExitCode);
            var tickRoot = JsonNode.Parse(tick.StandardOutput)!.AsObject();
            Assert.Equal("blocked", tickRoot["status"]!.GetValue<string>());
            Assert.True(tickRoot["dispatch_requested"]!.GetValue<bool>());
            Assert.False(tickRoot["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(tickRoot["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.Null(tickRoot["delegated_result"]);
            var tickRoleGate = tickRoot["role_mode_gate"]!.AsObject();
            Assert.False(tickRoleGate["allowed"]!.GetValue<bool>());
            Assert.Equal("role_mode_disabled", tickRoleGate["outcome"]!.GetValue<string>());
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_ReportsReadyOnlyForExternalCliDispatch()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-READY";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliProbeScript();

        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-ready");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-ready", "codex-worker-local-cli");

            var readiness = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-ready",
                "--refresh-worker-health");

            Assert.Equal(0, readiness.ExitCode);
            var root = JsonNode.Parse(readiness.StandardOutput)!.AsObject();
            Assert.Equal("worker_automation_readiness", root["kind"]!.GetValue<string>());
            Assert.Equal("ready", root["status"]!.GetValue<string>());
            Assert.True(root["automation_can_run_now"]!.GetValue<bool>());
            Assert.False(root["starts_run"]!.GetValue<bool>());
            Assert.False(root["issues_lease"]!.GetValue<bool>());
            Assert.False(root["grants_truth_write_authority"]!.GetValue<bool>());

            var eligibility = root["run_eligibility"]!.AsObject();
            Assert.Equal("eligible", eligibility["decision"]!.GetValue<string>());
            Assert.True(eligibility["host_ready"]!.GetValue<bool>());
            Assert.True(eligibility["dispatchable_task_ready"]!.GetValue<bool>());
            Assert.True(eligibility["worker_runtime_ready"]!.GetValue<bool>());
            Assert.True(eligibility["review_gate_clear"]!.GetValue<bool>());
            Assert.Equal(taskId, eligibility["next_task_id"]!.GetValue<string>());
            Assert.Equal("codex_cli", eligibility["selected_backend_id"]!.GetValue<string>());
            Assert.Equal("local_cli", eligibility["selected_protocol_family"]!.GetValue<string>());
            Assert.True(eligibility["selected_backend_external_app_cli"]!.GetValue<bool>());
            Assert.Equal(0, eligibility["blocker_count"]!.GetValue<int>());
            Assert.Equal($"task run {taskId}", eligibility["next_command"]!.GetValue<string>());

            var workerRuntime = root["worker_runtime"]!.AsObject();
            Assert.True(workerRuntime["available"]!.GetValue<bool>());
            Assert.Equal("available", workerRuntime["status"]!.GetValue<string>());
            Assert.Equal("codex_cli", workerRuntime["selected_backend_id"]!.GetValue<string>());
            Assert.Equal("local_cli", workerRuntime["selected_protocol_family"]!.GetValue<string>());
            Assert.True(workerRuntime["selected_backend_external_app_cli"]!.GetValue<bool>());
            Assert.Equal("closed_until_separate_governed_activation", workerRuntime["sdk_api_worker_boundary"]!.GetValue<string>());

            Assert.Empty(root["blockers"]!.AsArray());
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_ProjectsScheduledTickPlanForRegisteredWorkerCallback()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-SCHEDULED";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliProbeScript();

        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-scheduled");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-scheduled", "codex-worker-local-cli");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase1-scheduled-codex-cli-worker",
                "--repo-id",
                "repo-worker-automation-scheduled",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--scope",
                "host-dispatch-only",
                "--schedule-binding",
                "worker-evidence-ping",
                "--context-receipt",
                "phase1-context-receipt",
                "--process-id",
                Environment.ProcessId.ToString(),
                "--health",
                "healthy",
                "--reason",
                "phase1-scheduled-worker-readiness");

            var readiness = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-scheduled",
                "--refresh-worker-health");
            var tickPreflight = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-scheduled",
                "--refresh-worker-health");
            var tickDispatch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-scheduled",
                "--refresh-worker-health",
                "--dispatch");
            var tickExecute = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-scheduled",
                "--refresh-worker-health",
                "--dispatch",
                "--execute");

            Assert.Equal(0, readiness.ExitCode);
            var root = JsonNode.Parse(readiness.StandardOutput)!.AsObject();
            Assert.Equal("ready", root["status"]!.GetValue<string>());
            Assert.True(root["automation_can_run_now"]!.GetValue<bool>());

            var schedulePlan = root["schedule_tick_plan"]!.AsObject();
            Assert.Equal("worker_automation_schedule_tick_plan", schedulePlan["kind"]!.GetValue<string>());
            Assert.Equal("schedule_callback_projection_only", schedulePlan["readiness_claim"]!.GetValue<string>());
            Assert.Equal("blocked", schedulePlan["decision"]!.GetValue<string>());
            Assert.False(schedulePlan["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.True(schedulePlan["worker_automation_can_run_now"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_process_tracking_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_bound_worker_supervised_registration_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_bound_worker_dispatch_proof_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["context_receipt_ready"]!.GetValue<bool>());
            Assert.Equal(1, schedulePlan["schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["live_context_receipted_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, schedulePlan["context_receipted_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, schedulePlan["dispatch_eligible_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["process_observed_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["process_alive_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["process_tracked_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.True(schedulePlan["process_tracked_schedule_bound_worker_session_count_deprecated"]!.GetValue<bool>());
            Assert.Equal("process_observed_schedule_bound_worker_session_count", schedulePlan["process_tracked_schedule_bound_worker_session_count_replaced_by"]!.GetValue<string>());
            Assert.Equal("deprecated_alias_for_process_observed_metadata_not_dispatch_eligibility", schedulePlan["process_tracked_schedule_bound_worker_session_count_semantics"]!.GetValue<string>());
            Assert.Equal("process_tracked", schedulePlan["worker_session_process_tracking_status"]!.GetValue<string>());
            Assert.Equal("process_alive_without_host_handle", schedulePlan["worker_session_dispatch_proof_status"]!.GetValue<string>());
            Assert.Equal("dispatch_requires_host_owned_process_handle; pid_start_time_is_identity_evidence_only", schedulePlan["schedule_dispatch_process_ownership_policy"]!.GetValue<string>());
            Assert.False(schedulePlan["schedule_dispatch_uses_process_identity_proof"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_dispatch_process_identity_evidence_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_dispatch_uses_host_owned_process_handle"]!.GetValue<bool>());
            Assert.Equal(0, schedulePlan["host_process_handle_ready_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, schedulePlan["immediate_death_detection_ready_schedule_bound_worker_session_count"]!.GetValue<int>());
            var selectedWorkerActorSessionId = schedulePlan["selected_worker_actor_session_id"]!.GetValue<string>();
            Assert.Equal(selectedWorkerActorSessionId, Assert.Single(schedulePlan["process_observed_schedule_bound_worker_session_ids"]!.AsArray())!.GetValue<string>());
            Assert.Equal(selectedWorkerActorSessionId, Assert.Single(schedulePlan["process_alive_schedule_bound_worker_session_ids"]!.AsArray())!.GetValue<string>());
            Assert.Equal("phase1-scheduled-codex-cli-worker", schedulePlan["selected_worker_identity"]!.GetValue<string>());
            Assert.Equal("worker-evidence-ping", schedulePlan["selected_worker_schedule_binding"]!.GetValue<string>());
            Assert.Equal("phase1-context-receipt", schedulePlan["selected_worker_context_receipt"]!.GetValue<string>());
            Assert.Equal("manual", schedulePlan["selected_worker_registration_mode"]!.GetValue<string>());
            Assert.Null(schedulePlan["selected_worker_worker_instance_id"]);
            Assert.Null(schedulePlan["selected_worker_supervisor_launch_token_id"]);
            Assert.Equal("manual_actor_session", schedulePlan["selected_worker_supervisor_capability_mode"]!.GetValue<string>());
            Assert.Equal("manual_actor_session_process_identity_proof_no_host_handle", schedulePlan["selected_worker_process_ownership_status"]!.GetValue<string>());
            Assert.Equal("actor_session_process_id_and_start_time", schedulePlan["selected_worker_process_proof_source"]!.GetValue<string>());
            Assert.False(schedulePlan["selected_worker_host_process_handle_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["selected_worker_immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.Equal("process_alive", schedulePlan["selected_worker_process_tracking_status"]!.GetValue<string>());
            Assert.Equal("process_alive_without_host_handle", schedulePlan["selected_worker_dispatch_proof_status"]!.GetValue<string>());
            Assert.Equal(taskId, schedulePlan["next_task_id"]!.GetValue<string>());
            Assert.Equal("inspect worker-automation-readiness", schedulePlan["host_lifecycle_command"]!.GetValue<string>());
            var planHandoff = schedulePlan["host_lifecycle_handoff"]!.AsObject();
            Assert.Equal("worker_automation_host_lifecycle_handoff", planHandoff["kind"]!.GetValue<string>());
            Assert.Equal("blocked", planHandoff["status"]!.GetValue<string>());
            Assert.False(planHandoff["ready_for_task_run"]!.GetValue<bool>());
            Assert.Equal($"task run {taskId}", planHandoff["task_run_command"]!.GetValue<string>());
            Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", planHandoff["post_run_readback_command"]!.GetValue<string>());
            Assert.Equal("task run result.execution_run_id", planHandoff["expected_execution_run_id_source"]!.GetValue<string>());
            Assert.Equal("worker-dispatch-pilot-evidence.dispatch.latest_run_id", planHandoff["readback_execution_run_id_source"]!.GetValue<string>());
            Assert.True(planHandoff["run_id_required_after_task_run"]!.GetValue<bool>());
            Assert.True(planHandoff["result_ingestion_required_after_task_run"]!.GetValue<bool>());
            Assert.False(planHandoff["schedule_tick_executes_task"]!.GetValue<bool>());
            Assert.False(planHandoff["writes_task_truth"]!.GetValue<bool>());
            Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", schedulePlan["callback_result_check_command"]!.GetValue<string>());
            Assert.True(schedulePlan["coordinator_callback_required"]!.GetValue<bool>());
            Assert.Contains($"task ingest-result {taskId}", schedulePlan["result_ingestion_semantics"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.True(schedulePlan["callback_evidence_required"]!.GetValue<bool>());
            Assert.True(schedulePlan["planner_reentry_required_after_review"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_tick_wakes_host_only"]!.GetValue<bool>());
            Assert.False(schedulePlan["creates_second_scheduler"]!.GetValue<bool>());
            Assert.False(schedulePlan["creates_task_queue"]!.GetValue<bool>());
            Assert.False(schedulePlan["starts_run"]!.GetValue<bool>());
            Assert.False(schedulePlan["issues_lease"]!.GetValue<bool>());
            Assert.False(schedulePlan["grants_execution_authority"]!.GetValue<bool>());
            Assert.False(schedulePlan["grants_truth_write_authority"]!.GetValue<bool>());
            Assert.Equal(0, schedulePlan["readiness_blocker_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["schedule_blocker_count"]!.GetValue<int>());
            var scheduleBlocker = Assert.IsType<JsonObject>(Assert.Single(schedulePlan["schedule_blockers"]!.AsArray()));
            Assert.Equal("schedule_bound_worker_host_process_handle_required", scheduleBlocker["code"]!.GetValue<string>());
            Assert.Equal("host_process_handle_required_for_schedule_dispatch", scheduleBlocker["dispatch_proof_code"]!.GetValue<string>());
            Assert.Equal("process_alive_without_host_handle", scheduleBlocker["dispatch_proof_status"]!.GetValue<string>());
            Assert.True(scheduleBlocker["process_tracking_ready"]!.GetValue<bool>());
            Assert.False(scheduleBlocker["host_process_handle_ready"]!.GetValue<bool>());

            Assert.Equal(0, tickPreflight.ExitCode);
            var tickPreflightRoot = JsonNode.Parse(tickPreflight.StandardOutput)!.AsObject();
            Assert.Equal("worker_automation_schedule_tick", tickPreflightRoot["kind"]!.GetValue<string>());
            Assert.Equal("blocked", tickPreflightRoot["status"]!.GetValue<string>());
            Assert.False(tickPreflightRoot["dispatch_requested"]!.GetValue<bool>());
            Assert.True(tickPreflightRoot["dry_run"]!.GetValue<bool>());
            Assert.False(tickPreflightRoot["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(tickPreflightRoot["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.Equal("preflight_only", tickPreflightRoot["host_dispatch_mode"]!.GetValue<string>());
            Assert.Equal(taskId, tickPreflightRoot["task_id"]!.GetValue<string>());
            Assert.True(tickPreflightRoot["schedule_tick_wakes_host_only"]!.GetValue<bool>());
            Assert.True(tickPreflightRoot["uses_existing_task_run_lifecycle"]!.GetValue<bool>());
            Assert.False(tickPreflightRoot["creates_second_scheduler"]!.GetValue<bool>());
            Assert.False(tickPreflightRoot["creates_task_queue"]!.GetValue<bool>());
            Assert.False(tickPreflightRoot["starts_run_without_dispatch_flag"]!.GetValue<bool>());
            Assert.False(tickPreflightRoot["issues_lease_without_host"]!.GetValue<bool>());
            Assert.False(tickPreflightRoot["grants_execution_authority"]!.GetValue<bool>());
            Assert.False(tickPreflightRoot["grants_truth_write_authority"]!.GetValue<bool>());
            var preflightReceipt = tickPreflightRoot["schedule_tick_receipt"]!.AsObject();
            Assert.Equal("worker-automation-schedule-tick-receipt.v1", preflightReceipt["schema_version"]!.GetValue<string>());
            Assert.Equal($"worker-automation-handoff:repo-worker-automation-scheduled:{taskId}", preflightReceipt["host_lifecycle_handoff_id"]!.GetValue<string>());
            Assert.Equal(taskId, preflightReceipt["task_id"]!.GetValue<string>());
            Assert.False(preflightReceipt["dispatch_requested"]!.GetValue<bool>());
            Assert.False(preflightReceipt["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.False(preflightReceipt["host_dispatch_accepted"]!.GetValue<bool>());
            Assert.True(preflightReceipt["callback_is_evidence_only"]!.GetValue<bool>());
            Assert.False(preflightReceipt["writes_task_truth"]!.GetValue<bool>());
            var preflightHandoff = tickPreflightRoot["host_lifecycle_handoff"]!.AsObject();
            Assert.Equal("blocked", preflightHandoff["status"]!.GetValue<string>());
            Assert.False(preflightHandoff["ready_for_task_run"]!.GetValue<bool>());
            Assert.Equal($"task run {taskId}", preflightHandoff["task_run_command"]!.GetValue<string>());
            Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", preflightHandoff["post_run_readback_command"]!.GetValue<string>());
            Assert.True(preflightHandoff["run_id_required_after_task_run"]!.GetValue<bool>());
            Assert.False(preflightHandoff["schedule_tick_executes_task"]!.GetValue<bool>());
            var preflightResultCheck = tickPreflightRoot["callback_result_check"]!.AsObject();
            Assert.Equal("worker_automation_callback_result_check", preflightResultCheck["kind"]!.GetValue<string>());
            Assert.Equal("blocked_before_dispatch", preflightResultCheck["status"]!.GetValue<string>());
            Assert.True(preflightResultCheck["host_lifecycle_handoff_required"]!.GetValue<bool>());
            Assert.Equal($"task run {taskId}", preflightResultCheck["host_lifecycle_command"]!.GetValue<string>());
            Assert.True(preflightResultCheck["run_id_required_after_task_run"]!.GetValue<bool>());
            Assert.Equal("worker-dispatch-pilot-evidence.dispatch.latest_run_id", preflightResultCheck["run_id_readback_source"]!.GetValue<string>());
            Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", preflightResultCheck["evidence_command"]!.GetValue<string>());
            Assert.False(preflightResultCheck["evidence_readback_ready"]!.GetValue<bool>());
            Assert.True(preflightResultCheck["host_validation_required"]!.GetValue<bool>());
            Assert.Equal("worker-dispatch-pilot-evidence.host_validation.valid", preflightResultCheck["host_validation_readback_source"]!.GetValue<string>());
            Assert.False(preflightResultCheck["host_validation_valid"]!.GetValue<bool>());
            Assert.True(preflightResultCheck["completion_claim_required"]!.GetValue<bool>());
            Assert.True(preflightResultCheck["review_bundle_required"]!.GetValue<bool>());
            Assert.True(preflightResultCheck["worker_claim_is_not_truth"]!.GetValue<bool>());
            Assert.True(preflightResultCheck["schedule_callback_is_not_completion"]!.GetValue<bool>());
            var coordinatorCallback = tickPreflightRoot["coordinator_callback"]!.AsObject();
            Assert.Equal("worker_automation_coordinator_callback_projection", coordinatorCallback["kind"]!.GetValue<string>());
            Assert.True(coordinatorCallback["required"]!.GetValue<bool>());
            Assert.False(coordinatorCallback["writes_truth"]!.GetValue<bool>());
            Assert.False(coordinatorCallback["marks_task_completed"]!.GetValue<bool>());

            Assert.True(tickDispatch.ExitCode == 0, tickDispatch.CombinedOutput);
            var tickDispatchRoot = JsonNode.Parse(tickDispatch.StandardOutput)!.AsObject();
            var tickDispatchStatus = tickDispatchRoot["status"]!.GetValue<string>();
            Assert.Equal("blocked", tickDispatchStatus);
            Assert.True(tickDispatchRoot["dispatch_requested"]!.GetValue<bool>());
            Assert.True(tickDispatchRoot["dry_run"]!.GetValue<bool>());
            Assert.False(tickDispatchRoot["execute_requested"]!.GetValue<bool>());
            Assert.False(tickDispatchRoot["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.Equal("preflight_only", tickDispatchRoot["host_dispatch_mode"]!.GetValue<string>());
            Assert.Equal("operator", tickDispatchRoot["delegation_actor_kind"]!.GetValue<string>(), ignoreCase: true);
            Assert.Equal("scheduled-worker-automation", tickDispatchRoot["delegation_actor_identity"]!.GetValue<string>());
            var dispatchHandoff = tickDispatchRoot["host_lifecycle_handoff"]!.AsObject();
            Assert.Equal("blocked", dispatchHandoff["status"]!.GetValue<string>());
            Assert.False(dispatchHandoff["ready_for_task_run"]!.GetValue<bool>());
            Assert.False(dispatchHandoff["schedule_tick_dispatch_attempted"]!.GetValue<bool>());
            Assert.False(dispatchHandoff["schedule_tick_dispatch_accepted"]!.GetValue<bool>());
            Assert.Equal($"task run {taskId}", dispatchHandoff["task_run_command"]!.GetValue<string>());
            Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", dispatchHandoff["post_run_readback_command"]!.GetValue<string>());
            Assert.Null(tickDispatchRoot["delegated_result"]);
            var dispatchReceipt = tickDispatchRoot["schedule_tick_receipt"]!.AsObject();
            Assert.True(dispatchReceipt["dispatch_requested"]!.GetValue<bool>());
            Assert.True(dispatchReceipt["dry_run"]!.GetValue<bool>());
            Assert.False(dispatchReceipt["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.False(dispatchReceipt["host_dispatch_accepted"]!.GetValue<bool>());

            var dispatchResultCheck = tickDispatchRoot["callback_result_check"]!.AsObject();
            Assert.Equal("blocked_before_dispatch", dispatchResultCheck["status"]!.GetValue<string>());
            Assert.False(dispatchResultCheck["evidence_readback_ready"]!.GetValue<bool>());
            Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", dispatchResultCheck["evidence_command"]!.GetValue<string>());
            Assert.True(dispatchResultCheck["host_validation_required"]!.GetValue<bool>());
            Assert.Equal("worker-dispatch-pilot-evidence.host_validation.valid", dispatchResultCheck["host_validation_readback_source"]!.GetValue<string>());
            Assert.False(dispatchResultCheck["host_validation_valid"]!.GetValue<bool>());
            Assert.True(dispatchResultCheck["closure_writeback_requires_review_bundle"]!.GetValue<bool>());
            Assert.True(dispatchResultCheck["planner_reentry_required_after_review"]!.GetValue<bool>());

            Assert.True(tickExecute.ExitCode == 0, tickExecute.CombinedOutput);
            var tickExecuteRoot = JsonNode.Parse(tickExecute.StandardOutput)!.AsObject();
            Assert.Equal("blocked", tickExecuteRoot["status"]!.GetValue<string>());
            Assert.True(tickExecuteRoot["dispatch_requested"]!.GetValue<bool>());
            Assert.False(tickExecuteRoot["dry_run"]!.GetValue<bool>());
            Assert.True(tickExecuteRoot["execute_requested"]!.GetValue<bool>());
            Assert.True(tickExecuteRoot["execute_blocked_by_schedule_tick_boundary"]!.GetValue<bool>());
            Assert.Equal($"task run {taskId}", tickExecuteRoot["execute_next_command"]!.GetValue<string>());
            Assert.False(tickExecuteRoot["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.Equal("execute_blocked", tickExecuteRoot["host_dispatch_mode"]!.GetValue<string>());
            var executeHandoff = tickExecuteRoot["host_lifecycle_handoff"]!.AsObject();
            Assert.Equal("blocked", executeHandoff["status"]!.GetValue<string>());
            Assert.False(executeHandoff["ready_for_task_run"]!.GetValue<bool>());
            Assert.True(executeHandoff["execute_requested"]!.GetValue<bool>());
            Assert.False(executeHandoff["schedule_tick_executes_task"]!.GetValue<bool>());
            Assert.Equal($"task run {taskId}", executeHandoff["task_run_command"]!.GetValue<string>());
            Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", executeHandoff["post_run_readback_command"]!.GetValue<string>());
            var executeReceipt = tickExecuteRoot["schedule_tick_receipt"]!.AsObject();
            Assert.True(executeReceipt["dispatch_requested"]!.GetValue<bool>());
            Assert.False(executeReceipt["dry_run"]!.GetValue<bool>());
            Assert.False(executeReceipt["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.False(executeReceipt["host_dispatch_accepted"]!.GetValue<bool>());
            Assert.True(executeReceipt["callback_is_evidence_only"]!.GetValue<bool>());
            var executeResultCheck = tickExecuteRoot["callback_result_check"]!.AsObject();
            Assert.Equal("blocked_before_dispatch", executeResultCheck["status"]!.GetValue<string>());
            Assert.False(executeResultCheck["evidence_readback_ready"]!.GetValue<bool>());
            Assert.False(executeResultCheck["evidence_readback_included"]!.GetValue<bool>());
            Assert.False(executeResultCheck["evidence_chain_complete"]!.GetValue<bool>());
            Assert.Empty(executeResultCheck["evidence_missing_links"]!.AsArray());
            Assert.True(executeResultCheck["execute_blocked_by_schedule_tick_boundary"]!.GetValue<bool>());
            Assert.False(executeResultCheck["host_result_ingestion_attempted"]!.GetValue<bool>());
            Assert.False(executeResultCheck["host_result_ingestion_applied"]!.GetValue<bool>());
            Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", executeResultCheck["evidence_command"]!.GetValue<string>());
            Assert.Null(tickExecuteRoot["callback_result_evidence"]);
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_TreatsSupervisedRegistrationAsScheduleProof()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-SUPERVISED-SCHEDULE";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliProbeScript();

        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-supervised");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-supervised", "codex-worker-local-cli");

            var launch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-worker-automation-supervised",
                "--identity",
                "phase-s4-supervised-codex-cli-worker",
                "--worker-instance-id",
                "worker-instance-s4",
                "--actor-session-id",
                "actor-worker-s4",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--schedule-binding",
                "worker-evidence-ping",
                "--reason",
                "phase S4 issue supervised launch token");
            var launchRoot = ParseFirstJsonObject(launch.StandardOutput);
            var launchToken = launchRoot["launch_token"]!.GetValue<string>();
            var launchTokenId = launchRoot["launch_token_id"]!.GetValue<string>();
            var registration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-s4-supervised-codex-cli-worker",
                "--repo-id",
                "repo-worker-automation-supervised",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-s4",
                "--launch-token",
                launchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-s4-context-receipt",
                "--health",
                "healthy",
                "--reason",
                "phase S4 supervised schedule worker registration");

            var readiness = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-supervised",
                "--refresh-worker-health");
            var tickPreflight = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-supervised",
                "--refresh-worker-health");

            Assert.Equal(0, launch.ExitCode);
            Assert.Equal(0, registration.ExitCode);
            Assert.Contains("\"registration_mode\": \"supervised\"", registration.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, readiness.ExitCode);
            var root = JsonNode.Parse(readiness.StandardOutput)!.AsObject();
            Assert.Equal("ready", root["status"]!.GetValue<string>());
            Assert.True(root["automation_can_run_now"]!.GetValue<bool>());
            var schedulePlan = root["schedule_tick_plan"]!.AsObject();
            Assert.Equal("blocked", schedulePlan["decision"]!.GetValue<string>());
            Assert.False(schedulePlan["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_process_tracking_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_supervised_registration_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_bound_worker_dispatch_proof_ready"]!.GetValue<bool>());
            Assert.Equal(1, schedulePlan["supervised_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["process_observed_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["process_alive_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["process_tracked_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.True(schedulePlan["process_tracked_schedule_bound_worker_session_count_deprecated"]!.GetValue<bool>());
            Assert.Equal("process_tracked", schedulePlan["worker_session_process_tracking_status"]!.GetValue<string>());
            Assert.Equal("supervised_process_alive_without_host_handle", schedulePlan["worker_session_dispatch_proof_status"]!.GetValue<string>());
            Assert.Equal("dispatch_requires_host_owned_process_handle; pid_start_time_is_identity_evidence_only", schedulePlan["schedule_dispatch_process_ownership_policy"]!.GetValue<string>());
            Assert.False(schedulePlan["schedule_dispatch_uses_process_identity_proof"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_dispatch_process_identity_evidence_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_dispatch_uses_host_owned_process_handle"]!.GetValue<bool>());
            Assert.Equal(0, schedulePlan["host_process_handle_ready_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, schedulePlan["immediate_death_detection_ready_schedule_bound_worker_session_count"]!.GetValue<int>());
            var selectedWorkerActorSessionId = schedulePlan["selected_worker_actor_session_id"]!.GetValue<string>();
            Assert.Equal(selectedWorkerActorSessionId, Assert.Single(schedulePlan["process_observed_schedule_bound_worker_session_ids"]!.AsArray())!.GetValue<string>());
            Assert.Equal(selectedWorkerActorSessionId, Assert.Single(schedulePlan["process_alive_schedule_bound_worker_session_ids"]!.AsArray())!.GetValue<string>());
            Assert.Equal("phase-s4-supervised-codex-cli-worker", schedulePlan["selected_worker_identity"]!.GetValue<string>());
            Assert.Equal("supervised", schedulePlan["selected_worker_registration_mode"]!.GetValue<string>());
            Assert.Equal("worker-instance-s4", schedulePlan["selected_worker_worker_instance_id"]!.GetValue<string>());
            Assert.Equal(launchTokenId, schedulePlan["selected_worker_supervisor_launch_token_id"]!.GetValue<string>());
            Assert.Equal("registration_only_launch_token", schedulePlan["selected_worker_supervisor_capability_mode"]!.GetValue<string>());
            Assert.Equal("supervised_actor_session_process_identity_proof_no_host_handle", schedulePlan["selected_worker_process_ownership_status"]!.GetValue<string>());
            Assert.Equal("actor_session_process_id_and_start_time", schedulePlan["selected_worker_process_proof_source"]!.GetValue<string>());
            Assert.False(schedulePlan["selected_worker_host_process_handle_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["selected_worker_immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.Equal("process_alive", schedulePlan["selected_worker_process_tracking_status"]!.GetValue<string>());
            Assert.Equal("supervised_process_alive_without_host_handle", schedulePlan["selected_worker_dispatch_proof_status"]!.GetValue<string>());
            Assert.Equal(taskId, schedulePlan["next_task_id"]!.GetValue<string>());
            Assert.DoesNotContain(launchToken, readiness.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, tickPreflight.ExitCode);
            var tickRoot = JsonNode.Parse(tickPreflight.StandardOutput)!.AsObject();
            Assert.Equal("blocked", tickRoot["status"]!.GetValue<string>());
            Assert.False(tickRoot["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(tickRoot["host_dispatch_attempted"]!.GetValue<bool>());
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_AllowsHostHandleBackedSupervisedWorkerScheduleDispatch()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-HOST-HANDLE-SCHEDULE";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliProbeScript();
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);

        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-host-handle");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-host-handle", "codex-worker-local-cli");

            var launch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-worker-automation-host-handle",
                "--identity",
                "phase-r5-host-handle-codex-cli-worker",
                "--worker-instance-id",
                "worker-instance-r5-host-handle",
                "--actor-session-id",
                "actor-worker-r5-host-handle",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--schedule-binding",
                "worker-evidence-ping",
                "--reason",
                "phase R5 issue supervised launch token");
            var launchRoot = ParseFirstJsonObject(launch.StandardOutput);
            var launchToken = launchRoot["launch_token"]!.GetValue<string>();
            var registration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-r5-host-handle-codex-cli-worker",
                "--repo-id",
                "repo-worker-automation-host-handle",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-r5-host-handle",
                "--launch-token",
                launchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-r5-context-receipt",
                "--health",
                "healthy",
                "--reason",
                "phase R5 supervised schedule worker registration");

            var supervisorState = new WorkerSupervisorStateService(new JsonWorkerSupervisorStateRepository(paths));
            var runningSupervisor = Assert.Single(supervisorState.List("repo-worker-automation-host-handle"));
            var hostProcessHandleOwnerSessionId = runningSupervisor.HostSessionId ?? "host-session-r5";
            supervisorState.RecordHostProcessHandle(
                "worker-instance-r5-host-handle",
                "host-process-handle-r5",
                hostProcessHandleOwnerSessionId,
                "phase R5 host process handle proof",
                stdoutLogPath: "logs/worker-r5.stdout.log",
                stderrLogPath: "logs/worker-r5.stderr.log");

            var instances = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-worker-automation-host-handle");
            var readiness = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-host-handle",
                "--refresh-worker-health");
            var tickPreflight = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-host-handle",
                "--refresh-worker-health");

            Assert.Equal(0, launch.ExitCode);
            Assert.Equal(0, registration.ExitCode);

            Assert.Equal(0, instances.ExitCode);
            var instancesRoot = ParseFirstJsonObject(instances.StandardOutput);
            Assert.Equal("host_process_supervision_ready", instancesRoot["supervisor_readiness_posture"]!.GetValue<string>());
            Assert.True(instancesRoot["host_process_supervision_ready"]!.GetValue<bool>());
            Assert.True(instancesRoot["immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.Equal(0, instancesRoot["supervisor_automation_gap_count"]!.GetValue<int>());
            var entry = Assert.IsType<JsonObject>(Assert.Single(instancesRoot["entries"]!.AsArray()));
            Assert.Equal("host-process-handle-r5", entry["host_process_handle_id"]!.GetValue<string>());
            Assert.Equal(hostProcessHandleOwnerSessionId, entry["host_process_handle_owner_session_id"]!.GetValue<string>());
            Assert.NotNull(entry["host_process_handle_acquired_at"]);
            Assert.Equal("host_owned_process_handle", entry["supervisor_capability_mode"]!.GetValue<string>());
            Assert.Equal("host_owned_process_handle_ready", entry["process_ownership_status"]!.GetValue<string>());
            Assert.True(entry["host_process_spawned"]!.GetValue<bool>());
            Assert.True(entry["host_process_handle_held"]!.GetValue<bool>());
            Assert.True(entry["std_io_captured"]!.GetValue<bool>());
            Assert.True(entry["immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.Equal("host_process_handle_plus_actor_session_process_id_and_start_time", entry["process_proof_source"]!.GetValue<string>());

            Assert.Equal(0, readiness.ExitCode);
            var root = JsonNode.Parse(readiness.StandardOutput)!.AsObject();
            Assert.Equal("ready", root["status"]!.GetValue<string>());
            Assert.True(root["automation_can_run_now"]!.GetValue<bool>());
            var schedulePlan = root["schedule_tick_plan"]!.AsObject();
            Assert.Equal("dispatchable", schedulePlan["decision"]!.GetValue<string>());
            Assert.True(schedulePlan["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_process_tracking_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_supervised_registration_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_dispatch_proof_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["context_receipt_ready"]!.GetValue<bool>());
            Assert.Equal(1, schedulePlan["dispatch_eligible_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["host_process_handle_ready_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["immediate_death_detection_ready_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal("host_process_handle_ready", schedulePlan["worker_session_dispatch_proof_status"]!.GetValue<string>());
            Assert.True(schedulePlan["schedule_dispatch_process_identity_evidence_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_dispatch_uses_host_owned_process_handle"]!.GetValue<bool>());
            Assert.Equal("host_owned_process_handle", schedulePlan["selected_worker_supervisor_capability_mode"]!.GetValue<string>());
            Assert.Equal("host_owned_process_handle_ready", schedulePlan["selected_worker_process_ownership_status"]!.GetValue<string>());
            Assert.True(schedulePlan["selected_worker_host_process_handle_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["selected_worker_immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.Equal("host_process_handle_ready", schedulePlan["selected_worker_dispatch_proof_status"]!.GetValue<string>());
            Assert.Equal(taskId, schedulePlan["next_task_id"]!.GetValue<string>());
            Assert.Equal($"task run {taskId}", schedulePlan["host_lifecycle_command"]!.GetValue<string>());
            Assert.Equal(0, schedulePlan["schedule_blocker_count"]!.GetValue<int>());

            Assert.Equal(0, tickPreflight.ExitCode);
            var tickRoot = JsonNode.Parse(tickPreflight.StandardOutput)!.AsObject();
            Assert.Equal("ready_preflight_only", tickRoot["status"]!.GetValue<string>());
            Assert.True(tickRoot["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(tickRoot["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.DoesNotContain(launchToken, readiness.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_BlocksFreshHeartbeatOnlyScheduleWorker()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-HEARTBEAT-ONLY-SCHEDULE";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliProbeScript();

        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-heartbeat-only");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-heartbeat-only", "codex-worker-local-cli");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase23-heartbeat-only-codex-cli-worker",
                "--repo-id",
                "repo-worker-automation-heartbeat-only",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--scope",
                "host-dispatch-only",
                "--schedule-binding",
                "worker-evidence-ping",
                "--context-receipt",
                "phase23-context-receipt",
                "--health",
                "healthy",
                "--reason",
                "phase23 heartbeat-only schedule worker should not dispatch");

            var readiness = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-heartbeat-only",
                "--refresh-worker-health");
            var tickPreflight = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-heartbeat-only",
                "--refresh-worker-health");
            var tickDispatch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-heartbeat-only",
                "--refresh-worker-health",
                "--dispatch");

            Assert.Equal(0, readiness.ExitCode);
            var root = JsonNode.Parse(readiness.StandardOutput)!.AsObject();
            Assert.Equal("ready", root["status"]!.GetValue<string>());
            Assert.True(root["automation_can_run_now"]!.GetValue<bool>());

            var schedulePlan = root["schedule_tick_plan"]!.AsObject();
            Assert.Equal("blocked", schedulePlan["decision"]!.GetValue<string>());
            Assert.False(schedulePlan["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.True(schedulePlan["schedule_bound_worker_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_bound_worker_process_tracking_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_bound_worker_supervised_registration_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_bound_worker_dispatch_proof_ready"]!.GetValue<bool>());
            Assert.True(schedulePlan["context_receipt_ready"]!.GetValue<bool>());
            Assert.Equal(1, schedulePlan["live_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, schedulePlan["dispatch_eligible_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["live_context_receipted_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, schedulePlan["context_receipted_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, schedulePlan["process_observed_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Empty(schedulePlan["process_observed_schedule_bound_worker_session_ids"]!.AsArray());
            Assert.Equal(0, schedulePlan["process_alive_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Empty(schedulePlan["process_alive_schedule_bound_worker_session_ids"]!.AsArray());
            Assert.Equal(0, schedulePlan["process_tracked_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.True(schedulePlan["process_tracked_schedule_bound_worker_session_count_deprecated"]!.GetValue<bool>());
            Assert.Equal("ready", schedulePlan["worker_session_liveness_status"]!.GetValue<string>());
            Assert.Equal("heartbeat_only", schedulePlan["worker_session_process_tracking_status"]!.GetValue<string>());
            Assert.Equal("missing", schedulePlan["worker_session_dispatch_proof_status"]!.GetValue<string>());
            Assert.Equal("heartbeat_only", schedulePlan["selected_worker_process_tracking_status"]!.GetValue<string>());
            Assert.Equal("missing", schedulePlan["selected_worker_dispatch_proof_status"]!.GetValue<string>());
            var scheduleBlocker = Assert.IsType<JsonObject>(Assert.Single(schedulePlan["schedule_blockers"]!.AsArray()));
            Assert.Equal("schedule_bound_worker_host_process_handle_required", scheduleBlocker["code"]!.GetValue<string>());
            Assert.Equal("host_process_handle_required_for_schedule_dispatch", scheduleBlocker["dispatch_proof_code"]!.GetValue<string>());
            Assert.Equal("heartbeat_only", scheduleBlocker["process_tracking_status"]!.GetValue<string>());
            Assert.Equal("missing", scheduleBlocker["dispatch_proof_status"]!.GetValue<string>());
            Assert.False(scheduleBlocker["process_tracking_ready"]!.GetValue<bool>());
            Assert.False(scheduleBlocker["host_process_handle_ready"]!.GetValue<bool>());
            Assert.Contains("worker-supervisor-launch", scheduleBlocker["next_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("Host-owned worker supervisor", scheduleBlocker["next_action"]!.GetValue<string>(), StringComparison.Ordinal);

            Assert.Equal(0, tickPreflight.ExitCode);
            var tickRoot = JsonNode.Parse(tickPreflight.StandardOutput)!.AsObject();
            Assert.Equal("blocked", tickRoot["status"]!.GetValue<string>());
            Assert.False(tickRoot["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(tickRoot["host_dispatch_attempted"]!.GetValue<bool>());

            Assert.Equal(0, tickDispatch.ExitCode);
            var dispatchRoot = JsonNode.Parse(tickDispatch.StandardOutput)!.AsObject();
            Assert.Equal("blocked", dispatchRoot["status"]!.GetValue<string>());
            Assert.True(dispatchRoot["dispatch_requested"]!.GetValue<bool>());
            Assert.False(dispatchRoot["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(dispatchRoot["host_dispatch_attempted"]!.GetValue<bool>());
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_BlocksProcessMismatchedScheduleWorker()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-PROCESS-MISMATCH";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliProbeScript();

        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-process-mismatch");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-process-mismatch", "codex-worker-local-cli");
            WriteProcessMismatchedScheduleWorkerSession(sandbox.RootPath);

            var readiness = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-process-mismatch",
                "--refresh-worker-health");
            var sessions = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "actor",
                "sessions",
                "--kind",
                "worker",
                "--repo-id",
                "repo-worker-automation-process-mismatch");

            Assert.Equal(0, readiness.ExitCode);
            var root = JsonNode.Parse(readiness.StandardOutput)!.AsObject();
            Assert.Equal("ready", root["status"]!.GetValue<string>());
            Assert.True(root["automation_can_run_now"]!.GetValue<bool>());

            var schedulePlan = root["schedule_tick_plan"]!.AsObject();
            Assert.Equal("blocked", schedulePlan["decision"]!.GetValue<string>());
            Assert.False(schedulePlan["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_bound_worker_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_bound_worker_process_tracking_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["context_receipt_ready"]!.GetValue<bool>());
            Assert.Equal(1, schedulePlan["schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, schedulePlan["live_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["closed_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal("closed_or_unavailable", schedulePlan["worker_session_liveness_status"]!.GetValue<string>());
            Assert.Equal("process_mismatch", schedulePlan["worker_session_process_tracking_status"]!.GetValue<string>());
            Assert.Equal(1, schedulePlan["process_observed_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal("actor-worker-process-mismatch", Assert.Single(schedulePlan["process_observed_schedule_bound_worker_session_ids"]!.AsArray())!.GetValue<string>());
            Assert.Equal(0, schedulePlan["process_alive_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Empty(schedulePlan["process_alive_schedule_bound_worker_session_ids"]!.AsArray());
            Assert.Equal(1, schedulePlan["process_tracked_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.True(schedulePlan["process_tracked_schedule_bound_worker_session_count_deprecated"]!.GetValue<bool>());
            Assert.Equal(1, schedulePlan["process_mismatch_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal("actor-worker-process-mismatch", Assert.Single(schedulePlan["process_mismatch_schedule_bound_worker_session_ids"]!.AsArray())!.GetValue<string>());
            var scheduleBlocker = Assert.IsType<JsonObject>(Assert.Single(schedulePlan["schedule_blockers"]!.AsArray()));
            Assert.Equal("schedule_bound_worker_session_closed", scheduleBlocker["code"]!.GetValue<string>());
            Assert.Equal("process_mismatch", scheduleBlocker["process_tracking_status"]!.GetValue<string>());
            Assert.Equal("actor-worker-process-mismatch", Assert.Single(scheduleBlocker["process_mismatch_session_ids"]!.AsArray())!.GetValue<string>());
            var planLivenessReason = Assert.Single(scheduleBlocker["non_live_worker_liveness_reasons"]!.AsArray())!.GetValue<string>();
            Assert.Equal("actor-worker-process-mismatch:process_mismatch", planLivenessReason);

            Assert.Equal(0, sessions.ExitCode);
            Assert.Contains($"process_id: {Environment.ProcessId}", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("process_started_at: 2000-01-01T00:00:00.0000000+00:00", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("process_tracking: process_mismatch", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("liveness: closed", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("reason=process_mismatch", sessions.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Fact]
    public void WorkerAutomationReadiness_BlocksScheduleTickForStaleRegisteredWorkerCallback()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-STALE-SCHEDULE";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliProbeScript();

        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-stale");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-stale", "codex-worker-local-cli");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase4-stale-codex-cli-worker",
                "--repo-id",
                "repo-worker-automation-stale",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--scope",
                "host-dispatch-only",
                "--schedule-binding",
                "worker-evidence-ping",
                "--context-receipt",
                "phase4-context-receipt",
                "--health",
                "healthy",
                "--reason",
                "phase4-scheduled-worker-readiness");
            BackdateScheduleBoundWorkerSessions(sandbox.RootPath, TimeSpan.FromMinutes(30));

            var readiness = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-stale",
                "--refresh-worker-health");
            var tickPreflight = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-stale",
                "--refresh-worker-health");

            Assert.Equal(0, readiness.ExitCode);
            var root = JsonNode.Parse(readiness.StandardOutput)!.AsObject();
            Assert.Equal("ready", root["status"]!.GetValue<string>());
            Assert.True(root["automation_can_run_now"]!.GetValue<bool>());

            var schedulePlan = root["schedule_tick_plan"]!.AsObject();
            Assert.Equal("blocked", schedulePlan["decision"]!.GetValue<string>());
            Assert.False(schedulePlan["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(schedulePlan["schedule_bound_worker_ready"]!.GetValue<bool>());
            Assert.False(schedulePlan["context_receipt_ready"]!.GetValue<bool>());
            Assert.Equal(1, schedulePlan["schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, schedulePlan["live_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(1, schedulePlan["stale_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal("stale", schedulePlan["worker_session_liveness_status"]!.GetValue<string>());
            Assert.Equal("heartbeat_only", schedulePlan["worker_session_process_tracking_status"]!.GetValue<string>());
            Assert.Equal(0, schedulePlan["process_observed_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Empty(schedulePlan["process_observed_schedule_bound_worker_session_ids"]!.AsArray());
            Assert.Equal(0, schedulePlan["process_alive_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Empty(schedulePlan["process_alive_schedule_bound_worker_session_ids"]!.AsArray());
            Assert.Equal(0, schedulePlan["process_tracked_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.True(schedulePlan["process_tracked_schedule_bound_worker_session_count_deprecated"]!.GetValue<bool>());
            Assert.Equal(1, schedulePlan["heartbeat_only_schedule_bound_worker_session_count"]!.GetValue<int>());
            var staleSessionIds = schedulePlan["stale_schedule_bound_worker_session_ids"]!.AsArray();
            var staleSessionId = Assert.Single(staleSessionIds)!.GetValue<string>();
            Assert.Equal(staleSessionId, Assert.Single(schedulePlan["heartbeat_only_schedule_bound_worker_session_ids"]!.AsArray())!.GetValue<string>());
            var planLivenessReason = Assert.Single(schedulePlan["schedule_blockers"]!.AsArray())!.AsObject()["non_live_worker_liveness_reasons"]!.AsArray();
            Assert.Equal($"{staleSessionId}:heartbeat_stale", Assert.Single(planLivenessReason)!.GetValue<string>());
            var expectedStopCommand = $"api actor-session-stop --actor-session-id {staleSessionId} --reason worker-thread-not-live";
            var planStopCommand = Assert.Single(schedulePlan["non_live_worker_stop_commands"]!.AsArray())!.GetValue<string>();
            Assert.Equal(expectedStopCommand, planStopCommand);
            var scheduleBlocker = Assert.IsType<JsonObject>(Assert.Single(schedulePlan["schedule_blockers"]!.AsArray()));
            Assert.Equal("schedule_bound_worker_session_stale", scheduleBlocker["code"]!.GetValue<string>());
            Assert.Equal("heartbeat_only", scheduleBlocker["process_tracking_status"]!.GetValue<string>());
            Assert.Equal(staleSessionId, Assert.Single(scheduleBlocker["heartbeat_only_session_ids"]!.AsArray())!.GetValue<string>());
            Assert.Contains("Refresh the worker actor session", scheduleBlocker["next_action"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("stop the stale/closed actor session", scheduleBlocker["next_action"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Equal(expectedStopCommand, scheduleBlocker["next_command"]!.GetValue<string>());
            var blockerStopCommand = Assert.Single(scheduleBlocker["non_live_worker_stop_commands"]!.AsArray())!.GetValue<string>();
            Assert.Equal(expectedStopCommand, blockerStopCommand);

            Assert.Equal(0, tickPreflight.ExitCode);
            var tickRoot = JsonNode.Parse(tickPreflight.StandardOutput)!.AsObject();
            Assert.Equal("blocked", tickRoot["status"]!.GetValue<string>());
            Assert.False(tickRoot["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(tickRoot["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.Equal("preflight_only", tickRoot["host_dispatch_mode"]!.GetValue<string>());
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Fact]
    public void ActorRoleReadiness_TreatsStaleWorkerSessionAsMissingRole()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-actor-role-stale");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "planner",
                "--identity",
                "phase7-live-planner",
                "--repo-id",
                "repo-actor-role-stale",
                "--actor-session-id",
                "actor-planner-phase7-live",
                "--scope",
                "planning-only",
                "--reason",
                "phase7-live-planner-registration");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase7-stale-worker",
                "--repo-id",
                "repo-actor-role-stale",
                "--actor-session-id",
                "actor-worker-phase7-stale",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--scope",
                "host-dispatch-only",
                "--schedule-binding",
                "worker-evidence-ping",
                "--context-receipt",
                "phase7-context-receipt",
                "--health",
                "healthy",
                "--reason",
                "phase7-stale-worker-registration");
            BackdateScheduleBoundWorkerSessions(sandbox.RootPath, TimeSpan.FromMinutes(30));

            var roleReadiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "actor-role-readiness");
            var fallbackPolicy = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-session-fallback-policy", "--repo-id", "repo-actor-role-stale");

            Assert.Equal(0, roleReadiness.ExitCode);
            Assert.Contains("\"kind\": \"actor_role_binding_readiness\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"registered_actor_count\": 3", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"non_live_actor_session_count\": 1", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"stale_actor_session_count\": 1", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"closed_actor_session_count\": 0", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"actor_session_process_tracking_policy\": \"process_id_and_process_started_at_required_for_process_alive; heartbeat_only_sessions_depend_on_freshness_window\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"heartbeat_only_actor_session_ids\": [", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"non_live_actor_session_policy\": \"stale_or_closed_sessions_do_not_satisfy_role_binding\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_planner_registered\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_worker_registered\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"fallback_allowed\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"missing_actor_roles\": [", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"stale_actor_session_ids\": [", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"actor-worker-phase7-stale\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"non_live_actor_stop_commands\": [", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("api actor-session-stop --actor-session-id actor-worker-phase7-stale --reason actor-thread-not-live", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_role_binding_ready\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_dispatch_ready\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_dispatch_ready_deprecated\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_callback_projection_ready\": false", roleReadiness.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, fallbackPolicy.ExitCode);
            Assert.Contains("\"fallback_mode\": \"same_agent_split_role_fallback_allowed_with_evidence\"", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_planner_registered\": true", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_worker_registered\": false", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"registered_actor_count\": 3", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"non_live_actor_session_count\": 1", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"stale_actor_session_count\": 1", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"non_live_actor_session_policy\": \"stale_or_closed_sessions_do_not_satisfy_role_binding\"", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"non_live_actor_stop_commands\": [", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("api actor-session-stop --actor-session-id actor-worker-phase7-stale --reason actor-thread-not-live", fallbackPolicy.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorSessionStop_BlocksScheduleTickWithoutDeletingSession()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-WORKER-AUTOMATION-STOPPED-SCHEDULE";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliProbeScript();

        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-automation-stopped");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-automation-stopped", "codex-worker-local-cli");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase5-stopped-codex-cli-worker",
                "--repo-id",
                "repo-worker-automation-stopped",
                "--actor-session-id",
                "actor-worker-phase5-stopped",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--scope",
                "host-dispatch-only",
                "--schedule-binding",
                "worker-evidence-ping",
                "--context-receipt",
                "phase5-context-receipt",
                "--process-id",
                Environment.ProcessId.ToString(),
                "--health",
                "healthy",
                "--reason",
                "phase5-scheduled-worker-readiness");

            var readinessBeforeStop = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-stopped",
                "--refresh-worker-health");
            var stop = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-stop",
                "--actor-session-id",
                "actor-worker-phase5-stopped",
                "--reason",
                "worker thread closed");
            var sessions = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions", "--kind", "worker");
            var events = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "os-events", "--actor-session-id", "actor-worker-phase5-stopped");
            var readinessAfterStop = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-readiness",
                "--repo-id",
                "repo-worker-automation-stopped",
                "--refresh-worker-health");
            var tickPreflightAfterStop = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                "repo-worker-automation-stopped",
                "--refresh-worker-health");

            Assert.Equal(0, readinessBeforeStop.ExitCode);
            var beforeRoot = JsonNode.Parse(readinessBeforeStop.StandardOutput)!.AsObject();
            var beforePlan = beforeRoot["schedule_tick_plan"]!.AsObject();
            Assert.Equal("blocked", beforePlan["decision"]!.GetValue<string>());
            Assert.False(beforePlan["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.True(beforePlan["schedule_bound_worker_ready"]!.GetValue<bool>());
            Assert.True(beforePlan["context_receipt_ready"]!.GetValue<bool>());
            Assert.False(beforePlan["schedule_bound_worker_dispatch_proof_ready"]!.GetValue<bool>());
            Assert.Equal("process_alive_without_host_handle", beforePlan["worker_session_dispatch_proof_status"]!.GetValue<string>());
            Assert.Equal("actor-worker-phase5-stopped", beforePlan["selected_worker_actor_session_id"]!.GetValue<string>());

            Assert.Equal(0, stop.ExitCode);
            var stopRoot = ParseFirstJsonObject(stop.StandardOutput);
            Assert.Equal("actor-session-stop.v1", stopRoot["schema_version"]!.GetValue<string>());
            Assert.False(stopRoot["dry_run"]!.GetValue<bool>());
            Assert.Equal("actor-worker-phase5-stopped", stopRoot["actor_session_id"]!.GetValue<string>());
            Assert.True(stopRoot["found"]!.GetValue<bool>());
            Assert.True(stopRoot["stopped"]!.GetValue<bool>());
            Assert.Equal("worker", stopRoot["kind"]!.GetValue<string>());
            Assert.Equal("phase5-stopped-codex-cli-worker", stopRoot["actor_identity"]!.GetValue<string>());
            Assert.Equal("repo-worker-automation-stopped", stopRoot["repo_id"]!.GetValue<string>());
            Assert.Equal("active", stopRoot["previous_state"]!.GetValue<string>());
            Assert.Equal("stopped", stopRoot["current_state"]!.GetValue<string>());
            Assert.Equal("worker thread closed", stopRoot["reason"]!.GetValue<string>());

            Assert.Equal(0, sessions.ExitCode);
            Assert.Contains("\"actor_session_id\": \"actor-worker-phase5-stopped\"", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"state\": \"stopped\"", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, events.ExitCode);
            Assert.Contains("actor_session_stopped", events.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, readinessAfterStop.ExitCode);
            var afterRoot = JsonNode.Parse(readinessAfterStop.StandardOutput)!.AsObject();
            Assert.Equal("ready", afterRoot["status"]!.GetValue<string>());
            Assert.True(afterRoot["automation_can_run_now"]!.GetValue<bool>());
            var afterPlan = afterRoot["schedule_tick_plan"]!.AsObject();
            Assert.Equal("blocked", afterPlan["decision"]!.GetValue<string>());
            Assert.False(afterPlan["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(afterPlan["schedule_bound_worker_ready"]!.GetValue<bool>());
            Assert.False(afterPlan["context_receipt_ready"]!.GetValue<bool>());
            Assert.Equal(0, afterPlan["schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, afterPlan["live_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, afterPlan["stale_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal(0, afterPlan["closed_schedule_bound_worker_session_count"]!.GetValue<int>());
            Assert.Equal("not_registered", afterPlan["worker_session_liveness_status"]!.GetValue<string>());
            var scheduleBlocker = Assert.IsType<JsonObject>(Assert.Single(afterPlan["schedule_blockers"]!.AsArray()));
            Assert.Equal("no_schedule_bound_worker_session", scheduleBlocker["code"]!.GetValue<string>());

            Assert.Equal(0, tickPreflightAfterStop.ExitCode);
            var tickRoot = JsonNode.Parse(tickPreflightAfterStop.StandardOutput)!.AsObject();
            Assert.Equal("blocked", tickRoot["status"]!.GetValue<string>());
            Assert.False(tickRoot["schedule_tick_can_dispatch"]!.GetValue<bool>());
            Assert.False(tickRoot["host_dispatch_attempted"]!.GetValue<bool>());
            Assert.Equal("preflight_only", tickRoot["host_dispatch_mode"]!.GetValue<string>());
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Fact]
    public void ActorRepairSessions_ClearsCliHelpTaskResidueThroughHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteStaleActorSessionResidue(sandbox.RootPath);

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var dryRun = ProgramHarness.Run("--repo-root", sandbox.RootPath, "actor", "repair-sessions", "--dry-run");
            var repair = ProgramHarness.Run("--repo-root", sandbox.RootPath, "actor", "repair-sessions");
            var sessions = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions", "--kind", "operator");
            var noOpRepair = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-session-repair", "--dry-run");

            Assert.Equal(0, dryRun.ExitCode);
            Assert.Contains("Repaired sessions: 1", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("--help", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, repair.ExitCode);
            Assert.Contains("Repaired sessions: 1", repair.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, sessions.ExitCode);
            Assert.Contains("\"state\": \"idle\"", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("\"current_task_id\": \"--help\"", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("worker-run-help-residue", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, noOpRepair.ExitCode);
            Assert.Contains("\"repaired_count\": 0", noOpRepair.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorRegister_BindsPlannerAndWorkerSessionsWithoutStartingExecution()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var planner = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "actor",
                "register",
                "--kind",
                "planner",
                "--identity",
                "phase19-planner",
                "--provider-profile",
                "planner-high-context",
                "--scope",
                "planning-only",
                "--reason",
                "phase19-planner-registration");
            var worker = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase19-codex-cli-worker",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--scope",
                "host-dispatch-only",
                "--schedule-binding",
                "worker-evidence-ping",
                "--context-receipt",
                "phase19-context-receipt",
                "--process-id",
                Environment.ProcessId.ToString(),
                "--reason",
                "phase19-worker-registration");
            var planners = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions", "--kind", "planner");
            var workers = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions", "--kind", "worker");
            var roleReadiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "actor-role-readiness");
            var events = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "os-events");

            Assert.Equal(0, planner.ExitCode);
            Assert.Contains("Role boundary: planner_decision_only_no_implementation", planner.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Starts run: False", planner.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, worker.ExitCode);
            Assert.Contains("\"kind\": \"worker\"", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"role_boundary\": \"worker_execution_only_host_dispatch_required\"", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"lease_eligible\": true", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"starts_run\": false", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"issues_lease\": false", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"task_truth_write_allowed\": false", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"schedule_binding_state\": \"registered_projection_only\"", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"callback_wake_authority\": \"host_mediated_worker_callback_projection_only\"", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"schedule_replacement_policy\": \"replace_by_reregistering_actor_session_schedule_binding\"", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"schedule_binding_grants_execution_authority\": false", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"schedule_binding_grants_truth_write_authority\": false", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, planners.ExitCode);
            Assert.Contains("phase19-planner", planners.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"provider_profile\": \"planner-high-context\"", planners.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, workers.ExitCode);
            Assert.Contains("phase19-codex-cli-worker", workers.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"provider_profile\": \"codex-cli\"", workers.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"capability_profile\": \"external-codex-worker\"", workers.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"session_scope\": \"host-dispatch-only\"", workers.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"\"process_id\": {Environment.ProcessId}", workers.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"process_started_at\":", workers.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"current_task_id\": null", workers.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"current_run_id\": null", workers.StandardOutput, StringComparison.Ordinal);
            var actorQuery = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "query", "actors");
            Assert.Equal(0, actorQuery.ExitCode);
            Assert.Contains("\"process_started_at\":", actorQuery.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"process_tracking_status\": \"process_alive\"", actorQuery.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, roleReadiness.ExitCode);
            Assert.Contains("\"process_tracked_schedule_bound_worker_session_ids\": [", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"process_unverified_schedule_bound_worker_session_ids\": []", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"schedule_bound_worker_process_tracking_ready\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_role_binding_ready\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_role_binding_ready_policy\": \"planner_and_worker_actor_sessions_registered_and_live; does_not_prove_worker_automation_dispatch_readiness\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_dispatch_ready\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_dispatch_ready_deprecated\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"explicit_dispatch_ready_replaced_by\": \"explicit_role_binding_ready\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_callback_registration_present\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_callback_projection_ready\": true", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_callback_projection_policy\": \"schedule_bound_worker_requires_process_alive_before_callback_projection_is_ready\"", roleReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, events.ExitCode);
            Assert.Contains("phase19-worker-registration", events.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorRegister_TextSurfaceProjectsScheduleCallbackBoundary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var registration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "actor",
                "register",
                "--kind",
                "worker",
                "--identity",
                "phase7-scheduled-worker",
                "--provider-profile",
                "codex-cli",
                "--scope",
                "host-dispatch-only",
                "--schedule-binding",
                "worker-evidence-ping",
                "--context-receipt",
                "phase7-context-receipt",
                "--reason",
                "phase7-schedule-binding");
            var sessions = ProgramHarness.Run("--repo-root", sandbox.RootPath, "actor", "sessions", "--kind", "worker");

            Assert.Equal(0, registration.ExitCode);
            Assert.Contains("Schedule binding state: registered_projection_only", registration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Callback wake authority: host_mediated_worker_callback_projection_only", registration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Schedule replacement policy: replace_by_reregistering_actor_session_schedule_binding", registration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Schedule grants execution authority: False", registration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Schedule grants truth write authority: False", registration.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, sessions.ExitCode);
            Assert.Contains("binding: schedule=worker-evidence-ping; context_receipt=phase7-context-receipt", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("phase7-scheduled-worker", sessions.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorFallbackPolicy_ProjectsMissingRolesAndExplicitSessionReadiness()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var initial = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-session-fallback-policy");
            var planner = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "actor",
                "register",
                "--kind",
                "planner",
                "--identity",
                "phase8-planner",
                "--scope",
                "planning-only",
                "--reason",
                "phase8-planner-registration");
            var worker = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase8-codex-cli-worker",
                "--provider-profile",
                "codex-cli",
                "--scope",
                "host-dispatch-only",
                "--reason",
                "phase8-worker-registration");
            var ready = ProgramHarness.Run("--repo-root", sandbox.RootPath, "actor", "fallback-policy");

            Assert.Equal(0, initial.ExitCode);
            Assert.Contains("\"fallback_mode\": \"same_agent_split_role_fallback_allowed_with_evidence\"", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"missing_actor_roles\": [", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"planner\"", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker\"", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"grants_execution_authority\": false", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"grants_truth_write_authority\": false", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"creates_task_queue\": false", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"fallback_run_packet_required\": true", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"role_switch_receipt\"", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"context_receipt\"", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"execution_claim\"", initial.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"review_bundle\"", initial.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, planner.ExitCode);
            Assert.Equal(0, worker.ExitCode);
            Assert.Equal(0, ready.ExitCode);
            Assert.Contains("Fallback mode: explicit_actor_sessions_ready", ready.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Fallback allowed: False", ready.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Explicit planner registered: True", ready.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Explicit worker registered: True", ready.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Missing actor roles: (none)", ready.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Grants execution authority: False", ready.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Grants truth write authority: False", ready.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Creates task queue: False", ready.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Fallback run packet required: True", ready.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Required run packet fields: role_switch_receipt, context_receipt, execution_claim, review_bundle", ready.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorSessionHeartbeat_RefreshesLivenessAndCanMarkClosed()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-actor-heartbeat");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase9-heartbeat-worker",
                "--repo-id",
                "repo-actor-heartbeat",
                "--actor-session-id",
                "actor-worker-phase9-heartbeat",
                "--scope",
                "host-dispatch-only",
                "--schedule-binding",
                "worker-evidence-ping",
                "--context-receipt",
                "phase9-initial-receipt",
                "--health",
                "healthy",
                "--reason",
                "phase9 heartbeat registration");
            BackdateScheduleBoundWorkerSessions(sandbox.RootPath, TimeSpan.FromMinutes(25));

            var stalePolicy = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-session-fallback-policy", "--repo-id", "repo-actor-heartbeat");
            var heartbeat = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-heartbeat",
                "--actor-session-id",
                "actor-worker-phase9-heartbeat",
                "--health",
                "healthy",
                "--context-receipt",
                "phase9-refreshed-receipt",
                "--reason",
                "phase9 worker heartbeat");
            var heartbeatText = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "actor",
                "heartbeat",
                "--actor-session-id",
                "actor-worker-phase9-heartbeat",
                "--health",
                "healthy",
                "--context-receipt",
                "phase9-text-receipt",
                "--reason",
                "phase9 text heartbeat");
            var refreshedPolicy = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-session-fallback-policy", "--repo-id", "repo-actor-heartbeat");
            var closedHeartbeat = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-heartbeat",
                "--actor-session-id",
                "actor-worker-phase9-heartbeat",
                "--health",
                "closed",
                "--reason",
                "worker thread closed without stop callback");
            var closedPolicy = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-session-fallback-policy", "--repo-id", "repo-actor-heartbeat");
            var sessionsText = ProgramHarness.Run("--repo-root", sandbox.RootPath, "actor", "sessions", "--kind", "worker");
            var events = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "os-events", "--actor-session-id", "actor-worker-phase9-heartbeat");

            Assert.Equal(0, stalePolicy.ExitCode);
            Assert.Contains("\"stale_actor_session_count\": 1", stalePolicy.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, heartbeat.ExitCode);
            var heartbeatRoot = ParseFirstJsonObject(heartbeat.StandardOutput);
            Assert.Equal("actor-session-heartbeat.v1", heartbeatRoot["schema_version"]!.GetValue<string>());
            Assert.True(heartbeatRoot["found"]!.GetValue<bool>());
            Assert.True(heartbeatRoot["heartbeat_accepted"]!.GetValue<bool>());
            Assert.True(heartbeatRoot["updated"]!.GetValue<bool>());
            Assert.Equal("live", heartbeatRoot["liveness_status"]!.GetValue<string>());
            Assert.Equal("healthy", heartbeatRoot["current_health_posture"]!.GetValue<string>());
            Assert.Equal("phase9-refreshed-receipt", heartbeatRoot["current_context_receipt"]!.GetValue<string>());
            Assert.False(heartbeatRoot["grants_execution_authority"]!.GetValue<bool>());
            Assert.False(heartbeatRoot["grants_truth_write_authority"]!.GetValue<bool>());
            Assert.False(heartbeatRoot["creates_task_queue"]!.GetValue<bool>());

            Assert.Equal(0, heartbeatText.ExitCode);
            Assert.Contains("Actor session heartbeat", heartbeatText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Health: healthy -> healthy", heartbeatText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Context receipt: phase9-refreshed-receipt -> phase9-text-receipt", heartbeatText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Grants execution authority: False", heartbeatText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Grants truth write authority: False", heartbeatText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Creates task queue: False", heartbeatText.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, refreshedPolicy.ExitCode);
            Assert.Contains("\"stale_actor_session_count\": 0", refreshedPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"live_actor_session_count\": 1", refreshedPolicy.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, closedHeartbeat.ExitCode);
            var closedRoot = ParseFirstJsonObject(closedHeartbeat.StandardOutput);
            Assert.Equal("closed", closedRoot["liveness_status"]!.GetValue<string>());
            Assert.Equal("closed", closedRoot["current_health_posture"]!.GetValue<string>());
            Assert.Equal(0, closedPolicy.ExitCode);
            Assert.Contains("\"closed_actor_session_count\": 1", closedPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"live_actor_session_count\": 0", closedPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("api actor-session-stop --actor-session-id actor-worker-phase9-heartbeat --reason actor-thread-not-live", closedPolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, sessionsText.ExitCode);
            Assert.Contains("Actor session liveness: live=0 stale=0 closed=1", sessionsText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("liveness: closed;", sessionsText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("reason=health_closed", sessionsText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("next_action: api actor-session-stop --actor-session-id actor-worker-phase9-heartbeat --reason actor-thread-not-live", sessionsText.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, events.ExitCode);
            Assert.Contains("actor_session_heartbeat", events.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ManualFallbackFailure_ReturnsStrictFallbackRunPacket()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var delegationPolicyPath = Path.Combine(sandbox.RootPath, ".carves-platform", "policies", "delegation.policy.json");
            Directory.CreateDirectory(Path.GetDirectoryName(delegationPolicyPath)!);
            File.WriteAllText(
                delegationPolicyPath,
                """
{
  "version": "1.0",
  "require_inspect_before_execution": true,
  "require_resident_host": true,
  "allow_manual_execution_fallback": true,
  "inspect_commands": [
    "task inspect <task-id>",
    "card inspect <card-id>"
  ],
  "run_commands": [
    "task run <task-id>"
  ]
}
""");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", "T-MISSING-FALLBACK-PACKET", "--manual-fallback");
            var output = result.CombinedOutput;

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("\"accepted\": false", output, StringComparison.Ordinal);
            Assert.Contains("\"manual_fallback\": true", output, StringComparison.Ordinal);
            Assert.Contains("\"fallback_run_packet\": {", output, StringComparison.Ordinal);
            Assert.Contains("\"schema_version\": \"fallback-run-packet.v1\"", output, StringComparison.Ordinal);
            Assert.Contains("\"task_id\": \"T-MISSING-FALLBACK-PACKET\"", output, StringComparison.Ordinal);
            Assert.Contains("\"status\": \"incomplete\"", output, StringComparison.Ordinal);
            Assert.Contains("\"strictly_required\": true", output, StringComparison.Ordinal);
            Assert.Contains("\"closure_blocker_when_incomplete\": true", output, StringComparison.Ordinal);
            Assert.Contains("\"required_receipts\": [", output, StringComparison.Ordinal);
            Assert.Contains("\"role_switch_receipt\"", output, StringComparison.Ordinal);
            Assert.Contains("\"context_receipt\"", output, StringComparison.Ordinal);
            Assert.Contains("\"execution_claim\"", output, StringComparison.Ordinal);
            Assert.Contains("\"review_bundle\"", output, StringComparison.Ordinal);
            Assert.Contains("\"grants_execution_authority\": false", output, StringComparison.Ordinal);
            Assert.Contains("\"grants_truth_write_authority\": false", output, StringComparison.Ordinal);
            Assert.Contains("\"creates_task_queue\": false", output, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorClearSessions_ClearsPersistentSessionCacheByRepoThroughHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-clear-worker",
                "--repo-id",
                "repo-clear-a",
                "--reason",
                "phase-clear-registration");
            ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "planner",
                "--identity",
                "phase-clear-planner",
                "--repo-id",
                "repo-clear-b",
                "--reason",
                "phase-clear-other-repo");

            var dryRun = ProgramHarness.Run("--repo-root", sandbox.RootPath, "actor", "clear-sessions", "--repo-id", "repo-clear-a", "--dry-run");
            var clear = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-session-clear", "--repo-id", "repo-clear-a", "--reason", "clear repo cache before switching project");
            var sessions = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions");
            var events = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "os-events");

            Assert.Equal(0, dryRun.ExitCode);
            Assert.Contains("Actor session clear dry run", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("phase-clear-worker", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, clear.ExitCode);
            Assert.Contains("\"cleared_count\": 1", clear.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("phase-clear-worker", clear.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, sessions.ExitCode);
            Assert.DoesNotContain("phase-clear-worker", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("phase-clear-planner", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, events.ExitCode);
            Assert.Contains("actor_session_cache_cleared", events.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorRegister_SupervisedRegistrationRequiresLaunchTokenAndBindsSupervisorState()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var supervisorRepository = new JsonWorkerSupervisorStateRepository(paths);
        var supervisorService = new WorkerSupervisorStateService(supervisorRepository);
        var launch = supervisorService.RegisterPendingLaunch(
            "repo-supervised-registration",
            "phase-s2-supervised-worker",
            "phase S2 pending supervised worker launch",
            workerInstanceId: "worker-instance-s2",
            hostSessionId: "host-session-s2",
            actorSessionId: "actor-worker-s2",
            providerProfile: "codex-cli",
            capabilityProfile: "external-codex-worker",
            scheduleBinding: "worker-evidence-ping");

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var manualWithSupervisorFields = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-s2-supervised-worker",
                "--repo-id",
                "repo-supervised-registration",
                "--worker-instance-id",
                "worker-instance-s2",
                "--launch-token",
                launch.LaunchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--reason",
                "phase S2 manual registration with supervisor fields should fail");
            var rejected = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-s2-supervised-worker",
                "--repo-id",
                "repo-supervised-registration",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-s2",
                "--launch-token",
                "wrong-token",
                "--process-id",
                Environment.ProcessId.ToString(),
                "--reason",
                "phase S2 wrong token should fail");
            var accepted = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-s2-supervised-worker",
                "--repo-id",
                "repo-supervised-registration",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-s2",
                "--launch-token",
                launch.LaunchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-s2-context",
                "--health",
                "healthy",
                "--reason",
                "phase S2 supervised worker registration");
            var sessions = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-sessions",
                "--kind",
                "worker",
                "--repo-id",
                "repo-supervised-registration");
            var textSessions = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "actor",
                "sessions",
                "--kind",
                "worker",
                "--repo-id",
                "repo-supervised-registration");
            var events = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "os-events",
                "--actor-session-id",
                "actor-worker-s2");

            Assert.NotEqual(0, manualWithSupervisorFields.ExitCode);
            Assert.Contains(
                "Worker supervisor launch fields require --registration-mode supervised",
                manualWithSupervisorFields.CombinedOutput,
                StringComparison.Ordinal);
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains(
                "launch token verification failed",
                rejected.CombinedOutput,
                StringComparison.Ordinal);
            Assert.Equal(0, accepted.ExitCode);
            Assert.Contains("\"operation\": \"register_supervised_worker\"", accepted.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"actor_session_id\": \"actor-worker-s2\"", accepted.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"registration_mode\": \"supervised\"", accepted.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_instance_id\": \"worker-instance-s2\"", accepted.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"\"supervisor_launch_token_id\": \"{launch.LaunchTokenId}\"", accepted.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"supervisor_launch_verified\": true", accepted.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(launch.LaunchToken, accepted.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, sessions.ExitCode);
            Assert.Contains("\"actor_session_id\": \"actor-worker-s2\"", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"registration_mode\": \"supervised\"", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"worker_instance_id\": \"worker-instance-s2\"", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"\"supervisor_launch_token_id\": \"{launch.LaunchTokenId}\"", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(launch.LaunchToken, sessions.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, textSessions.ExitCode);
            Assert.Contains("registration: mode=Supervised; worker_instance=worker-instance-s2", textSessions.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"supervisor_launch_token={launch.LaunchTokenId}", textSessions.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(launch.LaunchToken, textSessions.StandardOutput, StringComparison.Ordinal);

            var supervisorRecord = Assert.Single(supervisorRepository.Load().Entries);
            var persistedSupervisorJson = File.ReadAllText(paths.PlatformWorkerSupervisorStateLiveStateFile);
            Assert.Equal(WorkerSupervisorInstanceState.Running, supervisorRecord.State);
            Assert.Equal("actor-worker-s2", supervisorRecord.ActorSessionId);
            Assert.Equal(Environment.ProcessId, supervisorRecord.ProcessId);
            Assert.NotNull(supervisorRecord.ProcessStartedAt);
            Assert.DoesNotContain(launch.LaunchToken, persistedSupervisorJson, StringComparison.Ordinal);

            Assert.Equal(0, events.ExitCode);
            Assert.Contains("\"reason_code\": \"supervised_actor_session_registered\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"event_kind\": \"worker_supervisor_running\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"reason_code\": \"worker_supervisor_running\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("worker-instance-s2", events.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void WorkerSupervisorLaunch_SurfaceIssuesOneTimeTokenAndProjectsSanitizedState()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var launch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-s3",
                "--identity",
                "phase-s3-supervised-worker",
                "--worker-instance-id",
                "worker-instance-s3",
                "--actor-session-id",
                "actor-worker-s3",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--schedule-binding",
                "worker-evidence-ping",
                "--reason",
                "phase S3 issue supervised launch token");
            var launchRoot = ParseFirstJsonObject(launch.StandardOutput);
            var launchToken = launchRoot["launch_token"]!.GetValue<string>();
            var launchTokenId = launchRoot["launch_token_id"]!.GetValue<string>();
            var persistedAfterLaunch = File.ReadAllText(paths.PlatformWorkerSupervisorStateLiveStateFile);
            var duplicateLaunch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-s3",
                "--identity",
                "phase-s3-replacement-worker",
                "--worker-instance-id",
                "worker-instance-s3",
                "--actor-session-id",
                "actor-worker-s3-replacement",
                "--reason",
                "phase S7 duplicate supervisor launch must fail");
            var persistedAfterDuplicateLaunch = File.ReadAllText(paths.PlatformWorkerSupervisorStateLiveStateFile);
            var instancesBeforeRegistration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-s3");
            var textInstancesBeforeRegistration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "worker",
                "supervisor-instances",
                "--repo-id",
                "repo-supervisor-s3");

            Assert.Equal(0, launch.ExitCode);
            Assert.Equal("worker-supervisor-launch-registration.v1", launchRoot["schema_version"]!.GetValue<string>());
            Assert.Equal("worker_supervisor_launch_registered", launchRoot["outcome"]!.GetValue<string>());
            Assert.Equal("worker-instance-s3", launchRoot["worker_instance_id"]!.GetValue<string>());
            Assert.Equal("repo-supervisor-s3", launchRoot["repo_id"]!.GetValue<string>());
            Assert.Equal("phase-s3-supervised-worker", launchRoot["worker_identity"]!.GetValue<string>());
            Assert.StartsWith("wlt-", launchTokenId, StringComparison.Ordinal);
            Assert.StartsWith($"{launchTokenId}.", launchToken, StringComparison.Ordinal);
            Assert.True(launchRoot["launch_token_returned"]!.GetValue<bool>());
            Assert.Equal("returned_once_not_persisted", launchRoot["launch_token_visibility"]!.GetValue<string>());
            Assert.Contains("--registration-mode supervised", launchRoot["api_registration_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("--worker-instance-id worker-instance-s3", launchRoot["api_registration_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains(launchToken, launchRoot["api_registration_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Equal("registration_only_launch_token", launchRoot["supervisor_capability_mode"]!.GetValue<string>());
            Assert.Equal("host_registered_launch_token_process_not_spawned", launchRoot["process_ownership_status"]!.GetValue<string>());
            Assert.False(launchRoot["host_process_spawned"]!.GetValue<bool>());
            Assert.False(launchRoot["host_process_handle_held"]!.GetValue<bool>());
            Assert.False(launchRoot["std_io_captured"]!.GetValue<bool>());
            Assert.False(launchRoot["immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.True(launchRoot["requires_worker_process_registration"]!.GetValue<bool>());
            Assert.Equal("supervised_actor_registration_process_id_and_start_time", launchRoot["process_proof_source"]!.GetValue<string>());
            Assert.False(launchRoot["starts_run"]!.GetValue<bool>());
            Assert.False(launchRoot["issues_lease"]!.GetValue<bool>());
            Assert.False(launchRoot["grants_execution_authority"]!.GetValue<bool>());
            Assert.False(launchRoot["grants_truth_write_authority"]!.GetValue<bool>());
            Assert.DoesNotContain(launchToken, persistedAfterLaunch, StringComparison.Ordinal);
            Assert.Contains("\"launch_token_hash\"", persistedAfterLaunch, StringComparison.Ordinal);
            Assert.NotEqual(0, duplicateLaunch.ExitCode);
            Assert.Contains(
                "Worker supervisor instance 'worker-instance-s3' already exists with state Requested",
                duplicateLaunch.CombinedOutput,
                StringComparison.Ordinal);
            Assert.DoesNotContain("phase-s3-replacement-worker", persistedAfterDuplicateLaunch, StringComparison.Ordinal);
            Assert.Contains("phase-s3-supervised-worker", persistedAfterDuplicateLaunch, StringComparison.Ordinal);

            Assert.Equal(0, instancesBeforeRegistration.ExitCode);
            var instancesBeforeRoot = ParseFirstJsonObject(instancesBeforeRegistration.StandardOutput);
            Assert.Equal("registration_ready_process_supervision_not_ready", instancesBeforeRoot["supervisor_readiness_posture"]!.GetValue<string>());
            Assert.False(instancesBeforeRoot["host_process_supervision_ready"]!.GetValue<bool>());
            Assert.False(instancesBeforeRoot["immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.Equal(4, instancesBeforeRoot["supervisor_automation_gap_count"]!.GetValue<int>());
            var beforeSurfaceGaps = instancesBeforeRoot["supervisor_automation_gaps"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .ToArray();
            Assert.Contains("host_process_spawn_not_implemented", beforeSurfaceGaps);
            Assert.Contains("host_process_handle_not_held", beforeSurfaceGaps);
            Assert.Contains("stdio_capture_not_available", beforeSurfaceGaps);
            Assert.Contains("immediate_death_detection_not_available", beforeSurfaceGaps);
            var beforeEntry = Assert.IsType<JsonObject>(Assert.Single(instancesBeforeRoot["entries"]!.AsArray()));
            Assert.Equal("requested", beforeEntry["state"]!.GetValue<string>());
            Assert.Equal("worker-instance-s3", beforeEntry["worker_instance_id"]!.GetValue<string>());
            Assert.Equal(launchTokenId, beforeEntry["launch_token_id"]!.GetValue<string>());
            Assert.True(beforeEntry["launch_token_hash_present"]!.GetValue<bool>());
            Assert.False(beforeEntry["raw_launch_token_projected"]!.GetValue<bool>());
            Assert.Equal("raw_token_never_projected_from_state", beforeEntry["launch_token_visibility"]!.GetValue<string>());
            Assert.True(beforeEntry["registration_allowed"]!.GetValue<bool>());
            Assert.False(beforeEntry["recovery_required"]!.GetValue<bool>());
            Assert.Equal("pending_supervised_actor_registration", beforeEntry["lifecycle_status"]!.GetValue<string>());
            Assert.Equal("registration_only_launch_token", beforeEntry["supervisor_capability_mode"]!.GetValue<string>());
            Assert.Equal("host_registered_launch_token_process_not_spawned", beforeEntry["process_ownership_status"]!.GetValue<string>());
            Assert.False(beforeEntry["host_process_spawned"]!.GetValue<bool>());
            Assert.False(beforeEntry["host_process_handle_held"]!.GetValue<bool>());
            Assert.False(beforeEntry["std_io_captured"]!.GetValue<bool>());
            Assert.False(beforeEntry["immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.True(beforeEntry["requires_worker_process_registration"]!.GetValue<bool>());
            Assert.Equal("none_until_supervised_actor_registration", beforeEntry["process_proof_source"]!.GetValue<string>());
            Assert.Equal(4, beforeEntry["supervisor_automation_gap_count"]!.GetValue<int>());
            var beforeEntryGaps = beforeEntry["supervisor_automation_gaps"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .ToArray();
            Assert.Contains("host_process_spawn_not_implemented", beforeEntryGaps);
            Assert.Contains("host_process_handle_not_held", beforeEntryGaps);
            Assert.Contains("stdio_capture_not_available", beforeEntryGaps);
            Assert.Contains("immediate_death_detection_not_available", beforeEntryGaps);
            Assert.Contains("api actor-session-register", beforeEntry["next_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, instancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, textInstancesBeforeRegistration.ExitCode);
            Assert.Contains("Worker supervisor instances: total=1; repo=repo-supervisor-s3", textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Supervisor readiness: posture=registration_ready_process_supervision_not_ready; host_process_supervision_ready=False; immediate_death_detection_ready=False; gap_count=4", textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Supervisor automation gaps: host_process_handle_not_held, host_process_spawn_not_implemented, immediate_death_detection_not_available, stdio_capture_not_available", textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("worker-instance-s3 [Requested]", textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("raw_token_projected=False", textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("supervisor_mode: capability=registration_only_launch_token; process_ownership=host_registered_launch_token_process_not_spawned; host_process_spawned=False; host_handle_held=False; immediate_death_detection=False", textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("process_proof: source=none_until_supervised_actor_registration; stdio_captured=False; stdout=(none); stderr=(none); worker_registration_required=True", textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("automation_gaps: count=4; gaps=host_process_spawn_not_implemented, host_process_handle_not_held, stdio_capture_not_available, immediate_death_detection_not_available", textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("registration_allowed=True; recovery_required=False; status=pending_supervised_actor_registration", textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, textInstancesBeforeRegistration.StandardOutput, StringComparison.Ordinal);

            var registration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-s3-supervised-worker",
                "--repo-id",
                "repo-supervisor-s3",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-s3",
                "--launch-token",
                launchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-s3-context",
                "--health",
                "healthy",
                "--reason",
                "phase S3 supervised worker registration");
            var repeatedRegistration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-s3-supervised-worker",
                "--repo-id",
                "repo-supervisor-s3",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-s3",
                "--launch-token",
                launchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-s3-repeated-context",
                "--health",
                "healthy",
                "--reason",
                "phase S6 old launch token should not register running instance");
            var instancesAfterRegistration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-s3");
            var supervisorEvents = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "os-events");
            var runningEvents = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "os-events",
                "--kind",
                "WorkerSupervisorRunning");

            Assert.Equal(0, registration.ExitCode);
            Assert.Contains("\"registration_mode\": \"supervised\"", registration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"actor_session_id\": \"actor-worker-s3\"", registration.StandardOutput, StringComparison.Ordinal);
            Assert.NotEqual(0, repeatedRegistration.ExitCode);
            Assert.Contains(
                "Worker supervisor instance 'worker-instance-s3' is not registrable because it is Running.",
                repeatedRegistration.CombinedOutput,
                StringComparison.Ordinal);

            Assert.Equal(0, instancesAfterRegistration.ExitCode);
            var instancesAfterRoot = ParseFirstJsonObject(instancesAfterRegistration.StandardOutput);
            Assert.Equal("registration_ready_process_supervision_not_ready", instancesAfterRoot["supervisor_readiness_posture"]!.GetValue<string>());
            Assert.False(instancesAfterRoot["host_process_supervision_ready"]!.GetValue<bool>());
            Assert.False(instancesAfterRoot["immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.Equal(4, instancesAfterRoot["supervisor_automation_gap_count"]!.GetValue<int>());
            var afterEntry = Assert.IsType<JsonObject>(Assert.Single(instancesAfterRoot["entries"]!.AsArray()));
            Assert.Equal("running", afterEntry["state"]!.GetValue<string>());
            Assert.Equal("actor-worker-s3", afterEntry["actor_session_id"]!.GetValue<string>());
            Assert.Equal(Environment.ProcessId, afterEntry["process_id"]!.GetValue<int>());
            Assert.False(afterEntry["registration_allowed"]!.GetValue<bool>());
            Assert.False(afterEntry["recovery_required"]!.GetValue<bool>());
            Assert.Equal("supervised_worker_running", afterEntry["lifecycle_status"]!.GetValue<string>());
            Assert.Equal("registration_only_launch_token", afterEntry["supervisor_capability_mode"]!.GetValue<string>());
            Assert.Equal("worker_process_registered_with_process_identity_proof_no_host_handle", afterEntry["process_ownership_status"]!.GetValue<string>());
            Assert.False(afterEntry["host_process_spawned"]!.GetValue<bool>());
            Assert.False(afterEntry["host_process_handle_held"]!.GetValue<bool>());
            Assert.False(afterEntry["std_io_captured"]!.GetValue<bool>());
            Assert.False(afterEntry["immediate_death_detection_ready"]!.GetValue<bool>());
            Assert.False(afterEntry["requires_worker_process_registration"]!.GetValue<bool>());
            Assert.Equal("actor_session_process_id_and_start_time", afterEntry["process_proof_source"]!.GetValue<string>());
            Assert.Equal(4, afterEntry["supervisor_automation_gap_count"]!.GetValue<int>());
            var afterEntryGaps = afterEntry["supervisor_automation_gaps"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .ToArray();
            Assert.Contains("host_process_spawn_not_implemented", afterEntryGaps);
            Assert.Contains("host_process_handle_not_held", afterEntryGaps);
            Assert.Contains("stdio_capture_not_available", afterEntryGaps);
            Assert.Contains("immediate_death_detection_not_available", afterEntryGaps);
            Assert.Contains("Do not reuse the launch token", afterEntry["next_action"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("api actor-session-heartbeat", afterEntry["next_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.False(afterEntry["raw_launch_token_projected"]!.GetValue<bool>());
            Assert.DoesNotContain(launchToken, instancesAfterRegistration.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, supervisorEvents.ExitCode);
            Assert.Contains("\"event_kind\": \"worker_supervisor_launch_requested\"", supervisorEvents.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"reason_code\": \"worker_supervisor_launch_requested\"", supervisorEvents.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"event_kind\": \"worker_supervisor_running\"", supervisorEvents.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"reason_code\": \"worker_supervisor_running\"", supervisorEvents.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("worker-instance-s3", supervisorEvents.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, supervisorEvents.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, runningEvents.ExitCode);
            Assert.Contains("\"event_kind\": \"worker_supervisor_running\"", runningEvents.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("\"event_kind\": \"worker_supervisor_launch_requested\"", runningEvents.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void WorkerSupervisorInstances_MarksExpiredLaunchTokenNotRegistrable()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var launch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-r1",
                "--identity",
                "phase-r1-supervised-worker",
                "--worker-instance-id",
                "worker-instance-r1",
                "--actor-session-id",
                "actor-worker-r1",
                "--schedule-binding",
                "worker-evidence-ping",
                "--reason",
                "phase R1 launch token expiry surface");
            var launchRoot = ParseFirstJsonObject(launch.StandardOutput);
            var launchToken = launchRoot["launch_token"]!.GetValue<string>();
            var launchTokenId = launchRoot["launch_token_id"]!.GetValue<string>();

            ExpireWorkerSupervisorLaunchToken(sandbox.RootPath, "worker-instance-r1");

            var instances = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-r1");
            var textInstances = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "worker",
                "supervisor-instances",
                "--repo-id",
                "repo-supervisor-r1");
            var rejectedRegistration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-r1-supervised-worker",
                "--repo-id",
                "repo-supervisor-r1",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-r1",
                "--launch-token",
                launchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-r1-context",
                "--health",
                "healthy",
                "--reason",
                "phase R1 expired launch token must not register");

            Assert.Equal(0, launch.ExitCode);
            Assert.Equal(0, instances.ExitCode);
            var instancesRoot = ParseFirstJsonObject(instances.StandardOutput);
            var entry = Assert.IsType<JsonObject>(Assert.Single(instancesRoot["entries"]!.AsArray()));
            Assert.Equal("requested", entry["state"]!.GetValue<string>());
            Assert.Equal("worker-instance-r1", entry["worker_instance_id"]!.GetValue<string>());
            Assert.Equal(launchTokenId, entry["launch_token_id"]!.GetValue<string>());
            Assert.True(entry["launch_token_hash_present"]!.GetValue<bool>());
            Assert.True(entry["launch_token_expired"]!.GetValue<bool>());
            Assert.False(entry["registration_allowed"]!.GetValue<bool>());
            Assert.True(entry["recovery_required"]!.GetValue<bool>());
            Assert.Equal("launch_token_expired_relaunch_required", entry["lifecycle_status"]!.GetValue<string>());
            Assert.False(entry["requires_worker_process_registration"]!.GetValue<bool>());
            Assert.Contains("expired launch token", entry["next_action"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("api worker-supervisor-launch", entry["next_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("<new-worker-instance-id>", entry["next_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.DoesNotContain("api actor-session-register", entry["next_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, instances.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, textInstances.ExitCode);
            Assert.Contains("launch_token: id=", textInstances.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("expired=True; raw_token_projected=False", textInstances.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("registration_allowed=False; recovery_required=True; status=launch_token_expired_relaunch_required", textInstances.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("api worker-supervisor-launch", textInstances.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, textInstances.StandardOutput, StringComparison.Ordinal);

            Assert.NotEqual(0, rejectedRegistration.ExitCode);
            Assert.Contains("launch token verification failed", rejectedRegistration.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, rejectedRegistration.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void WorkerSupervisorArchive_RemovesPendingInstanceAndAllowsGovernedReuse()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var launch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-s14",
                "--identity",
                "phase-s14-supervised-worker",
                "--worker-instance-id",
                "worker-instance-s14",
                "--actor-session-id",
                "actor-worker-s14",
                "--schedule-binding",
                "worker-evidence-ping",
                "--reason",
                "phase S14 launch to archive");
            var launchRoot = ParseFirstJsonObject(launch.StandardOutput);
            var launchToken = launchRoot["launch_token"]!.GetValue<string>();
            var duplicateBeforeArchive = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-s14",
                "--identity",
                "phase-s14-supervised-worker",
                "--worker-instance-id",
                "worker-instance-s14",
                "--reason",
                "phase S14 duplicate before archive");
            var dryRunArchive = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-archive",
                "--worker-instance-id",
                "worker-instance-s14",
                "--dry-run",
                "--reason",
                "phase S14 dry run archive");
            var instancesAfterDryRun = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-s14");
            var archive = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "worker",
                "supervisor-archive",
                "--worker-instance-id",
                "worker-instance-s14",
                "--reason",
                "phase S14 archive pending supervisor");
            var persistedAfterArchive = File.ReadAllText(paths.PlatformWorkerSupervisorStateLiveStateFile);
            var instancesAfterArchive = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-s14");
            var relaunch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-s14",
                "--identity",
                "phase-s14-supervised-worker",
                "--worker-instance-id",
                "worker-instance-s14",
                "--actor-session-id",
                "actor-worker-s14b",
                "--reason",
                "phase S14 relaunch after governed archive");
            var instancesAfterRelaunch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-s14");
            var events = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "os-events",
                "--kind",
                "WorkerSupervisorArchived");

            Assert.Equal(0, launch.ExitCode);
            Assert.StartsWith("wlt-", launchRoot["launch_token_id"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.NotEqual(0, duplicateBeforeArchive.ExitCode);
            Assert.Contains(
                "Worker supervisor instance 'worker-instance-s14' already exists with state Requested",
                duplicateBeforeArchive.CombinedOutput,
                StringComparison.Ordinal);

            Assert.Equal(0, dryRunArchive.ExitCode);
            var dryRunRoot = ParseFirstJsonObject(dryRunArchive.StandardOutput);
            Assert.Equal("worker-supervisor-archive.v1", dryRunRoot["schema_version"]!.GetValue<string>());
            Assert.True(dryRunRoot["dry_run"]!.GetValue<bool>());
            Assert.Equal("worker_supervisor_archive_dry_run", dryRunRoot["outcome"]!.GetValue<string>());
            Assert.False(dryRunRoot["archived"]!.GetValue<bool>());
            Assert.False(dryRunRoot["tombstone_projected"]!.GetValue<bool>());
            Assert.Equal("worker-instance-s14", dryRunRoot["worker_instance_id"]!.GetValue<string>());
            Assert.Equal("requested", dryRunRoot["previous_state"]!.GetValue<string>());
            Assert.False(dryRunRoot["starts_run"]!.GetValue<bool>());
            Assert.False(dryRunRoot["issues_lease"]!.GetValue<bool>());
            Assert.False(dryRunRoot["grants_execution_authority"]!.GetValue<bool>());
            Assert.False(dryRunRoot["grants_truth_write_authority"]!.GetValue<bool>());
            Assert.Contains("worker-supervisor-archive", dryRunRoot["next_command"]!.GetValue<string>(), StringComparison.Ordinal);

            Assert.Equal(0, instancesAfterDryRun.ExitCode);
            var afterDryRunRoot = ParseFirstJsonObject(instancesAfterDryRun.StandardOutput);
            Assert.Equal(1, afterDryRunRoot["total_count"]!.GetValue<int>());
            Assert.Equal("requested", Assert.IsType<JsonObject>(Assert.Single(afterDryRunRoot["entries"]!.AsArray()))["state"]!.GetValue<string>());

            Assert.Equal(0, archive.ExitCode);
            Assert.Contains("Worker supervisor archived", archive.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Archived: True", archive.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Worker instance: worker-instance-s14", archive.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Previous state: Requested", archive.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Archive tombstone: id=worker-supervisor-archive-", archive.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("projected=True", archive.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Grants execution authority: False", archive.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Grants truth write authority: False", archive.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, archive.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"archived_entries\"", persistedAfterArchive, StringComparison.Ordinal);
            Assert.Contains("\"archive_id\": \"worker-supervisor-archive-", persistedAfterArchive, StringComparison.Ordinal);
            Assert.Contains("\"worker_instance_id\": \"worker-instance-s14\"", persistedAfterArchive, StringComparison.Ordinal);
            Assert.Contains("\"previous_state\": \"requested\"", persistedAfterArchive, StringComparison.Ordinal);
            Assert.Contains("\"reason\": \"phase S14 archive pending supervisor\"", persistedAfterArchive, StringComparison.Ordinal);
            Assert.DoesNotContain("\"launch_token_hash\"", persistedAfterArchive, StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, persistedAfterArchive, StringComparison.Ordinal);

            Assert.Equal(0, instancesAfterArchive.ExitCode);
            var afterArchiveRoot = ParseFirstJsonObject(instancesAfterArchive.StandardOutput);
            Assert.Equal(0, afterArchiveRoot["total_count"]!.GetValue<int>());
            Assert.Equal(1, afterArchiveRoot["archived_count"]!.GetValue<int>());
            Assert.Equal("no_supervisor_instances", afterArchiveRoot["supervisor_readiness_posture"]!.GetValue<string>());
            Assert.Equal(1, afterArchiveRoot["supervisor_automation_gap_count"]!.GetValue<int>());
            Assert.Equal(
                "no_supervisor_instances_registered",
                Assert.Single(afterArchiveRoot["supervisor_automation_gaps"]!.AsArray())!.GetValue<string>());
            var archivedEntry = Assert.IsType<JsonObject>(Assert.Single(afterArchiveRoot["archived_entries"]!.AsArray()));
            Assert.StartsWith("worker-supervisor-archive-", archivedEntry["archive_id"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Equal("worker-instance-s14", archivedEntry["worker_instance_id"]!.GetValue<string>());
            Assert.Equal("requested", archivedEntry["previous_state"]!.GetValue<string>());
            Assert.Equal("phase S14 archive pending supervisor", archivedEntry["reason"]!.GetValue<string>());
            Assert.DoesNotContain(launchToken, instancesAfterArchive.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, relaunch.ExitCode);
            var relaunchRoot = ParseFirstJsonObject(relaunch.StandardOutput);
            Assert.Equal("worker_supervisor_launch_registered", relaunchRoot["outcome"]!.GetValue<string>());
            Assert.Equal("worker-instance-s14", relaunchRoot["worker_instance_id"]!.GetValue<string>());
            Assert.DoesNotContain(launchToken, relaunch.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, instancesAfterRelaunch.ExitCode);
            var afterRelaunchRoot = ParseFirstJsonObject(instancesAfterRelaunch.StandardOutput);
            Assert.Equal(1, afterRelaunchRoot["total_count"]!.GetValue<int>());
            Assert.Equal(1, afterRelaunchRoot["archived_count"]!.GetValue<int>());
            Assert.Equal("worker-instance-s14", Assert.IsType<JsonObject>(Assert.Single(afterRelaunchRoot["entries"]!.AsArray()))["worker_instance_id"]!.GetValue<string>());
            Assert.Equal("worker-instance-s14", Assert.IsType<JsonObject>(Assert.Single(afterRelaunchRoot["archived_entries"]!.AsArray()))["worker_instance_id"]!.GetValue<string>());

            Assert.Equal(0, events.ExitCode);
            Assert.Contains("\"event_kind\": \"worker_supervisor_archived\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"reason_code\": \"worker_supervisor_archived\"", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("worker-supervisor-archive-", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("worker-instance-s14", events.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, events.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorSessionStop_MarksBoundSupervisorStoppedBeforeArchive()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var launch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-s15",
                "--identity",
                "phase-s15-supervised-worker",
                "--worker-instance-id",
                "worker-instance-s15",
                "--actor-session-id",
                "actor-worker-s15",
                "--schedule-binding",
                "worker-evidence-ping",
                "--reason",
                "phase S15 launch before explicit stop");
            var launchRoot = ParseFirstJsonObject(launch.StandardOutput);
            var launchToken = launchRoot["launch_token"]!.GetValue<string>();
            var registration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-s15-supervised-worker",
                "--repo-id",
                "repo-supervisor-s15",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-s15",
                "--launch-token",
                launchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-s15-context",
                "--health",
                "healthy",
                "--reason",
                "phase S15 supervised registration");
            var archiveWhileRunning = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-archive",
                "--worker-instance-id",
                "worker-instance-s15",
                "--reason",
                "phase S15 archive must not stop a running worker");
            var stop = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-stop",
                "--actor-session-id",
                "actor-worker-s15",
                "--reason",
                "phase S15 explicit supervised worker stop");
            var instancesAfterStop = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-s15");
            var archiveAfterStop = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-archive",
                "--worker-instance-id",
                "worker-instance-s15",
                "--reason",
                "phase S15 archive stopped supervised worker");
            var instancesAfterArchive = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-s15");
            var relaunch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-s15",
                "--identity",
                "phase-s15-supervised-worker",
                "--worker-instance-id",
                "worker-instance-s15",
                "--actor-session-id",
                "actor-worker-s15b",
                "--reason",
                "phase S15 relaunch after stopped supervisor archive");
            var stoppedEvents = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "os-events",
                "--kind",
                "WorkerSupervisorStopped");

            Assert.Equal(0, launch.ExitCode);
            Assert.Equal(0, registration.ExitCode);
            Assert.NotEqual(0, archiveWhileRunning.ExitCode);
            Assert.Contains(
                "cannot be archived while it is Running",
                archiveWhileRunning.CombinedOutput,
                StringComparison.Ordinal);

            Assert.Equal(0, stop.ExitCode);
            var stopRoot = ParseFirstJsonObject(stop.StandardOutput);
            Assert.Equal("actor-session-stop.v1", stopRoot["schema_version"]!.GetValue<string>());
            Assert.True(stopRoot["stopped"]!.GetValue<bool>());
            Assert.Equal("active", stopRoot["previous_state"]!.GetValue<string>());
            Assert.Equal("stopped", stopRoot["current_state"]!.GetValue<string>());
            Assert.True(stopRoot["worker_supervisor_stop_required"]!.GetValue<bool>());
            Assert.True(stopRoot["worker_supervisor_stop_synchronized"]!.GetValue<bool>());
            Assert.Equal("supervisor_stopped_before_actor_session_stop", stopRoot["worker_supervisor_synchronization_status"]!.GetValue<string>());
            Assert.Equal("worker-instance-s15", stopRoot["worker_supervisor_instance_id"]!.GetValue<string>());
            Assert.Equal("running", stopRoot["worker_supervisor_previous_state"]!.GetValue<string>());
            Assert.Equal("stopped", stopRoot["worker_supervisor_current_state"]!.GetValue<string>());

            Assert.Equal(0, instancesAfterStop.ExitCode);
            var instancesAfterStopRoot = ParseFirstJsonObject(instancesAfterStop.StandardOutput);
            var stoppedEntry = Assert.IsType<JsonObject>(Assert.Single(instancesAfterStopRoot["entries"]!.AsArray()));
            Assert.Equal("stopped", stoppedEntry["state"]!.GetValue<string>());
            Assert.False(stoppedEntry["registration_allowed"]!.GetValue<bool>());
            Assert.True(stoppedEntry["recovery_required"]!.GetValue<bool>());
            Assert.Equal("supervised_worker_stopped_relaunch_required", stoppedEntry["lifecycle_status"]!.GetValue<string>());
            Assert.Equal("worker_process_stopped_no_host_handle", stoppedEntry["process_ownership_status"]!.GetValue<string>());
            Assert.Contains("Do not reuse this supervisor instance", stoppedEntry["next_action"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("api worker-supervisor-launch", stoppedEntry["next_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.DoesNotContain(launchToken, instancesAfterStop.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, archiveAfterStop.ExitCode);
            var archiveRoot = ParseFirstJsonObject(archiveAfterStop.StandardOutput);
            Assert.Equal("worker_supervisor_archived", archiveRoot["outcome"]!.GetValue<string>());
            Assert.True(archiveRoot["archived"]!.GetValue<bool>());
            Assert.Equal("stopped", archiveRoot["previous_state"]!.GetValue<string>());
            Assert.False(archiveRoot["grants_execution_authority"]!.GetValue<bool>());
            Assert.False(archiveRoot["grants_truth_write_authority"]!.GetValue<bool>());

            Assert.Equal(0, instancesAfterArchive.ExitCode);
            var instancesAfterArchiveRoot = ParseFirstJsonObject(instancesAfterArchive.StandardOutput);
            Assert.Equal(0, instancesAfterArchiveRoot["total_count"]!.GetValue<int>());

            Assert.Equal(0, relaunch.ExitCode);
            var relaunchRoot = ParseFirstJsonObject(relaunch.StandardOutput);
            Assert.Equal("worker_supervisor_launch_registered", relaunchRoot["outcome"]!.GetValue<string>());
            Assert.Equal("worker-instance-s15", relaunchRoot["worker_instance_id"]!.GetValue<string>());
            Assert.DoesNotContain(launchToken, relaunch.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, stoppedEvents.ExitCode);
            Assert.Contains("\"event_kind\": \"worker_supervisor_stopped\"", stoppedEvents.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"reason_code\": \"worker_supervisor_stopped\"", stoppedEvents.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("worker-instance-s15", stoppedEvents.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorRegister_DryRunDoesNotPersistSession()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var dryRun = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase19-dry-run-worker",
                "--provider-profile",
                "codex-cli",
                "--dry-run");
            var workers = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions", "--kind", "worker");

            Assert.Equal(0, dryRun.ExitCode);
            Assert.Contains("\"dry_run\": true", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"outcome\": \"actor_session_registration_dry_run\"", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"starts_run\": false", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, workers.ExitCode);
            Assert.DoesNotContain("phase19-dry-run-worker", workers.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void ActorRegister_SameAgentRoleSwitchLeavesHostEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var planner = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "planner",
                "--identity",
                "phase6-same-agent",
                "--scope",
                "planning-only",
                "--reason",
                "phase6-planner-registration");
            var worker = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase6-same-agent",
                "--scope",
                "host-dispatch-only",
                "--reason",
                "phase6-worker-registration");
            var sessions = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions");
            var events = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "os-events");

            Assert.Equal(0, planner.ExitCode);
            Assert.Contains("\"same_agent_role_switch_detected\": false", planner.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, worker.ExitCode);
            Assert.Contains("\"same_agent_role_switch_detected\": true", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"related_actor_session_ids\": [", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Same-agent role switch evidence", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("not permission for a role to cross its authority boundary", worker.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, sessions.ExitCode);
            Assert.Contains("\"kind\": \"planner\"", sessions.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"kind\": \"worker\"", sessions.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("phase6-same-agent", sessions.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, events.ExitCode);
            Assert.Contains("same_agent_role_switch_evidence", events.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("phase6-worker-registration", events.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void WorkerSupervisorEvents_SurfaceFiltersSupervisorLifecycleEvents()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var launch = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-launch",
                "--repo-id",
                "repo-supervisor-r7",
                "--identity",
                "phase-r7-supervised-worker",
                "--worker-instance-id",
                "worker-instance-r7",
                "--actor-session-id",
                "actor-worker-r7",
                "--provider-profile",
                "codex-cli",
                "--capability-profile",
                "external-codex-worker",
                "--schedule-binding",
                "worker-evidence-ping",
                "--reason",
                "phase R7 issue supervised launch token");
            var launchRoot = ParseFirstJsonObject(launch.StandardOutput);
            var launchToken = launchRoot["launch_token"]!.GetValue<string>();
            var registration = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "actor-session-register",
                "--kind",
                "worker",
                "--identity",
                "phase-r7-supervised-worker",
                "--repo-id",
                "repo-supervisor-r7",
                "--registration-mode",
                "supervised",
                "--worker-instance-id",
                "worker-instance-r7",
                "--launch-token",
                launchToken,
                "--process-id",
                Environment.ProcessId.ToString(),
                "--context-receipt",
                "phase-r7-context",
                "--health",
                "healthy",
                "--reason",
                "phase R7 supervised worker registration");
            var supervisorInstances = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-instances",
                "--repo-id",
                "repo-supervisor-r7");
            var supervisorEvents = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-events",
                "--repo-id",
                "repo-supervisor-r7",
                "--worker-instance-id",
                "worker-instance-r7");
            var actorFilteredEvents = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-events",
                "--actor-session-id",
                "actor-worker-r7");
            var textEvents = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "worker",
                "supervisor-events",
                "--repo-id",
                "repo-supervisor-r7",
                "--worker-instance-id",
                "worker-instance-r7");
            var unrelatedEvents = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-supervisor-events",
                "--worker-instance-id",
                "worker-instance-r7-missing");

            Assert.Equal(0, launch.ExitCode);
            Assert.Equal(0, registration.ExitCode);

            Assert.Equal(0, supervisorInstances.ExitCode);
            var instancesRoot = ParseFirstJsonObject(supervisorInstances.StandardOutput);
            var instanceEntry = Assert.IsType<JsonObject>(Assert.Single(instancesRoot["entries"]!.AsArray()));
            Assert.Equal("worker-instance-r7", instanceEntry["worker_instance_id"]!.GetValue<string>());
            Assert.Equal(2, instanceEntry["lifecycle_event_count"]!.GetValue<int>());
            Assert.Equal("worker_supervisor_running", instanceEntry["last_lifecycle_event_kind"]!.GetValue<string>());
            Assert.Equal("worker_supervisor_running", instanceEntry["last_lifecycle_reason_code"]!.GetValue<string>());
            Assert.NotNull(instanceEntry["last_lifecycle_event_at"]);
            Assert.Contains("api worker-supervisor-events", instanceEntry["lifecycle_events_command"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Contains("--worker-instance-id worker-instance-r7", instanceEntry["lifecycle_events_command"]!.GetValue<string>(), StringComparison.Ordinal);

            Assert.Equal(0, supervisorEvents.ExitCode);
            var root = ParseFirstJsonObject(supervisorEvents.StandardOutput);
            Assert.Equal("worker-supervisor-events.v1", root["schema_version"]!.GetValue<string>());
            Assert.Equal("repo-supervisor-r7", root["repo_filter"]!.GetValue<string>());
            Assert.Equal("worker-instance-r7", root["worker_instance_filter"]!.GetValue<string>());
            Assert.Equal(2, root["event_count"]!.GetValue<int>());
            Assert.Equal(2, root["displayed_event_count"]!.GetValue<int>());
            Assert.False(root["truncated"]!.GetValue<bool>());
            Assert.Equal("operator_os_events_filtered_by_worker_supervisor_lifecycle", root["event_source"]!.GetValue<string>());
            var includedKinds = root["included_event_kinds"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .ToArray();
            Assert.Contains("worker_supervisor_host_process_handle_acquired", includedKinds);
            var eventKinds = root["events"]!.AsArray()
                .Select(item => item!["event_kind"]!.GetValue<string>())
                .ToArray();
            Assert.Contains("worker_supervisor_launch_requested", eventKinds);
            Assert.Contains("worker_supervisor_running", eventKinds);
            foreach (var item in root["events"]!.AsArray().OfType<JsonObject>())
            {
                Assert.Equal("repo-supervisor-r7", item["repo_id"]!.GetValue<string>());
                Assert.Equal("worker-instance-r7", item["worker_instance_id"]!.GetValue<string>());
            }

            Assert.Equal(0, actorFilteredEvents.ExitCode);
            var actorRoot = ParseFirstJsonObject(actorFilteredEvents.StandardOutput);
            Assert.Equal(2, actorRoot["event_count"]!.GetValue<int>());
            Assert.Equal("actor-worker-r7", actorRoot["actor_session_filter"]!.GetValue<string>());

            Assert.Equal(0, textEvents.ExitCode);
            Assert.Contains("Worker supervisor events: total=2; rendered=2; hidden=0; repo=repo-supervisor-r7; worker_instance=worker-instance-r7", textEvents.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("WorkerSupervisorRunning", textEvents.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("WorkerSupervisorLaunchRequested", textEvents.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, unrelatedEvents.ExitCode);
            var unrelatedRoot = ParseFirstJsonObject(unrelatedEvents.StandardOutput);
            Assert.Equal(0, unrelatedRoot["event_count"]!.GetValue<int>());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    private static void StopHost(string repoRoot)
    {
        ProgramHarness.Run("--repo-root", repoRoot, "host", "stop", "actor surface cleanup");
    }

    private static string CreateCodexCliProbeScript()
    {
        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(Path.GetTempPath(), $"carves-codex-cli-probe-{Guid.NewGuid():N}.cmd");
            File.WriteAllText(path, """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  echo {"type":"thread.started","thread_id":"stub-codex-cli-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"dotnet test","exit_code":0,"aggregated_output":"ok","error":""}}
  echo {"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"README.md","kind":"modify"}]}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"- changed_files: README.md\n- contract_items_satisfied: patch_scope_recorded; scope_hygiene\n- tests_run: dotnet test\n- evidence_paths: worker execution artifact\n- known_limitations: none\n- next_recommendation: submit for Host review"}}
  echo {"type":"turn.completed","usage":{"input_tokens":13,"output_tokens":6}}
  exit /b 0
)
exit /b 0
""");
            return path;
        }

        var scriptPath = Path.Combine(Path.GetTempPath(), $"carves-codex-cli-probe-{Guid.NewGuid():N}.sh");
        File.WriteAllText(scriptPath, """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"stub-codex-cli-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"dotnet test","exit_code":0,"aggregated_output":"ok","error":""}}'
  echo '{"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"README.md","kind":"modify"}]}}'
  printf '%s\n' '{"type":"item.completed","item":{"type":"agent_message","text":"- changed_files: README.md\\n- contract_items_satisfied: patch_scope_recorded; scope_hygiene\\n- tests_run: dotnet test\\n- evidence_paths: worker execution artifact\\n- known_limitations: none\\n- next_recommendation: submit for Host review"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":13,"output_tokens":6}}'
  exit 0
fi
exit 0
""");
        File.SetUnixFileMode(scriptPath, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return scriptPath;
    }

    private static JsonObject ParseFirstJsonObject(string output)
    {
        var jsonStart = output.IndexOf('{', StringComparison.Ordinal);
        Assert.True(jsonStart >= 0, output);
        return JsonNode.Parse(output[jsonStart..])!.AsObject();
    }

    private static void WriteNullWorkerPolicy(string policyPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllText(
            policyPath,
            """
{
  "version": "1.0",
  "preferred_backend_id": "null_worker",
  "default_trust_profile_id": "workspace_build_test",
  "allow_routing_fallback": true,
  "fallback_backend_ids": [
    "null_worker"
  ],
  "allowed_backend_ids": [
    "null_worker"
  ]
}
""");
    }

    private static void WriteEnabledRoleGovernancePolicy(string repoRoot)
    {
        var policyPath = Path.Combine(repoRoot, ".carves-platform", "policies", "role-governance.policy.json");
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllText(policyPath, """
{
  "version": "1.0",
  "controlled_mode_default": false,
  "producer_cannot_self_approve": true,
  "reviewer_cannot_approve_same_task": true,
  "default_role_binding": {
    "producer": "planner",
    "executor": "worker",
    "reviewer": "planner",
    "approver": "operator",
    "scope_steward": "operator",
    "policy_owner": "operator"
  },
  "validation_lab_follow_on_lanes": [
    "approval_recovery",
    "controlled_mode_governance"
  ],
  "role_mode": "enabled",
  "planner_worker_split_enabled": true,
  "worker_delegation_enabled": true,
  "scheduler_auto_dispatch_enabled": true
}
""");
    }

    private static void WriteStaleActorSessionResidue(string repoRoot)
    {
        var path = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "sessions", "actor_sessions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var root = new JsonObject
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["schema_version"] = 1,
                ["actor_session_id"] = "actor-operator-residue-operator",
                ["kind"] = "operator",
                ["actor_identity"] = "operator",
                ["repo_id"] = "CARVES.Runtime",
                ["runtime_session_id"] = "default",
                ["state"] = "blocked",
                ["last_operation_class"] = "delegation",
                ["last_operation"] = "run_task",
                ["current_task_id"] = "--help",
                ["current_run_id"] = "worker-run-help-residue",
                ["last_permission_request_id"] = null,
                ["current_ownership_scope"] = null,
                ["current_ownership_target_id"] = null,
                ["last_reason"] = "Delegated execution ownership released for --help.",
                ["started_at"] = now,
                ["last_seen_at"] = now,
                ["updated_at"] = now,
            }),
        };
        File.WriteAllText(path, root.ToJsonString(new() { WriteIndented = true }));
    }

    private static void WriteManyActorSessionsForTruncation(string repoRoot)
    {
        var path = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "sessions", "actor_sessions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var entries = new JsonArray();
        var now = DateTimeOffset.UtcNow.ToString("O");
        for (var index = 0; index < 51; index++)
        {
            var suffix = index.ToString("000");
            entries.Add(BuildActorSessionNode(
                $"actor-worker-truncation-{suffix}",
                $"truncation-worker-{suffix}",
                "repo-truncation",
                "healthy",
                now));
        }

        entries.Add(BuildActorSessionNode(
            "actor-worker-truncation-999-closed",
            "truncation-worker-closed",
            "repo-truncation",
            "closed",
            now));
        entries.Add(BuildActorSessionNode(
            "actor-worker-other-001",
            "other-worker",
            "repo-other",
            "healthy",
            now));

        File.WriteAllText(path, new JsonObject { ["entries"] = entries }.ToJsonString(new() { WriteIndented = true }));
    }

    private static void WriteHostLivenessActorSessionResidue(string repoRoot)
    {
        var path = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "sessions", "actor_sessions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staleTimestamp = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(25)).ToString("O");
        var root = new JsonObject
        {
            ["entries"] = new JsonArray(BuildActorSessionNode(
                "actor-worker-reconcile-stale",
                "reconcile-stale-worker",
                "repo-reconcile",
                "healthy",
                staleTimestamp)),
        };

        File.WriteAllText(path, root.ToJsonString(new() { WriteIndented = true }));
    }

    private static void WriteHostDeadProcessActorSessionResidue(string repoRoot)
    {
        var path = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "sessions", "actor_sessions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var root = new JsonObject
        {
            ["entries"] = new JsonArray(BuildActorSessionNode(
                "actor-worker-reconcile-dead-process",
                "reconcile-dead-process-worker",
                "repo-process-reconcile",
                "healthy",
                now,
                int.MaxValue)),
        };

        File.WriteAllText(path, root.ToJsonString(new() { WriteIndented = true }));
    }

    private static void WriteProcessMismatchedScheduleWorkerSession(string repoRoot)
    {
        var path = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "sessions", "actor_sessions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var root = new JsonObject
        {
            ["entries"] = new JsonArray(BuildActorSessionNode(
                "actor-worker-process-mismatch",
                "process-mismatch-worker",
                "repo-worker-automation-process-mismatch",
                "healthy",
                now,
                Environment.ProcessId,
                "2000-01-01T00:00:00.0000000+00:00")),
        };

        File.WriteAllText(path, root.ToJsonString(new() { WriteIndented = true }));
    }

    private static void WriteProcessIdentityUnverifiedScheduleWorkerSession(string repoRoot)
    {
        var path = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "sessions", "actor_sessions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var root = new JsonObject
        {
            ["entries"] = new JsonArray(BuildActorSessionNode(
                "actor-worker-process-identity-unverified",
                "process-identity-unverified-worker",
                "repo-worker-automation-identity-unverified",
                "healthy",
                now,
                Environment.ProcessId)),
        };

        File.WriteAllText(path, root.ToJsonString(new() { WriteIndented = true }));
    }

    private static void WriteHeartbeatOnlyScheduleWorkerSession(string repoRoot)
    {
        var path = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "sessions", "actor_sessions.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var root = new JsonObject
        {
            ["entries"] = new JsonArray(BuildActorSessionNode(
                "actor-worker-heartbeat-only-reconcile",
                "heartbeat-only-worker",
                "repo-worker-automation-heartbeat-reconcile",
                "healthy",
                now)),
        };

        File.WriteAllText(path, root.ToJsonString(new() { WriteIndented = true }));
    }

    private static JsonObject BuildActorSessionNode(
        string actorSessionId,
        string actorIdentity,
        string repoId,
        string healthPosture,
        string timestamp,
        int? processId = null,
        string? processStartedAt = null)
    {
        var node = new JsonObject
        {
            ["schema_version"] = 1,
            ["actor_session_id"] = actorSessionId,
            ["kind"] = "worker",
            ["actor_identity"] = actorIdentity,
            ["repo_id"] = repoId,
            ["runtime_session_id"] = null,
            ["state"] = "active",
            ["provider_profile"] = "codex-cli",
            ["capability_profile"] = "external-codex-worker",
            ["session_scope"] = "host-dispatch-only",
            ["budget_profile"] = null,
            ["schedule_binding"] = "worker-evidence-ping",
            ["last_context_receipt"] = "phase18-context",
            ["lease_eligible"] = true,
            ["health_posture"] = healthPosture,
            ["last_operation_class"] = "actor_session_registration",
            ["last_operation"] = "register_worker",
            ["current_task_id"] = null,
            ["current_run_id"] = null,
            ["last_permission_request_id"] = null,
            ["current_ownership_scope"] = null,
            ["current_ownership_target_id"] = null,
            ["last_reason"] = "phase18 actor session fixture",
            ["started_at"] = timestamp,
            ["last_seen_at"] = timestamp,
            ["updated_at"] = timestamp,
        };
        if (processId is not null)
        {
            node["process_id"] = processId.Value;
        }
        if (!string.IsNullOrWhiteSpace(processStartedAt))
        {
            node["process_started_at"] = processStartedAt;
        }

        return node;
    }

    private static void BackdateScheduleBoundWorkerSessions(string repoRoot, TimeSpan age)
    {
        var path = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "sessions", "actor_sessions.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var staleTimestamp = DateTimeOffset.UtcNow.Subtract(age).ToString("O");
        foreach (var entry in root["entries"]!.AsArray().OfType<JsonObject>())
        {
            var kind = entry["kind"]?.GetValue<string>() ?? string.Empty;
            var scheduleBinding = entry["schedule_binding"]?.GetValue<string>();
            if (!string.Equals(kind, "worker", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(scheduleBinding))
            {
                continue;
            }

            entry["last_seen_at"] = staleTimestamp;
            entry["updated_at"] = staleTimestamp;
        }

        File.WriteAllText(path, root.ToJsonString(new() { WriteIndented = true }));
    }

    private static void RewriteActorSessionProcessStartedAt(
        string repoRoot,
        string actorSessionId,
        string processStartedAt)
    {
        var path = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "sessions", "actor_sessions.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var rewritten = false;
        foreach (var entry in root["entries"]!.AsArray().OfType<JsonObject>())
        {
            if (!string.Equals(entry["actor_session_id"]?.GetValue<string>(), actorSessionId, StringComparison.Ordinal))
            {
                continue;
            }

            entry["process_started_at"] = processStartedAt;
            rewritten = true;
        }

        Assert.True(rewritten, $"Actor session '{actorSessionId}' was not found.");
        File.WriteAllText(path, root.ToJsonString(new() { WriteIndented = true }));
    }

    private static void ExpireWorkerSupervisorLaunchToken(
        string repoRoot,
        string workerInstanceId)
    {
        var path = ControlPlanePaths.FromRepoRoot(repoRoot).PlatformWorkerSupervisorStateLiveStateFile;
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var expiredAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1)).ToString("O");
        var rewritten = false;
        foreach (var entry in root["entries"]!.AsArray().OfType<JsonObject>())
        {
            if (!string.Equals(entry["worker_instance_id"]?.GetValue<string>(), workerInstanceId, StringComparison.Ordinal))
            {
                continue;
            }

            entry["launch_token_expires_at"] = expiredAt;
            entry["updated_at"] = expiredAt;
            rewritten = true;
        }

        Assert.True(rewritten, $"Worker supervisor instance '{workerInstanceId}' was not found.");
        File.WriteAllText(path, root.ToJsonString(new() { WriteIndented = true }));
    }
}
