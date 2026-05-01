using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Host;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class HostClientSurfaceTests
{
    [Fact]
    public void HostStart_DeploysResidentHostOutsideSourceBuildOutput()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            var descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath)).RootElement;
            var sourceAssemblyDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)
                ?? throw new InvalidOperationException("Program assembly path is unavailable.");
            var deploymentDirectory = descriptor.GetProperty("deployment_directory").GetString();
            var executablePath = descriptor.GetProperty("executable_path").GetString();
            var stdoutLogPath = Path.Combine(deploymentDirectory!, "logs", "host.stdout.log");
            var stderrLogPath = Path.Combine(deploymentDirectory!, "logs", "host.stderr.log");

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(deploymentDirectory));
            Assert.False(string.IsNullOrWhiteSpace(executablePath));
            Assert.True(Directory.Exists(deploymentDirectory));
            Assert.True(File.Exists(executablePath));
            Assert.True(File.Exists(stdoutLogPath));
            Assert.True(File.Exists(stderrLogPath));
            Assert.DoesNotContain(Path.GetFullPath(sourceAssemblyDirectory), Path.GetFullPath(deploymentDirectory), StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                $"{Path.DirectorySeparatorChar}deployments{Path.DirectorySeparatorChar}",
                Path.GetFullPath(deploymentDirectory),
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Host deployment dir:", start.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Host stdout log:", start.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Host stderr log:", start.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostStartHelp_IsReadOnlyAndDoesNotStartResidentHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        var help = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--help");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("Usage: carves host", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Help requests are read-only", help.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Started resident host process", help.CombinedOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(descriptorPath));
    }

    [Fact]
    public void GatewayHelp_IsReadOnlyAndStatesGatewayBoundary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        var help = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "--help");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("Usage: carves gateway", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway role: resident connection, routing, and observability", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway boundary: it does not dispatch worker automation", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gateway serve [--port <port>] [--interval-ms <milliseconds>]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gateway restart [--json] [--force] [--port <port>] [--interval-ms <milliseconds>] [reason...]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gateway activity [--json] [--tail <lines>] [--since-minutes <minutes>] [--category <category>] [--event <event-kind>] [--command <command>] [--path <path>] [--request-id <id>] [--operation-id <id>] [--session-id <id>] [--message-id <id>] [--compact --retain <lines>]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gateway activity status [--json] [--require-maturity]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gateway activity maintenance [--json] [--before-days <days>]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gateway activity explain <request-id> [--json]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gateway activity verify [--json]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gateway activity archive [--json] [--before-days <days>]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Help requests are read-only", help.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Started resident host process", help.CombinedOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(descriptorPath));
    }

    [Fact]
    public void GatewayServeHelp_IsReadOnlyAndStatesForegroundRequestLogging()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        var help = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "serve", "--help");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("Usage: carves gateway", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gateway serve [--port <port>] [--interval-ms <milliseconds>]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway boundary: it does not dispatch worker automation", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Help requests are read-only", help.StandardOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(descriptorPath));
    }

    [Fact]
    public void GatewayServe_FailsClosedWhenGatewayIsAlreadyRunning()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);
        var runningPort = ReserveLoopbackPort();
        var blockedPort = ReserveLoopbackPort();
        while (blockedPort == runningPort)
        {
            blockedPort = ReserveLoopbackPort();
        }

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200", "--port", runningPort.ToString());
            var serve = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "serve",
                "--json",
                "--interval-ms",
                "200",
                "--port",
                blockedPort.ToString());
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(serve.StandardOutput) ? serve.StandardError : serve.StandardOutput);
            var root = document.RootElement;

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(1, serve.ExitCode);
            Assert.Equal("carves-gateway-serve.v1", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("gateway_ready").GetBoolean());
            Assert.False(root.GetProperty("foreground_serve_started").GetBoolean());
            Assert.True(root.GetProperty("already_running").GetBoolean());
            Assert.False(root.GetProperty("conflict_present").GetBoolean());
            Assert.Equal($"http://127.0.0.1:{runningPort}", root.GetProperty("base_url").GetString());
            Assert.Equal("carves gateway status --json", root.GetProperty("next_action").GetString());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void GatewayServe_FailsClosedWhenAliveStaleDescriptorExists()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "stale-gateway-serve-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();
        var requestedPort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            sandbox.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var serve = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "gateway",
            "serve",
            "--json",
            "--interval-ms",
            "200",
            "--port",
            requestedPort.ToString());
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(serve.StandardOutput) ? serve.StandardError : serve.StandardOutput);
        var root = document.RootElement;
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        using var descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));

        Assert.Equal(1, serve.ExitCode);
        Assert.Equal("carves-gateway-serve.v1", root.GetProperty("schema_version").GetString());
        Assert.False(root.GetProperty("gateway_ready").GetBoolean());
        Assert.False(root.GetProperty("foreground_serve_started").GetBoolean());
        Assert.False(root.GetProperty("already_running").GetBoolean());
        Assert.True(root.GetProperty("conflict_present").GetBoolean());
        Assert.Equal(sleeper.ProcessId, root.GetProperty("conflicting_process_id").GetInt32());
        Assert.Equal($"http://127.0.0.1:{stalePort}", root.GetProperty("conflicting_base_url").GetString());
        Assert.Equal("reconcile_stale_host", root.GetProperty("next_action_kind").GetString());
        Assert.Contains("gateway reconcile --replace-stale", root.GetProperty("next_action").GetString(), StringComparison.Ordinal);
        Assert.Equal("blocked", root.GetProperty("lifecycle").GetProperty("state").GetString());
        Assert.Equal("host_session_conflict", root.GetProperty("lifecycle").GetProperty("reason").GetString());
        Assert.Equal(sleeper.ProcessId, descriptor.RootElement.GetProperty("process_id").GetInt32());
        Assert.True(IsProcessAlive(sleeper.ProcessId));
    }

    [Fact]
    public void GatewayStatus_ProjectsGatewayBoundaryWithoutStartingHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "status");
        var jsonStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "status", "--json");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(jsonStatus.StandardOutput) ? jsonStatus.StandardError : jsonStatus.StandardOutput);
        var root = document.RootElement;

        Assert.NotEqual(0, status.ExitCode);
        Assert.NotEqual(0, jsonStatus.ExitCode);
        Assert.Contains("Gateway role: connection, routing, and observability only", status.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Gateway automation boundary: worker automation is controlled separately by role-mode gates", status.CombinedOutput, StringComparison.Ordinal);
        Assert.Equal("connection_routing_observability", root.GetProperty("gateway_role").GetString());
        Assert.Equal("no_worker_automation_dispatch", root.GetProperty("gateway_automation_boundary").GetString());
        Assert.False(File.Exists(descriptorPath));
    }

    [Fact]
    public async Task HostRootRoute_ReportsRunningStatusAndAgentEntryWithoutDashboardRequirement()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var baseUrl = ReadRepoHostBaseUrl(sandbox.RootPath);
            using var client = new HttpClient();

            using var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/?format=json");
            var body = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("carves-host-root.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal("CARVES Host", root.GetProperty("service").GetString());
            Assert.Equal("running", root.GetProperty("status").GetString());
            Assert.True(root.GetProperty("host_running").GetBoolean());
            Assert.Equal(baseUrl, root.GetProperty("base_url").GetString());
            Assert.Equal("carves host ensure --json", root.GetProperty("host_readiness_command").GetString());
            Assert.Equal("carves agent start --json", root.GetProperty("runtime_agent_start_command").GetString());
            Assert.Equal("carves up <target-project>", root.GetProperty("product_start_command").GetString());
            Assert.Equal("start CARVES", root.GetProperty("human_start_prompt").GetString());
            Assert.Contains("start CARVES", root.GetProperty("human_next_action").GetString(), StringComparison.Ordinal);
            Assert.Equal(".carves/carves agent start --json", root.GetProperty("target_project_agent_command").GetString());
            Assert.Contains(".carves/carves agent start --json", root.GetProperty("agent_instruction").GetString(), StringComparison.Ordinal);
            Assert.Contains(".carves/AGENT_START.md", root.GetProperty("target_project_agent_entry").GetString(), StringComparison.Ordinal);
            Assert.Equal("host_running_status_pointer_not_dashboard", root.GetProperty("root_route_role").GetString());
            Assert.Equal("connection_routing_observability", root.GetProperty("gateway_role").GetString());
            Assert.Equal("no_worker_automation_dispatch", root.GetProperty("gateway_automation_boundary").GetString());
            Assert.Equal("null_worker_current_version_no_api_sdk_worker_execution", root.GetProperty("worker_execution_boundary").GetString());
            Assert.Contains(root.GetProperty("routes").EnumerateArray(), route => route.GetString() == "/handshake");
            Assert.Contains(root.GetProperty("non_authority").EnumerateArray(), item => item.GetString() == "not_dashboard_product_entry");
            Assert.DoesNotContain("Unknown host route", body, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task HostRootRoute_DefaultRequestShowsHtmlStatusPointerNotJsonError()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var baseUrl = ReadRepoHostBaseUrl(sandbox.RootPath);
            using var client = new HttpClient();

            using var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("text/html", response.Content.Headers.ContentType?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CARVES Host is running", body, StringComparison.Ordinal);
            Assert.Contains("start CARVES", body, StringComparison.Ordinal);
            Assert.Contains(".carves/carves agent start --json", body, StringComparison.Ordinal);
            Assert.Contains("not the product dashboard", body, StringComparison.Ordinal);
            Assert.Contains("does not dispatch worker automation", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Unknown host route", body, StringComparison.Ordinal);
            Assert.DoesNotContain("\"error\"", body, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task HostRootRoute_WhenBrowserAcceptsHtml_ShowsStatusPointerNotDashboard()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var baseUrl = ReadRepoHostBaseUrl(sandbox.RootPath);
            using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/");
            request.Headers.Accept.ParseAdd("text/html");

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("text/html", response.Content.Headers.ContentType?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CARVES Host is running", body, StringComparison.Ordinal);
            Assert.Contains("start CARVES", body, StringComparison.Ordinal);
            Assert.Contains(".carves/carves agent start --json", body, StringComparison.Ordinal);
            Assert.Contains("not the product dashboard", body, StringComparison.Ordinal);
            Assert.Contains("does not dispatch worker automation", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Unknown host route", body, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void GatewayRestart_StartsFreshGatewayWhenMissing()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);
        var port = ReserveLoopbackPort();

        try
        {
            var restart = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "restart",
                "--json",
                "--interval-ms",
                "200",
                "--port",
                port.ToString(),
                "phase 5 missing gateway restart test");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(restart.StandardOutput) ? restart.StandardError : restart.StandardOutput);
            var root = document.RootElement;

            Assert.Equal(0, restart.ExitCode);
            Assert.Equal("carves-gateway-restart.v1", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("gateway_ready").GetBoolean());
            Assert.False(root.GetProperty("restarted").GetBoolean());
            Assert.False(root.GetProperty("stopped_previous").GetBoolean());
            Assert.True(root.GetProperty("started").GetBoolean());
            Assert.Equal($"http://127.0.0.1:{port}", root.GetProperty("base_url").GetString());
            Assert.Equal("connection_routing_observability", root.GetProperty("gateway_role").GetString());
            Assert.Equal("no_worker_automation_dispatch", root.GetProperty("gateway_automation_boundary").GetString());
            Assert.Equal("gateway ready", root.GetProperty("next_action").GetString());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void GatewayRestart_RestartsRunningGatewayWithSingleCommand()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);
        var firstPort = ReserveLoopbackPort();
        var secondPort = ReserveLoopbackPort();
        while (secondPort == firstPort)
        {
            secondPort = ReserveLoopbackPort();
        }

        try
        {
            var firstStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200", "--port", firstPort.ToString());
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var firstDescriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var firstProcessId = firstDescriptor.RootElement.GetProperty("process_id").GetInt32();

            var restart = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "restart",
                "--json",
                "--interval-ms",
                "200",
                "--port",
                secondPort.ToString(),
                "phase 5 running gateway restart test");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(restart.StandardOutput) ? restart.StandardError : restart.StandardOutput);
            var root = document.RootElement;

            Assert.Equal(0, firstStart.ExitCode);
            Assert.Equal(0, restart.ExitCode);
            Assert.Equal("carves-gateway-restart.v1", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("gateway_ready").GetBoolean());
            Assert.True(root.GetProperty("restarted").GetBoolean());
            Assert.True(root.GetProperty("stopped_previous").GetBoolean());
            Assert.True(root.GetProperty("started").GetBoolean());
            Assert.Equal(firstProcessId, root.GetProperty("previous_process_id").GetInt32());
            Assert.Equal($"http://127.0.0.1:{firstPort}", root.GetProperty("previous_base_url").GetString());
            Assert.Equal($"http://127.0.0.1:{secondPort}", root.GetProperty("base_url").GetString());
            Assert.NotEqual(firstProcessId, root.GetProperty("process_id").GetInt32());
            Assert.Equal("connection_routing_observability", root.GetProperty("gateway_role").GetString());
            Assert.Equal("no_worker_automation_dispatch", root.GetProperty("gateway_automation_boundary").GetString());
            Assert.Equal("gateway ready", root.GetProperty("next_action").GetString());
            Assert.False(IsProcessAlive(firstProcessId));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void GatewayLogs_ProjectsMissingLogsWithoutStartingHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        var logs = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "logs", "--json", "--tail", "5");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(logs.StandardOutput) ? logs.StandardError : logs.StandardOutput);
        var root = document.RootElement;

        Assert.NotEqual(0, logs.ExitCode);
        Assert.Equal("carves-gateway-logs.v1", root.GetProperty("schema_version").GetString());
        Assert.False(root.GetProperty("host_running").GetBoolean());
        Assert.False(root.GetProperty("logs_available").GetBoolean());
        Assert.Equal("connection_routing_observability", root.GetProperty("gateway_role").GetString());
        Assert.Equal("no_worker_automation_dispatch", root.GetProperty("gateway_automation_boundary").GetString());
        Assert.Equal("carves gateway start", root.GetProperty("next_action").GetString());
        Assert.False(File.Exists(descriptorPath));
    }

    [Fact]
    public void GatewayDoctor_ProjectsNotReadyWithoutStartingHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        var doctor = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "doctor", "--json", "--tail", "3");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(doctor.StandardOutput) ? doctor.StandardError : doctor.StandardOutput);
        var root = document.RootElement;

        Assert.NotEqual(0, doctor.ExitCode);
        Assert.Equal("carves-gateway-doctor.v14", root.GetProperty("schema_version").GetString());
        Assert.False(root.GetProperty("gateway_ready").GetBoolean());
        Assert.False(root.GetProperty("host_running").GetBoolean());
        Assert.False(root.GetProperty("logs_available").GetBoolean());
        Assert.False(root.GetProperty("activity_available").GetBoolean());
        Assert.Equal(0, root.GetProperty("activity_entry_count").GetInt32());
        Assert.Equal("start gateway and make a gateway request", root.GetProperty("activity_next_action").GetString());
        Assert.Equal("store_operational_posture", root.GetProperty("activity_next_action_source").GetString());
        Assert.Equal(1, root.GetProperty("activity_next_action_priority").GetInt32());
        Assert.Equal("store_not_initialized", root.GetProperty("activity_next_action_reason").GetString());
        Assert.Equal("connection_routing_observability", root.GetProperty("gateway_role").GetString());
        Assert.Equal("no_worker_automation_dispatch", root.GetProperty("gateway_automation_boundary").GetString());
        Assert.Contains("carves gateway", root.GetProperty("recommended_action").GetString(), StringComparison.Ordinal);
        Assert.False(root.GetProperty("activity_store_writer_lock_file_exists").GetBoolean());
        Assert.False(root.GetProperty("activity_store_writer_lock_currently_held").GetBoolean());
        Assert.Equal("missing", root.GetProperty("activity_store_writer_lock_status").GetString());
        Assert.Equal("store_not_initialized", root.GetProperty("activity_store_operational_posture").GetString());
        Assert.Equal(1, root.GetProperty("activity_store_issue_count").GetInt32());
        Assert.Contains(
            root.GetProperty("activity_store_issues").EnumerateArray().Select(item => item.GetString()),
            issue => string.Equals(issue, "activity_store_not_initialized", StringComparison.Ordinal));
        Assert.Equal("start gateway and make a gateway request", root.GetProperty("activity_store_recommended_action").GetString());
        Assert.Equal("stage_i_bounded_self_maintenance", root.GetProperty("activity_store_maturity_stage").GetString());
        Assert.False(root.GetProperty("activity_store_maturity_ready").GetBoolean());
        Assert.Equal("not_initialized", root.GetProperty("activity_store_maturity_posture").GetString());
        Assert.Contains("initialize", root.GetProperty("activity_store_maturity_summary").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, root.GetProperty("tail_line_count").GetInt32());
        Assert.False(File.Exists(descriptorPath));
    }

    [Fact]
    public void GatewayDoctor_ProjectsReadyGatewayAndLogVisibility()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var dashboard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "dashboard", "--text");
            var doctor = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "doctor", "--json", "--tail", "80");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(doctor.StandardOutput) ? doctor.StandardError : doctor.StandardOutput);
            var root = document.RootElement;

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, dashboard.ExitCode);
            Assert.Equal(0, doctor.ExitCode);
            Assert.Equal("carves-gateway-doctor.v14", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("gateway_ready").GetBoolean());
            Assert.True(root.GetProperty("host_running").GetBoolean());
            Assert.True(root.GetProperty("logs_available").GetBoolean());
            Assert.Equal("carves-gateway-activity-journal.v1", root.GetProperty("activity_journal_schema_version").GetString());
            Assert.Equal("activity_journal", root.GetProperty("activity_source").GetString());
            Assert.Equal("segmented_append_only", root.GetProperty("activity_journal_storage_mode").GetString());
            Assert.True(root.GetProperty("activity_journal_segment_count").GetInt32() >= 1);
            Assert.False(root.GetProperty("activity_journal_legacy_fallback_used").GetBoolean());
            Assert.True(root.GetProperty("activity_journal_exists").GetBoolean());
            Assert.True(root.GetProperty("activity_journal_byte_count").GetInt64() > 0);
            Assert.True(root.GetProperty("activity_journal_line_count").GetInt64() > 0);
            Assert.Equal("carves-gateway-activity-events.v3", root.GetProperty("activity_event_catalog_version").GetString());
            Assert.Equal("carves-gateway-activity-store.v1", root.GetProperty("activity_store_contract_version").GetString());
            Assert.Equal("carves-gateway-activity-store-record.v1", root.GetProperty("activity_store_record_schema_version").GetString());
            Assert.Equal("host_operational_evidence_not_governance_truth", root.GetProperty("activity_store_truth_boundary").GetString());
            Assert.Equal("operator_diagnosis_evidence_not_audit_truth", root.GetProperty("activity_store_authority_posture").GetString());
            Assert.Equal("bounded_local_store_with_drop_telemetry_not_guaranteed_complete", root.GetProperty("activity_store_completeness_posture").GetString());
            Assert.False(root.GetProperty("activity_store_audit_truth").GetBoolean());
            Assert.Equal("diagnose_gateway_requests_and_feedback_only", root.GetProperty("activity_store_operator_use").GetString());
            Assert.Contains(
                root.GetProperty("activity_store_not_for").EnumerateArray().Select(item => item.GetString()),
                posture => string.Equals(posture, "compliance_audit_truth", StringComparison.Ordinal));
            Assert.Equal("ready", root.GetProperty("activity_store_operational_posture").GetString());
            Assert.Equal("Activity store is ready for operator diagnosis.", root.GetProperty("activity_store_operator_summary").GetString());
            Assert.Equal(0, root.GetProperty("activity_store_issue_count").GetInt32());
            Assert.Empty(root.GetProperty("activity_store_issues").EnumerateArray());
            Assert.Equal("gateway activity verify", root.GetProperty("activity_store_recommended_action").GetString());
            Assert.Equal("verification_missing", root.GetProperty("activity_store_verification_freshness_posture").GetString());
            Assert.Equal("maintenance_missing", root.GetProperty("activity_store_maintenance_freshness_posture").GetString());
            Assert.Equal("stage_i_bounded_self_maintenance", root.GetProperty("activity_store_maturity_stage").GetString());
            Assert.False(root.GetProperty("activity_store_maturity_ready").GetBoolean());
            Assert.Equal("verification_missing", root.GetProperty("activity_store_maturity_posture").GetString());
            Assert.Equal("non_destructive_archive_no_delete", root.GetProperty("activity_store_retention_mode").GetString());
            Assert.Equal("automatic_on_append_and_manual_archive", root.GetProperty("activity_store_retention_execution_mode").GetString());
            Assert.Equal(30, root.GetProperty("activity_store_default_archive_before_days").GetInt32());
            Assert.Equal("bounded_file_lock", root.GetProperty("activity_store_writer_lock_mode").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("activity_store_writer_lock_path").GetString()));
            Assert.True(root.GetProperty("activity_store_writer_lock_exists").GetBoolean());
            Assert.True(root.GetProperty("activity_store_writer_lock_file_exists").GetBoolean());
            Assert.False(root.GetProperty("activity_store_writer_lock_currently_held").GetBoolean());
            Assert.Equal("available", root.GetProperty("activity_store_writer_lock_status").GetString());
            Assert.Equal(5000, root.GetProperty("activity_store_writer_lock_acquire_timeout_ms").GetInt32());
            Assert.Equal("segment_manifest_sha256_checkpoint_chain", root.GetProperty("activity_store_integrity_mode").GetString());
            Assert.Equal("carves-gateway-activity-drop-telemetry.v1", root.GetProperty("activity_store_drop_telemetry_schema_version").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("activity_store_drop_telemetry_path").GetString()));
            Assert.False(root.GetProperty("activity_store_drop_telemetry_exists").GetBoolean());
            Assert.Equal(0, root.GetProperty("activity_store_dropped_activity_count").GetInt64());
            Assert.Equal(string.Empty, root.GetProperty("activity_store_last_drop_reason").GetString());
            Assert.Contains(
                root.GetProperty("known_activity_event_kinds").EnumerateArray().Select(item => item.GetString()),
                kind => string.Equals(kind, "gateway-invoke-response", StringComparison.Ordinal));
            Assert.Contains(
                root.GetProperty("known_activity_categories").EnumerateArray().Select(item => item.GetString()),
                category => string.Equals(category, "cli_invoke", StringComparison.Ordinal));
            Assert.Contains(
                root.GetProperty("known_activity_categories").EnumerateArray().Select(item => item.GetString()),
                category => string.Equals(category, "gateway_request", StringComparison.Ordinal));
            Assert.True(root.GetProperty("activity_available").GetBoolean());
            Assert.True(root.GetProperty("activity_entry_count").GetInt32() >= 2);
            Assert.True(root.GetProperty("activity_request_entry_count").GetInt32() > 0);
            Assert.True(root.GetProperty("activity_feedback_entry_count").GetInt32() > 0);
            Assert.True(root.GetProperty("activity_cli_invoke_entry_count").GetInt32() > 0);
            Assert.True(root.GetProperty("activity_gateway_request_entry_count").GetInt32() > 0);
            Assert.Equal(0, root.GetProperty("activity_route_not_found_entry_count").GetInt32());
            Assert.Equal("gateway-invoke-response", root.GetProperty("last_activity_event").GetString());
            Assert.Equal("dashboard", root.GetProperty("last_activity_command").GetString());
            Assert.Equal("0", root.GetProperty("last_activity_exit_code").GetString());
            Assert.Equal("dashboard", root.GetProperty("last_request_command").GetString());
            Assert.Equal("--text", root.GetProperty("last_request_arguments").GetString());
            Assert.Equal("gateway-invoke-response", root.GetProperty("last_feedback_event").GetString());
            Assert.Equal("dashboard", root.GetProperty("last_feedback_command").GetString());
            Assert.Equal("0", root.GetProperty("last_feedback_exit_code").GetString());
            Assert.Equal("gateway-invoke-response", root.GetProperty("last_cli_invoke_event").GetString());
            Assert.Equal("dashboard", root.GetProperty("last_cli_invoke_command").GetString());
            Assert.Equal("0", root.GetProperty("last_cli_invoke_exit_code").GetString());
            Assert.Equal(string.Empty, root.GetProperty("last_rest_request_event").GetString());
            Assert.Equal(string.Empty, root.GetProperty("last_session_message_event").GetString());
            Assert.Equal(string.Empty, root.GetProperty("last_operation_feedback_event").GetString());
            Assert.Equal(string.Empty, root.GetProperty("last_route_not_found_event").GetString());
            Assert.Equal("gateway-request", root.GetProperty("last_gateway_request_event").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("last_gateway_request_method").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("last_gateway_request_path").GetString()));
            Assert.Equal("gateway activity visible", root.GetProperty("activity_next_action").GetString());
            Assert.Equal("activity_visibility", root.GetProperty("activity_next_action_source").GetString());
            Assert.Equal(0, root.GetProperty("activity_next_action_priority").GetInt32());
            Assert.Equal("activity_entries_available", root.GetProperty("activity_next_action_reason").GetString());
            Assert.Equal("gateway ready", root.GetProperty("recommended_action").GetString());
            Assert.True(root.GetProperty("command_surface_compatible").GetBoolean());
            Assert.Equal("command_surface_current", root.GetProperty("command_surface_readiness").GetString());
            Assert.True(root.GetProperty("standard_output_log_exists").GetBoolean());
            Assert.True(root.GetProperty("standard_error_log_exists").GetBoolean());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void GatewayActivityMaintenance_ProjectsReadOnlyDryRunWithoutStartingHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        var maintenance = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "maintenance", "--json");
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(maintenance.StandardOutput) ? maintenance.StandardError : maintenance.StandardOutput);
        var root = document.RootElement;

        Assert.Equal(0, maintenance.ExitCode);
        Assert.Equal("carves-gateway-activity-maintenance.v3", root.GetProperty("schema_version").GetString());
        Assert.False(root.GetProperty("host_running").GetBoolean());
        Assert.Equal("store_not_initialized", root.GetProperty("activity_store_operational_posture").GetString());
        Assert.True(root.GetProperty("maintenance_dry_run").GetBoolean());
        Assert.False(root.GetProperty("maintenance_will_modify_store").GetBoolean());
        Assert.False(root.GetProperty("maintenance_needed").GetBoolean());
        Assert.Equal("segment_store_missing", root.GetProperty("maintenance_reason").GetString());
        Assert.Equal("read_only_dry_run", root.GetProperty("maintenance_mode").GetString());
        Assert.Equal(0, root.GetProperty("archive_candidate_segment_count").GetInt32());
        Assert.Empty(root.GetProperty("archive_candidate_segments").EnumerateArray());
        Assert.False(root.GetProperty("activity_store_maintenance_summary_exists").GetBoolean());
        Assert.Equal(string.Empty, root.GetProperty("activity_store_last_maintenance_at_utc").GetString());
        Assert.Equal("start gateway and make a gateway request", root.GetProperty("next_action").GetString());
        Assert.Equal("store_operational_posture", root.GetProperty("next_action_source").GetString());
        Assert.Equal(1, root.GetProperty("next_action_priority").GetInt32());
        Assert.Equal("store_not_initialized", root.GetProperty("next_action_reason").GetString());
    }

    [Fact]
    public async Task GatewayActivityMaintenance_ReportsArchiveCandidatesWithoutMovingSegments()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        string oldSegmentPath = string.Empty;
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/i2-maintenance-current-route");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            StopHost(sandbox.RootPath);

            var beforeActivity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "20");
            using var beforeActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(beforeActivity.StandardOutput) ? beforeActivity.StandardError : beforeActivity.StandardOutput);
            var beforeActivityRoot = beforeActivityDocument.RootElement;
            var segmentDirectory = beforeActivityRoot.GetProperty("activity_journal_segment_directory").GetString();
            var archiveDirectory = beforeActivityRoot.GetProperty("activity_journal_archive_directory").GetString();

            Assert.Equal(0, beforeActivity.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(segmentDirectory));
            Assert.False(string.IsNullOrWhiteSpace(archiveDirectory));

            oldSegmentPath = Path.Combine(segmentDirectory!, "activity-20000101-0001.jsonl");
            Directory.CreateDirectory(segmentDirectory!);
            var oldRecord = new
            {
                SchemaVersion = "carves-gateway-activity-journal.v1",
                Timestamp = DateTimeOffset.Parse("2000-01-01T00:00:00+00:00"),
                Event = "gateway-route-not-found",
                Fields = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["method"] = "GET",
                    ["path"] = "/i2-maintenance-old-route",
                    ["status"] = "404",
                },
            };
            File.WriteAllText(
                oldSegmentPath,
                JsonSerializer.Serialize(oldRecord, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) + Environment.NewLine,
                Encoding.UTF8);

            var maintenance = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "maintenance",
                "--json",
                "--before-days",
                "1");
            using var maintenanceDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(maintenance.StandardOutput) ? maintenance.StandardError : maintenance.StandardOutput);
            var root = maintenanceDocument.RootElement;
            var candidates = root.GetProperty("archive_candidate_segments").EnumerateArray().ToArray();
            var candidateArchivePath = candidates.Single().GetProperty("archive_path").GetString();

            Assert.Equal(0, maintenance.ExitCode);
            Assert.Equal("carves-gateway-activity-maintenance.v3", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("maintenance_dry_run").GetBoolean());
            Assert.False(root.GetProperty("maintenance_will_modify_store").GetBoolean());
            Assert.True(root.GetProperty("maintenance_needed").GetBoolean());
            Assert.Equal("archive_candidates_found", root.GetProperty("maintenance_reason").GetString());
            Assert.Equal("gateway activity archive --before-days 1", root.GetProperty("maintenance_recommended_action").GetString());
            Assert.Equal("gateway activity archive --before-days 1", root.GetProperty("next_action").GetString());
            Assert.Equal("maintenance_plan", root.GetProperty("next_action_source").GetString());
            Assert.Equal(2, root.GetProperty("next_action_priority").GetInt32());
            Assert.Equal("archive_candidates_found", root.GetProperty("next_action_reason").GetString());
            Assert.Equal(1, root.GetProperty("archive_candidate_segment_count").GetInt32());
            Assert.True(root.GetProperty("archive_candidate_byte_count").GetInt64() > 0);
            Assert.Equal(1, root.GetProperty("archive_candidate_record_count").GetInt64());
            Assert.Equal(oldSegmentPath, candidates.Single().GetProperty("source_path").GetString());
            Assert.False(string.IsNullOrWhiteSpace(candidateArchivePath));
            Assert.StartsWith(archiveDirectory!, candidateArchivePath!, StringComparison.Ordinal);
            Assert.True(File.Exists(oldSegmentPath));
            Assert.False(File.Exists(candidateArchivePath));
            Assert.False(root.GetProperty("activity_store_maintenance_summary_exists").GetBoolean());

            var textMaintenance = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "maintenance", "--before-days", "1");
            Assert.Equal(0, textMaintenance.ExitCode);
            Assert.Contains("CARVES gateway activity maintenance", textMaintenance.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Maintenance dry run: True", textMaintenance.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Maintenance will modify store: False", textMaintenance.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Archive candidate segment count: 1", textMaintenance.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next action source: maintenance_plan", textMaintenance.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
            if (!string.IsNullOrWhiteSpace(oldSegmentPath) && File.Exists(oldSegmentPath))
            {
                File.Delete(oldSegmentPath);
            }
        }
    }

    [Fact]
    public void GatewayActivityStatus_ProjectsNotInitializedStoreWithoutStartingHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "status", "--json");
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(status.StandardOutput) ? status.StandardError : status.StandardOutput);
        var root = document.RootElement;

        Assert.NotEqual(0, status.ExitCode);
        Assert.Equal("carves-gateway-activity-status.v9", root.GetProperty("schema_version").GetString());
        Assert.False(root.GetProperty("status_ok").GetBoolean());
        Assert.False(root.GetProperty("activity_store_operational_ok").GetBoolean());
        Assert.False(root.GetProperty("activity_store_maturity_required").GetBoolean());
        Assert.False(root.GetProperty("activity_store_exit_ok").GetBoolean());
        Assert.Equal("operational_only", root.GetProperty("activity_store_exit_policy").GetString());
        Assert.Equal("store_not_initialized", root.GetProperty("activity_store_exit_reason").GetString());
        Assert.False(root.GetProperty("host_running").GetBoolean());
        Assert.Equal("store_not_initialized", root.GetProperty("activity_store_operational_posture").GetString());
        Assert.Equal("Activity store has not recorded Gateway activity yet.", root.GetProperty("activity_store_operator_summary").GetString());
        Assert.Equal(1, root.GetProperty("activity_store_issue_count").GetInt32());
        Assert.Contains(
            root.GetProperty("activity_store_issues").EnumerateArray().Select(item => item.GetString()),
            issue => string.Equals(issue, "activity_store_not_initialized", StringComparison.Ordinal));
        Assert.Equal("start gateway and make a gateway request", root.GetProperty("activity_store_recommended_action").GetString());
        Assert.Equal("host_operational_evidence_not_governance_truth", root.GetProperty("activity_store_truth_boundary").GetString());
        Assert.False(root.GetProperty("activity_store_audit_truth").GetBoolean());
        Assert.False(root.GetProperty("activity_journal_exists").GetBoolean());
        Assert.Equal(0, root.GetProperty("activity_journal_manifest_record_count").GetInt64());
        Assert.Equal(0, root.GetProperty("activity_journal_manifest_byte_count").GetInt64());
        Assert.Equal(string.Empty, root.GetProperty("activity_journal_manifest_first_timestamp_utc").GetString());
        Assert.Equal(string.Empty, root.GetProperty("activity_journal_manifest_last_timestamp_utc").GetString());
        Assert.False(root.GetProperty("activity_store_maintenance_summary_exists").GetBoolean());
        Assert.False(root.GetProperty("activity_store_verification_summary_exists").GetBoolean());
        Assert.Equal("not_applicable", root.GetProperty("activity_store_maintenance_freshness_posture").GetString());
        Assert.Equal("start gateway and make a gateway request", root.GetProperty("activity_store_maintenance_freshness_recommended_action").GetString());
        Assert.Equal("not_applicable", root.GetProperty("activity_store_verification_freshness_posture").GetString());
        Assert.Equal("start gateway and make a gateway request", root.GetProperty("activity_store_verification_freshness_recommended_action").GetString());
        Assert.False(root.GetProperty("activity_store_verification_current_proof").GetBoolean());
        Assert.Equal("activity_store_not_initialized", root.GetProperty("activity_store_verification_current_proof_reason").GetString());
        Assert.Equal("start gateway and make a gateway request", root.GetProperty("activity_store_next_action").GetString());
        Assert.Equal("store_operational_posture", root.GetProperty("activity_store_next_action_source").GetString());
        Assert.Equal(1, root.GetProperty("activity_store_next_action_priority").GetInt32());
        Assert.Equal("store_not_initialized", root.GetProperty("activity_store_next_action_reason").GetString());
        Assert.Equal("stage_i_bounded_self_maintenance", root.GetProperty("activity_store_maturity_stage").GetString());
        Assert.False(root.GetProperty("activity_store_maturity_ready").GetBoolean());
        Assert.Equal("not_initialized", root.GetProperty("activity_store_maturity_posture").GetString());
        Assert.Contains("initialize", root.GetProperty("activity_store_maturity_summary").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            root.GetProperty("activity_store_maturity_limitations").EnumerateArray().Select(item => item.GetString()),
            limitation => string.Equals(limitation, "not_compliance_audit_truth", StringComparison.Ordinal));
    }

    [Fact]
    public void GatewayActivityStatus_ProjectsReadyStoreAfterGatewayActivity()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var dashboard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "dashboard", "--text");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "status", "--json");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(status.StandardOutput) ? status.StandardError : status.StandardOutput);
            var root = document.RootElement;

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, dashboard.ExitCode);
            Assert.Equal(0, status.ExitCode);
            Assert.Equal("carves-gateway-activity-status.v9", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("status_ok").GetBoolean());
            Assert.True(root.GetProperty("activity_store_operational_ok").GetBoolean());
            Assert.False(root.GetProperty("activity_store_maturity_required").GetBoolean());
            Assert.True(root.GetProperty("activity_store_exit_ok").GetBoolean());
            Assert.Equal("operational_only", root.GetProperty("activity_store_exit_policy").GetString());
            Assert.Equal("operational_ok", root.GetProperty("activity_store_exit_reason").GetString());
            Assert.True(root.GetProperty("host_running").GetBoolean());
            Assert.Equal("ready", root.GetProperty("activity_store_operational_posture").GetString());
            Assert.Equal("Activity store is ready for operator diagnosis.", root.GetProperty("activity_store_operator_summary").GetString());
            Assert.Equal(0, root.GetProperty("activity_store_issue_count").GetInt32());
            Assert.Empty(root.GetProperty("activity_store_issues").EnumerateArray());
            Assert.Equal("gateway activity verify", root.GetProperty("activity_store_recommended_action").GetString());
            Assert.Equal("latest_completed_maintenance_summary", root.GetProperty("activity_store_maintenance_freshness_mode").GetString());
            Assert.Equal(30, root.GetProperty("activity_store_maintenance_freshness_threshold_days").GetInt32());
            Assert.Equal("maintenance_missing", root.GetProperty("activity_store_maintenance_freshness_posture").GetString());
            Assert.Equal(-1, root.GetProperty("activity_store_maintenance_freshness_age_minutes").GetInt64());
            Assert.Equal("gateway activity maintenance", root.GetProperty("activity_store_maintenance_freshness_recommended_action").GetString());
            Assert.Equal("latest_completed_verification_summary", root.GetProperty("activity_store_verification_freshness_mode").GetString());
            Assert.Equal(24, root.GetProperty("activity_store_verification_freshness_threshold_hours").GetInt32());
            Assert.Equal("verification_missing", root.GetProperty("activity_store_verification_freshness_posture").GetString());
            Assert.Equal(-1, root.GetProperty("activity_store_verification_freshness_age_minutes").GetInt64());
            Assert.Equal("gateway activity verify", root.GetProperty("activity_store_verification_freshness_recommended_action").GetString());
            Assert.False(root.GetProperty("activity_store_verification_current_proof").GetBoolean());
            Assert.Equal("verification_summary_missing", root.GetProperty("activity_store_verification_current_proof_reason").GetString());
            Assert.Equal("gateway activity verify", root.GetProperty("activity_store_next_action").GetString());
            Assert.Equal("verification_freshness", root.GetProperty("activity_store_next_action_source").GetString());
            Assert.Equal(2, root.GetProperty("activity_store_next_action_priority").GetInt32());
            Assert.Equal("verification_missing", root.GetProperty("activity_store_next_action_reason").GetString());
            Assert.Equal("gateway activity verify", root.GetProperty("next_action").GetString());
            Assert.Equal("stage_i_bounded_self_maintenance", root.GetProperty("activity_store_maturity_stage").GetString());
            Assert.False(root.GetProperty("activity_store_maturity_ready").GetBoolean());
            Assert.Equal("verification_missing", root.GetProperty("activity_store_maturity_posture").GetString());
            Assert.Contains("fresh successful activity verification", root.GetProperty("activity_store_maturity_summary").GetString(), StringComparison.Ordinal);
            Assert.Equal("operator_diagnosis_evidence_not_audit_truth", root.GetProperty("activity_store_authority_posture").GetString());
            Assert.Equal("bounded_local_store_with_drop_telemetry_not_guaranteed_complete", root.GetProperty("activity_store_completeness_posture").GetString());
            Assert.False(root.GetProperty("activity_store_audit_truth").GetBoolean());
            Assert.Equal("available", root.GetProperty("activity_store_writer_lock_status").GetString());
            Assert.False(root.GetProperty("activity_store_writer_lock_currently_held").GetBoolean());
            Assert.True(root.GetProperty("activity_journal_exists").GetBoolean());
            Assert.True(root.GetProperty("activity_journal_manifest_exists").GetBoolean());
            Assert.True(root.GetProperty("activity_journal_checkpoint_chain_exists").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("activity_journal_archive_directory").GetString()));
            Assert.Equal(0, root.GetProperty("activity_journal_archive_segment_count").GetInt32());
            Assert.Equal(0, root.GetProperty("activity_journal_archive_byte_count").GetInt64());
            Assert.True(root.GetProperty("activity_journal_segment_count").GetInt32() >= 1);
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("activity_journal_manifest_generated_at_utc").GetString()));
            Assert.True(root.GetProperty("activity_journal_manifest_record_count").GetInt64() > 0);
            Assert.True(root.GetProperty("activity_journal_manifest_byte_count").GetInt64() > 0);
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("activity_journal_manifest_first_timestamp_utc").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("activity_journal_manifest_last_timestamp_utc").GetString()));
            Assert.True(root.GetProperty("activity_journal_line_count").GetInt64() > 0);

            var strictStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "status", "--json", "--require-maturity");
            using var strictDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(strictStatus.StandardOutput) ? strictStatus.StandardError : strictStatus.StandardOutput);
            var strictRoot = strictDocument.RootElement;

            Assert.Equal(1, strictStatus.ExitCode);
            Assert.False(strictRoot.GetProperty("status_ok").GetBoolean());
            Assert.True(strictRoot.GetProperty("activity_store_operational_ok").GetBoolean());
            Assert.True(strictRoot.GetProperty("activity_store_maturity_required").GetBoolean());
            Assert.False(strictRoot.GetProperty("activity_store_exit_ok").GetBoolean());
            Assert.Equal("operational_and_maturity_ready", strictRoot.GetProperty("activity_store_exit_policy").GetString());
            Assert.Equal("verification_missing", strictRoot.GetProperty("activity_store_exit_reason").GetString());

            var textStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "status");
            Assert.Equal(0, textStatus.ExitCode);
            Assert.Contains("CARVES gateway activity status", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store operational posture: ready", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store next action source: verification_freshness", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store maturity ready: False", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store maturity posture: verification_missing", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store maintenance freshness posture: maintenance_missing", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store verification freshness posture: verification_missing", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store operational ok: True", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store maturity required: False", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store exit policy: operational_only", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store exit ok: True", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store exit reason: operational_ok", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity journal archive segment count: 0", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity journal manifest records: ", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity journal manifest last UTC: ", textStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Status ok: True", textStatus.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayDoctor_ProjectsRouteNotFoundActivitySummary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/h8-doctor-route");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            var doctor = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "doctor", "--json", "--tail", "80");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(doctor.StandardOutput) ? doctor.StandardError : doctor.StandardOutput);
            var root = document.RootElement;

            Assert.Equal(0, doctor.ExitCode);
            Assert.Equal("carves-gateway-doctor.v14", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("activity_route_not_found_entry_count").GetInt32() >= 1);
            Assert.Equal("gateway-route-not-found", root.GetProperty("last_route_not_found_event").GetString());
            Assert.Equal("GET", root.GetProperty("last_route_not_found_method").GetString());
            Assert.Equal("/h8-doctor-route", root.GetProperty("last_route_not_found_path").GetString());
            Assert.Equal("404", root.GetProperty("last_route_not_found_status").GetString());
            Assert.True(root.GetProperty("activity_gateway_request_entry_count").GetInt32() >= 1);
            Assert.Equal("gateway-request", root.GetProperty("last_gateway_request_event").GetString());
            Assert.Equal("GET", root.GetProperty("last_gateway_request_method").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("last_gateway_request_path").GetString()));
            Assert.Equal("gateway activity visible", root.GetProperty("activity_next_action").GetString());
            Assert.Equal("activity_visibility", root.GetProperty("activity_next_action_source").GetString());
            Assert.Equal("activity_entries_available", root.GetProperty("activity_next_action_reason").GetString());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void GatewayDoctor_DistinguishesLockFileFromHeldLock()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        FileStream? lockBlocker = null;
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var runtimeDirectory = descriptorDocument.RootElement.GetProperty("runtime_directory").GetString();
            var lockPath = Path.Combine(runtimeDirectory!, "gateway-activity-store", "activity.lock");
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(runtimeDirectory));

            var dashboard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "dashboard", "--text");
            Assert.Equal(0, dashboard.ExitCode);

            lockBlocker = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var doctor = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "doctor", "--json", "--tail", "20");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(doctor.StandardOutput) ? doctor.StandardError : doctor.StandardOutput);
            var root = document.RootElement;

            Assert.Equal("carves-gateway-doctor.v14", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("activity_store_writer_lock_exists").GetBoolean());
            Assert.True(root.GetProperty("activity_store_writer_lock_file_exists").GetBoolean());
            Assert.True(root.GetProperty("activity_store_writer_lock_currently_held").GetBoolean());
            Assert.Equal("held", root.GetProperty("activity_store_writer_lock_status").GetString());
            Assert.Equal("writer_lock_held", root.GetProperty("activity_store_operational_posture").GetString());
            Assert.Contains(
                root.GetProperty("activity_store_issues").EnumerateArray().Select(item => item.GetString()),
                issue => string.Equals(issue, "writer_lock_currently_held", StringComparison.Ordinal));
            Assert.Equal(
                "retry after the current activity writer releases activity.lock",
                root.GetProperty("activity_next_action").GetString());
            Assert.Equal("store_operational_posture", root.GetProperty("activity_next_action_source").GetString());
            Assert.Equal(1, root.GetProperty("activity_next_action_priority").GetInt32());
            Assert.Equal("writer_lock_held", root.GetProperty("activity_next_action_reason").GetString());

            var activity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "20");
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            var activityRoot = activityDocument.RootElement;

            Assert.Equal(0, activity.ExitCode);
            Assert.Equal("writer_lock_held", activityRoot.GetProperty("activity_store_operational_posture").GetString());
            Assert.Equal(
                "retry after the current activity writer releases activity.lock",
                activityRoot.GetProperty("next_action").GetString());
            Assert.Equal("store_operational_posture", activityRoot.GetProperty("next_action_source").GetString());
            Assert.Equal(1, activityRoot.GetProperty("next_action_priority").GetInt32());
            Assert.Equal("writer_lock_held", activityRoot.GetProperty("next_action_reason").GetString());
        }
        finally
        {
            lockBlocker?.Dispose();
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayDoctor_ProjectsActivityDropTelemetryWhenStoreWriteIsBlocked()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        FileStream? lockBlocker = null;
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var descriptorRoot = descriptorDocument.RootElement;
            var baseUrl = descriptorRoot.GetProperty("base_url").GetString();
            var runtimeDirectory = descriptorRoot.GetProperty("runtime_directory").GetString();
            var lockPath = Path.Combine(runtimeDirectory!, "gateway-activity-store", "activity.lock");
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));
            Assert.False(string.IsNullOrWhiteSpace(runtimeDirectory));

            lockBlocker = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var requestTask = client.GetAsync($"{baseUrl!.TrimEnd('/')}/h1-drop-telemetry-route");
            await Task.Delay(TimeSpan.FromMilliseconds(6500));
            await lockBlocker.DisposeAsync();
            lockBlocker = null;
            using var unknownRoute = await requestTask.WaitAsync(TimeSpan.FromSeconds(25));
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            var doctor = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "doctor", "--json", "--tail", "80");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(doctor.StandardOutput) ? doctor.StandardError : doctor.StandardOutput);
            var root = document.RootElement;

            Assert.Equal(0, doctor.ExitCode);
            Assert.Equal("carves-gateway-doctor.v14", root.GetProperty("schema_version").GetString());
            Assert.Equal("carves-gateway-activity-drop-telemetry.v1", root.GetProperty("activity_store_drop_telemetry_schema_version").GetString());
            Assert.True(root.GetProperty("activity_store_drop_telemetry_exists").GetBoolean());
            Assert.True(root.GetProperty("activity_store_dropped_activity_count").GetInt64() >= 1);
            Assert.Equal("activity_store_lock_unavailable", root.GetProperty("activity_store_last_drop_reason").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("activity_store_last_drop_at_utc").GetString()));
            Assert.StartsWith("gateway-", root.GetProperty("activity_store_last_drop_event").GetString(), StringComparison.Ordinal);
            Assert.StartsWith("gwreq-", root.GetProperty("activity_store_last_drop_request_id").GetString(), StringComparison.Ordinal);
            Assert.Equal(
                "inspect gateway doctor drop telemetry and gateway logs",
                root.GetProperty("activity_next_action").GetString());
            Assert.Equal("store_operational_posture", root.GetProperty("activity_next_action_source").GetString());
            Assert.Equal("activity_loss_detected", root.GetProperty("activity_next_action_reason").GetString());

            var activity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "80");
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            var activityRoot = activityDocument.RootElement;

            Assert.Equal(0, activity.ExitCode);
            Assert.Equal("activity_loss_detected", activityRoot.GetProperty("activity_store_operational_posture").GetString());
            Assert.Equal(
                "inspect gateway doctor drop telemetry and gateway logs",
                activityRoot.GetProperty("next_action").GetString());
            Assert.Equal("store_operational_posture", activityRoot.GetProperty("next_action_source").GetString());
            Assert.Equal("activity_loss_detected", activityRoot.GetProperty("next_action_reason").GetString());
        }
        finally
        {
            if (lockBlocker is not null)
            {
                await lockBlocker.DisposeAsync();
            }

            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayLogs_ProjectsStartedGatewayLogPaths()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            var dashboard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "dashboard", "--text");
            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/h4-unknown-route");
            var logs = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "logs", "--json", "--tail", "40");
            var activity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "80");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(logs.StandardOutput) ? logs.StandardError : logs.StandardOutput);
            var root = document.RootElement;
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            var activityRoot = activityDocument.RootElement;
            var stdoutPath = root.GetProperty("standard_output_log_path").GetString();
            var stderrPath = root.GetProperty("standard_error_log_path").GetString();
            var stdoutTail = root.GetProperty("standard_output_tail")
                .EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .ToArray();
            var activityEntries = activityRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, dashboard.ExitCode);
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);
            Assert.Equal(0, logs.ExitCode);
            Assert.Equal(0, activity.ExitCode);
            Assert.True(root.GetProperty("host_running").GetBoolean());
            Assert.True(root.GetProperty("logs_available").GetBoolean());
            Assert.Equal("carves-gateway-activity.v17", activityRoot.GetProperty("schema_version").GetString());
            Assert.Equal("carves-gateway-activity-events.v3", activityRoot.GetProperty("event_catalog_version").GetString());
            Assert.Equal("carves-gateway-activity-store.v1", activityRoot.GetProperty("activity_store_contract_version").GetString());
            Assert.Equal("carves-gateway-activity-store-record.v1", activityRoot.GetProperty("activity_store_record_schema_version").GetString());
            Assert.Equal("carves-gateway-activity-manifest-checkpoint.v1", activityRoot.GetProperty("activity_store_checkpoint_schema_version").GetString());
            Assert.Equal("docs/runtime/gateway-activity-store-v1-contract.md", activityRoot.GetProperty("activity_store_contract_path").GetString());
            Assert.Equal("docs/contracts/gateway-activity-store-v1.schema.json", activityRoot.GetProperty("activity_store_schema_path").GetString());
            Assert.Equal("docs/contracts/gateway-activity-segment-manifest-v1.schema.json", activityRoot.GetProperty("activity_store_manifest_schema_path").GetString());
            Assert.Equal("docs/contracts/gateway-activity-manifest-checkpoint-v1.schema.json", activityRoot.GetProperty("activity_store_checkpoint_schema_path").GetString());
            Assert.Equal("host_operational_evidence_not_governance_truth", activityRoot.GetProperty("activity_store_truth_boundary").GetString());
            Assert.Equal("operator_diagnosis_evidence_not_audit_truth", activityRoot.GetProperty("activity_store_authority_posture").GetString());
            Assert.Equal("bounded_local_store_with_drop_telemetry_not_guaranteed_complete", activityRoot.GetProperty("activity_store_completeness_posture").GetString());
            Assert.False(activityRoot.GetProperty("activity_store_audit_truth").GetBoolean());
            Assert.Equal("diagnose_gateway_requests_and_feedback_only", activityRoot.GetProperty("activity_store_operator_use").GetString());
            Assert.Contains(
                activityRoot.GetProperty("activity_store_not_for").EnumerateArray().Select(item => item.GetString()),
                posture => string.Equals(posture, "approval_truth", StringComparison.Ordinal));
            Assert.Equal("ready", activityRoot.GetProperty("activity_store_operational_posture").GetString());
            Assert.Equal("Activity store is ready for operator diagnosis.", activityRoot.GetProperty("activity_store_operator_summary").GetString());
            Assert.Equal(0, activityRoot.GetProperty("activity_store_issue_count").GetInt32());
            Assert.Empty(activityRoot.GetProperty("activity_store_issues").EnumerateArray());
            Assert.Equal("gateway activity verify", activityRoot.GetProperty("activity_store_recommended_action").GetString());
            Assert.Equal("verification_missing", activityRoot.GetProperty("activity_store_verification_freshness_posture").GetString());
            Assert.False(activityRoot.GetProperty("activity_store_verification_current_proof").GetBoolean());
            Assert.Equal("verification_summary_missing", activityRoot.GetProperty("activity_store_verification_current_proof_reason").GetString());
            Assert.Equal("maintenance_missing", activityRoot.GetProperty("activity_store_maintenance_freshness_posture").GetString());
            Assert.Equal("stage_i_bounded_self_maintenance", activityRoot.GetProperty("activity_store_maturity_stage").GetString());
            Assert.False(activityRoot.GetProperty("activity_store_maturity_ready").GetBoolean());
            Assert.Equal("verification_missing", activityRoot.GetProperty("activity_store_maturity_posture").GetString());
            Assert.Equal("non_destructive_archive_no_delete", activityRoot.GetProperty("activity_store_retention_mode").GetString());
            Assert.Equal("automatic_on_append_and_manual_archive", activityRoot.GetProperty("activity_store_retention_execution_mode").GetString());
            Assert.Equal(30, activityRoot.GetProperty("activity_store_default_archive_before_days").GetInt32());
            Assert.Equal("bounded_file_lock", activityRoot.GetProperty("activity_store_writer_lock_mode").GetString());
            Assert.True(File.Exists(activityRoot.GetProperty("activity_store_writer_lock_path").GetString()));
            Assert.True(activityRoot.GetProperty("activity_store_writer_lock_exists").GetBoolean());
            Assert.True(activityRoot.GetProperty("activity_store_writer_lock_file_exists").GetBoolean());
            Assert.False(activityRoot.GetProperty("activity_store_writer_lock_currently_held").GetBoolean());
            Assert.Equal("available", activityRoot.GetProperty("activity_store_writer_lock_status").GetString());
            Assert.Equal(5000, activityRoot.GetProperty("activity_store_writer_lock_acquire_timeout_ms").GetInt32());
            Assert.Equal("segment_manifest_sha256_checkpoint_chain", activityRoot.GetProperty("activity_store_integrity_mode").GetString());
            Assert.Equal("carves-gateway-activity-drop-telemetry.v1", activityRoot.GetProperty("activity_store_drop_telemetry_schema_version").GetString());
            Assert.False(string.IsNullOrWhiteSpace(activityRoot.GetProperty("activity_store_drop_telemetry_path").GetString()));
            Assert.False(activityRoot.GetProperty("activity_store_drop_telemetry_exists").GetBoolean());
            Assert.Equal(0, activityRoot.GetProperty("activity_store_dropped_activity_count").GetInt64());
            Assert.Equal(string.Empty, activityRoot.GetProperty("activity_store_last_drop_reason").GetString());
            Assert.Contains(
                activityRoot.GetProperty("activity_store_envelope_required_fields").EnumerateArray().Select(item => item.GetString()),
                field => string.Equals(field, "event_id", StringComparison.Ordinal));
            Assert.Contains(
                activityRoot.GetProperty("activity_store_stable_query_fields").EnumerateArray().Select(item => item.GetString()),
                field => string.Equals(field, "request_id", StringComparison.Ordinal));
            Assert.Contains(
                activityRoot.GetProperty("activity_store_forbidden_field_families").EnumerateArray().Select(item => item.GetString()),
                field => string.Equals(field, "raw_message_text", StringComparison.Ordinal));
            Assert.Contains(
                activityRoot.GetProperty("activity_store_route_coverage_families").EnumerateArray().Select(item => item.GetString()),
                route => string.Equals(route, "/sessions/{session_id}/messages", StringComparison.Ordinal));
            Assert.Contains(
                activityRoot.GetProperty("known_categories").EnumerateArray().Select(item => item.GetString()),
                category => string.Equals(category, "route_not_found", StringComparison.Ordinal));
            Assert.Contains(
                activityRoot.GetProperty("known_categories").EnumerateArray().Select(item => item.GetString()),
                category => string.Equals(category, "gateway_request", StringComparison.Ordinal));
            Assert.Equal("carves-gateway-activity-journal.v1", activityRoot.GetProperty("activity_journal_schema_version").GetString());
            Assert.Equal("activity_journal", activityRoot.GetProperty("activity_source").GetString());
            Assert.Equal("tail_window", activityRoot.GetProperty("activity_query_mode").GetString());
            Assert.False(activityRoot.GetProperty("activity_query_store_backed_since").GetBoolean());
            Assert.True(activityRoot.GetProperty("activity_query_tail_bounded").GetBoolean());
            Assert.Equal("segmented_append_only", activityRoot.GetProperty("activity_journal_storage_mode").GetString());
            Assert.True(Directory.Exists(activityRoot.GetProperty("activity_journal_segment_directory").GetString()));
            Assert.True(File.Exists(activityRoot.GetProperty("activity_journal_active_segment_path").GetString()));
            Assert.True(activityRoot.GetProperty("activity_journal_segment_count").GetInt32() >= 1);
            Assert.True(File.Exists(activityRoot.GetProperty("activity_journal_manifest_path").GetString()));
            Assert.True(activityRoot.GetProperty("activity_journal_manifest_exists").GetBoolean());
            Assert.Equal("carves-gateway-activity-segment-manifest.v1", activityRoot.GetProperty("activity_journal_manifest_schema_version").GetString());
            Assert.True(File.Exists(activityRoot.GetProperty("activity_journal_checkpoint_chain_path").GetString()));
            Assert.True(activityRoot.GetProperty("activity_journal_checkpoint_chain_exists").GetBoolean());
            Assert.True(activityRoot.GetProperty("activity_journal_checkpoint_chain_count").GetInt32() > 0);
            Assert.True(activityRoot.GetProperty("activity_journal_checkpoint_chain_latest_sequence").GetInt64() > 0);
            Assert.Matches("^[0-9a-f]{64}$", activityRoot.GetProperty("activity_journal_checkpoint_chain_latest_checkpoint_sha256").GetString() ?? string.Empty);
            Assert.Matches("^[0-9a-f]{64}$", activityRoot.GetProperty("activity_journal_checkpoint_chain_latest_manifest_sha256").GetString() ?? string.Empty);
            Assert.False(string.IsNullOrWhiteSpace(activityRoot.GetProperty("activity_journal_manifest_generated_at_utc").GetString()));
            Assert.True(activityRoot.GetProperty("activity_journal_manifest_record_count").GetInt64() > 0);
            Assert.True(activityRoot.GetProperty("activity_journal_manifest_byte_count").GetInt64() > 0);
            Assert.False(string.IsNullOrWhiteSpace(activityRoot.GetProperty("activity_journal_manifest_first_timestamp_utc").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(activityRoot.GetProperty("activity_journal_manifest_last_timestamp_utc").GetString()));
            var activitySegments = activityRoot.GetProperty("activity_journal_segments").EnumerateArray().ToArray();
            Assert.True(activitySegments.Length >= 1);
            var activeSegment = activitySegments.Single(segment =>
                string.Equals(
                    segment.GetProperty("path").GetString(),
                    activityRoot.GetProperty("activity_journal_active_segment_path").GetString(),
                    StringComparison.Ordinal));
            Assert.True(File.Exists(activeSegment.GetProperty("path").GetString()));
            Assert.StartsWith("activity-", activeSegment.GetProperty("file_name").GetString());
            Assert.True(activeSegment.GetProperty("record_count").GetInt64() > 0);
            Assert.True(activeSegment.GetProperty("byte_count").GetInt64() > 0);
            Assert.False(string.IsNullOrWhiteSpace(activeSegment.GetProperty("first_timestamp_utc").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(activeSegment.GetProperty("last_timestamp_utc").GetString()));
            Assert.Matches("^[0-9a-f]{64}$", activeSegment.GetProperty("sha256").GetString() ?? string.Empty);
            Assert.False(activityRoot.GetProperty("activity_journal_legacy_fallback_used").GetBoolean());
            Assert.True(activityRoot.GetProperty("activity_journal_exists").GetBoolean());
            Assert.True(activityRoot.GetProperty("activity_journal_byte_count").GetInt64() > 0);
            Assert.True(activityRoot.GetProperty("activity_journal_line_count").GetInt64() > 0);
            Assert.False(activityRoot.GetProperty("activity_journal_compact_requested").GetBoolean());
            Assert.False(activityRoot.GetProperty("activity_filter_applied").GetBoolean());
            Assert.Equal("all", activityRoot.GetProperty("activity_filter_category").GetString());
            Assert.Equal(string.Empty, activityRoot.GetProperty("activity_filter_command").GetString());
            Assert.Equal(string.Empty, activityRoot.GetProperty("activity_filter_path").GetString());
            Assert.Equal(string.Empty, activityRoot.GetProperty("activity_filter_request_id").GetString());
            Assert.Equal(string.Empty, activityRoot.GetProperty("activity_filter_operation_id").GetString());
            Assert.Equal(string.Empty, activityRoot.GetProperty("activity_filter_session_id").GetString());
            Assert.Equal(string.Empty, activityRoot.GetProperty("activity_filter_message_id").GetString());
            Assert.Equal(0, activityRoot.GetProperty("activity_filter_since_minutes").GetInt32());
            Assert.Equal(string.Empty, activityRoot.GetProperty("activity_filter_since_utc").GetString());
            Assert.True(activityRoot.GetProperty("source_entry_count").GetInt32() >= activityRoot.GetProperty("entry_count").GetInt32());
            Assert.True(activityRoot.GetProperty("request_entry_count").GetInt32() > 0);
            Assert.True(activityRoot.GetProperty("feedback_entry_count").GetInt32() > 0);
            Assert.True(activityRoot.GetProperty("cli_invoke_entry_count").GetInt32() > 0);
            Assert.True(activityRoot.GetProperty("route_not_found_entry_count").GetInt32() > 0);
            Assert.True(activityRoot.GetProperty("gateway_request_entry_count").GetInt32() > 0);
            Assert.Contains(
                activityRoot.GetProperty("known_event_kinds").EnumerateArray().Select(item => item.GetString()),
                kind => string.Equals(kind, "gateway-request", StringComparison.Ordinal));
            Assert.Contains(
                activityRoot.GetProperty("known_event_kinds").EnumerateArray().Select(item => item.GetString()),
                kind => string.Equals(kind, "gateway-request-failed", StringComparison.Ordinal));
            Assert.Contains(
                activityRoot.GetProperty("known_event_kinds").EnumerateArray().Select(item => item.GetString()),
                kind => string.Equals(kind, "gateway-session-message-response", StringComparison.Ordinal));
            Assert.Contains(
                activityRoot.GetProperty("known_event_kinds").EnumerateArray().Select(item => item.GetString()),
                kind => string.Equals(kind, "gateway-route-not-found", StringComparison.Ordinal));
            Assert.True(activityRoot.GetProperty("logs_available").GetBoolean());
            Assert.True(activityRoot.GetProperty("activity_available").GetBoolean());
            Assert.Equal("gateway activity visible", activityRoot.GetProperty("next_action").GetString());
            Assert.Equal("activity_visibility", activityRoot.GetProperty("next_action_source").GetString());
            Assert.Equal(0, activityRoot.GetProperty("next_action_priority").GetInt32());
            Assert.Equal("activity_entries_available", activityRoot.GetProperty("next_action_reason").GetString());
            Assert.True(root.GetProperty("standard_output_log_exists").GetBoolean());
            Assert.True(root.GetProperty("standard_error_log_exists").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(stdoutPath));
            Assert.False(string.IsNullOrWhiteSpace(stderrPath));
            Assert.True(File.Exists(stdoutPath));
            Assert.True(File.Exists(stderrPath));
            Assert.Equal(40, root.GetProperty("tail_line_count").GetInt32());
            Assert.Contains(stdoutTail, line => line.Contains("gateway-started", StringComparison.Ordinal));
            Assert.Contains(stdoutTail, line => line.Contains("gateway-request", StringComparison.Ordinal));
            Assert.Contains(stdoutTail, line => line.Contains("path=/handshake", StringComparison.Ordinal));
            Assert.Contains(stdoutTail, line => line.Contains("gateway-invoke-request", StringComparison.Ordinal)
                                                && line.Contains("command=dashboard", StringComparison.Ordinal)
                                                && line.Contains("arguments=--text", StringComparison.Ordinal));
            Assert.Contains(stdoutTail, line => line.Contains("gateway-invoke-response", StringComparison.Ordinal)
                                                && line.Contains("command=dashboard", StringComparison.Ordinal)
                                                && line.Contains("exit_code=0", StringComparison.Ordinal));
            Assert.Contains(stdoutTail, line => line.Contains("gateway-route-not-found", StringComparison.Ordinal)
                                                && line.Contains("path=/h4-unknown-route", StringComparison.Ordinal)
                                                && line.Contains("status=404", StringComparison.Ordinal));
            Assert.Contains(activityEntries, entry =>
                entry.GetProperty("event").GetString() == "gateway-invoke-request"
                && entry.GetProperty("fields").GetProperty("command").GetString() == "dashboard"
                && entry.GetProperty("fields").GetProperty("arguments").GetString() == "--text");
            Assert.Contains(activityEntries, entry =>
                entry.GetProperty("event").GetString() == "gateway-invoke-response"
                && entry.GetProperty("fields").GetProperty("command").GetString() == "dashboard"
                && entry.GetProperty("fields").GetProperty("exit_code").GetString() == "0");
            Assert.Contains(activityEntries, entry =>
                entry.GetProperty("event").GetString() == "gateway-request"
                && entry.GetProperty("fields").GetProperty("method").GetString() == "GET"
                && entry.GetProperty("fields").GetProperty("path").GetString() == "/handshake");
            Assert.Contains(activityEntries, entry =>
                entry.GetProperty("event").GetString() == "gateway-request"
                && entry.GetProperty("fields").GetProperty("method").GetString() == "POST"
                && entry.GetProperty("fields").GetProperty("path").GetString() == "/invoke");
            Assert.Contains(activityEntries, entry =>
                entry.GetProperty("event").GetString() == "gateway-route-not-found"
                && entry.GetProperty("fields").GetProperty("method").GetString() == "GET"
                && entry.GetProperty("fields").GetProperty("path").GetString() == "/h4-unknown-route"
                && entry.GetProperty("fields").GetProperty("status").GetString() == "404");
            var routeGatewayRequest = activityEntries.Single(entry =>
                entry.GetProperty("event").GetString() == "gateway-request"
                && entry.GetProperty("fields").GetProperty("method").GetString() == "GET"
                && entry.GetProperty("fields").GetProperty("path").GetString() == "/h4-unknown-route");
            var routeNotFound = activityEntries.Single(entry =>
                entry.GetProperty("event").GetString() == "gateway-route-not-found"
                && entry.GetProperty("fields").GetProperty("path").GetString() == "/h4-unknown-route");
            var routeRequestId = routeGatewayRequest.GetProperty("fields").GetProperty("request_id").GetString();
            Assert.NotNull(routeRequestId);
            Assert.StartsWith("gwreq-", routeRequestId);
            Assert.Equal(routeRequestId, routeNotFound.GetProperty("fields").GetProperty("request_id").GetString());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayActivity_UsesJournalWhenStdoutTailCannotProveActivity()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var descriptorRoot = descriptorDocument.RootElement;
            var baseUrl = descriptorRoot.GetProperty("base_url").GetString();
            var deploymentDirectory = descriptorRoot.GetProperty("deployment_directory").GetString();
            var stdoutPath = Path.Combine(deploymentDirectory!, "logs", "host.stdout.log");
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));
            Assert.False(string.IsNullOrWhiteSpace(deploymentDirectory));

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/h5-journal-only-route");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            File.WriteAllText(stdoutPath, string.Empty);
            var activity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "80");
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            var activityRoot = activityDocument.RootElement;
            var activityEntries = activityRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, activity.ExitCode);
            Assert.Equal("carves-gateway-activity.v17", activityRoot.GetProperty("schema_version").GetString());
            Assert.Equal("activity_journal", activityRoot.GetProperty("activity_source").GetString());
            Assert.Equal("segmented_append_only", activityRoot.GetProperty("activity_journal_storage_mode").GetString());
            Assert.True(File.Exists(activityRoot.GetProperty("activity_journal_active_segment_path").GetString()));
            Assert.True(activityRoot.GetProperty("activity_journal_exists").GetBoolean());
            Assert.Equal("gateway activity visible", activityRoot.GetProperty("next_action").GetString());
            Assert.Equal("activity_visibility", activityRoot.GetProperty("next_action_source").GetString());
            Assert.Equal("activity_entries_available", activityRoot.GetProperty("next_action_reason").GetString());
            Assert.Contains(activityEntries, entry =>
                entry.GetProperty("event").GetString() == "gateway-route-not-found"
                && entry.GetProperty("fields").GetProperty("path").GetString() == "/h5-journal-only-route"
                && entry.GetProperty("fields").GetProperty("status").GetString() == "404");
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void GatewayActivity_ReadsLegacyJournalWhenSegmentsAreAbsent()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var deploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "legacy-activity-test");
        Directory.CreateDirectory(runtimeDirectory);
        Directory.CreateDirectory(deploymentDirectory);
        WriteHostDescriptor(
            sandbox.RootPath,
            processId: 999_999,
            runtimeDirectory,
            deploymentDirectory,
            Path.Combine(deploymentDirectory, "Carves.Runtime.Host.dll"),
            "http://127.0.0.1:1",
            port: 1);
        var legacyJournalPath = Path.Combine(runtimeDirectory, "gateway-activity.jsonl");
        File.WriteAllText(
            legacyJournalPath,
            $"{{\"schema_version\":\"carves-gateway-activity-journal.v1\",\"timestamp\":\"{DateTimeOffset.UtcNow:O}\",\"event\":\"gateway-route-not-found\",\"fields\":{{\"method\":\"GET\",\"path\":\"/legacy-activity-route\",\"status\":\"404\"}}}}{Environment.NewLine}");

        var activity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "20");
        using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
        var activityRoot = activityDocument.RootElement;
        var activityEntries = activityRoot.GetProperty("entries").EnumerateArray().ToArray();

        Assert.Equal(0, activity.ExitCode);
        Assert.Equal("legacy_single_file", activityRoot.GetProperty("activity_journal_storage_mode").GetString());
        Assert.Equal(0, activityRoot.GetProperty("activity_journal_segment_count").GetInt32());
        Assert.False(activityRoot.GetProperty("activity_journal_manifest_exists").GetBoolean());
        Assert.Empty(activityRoot.GetProperty("activity_journal_segments").EnumerateArray());
        Assert.True(activityRoot.GetProperty("activity_journal_legacy_exists").GetBoolean());
        Assert.True(activityRoot.GetProperty("activity_journal_legacy_fallback_used").GetBoolean());
        Assert.Equal(legacyJournalPath, activityRoot.GetProperty("activity_journal_legacy_path").GetString());
        Assert.Contains(activityEntries, entry =>
            entry.GetProperty("event").GetString() == "gateway-route-not-found"
            && entry.GetProperty("fields").GetProperty("path").GetString() == "/legacy-activity-route"
            && entry.GetProperty("fields").GetProperty("status").GetString() == "404");
    }

    [Fact]
    public async Task GatewayActivity_AppendsSegmentAcrossGatewayRestart()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            using var beforeRestart = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/a2-segment-before-restart");
            Assert.Equal(HttpStatusCode.NotFound, beforeRestart.StatusCode);

            var firstActivity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "80");
            using var firstActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(firstActivity.StandardOutput) ? firstActivity.StandardError : firstActivity.StandardOutput);
            var firstRoot = firstActivityDocument.RootElement;
            var firstSegmentPath = firstRoot.GetProperty("activity_journal_active_segment_path").GetString();
            var firstLineCount = firstRoot.GetProperty("activity_journal_line_count").GetInt64();

            Assert.Equal(0, firstActivity.ExitCode);
            Assert.Equal("segmented_append_only", firstRoot.GetProperty("activity_journal_storage_mode").GetString());
            Assert.False(string.IsNullOrWhiteSpace(firstSegmentPath));
            Assert.True(File.Exists(firstSegmentPath));
            Assert.True(firstLineCount > 0);

            StopHost(sandbox.RootPath);

            var restart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            using var restartedDescriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var restartedBaseUrl = restartedDescriptorDocument.RootElement.GetProperty("base_url").GetString();

            Assert.Equal(0, restart.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(restartedBaseUrl));

            using var afterRestart = await client.GetAsync($"{restartedBaseUrl!.TrimEnd('/')}/a2-segment-after-restart");
            Assert.Equal(HttpStatusCode.NotFound, afterRestart.StatusCode);

            var secondActivity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "120");
            using var secondActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(secondActivity.StandardOutput) ? secondActivity.StandardError : secondActivity.StandardOutput);
            var secondRoot = secondActivityDocument.RootElement;
            var secondEntries = secondRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, secondActivity.ExitCode);
            Assert.Equal("segmented_append_only", secondRoot.GetProperty("activity_journal_storage_mode").GetString());
            Assert.Equal(firstSegmentPath, secondRoot.GetProperty("activity_journal_active_segment_path").GetString());
            Assert.True(File.Exists(secondRoot.GetProperty("activity_journal_manifest_path").GetString()));
            Assert.True(secondRoot.GetProperty("activity_journal_manifest_exists").GetBoolean());
            Assert.True(secondRoot.GetProperty("activity_journal_manifest_record_count").GetInt64() >= firstLineCount);
            Assert.True(secondRoot.GetProperty("activity_journal_line_count").GetInt64() >= firstLineCount);
            Assert.Contains(secondEntries, entry =>
                entry.GetProperty("event").GetString() == "gateway-route-not-found"
                && entry.GetProperty("fields").GetProperty("path").GetString() == "/a2-segment-after-restart");
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayActivity_ConcurrentRequestsKeepSegmentStoreVerifiable()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            var responses = await Task.WhenAll(
                Enumerable.Range(0, 16)
                    .Select(index => client.GetAsync($"{baseUrl!.TrimEnd('/')}/a10-concurrent-route-{index}")));
            try
            {
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }

            var verify = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "verify", "--json");
            using var verifyDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(verify.StandardOutput) ? verify.StandardError : verify.StandardOutput);
            var verifyRoot = verifyDocument.RootElement;

            Assert.Equal(0, verify.ExitCode);
            Assert.True(verifyRoot.GetProperty("verification_passed").GetBoolean());
            Assert.Equal(0, verifyRoot.GetProperty("issue_count").GetInt32());

            var activity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "120",
                "--category",
                "route_not_found");
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            var activityRoot = activityDocument.RootElement;
            var routeEntries = activityRoot.GetProperty("entries").EnumerateArray().ToArray();
            var concurrentRouteCount = routeEntries.Count(entry =>
                entry.GetProperty("fields").GetProperty("path").GetString()?.StartsWith("/a10-concurrent-route-", StringComparison.Ordinal) == true);

            Assert.Equal(0, activity.ExitCode);
            Assert.Equal(16, concurrentRouteCount);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayActivity_DoesNotDestructivelyCompactSegmentStore()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            for (var index = 0; index < 6; index++)
            {
                using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/h6-compact-route-{index}");
                Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);
            }

            StopHost(sandbox.RootPath);

            var activity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "20",
                "--compact",
                "--retain",
                "4");
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            var activityRoot = activityDocument.RootElement;
            var activityEntries = activityRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, activity.ExitCode);
            Assert.Equal("carves-gateway-activity.v17", activityRoot.GetProperty("schema_version").GetString());
            Assert.Equal("activity_journal", activityRoot.GetProperty("activity_source").GetString());
            Assert.Equal("segmented_append_only", activityRoot.GetProperty("activity_journal_storage_mode").GetString());
            Assert.True(activityRoot.GetProperty("activity_journal_exists").GetBoolean());
            Assert.True(activityRoot.GetProperty("activity_journal_compact_requested").GetBoolean());
            Assert.False(activityRoot.GetProperty("activity_journal_compact_applied").GetBoolean());
            Assert.Equal("segment_store_append_only", activityRoot.GetProperty("activity_journal_compact_reason").GetString());
            Assert.Equal(4, activityRoot.GetProperty("activity_journal_compact_retain_line_limit").GetInt32());
            Assert.True(activityRoot.GetProperty("activity_journal_compact_original_line_count").GetInt64() > 4);
            Assert.True(activityRoot.GetProperty("activity_journal_line_count").GetInt64() > 4);
            Assert.Contains(activityEntries, entry =>
                entry.GetProperty("event").GetString() == "gateway-route-not-found"
                && entry.GetProperty("fields").GetProperty("path").GetString() == "/h6-compact-route-5"
                && entry.GetProperty("fields").GetProperty("status").GetString() == "404");
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayActivityArchive_MovesOldSegmentsWithoutDeletingEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        string oldSegmentPath = string.Empty;
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/a6-archive-current-route");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            StopHost(sandbox.RootPath);

            var beforeActivity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "20");
            using var beforeActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(beforeActivity.StandardOutput) ? beforeActivity.StandardError : beforeActivity.StandardOutput);
            var beforeActivityRoot = beforeActivityDocument.RootElement;
            var segmentDirectory = beforeActivityRoot.GetProperty("activity_journal_segment_directory").GetString();
            var archiveDirectory = beforeActivityRoot.GetProperty("activity_journal_archive_directory").GetString();
            Assert.Equal(0, beforeActivity.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(segmentDirectory));
            Assert.False(string.IsNullOrWhiteSpace(archiveDirectory));

            oldSegmentPath = Path.Combine(segmentDirectory!, "activity-20000101-0001.jsonl");
            Directory.CreateDirectory(segmentDirectory!);
            var oldRecord = new
            {
                SchemaVersion = "carves-gateway-activity-journal.v1",
                Timestamp = DateTimeOffset.Parse("2000-01-01T00:00:00+00:00"),
                Event = "gateway-route-not-found",
                Fields = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["method"] = "GET",
                    ["path"] = "/a6-archive-old-route",
                    ["status"] = "404",
                },
            };
            File.WriteAllText(
                oldSegmentPath,
                JsonSerializer.Serialize(oldRecord, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) + Environment.NewLine,
                Encoding.UTF8);

            var archive = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "archive",
                "--json",
                "--before-days",
                "1");
            using var archiveDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(archive.StandardOutput) ? archive.StandardError : archive.StandardOutput);
            var archiveRoot = archiveDocument.RootElement;
            var archivedSegments = archiveRoot.GetProperty("archived_segments").EnumerateArray().ToArray();
            var archivedPath = archivedSegments.Single().GetProperty("archive_path").GetString();

            Assert.Equal(0, archive.ExitCode);
            Assert.Equal("carves-gateway-activity-archive.v6", archiveRoot.GetProperty("schema_version").GetString());
            Assert.Equal("non_destructive_segment_move", archiveRoot.GetProperty("archive_mode").GetString());
            Assert.True(archiveRoot.GetProperty("archive_requested").GetBoolean());
            Assert.True(archiveRoot.GetProperty("archive_applied").GetBoolean());
            Assert.Equal("segments_archived", archiveRoot.GetProperty("archive_reason").GetString());
            Assert.Equal(1, archiveRoot.GetProperty("candidate_segment_count").GetInt32());
            Assert.Equal(1, archiveRoot.GetProperty("archived_segment_count").GetInt32());
            Assert.True(archiveRoot.GetProperty("activity_store_maintenance_summary_exists").GetBoolean());
            Assert.Equal("manual_archive", archiveRoot.GetProperty("activity_store_last_maintenance_operation").GetString());
            Assert.True(archiveRoot.GetProperty("activity_store_last_maintenance_applied").GetBoolean());
            Assert.Equal("segments_archived", archiveRoot.GetProperty("activity_store_last_maintenance_reason").GetString());
            Assert.Equal(1, archiveRoot.GetProperty("activity_store_last_maintenance_archived_segment_count").GetInt32());
            Assert.Equal("gateway activity verify", archiveRoot.GetProperty("activity_store_last_maintenance_recommended_action").GetString());
            Assert.Equal("gateway activity verify", archiveRoot.GetProperty("next_action").GetString());
            Assert.Equal("archive_result", archiveRoot.GetProperty("next_action_source").GetString());
            Assert.Equal(2, archiveRoot.GetProperty("next_action_priority").GetInt32());
            Assert.Equal("segments_archived", archiveRoot.GetProperty("next_action_reason").GetString());
            Assert.False(File.Exists(oldSegmentPath));
            Assert.False(string.IsNullOrWhiteSpace(archivedPath));
            Assert.StartsWith(archiveDirectory!, archivedPath!, StringComparison.Ordinal);
            Assert.True(File.Exists(archivedPath));

            var afterActivity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "80");
            using var afterActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(afterActivity.StandardOutput) ? afterActivity.StandardError : afterActivity.StandardOutput);
            var afterActivityRoot = afterActivityDocument.RootElement;
            var activeSegments = afterActivityRoot.GetProperty("activity_journal_segments")
                .EnumerateArray()
                .Select(segment => segment.GetProperty("file_name").GetString())
                .ToArray();

            Assert.Equal(0, afterActivity.ExitCode);
            Assert.Equal("carves-gateway-activity.v17", afterActivityRoot.GetProperty("schema_version").GetString());
            Assert.Equal(1, afterActivityRoot.GetProperty("activity_journal_archive_segment_count").GetInt32());
            Assert.True(afterActivityRoot.GetProperty("activity_journal_archive_byte_count").GetInt64() > 0);
            Assert.DoesNotContain("activity-20000101-0001.jsonl", activeSegments);

            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "status", "--json");
            using var statusDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(status.StandardOutput) ? status.StandardError : status.StandardOutput);
            var statusRoot = statusDocument.RootElement;

            Assert.Equal(0, status.ExitCode);
            Assert.Equal("carves-gateway-activity-status.v9", statusRoot.GetProperty("schema_version").GetString());
            Assert.Equal(1, statusRoot.GetProperty("activity_journal_archive_segment_count").GetInt32());
            Assert.True(statusRoot.GetProperty("activity_journal_archive_byte_count").GetInt64() > 0);
            Assert.True(statusRoot.GetProperty("activity_store_maintenance_summary_exists").GetBoolean());
            Assert.Equal("manual_archive", statusRoot.GetProperty("activity_store_last_maintenance_operation").GetString());
            Assert.False(statusRoot.GetProperty("activity_store_last_maintenance_dry_run").GetBoolean());
            Assert.True(statusRoot.GetProperty("activity_store_last_maintenance_applied").GetBoolean());
            Assert.Equal("segments_archived", statusRoot.GetProperty("activity_store_last_maintenance_reason").GetString());
            Assert.Equal(1, statusRoot.GetProperty("activity_store_last_maintenance_before_days").GetInt32());
            Assert.Equal(1, statusRoot.GetProperty("activity_store_last_maintenance_candidate_segment_count").GetInt32());
            Assert.Equal(1, statusRoot.GetProperty("activity_store_last_maintenance_archived_segment_count").GetInt32());
            Assert.True(statusRoot.GetProperty("activity_store_last_maintenance_archived_byte_count").GetInt64() > 0);
            Assert.Equal(1, statusRoot.GetProperty("activity_store_last_maintenance_archived_record_count").GetInt64());
            Assert.Equal("gateway activity verify", statusRoot.GetProperty("activity_store_last_maintenance_recommended_action").GetString());
            Assert.Equal("maintenance_fresh", statusRoot.GetProperty("activity_store_maintenance_freshness_posture").GetString());
            Assert.True(statusRoot.GetProperty("activity_store_maintenance_freshness_age_minutes").GetInt64() >= 0);
            Assert.Equal("no maintenance action needed", statusRoot.GetProperty("activity_store_maintenance_freshness_recommended_action").GetString());
            Assert.Equal("verification_freshness", statusRoot.GetProperty("activity_store_next_action_source").GetString());
            Assert.Equal("verification_missing", statusRoot.GetProperty("activity_store_next_action_reason").GetString());

            var verify = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "verify", "--json");
            using var verifyDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(verify.StandardOutput) ? verify.StandardError : verify.StandardOutput);
            Assert.Equal(0, verify.ExitCode);
            Assert.True(verifyDocument.RootElement.GetProperty("verification_passed").GetBoolean());

            var afterVerifyStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "status", "--json");
            using var afterVerifyStatusDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(afterVerifyStatus.StandardOutput) ? afterVerifyStatus.StandardError : afterVerifyStatus.StandardOutput);
            var afterVerifyStatusRoot = afterVerifyStatusDocument.RootElement;

            Assert.Equal(0, afterVerifyStatus.ExitCode);
            Assert.Equal("verification_fresh", afterVerifyStatusRoot.GetProperty("activity_store_verification_freshness_posture").GetString());
            Assert.True(afterVerifyStatusRoot.GetProperty("activity_store_verification_current_proof").GetBoolean());
            Assert.Equal("verification_summary_matches_current_manifest", afterVerifyStatusRoot.GetProperty("activity_store_verification_current_proof_reason").GetString());
            Assert.Equal("maintenance_fresh", afterVerifyStatusRoot.GetProperty("activity_store_maintenance_freshness_posture").GetString());
            Assert.Equal("no activity store action needed", afterVerifyStatusRoot.GetProperty("activity_store_next_action").GetString());
            Assert.Equal("none", afterVerifyStatusRoot.GetProperty("activity_store_next_action_source").GetString());
            Assert.Equal(0, afterVerifyStatusRoot.GetProperty("activity_store_next_action_priority").GetInt32());
            Assert.Equal("store_ready_verification_fresh_maintenance_fresh", afterVerifyStatusRoot.GetProperty("activity_store_next_action_reason").GetString());
            Assert.True(afterVerifyStatusRoot.GetProperty("activity_store_maturity_ready").GetBoolean());
            Assert.Equal("bounded_self_maintenance_current", afterVerifyStatusRoot.GetProperty("activity_store_maturity_posture").GetString());
            Assert.Contains("operator-diagnosis boundary", afterVerifyStatusRoot.GetProperty("activity_store_maturity_summary").GetString(), StringComparison.Ordinal);
            Assert.Equal("no activity store action needed", afterVerifyStatusRoot.GetProperty("next_action").GetString());

            var strictReadyStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "status", "--json", "--require-maturity");
            using var strictReadyStatusDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(strictReadyStatus.StandardOutput) ? strictReadyStatus.StandardError : strictReadyStatus.StandardOutput);
            var strictReadyStatusRoot = strictReadyStatusDocument.RootElement;

            Assert.Equal(0, strictReadyStatus.ExitCode);
            Assert.True(strictReadyStatusRoot.GetProperty("status_ok").GetBoolean());
            Assert.True(strictReadyStatusRoot.GetProperty("activity_store_operational_ok").GetBoolean());
            Assert.True(strictReadyStatusRoot.GetProperty("activity_store_maturity_required").GetBoolean());
            Assert.True(strictReadyStatusRoot.GetProperty("activity_store_exit_ok").GetBoolean());
            Assert.Equal("operational_and_maturity_ready", strictReadyStatusRoot.GetProperty("activity_store_exit_policy").GetString());
            Assert.Equal("maturity_ready", strictReadyStatusRoot.GetProperty("activity_store_exit_reason").GetString());
        }
        finally
        {
            StopHost(sandbox.RootPath);
            if (!string.IsNullOrWhiteSpace(oldSegmentPath) && File.Exists(oldSegmentPath))
            {
                File.Delete(oldSegmentPath);
            }
        }
    }

    [Fact]
    public async Task GatewayActivity_AutomaticallyArchivesExpiredSegmentsAfterAppend()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var oldSegmentPath = string.Empty;
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var descriptorRoot = descriptorDocument.RootElement;
            var baseUrl = descriptorRoot.GetProperty("base_url").GetString();
            var runtimeDirectory = descriptorRoot.GetProperty("runtime_directory").GetString();
            var segmentDirectory = Path.Combine(runtimeDirectory!, "gateway-activity-store", "segments");
            var archiveDirectory = Path.Combine(runtimeDirectory!, "gateway-activity-store", "archive");
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));
            Assert.False(string.IsNullOrWhiteSpace(runtimeDirectory));

            oldSegmentPath = Path.Combine(segmentDirectory, "activity-20000101-0001.jsonl");
            Directory.CreateDirectory(segmentDirectory);
            var oldRecord = new
            {
                SchemaVersion = "carves-gateway-activity-journal.v1",
                Timestamp = DateTimeOffset.Parse("2000-01-01T00:00:00+00:00"),
                Event = "gateway-route-not-found",
                Fields = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["method"] = "GET",
                    ["path"] = "/h3-auto-retention-old-route",
                    ["status"] = "404",
                },
            };
            File.WriteAllText(
                oldSegmentPath,
                JsonSerializer.Serialize(oldRecord, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) + Environment.NewLine,
                Encoding.UTF8);

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/h3-auto-retention-trigger");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            var activity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "80");
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            var activityRoot = activityDocument.RootElement;
            var activeSegments = activityRoot.GetProperty("activity_journal_segments")
                .EnumerateArray()
                .Select(segment => segment.GetProperty("file_name").GetString())
                .ToArray();
            var archivedOldSegments = Directory.Exists(archiveDirectory)
                ? Directory.GetFiles(archiveDirectory, "activity-20000101-0001.jsonl", SearchOption.AllDirectories)
                : Array.Empty<string>();

            Assert.Equal(0, activity.ExitCode);
            Assert.Equal("automatic_on_append_and_manual_archive", activityRoot.GetProperty("activity_store_retention_execution_mode").GetString());
            Assert.False(File.Exists(oldSegmentPath));
            Assert.Single(archivedOldSegments);
            Assert.True(File.Exists(archivedOldSegments[0]));
            Assert.True(activityRoot.GetProperty("activity_journal_archive_segment_count").GetInt32() >= 1);
            Assert.DoesNotContain("activity-20000101-0001.jsonl", activeSegments);
        }
        finally
        {
            StopHost(sandbox.RootPath);
            if (!string.IsNullOrWhiteSpace(oldSegmentPath) && File.Exists(oldSegmentPath))
            {
                File.Delete(oldSegmentPath);
            }
        }
    }

    [Fact]
    public async Task GatewayActivityVerify_ValidatesSegmentManifest()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/a5-verify-route");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            var verify = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "verify", "--json");
            using var verifyDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(verify.StandardOutput) ? verify.StandardError : verify.StandardOutput);
            var verifyRoot = verifyDocument.RootElement;

            Assert.Equal(0, verify.ExitCode);
            Assert.Equal("carves-gateway-activity-verify.v8", verifyRoot.GetProperty("schema_version").GetString());
            Assert.True(verifyRoot.GetProperty("verification_passed").GetBoolean());
            Assert.Equal("verified", verifyRoot.GetProperty("verification_posture").GetString());
            Assert.False(verifyRoot.GetProperty("verification_possibly_transient").GetBoolean());
            Assert.Equal("store_lock_consistent_snapshot", verifyRoot.GetProperty("activity_store_verify_consistency_mode").GetString());
            Assert.True(verifyRoot.GetProperty("activity_store_verify_snapshot_lock_acquired").GetBoolean());
            Assert.Equal("acquired", verifyRoot.GetProperty("activity_store_verify_snapshot_lock_status").GetString());
            Assert.Equal(
                verifyRoot.GetProperty("activity_store_writer_lock_path").GetString(),
                verifyRoot.GetProperty("activity_store_verify_snapshot_lock_path").GetString());
            Assert.Equal(5000, verifyRoot.GetProperty("activity_store_verify_snapshot_lock_acquire_timeout_ms").GetInt32());
            Assert.Equal(0, verifyRoot.GetProperty("issue_count").GetInt32());
            Assert.Equal("carves-gateway-activity-segment-manifest.v1", verifyRoot.GetProperty("activity_store_manifest_schema_version").GetString());
            Assert.Equal("carves-gateway-activity-manifest-checkpoint.v1", verifyRoot.GetProperty("activity_store_checkpoint_schema_version").GetString());
            Assert.Equal("non_destructive_archive_no_delete", verifyRoot.GetProperty("activity_store_retention_mode").GetString());
            Assert.Equal("bounded_file_lock", verifyRoot.GetProperty("activity_store_writer_lock_mode").GetString());
            Assert.True(File.Exists(verifyRoot.GetProperty("activity_store_writer_lock_path").GetString()));
            Assert.True(verifyRoot.GetProperty("activity_store_writer_lock_exists").GetBoolean());
            Assert.True(verifyRoot.GetProperty("activity_store_writer_lock_file_exists").GetBoolean());
            Assert.False(verifyRoot.GetProperty("activity_store_writer_lock_currently_held").GetBoolean());
            Assert.Equal("available", verifyRoot.GetProperty("activity_store_writer_lock_status").GetString());
            Assert.Equal("segment_manifest_sha256_checkpoint_chain", verifyRoot.GetProperty("activity_store_integrity_mode").GetString());
            Assert.True(verifyRoot.GetProperty("activity_store_verification_summary_exists").GetBoolean());
            Assert.True(verifyRoot.GetProperty("activity_store_last_verification_passed").GetBoolean());
            Assert.Equal("verified", verifyRoot.GetProperty("activity_store_last_verification_posture").GetString());
            Assert.False(verifyRoot.GetProperty("activity_store_last_verification_possibly_transient").GetBoolean());
            Assert.Equal("store_lock_consistent_snapshot", verifyRoot.GetProperty("activity_store_last_verification_consistency_mode").GetString());
            Assert.True(verifyRoot.GetProperty("activity_store_last_verification_snapshot_lock_acquired").GetBoolean());
            Assert.Equal(0, verifyRoot.GetProperty("activity_store_last_verification_issue_count").GetInt32());
            Assert.Equal("gateway activity verified", verifyRoot.GetProperty("activity_store_last_verification_recommended_action").GetString());
            Assert.Equal("gateway activity verified", verifyRoot.GetProperty("next_action").GetString());
            Assert.Equal("verification_result", verifyRoot.GetProperty("next_action_source").GetString());
            Assert.Equal(0, verifyRoot.GetProperty("next_action_priority").GetInt32());
            Assert.Equal("verified", verifyRoot.GetProperty("next_action_reason").GetString());
            Assert.True(File.Exists(verifyRoot.GetProperty("manifest_path").GetString()));
            Assert.True(Directory.Exists(verifyRoot.GetProperty("segment_directory").GetString()));
            Assert.True(File.Exists(verifyRoot.GetProperty("checkpoint_chain_path").GetString()));
            Assert.True(verifyRoot.GetProperty("checkpoint_chain_exists").GetBoolean());
            Assert.True(verifyRoot.GetProperty("checkpoint_chain_count").GetInt32() > 0);
            Assert.True(verifyRoot.GetProperty("checkpoint_chain_latest_sequence").GetInt64() > 0);
            Assert.Matches("^[0-9a-f]{64}$", verifyRoot.GetProperty("checkpoint_chain_latest_checkpoint_sha256").GetString() ?? string.Empty);
            Assert.Matches("^[0-9a-f]{64}$", verifyRoot.GetProperty("checkpoint_chain_latest_manifest_sha256").GetString() ?? string.Empty);
            Assert.True(verifyRoot.GetProperty("stored_manifest").GetProperty("record_count").GetInt64() > 0);
            Assert.Equal(
                verifyRoot.GetProperty("stored_manifest").GetProperty("record_count").GetInt64(),
                verifyRoot.GetProperty("actual_manifest").GetProperty("record_count").GetInt64());

            var textVerify = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "verify");
            Assert.Equal(0, textVerify.ExitCode);
            Assert.Contains("Verification passed: True", textVerify.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store verification summary exists: True", textVerify.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Activity store verify snapshot lock acquired: True", textVerify.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Checkpoint chain exists: True", textVerify.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Issues: none", textVerify.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next action source: verification_result", textVerify.StandardOutput, StringComparison.Ordinal);

            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "status", "--json");
            using var statusDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(status.StandardOutput) ? status.StandardError : status.StandardOutput);
            var statusRoot = statusDocument.RootElement;

            Assert.Equal(0, status.ExitCode);
            Assert.Equal("carves-gateway-activity-status.v9", statusRoot.GetProperty("schema_version").GetString());
            Assert.True(statusRoot.GetProperty("activity_store_verification_summary_exists").GetBoolean());
            Assert.True(statusRoot.GetProperty("activity_store_last_verification_passed").GetBoolean());
            Assert.Equal("verified", statusRoot.GetProperty("activity_store_last_verification_posture").GetString());
            Assert.Equal(0, statusRoot.GetProperty("activity_store_last_verification_issue_count").GetInt32());
            Assert.Equal("gateway activity verified", statusRoot.GetProperty("activity_store_last_verification_recommended_action").GetString());
            Assert.False(string.IsNullOrWhiteSpace(statusRoot.GetProperty("activity_store_last_verification_manifest_generated_at_utc").GetString()));
            Assert.Matches("^[0-9a-f]{64}$", statusRoot.GetProperty("activity_store_last_verification_manifest_sha256").GetString() ?? string.Empty);
            Assert.True(statusRoot.GetProperty("activity_store_last_verification_checkpoint_latest_sequence").GetInt64() > 0);
            Assert.Matches("^[0-9a-f]{64}$", statusRoot.GetProperty("activity_store_last_verification_checkpoint_latest_checkpoint_sha256").GetString() ?? string.Empty);
            Assert.Matches("^[0-9a-f]{64}$", statusRoot.GetProperty("activity_store_last_verification_checkpoint_latest_manifest_sha256").GetString() ?? string.Empty);
            Assert.Equal("verification_fresh", statusRoot.GetProperty("activity_store_verification_freshness_posture").GetString());
            Assert.True(statusRoot.GetProperty("activity_store_verification_freshness_age_minutes").GetInt64() >= 0);
            Assert.Equal("no verification action needed", statusRoot.GetProperty("activity_store_verification_freshness_recommended_action").GetString());
            Assert.True(statusRoot.GetProperty("activity_store_verification_current_proof").GetBoolean());
            Assert.Equal("verification_summary_matches_current_manifest", statusRoot.GetProperty("activity_store_verification_current_proof_reason").GetString());
            Assert.Equal("maintenance_missing", statusRoot.GetProperty("activity_store_maintenance_freshness_posture").GetString());
            Assert.Equal("gateway activity maintenance", statusRoot.GetProperty("activity_store_maintenance_freshness_recommended_action").GetString());
            Assert.Equal("maintenance_freshness", statusRoot.GetProperty("activity_store_next_action_source").GetString());
            Assert.Equal(3, statusRoot.GetProperty("activity_store_next_action_priority").GetInt32());
            Assert.Equal("maintenance_missing", statusRoot.GetProperty("activity_store_next_action_reason").GetString());
            Assert.Equal("gateway activity maintenance", statusRoot.GetProperty("next_action").GetString());

            using var changedRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/h1-current-proof-after-verify");
            Assert.Equal(HttpStatusCode.NotFound, changedRoute.StatusCode);

            var changedStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "status", "--json");
            using var changedStatusDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(changedStatus.StandardOutput) ? changedStatus.StandardError : changedStatus.StandardOutput);
            var changedStatusRoot = changedStatusDocument.RootElement;

            Assert.Equal(0, changedStatus.ExitCode);
            Assert.Equal("verification_stale_store_changed", changedStatusRoot.GetProperty("activity_store_verification_freshness_posture").GetString());
            Assert.Equal("gateway activity verify", changedStatusRoot.GetProperty("activity_store_verification_freshness_recommended_action").GetString());
            Assert.False(changedStatusRoot.GetProperty("activity_store_verification_current_proof").GetBoolean());
            Assert.Equal("checkpoint_sequence_changed", changedStatusRoot.GetProperty("activity_store_verification_current_proof_reason").GetString());
            Assert.Equal("gateway activity verify", changedStatusRoot.GetProperty("activity_store_next_action").GetString());
            Assert.Equal("verification_freshness", changedStatusRoot.GetProperty("activity_store_next_action_source").GetString());
            Assert.Equal(2, changedStatusRoot.GetProperty("activity_store_next_action_priority").GetInt32());
            Assert.Equal("verification_stale_store_changed", changedStatusRoot.GetProperty("activity_store_next_action_reason").GetString());
            Assert.False(changedStatusRoot.GetProperty("activity_store_maturity_ready").GetBoolean());
            Assert.Equal("verification_stale_store_changed", changedStatusRoot.GetProperty("activity_store_maturity_posture").GetString());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayActivityVerify_ReturnsTransientWhenSnapshotLockIsUnavailable()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        FileStream? lockBlocker = null;
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var descriptorRoot = descriptorDocument.RootElement;
            var baseUrl = descriptorRoot.GetProperty("base_url").GetString();
            var runtimeDirectory = descriptorRoot.GetProperty("runtime_directory").GetString();
            var lockPath = Path.Combine(runtimeDirectory!, "gateway-activity-store", "activity.lock");
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));
            Assert.False(string.IsNullOrWhiteSpace(runtimeDirectory));

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/h2-verify-lock-route");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            lockBlocker = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var verify = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "verify", "--json");
            using var verifyDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(verify.StandardOutput) ? verify.StandardError : verify.StandardOutput);
            var verifyRoot = verifyDocument.RootElement;
            var issueCodes = verifyRoot.GetProperty("issues")
                .EnumerateArray()
                .Select(item => item.GetProperty("code").GetString())
                .ToArray();

            Assert.Equal(1, verify.ExitCode);
            Assert.Equal("carves-gateway-activity-verify.v8", verifyRoot.GetProperty("schema_version").GetString());
            Assert.False(verifyRoot.GetProperty("verification_passed").GetBoolean());
            Assert.Equal("possibly_transient", verifyRoot.GetProperty("verification_posture").GetString());
            Assert.True(verifyRoot.GetProperty("verification_possibly_transient").GetBoolean());
            Assert.Equal("store_lock_consistent_snapshot", verifyRoot.GetProperty("activity_store_verify_consistency_mode").GetString());
            Assert.False(verifyRoot.GetProperty("activity_store_verify_snapshot_lock_acquired").GetBoolean());
            Assert.Equal("lock_unavailable", verifyRoot.GetProperty("activity_store_verify_snapshot_lock_status").GetString());
            Assert.False(verifyRoot.GetProperty("activity_store_verification_summary_exists").GetBoolean());
            Assert.Equal(lockPath, verifyRoot.GetProperty("activity_store_verify_snapshot_lock_path").GetString());
            Assert.Equal(5000, verifyRoot.GetProperty("activity_store_verify_snapshot_lock_acquire_timeout_ms").GetInt32());
            Assert.Contains(issueCodes, code => string.Equals(code, "verification_lock_unavailable", StringComparison.Ordinal));
            Assert.Equal("retry gateway activity verify", verifyRoot.GetProperty("next_action").GetString());
            Assert.Equal("verification_snapshot_lock", verifyRoot.GetProperty("next_action_source").GetString());
            Assert.Equal(1, verifyRoot.GetProperty("next_action_priority").GetInt32());
            Assert.Equal("lock_unavailable", verifyRoot.GetProperty("next_action_reason").GetString());
        }
        finally
        {
            if (lockBlocker is not null)
            {
                await lockBlocker.DisposeAsync();
            }

            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayActivityVerify_FailsWhenSegmentDriftsFromManifest()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var segmentPath = string.Empty;
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/a5-tamper-route");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            var activity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "80");
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            segmentPath = activityDocument.RootElement.GetProperty("activity_journal_active_segment_path").GetString() ?? string.Empty;
            Assert.True(File.Exists(segmentPath));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }

        File.AppendAllText(segmentPath, $"tampered-{Guid.NewGuid():N}{Environment.NewLine}");

        var verify = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "verify", "--json");
        using var verifyDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(verify.StandardOutput) ? verify.StandardError : verify.StandardOutput);
        var verifyRoot = verifyDocument.RootElement;
        var issueCodes = verifyRoot.GetProperty("issues")
            .EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Equal(1, verify.ExitCode);
        Assert.False(verifyRoot.GetProperty("verification_passed").GetBoolean());
        Assert.Equal("failed", verifyRoot.GetProperty("verification_posture").GetString());
        Assert.True(verifyRoot.GetProperty("issue_count").GetInt32() > 0);
        Assert.Contains(issueCodes, code => string.Equals(code, "segment_sha256_mismatch", StringComparison.Ordinal));
        Assert.Contains(issueCodes, code => string.Equals(code, "segment_record_count_mismatch", StringComparison.Ordinal));
        Assert.Equal("inspect gateway activity verify issues", verifyRoot.GetProperty("next_action").GetString());
        Assert.Equal("verification_result", verifyRoot.GetProperty("next_action_source").GetString());
        Assert.Equal(1, verifyRoot.GetProperty("next_action_priority").GetInt32());
        Assert.Equal("failed", verifyRoot.GetProperty("next_action_reason").GetString());
    }

    [Fact]
    public async Task GatewayActivityVerify_FailsWhenManifestDriftsFromCheckpoint()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var manifestPath = string.Empty;
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/a7-checkpoint-route");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            var activity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "80");
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            var activityRoot = activityDocument.RootElement;
            manifestPath = activityRoot.GetProperty("activity_journal_manifest_path").GetString() ?? string.Empty;
            Assert.True(File.Exists(manifestPath));
            Assert.True(File.Exists(activityRoot.GetProperty("activity_journal_checkpoint_chain_path").GetString()));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }

        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest["generated_at_utc"] = "1999-01-01T00:00:00.0000000+00:00";
        File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }), Encoding.UTF8);

        var verify = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "verify", "--json");
        using var verifyDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(verify.StandardOutput) ? verify.StandardError : verify.StandardOutput);
        var verifyRoot = verifyDocument.RootElement;
        var issueCodes = verifyRoot.GetProperty("issues")
            .EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        Assert.Equal(1, verify.ExitCode);
        Assert.False(verifyRoot.GetProperty("verification_passed").GetBoolean());
        Assert.Equal("failed", verifyRoot.GetProperty("verification_posture").GetString());
        Assert.Contains(issueCodes, code => string.Equals(code, "manifest_checkpoint_manifest_sha256_mismatch", StringComparison.Ordinal));
        Assert.Equal("inspect gateway activity verify issues", verifyRoot.GetProperty("next_action").GetString());
        Assert.Equal("verification_result", verifyRoot.GetProperty("next_action_source").GetString());
        Assert.Equal(1, verifyRoot.GetProperty("next_action_priority").GetInt32());
        Assert.Equal("failed", verifyRoot.GetProperty("next_action_reason").GetString());
    }

    [Fact]
    public async Task GatewayActivity_FiltersByCategoryAndEvent()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            var dashboard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "dashboard", "--text");
            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/h7-filter-route");

            Assert.Equal(0, dashboard.ExitCode);
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            var routeActivity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "80",
                "--category",
                "route_not_found");
            using var routeActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(routeActivity.StandardOutput) ? routeActivity.StandardError : routeActivity.StandardOutput);
            var routeRoot = routeActivityDocument.RootElement;
            var routeEntries = routeRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, routeActivity.ExitCode);
            Assert.Equal("carves-gateway-activity.v17", routeRoot.GetProperty("schema_version").GetString());
            Assert.True(routeRoot.GetProperty("activity_filter_applied").GetBoolean());
            Assert.Equal("route_not_found", routeRoot.GetProperty("activity_filter_category").GetString());
            Assert.True(routeRoot.GetProperty("source_entry_count").GetInt32() > routeRoot.GetProperty("entry_count").GetInt32());
            Assert.True(routeRoot.GetProperty("route_not_found_entry_count").GetInt32() >= 1);
            Assert.All(routeEntries, entry => Assert.Equal("gateway-route-not-found", entry.GetProperty("event").GetString()));
            Assert.Contains(routeEntries, entry =>
                entry.GetProperty("fields").GetProperty("path").GetString() == "/h7-filter-route");

            var gatewayRequestActivity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "80",
                "--category",
                "gateway_request");
            using var gatewayRequestActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(gatewayRequestActivity.StandardOutput) ? gatewayRequestActivity.StandardError : gatewayRequestActivity.StandardOutput);
            var gatewayRequestRoot = gatewayRequestActivityDocument.RootElement;
            var gatewayRequestEntries = gatewayRequestRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, gatewayRequestActivity.ExitCode);
            Assert.True(gatewayRequestRoot.GetProperty("activity_filter_applied").GetBoolean());
            Assert.Equal("gateway_request", gatewayRequestRoot.GetProperty("activity_filter_category").GetString());
            Assert.True(gatewayRequestRoot.GetProperty("gateway_request_entry_count").GetInt32() >= 1);
            Assert.All(gatewayRequestEntries, entry => Assert.Equal("gateway-request", entry.GetProperty("event").GetString()));
            Assert.Contains(gatewayRequestEntries, entry =>
                entry.GetProperty("fields").GetProperty("path").GetString() == "/h7-filter-route");

            var pathActivity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "80",
                "--path",
                "/h7-filter-route");
            using var pathActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(pathActivity.StandardOutput) ? pathActivity.StandardError : pathActivity.StandardOutput);
            var pathRoot = pathActivityDocument.RootElement;
            var pathEntries = pathRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, pathActivity.ExitCode);
            Assert.True(pathRoot.GetProperty("activity_filter_applied").GetBoolean());
            Assert.Equal("/h7-filter-route", pathRoot.GetProperty("activity_filter_path").GetString());
            Assert.All(pathEntries, entry => Assert.Equal("/h7-filter-route", entry.GetProperty("fields").GetProperty("path").GetString()));
            Assert.Contains(pathEntries, entry => entry.GetProperty("event").GetString() == "gateway-request");
            Assert.Contains(pathEntries, entry => entry.GetProperty("event").GetString() == "gateway-route-not-found");
            var pathGatewayRequest = pathEntries.Single(entry => entry.GetProperty("event").GetString() == "gateway-request");
            var pathRequestId = pathGatewayRequest.GetProperty("fields").GetProperty("request_id").GetString();
            Assert.NotNull(pathRequestId);
            Assert.StartsWith("gwreq-", pathRequestId);

            var requestIdActivity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "80",
                "--request-id",
                pathRequestId);
            using var requestIdActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(requestIdActivity.StandardOutput) ? requestIdActivity.StandardError : requestIdActivity.StandardOutput);
            var requestIdRoot = requestIdActivityDocument.RootElement;
            var requestIdEntries = requestIdRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, requestIdActivity.ExitCode);
            Assert.True(requestIdRoot.GetProperty("activity_filter_applied").GetBoolean());
            Assert.Equal(pathRequestId, requestIdRoot.GetProperty("activity_filter_request_id").GetString());
            Assert.All(requestIdEntries, entry => Assert.Equal(pathRequestId, entry.GetProperty("fields").GetProperty("request_id").GetString()));
            Assert.Contains(requestIdEntries, entry => entry.GetProperty("event").GetString() == "gateway-request");
            Assert.Contains(requestIdEntries, entry => entry.GetProperty("event").GetString() == "gateway-route-not-found");

            var explain = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "explain",
                pathRequestId!,
                "--json");
            using var explainDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(explain.StandardOutput) ? explain.StandardError : explain.StandardOutput);
            var explainRoot = explainDocument.RootElement;
            var explainEntries = explainRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, explain.ExitCode);
            Assert.Equal("carves-gateway-activity-explain.v6", explainRoot.GetProperty("schema_version").GetString());
            Assert.True(explainRoot.GetProperty("found").GetBoolean());
            Assert.Equal("found", explainRoot.GetProperty("explanation_posture").GetString());
            Assert.Equal("active_and_archived_segments_with_legacy_fallback", explainRoot.GetProperty("activity_search_scope").GetString());
            Assert.Equal(pathRequestId, explainRoot.GetProperty("request_id").GetString());
            Assert.Equal("GET", explainRoot.GetProperty("method").GetString());
            Assert.Equal("/h7-filter-route", explainRoot.GetProperty("path").GetString());
            Assert.Equal("404", explainRoot.GetProperty("status").GetString());
            Assert.Equal("inspect listed activity entries", explainRoot.GetProperty("next_action").GetString());
            Assert.Equal("request_chain_lookup", explainRoot.GetProperty("next_action_source").GetString());
            Assert.Equal(0, explainRoot.GetProperty("next_action_priority").GetInt32());
            Assert.Equal("request_chain_found", explainRoot.GetProperty("next_action_reason").GetString());
            Assert.True(explainRoot.GetProperty("entry_count").GetInt32() >= 2);
            Assert.Contains(explainEntries, entry => entry.GetProperty("event").GetString() == "gateway-request");
            Assert.Contains(explainEntries, entry => entry.GetProperty("event").GetString() == "gateway-route-not-found");

            var explainText = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "explain",
                pathRequestId!);

            Assert.Equal(0, explainText.ExitCode);
            Assert.Contains("CARVES gateway activity explain", explainText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Request id: {pathRequestId}", explainText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("gateway-route-not-found", explainText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next action source: request_chain_lookup", explainText.StandardOutput, StringComparison.Ordinal);

            var missingExplain = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "explain",
                "gwreq-a9-missing",
                "--json");
            using var missingExplainDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(missingExplain.StandardOutput) ? missingExplain.StandardError : missingExplain.StandardOutput);
            var missingExplainRoot = missingExplainDocument.RootElement;

            Assert.Equal(1, missingExplain.ExitCode);
            Assert.False(missingExplainRoot.GetProperty("found").GetBoolean());
            Assert.Equal("not_found", missingExplainRoot.GetProperty("explanation_posture").GetString());
            Assert.Equal("gwreq-a9-missing", missingExplainRoot.GetProperty("request_id").GetString());
            Assert.Equal(0, missingExplainRoot.GetProperty("entry_count").GetInt32());
            Assert.Equal("run `gateway activity --request-id gwreq-a9-missing` or verify the request id", missingExplainRoot.GetProperty("next_action").GetString());
            Assert.Equal("request_chain_lookup", missingExplainRoot.GetProperty("next_action_source").GetString());
            Assert.Equal(2, missingExplainRoot.GetProperty("next_action_priority").GetInt32());
            Assert.Equal("request_chain_not_found", missingExplainRoot.GetProperty("next_action_reason").GetString());

            var missingExplainText = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "explain",
                "gwreq-a9-missing");

            Assert.Equal(1, missingExplainText.ExitCode);
            Assert.Contains("Next action: run `gateway activity --request-id gwreq-a9-missing` or verify the request id", missingExplainText.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("<id>", missingExplainText.CombinedOutput, StringComparison.Ordinal);

            var gatewayRequestText = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--tail",
                "80",
                "--category",
                "gateway_request");

            Assert.Equal(0, gatewayRequestText.ExitCode);
            Assert.Contains("gateway-request", gatewayRequestText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("method=GET", gatewayRequestText.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("path=/h7-filter-route", gatewayRequestText.StandardOutput, StringComparison.Ordinal);

            var commandActivity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "80",
                "--command",
                "dashboard");
            using var commandActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(commandActivity.StandardOutput) ? commandActivity.StandardError : commandActivity.StandardOutput);
            var commandRoot = commandActivityDocument.RootElement;
            var commandEntries = commandRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, commandActivity.ExitCode);
            Assert.True(commandRoot.GetProperty("activity_filter_applied").GetBoolean());
            Assert.Equal("dashboard", commandRoot.GetProperty("activity_filter_command").GetString());
            Assert.All(commandEntries, entry => Assert.Equal("dashboard", entry.GetProperty("fields").GetProperty("command").GetString()));

            var recentActivity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "80",
                "--since-minutes",
                "60");
            using var recentActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(recentActivity.StandardOutput) ? recentActivity.StandardError : recentActivity.StandardOutput);
            var recentRoot = recentActivityDocument.RootElement;
            var recentEntries = recentRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, recentActivity.ExitCode);
            Assert.True(recentRoot.GetProperty("activity_filter_applied").GetBoolean());
            Assert.Equal(60, recentRoot.GetProperty("activity_filter_since_minutes").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(recentRoot.GetProperty("activity_filter_since_utc").GetString()));
            Assert.Equal("store_backed_since", recentRoot.GetProperty("activity_query_mode").GetString());
            Assert.True(recentRoot.GetProperty("activity_query_store_backed_since").GetBoolean());
            Assert.False(recentRoot.GetProperty("activity_query_tail_bounded").GetBoolean());
            Assert.True(recentEntries.Length > 0);

            var responseActivity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "80",
                "--event",
                "gateway-invoke-response");
            using var responseActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(responseActivity.StandardOutput) ? responseActivity.StandardError : responseActivity.StandardOutput);
            var responseRoot = responseActivityDocument.RootElement;
            var responseEntries = responseRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, responseActivity.ExitCode);
            Assert.True(responseRoot.GetProperty("activity_filter_applied").GetBoolean());
            Assert.Equal("gateway-invoke-response", responseRoot.GetProperty("activity_filter_event").GetString());
            Assert.All(responseEntries, entry => Assert.Equal("gateway-invoke-response", entry.GetProperty("event").GetString()));
            Assert.Contains(responseEntries, entry =>
                entry.GetProperty("fields").GetProperty("command").GetString() == "dashboard"
                && entry.GetProperty("fields").GetProperty("exit_code").GetString() == "0");
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task GatewayActivitySinceMinutes_ReadsStoreBeyondTailWindow()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var activeSegmentPath = string.Empty;
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var baseUrl = descriptorDocument.RootElement.GetProperty("base_url").GetString();
            using var client = new HttpClient();

            Assert.Equal(0, start.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(baseUrl));

            using var unknownRoute = await client.GetAsync($"{baseUrl!.TrimEnd('/')}/a8-since-seed-route");
            Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);

            var activity = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "activity", "--json", "--tail", "20");
            using var activityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(activity.StandardOutput) ? activity.StandardError : activity.StandardOutput);
            activeSegmentPath = activityDocument.RootElement.GetProperty("activity_journal_active_segment_path").GetString() ?? string.Empty;
            Assert.True(File.Exists(activeSegmentPath));

            StopHost(sandbox.RootPath);

            var targetRecord = new
            {
                SchemaVersion = "carves-gateway-activity-journal.v1",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-30),
                Event = "gateway-route-not-found",
                Fields = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["method"] = "GET",
                    ["path"] = "/a8-since-store-backed-hidden-by-tail",
                    ["status"] = "404",
                },
            };
            File.AppendAllText(
                activeSegmentPath,
                JsonSerializer.Serialize(targetRecord, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) + Environment.NewLine,
                Encoding.UTF8);

            for (var index = 0; index < 3; index++)
            {
                var fillerRecord = new
                {
                    SchemaVersion = "carves-gateway-activity-journal.v1",
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
                    Event = "gateway-route-not-found",
                    Fields = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["method"] = "GET",
                        ["path"] = $"/a8-since-tail-filler-{index}",
                        ["status"] = "404",
                    },
                };
                File.AppendAllText(
                    activeSegmentPath,
                    JsonSerializer.Serialize(fillerRecord, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }) + Environment.NewLine,
                    Encoding.UTF8);
            }

            var recentActivity = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "gateway",
                "activity",
                "--json",
                "--tail",
                "1",
                "--since-minutes",
                "60",
                "--path",
                "/a8-since-store-backed-hidden-by-tail");
            using var recentActivityDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(recentActivity.StandardOutput) ? recentActivity.StandardError : recentActivity.StandardOutput);
            var recentRoot = recentActivityDocument.RootElement;
            var recentEntries = recentRoot.GetProperty("entries").EnumerateArray().ToArray();

            Assert.Equal(0, recentActivity.ExitCode);
            Assert.Equal("carves-gateway-activity.v17", recentRoot.GetProperty("schema_version").GetString());
            Assert.True(recentRoot.GetProperty("activity_filter_applied").GetBoolean());
            Assert.Equal(1, recentRoot.GetProperty("tail_line_count").GetInt32());
            Assert.Equal("store_backed_since", recentRoot.GetProperty("activity_query_mode").GetString());
            Assert.True(recentRoot.GetProperty("activity_query_store_backed_since").GetBoolean());
            Assert.False(recentRoot.GetProperty("activity_query_tail_bounded").GetBoolean());
            Assert.Single(recentEntries);
            Assert.Equal(
                "/a8-since-store-backed-hidden-by-tail",
                recentEntries[0].GetProperty("fields").GetProperty("path").GetString());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void TaskRunHelp_IsReadOnlyAndDoesNotRequireResidentHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        StopHost(sandbox.RootPath);

        var help = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", "--help");
        var aliasHelp = ProgramHarness.Run("--repo-root", sandbox.RootPath, "run", "task", "--help");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");

        Assert.Equal(0, help.ExitCode);
        Assert.Equal(0, aliasHelp.ExitCode);
        Assert.Contains("Usage: carves task run", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Help requests are read-only", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Usage: carves task run", aliasHelp.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("\"task_id\": \"--help\"", help.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Delegated execution requires a resident host", help.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Started resident host process", help.CombinedOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(descriptorPath));
    }


    [Fact]
    public void HostStart_FailsClosedWhenLockedStaleDeploymentGenerationExists()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "stale-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        var lockedBinaryPath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Application.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        File.WriteAllText(lockedBinaryPath, "stale application");

        using var locker = LockedFileProcess.Start(lockedBinaryPath);
        WriteHostDescriptor(
            sandbox.RootPath,
            locker.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            "http://127.0.0.1:1",
            1);

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            using var descriptorDocument = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var deploymentDirectory = descriptorDocument.RootElement.GetProperty("deployment_directory").GetString();

            Assert.NotEqual(0, start.ExitCode);
            Assert.Contains("host_session_conflict", start.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Refusing to start another host generation automatically.", start.CombinedOutput, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(deploymentDirectory));
            Assert.True(
                string.Equals(
                    Path.GetFullPath(staleDeploymentDirectory),
                    Path.GetFullPath(deploymentDirectory!),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            Assert.True(IsProcessAlive(locker.ProcessId));
            Assert.True(Directory.Exists(staleDeploymentDirectory));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostEnsureJson_FailsClosedWhenAliveStaleDescriptorExists()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "stale-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            sandbox.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var ensure = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "ensure", "--json", "--interval-ms", "200");
        var ensureJson = string.IsNullOrWhiteSpace(ensure.StandardOutput)
            ? ensure.StandardError
            : ensure.StandardOutput;
        using var document = JsonDocument.Parse(ensureJson);
        var root = document.RootElement;
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        using var descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));

        Assert.Equal(1, ensure.ExitCode);
        Assert.Equal("carves-host-ensure.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("host_session_conflict", root.GetProperty("host_readiness").GetString());
        Assert.False(root.GetProperty("host_running").GetBoolean());
        Assert.False(root.GetProperty("started").GetBoolean());
        Assert.False(root.GetProperty("compatible").GetBoolean());
        Assert.True(root.GetProperty("conflict_present").GetBoolean());
        Assert.Equal(sleeper.ProcessId, root.GetProperty("conflicting_process_id").GetInt32());
        Assert.Equal($"http://127.0.0.1:{stalePort}", root.GetProperty("conflicting_base_url").GetString());
        Assert.Contains("Refusing to start another host generation automatically.", root.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("reconcile_stale_host", root.GetProperty("next_action_kind").GetString());
        Assert.Equal("blocked", root.GetProperty("lifecycle").GetProperty("state").GetString());
        Assert.Equal("host_session_conflict", root.GetProperty("lifecycle").GetProperty("reason").GetString());
        Assert.Equal("reconcile_stale_host", root.GetProperty("lifecycle").GetProperty("action_kind").GetString());
        Assert.Contains("host reconcile --replace-stale", root.GetProperty("next_action").GetString(), StringComparison.Ordinal);
        Assert.Equal(sleeper.ProcessId, descriptor.RootElement.GetProperty("process_id").GetInt32());
        Assert.True(IsProcessAlive(sleeper.ProcessId));
    }

    [Fact]
    public void GatewayRestartJson_FailsClosedWhenAliveStaleDescriptorExists()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "stale-gateway-restart-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            sandbox.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var restart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "gateway", "restart", "--json", "--interval-ms", "200");
        var restartJson = string.IsNullOrWhiteSpace(restart.StandardOutput)
            ? restart.StandardError
            : restart.StandardOutput;
        using var document = JsonDocument.Parse(restartJson);
        var root = document.RootElement;
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        using var descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));

        Assert.Equal(1, restart.ExitCode);
        Assert.Equal("carves-gateway-restart.v1", root.GetProperty("schema_version").GetString());
        Assert.False(root.GetProperty("gateway_ready").GetBoolean());
        Assert.False(root.GetProperty("restarted").GetBoolean());
        Assert.False(root.GetProperty("stopped_previous").GetBoolean());
        Assert.False(root.GetProperty("started").GetBoolean());
        Assert.True(root.GetProperty("conflict_present").GetBoolean());
        Assert.Equal(sleeper.ProcessId, root.GetProperty("conflicting_process_id").GetInt32());
        Assert.Equal($"http://127.0.0.1:{stalePort}", root.GetProperty("conflicting_base_url").GetString());
        Assert.Equal("reconcile_stale_host", root.GetProperty("next_action_kind").GetString());
        Assert.Contains("gateway reconcile --replace-stale", root.GetProperty("next_action").GetString(), StringComparison.Ordinal);
        Assert.Equal("blocked", root.GetProperty("lifecycle").GetProperty("state").GetString());
        Assert.Equal("host_session_conflict", root.GetProperty("lifecycle").GetProperty("reason").GetString());
        Assert.Equal(sleeper.ProcessId, descriptor.RootElement.GetProperty("process_id").GetInt32());
        Assert.True(IsProcessAlive(sleeper.ProcessId));
    }

    [Fact]
    public void HostEnsureJson_FailsClosedWhenStartupLockIsHeld()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var startupLockPath = ResolveHostStartupLockPath(sandbox.RootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(startupLockPath)!);
        using var startupLock = new FileStream(startupLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var ensure = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "ensure", "--json", "--interval-ms", "200");
        var ensureJson = string.IsNullOrWhiteSpace(ensure.StandardOutput)
            ? ensure.StandardError
            : ensure.StandardOutput;
        using var document = JsonDocument.Parse(ensureJson);
        var root = document.RootElement;
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");

        Assert.Equal(1, ensure.ExitCode);
        Assert.Equal("carves-host-ensure.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("host_start_in_progress", root.GetProperty("host_readiness").GetString());
        Assert.False(root.GetProperty("host_running").GetBoolean());
        Assert.False(root.GetProperty("started").GetBoolean());
        Assert.False(root.GetProperty("compatible").GetBoolean());
        Assert.True(root.GetProperty("startup_lock_present").GetBoolean());
        Assert.Equal(startupLockPath, root.GetProperty("startup_lock_path").GetString());
        Assert.Contains("Refusing to start another host generation concurrently.", root.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("wait_for_startup", root.GetProperty("next_action_kind").GetString());
        Assert.Equal("waiting", root.GetProperty("lifecycle").GetProperty("state").GetString());
        Assert.Equal("host_start_in_progress", root.GetProperty("lifecycle").GetProperty("reason").GetString());
        Assert.Contains("host status --json", root.GetProperty("next_action").GetString(), StringComparison.Ordinal);
        Assert.False(File.Exists(descriptorPath));
    }

    [Fact]
    public void HostEnsureJson_ReconcilesHealthyExistingGenerationWhenActiveDescriptorIsStale()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        var originalDescriptorJson = File.ReadAllText(descriptorPath);
        using var originalDescriptor = JsonDocument.Parse(originalDescriptorJson);
        var originalBaseUrl = originalDescriptor.RootElement.GetProperty("base_url").GetString()
            ?? throw new InvalidOperationException("Expected active host base_url.");
        var originalProcessId = originalDescriptor.RootElement.GetProperty("process_id").GetInt32();

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "stale-reconcile");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        try
        {
            WriteHostDescriptor(
                sandbox.RootPath,
                sleeper.ProcessId,
                runtimeDirectory,
                staleDeploymentDirectory,
                staleExecutablePath,
                $"http://127.0.0.1:{stalePort}",
                stalePort);

            var ensure = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "ensure", "--json", "--interval-ms", "200");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(ensure.StandardOutput) ? ensure.StandardError : ensure.StandardOutput);
            var root = document.RootElement;
            using var repairedDescriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, ensure.ExitCode);
            Assert.Equal("carves-host-ensure.v1", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("host_running").GetBoolean());
            Assert.True(root.GetProperty("compatible").GetBoolean());
            Assert.False(root.GetProperty("started").GetBoolean());
            Assert.Equal("connected", root.GetProperty("host_readiness").GetString());
            Assert.Equal(originalBaseUrl, root.GetProperty("base_url").GetString());
            Assert.Contains("Repaired active host descriptor", root.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal("none", root.GetProperty("next_action_kind").GetString());
            Assert.Equal("ready", root.GetProperty("lifecycle").GetProperty("state").GetString());
            Assert.False(root.GetProperty("lifecycle").GetProperty("blocks_automation").GetBoolean());
            Assert.Equal(originalBaseUrl, repairedDescriptor.RootElement.GetProperty("base_url").GetString());
            Assert.Equal(originalProcessId, repairedDescriptor.RootElement.GetProperty("process_id").GetInt32());
            Assert.True(IsProcessAlive(sleeper.ProcessId));
        }
        finally
        {
            File.WriteAllText(descriptorPath, originalDescriptorJson);
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostStatusJson_ProjectsHealthyWithPointerRepairWhenActiveDescriptorIsRepaired()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        var originalDescriptorJson = File.ReadAllText(descriptorPath);
        using var originalDescriptor = JsonDocument.Parse(originalDescriptorJson);
        var originalBaseUrl = originalDescriptor.RootElement.GetProperty("base_url").GetString()
            ?? throw new InvalidOperationException("Expected active host base_url.");
        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "stale-status-repair");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        try
        {
            WriteHostDescriptor(
                sandbox.RootPath,
                sleeper.ProcessId,
                runtimeDirectory,
                staleDeploymentDirectory,
                staleExecutablePath,
                $"http://127.0.0.1:{stalePort}",
                stalePort);

            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--json");
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(status.StandardOutput) ? status.StandardError : status.StandardOutput);
            var root = document.RootElement;

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, status.ExitCode);
            Assert.Equal("carves-host-status.v1", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("host_running").GetBoolean());
            Assert.Equal("healthy_with_pointer_repair", root.GetProperty("host_readiness").GetString());
            Assert.Equal("healthy_with_pointer_repair", root.GetProperty("host_operational_state").GetString());
            Assert.False(root.GetProperty("conflict_present").GetBoolean());
            Assert.False(root.GetProperty("safe_to_start_new_host").GetBoolean());
            Assert.True(root.GetProperty("pointer_repair_applied").GetBoolean());
            Assert.Equal("none", root.GetProperty("recommended_action_kind").GetString());
            Assert.Equal("ready", root.GetProperty("lifecycle").GetProperty("state").GetString());
            Assert.Equal("none", root.GetProperty("lifecycle").GetProperty("action_kind").GetString());
            Assert.Equal("host ready", root.GetProperty("recommended_action").GetString());
            Assert.Equal(originalBaseUrl, root.GetProperty("base_url").GetString());
        }
        finally
        {
            File.WriteAllText(descriptorPath, originalDescriptorJson);
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostHonesty_ProjectsConsistentConflictAcrossHostStatusDoctorAndPilotStatus()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "consistency-conflict");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            sandbox.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var hostStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--json");
        var doctor = CliProgramHarness.RunInDirectory(sandbox.RootPath, "doctor", "--json");
        var pilotStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-product-closure-pilot-status");

        using var hostStatusDocument = JsonDocument.Parse(string.IsNullOrWhiteSpace(hostStatus.StandardOutput) ? hostStatus.StandardError : hostStatus.StandardOutput);
        using var doctorDocument = JsonDocument.Parse(doctor.StandardOutput);
        using var pilotDocument = JsonDocument.Parse(pilotStatus.StandardOutput);
        var hostRoot = hostStatusDocument.RootElement;
        var doctorRoot = doctorDocument.RootElement;
        var pilotRoot = pilotDocument.RootElement;

        Assert.Equal(1, hostStatus.ExitCode);
        Assert.Equal(1, doctor.ExitCode);
        Assert.Equal(0, pilotStatus.ExitCode);
        Assert.Equal("host_session_conflict", hostRoot.GetProperty("host_readiness").GetString());
        Assert.Equal("host_session_conflict", doctorRoot.GetProperty("host_readiness").GetString());
        Assert.Equal("host_session_conflict", pilotRoot.GetProperty("host_readiness").GetString());
        Assert.Equal("host_session_conflict", hostRoot.GetProperty("host_operational_state").GetString());
        Assert.Equal("host_session_conflict", doctorRoot.GetProperty("host_operational_state").GetString());
        Assert.Equal("host_session_conflict", pilotRoot.GetProperty("host_operational_state").GetString());
        Assert.True(hostRoot.GetProperty("conflict_present").GetBoolean());
        Assert.True(doctorRoot.GetProperty("host_conflict_present").GetBoolean());
        Assert.True(pilotRoot.GetProperty("host_conflict_present").GetBoolean());
        Assert.False(hostRoot.GetProperty("safe_to_start_new_host").GetBoolean());
        Assert.False(doctorRoot.GetProperty("host_safe_to_start_new_host").GetBoolean());
        Assert.False(pilotRoot.GetProperty("host_safe_to_start_new_host").GetBoolean());
        Assert.Contains("host reconcile --replace-stale", hostRoot.GetProperty("recommended_action").GetString(), StringComparison.Ordinal);
        Assert.Contains("host reconcile --replace-stale", doctorRoot.GetProperty("host_recommended_action").GetString(), StringComparison.Ordinal);
        Assert.Contains("host reconcile --replace-stale", pilotRoot.GetProperty("host_recommended_action").GetString(), StringComparison.Ordinal);
        Assert.False(pilotRoot.GetProperty("safe_to_start_new_execution").GetBoolean());
    }

    [Fact]
    public void Doctor_ProjectsHealthyWithPointerRepairWhenDoctorFirstReconcilesStalePointer()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        var originalDescriptorJson = File.ReadAllText(descriptorPath);
        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "consistency-pointer-repair");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        try
        {
            WriteHostDescriptor(
                sandbox.RootPath,
                sleeper.ProcessId,
                runtimeDirectory,
                staleDeploymentDirectory,
                staleExecutablePath,
                $"http://127.0.0.1:{stalePort}",
                stalePort);

            var doctor = CliProgramHarness.RunInDirectory(sandbox.RootPath, "doctor", "--json");
            using var doctorDocument = JsonDocument.Parse(doctor.StandardOutput);
            var doctorRoot = doctorDocument.RootElement;

            Assert.Equal(0, start.ExitCode);
            Assert.Equal("healthy_with_pointer_repair", doctorRoot.GetProperty("host_readiness").GetString());
            Assert.Equal("healthy_with_pointer_repair", doctorRoot.GetProperty("host_operational_state").GetString());
            Assert.False(doctorRoot.GetProperty("host_conflict_present").GetBoolean());
            Assert.False(doctorRoot.GetProperty("host_safe_to_start_new_host").GetBoolean());
            Assert.Equal("host ready", doctorRoot.GetProperty("host_recommended_action").GetString());
        }
        finally
        {
            File.WriteAllText(descriptorPath, originalDescriptorJson);
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void PilotStatusApi_ProjectsHealthyWithPointerRepairWhenPilotStatusFirstReconcilesStalePointer()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        var originalDescriptorJson = File.ReadAllText(descriptorPath);
        using var originalDescriptor = JsonDocument.Parse(originalDescriptorJson);
        var originalBaseUrl = originalDescriptor.RootElement.GetProperty("base_url").GetString()
            ?? throw new InvalidOperationException("Expected active host base_url.");

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "pilot-pointer-repair");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        try
        {
            WriteHostDescriptor(
                sandbox.RootPath,
                sleeper.ProcessId,
                runtimeDirectory,
                staleDeploymentDirectory,
                staleExecutablePath,
                $"http://127.0.0.1:{stalePort}",
                stalePort);

            var pilotStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-product-closure-pilot-status");
            using var pilotDocument = JsonDocument.Parse(pilotStatus.StandardOutput);
            var pilotRoot = pilotDocument.RootElement;

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, pilotStatus.ExitCode);
            Assert.Equal("healthy_with_pointer_repair", pilotRoot.GetProperty("host_readiness").GetString());
            Assert.Equal("healthy_with_pointer_repair", pilotRoot.GetProperty("host_operational_state").GetString());
            Assert.False(pilotRoot.GetProperty("host_conflict_present").GetBoolean());
            Assert.False(pilotRoot.GetProperty("host_safe_to_start_new_host").GetBoolean());
            Assert.True(pilotRoot.GetProperty("host_pointer_repair_applied").GetBoolean());
            Assert.Equal("host ready", pilotRoot.GetProperty("host_recommended_action").GetString());
            Assert.Contains("Repaired active host descriptor", pilotRoot.GetProperty("host_summary_message").GetString(), StringComparison.Ordinal);
            Assert.True(pilotRoot.GetProperty("safe_to_start_new_execution").GetBoolean());
        }
        finally
        {
            File.WriteAllText(descriptorPath, originalDescriptorJson);
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostReconcileJson_ReplaceStale_ReplacesConflictingGenerationAndStartsFreshHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "stale-replace");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            sandbox.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        try
        {
            var reconcile = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "reconcile", "--replace-stale", "--json", "--interval-ms", "200");
            var reconcileJson = string.IsNullOrWhiteSpace(reconcile.StandardOutput)
                ? reconcile.StandardError
                : reconcile.StandardOutput;
            using var document = JsonDocument.Parse(reconcileJson);
            var root = document.RootElement;
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            var generationDescriptorsDirectory = Path.Combine(ResolveHostRuntimeDirectory(sandbox.RootPath), "host-generations");
            var snapshotPath = ControlPlanePaths.FromRepoRoot(sandbox.RootPath).PlatformHostSnapshotLiveStateFile;
            using var descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            using var snapshot = JsonDocument.Parse(File.ReadAllText(snapshotPath));
            var reconciledBaseUrl = root.GetProperty("base_url").GetString();
            var generationDescriptorPath = Assert.Single(Directory.EnumerateFiles(generationDescriptorsDirectory, "*.json", SearchOption.TopDirectoryOnly));
            using var generationDescriptor = JsonDocument.Parse(File.ReadAllText(generationDescriptorPath));
            var reconciledProcessId = descriptor.RootElement.GetProperty("process_id").GetInt32();

            Assert.Equal(0, reconcile.ExitCode);
            Assert.Equal("carves-host-reconcile.v1", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("host_running").GetBoolean());
            Assert.True(root.GetProperty("replaced").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(reconciledBaseUrl));
            Assert.DoesNotContain($"http://127.0.0.1:{stalePort}", reconciledBaseUrl, StringComparison.Ordinal);
            Assert.Contains("Replaced stale resident host process", root.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal(reconciledBaseUrl, descriptor.RootElement.GetProperty("base_url").GetString());
            Assert.Equal(reconciledProcessId, root.GetProperty("process_id").GetInt32());
            Assert.Equal(reconciledBaseUrl, generationDescriptor.RootElement.GetProperty("base_url").GetString());
            Assert.Equal(reconciledProcessId, generationDescriptor.RootElement.GetProperty("process_id").GetInt32());
            Assert.Equal("live", snapshot.RootElement.GetProperty("state").GetString());
            Assert.Equal(reconciledBaseUrl, snapshot.RootElement.GetProperty("base_url").GetString());
            Assert.Equal(reconciledProcessId, snapshot.RootElement.GetProperty("process_id").GetInt32());
            Assert.False(IsProcessAlive(sleeper.ProcessId));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostReconcileJson_ReplaceStale_DoesNotReplaceWhenHealthyGenerationCanBeReconciled()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        var originalDescriptorJson = File.ReadAllText(descriptorPath);
        using var originalDescriptor = JsonDocument.Parse(originalDescriptorJson);
        var originalBaseUrl = originalDescriptor.RootElement.GetProperty("base_url").GetString()
            ?? throw new InvalidOperationException("Expected active host base_url.");

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "reconcile-no-replace");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        try
        {
            WriteHostDescriptor(
                sandbox.RootPath,
                sleeper.ProcessId,
                runtimeDirectory,
                staleDeploymentDirectory,
                staleExecutablePath,
                $"http://127.0.0.1:{stalePort}",
                stalePort);

            var reconcile = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "reconcile", "--replace-stale", "--json", "--interval-ms", "200");
            var reconcileJson = string.IsNullOrWhiteSpace(reconcile.StandardOutput)
                ? reconcile.StandardError
                : reconcile.StandardOutput;
            using var document = JsonDocument.Parse(reconcileJson);
            var root = document.RootElement;
            using var repairedDescriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, reconcile.ExitCode);
            Assert.Equal("carves-host-reconcile.v1", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("host_running").GetBoolean());
            Assert.False(root.GetProperty("replaced").GetBoolean());
            Assert.Contains("already healthy", root.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("none", root.GetProperty("next_action_kind").GetString());
            Assert.Equal("ready", root.GetProperty("lifecycle").GetProperty("state").GetString());
            Assert.Equal("host ready", root.GetProperty("next_action").GetString());
            Assert.Equal(originalBaseUrl, repairedDescriptor.RootElement.GetProperty("base_url").GetString());
            Assert.True(IsProcessAlive(sleeper.ProcessId));
        }
        finally
        {
            File.WriteAllText(descriptorPath, originalDescriptorJson);
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void HostReconcileJson_ReplaceStale_DoesNotStartFreshHostWhenNoStaleConflictExists()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");

        var reconcile = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "reconcile", "--replace-stale", "--json", "--interval-ms", "200");
        var reconcileJson = string.IsNullOrWhiteSpace(reconcile.StandardOutput)
            ? reconcile.StandardError
            : reconcile.StandardOutput;
        using var document = JsonDocument.Parse(reconcileJson);
        var root = document.RootElement;

        Assert.Equal(1, reconcile.ExitCode);
        Assert.Equal("carves-host-reconcile.v1", root.GetProperty("schema_version").GetString());
        Assert.False(root.GetProperty("host_running").GetBoolean());
        Assert.False(root.GetProperty("replaced").GetBoolean());
        Assert.False(root.GetProperty("conflict_present").GetBoolean());
        Assert.Contains("No stale conflicting resident host generation exists", root.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("ensure_host", root.GetProperty("next_action_kind").GetString());
        Assert.Equal("recoverable", root.GetProperty("lifecycle").GetProperty("state").GetString());
        Assert.Equal("ensure_host", root.GetProperty("lifecycle").GetProperty("action_kind").GetString());
        Assert.Equal("carves host ensure --json", root.GetProperty("next_action").GetString());
        Assert.False(File.Exists(descriptorPath));
    }

    [Fact]
    public void HostReconcileJson_ReplaceStale_FailsClosedWhenConflictingProcessCannotBeTerminated()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var nonTerminableProcessId = OperatingSystem.IsWindows() ? 4 : 1;
        Assert.True(IsProcessAlive(nonTerminableProcessId), $"Expected PID {nonTerminableProcessId} to exist for non-terminable stale host simulation.");

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "reconcile-unterminable");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        WriteHostDescriptor(
            sandbox.RootPath,
            nonTerminableProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            "http://127.0.0.1:1",
            1);

        var reconcile = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "reconcile", "--replace-stale", "--json", "--interval-ms", "200");
        var reconcileJson = string.IsNullOrWhiteSpace(reconcile.StandardOutput)
            ? reconcile.StandardError
            : reconcile.StandardOutput;
        using var document = JsonDocument.Parse(reconcileJson);
        var root = document.RootElement;
        var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
        var snapshotPath = ControlPlanePaths.FromRepoRoot(sandbox.RootPath).PlatformHostSnapshotLiveStateFile;
        using var descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));
        using var snapshot = JsonDocument.Parse(File.ReadAllText(snapshotPath));

        Assert.Equal(1, reconcile.ExitCode);
        Assert.Equal("carves-host-reconcile.v1", root.GetProperty("schema_version").GetString());
        Assert.False(root.GetProperty("host_running").GetBoolean());
        Assert.False(root.GetProperty("replaced").GetBoolean());
        Assert.True(root.GetProperty("conflict_present").GetBoolean());
        Assert.Equal(nonTerminableProcessId, root.GetProperty("conflicting_process_id").GetInt32());
        Assert.Contains("could not be terminated", root.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("manual_terminate_conflicting_process", root.GetProperty("next_action_kind").GetString());
        Assert.Equal("blocked", root.GetProperty("lifecycle").GetProperty("state").GetString());
        Assert.Equal("manual_terminate_conflicting_process", root.GetProperty("lifecycle").GetProperty("action_kind").GetString());
        Assert.Contains("rerun `carves host reconcile --replace-stale --json`", root.GetProperty("next_action").GetString(), StringComparison.Ordinal);
        Assert.Equal(nonTerminableProcessId, descriptor.RootElement.GetProperty("process_id").GetInt32());
        Assert.Equal("stale", snapshot.RootElement.GetProperty("state").GetString());
        Assert.Equal(nonTerminableProcessId, snapshot.RootElement.GetProperty("process_id").GetInt32());
    }


    [Fact]
    public void HostLifecycle_RegistersHeartbeatAndStoppedStateInHostRegistry()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var activeEntry = Assert.Single(ReadHostRegistry(sandbox.RootPath), item => string.Equals(item.Status, "active", StringComparison.OrdinalIgnoreCase));
            var lastSeenBeforeHeartbeat = activeEntry.LastSeen;

            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "status");
            var heartbeatEntry = Assert.Single(ReadHostRegistry(sandbox.RootPath), item => string.Equals(item.HostId, activeEntry.HostId, StringComparison.Ordinal));
            var stop = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "host registry validation");
            var coldHosts = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "hosts");
            var stoppedEntry = Assert.Single(ReadHostRegistry(sandbox.RootPath), item => string.Equals(item.HostId, activeEntry.HostId, StringComparison.Ordinal));

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, status.ExitCode);
            Assert.Equal(0, stop.ExitCode);
            Assert.Equal(0, coldHosts.ExitCode);
            Assert.Equal("active", activeEntry.Status);
            Assert.Equal(activeEntry.HostId, heartbeatEntry.HostId);
            Assert.True(heartbeatEntry.LastSeen >= lastSeenBeforeHeartbeat);
            Assert.Equal("stopped", stoppedEntry.Status);
            Assert.Equal(activeEntry.HostId, stoppedEntry.HostId);
            Assert.Contains(activeEntry.HostId, coldHosts.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("(stopped)", coldHosts.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void HostRestart_ReusesStableHostRegistryIdentityWithoutDuplicates()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var firstStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var firstEntry = Assert.Single(ReadHostRegistry(sandbox.RootPath), item => string.Equals(item.Status, "active", StringComparison.OrdinalIgnoreCase));
            var firstHostId = firstEntry.HostId;

            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "restart registry test");
            var secondStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var entries = ReadHostRegistry(sandbox.RootPath);
            var activeEntry = Assert.Single(entries, item => string.Equals(item.Status, "active", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(0, firstStart.ExitCode);
            Assert.Equal(0, secondStart.ExitCode);
            Assert.Equal(firstHostId, activeEntry.HostId);
            Assert.Equal("active", activeEntry.Status);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void Discover_MarksUnresponsiveLiveHostAsUnknownInRegistry()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var descriptorPath = Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json");
            var originalDescriptor = File.ReadAllText(descriptorPath);
            var descriptor = JsonNode.Parse(originalDescriptor)!.AsObject();
            descriptor["base_url"] = "http://127.0.0.1:65530";
            File.WriteAllText(descriptorPath, descriptor.ToJsonString(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            }));

            var discover = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "discover");
            var entry = Assert.Single(ReadHostRegistry(sandbox.RootPath), item => string.Equals(item.Status, "unknown", StringComparison.OrdinalIgnoreCase));

            Assert.NotEqual(0, discover.ExitCode);
            Assert.Equal("unknown", entry.Status);
            Assert.Contains("stale", discover.CombinedOutput, StringComparison.OrdinalIgnoreCase);

            File.WriteAllText(descriptorPath, originalDescriptor);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public async Task HostStatus_RecoversTransientHandshakeAndReplacesPersistedStaleSnapshot()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        await using var fakeHost = FakeDiscoveryHost.Start(handshakeFailuresBeforeSuccess: 1, marker: "transient-handshake");

        WriteHostDescriptor(
            Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json"),
            sandbox.RootPath,
            fakeHost.BaseUrl,
            Process.GetCurrentProcess().Id,
            "host-card-309-transient",
            Environment.MachineName);
        SeedStaleHostSnapshot(sandbox.RootPath, fakeHost.BaseUrl, Process.GetCurrentProcess().Id);

        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "workbench");
        var workbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "workbench", "review");
        var workbenchUrl = ExtractOutputValue(workbench.StandardOutput, "Workbench: ");

        using var client = new HttpClient();
        var html = await client.GetStringAsync(workbenchUrl);
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        using var snapshot = JsonDocument.Parse(File.ReadAllText(paths.PlatformHostSnapshotLiveStateFile));

        Assert.Equal(0, status.ExitCode);
        Assert.Equal(0, workbench.ExitCode);
        Assert.True(fakeHost.HandshakeRequestCount >= 3);
        Assert.Contains($"Base URL: {fakeHost.BaseUrl}", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Host snapshot state: Live", status.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Host snapshot state: Stale", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(fakeHost.BaseUrl, workbench.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CARVES Fake Workbench", html, StringComparison.Ordinal);
        Assert.Equal("live", snapshot.RootElement.GetProperty("state").GetString());
        Assert.Equal(fakeHost.BaseUrl, snapshot.RootElement.GetProperty("base_url").GetString());
    }


    [Fact]
    public async Task Workbench_CommandPrefersRepoDescriptorOverForeignMachineResidue()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        await using var foreignHost = FakeDiscoveryHost.Start(marker: "foreign-machine-residue");
        var machineDescriptorPath = Path.Combine(Path.GetTempPath(), "carves-runtime-host", "active-host.json");
        var machineDescriptorBackupExists = File.Exists(machineDescriptorPath);
        var machineDescriptorBackup = machineDescriptorBackupExists
            ? File.ReadAllText(machineDescriptorPath)
            : null;

        try
        {
            if (machineDescriptorBackupExists)
            {
                File.Delete(machineDescriptorPath);
            }

            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            Assert.Equal(0, start.ExitCode);
            var repoDescriptor = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json")))!.AsObject();
            var repoBaseUrl = repoDescriptor["base_url"]!.GetValue<string>();

            WriteHostDescriptor(
                machineDescriptorPath,
                @"C:\foreign\repo",
                foreignHost.BaseUrl,
                Process.GetCurrentProcess().Id,
                "host-foreign-residue",
                Environment.MachineName);

            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "workbench");
            var workbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "workbench", "review");

            Assert.Equal(0, status.ExitCode);
            Assert.Equal(0, workbench.ExitCode);
            Assert.Contains($"Base URL: {repoBaseUrl}", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(repoBaseUrl, workbench.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(foreignHost.BaseUrl, status.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(foreignHost.BaseUrl, workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, foreignHost.HandshakeRequestCount);
        }
        finally
        {
            if (machineDescriptorBackupExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(machineDescriptorPath)!);
                File.WriteAllText(machineDescriptorPath, machineDescriptorBackup!);
            }
            else if (File.Exists(machineDescriptorPath))
            {
                File.Delete(machineDescriptorPath);
            }

            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void Workbench_CommandFallsBackWithConflictHonestyWhenResidentHostSessionConflicts()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "workbench-conflict");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            sandbox.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var workbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "workbench", "review");

        Assert.Equal(0, workbench.ExitCode);
        Assert.Contains("Resident host session conflict is present; executing `workbench` through the cold path.", workbench.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves host reconcile --replace-stale --json", workbench.StandardOutput, StringComparison.Ordinal);
    }


    [Fact]
    public void Workbench_CommandTracksCurrentHostAfterResidentRestart()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var machineDescriptorPath = Path.Combine(Path.GetTempPath(), "carves-runtime-host", "active-host.json");
        var machineDescriptorBackupExists = File.Exists(machineDescriptorPath);
        var machineDescriptorBackup = machineDescriptorBackupExists
            ? File.ReadAllText(machineDescriptorPath)
            : null;
        var firstPort = ReserveLoopbackPort();
        var secondPort = ReserveLoopbackPort();
        while (secondPort == firstPort)
        {
            secondPort = ReserveLoopbackPort();
        }

        try
        {
            if (machineDescriptorBackupExists)
            {
                File.Delete(machineDescriptorPath);
            }

            var firstStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200", "--port", firstPort.ToString());
            var firstWorkbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "workbench", "review");
            var stop = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "restart workbench convergence test");
            var secondStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200", "--port", secondPort.ToString());
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "workbench");
            var secondWorkbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "workbench", "review");

            Assert.Equal(0, firstStart.ExitCode);
            Assert.Equal(0, firstWorkbench.ExitCode);
            Assert.Equal(0, stop.ExitCode);
            Assert.Equal(0, secondStart.ExitCode);
            Assert.Equal(0, status.ExitCode);
            Assert.Equal(0, secondWorkbench.ExitCode);
            Assert.Contains($"http://127.0.0.1:{firstPort}/workbench/review", firstWorkbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Base URL: http://127.0.0.1:{secondPort}", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Host snapshot state: Live", status.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain($"Base URL: http://127.0.0.1:{firstPort}", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"http://127.0.0.1:{secondPort}/workbench/review", secondWorkbench.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain($"http://127.0.0.1:{firstPort}/workbench/review", secondWorkbench.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            if (machineDescriptorBackupExists)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(machineDescriptorPath)!);
                File.WriteAllText(machineDescriptorPath, machineDescriptorBackup!);
            }
            else if (File.Exists(machineDescriptorPath))
            {
                File.Delete(machineDescriptorPath);
            }

            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void ColdHostStatus_ReportsStoppedSnapshotAfterGracefulStop()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
        var stop = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "graceful stop for snapshot");
        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "host", "status");

        Assert.Equal(0, stop.ExitCode);
        Assert.Contains("Host stop requested:", stop.StandardOutput, StringComparison.Ordinal);
        Assert.NotEqual(0, status.ExitCode);
        Assert.Contains("Last snapshot: stopped", status.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Snapshot state: Stopped", status.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Snapshot recorded at:", status.CombinedOutput, StringComparison.Ordinal);
    }


    [Fact]
    public void HostStopForce_LeavesStoppedSnapshotWithoutStreamNoise()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
        var stop = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "--force", "forced stop for validation");
        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "host", "status");

        Assert.Equal(0, stop.ExitCode);
        Assert.DoesNotContain("Error while copying content to a stream", stop.CombinedOutput, StringComparison.Ordinal);
        Assert.NotEqual(0, status.ExitCode);
        Assert.Contains("Snapshot state: Stopped", status.CombinedOutput, StringComparison.Ordinal);
    }


    [Fact]
    public void HostRoutedCardTaskInspectAndDiscussShareMachineTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-CARD-077-SURFACE", scope: ["README.md"]);

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var card = ProgramHarness.Run("--repo-root", sandbox.RootPath, "card", "inspect", "CARD-077");
            var task = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "inspect", "T-CARD-077-001");
            var discuss = ProgramHarness.Run("--repo-root", sandbox.RootPath, "discuss", "task", "T-CARD-077-001");

            Assert.Equal(0, card.ExitCode);
            Assert.Contains("\"card_id\": \"CARD-077\"", card.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, task.ExitCode);
            Assert.Contains("\"task_id\": \"T-CARD-077-001\"", task.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, discuss.ExitCode);
            Assert.Contains("\"kind\": \"task_discussion\"", discuss.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

}
