using System.Reflection;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class HostClientSurfaceTests
{
    [Fact]
    public void BareCommand_WithoutHost_RequiresExplicitHostEnsure()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("No resident host is running", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("host ensure --json", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void HostStart_BareCommandAndStatusUseHandshakeAndThinClientRouting()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var discovery = ProgramHarness.Run("--repo-root", sandbox.RootPath);
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "status");

            Assert.Equal(0, start.ExitCode);
            Assert.Contains("CARVES Host summary", start.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, discovery.ExitCode);
            Assert.Contains("CARVES Host summary", discovery.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, status.ExitCode);
            Assert.Contains("Connected to host:", status.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostStatus_CanRejectMissingCapability()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "missing-capability");

            Assert.NotEqual(0, status.ExitCode);
            Assert.Contains("missing required capabilities", status.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostStatus_RequiresAndRecognizesControlPlaneMutationCapability()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "control-plane-mutation");

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, status.ExitCode);
            Assert.Contains("CARVES Host summary", status.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostStatusJson_WithHost_ProjectsSelectedLoopCapabilityReadiness()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var delegatedExecutionStatus = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "host",
                "status",
                "--json",
                "--require-capability",
                "delegated-execution");
            var controlPlaneMutationStatus = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "host",
                "status",
                "--json",
                "--require-capability",
                "control-plane-mutation");

            Assert.True(start.ExitCode == 0, start.CombinedOutput);
            AssertSelectedLoopHostStatus(delegatedExecutionStatus, "delegated-execution");
            AssertSelectedLoopHostStatus(controlPlaneMutationStatus, "control-plane-mutation");
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void PlannerAndReviewMutations_AreClassifiedAsControlPlaneMutations()
    {
        var programType = typeof(Program);
        var resolveHostCapability = programType.GetMethod("ResolveHostCapability", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected Program.ResolveHostCapability.");
        var isControlPlaneMutationCommand = programType.GetMethod("IsControlPlaneMutationCommand", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected Program.IsControlPlaneMutationCommand.");

        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["approve-task", new[] { "T-INTEGRATION-APPROVE-TASK" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["review-task", new[] { "T-INTEGRATION-REVIEW-MUTATION", "complete", "reason" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["approve-review", new[] { "T-INTEGRATION-REVIEW-MUTATION", "reason" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["reject-review", new[] { "T-INTEGRATION-REVIEW-MUTATION", "reason" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["reopen-review", new[] { "T-INTEGRATION-REVIEW-MUTATION", "reason" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["task", new[] { "ingest-result", "T-INTEGRATION-REVIEW-MUTATION" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["supersede-card-tasks", new[] { "CARD-INTEGRATION", "stale", "lineage" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["sync-state", Array.Empty<string>()]));

        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["approve-task", new[] { "T-INTEGRATION-APPROVE-TASK" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["review-task", new[] { "T-INTEGRATION-REVIEW-MUTATION", "complete", "reason" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["approve-review", new[] { "T-INTEGRATION-REVIEW-MUTATION", "reason" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["reject-review", new[] { "T-INTEGRATION-REVIEW-MUTATION", "reason" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["reopen-review", new[] { "T-INTEGRATION-REVIEW-MUTATION", "reason" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["task", new[] { "ingest-result", "T-INTEGRATION-REVIEW-MUTATION" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["supersede-card-tasks", new[] { "CARD-INTEGRATION", "stale", "lineage" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["sync-state", Array.Empty<string>()])!);
        Assert.False((bool)isControlPlaneMutationCommand.Invoke(null, ["actor", new[] { "fallback-policy" }])!);
        Assert.False((bool)isControlPlaneMutationCommand.Invoke(null, ["api", new[] { "actor-session-fallback-policy" }])!);
        Assert.False((bool)isControlPlaneMutationCommand.Invoke(null, ["worker", new[] { "supervisor-events", "--worker-instance-id", "worker-instance-a" }])!);
        Assert.False((bool)isControlPlaneMutationCommand.Invoke(null, ["api", new[] { "worker-supervisor-events", "--worker-instance-id", "worker-instance-a" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["worker", new[] { "supervisor-launch", "--identity", "codex-cli" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["api", new[] { "worker-supervisor-launch", "--identity", "codex-cli" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["worker", new[] { "supervisor-archive", "--worker-instance-id", "worker-instance-a" }])!);
        Assert.True((bool)isControlPlaneMutationCommand.Invoke(null, ["api", new[] { "worker-supervisor-archive", "--worker-instance-id", "worker-instance-a" }])!);
    }

    [Fact]
    public void MinimumStatefulActionFamilies_AnchorSingleGateCoverageForPh2()
    {
        var hostAssembly = typeof(Program).Assembly;
        var catalogType = hostAssembly.GetType("Carves.Runtime.Host.HostCommandRoutingCatalog", throwOnError: true)
            ?? throw new InvalidOperationException("Expected HostCommandRoutingCatalog type.");
        var resolveFamily = catalogType.GetMethod("ResolveMinimumStatefulActionFamily", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected HostCommandRoutingCatalog.ResolveMinimumStatefulActionFamily.");
        var resolveFamilyCapability = catalogType.GetMethod("ResolveRequiredCapabilityForMinimumStatefulActionFamily", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected HostCommandRoutingCatalog.ResolveRequiredCapabilityForMinimumStatefulActionFamily.");

        Assert.Equal("memory-truth-write", resolveFamily.Invoke(null, ["intent", new[] { "accept" }]));
        Assert.Equal("memory-truth-write", resolveFamily.Invoke(null, ["memory", new[] { "promote", "--from-evidence", "E-INTEGRATION" }]));
        Assert.Equal("planning-approval", resolveFamily.Invoke(null, ["approve-card", new[] { "CARD-INTEGRATION", "reason" }]));
        Assert.Equal("planning-approval", resolveFamily.Invoke(null, ["approve-taskgraph-draft", new[] { "TG-CARD-INTEGRATION", "reason" }]));
        Assert.Equal("planning-approval", resolveFamily.Invoke(null, ["card", new[] { "status", "CARD-INTEGRATION", "approved", "reason" }]));
        Assert.Equal("task-execution", resolveFamily.Invoke(null, ["task", new[] { "run", "T-INTEGRATION" }]));
        Assert.Equal("task-execution", resolveFamily.Invoke(null, ["run", new[] { "task", "T-INTEGRATION" }]));
        Assert.Equal("review-writeback", resolveFamily.Invoke(null, ["review-task", new[] { "T-INTEGRATION", "complete", "reason" }]));
        Assert.Equal("review-writeback", resolveFamily.Invoke(null, ["approve-review", new[] { "T-INTEGRATION", "reason" }]));
        Assert.Equal("runtime-state-repair", resolveFamily.Invoke(null, ["actor", new[] { "repair-sessions" }]));
        Assert.Equal("runtime-state-repair", resolveFamily.Invoke(null, ["actor", new[] { "clear-sessions", "--repo-id", "repo-a" }]));
        Assert.Equal("runtime-state-repair", resolveFamily.Invoke(null, ["actor", new[] { "heartbeat", "--actor-session-id", "actor-a" }]));
        Assert.Equal("runtime-state-repair", resolveFamily.Invoke(null, ["api", new[] { "actor-session-repair" }]));
        Assert.Equal("runtime-state-repair", resolveFamily.Invoke(null, ["api", new[] { "actor-session-clear", "--repo-id", "repo-a" }]));
        Assert.Equal("runtime-state-repair", resolveFamily.Invoke(null, ["api", new[] { "actor-session-stop", "--actor-session-id", "actor-a" }]));
        Assert.Equal("runtime-state-repair", resolveFamily.Invoke(null, ["api", new[] { "actor-session-heartbeat", "--actor-session-id", "actor-a" }]));
        Assert.Equal("actor-session-registration", resolveFamily.Invoke(null, ["actor", new[] { "register", "--kind", "worker", "--identity", "codex-cli" }]));
        Assert.Equal("actor-session-registration", resolveFamily.Invoke(null, ["api", new[] { "actor-session-register", "--kind", "worker", "--identity", "codex-cli" }]));
        Assert.Equal("actor-session-registration", resolveFamily.Invoke(null, ["worker", new[] { "supervisor-launch", "--identity", "codex-cli" }]));
        Assert.Equal("actor-session-registration", resolveFamily.Invoke(null, ["api", new[] { "worker-supervisor-launch", "--identity", "codex-cli" }]));
        Assert.Equal("runtime-state-repair", resolveFamily.Invoke(null, ["worker", new[] { "supervisor-archive", "--worker-instance-id", "worker-instance-a" }]));
        Assert.Equal("runtime-state-repair", resolveFamily.Invoke(null, ["api", new[] { "worker-supervisor-archive", "--worker-instance-id", "worker-instance-a" }]));
        Assert.Equal("worker-policy-activation", resolveFamily.Invoke(null, ["worker", new[] { "activate-external-app-cli" }]));
        Assert.Equal("worker-policy-activation", resolveFamily.Invoke(null, ["worker", new[] { "activate-external-codex" }]));
        Assert.Equal("worker-policy-activation", resolveFamily.Invoke(null, ["api", new[] { "worker-external-app-cli-activation" }]));
        Assert.Equal("worker-policy-activation", resolveFamily.Invoke(null, ["api", new[] { "worker-external-codex-activation" }]));
        Assert.Null(resolveFamily.Invoke(null, ["actor", new[] { "fallback-policy" }]));
        Assert.Null(resolveFamily.Invoke(null, ["api", new[] { "actor-session-fallback-policy" }]));
        Assert.Null(resolveFamily.Invoke(null, ["worker", new[] { "supervisor-events", "--worker-instance-id", "worker-instance-a" }]));
        Assert.Null(resolveFamily.Invoke(null, ["api", new[] { "worker-supervisor-events", "--worker-instance-id", "worker-instance-a" }]));
        Assert.Null(resolveFamily.Invoke(null, ["intent", new[] { "draft" }]));
        Assert.Null(resolveFamily.Invoke(null, ["plan", new[] { "export-card", "payload.json" }]));

        Assert.Equal("control-plane-mutation", resolveFamilyCapability.Invoke(null, ["memory-truth-write"]));
        Assert.Equal("control-plane-mutation", resolveFamilyCapability.Invoke(null, ["planning-approval"]));
        Assert.Equal("delegated-execution", resolveFamilyCapability.Invoke(null, ["task-execution"]));
        Assert.Equal("control-plane-mutation", resolveFamilyCapability.Invoke(null, ["review-writeback"]));
        Assert.Equal("control-plane-mutation", resolveFamilyCapability.Invoke(null, ["runtime-state-repair"]));
        Assert.Equal("control-plane-mutation", resolveFamilyCapability.Invoke(null, ["actor-session-registration"]));
        Assert.Equal("control-plane-mutation", resolveFamilyCapability.Invoke(null, ["worker-policy-activation"]));
        Assert.Null(resolveFamilyCapability.Invoke(null, [null]));
    }

    [Fact]
    public void HostRoutingCatalog_ResolvesCardAndTaskAliasCapabilities()
    {
        var programType = typeof(Program);
        var resolveHostCapability = programType.GetMethod("ResolveHostCapability", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected Program.ResolveHostCapability.");

        Assert.Equal("planner-inspect", resolveHostCapability.Invoke(null, ["plan", Array.Empty<string>()]));
        Assert.Equal("planner-inspect", resolveHostCapability.Invoke(null, ["plan", new[] { "status" }]));
        Assert.Equal("planner-inspect", resolveHostCapability.Invoke(null, ["plan", new[] { "packet" }]));
        Assert.Equal("interaction-surface", resolveHostCapability.Invoke(null, ["plan", new[] { "init" }]));
        Assert.Equal("interaction-surface", resolveHostCapability.Invoke(null, ["plan", new[] { "export-card", "payload.json" }]));
        Assert.Equal("interaction-surface", resolveHostCapability.Invoke(null, ["plan", new[] { "export-packet", "packet.json" }]));
        Assert.Equal("interaction-surface", resolveHostCapability.Invoke(null, ["intent", new[] { "draft" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["intent", new[] { "accept" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["plan", new[] { "issue-workspace", "T-INTEGRATION" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["plan", new[] { "submit-workspace", "T-INTEGRATION", "reason" }]));
        Assert.Equal("planner-inspect", resolveHostCapability.Invoke(null, ["plan-card", new[] { "sample.md" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["plan-card", new[] { "sample.md", "--persist" }]));
        Assert.Equal("card-task-inspect", resolveHostCapability.Invoke(null, ["card", new[] { "inspect", "CARD-INTEGRATION" }]));
        Assert.Equal("card-task-inspect", resolveHostCapability.Invoke(null, ["task", new[] { "inspect", "T-INTEGRATION", "--runs" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["card", new[] { "create-draft", "payload.json" }]));
        Assert.Equal("delegated-execution", resolveHostCapability.Invoke(null, ["task", new[] { "run", "T-INTEGRATION" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["task", new[] { "ingest-result", "T-INTEGRATION" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["task", new[] { "retry", "T-INTEGRATION", "reason" }]));
        Assert.Equal("delegated-execution", resolveHostCapability.Invoke(null, ["run", new[] { "task", "T-INTEGRATION" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["actor", new[] { "register", "--kind", "worker", "--identity", "codex-cli" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["api", new[] { "actor-session-register", "--kind", "worker", "--identity", "codex-cli" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["worker", new[] { "supervisor-launch", "--identity", "codex-cli" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["api", new[] { "worker-supervisor-launch", "--identity", "codex-cli" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["worker", new[] { "supervisor-archive", "--worker-instance-id", "worker-instance-a" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["api", new[] { "worker-supervisor-archive", "--worker-instance-id", "worker-instance-a" }]));
        Assert.Equal("worker-inspect", resolveHostCapability.Invoke(null, ["worker", new[] { "supervisor-instances", "--repo-id", "repo-a" }]));
        Assert.Equal("worker-inspect", resolveHostCapability.Invoke(null, ["api", new[] { "worker-supervisor-instances", "--repo-id", "repo-a" }]));
        Assert.Equal("worker-inspect", resolveHostCapability.Invoke(null, ["worker", new[] { "supervisor-events", "--worker-instance-id", "worker-instance-a" }]));
        Assert.Equal("worker-inspect", resolveHostCapability.Invoke(null, ["api", new[] { "worker-supervisor-events", "--worker-instance-id", "worker-instance-a" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["actor", new[] { "clear-sessions", "--repo-id", "repo-a" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["actor", new[] { "heartbeat", "--actor-session-id", "actor-a" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["api", new[] { "actor-session-clear", "--repo-id", "repo-a" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["api", new[] { "actor-session-stop", "--actor-session-id", "actor-a" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["api", new[] { "actor-session-heartbeat", "--actor-session-id", "actor-a" }]));
        Assert.Equal("runtime-status", resolveHostCapability.Invoke(null, ["actor", new[] { "fallback-policy" }]));
        Assert.Equal("runtime-status", resolveHostCapability.Invoke(null, ["api", new[] { "actor-session-fallback-policy" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["worker", new[] { "activate-external-app-cli" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["worker", new[] { "activate-external-codex" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["api", new[] { "worker-external-app-cli-activation" }]));
        Assert.Equal("control-plane-mutation", resolveHostCapability.Invoke(null, ["api", new[] { "worker-external-codex-activation" }]));
    }

    private static void AssertSelectedLoopHostStatus(ProgramHarness.ProgramRunResult status, string requiredCapability)
    {
        Assert.True(status.ExitCode == 0, status.CombinedOutput);
        using var document = ParseJsonOutput(status.StandardOutput);
        var root = document.RootElement;
        var capabilities = root.GetProperty("capabilities")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Equal("carves-host-status.v1", root.GetProperty("schema_version").GetString());
        Assert.True(root.GetProperty("host_running").GetBoolean());
        Assert.Equal(requiredCapability, root.GetProperty("required_capability").GetString());
        Assert.Contains(requiredCapability, capabilities);
        Assert.Equal("connection_routing_observability", root.GetProperty("gateway_role").GetString());
        Assert.Equal("no_worker_automation_dispatch", root.GetProperty("gateway_automation_boundary").GetString());
        Assert.True(root.GetProperty("host_command_surface_compatible").GetBoolean());
    }

    [Fact]
    public void HostRoutingCatalog_LocksFallbackNoticeAndDelegatedExecutionBehavior()
    {
        var hostAssembly = typeof(Program).Assembly;
        var catalogType = hostAssembly.GetType("Carves.Runtime.Host.HostCommandRoutingCatalog", throwOnError: true)
            ?? throw new InvalidOperationException("Expected HostCommandRoutingCatalog type.");
        var shouldEmitFallbackNotice = catalogType.GetMethod("ShouldEmitFallbackNotice", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected HostCommandRoutingCatalog.ShouldEmitFallbackNotice.");
        var isDelegatedExecution = catalogType.GetMethod("IsDelegatedExecution", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Expected HostCommandRoutingCatalog.IsDelegatedExecution.");

        Assert.True((bool)shouldEmitFallbackNotice.Invoke(null, ["plan"])!);
        Assert.True((bool)shouldEmitFallbackNotice.Invoke(null, ["status"])!);
        Assert.True((bool)shouldEmitFallbackNotice.Invoke(null, ["inspect"])!);
        Assert.True((bool)shouldEmitFallbackNotice.Invoke(null, ["list-cards"])!);
        Assert.False((bool)shouldEmitFallbackNotice.Invoke(null, ["api"])!);
        Assert.False((bool)shouldEmitFallbackNotice.Invoke(null, ["task"])!);
        Assert.False((bool)shouldEmitFallbackNotice.Invoke(null, ["agent"])!);

        Assert.True((bool)isDelegatedExecution.Invoke(null, ["task", new[] { "run", "T-INTEGRATION" }])!);
        Assert.True((bool)isDelegatedExecution.Invoke(null, ["run", new[] { "task", "T-INTEGRATION" }])!);
        Assert.False((bool)isDelegatedExecution.Invoke(null, ["task", new[] { "inspect", "T-INTEGRATION" }])!);
        Assert.False((bool)isDelegatedExecution.Invoke(null, ["run", new[] { "next" }])!);
    }

    [Fact]
    public void SyncState_WithHost_RoutesThroughResidentHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-HOST-SYNC");

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var sync = ProgramHarness.Run("--repo-root", sandbox.RootPath, "sync-state");

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, sync.ExitCode);
            Assert.Contains("Connected to host:", sync.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Synchronized task graph and markdown views", sync.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void LocalHostClient_UsesAcceptedOperationPollingPolicyForControlPlaneMutations()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        File.WriteAllText(
            Path.Combine(sandbox.RootPath, ".carves-platform", "policies", "host-invoke.policy.json"),
            """
{
  "version": "1.0",
  "default_read": {
    "request_timeout_seconds": 5,
    "use_accepted_operation_polling": false,
    "poll_interval_ms": 250,
    "stall_timeout_seconds": 0,
    "max_wait_seconds": 0
  },
  "control_plane_mutation": {
    "request_timeout_seconds": 6,
    "use_accepted_operation_polling": true,
    "poll_interval_ms": 250,
    "base_wait_seconds": 12,
    "stall_timeout_seconds": 11,
    "max_wait_seconds": 17
  },
  "attach_flow": {
    "request_timeout_seconds": 30,
    "use_accepted_operation_polling": false,
    "poll_interval_ms": 250,
    "base_wait_seconds": 0,
    "stall_timeout_seconds": 0,
    "max_wait_seconds": 0
  },
  "delegated_execution": {
    "request_timeout_seconds": 900,
    "use_accepted_operation_polling": false,
    "poll_interval_ms": 250,
    "base_wait_seconds": 0,
    "stall_timeout_seconds": 0,
    "max_wait_seconds": 0
  }
}
""");

        var hostAssembly = typeof(Program).Assembly;
        var clientType = hostAssembly.GetType("Carves.Runtime.Host.LocalHostClient", throwOnError: true)
            ?? throw new InvalidOperationException("Expected LocalHostClient type.");
        var client = Activator.CreateInstance(clientType, sandbox.RootPath)
            ?? throw new InvalidOperationException("Expected LocalHostClient instance.");
        var resolvePolicy = clientType.GetMethod("ResolveInvokePolicy", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected LocalHostClient.ResolveInvokePolicy.");

        var reviewPolicy = resolvePolicy.Invoke(client, ["review-task", new[] { "T-INTEGRATION-REVIEW-MUTATION", "complete", "reason" }])!;
        var approveReviewPolicy = resolvePolicy.Invoke(client, ["approve-review", new[] { "T-INTEGRATION-REVIEW-MUTATION", "reason" }])!;
        var intentAcceptPolicy = resolvePolicy.Invoke(client, ["intent", new[] { "accept" }])!;
        var memoryPromotePolicy = resolvePolicy.Invoke(client, ["memory", new[] { "promote", "--from-evidence", "E-INTEGRATION" }])!;
        var actorRegisterPolicy = resolvePolicy.Invoke(client, ["actor", new[] { "register", "--kind", "worker", "--identity", "codex-cli" }])!;
        var apiActorRegisterPolicy = resolvePolicy.Invoke(client, ["api", new[] { "actor-session-register", "--kind", "worker", "--identity", "codex-cli" }])!;
        var workerSupervisorLaunchPolicy = resolvePolicy.Invoke(client, ["worker", new[] { "supervisor-launch", "--identity", "codex-cli" }])!;
        var apiWorkerSupervisorLaunchPolicy = resolvePolicy.Invoke(client, ["api", new[] { "worker-supervisor-launch", "--identity", "codex-cli" }])!;
        var workerSupervisorArchivePolicy = resolvePolicy.Invoke(client, ["worker", new[] { "supervisor-archive", "--worker-instance-id", "worker-instance-a" }])!;
        var apiWorkerSupervisorArchivePolicy = resolvePolicy.Invoke(client, ["api", new[] { "worker-supervisor-archive", "--worker-instance-id", "worker-instance-a" }])!;
        var workerSupervisorEventsPolicy = resolvePolicy.Invoke(client, ["worker", new[] { "supervisor-events", "--worker-instance-id", "worker-instance-a" }])!;
        var apiWorkerSupervisorEventsPolicy = resolvePolicy.Invoke(client, ["api", new[] { "worker-supervisor-events", "--worker-instance-id", "worker-instance-a" }])!;
        var actorClearPolicy = resolvePolicy.Invoke(client, ["actor", new[] { "clear-sessions", "--repo-id", "repo-a" }])!;
        var apiActorClearPolicy = resolvePolicy.Invoke(client, ["api", new[] { "actor-session-clear", "--repo-id", "repo-a" }])!;
        var apiActorStopPolicy = resolvePolicy.Invoke(client, ["api", new[] { "actor-session-stop", "--actor-session-id", "actor-a" }])!;
        var actorHeartbeatPolicy = resolvePolicy.Invoke(client, ["actor", new[] { "heartbeat", "--actor-session-id", "actor-a" }])!;
        var apiActorHeartbeatPolicy = resolvePolicy.Invoke(client, ["api", new[] { "actor-session-heartbeat", "--actor-session-id", "actor-a" }])!;
        var actorFallbackPolicy = resolvePolicy.Invoke(client, ["actor", new[] { "fallback-policy" }])!;
        var apiActorFallbackPolicy = resolvePolicy.Invoke(client, ["api", new[] { "actor-session-fallback-policy" }])!;
        var externalAppCliWorkerActivationPolicy = resolvePolicy.Invoke(client, ["worker", new[] { "activate-external-app-cli" }])!;
        var workerActivationPolicy = resolvePolicy.Invoke(client, ["worker", new[] { "activate-external-codex" }])!;
        var apiExternalAppCliWorkerActivationPolicy = resolvePolicy.Invoke(client, ["api", new[] { "worker-external-app-cli-activation" }])!;
        var apiWorkerActivationPolicy = resolvePolicy.Invoke(client, ["api", new[] { "worker-external-codex-activation" }])!;
        var approveCardPolicy = resolvePolicy.Invoke(client, ["approve-card", new[] { "CARD-INTEGRATION", "reason" }])!;
        var approveTaskGraphPolicy = resolvePolicy.Invoke(client, ["approve-taskgraph-draft", new[] { "TG-CARD-INTEGRATION", "reason" }])!;
        var issueWorkspacePolicy = resolvePolicy.Invoke(client, ["plan", new[] { "issue-workspace", "T-INTEGRATION" }])!;
        var submitWorkspacePolicy = resolvePolicy.Invoke(client, ["plan", new[] { "submit-workspace", "T-INTEGRATION", "reason" }])!;
        var syncPolicy = resolvePolicy.Invoke(client, ["sync-state", Array.Empty<string>()])!;
        var statusPolicy = resolvePolicy.Invoke(client, ["status", Array.Empty<string>()])!;
        var taskRunPolicy = resolvePolicy.Invoke(client, ["task", new[] { "run", "T-INTEGRATION" }])!;
        var apiThreadStartPolicy = resolvePolicy.Invoke(client, ["api", new[] { "runtime-agent-thread-start" }])!;
        var apiWorkerScheduleTickPolicy = resolvePolicy.Invoke(client, ["api", new[] { "worker-automation-schedule-tick" }])!;
        var apiWorkerScheduleTickDispatchPolicy = resolvePolicy.Invoke(client, ["api", new[] { "worker-automation-schedule-tick", "--dispatch" }])!;
        var inspectThreadStartPolicy = resolvePolicy.Invoke(client, ["inspect", new[] { "runtime-agent-thread-start" }])!;
        var agentStartPolicy = resolvePolicy.Invoke(client, ["agent", new[] { "start" }])!;
        var pilotThreadStartPolicy = resolvePolicy.Invoke(client, ["pilot", new[] { "thread-start" }])!;

        Assert.True((bool)reviewPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(reviewPolicy)!);
        Assert.True((bool)approveReviewPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(approveReviewPolicy)!);
        Assert.True((bool)intentAcceptPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(intentAcceptPolicy)!);
        Assert.True((bool)memoryPromotePolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(memoryPromotePolicy)!);
        Assert.True((bool)actorRegisterPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(actorRegisterPolicy)!);
        Assert.True((bool)apiActorRegisterPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiActorRegisterPolicy)!);
        Assert.True((bool)workerSupervisorLaunchPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(workerSupervisorLaunchPolicy)!);
        Assert.True((bool)apiWorkerSupervisorLaunchPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiWorkerSupervisorLaunchPolicy)!);
        Assert.True((bool)workerSupervisorArchivePolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(workerSupervisorArchivePolicy)!);
        Assert.True((bool)apiWorkerSupervisorArchivePolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiWorkerSupervisorArchivePolicy)!);
        Assert.False((bool)workerSupervisorEventsPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(workerSupervisorEventsPolicy)!);
        Assert.False((bool)apiWorkerSupervisorEventsPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiWorkerSupervisorEventsPolicy)!);
        Assert.True((bool)actorClearPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(actorClearPolicy)!);
        Assert.True((bool)apiActorClearPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiActorClearPolicy)!);
        Assert.True((bool)apiActorStopPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiActorStopPolicy)!);
        Assert.True((bool)actorHeartbeatPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(actorHeartbeatPolicy)!);
        Assert.True((bool)apiActorHeartbeatPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiActorHeartbeatPolicy)!);
        Assert.False((bool)actorFallbackPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(actorFallbackPolicy)!);
        Assert.False((bool)apiActorFallbackPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiActorFallbackPolicy)!);
        Assert.True((bool)externalAppCliWorkerActivationPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(externalAppCliWorkerActivationPolicy)!);
        Assert.True((bool)workerActivationPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(workerActivationPolicy)!);
        Assert.True((bool)apiExternalAppCliWorkerActivationPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiExternalAppCliWorkerActivationPolicy)!);
        Assert.True((bool)apiWorkerActivationPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiWorkerActivationPolicy)!);
        Assert.True((bool)approveCardPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(approveCardPolicy)!);
        Assert.True((bool)approveTaskGraphPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(approveTaskGraphPolicy)!);
        Assert.True((bool)issueWorkspacePolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(issueWorkspacePolicy)!);
        Assert.True((bool)submitWorkspacePolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(submitWorkspacePolicy)!);
        Assert.True((bool)syncPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(syncPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(6), (TimeSpan)reviewPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(reviewPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(6), (TimeSpan)intentAcceptPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(intentAcceptPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(6), (TimeSpan)approveCardPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(approveCardPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(6), (TimeSpan)approveTaskGraphPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(approveTaskGraphPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(6), (TimeSpan)submitWorkspacePolicy.GetType().GetProperty("RequestTimeout")!.GetValue(submitWorkspacePolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(12), (TimeSpan)reviewPolicy.GetType().GetProperty("BaseWait")!.GetValue(reviewPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(17), (TimeSpan)reviewPolicy.GetType().GetProperty("MaxWait")!.GetValue(reviewPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(5), (TimeSpan)actorFallbackPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(actorFallbackPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(5), (TimeSpan)apiActorFallbackPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(apiActorFallbackPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(5), (TimeSpan)workerSupervisorEventsPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(workerSupervisorEventsPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(5), (TimeSpan)apiWorkerSupervisorEventsPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(apiWorkerSupervisorEventsPolicy)!);
        Assert.False((bool)statusPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(statusPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(5), (TimeSpan)statusPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(statusPolicy)!);
        Assert.False((bool)taskRunPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(taskRunPolicy)!);
        Assert.Equal(TimeSpan.FromMinutes(15), (TimeSpan)taskRunPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(taskRunPolicy)!);
        Assert.False((bool)apiThreadStartPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiThreadStartPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(30), (TimeSpan)apiThreadStartPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(apiThreadStartPolicy)!);
        Assert.False((bool)apiWorkerScheduleTickPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiWorkerScheduleTickPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(30), (TimeSpan)apiWorkerScheduleTickPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(apiWorkerScheduleTickPolicy)!);
        Assert.False((bool)apiWorkerScheduleTickDispatchPolicy.GetType().GetProperty("UseAcceptedOperationPolling")!.GetValue(apiWorkerScheduleTickDispatchPolicy)!);
        Assert.Equal(TimeSpan.FromMinutes(15), (TimeSpan)apiWorkerScheduleTickDispatchPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(apiWorkerScheduleTickDispatchPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(30), (TimeSpan)inspectThreadStartPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(inspectThreadStartPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(30), (TimeSpan)agentStartPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(agentStartPolicy)!);
        Assert.Equal(TimeSpan.FromSeconds(30), (TimeSpan)pilotThreadStartPolicy.GetType().GetProperty("RequestTimeout")!.GetValue(pilotThreadStartPolicy)!);
    }

    [Fact]
    public void LocalHostClient_RunTaskAgentTimeoutCanBeCappedByEnvironment()
    {
        using var timeoutScope = DelegatedExecutionTimeoutScope.Apply(agentRequestTimeoutCapSeconds: 25);

        var hostAssembly = typeof(Program).Assembly;
        var clientType = hostAssembly.GetType("Carves.Runtime.Host.LocalHostClient", throwOnError: true)
            ?? throw new InvalidOperationException("Expected LocalHostClient type.");
        var requestType = hostAssembly.GetType("Carves.Runtime.Host.AgentRequestEnvelope", throwOnError: true)
            ?? throw new InvalidOperationException("Expected AgentRequestEnvelope type.");
        var resolveAgentTimeout = clientType.GetMethod("ResolveAgentTimeout", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected LocalHostClient.ResolveAgentTimeout.");
        var request = Activator.CreateInstance(
                          requestType,
                          "REQ-INTEGRATION-RUN-TASK",
                          "request",
                          "run_task",
                          "T-INTEGRATION-HOST-AGENT-TIMEOUT",
                          "operator",
                          "session-default",
                          new JsonObject(),
                          null)
                      ?? throw new InvalidOperationException("Expected AgentRequestEnvelope instance.");

        var timeout = (TimeSpan)resolveAgentTimeout.Invoke(null, [request])!;

        Assert.Equal(TimeSpan.FromSeconds(25), timeout);
    }

    [Fact]
    public void LocalHostClient_ExtendsAggregateSurfaceReadTimeoutForThreadStart()
    {
        var hostAssembly = typeof(Program).Assembly;
        var clientType = hostAssembly.GetType("Carves.Runtime.Host.LocalHostClient", throwOnError: true)
            ?? throw new InvalidOperationException("Expected LocalHostClient type.");
        var resolveSurfaceReadTimeout = clientType.GetMethod("ResolveSurfaceReadTimeout", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected LocalHostClient.ResolveSurfaceReadTimeout.");

        Assert.Equal(TimeSpan.FromSeconds(30), resolveSurfaceReadTimeout.Invoke(null, ["/api/runtime-agent-thread-start"]));
        Assert.Equal(TimeSpan.FromSeconds(30), resolveSurfaceReadTimeout.Invoke(null, ["/inspect/runtime-agent-thread-start?format=json"]));
        Assert.Equal(TimeSpan.FromSeconds(30), resolveSurfaceReadTimeout.Invoke(null, ["/api/worker-automation-schedule-tick"]));
        Assert.Equal(TimeSpan.FromSeconds(30), resolveSurfaceReadTimeout.Invoke(null, ["/inspect/worker-automation-schedule-tick?format=json"]));
        Assert.Equal(TimeSpan.FromSeconds(5), resolveSurfaceReadTimeout.Invoke(null, ["/api/runtime-agent-bootstrap-packet"]));
    }

    [Fact]
    public void WorkerRequestBudgetPolicyService_CanCapDelegatedRequestBudgetFromEnvironment()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "carves-runtime-budget-cap", Guid.NewGuid().ToString("N"));
        var worktreeRoot = Path.Combine(repoRoot, ".carves-worktrees", "T-INTEGRATION-BUDGET-CAP");
        Directory.CreateDirectory(Path.Combine(repoRoot, ".ai"));
        Directory.CreateDirectory(worktreeRoot);
        using var timeoutScope = DelegatedExecutionTimeoutScope.Apply(workerRequestTimeoutCapSeconds: 25);

        try
        {
            var task = new TaskNode
            {
                TaskId = "T-INTEGRATION-BUDGET-CAP",
                Title = "Integration delegated budget cap proof",
                Description = "Caps the delegated worker budget only when the test override is present.",
                TaskType = TaskType.Execution,
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                Scope = ["README.md"],
                Acceptance = ["delegated request budget is capped"],
                Validation = new ValidationPlan
                {
                    Commands =
                    [
                        ["dotnet", "test", "tests/Ordering.UnitTests/Ordering.UnitTests.csproj"],
                    ],
                },
            };
            var service = new WorkerRequestBudgetPolicyService(30);

            var budget = service.Resolve(
                task,
                new WorkerSelectionDecision
                {
                    SelectedBackendId = "codex_cli",
                    SelectedProviderId = "codex",
                    SelectedProviderTimeoutSeconds = 120,
                    SelectedBackendSupportsLongRunningTasks = true,
                },
                repoRoot,
                worktreeRoot);

            Assert.Equal(25, budget.TimeoutSeconds);
            Assert.Contains("timeout_cap=25s", budget.Reasons);
        }
        finally
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void HostAcceptedOperationWaitBudget_ExtendsOnProgressButCapsAtHardMax()
    {
        var hostAssembly = typeof(Program).Assembly;
        var budgetType = hostAssembly.GetType("Carves.Runtime.Host.HostAcceptedOperationWaitBudget", throwOnError: true)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationWaitBudget type.");
        var policyType = hostAssembly.GetType("Carves.Runtime.Host.HostCommandInvokePolicy", throwOnError: true)
            ?? throw new InvalidOperationException("Expected HostCommandInvokePolicy type.");
        var computeInitial = budgetType.GetMethod("ComputeInitialDeadline", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationWaitBudget.ComputeInitialDeadline.");
        var computeAdaptive = budgetType.GetMethod("ComputeAdaptiveDeadline", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationWaitBudget.ComputeAdaptiveDeadline.");
        var policy = Activator.CreateInstance(
            policyType,
            TimeSpan.FromSeconds(5),
            true,
            TimeSpan.FromMilliseconds(250),
            TimeSpan.FromSeconds(12),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20))
            ?? throw new InvalidOperationException("Expected HostCommandInvokePolicy instance.");

        var acceptedAt = new DateTimeOffset(2026, 4, 4, 9, 0, 0, TimeSpan.Zero);
        var initial = (DateTimeOffset)computeInitial.Invoke(null, [acceptedAt, policy])!;
        var extended = (DateTimeOffset)computeAdaptive.Invoke(null, [acceptedAt, acceptedAt.AddSeconds(15), policy])!;
        var capped = (DateTimeOffset)computeAdaptive.Invoke(null, [acceptedAt, acceptedAt.AddSeconds(19), policy])!;

        Assert.Equal(acceptedAt.AddSeconds(12), initial);
        Assert.Equal(acceptedAt.AddSeconds(20), extended);
        Assert.Equal(acceptedAt.AddSeconds(20), capped);
    }

    [Fact]
    public void HostAcceptedOperationStore_TracksSemanticProgressSeparatelyFromHeartbeat()
    {
        var hostAssembly = typeof(Program).Assembly;
        var storeType = hostAssembly.GetType("Carves.Runtime.Host.HostAcceptedOperationStore", throwOnError: true)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore type.");
        var store = Activator.CreateInstance(storeType)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore instance.");
        var accept = storeType.GetMethod(
            "Accept",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(string), typeof(string)],
            null)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.Accept.");
        var recordHeartbeat = storeType.GetMethod("RecordHeartbeat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.RecordHeartbeat.");
        var markDispatching = storeType.GetMethod("MarkDispatching", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.MarkDispatching.");
        var markRunning = storeType.GetMethod("MarkRunning", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.MarkRunning.");
        var tryGet = storeType.GetMethod("TryGet", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.TryGet.");

        var accepted = accept.Invoke(store, ["review-task", null])!;
        var responseType = accepted.GetType();
        var operationId = (string)responseType.GetProperty("OperationId")!.GetValue(accepted)!;
        var acceptedUpdatedAt = (DateTimeOffset)responseType.GetProperty("UpdatedAt")!.GetValue(accepted)!;
        var acceptedProgressAt = (DateTimeOffset)responseType.GetProperty("ProgressAt")!.GetValue(accepted)!;

        IntegrationTestWait.WaitForUtcAdvance(acceptedUpdatedAt, TimeSpan.FromSeconds(1));
        recordHeartbeat.Invoke(store, [operationId]);
        var heartbeatStatus = tryGet.Invoke(store, [operationId])!;
        var heartbeatType = heartbeatStatus.GetType();
        var heartbeatUpdatedAt = (DateTimeOffset)heartbeatType.GetProperty("UpdatedAt")!.GetValue(heartbeatStatus)!;
        var heartbeatProgressAt = (DateTimeOffset)heartbeatType.GetProperty("ProgressAt")!.GetValue(heartbeatStatus)!;

        Assert.Equal("accepted", heartbeatType.GetProperty("ProgressMarker")!.GetValue(heartbeatStatus));
        Assert.True(heartbeatUpdatedAt > acceptedUpdatedAt);
        Assert.Equal(acceptedProgressAt, heartbeatProgressAt);

        IntegrationTestWait.WaitForUtcAdvance(heartbeatProgressAt, TimeSpan.FromSeconds(1));
        markDispatching.Invoke(store, [operationId]);
        var dispatchingStatus = tryGet.Invoke(store, [operationId])!;
        var dispatchingType = dispatchingStatus.GetType();
        var dispatchingProgressAt = (DateTimeOffset)dispatchingType.GetProperty("ProgressAt")!.GetValue(dispatchingStatus)!;

        Assert.Equal("dispatching", dispatchingType.GetProperty("ProgressMarker")!.GetValue(dispatchingStatus));
        Assert.Equal(1, dispatchingType.GetProperty("ProgressOrdinal")!.GetValue(dispatchingStatus));
        Assert.True(dispatchingProgressAt > heartbeatProgressAt);

        IntegrationTestWait.WaitForUtcAdvance(dispatchingProgressAt, TimeSpan.FromSeconds(1));
        markRunning.Invoke(store, [operationId]);
        var runningStatus = tryGet.Invoke(store, [operationId])!;
        var runningType = runningStatus.GetType();

        Assert.Equal("running", runningType.GetProperty("OperationState")!.GetValue(runningStatus));
        Assert.Equal("running", runningType.GetProperty("ProgressMarker")!.GetValue(runningStatus));
        Assert.Equal(2, runningType.GetProperty("ProgressOrdinal")!.GetValue(runningStatus));
    }
}
