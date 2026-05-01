using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class PlatformHostTests
{
    [Fact]
    public void PlatformCommands_RegisterInspectAndControlRuntime()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var register = RunProgram("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-alpha");
        var repoList = RunProgram("--repo-root", sandbox.RootPath, "repo", "list");
        var inspect = RunProgram("--repo-root", sandbox.RootPath, "repo", "inspect", "repo-alpha");
        var runtimeList = RunProgram("--repo-root", sandbox.RootPath, "runtime", "list");
        var runtimeStart = RunProgram("--repo-root", sandbox.RootPath, "runtime", "start", "repo-alpha", "--dry-run");
        var runtimePause = RunProgram("--repo-root", sandbox.RootPath, "runtime", "pause", "repo-alpha", "Pause", "runtime");
        var runtimeStop = RunProgram("--repo-root", sandbox.RootPath, "runtime", "stop", "repo-alpha", "Stop", "runtime");

        Assert.Equal(0, register.ExitCode);
        Assert.Equal(0, repoList.ExitCode);
        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, runtimeList.ExitCode);
        Assert.Equal(0, runtimeStart.ExitCode);
        Assert.Equal(0, runtimePause.ExitCode);
        Assert.Equal(0, runtimeStop.ExitCode);
        Assert.Contains("repo-alpha", repoList.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Started runtime instance for repo-alpha", runtimeStart.StandardOutput, StringComparison.Ordinal);
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".carves-platform", "repos", "registry.json")));
        Assert.True(File.Exists(paths.PlatformRuntimeInstancesLiveStateFile));
    }

    [Fact]
    public void PlatformCommands_ExposeProviderGovernanceApiDashboardAndWorkers()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        RunProgram("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-alpha");

        var providerList = RunProgram("--repo-root", sandbox.RootPath, "provider", "list");
        var providerBind = RunProgram("--repo-root", sandbox.RootPath, "provider", "bind", "repo-alpha", "planner-high-context");
        var governance = RunProgram("--repo-root", sandbox.RootPath, "governance", "show");
        var workerList = RunProgram("--repo-root", sandbox.RootPath, "worker", "list");
        var workerQuarantine = RunProgram("--repo-root", sandbox.RootPath, "worker", "quarantine", "local-default", "Maintenance");
        var workerHeartbeat = RunProgram("--repo-root", sandbox.RootPath, "worker", "heartbeat", "local-default");
        var api = RunProgram("--repo-root", sandbox.RootPath, "api", "platform-status");
        var dashboard = RunProgram("--repo-root", sandbox.RootPath, "dashboard", "repo-alpha");
        var apiJson = JsonNode.Parse(api.StandardOutput)!.AsObject();

        Assert.Equal(0, providerList.ExitCode);
        Assert.Equal(0, providerBind.ExitCode);
        Assert.Equal(0, governance.ExitCode);
        Assert.Equal(0, workerList.ExitCode);
        Assert.Equal(0, workerQuarantine.ExitCode);
        Assert.Equal(0, workerHeartbeat.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Equal(0, dashboard.ExitCode);
        Assert.Contains("planner-high-context", providerBind.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Platform policy:", governance.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Platform Overview", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Session Gateway assist:", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Session Gateway assist next action:", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Acceptance contract ingress policy: planning_truth_mutation=auto_minimum_contract; execution_dispatch=explicit_gap_required", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Vendor-native acceleration: optional_vendor_native_acceleration_ready", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Codex reinforcement: repo_guard_assets_ready", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Claude reinforcement: bounded_runtime_qualification_ready", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent recommended mode: mode_a_open_repo_advisory", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent recommendation posture: advisory_until_formal_planning", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent constraint tier: soft_advisory", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent first stronger-mode blocker: formal_planning_packet_available", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent first stronger-mode blocker action: run `plan init [candidate-card-id]`", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation: plan_init_required_before_mode_e_activation", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation command: (none)", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E result return channel: (none)", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation next action: Run `plan init [candidate-card-id]` before selecting Mode E brokered execution.", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation first blocker: formal_planning_packet_available", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation first blocker action: run `plan init [candidate-card-id]`", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation playbook steps: 2", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation first playbook command: plan init [candidate-card-id]", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Formal planning entry trigger: discussion_only", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Formal planning entry command: plan init [candidate-card-id]", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Formal planning entry next action: When the conversation becomes durable planning, run `plan init [candidate-card-id]`", dashboard.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(1, apiJson["registered_repo_count"]!.GetValue<int>());
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        Assert.True(File.Exists(paths.PlatformGovernanceEventsRuntimeFile));
        Assert.True(File.Exists(paths.PlatformWorkerRegistryLiveStateFile));
    }

    [Fact]
    public void Status_ProjectsSessionGatewayGovernanceAssistSummary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        RunProgram("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-alpha");

        var status = RunProgram("--repo-root", sandbox.RootPath, "status");

        Assert.Equal(0, status.ExitCode);
        Assert.Contains("Session Gateway assist:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Session Gateway assist top pressure:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Session Gateway assist next action:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Acceptance contract ingress policy: planning_truth_mutation=auto_minimum_contract; execution_dispatch=explicit_gap_required", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Vendor-native acceleration: optional_vendor_native_acceleration_ready", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Codex reinforcement: repo_guard_assets_ready", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Claude reinforcement: bounded_runtime_qualification_ready", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent recommended mode: mode_a_open_repo_advisory", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent recommendation posture: advisory_until_formal_planning", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent constraint tier: soft_advisory", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent first stronger-mode blocker: formal_planning_packet_available", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External agent first stronger-mode blocker action: run `plan init [candidate-card-id]`", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation: plan_init_required_before_mode_e_activation", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation command: (none)", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E result return channel: (none)", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation next action: Run `plan init [candidate-card-id]` before selecting Mode E brokered execution.", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation first blocker: formal_planning_packet_available", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation first blocker action: run `plan init [candidate-card-id]`", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation playbook steps: 2", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E activation first playbook command: plan init [candidate-card-id]", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Formal planning entry trigger: discussion_only", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Formal planning entry command: plan init [candidate-card-id]", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Formal planning entry next action: When the conversation becomes durable planning, run `plan init [candidate-card-id]`", status.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Stage55PlatformCommands_ExposeSchedulingGatewayQuotaAndLint()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        RunProgram("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-alpha");

        var workerList = RunProgram("--repo-root", sandbox.RootPath, "worker", "list");
        var runtimeInspect = RunProgram("--repo-root", sandbox.RootPath, "runtime", "inspect", "repo-alpha");
        var runtimeSchedule = RunProgram("--repo-root", sandbox.RootPath, "runtime", "schedule", "--slots", "1");
        var providerQuota = RunProgram("--repo-root", sandbox.RootPath, "provider", "quota");
        var providerRoute = RunProgram("--repo-root", sandbox.RootPath, "provider", "route", "repo-alpha", "worker");
        var workerLeases = RunProgram("--repo-root", sandbox.RootPath, "worker", "leases");
        var apiProviderQuota = RunProgram("--repo-root", sandbox.RootPath, "api", "provider-quota");
        var apiProviderRoute = RunProgram("--repo-root", sandbox.RootPath, "api", "provider-route", "repo-alpha", "worker");
        var apiPlatformSchedule = RunProgram("--repo-root", sandbox.RootPath, "api", "platform-schedule", "--slots", "1");
        var apiRepoGateway = RunProgram("--repo-root", sandbox.RootPath, "api", "repo-gateway", "repo-alpha");
        var lint = RunProgram("--repo-root", sandbox.RootPath, "lint-platform");
        var quotaJson = JsonNode.Parse(apiProviderQuota.StandardOutput)!.AsArray();
        var routeJson = JsonNode.Parse(apiProviderRoute.StandardOutput)!.AsObject();
        var scheduleJson = JsonNode.Parse(apiPlatformSchedule.StandardOutput)!.AsObject();
        var gatewayJson = JsonNode.Parse(apiRepoGateway.StandardOutput)!.AsObject();
        var lintOutput = CombineOutput(lint);
        var lintFindingCount = ParseLintFindingCount(lintOutput);

        Assert.Equal(0, workerList.ExitCode);
        Assert.Equal(0, runtimeInspect.ExitCode);
        Assert.Equal(0, runtimeSchedule.ExitCode);
        Assert.Equal(0, providerQuota.ExitCode);
        Assert.Equal(0, providerRoute.ExitCode);
        Assert.Equal(0, workerLeases.ExitCode);
        Assert.Equal(0, apiProviderQuota.ExitCode);
        Assert.Equal(0, apiProviderRoute.ExitCode);
        Assert.Equal(0, apiPlatformSchedule.ExitCode);
        Assert.Equal(0, apiRepoGateway.ExitCode);
        Assert.Equal(lintFindingCount > 0 ? 1 : 0, lint.ExitCode);
        Assert.Contains("Projection freshness:", runtimeInspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway:", runtimeInspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Requested slots: 1", runtimeSchedule.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Provider quotas:", providerQuota.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Allowed: True", providerRoute.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Worker leases:", workerLeases.StandardOutput, StringComparison.Ordinal);
        Assert.NotEmpty(quotaJson);
        Assert.True(routeJson["allowed"]!.GetValue<bool>());
        Assert.Equal(1, scheduleJson["requested_slots"]!.GetValue<int>());
        Assert.True(gatewayJson["reachable"]!.GetValue<bool>());
        Assert.Contains("Platform lint findings:", lintOutput, StringComparison.Ordinal);
    }

    private static int ParseLintFindingCount(string output)
    {
        var line = output.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(item => item.StartsWith("Platform lint findings:", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(line));
        return int.Parse(line["Platform lint findings:".Length..].Trim());
    }

    private static string CombineOutput(ProgramRunResult result)
    {
        return string.Concat(result.StandardOutput, Environment.NewLine, result.StandardError);
    }

    private static ProgramRunResult RunProgram(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = Program.Main(args);
            return new ProgramRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}
