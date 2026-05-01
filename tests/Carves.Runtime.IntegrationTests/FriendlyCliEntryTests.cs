using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class FriendlyCliEntryTests
{
    [Fact]
    public void Help_ShowsProductLandingWithoutInternalCommandWall()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Start CARVES in a project:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Visible gateway:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Global shim:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Essential commands:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves up [path] [--json]       # first-use product entry", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves shim                     # global shim guidance only", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves help all                 # full command reference", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves doctor", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("carves pilot <boot|agent-start|guide|status|preflight", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Compatibility aliases:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--cold", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--host", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpAll_ShowsCanonicalVerbFamiliesAndCompatibilityAliases()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "help", "all");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CARVES CLI full command reference", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Canonical commands:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves up [path] [--json]       # first-use product entry", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves shim                     # global shim guidance only", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves init [path] [--json]     # lower-level attach/init primitive", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves doctor", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves inspect", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves plan", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves audit", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves maintain", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves context", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves memory", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves evidence", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves agent <start|context|handoff|bootstrap|trace>", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves search", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves gateway <serve|start|ensure|reconcile|restart|status|doctor|logs|activity|stop|pause|resume>", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Compatibility aliases:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--cold", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--host", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void NoArgs_ShowsProductLandingAndVisibleGatewayBoundary()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath());

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CARVES CLI", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Local AI coding workflow control plane.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Start CARVES in a project:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves up <target-project>", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("then open the target project and say: start CARVES", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Visible gateway:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves gateway", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves gateway serve", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves gateway status", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves status --watch", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("read-only status heartbeat", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Global shim:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves shim", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("not a dashboard requirement", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("not worker execution authority", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("global carves is a locator/dispatcher, not lifecycle truth", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Essential commands:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves help all", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("carves pilot <boot|agent-start|guide|status|preflight", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Not a git repository.", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Shim_DescribesGlobalLocatorWithoutMutatingPathOrAuthority()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "shim");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: carves shim", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Global shim guidance only", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("does not install files or mutate PATH", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CARVES_RUNTIME_ROOT", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("exec \"<runtime_root>/carves\" \"$@\"", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("& \"<runtime_root>/carves\" @args", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves up <target-project>", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".carves/carves agent start --json", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("not lifecycle truth", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("not worker execution authority", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("operator should create the shim explicitly", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Not a git repository.", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ShimHelp_UsesSameGlobalLocatorGuidance()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "help", "shim");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: carves shim", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CARVES_RUNTIME_ROOT", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("not worker execution authority", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void GatewayHelp_DescribesResidentGatewayWithoutWorkerAutomationClaim()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "help", "gateway");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: carves gateway", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway role: resident connection, routing, and observability", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway boundary: it does not dispatch worker automation", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves gateway with no subcommand is the foreground gateway terminal.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("serve [--port <port>] [--interval-ms <milliseconds>] runs the gateway in this terminal and prints gateway requests.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("restart [--json] [--force] [--port <port>] [--interval-ms <milliseconds>] [reason...]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("doctor [--json] [--tail <lines>]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("logs [--json] [--tail <lines>]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("activity [--json] [--tail <lines>] summarizes recent CARVES requests and feedback from gateway logs.", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void UpHelp_PresentsProductStartupEntryWithoutWorkerAutomationClaim()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "help", "up");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: carves up [path] [--json]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("First-use product entry", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("prepare Host readiness", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("human start prompt", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Does not dispatch worker automation", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("require the dashboard", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusHelp_DescribesReadOnlyWatchHeartbeat()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "help", "status");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: carves status [--watch] [--iterations <n>] [--interval-ms <ms>]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Read-only CARVES status", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--watch renders a visible status heartbeat", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("does not start worker execution", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void GatewayStatus_UsesGatewayAliasAndShowsBoundary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly gateway status starts clean");

        var result = CliProgramHarness.RunInDirectory(sandbox.RootPath, "gateway", "status");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Gateway role: connection, routing, and observability only", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway automation boundary: worker automation is controlled separately by role-mode gates", result.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Started resident host process", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void GatewayNoArgs_WhenGatewayAlreadyRunning_UsesForegroundServeBoundary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly gateway no args starts clean");
        var port = ReserveLoopbackPort();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200", "--port", port.ToString());
            var result = CliProgramHarness.RunInDirectory(sandbox.RootPath, "gateway");

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("CARVES gateway serve", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Foreground serve started: False", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Gateway ready: True", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Already running: True", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Gateway role: connection, routing, and observability only", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Gateway automation boundary: worker automation is controlled separately by role-mode gates", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Next action: carves gateway status --json", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly gateway no args cleanup");
        }
    }

    [Fact]
    public void StatusWatch_OneIterationShowsReadableHeartbeatWithoutStartingGateway()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly status watch starts clean");

        var result = CliProgramHarness.RunInDirectory(
            sandbox.RootPath,
            "status",
            "--watch",
            "--iterations",
            "1",
            "--interval-ms",
            "0");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("=== CARVES Status", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Project:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Host:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Host operational state:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Worker: idle", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Next:", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Started resident host process", result.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("task run", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("approve-review", result.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GatewayDoctor_UsesGatewayAliasWithoutStartingHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly gateway doctor starts clean");

        var result = CliProgramHarness.RunInDirectory(sandbox.RootPath, "gateway", "doctor");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("CARVES gateway doctor", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway ready: False", result.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway role: connection, routing, and observability only", result.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Started resident host process", result.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Init_OutsideGitRepo_ShowsNoChangesFirstRunGuidance()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-cli-init-non-git", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var result = CliProgramHarness.RunInDirectory(tempRoot, "--repo-root", tempRoot, "init");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("CARVES init", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Target repo: not_repository_workspace", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Action: no_changes", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("run git init or pass --repo-root <path>", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("init does not create business goals, cards, tasks, or acceptance contracts", result.StandardOutput, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(tempRoot, ".ai")));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Attach_OutsideGitRepo_ShowsFriendlyNextAction()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-cli-non-git", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var result = CliProgramHarness.RunInDirectory(tempRoot, "--repo-root", tempRoot, "attach");

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Not a git repository.", result.StandardError, StringComparison.Ordinal);
            Assert.Contains("run git init or switch to a project folder", result.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Doctor_OutsideGitRepo_SeparatesToolReadinessFromMissingTargetRepo()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-cli-doctor-non-git", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var missingRepo = Path.Combine(tempRoot, "missing-target");
            var result = CliProgramHarness.RunInDirectory(tempRoot, "--repo-root", missingRepo, "doctor");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("CARVES doctor", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Tool readiness: available", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Target repo: not_found", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Target repo path: (none)", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime readiness: not_checked", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Host readiness: not_checked", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Status scope: target_repo_status_only", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Agent handoff: carves agent handoff", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("target_repo_not_found", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("run git init or pass --repo-root <path>", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Not a git repository.", result.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Doctor_FromTargetRepoWithoutRuntime_ProjectsTargetAndHostReadinessSeparately()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", targetRepo.RootPath, "host", "stop", "friendly cli doctor test ensures no resident host");

        var result = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "doctor", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = System.Text.Json.JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("available", root.GetProperty("tool_readiness").GetString());
        Assert.Equal("found", root.GetProperty("target_repo").GetString());
        Assert.Equal(Path.GetFullPath(targetRepo.RootPath), root.GetProperty("target_repo_path").GetString());
        Assert.Equal("missing_runtime", root.GetProperty("target_repo_readiness").GetString());
        Assert.Equal("missing", root.GetProperty("runtime_readiness").GetString());
        Assert.Equal("not_running", root.GetProperty("host_readiness").GetString());
        Assert.Equal("ensure_host", root.GetProperty("host_recommended_action_kind").GetString());
        Assert.Equal("recoverable", root.GetProperty("host_lifecycle_state").GetString());
        Assert.Equal("not_running", root.GetProperty("host_lifecycle_reason").GetString());
        Assert.Equal("target_repo_status_only", root.GetProperty("status_scope").GetString());
        Assert.Equal("carves agent handoff", root.GetProperty("agent_handoff_command").GetString());
        Assert.Equal("carves host ensure --json", root.GetProperty("next_action").GetString());
        Assert.False(root.GetProperty("is_ready").GetBoolean());
        Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "missing_runtime");
        Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "resident_host_not_running");
    }

    [Fact]
    public void Doctor_WhenHostSessionConflictExists_ProjectsConflictAndReconcileNextAction()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");

        var runtimeDirectory = ResolveHostRuntimeDirectory(targetRepo.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "doctor-stale-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            targetRepo.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var result = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "doctor", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("host_session_conflict", root.GetProperty("host_readiness").GetString());
        Assert.Equal("host_session_conflict", root.GetProperty("host_operational_state").GetString());
        Assert.True(root.GetProperty("host_conflict_present").GetBoolean());
        Assert.False(root.GetProperty("host_safe_to_start_new_host").GetBoolean());
        Assert.Equal("reconcile_stale_host", root.GetProperty("host_recommended_action_kind").GetString());
        Assert.Equal("blocked", root.GetProperty("host_lifecycle_state").GetString());
        Assert.Equal("host_session_conflict", root.GetProperty("host_lifecycle_reason").GetString());
        Assert.Contains("host reconcile --replace-stale", root.GetProperty("host_recommended_action").GetString(), StringComparison.Ordinal);
        Assert.Contains("host reconcile --replace-stale", root.GetProperty("next_action").GetString(), StringComparison.Ordinal);
        Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "resident_host_session_conflict");
    }

    [Fact]
    public void MinimumStatefulActionAliases_RequireHostEnsureWhenHostIsMissing()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly cli ph10 host gate probe");
        sandbox.MarkAllTasksCompleted();

        var intentAccept = CliProgramHarness.RunInDirectory(sandbox.RootPath, "intent", "accept");
        var memoryPromote = CliProgramHarness.RunInDirectory(sandbox.RootPath, "memory", "promote", "--from-evidence", "E-PH10");
        var approveCard = CliProgramHarness.RunInDirectory(sandbox.RootPath, "plan", "approve-card", "CARD-PH10", "probe");
        var approveTaskGraph = CliProgramHarness.RunInDirectory(sandbox.RootPath, "plan", "approve-taskgraph", "TG-CARD-PH10", "probe");
        var runTask = CliProgramHarness.RunInDirectory(sandbox.RootPath, "run", "task", "T-PH10");
        var approveReview = CliProgramHarness.RunInDirectory(sandbox.RootPath, "review", "approve", "T-PH10", "probe");

        AssertHostEnsureRequired(intentAccept);
        AssertHostEnsureRequired(memoryPromote);
        AssertHostEnsureRequired(approveCard);
        AssertHostEnsureRequired(approveTaskGraph);
        AssertHostEnsureRequired(runTask);
        AssertHostEnsureRequired(approveReview);
    }

    [Fact]
    public void Init_TargetRepoWithoutHost_DoesNotCreateRuntimeAndProjectsJsonNextAction()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", targetRepo.RootPath, "host", "stop", "friendly cli init test ensures no resident host");

        var result = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "init", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = System.Text.Json.JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("carves-init.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("found", root.GetProperty("target_repo").GetString());
        Assert.Equal("missing_runtime", root.GetProperty("target_repo_readiness").GetString());
        Assert.Equal("missing", root.GetProperty("runtime_readiness_before").GetString());
        Assert.Equal("missing", root.GetProperty("runtime_readiness_after").GetString());
        Assert.Equal("not_running", root.GetProperty("host_readiness").GetString());
        Assert.Equal("no_changes", root.GetProperty("action").GetString());
        Assert.Equal("carves host ensure --json", root.GetProperty("next_action").GetString());
        Assert.False(root.GetProperty("is_initialized").GetBoolean());
        Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "missing_runtime");
        Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "resident_host_not_running");
        Assert.Contains(root.GetProperty("commands").EnumerateArray(), command => command.GetString() == "carves host ensure --json");
        Assert.False(File.Exists(Path.Combine(targetRepo.RootPath, ".ai", "runtime.json")));
    }

    [Fact]
    public void Init_WhenHostSessionConflictExists_ProjectsJsonReconcileNextAction()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");

        var runtimeDirectory = ResolveHostRuntimeDirectory(targetRepo.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "init-stale-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            targetRepo.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var result = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "init", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("host_session_conflict", root.GetProperty("host_readiness").GetString());
        Assert.Equal("carves host reconcile --replace-stale --json", root.GetProperty("next_action").GetString());
        Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "resident_host_session_conflict");
        Assert.Contains(root.GetProperty("commands").EnumerateArray(), command => command.GetString() == "carves host reconcile --replace-stale --json");
    }

    [Fact]
    public void Init_WithWrapperRuntimeRoot_AttachesWithoutResidentHostAndBindsTargetToRuntimeRoot()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "friendly cli wrapper init test ensures no resident host");

        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", runtimeRepo.RootPath);
        try
        {
            var init = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "init", "--json");

            Assert.Equal(0, init.ExitCode);
            using var initDocument = System.Text.Json.JsonDocument.Parse(init.StandardOutput);
            var initRoot = initDocument.RootElement;
            Assert.Equal("runtime_initialized", initRoot.GetProperty("target_repo_readiness").GetString());
            Assert.Equal("initialized", initRoot.GetProperty("runtime_readiness_after").GetString());
            Assert.Equal("not_required_wrapper_runtime_root", initRoot.GetProperty("host_readiness").GetString());
            Assert.Equal("initialized_runtime", initRoot.GetProperty("action").GetString());
            Assert.True(initRoot.GetProperty("is_initialized").GetBoolean());

            var handshakePath = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "attach-handshake.json");
            using var handshakeDocument = System.Text.Json.JsonDocument.Parse(File.ReadAllText(handshakePath));
            Assert.Equal(
                Path.GetFullPath(runtimeRepo.RootPath),
                handshakeDocument.RootElement.GetProperty("request").GetProperty("runtime_root").GetString());

            var manifestPath = Path.Combine(targetRepo.RootPath, ".ai", "runtime.json");
            using var manifestDocument = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Equal(
                Path.GetFullPath(runtimeRepo.RootPath),
                manifestDocument.RootElement.GetProperty("runtime_root").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
        }

        var invocation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "invocation", "--json");

        Assert.Equal(0, invocation.ExitCode);
        using var invocationDocument = System.Text.Json.JsonDocument.Parse(invocation.StandardOutput);
        var invocationRoot = invocationDocument.RootElement;
        Assert.Equal(Path.GetFullPath(runtimeRepo.RootPath), invocationRoot.GetProperty("runtime_document_root").GetString());
        Assert.Equal("attach_handshake_runtime_root", invocationRoot.GetProperty("runtime_document_root_mode").GetString());
        Assert.True(invocationRoot.GetProperty("invocation_contract_complete").GetBoolean());
    }

    [Fact]
    public void Up_FromRuntimeDirectory_AutoEnsuresHostAndMaterializesProjectLocalAgentEntry()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "friendly cli up test ensures no resident host");

        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", null);
        try
        {
            var up = CliProgramHarness.RunInDirectory(runtimeRepo.RootPath, "up", targetRepo.RootPath, "--json");

            Assert.Equal(0, up.ExitCode);
            using var upDocument = JsonDocument.Parse(up.StandardOutput);
            var root = upDocument.RootElement;
            Assert.Equal("carves-up.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal("runtime_initialized", root.GetProperty("target_repo_readiness").GetString());
            Assert.Equal("initialized", root.GetProperty("runtime_readiness_after").GetString());
            Assert.Equal(Path.GetFullPath(runtimeRepo.RootPath), root.GetProperty("runtime_authority_root").GetString());
            Assert.Equal(Path.GetFullPath(runtimeRepo.RootPath), root.GetProperty("host_authority_root").GetString());
            Assert.Contains(root.GetProperty("host_readiness").GetString(), new[] { "connected", "healthy_with_pointer_repair" });
            Assert.True(root.GetProperty("host_auto_ensured").GetBoolean());
            Assert.Equal("newly_attached_git_project", root.GetProperty("target_project_classification").GetString());
            Assert.Equal("carves_up", root.GetProperty("target_classification_owner").GetString());
            Assert.Equal("git_repo_probe_and_attach_result", root.GetProperty("target_classification_source").GetString());
            Assert.False(root.GetProperty("agent_target_classification_allowed").GetBoolean());
            Assert.Equal("attach_missing_runtime", root.GetProperty("target_startup_mode").GetString());
            Assert.Equal("attached_missing_runtime_without_agent_guessing", root.GetProperty("existing_project_handling").GetString());
            Assert.Equal("ready_for_agent_start", root.GetProperty("action").GetString());
            Assert.True(root.GetProperty("is_ready").GetBoolean());
            Assert.True(root.GetProperty("project_local_launcher_exists").GetBoolean());
            Assert.True(root.GetProperty("agent_start_markdown_exists").GetBoolean());
            Assert.True(root.GetProperty("agent_start_json_exists").GetBoolean());
            Assert.Equal("CARVES_START.md", root.GetProperty("visible_agent_start").GetString());
            Assert.True(root.GetProperty("visible_agent_start_exists").GetBoolean());
            Assert.Equal(".carves/carves agent start --json", root.GetProperty("next_action").GetString());
            Assert.Equal(".carves/carves agent start --json", root.GetProperty("agent_start_command").GetString());
            Assert.Contains("start CARVES", root.GetProperty("human_next_action").GetString(), StringComparison.Ordinal);
            Assert.Equal("start CARVES", root.GetProperty("agent_start_prompt").GetString());
            Assert.Contains(
                "Do not plan or edit before that readback.",
                root.GetProperty("agent_start_copy_paste_prompt").GetString(),
                StringComparison.Ordinal);
            Assert.Equal(
                "read .carves/AGENT_START.md, then run .carves/carves agent start --json",
                root.GetProperty("agent_instruction").GetString());
            Assert.Equal("null_worker_current_version_no_api_sdk_worker_execution", root.GetProperty("worker_execution_boundary").GetString());
            Assert.Contains(root.GetProperty("non_authority").EnumerateArray(), item => item.GetString() == "not_dashboard_product_entry");
            Assert.Contains(root.GetProperty("non_authority").EnumerateArray(), item => item.GetString() == "not_global_alias_authority");

            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, ".ai", "runtime.json")));
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, ".carves", "carves")));
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, ".carves", "AGENT_START.md")));
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, ".carves", "agent-start.json")));
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, "CARVES_START.md")));
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, "AGENTS.md")));

            using var agentStart = JsonDocument.Parse(File.ReadAllText(Path.Combine(targetRepo.RootPath, ".carves", "agent-start.json")));
            Assert.Equal(Path.GetFullPath(runtimeRepo.RootPath), agentStart.RootElement.GetProperty("runtime_root").GetString());
            Assert.Equal("CARVES_START.md", agentStart.RootElement.GetProperty("visible_start_file").GetString());
            Assert.Equal(".carves/carves agent start --json", agentStart.RootElement.GetProperty("first_agent_command").GetString());
            Assert.Equal("start CARVES", agentStart.RootElement.GetProperty("human_start_prompt").GetString());
            Assert.Contains(
                "Do not plan or edit before that readback.",
                agentStart.RootElement.GetProperty("copy_paste_prompt").GetString(),
                StringComparison.Ordinal);
            Assert.Equal(
                "read .carves/AGENT_START.md, then run .carves/carves agent start --json",
                agentStart.RootElement.GetProperty("agent_instruction").GetString());
            Assert.Equal("carves_up", agentStart.RootElement.GetProperty("target_classification_owner").GetString());
            Assert.False(agentStart.RootElement.GetProperty("agent_target_classification_allowed").GetBoolean());
            Assert.Contains(
                "do not treat the target as a new project",
                agentStart.RootElement.GetProperty("existing_project_rule").GetString(),
                StringComparison.Ordinal);
            Assert.Contains(
                agentStart.RootElement.GetProperty("visible_gateway_commands").EnumerateArray(),
                command => command.GetString() == ".carves/carves gateway status");
            Assert.Contains(
                agentStart.RootElement.GetProperty("visible_gateway_commands").EnumerateArray(),
                command => command.GetString() == ".carves/carves status --watch --iterations 1 --interval-ms 0");
            Assert.Equal(".carves/carves gateway", agentStart.RootElement.GetProperty("foreground_gateway_command").GetString());
            Assert.Contains(
                "prefer the bound project-local .carves/carves launcher",
                agentStart.RootElement.GetProperty("global_shim_rule").GetString(),
                StringComparison.Ordinal);

            var agentStartMarkdown = File.ReadAllText(Path.Combine(targetRepo.RootPath, ".carves", "AGENT_START.md"));
            Assert.Contains("If the operator says `start CARVES`", agentStartMarkdown, StringComparison.Ordinal);
            Assert.Contains("Copy/Paste Prompt", agentStartMarkdown, StringComparison.Ordinal);
            Assert.Contains("## Is CARVES Running?", agentStartMarkdown, StringComparison.Ordinal);
            Assert.Contains(".carves/carves gateway status", agentStartMarkdown, StringComparison.Ordinal);
            Assert.Contains(".carves/carves status --watch --iterations 1 --interval-ms 0", agentStartMarkdown, StringComparison.Ordinal);
            Assert.Contains("CARVES_START.md", agentStartMarkdown, StringComparison.Ordinal);
            Assert.Contains("Do not classify this project as new or old yourself", agentStartMarkdown, StringComparison.Ordinal);

            var visibleStart = File.ReadAllText(Path.Combine(targetRepo.RootPath, "CARVES_START.md"));
            Assert.Contains("Copy/Paste Prompt", visibleStart, StringComparison.Ordinal);
            Assert.Contains("Do not plan or edit before that readback.", visibleStart, StringComparison.Ordinal);
            Assert.Contains("start CARVES", visibleStart, StringComparison.Ordinal);
            Assert.Contains(".carves/AGENT_START.md", visibleStart, StringComparison.Ordinal);
            Assert.Contains(".carves/carves agent start --json", visibleStart, StringComparison.Ordinal);
            Assert.Contains(".carves/carves gateway status", visibleStart, StringComparison.Ordinal);
            Assert.Contains(".carves/carves status --watch --iterations 1 --interval-ms 0", visibleStart, StringComparison.Ordinal);
            Assert.Contains("Do not use a global `carves` shim as authority", visibleStart, StringComparison.Ordinal);
            Assert.Contains("Do not classify this project as new or old yourself", visibleStart, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
            ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "--force", "friendly cli up test cleanup");
        }
    }

    [Fact]
    public void Up_TextOutputReportsHumanAndAgentNextSteps()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "friendly cli up text output starts clean");

        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", null);
        try
        {
            var text = CliProgramHarness.RunInDirectory(runtimeRepo.RootPath, "up", targetRepo.RootPath);

            Assert.Equal(0, text.ExitCode);
            Assert.Contains("Next for you:", text.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Target classification: newly_attached_git_project", text.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Target startup mode: attach_missing_runtime", text.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("start CARVES", text.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Copy/paste to your agent:", text.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Do not plan or edit before that readback.", text.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next for the agent:", text.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Visible start pointer: CARVES_START.md", text.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("read .carves/AGENT_START.md, then run .carves/carves agent start --json", text.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
            ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "--force", "friendly cli up text output cleanup");
        }
    }

    [Fact]
    public void Up_WhenTargetAlreadyInitializedAndBootstrapFilesAreUncommitted_IsIdempotent()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "friendly cli up idempotent test starts clean");

        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", null);
        try
        {
            var first = CliProgramHarness.RunInDirectory(runtimeRepo.RootPath, "up", targetRepo.RootPath, "--json");
            Assert.Equal(0, first.ExitCode);
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, ".ai", "runtime.json")));
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, "CARVES_START.md")));
            File.WriteAllText(
                Path.Combine(targetRepo.RootPath, ".carves", "AGENT_START.md"),
                "# CARVES Agent Start\n\nOld generated start packet.\n");
            File.WriteAllText(
                Path.Combine(targetRepo.RootPath, ".carves", "agent-start.json"),
                """
{
  "schema_version": "carves.agent_start.v1",
  "human_start_prompt": "start CARVES"
}
""");
            File.WriteAllText(
                Path.Combine(targetRepo.RootPath, "CARVES_START.md"),
                "# Start CARVES\n\nThis file is a visible pointer for coding agents.\n");

            var second = CliProgramHarness.RunInDirectory(runtimeRepo.RootPath, "up", targetRepo.RootPath, "--json");

            Assert.Equal(0, second.ExitCode);
            using var secondDocument = JsonDocument.Parse(second.StandardOutput);
            var root = secondDocument.RootElement;
            Assert.Equal("ready_for_agent_start", root.GetProperty("action").GetString());
            Assert.Equal("initialized", root.GetProperty("runtime_readiness_before").GetString());
            Assert.Equal("initialized", root.GetProperty("runtime_readiness_after").GetString());
            Assert.Equal("existing_carves_project", root.GetProperty("target_project_classification").GetString());
            Assert.Equal("reuse_existing_runtime", root.GetProperty("target_startup_mode").GetString());
            Assert.Equal("preserved_existing_carves_project_no_reinit", root.GetProperty("existing_project_handling").GetString());
            Assert.False(root.GetProperty("agent_target_classification_allowed").GetBoolean());
            Assert.Contains(
                "Do not plan or edit before that readback.",
                root.GetProperty("agent_start_copy_paste_prompt").GetString(),
                StringComparison.Ordinal);
            Assert.True(root.GetProperty("visible_agent_start_exists").GetBoolean());
            Assert.True(root.GetProperty("is_ready").GetBoolean());
            Assert.Equal(".carves/carves agent start --json", root.GetProperty("next_action").GetString());
            Assert.DoesNotContain("attach_failed", second.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("dirty_target", second.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains(
                "Do not plan or edit before that readback.",
                File.ReadAllText(Path.Combine(targetRepo.RootPath, ".carves", "AGENT_START.md")),
                StringComparison.Ordinal);
            Assert.Contains(
                "Do not plan or edit before that readback.",
                File.ReadAllText(Path.Combine(targetRepo.RootPath, ".carves", "agent-start.json")),
                StringComparison.Ordinal);
            Assert.Contains(
                "Do not plan or edit before that readback.",
                File.ReadAllText(Path.Combine(targetRepo.RootPath, "CARVES_START.md")),
                StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
            ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "--force", "friendly cli up idempotent cleanup");
        }
    }

    [Fact]
    public void Up_WhenExistingTargetIsBoundToDifferentRuntimeRoot_RequiresOperatorRebind()
    {
        using var boundRuntimeRepo = RepoSandbox.CreateFromCurrentRepo();
        boundRuntimeRepo.MarkAllTasksCompleted();
        using var selectedRuntimeRepo = GitSandbox.Create();
        selectedRuntimeRepo.WriteFile("CARVES.Runtime.sln", "Microsoft Visual Studio Solution File\n");
        selectedRuntimeRepo.WriteFile("src/CARVES.Runtime.Cli/carves.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        selectedRuntimeRepo.CommitAll("Selected Runtime marker files");
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", boundRuntimeRepo.RootPath, "host", "stop", "friendly cli up rebind test bound runtime clean");

        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", null);
        try
        {
            var first = CliProgramHarness.RunInDirectory(boundRuntimeRepo.RootPath, "up", targetRepo.RootPath, "--json");
            Assert.Equal(0, first.ExitCode);
            var launcherBefore = File.ReadAllText(Path.Combine(targetRepo.RootPath, ".carves", "carves"));

            var second = CliProgramHarness.RunInDirectory(selectedRuntimeRepo.RootPath, "up", targetRepo.RootPath, "--json");

            Assert.Equal(1, second.ExitCode);
            using var document = JsonDocument.Parse(second.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("rebind_required", root.GetProperty("action").GetString());
            Assert.Equal("existing_carves_project", root.GetProperty("target_project_classification").GetString());
            Assert.Equal("blocked_rebind_required", root.GetProperty("target_startup_mode").GetString());
            Assert.Equal("operator_rebind_required_agent_must_stop", root.GetProperty("existing_project_handling").GetString());
            Assert.Equal("runtime_binding_conflicts_with_runtime_authority", root.GetProperty("target_runtime_binding_status").GetString());
            Assert.Equal("attach_handshake_and_runtime_manifest", root.GetProperty("target_runtime_binding_source").GetString());
            Assert.Equal(Path.GetFullPath(boundRuntimeRepo.RootPath), root.GetProperty("target_bound_runtime_root").GetString());
            Assert.Equal(Path.GetFullPath(selectedRuntimeRepo.RootPath), root.GetProperty("runtime_authority_root").GetString());
            Assert.False(root.GetProperty("agent_runtime_rebind_allowed").GetBoolean());
            Assert.False(root.GetProperty("agent_target_classification_allowed").GetBoolean());
            Assert.False(root.GetProperty("host_auto_ensured").GetBoolean());
            Assert.Equal("not_checked", root.GetProperty("host_readiness").GetString());
            Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "runtime_binding_mismatch");
            Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "operator_rebind_required");
            Assert.Contains("operator decision required", root.GetProperty("next_action").GetString(), StringComparison.Ordinal);
            Assert.Contains(Path.GetFullPath(boundRuntimeRepo.RootPath), root.GetProperty("next_action").GetString(), StringComparison.Ordinal);
            Assert.Equal(launcherBefore, File.ReadAllText(Path.Combine(targetRepo.RootPath, ".carves", "carves")));
            Assert.DoesNotContain(Path.GetFullPath(selectedRuntimeRepo.RootPath), File.ReadAllText(Path.Combine(targetRepo.RootPath, ".carves", "carves")), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
            ProgramHarness.Run("--repo-root", boundRuntimeRepo.RootPath, "host", "stop", "--force", "friendly cli up rebind test bound runtime cleanup");
        }
    }

    [Fact]
    public void Up_WithExistingRootAgents_PreservesTargetInstructionsAndReportsSuggestedPatch()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        const string existingRootAgents = "# Target AGENTS\n\nUse target-owned instructions.\n";
        targetRepo.WriteFile("AGENTS.md", existingRootAgents);
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "friendly cli up existing agents test ensures no resident host");

        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", null);
        try
        {
            var up = CliProgramHarness.RunInDirectory(runtimeRepo.RootPath, "up", targetRepo.RootPath, "--json");

            Assert.Equal(0, up.ExitCode);
            using var upDocument = JsonDocument.Parse(up.StandardOutput);
            var root = upDocument.RootElement;
            Assert.Equal("ready_for_agent_start", root.GetProperty("action").GetString());
            Assert.Equal(
                "target_owned_root_agents_preserved_manual_carves_entry_recommended",
                root.GetProperty("root_agents_integration_posture").GetString());
            Assert.Contains(".carves/AGENT_START.md", root.GetProperty("root_agents_suggested_patch").GetString(), StringComparison.Ordinal);
            Assert.Contains(".carves/carves", root.GetProperty("root_agents_suggested_patch").GetString(), StringComparison.Ordinal);
            Assert.True(root.GetProperty("visible_agent_start_exists").GetBoolean());
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, "CARVES_START.md")));
            Assert.Equal(existingRootAgents, File.ReadAllText(Path.Combine(targetRepo.RootPath, "AGENTS.md")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
            ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "--force", "friendly cli up existing agents cleanup");
        }
    }

    [Fact]
    public void Up_WithoutRuntimeAuthorityRoot_BlocksBeforeHostMutation()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", targetRepo.RootPath, "host", "stop", "friendly cli up host gate test ensures no resident host");

        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", null);
        try
        {
            var up = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "up", "--json");

            Assert.Equal(1, up.ExitCode);
            using var upDocument = JsonDocument.Parse(up.StandardOutput);
            var root = upDocument.RootElement;
            Assert.Equal("carves-up.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal("blocked_runtime_authority_missing", root.GetProperty("action").GetString());
            Assert.Equal("not_checked", root.GetProperty("host_readiness").GetString());
            Assert.Equal("run from the CARVES.Runtime folder or pass --runtime-root <path>", root.GetProperty("next_action").GetString());
            Assert.False(root.GetProperty("is_ready").GetBoolean());
            Assert.False(root.GetProperty("host_auto_ensured").GetBoolean());
            Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "runtime_authority_root_missing");
            Assert.False(File.Exists(Path.Combine(targetRepo.RootPath, ".ai", "runtime.json")));
            Assert.False(File.Exists(Path.Combine(targetRepo.RootPath, ".carves", "agent-start.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
        }
    }

    [Fact]
    public void Up_WhenRuntimeHostSessionConflictExists_FailsClosedWithReconcileGuidance()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");

        var runtimeDirectory = ResolveHostRuntimeDirectory(runtimeRepo.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "up-stale-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            runtimeRepo.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", null);
        try
        {
            var up = CliProgramHarness.RunInDirectory(runtimeRepo.RootPath, "up", targetRepo.RootPath, "--json");

            Assert.Equal(1, up.ExitCode);
            using var upDocument = JsonDocument.Parse(up.StandardOutput);
            var root = upDocument.RootElement;
            Assert.Equal("blocked_host_session_conflict", root.GetProperty("action").GetString());
            Assert.Equal(Path.GetFullPath(runtimeRepo.RootPath), root.GetProperty("host_authority_root").GetString());
            Assert.False(root.GetProperty("host_auto_ensured").GetBoolean());
            Assert.Equal("carves host reconcile --replace-stale --json", root.GetProperty("next_action").GetString());
            Assert.Contains(root.GetProperty("gaps").EnumerateArray(), gap => gap.GetString() == "resident_host_session_conflict");
            Assert.False(File.Exists(Path.Combine(targetRepo.RootPath, ".ai", "runtime.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
        }
    }

    [Fact]
    public void ConflictingTransportFlags_ReturnFriendlyParseError()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "--cold", "--host", "status");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Choose either --cold or --host, not both.", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void Attach_WithoutHost_ShowsFriendlyHostEnsureGuidance()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", targetRepo.RootPath, "host", "stop", "friendly cli test ensures no resident host");

        var result = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "attach");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Project: {Path.GetFileName(targetRepo.RootPath)}", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Host: not running", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Minimum onboarding:", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("README.md -> AGENTS.md -> carves inspect runtime-first-run-operator-packet", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves host ensure --json", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Guidance:", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves attach", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void Attach_WhenHostSessionConflictExists_ShowsFriendlyReconcileGuidance()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");

        var runtimeDirectory = ResolveHostRuntimeDirectory(targetRepo.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "attach-stale-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            targetRepo.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var result = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "attach");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Host: session conflict", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Host operational state: host_session_conflict", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves host reconcile --replace-stale --json", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WithHostTransportWithoutHost_ShowsFriendlyHostEnsureGuidance()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", targetRepo.RootPath, "host", "stop", "friendly cli host transport run test ensures no resident host");

        var result = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--host", "run");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("Host not running.", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves host ensure --json", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WithHostTransportWhenHostSessionConflictExists_ShowsFriendlyReconcileGuidance()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");

        var runtimeDirectory = ResolveHostRuntimeDirectory(targetRepo.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "run-stale-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            targetRepo.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var result = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--host", "run");

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("Host session conflict.", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves host reconcile --replace-stale --json", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_WhenHostSessionConflictExists_ShowsFriendlyReconcileGuidance()
    {
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");

        var runtimeDirectory = ResolveHostRuntimeDirectory(targetRepo.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "status-stale-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            targetRepo.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var result = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "status");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Host: session conflict", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Host operational state: host_session_conflict", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Host recommended action: carves host reconcile --replace-stale --json", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Next:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves host reconcile --replace-stale --json", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void HostEnsureJson_StartsOrValidatesResidentHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "--force", "friendly cli host ensure test starts clean");

        try
        {
            var ensure = CliProgramHarness.RunInDirectory(sandbox.RootPath, "host", "ensure", "--json", "--interval-ms", "200");

            Assert.Equal(0, ensure.ExitCode);
            using var document = System.Text.Json.JsonDocument.Parse(ensure.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("carves-host-ensure.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal(Path.GetFullPath(sandbox.RootPath), root.GetProperty("repo_root").GetString());
            Assert.True(root.GetProperty("host_running").GetBoolean());
            Assert.True(root.GetProperty("compatible").GetBoolean());
            Assert.Equal("host ready", root.GetProperty("next_action").GetString());
            Assert.Contains(
                root.GetProperty("host_readiness").GetString(),
                new[] { "started", "connected" });

            var secondEnsure = CliProgramHarness.RunInDirectory(sandbox.RootPath, "host", "ensure", "--json");

            Assert.Equal(0, secondEnsure.ExitCode);
            using var secondDocument = System.Text.Json.JsonDocument.Parse(secondEnsure.StandardOutput);
            Assert.True(secondDocument.RootElement.GetProperty("host_running").GetBoolean());
            Assert.True(secondDocument.RootElement.GetProperty("compatible").GetBoolean());
        }
        finally
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "--force", "friendly cli host ensure cleanup");
        }
    }

    [Fact]
    public void Attach_AndStatus_FromProjectDirectory_PrintFriendlySummaryWithoutRepoRootFlag()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

        try
        {
            var hostStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "attach-flow");
            var attach = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "attach");
            AddSyntheticPendingTask(targetRepo.RootPath, "T-FRIENDLY-CLI-001");
            var status = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "status");

            Assert.True(hostStart.ExitCode is 0 or 1, hostStart.CombinedOutput);
            Assert.True(attach.ExitCode == 0, attach.CombinedOutput);
            Assert.True(status.ExitCode == 0, status.CombinedOutput);
            Assert.Contains($"Project: {Path.GetFileName(targetRepo.RootPath)}", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Host: connected", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime:", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next:", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Guidance:", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves inspect runtime-agent-delivery-readiness", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Dispatchable tasks:", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next dispatchable task:", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next:", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Guidance:", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves inspect runtime-agent-validation-bundle", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Idle reason:", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Worker:", status.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly cli test cleanup");
        }
    }

    [Fact]
    public void Init_WithResidentHost_AttachesRuntimeAndPrintsFormalPlanningNextSteps()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

        try
        {
            var hostStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "attach-flow");
            var init = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "init");

            Assert.True(hostStart.ExitCode is 0 or 1, hostStart.CombinedOutput);
            Assert.True(init.ExitCode == 0, init.CombinedOutput);
            Assert.Contains("CARVES init", init.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Target repo: found", init.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Host readiness: connected", init.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Action: initialized_runtime", init.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves agent handoff", init.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves inspect runtime-first-run-operator-packet", init.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves plan init [candidate-card-id]", init.StandardOutput, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, ".ai", "runtime.json")));
        }
        finally
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly cli init cleanup");
        }
    }


    [Fact]
    public void Inspect_RuntimeFirstRunOperatorPacket_FromProjectDirectory_UsesSingleArgumentPassThrough()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

        try
        {
            var attach = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "attach");
            var inspect = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-first-run-operator-packet");

            Assert.Equal(0, attach.ExitCode);
            Assert.Equal(0, inspect.ExitCode);
            Assert.Contains("Runtime first-run operator packet", inspect.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Packet doc: docs/runtime/runtime-first-run-operator-packet.md", inspect.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Usage: carves inspect <card|task|taskgraph|review|audit|packet> ...", inspect.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly cli inspect cleanup");
        }
    }

    [Fact]
    public void Attach_FromNestedProjectDirectory_ResolvesGitRootWithoutRepoRootFlag()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteSyntheticGitMarker(sandbox.RootPath);

        var nestedDirectory = Path.Combine(sandbox.RootPath, "src", "CARVES.Runtime.Cli");
        var status = CliProgramHarness.RunInDirectory(nestedDirectory, "status");

        Assert.Equal(1, status.ExitCode);
        Assert.Contains($"Project: {Path.GetFileName(sandbox.RootPath)}", status.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain($"Project: {Path.GetFileName(nestedDirectory)}", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Host: not running", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Minimum onboarding:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("README.md -> AGENTS.md -> carves inspect runtime-first-run-operator-packet", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Guidance:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves host ensure --json", status.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RepoLocator_PrefersRuntimeWorkspaceMarkersOverForeignAncestorGitRoot()
    {
        var parentRoot = Path.Combine(Path.GetTempPath(), "carves-cli-parent-git", Guid.NewGuid().ToString("N"));
        var workspaceRoot = Path.Combine(parentRoot, "runtime-sandbox");
        Directory.CreateDirectory(Path.Combine(parentRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, ".ai"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "src"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "tests"));
        File.WriteAllText(Path.Combine(workspaceRoot, "README.md"), "# Runtime Sandbox");
        File.WriteAllText(Path.Combine(workspaceRoot, "AGENTS.md"), "# Runtime Sandbox");
        File.WriteAllText(Path.Combine(workspaceRoot, "RuntimeSandbox.sln"), "Microsoft Visual Studio Solution File, Format Version 12.00");

        try
        {
            var repoLocatorType = typeof(Carves.Runtime.Cli.Program).Assembly.GetType("Carves.Runtime.Cli.RepoLocator", throwOnError: true)
                ?? throw new InvalidOperationException("Expected RepoLocator type.");
            var resolve = repoLocatorType.GetMethod(
                "Resolve",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null,
                [typeof(string), typeof(string)],
                null)
                ?? throw new InvalidOperationException("Expected RepoLocator.Resolve overload.");

            var resolvedRoot = (string?)resolve.Invoke(null, [null, workspaceRoot]);

            Assert.Equal(Path.GetFullPath(workspaceRoot), resolvedRoot);
        }
        finally
        {
            Directory.Delete(parentRoot, recursive: true);
        }
    }

    [Fact]
    public void Attach_FromProjectDirectory_WorksForRepoWithoutTopLevelSrcDirectory()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("App/FriendlyCli.cs", "namespace Friendly.Cli; public sealed class FriendlyCliService { }");
        targetRepo.WriteFile("Lib/FriendlyShared.cs", "namespace Friendly.Lib; public sealed class FriendlyShared { }");
        targetRepo.CommitAll("Initial commit");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

        try
        {
            var attach = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "attach");
            var status = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "status");
            var systemConfigPath = Path.Combine(targetRepo.RootPath, ".ai", "config", "system.json");

            Assert.True(attach.ExitCode == 0, attach.CombinedOutput);
            Assert.True(status.ExitCode == 0, status.CombinedOutput);
            Assert.Contains($"Project: {Path.GetFileName(targetRepo.RootPath)}", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Host: connected", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime:", attach.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("State:", status.StandardOutput, StringComparison.Ordinal);
            Assert.True(File.Exists(systemConfigPath));
        }
        finally
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly cli no-src cleanup");
        }
    }

    [Fact]
    public void InspectCard_WithCanonicalShell_SupportsColdTransportWithoutHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var result = CliProgramHarness.RunInDirectory(sandbox.RootPath, "--cold", "inspect", "card", "CARD-298");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"kind\": \"card\"", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"card_id\": \"CARD-298\"", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyCardAlias_RemainsAvailableThroughCompatibilityLayer()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var result = CliProgramHarness.RunInDirectory(sandbox.RootPath, "--cold", "card", "inspect", "CARD-298");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"card_id\": \"CARD-298\"", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Workbench_TextWatchSurface_IsAvailableThroughCanonicalShell()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-FRIENDLY-WORKBENCH-REVIEW");

        var result = CliProgramHarness.RunInDirectory(
            sandbox.RootPath,
            "--cold",
            "workbench",
            "review",
            "--watch",
            "--iterations",
            "1",
            "--interval-ms",
            "0");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("=== CARVES Workbench", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Surface: review", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Review queue:", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("T-CARD-139-001", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Workbench_Overview_ProjectsAcceptanceContractGapsIntoTextSurface()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-FRIENDLY-WORKBENCH-AC-GAP", includeAcceptanceContract: false);

        var result = CliProgramHarness.RunInDirectory(sandbox.RootPath, "--cold", "workbench", "overview");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Surface: overview", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Acceptance contract gaps: 1", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("acceptance_contract_gaps=1", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("T-FRIENDLY-WORKBENCH-AC-GAP [Pending]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("project a minimum acceptance contract onto task truth before dispatch", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("missing an acceptance contract", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Workbench_WithoutText_PrintsBrowserUrlWhenHostIsRunning()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "workbench");

            var result = CliProgramHarness.RunInDirectory(sandbox.RootPath, "workbench", "review");

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, status.ExitCode);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Workbench:", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("/workbench/review", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "friendly cli workbench cleanup");
        }
    }

    [Fact]
    public void SearchTask_UsesLocalTaskGraphWithoutHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteSyntheticGitMarker(sandbox.RootPath);
        sandbox.AddSyntheticPendingTask("T-FRIENDLY-CLI-SEARCH-001");

        var result = CliProgramHarness.RunInDirectory(sandbox.RootPath, "search", "task", "T-FRIENDLY-CLI-SEARCH-001");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Task search: T-FRIENDLY-CLI-SEARCH-001", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("T-FRIENDLY-CLI-SEARCH-001 [", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Synthetic integration dry-run task", result.StandardOutput, StringComparison.Ordinal);
    }

    private static void WriteSyntheticGitMarker(string repoRoot)
    {
        File.WriteAllText(Path.Combine(repoRoot, ".git"), "gitdir: synthetic-cli-test");
    }

    private static void AddSyntheticPendingTask(string repoRoot, string taskId)
    {
        var graphPath = Path.Combine(repoRoot, ".ai", "tasks", "graph.json");
        if (!File.Exists(graphPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(graphPath)!);
            File.WriteAllText(graphPath, """
{
  "version": 1,
  "updated_at": "2026-04-08T00:00:00Z",
  "cards": [],
  "tasks": []
}
""");
        }

        var graphNode = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        var tasks = graphNode["tasks"]!.AsArray();
        tasks.Add(new System.Text.Json.Nodes.JsonObject
        {
            ["task_id"] = taskId,
            ["title"] = "Friendly CLI synthetic ready task",
            ["status"] = "pending",
            ["priority"] = "P1",
            ["card_id"] = "CARD-FRIENDLY-CLI",
            ["dependencies"] = new System.Text.Json.Nodes.JsonArray(),
            ["node_file"] = $"nodes/{taskId}.json",
        });
        File.WriteAllText(graphPath, graphNode.ToJsonString(new() { WriteIndented = true }));

        var nodePath = Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(nodePath)!);
        var taskNode = new System.Text.Json.Nodes.JsonObject
        {
            ["schema_version"] = 1,
            ["task_id"] = taskId,
            ["title"] = "Friendly CLI synthetic ready task",
            ["description"] = "Ensures friendly CLI status surfaces can see a ready task.",
            ["status"] = "pending",
            ["task_type"] = "execution",
            ["priority"] = "P1",
            ["source"] = "INTEGRATION",
            ["card_id"] = "CARD-FRIENDLY-CLI",
            ["base_commit"] = "abc123",
            ["result_commit"] = null,
            ["dependencies"] = new System.Text.Json.Nodes.JsonArray(),
            ["scope"] = new System.Text.Json.Nodes.JsonArray("README.md"),
            ["acceptance"] = new System.Text.Json.Nodes.JsonArray("friendly cli sees ready task"),
            ["acceptance_contract"] = new System.Text.Json.Nodes.JsonObject
            {
                ["contract_id"] = $"AC-{taskId}",
                ["title"] = $"Acceptance contract for {taskId}",
                ["status"] = "compiled",
                ["owner"] = "planner",
                ["created_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["intent"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["goal"] = "Keep friendly CLI ready-task fixtures aligned with governed execution truth.",
                    ["business_value"] = "Friendly CLI status should only project genuinely dispatchable tasks.",
                },
                ["acceptance_examples"] = new System.Text.Json.Nodes.JsonArray(),
                ["checks"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["unit_tests"] = new System.Text.Json.Nodes.JsonArray(),
                    ["integration_tests"] = new System.Text.Json.Nodes.JsonArray(),
                    ["regression_tests"] = new System.Text.Json.Nodes.JsonArray(),
                    ["policy_checks"] = new System.Text.Json.Nodes.JsonArray(),
                    ["additional_checks"] = new System.Text.Json.Nodes.JsonArray(),
                },
                ["constraints"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["must_not"] = new System.Text.Json.Nodes.JsonArray("keep integration fixture deterministic"),
                    ["architecture"] = new System.Text.Json.Nodes.JsonArray(),
                    ["scope_limit"] = null,
                },
                ["non_goals"] = new System.Text.Json.Nodes.JsonArray("Do not bypass acceptance contract gating in friendly CLI fixtures."),
                ["evidence_required"] = new System.Text.Json.Nodes.JsonArray(),
                ["human_review"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["required"] = true,
                    ["provisional_allowed"] = false,
                    ["decisions"] = new System.Text.Json.Nodes.JsonArray("accept", "reject", "reopen"),
                },
                ["traceability"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["source_card_id"] = "CARD-FRIENDLY-CLI",
                    ["source_task_id"] = taskId,
                    ["derived_task_ids"] = new System.Text.Json.Nodes.JsonArray(),
                    ["related_artifacts"] = new System.Text.Json.Nodes.JsonArray(),
                },
            },
            ["constraints"] = new System.Text.Json.Nodes.JsonArray("keep integration fixture deterministic"),
            ["validation"] = new System.Text.Json.Nodes.JsonObject
            {
                ["commands"] = new System.Text.Json.Nodes.JsonArray(),
                ["checks"] = new System.Text.Json.Nodes.JsonArray(),
                ["expected_evidence"] = new System.Text.Json.Nodes.JsonArray(),
            },
            ["retry_count"] = 0,
            ["capabilities"] = new System.Text.Json.Nodes.JsonArray(),
            ["metadata"] = new System.Text.Json.Nodes.JsonObject(),
            ["planner_review"] = new System.Text.Json.Nodes.JsonObject
            {
                ["verdict"] = "continue",
                ["reason"] = "Friendly CLI synthetic task is ready.",
                ["acceptance_met"] = false,
                ["boundary_preserved"] = true,
                ["scope_drift_detected"] = false,
                ["follow_up_suggestions"] = new System.Text.Json.Nodes.JsonArray(),
            },
            ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["updated_at"] = DateTimeOffset.UtcNow.ToString("O"),
        };
        File.WriteAllText(nodePath, taskNode.ToJsonString(new() { WriteIndented = true }));
    }

    private sealed class GitSandbox : IDisposable
    {
        private GitSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static GitSandbox Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-runtime-friendly-cli", Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(rootPath);
            return new GitSandbox(rootPath);
        }

        public void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void CommitAll(string message)
        {
            RunGit(RootPath, "add", ".");
            RunGit(RootPath, "commit", "-m", message);
        }

        public void Dispose()
        {
            if (!Directory.Exists(RootPath))
            {
                return;
            }

            try
            {
                Directory.Delete(RootPath, true);
            }
            catch
            {
            }
        }

        internal static void RunGit(string workingDirectory, params string[] arguments)
        {
            GitTestHarness.Run(workingDirectory, arguments);
        }
    }

    private static void AssertHostEnsureRequired(CliProgramHarness.CliRunResult result)
    {
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("host ensure --json", result.CombinedOutput, StringComparison.Ordinal);
    }

    private static string ResolveHostRuntimeDirectory(string repoRoot)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(repoRoot)));
        var repoHash = Convert.ToHexString(hash).ToLowerInvariant()[..16];
        return Path.Combine(Path.GetTempPath(), "carves-runtime-host", repoHash);
    }

    private static void WriteHostDescriptor(
        string repoRoot,
        int processId,
        string runtimeDirectory,
        string deploymentDirectory,
        string executablePath,
        string baseUrl,
        int port)
    {
        var descriptorPath = Path.Combine(repoRoot, ".carves-platform", "host", "descriptor.json");
        var descriptor = new JsonObject
        {
            ["host_id"] = $"host-{Guid.NewGuid():N}",
            ["machine_id"] = $"machine-{Environment.MachineName}",
            ["repo_root"] = Path.GetFullPath(repoRoot),
            ["base_url"] = baseUrl,
            ["port"] = port,
            ["process_id"] = processId,
            ["runtime_directory"] = runtimeDirectory,
            ["deployment_directory"] = deploymentDirectory,
            ["executable_path"] = executablePath,
            ["started_at"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
            ["version"] = "0.4.0-beta.1",
            ["stage"] = "Stage-8A fleet discovery and registry completed",
        };

        Directory.CreateDirectory(Path.GetDirectoryName(descriptorPath)!);
        File.WriteAllText(descriptorPath, descriptor.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        }));
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class LongRunningProcess : IDisposable
    {
        private readonly Process process;

        private LongRunningProcess(Process process)
        {
            this.process = process;
        }

        public int ProcessId => process.Id;

        public static LongRunningProcess Start()
        {
            var startInfo = new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = "powershell";
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add("Start-Sleep -Seconds 120");
            }
            else
            {
                startInfo.FileName = "bash";
                startInfo.ArgumentList.Add("-lc");
                startInfo.ArgumentList.Add("sleep 120");
            }

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start long-running placeholder process.");
            Thread.Sleep(50);
            return new LongRunningProcess(process);
        }

        public void Dispose()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
            }

            process.Dispose();
        }
    }
}
