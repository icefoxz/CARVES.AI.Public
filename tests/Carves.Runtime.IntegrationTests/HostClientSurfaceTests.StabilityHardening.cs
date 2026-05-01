using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class HostClientSurfaceTests
{
    [Fact]
    public async Task LocalHostClient_RecoversAcceptedOperationAfterInitialRequestTimeout()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
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
    "request_timeout_seconds": 1,
    "use_accepted_operation_polling": true,
    "poll_interval_ms": 100,
    "base_wait_seconds": 8,
    "stall_timeout_seconds": 8,
    "max_wait_seconds": 12
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

        await using var fakeHost = DelayedAcceptedOperationHost.Start(initialInvokeDelayMs: 1200);
        WriteHostDescriptor(
            Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json"),
            sandbox.RootPath,
            fakeHost.BaseUrl,
            Process.GetCurrentProcess().Id,
            "fake-stability-host",
            "fake-machine");

        var hostAssembly = typeof(Program).Assembly;
        var clientType = hostAssembly.GetType("Carves.Runtime.Host.LocalHostClient", throwOnError: true)
            ?? throw new InvalidOperationException("Expected LocalHostClient type.");
        var client = Activator.CreateInstance(clientType, sandbox.RootPath)
            ?? throw new InvalidOperationException("Expected LocalHostClient instance.");
        var invoke = clientType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected LocalHostClient.Invoke.");

        var result = (OperatorCommandResult)invoke.Invoke(client, ["sync-state", Array.Empty<string>(), Array.Empty<string>()])!;

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(result.Lines, line => line.Contains("Accepted-operation polling recovered after the initial host request timed out.", StringComparison.Ordinal));
        Assert.Contains(result.Lines, line => line.Contains("Recovered accepted operation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LocalHostClient_BlocksInvokeWhenResidentHostCommandSurfaceIsStale()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        await using var fakeHost = DelayedAcceptedOperationHost.Start(initialInvokeDelayMs: 0, includeCommandSurfaceMetadata: false);
        WriteHostDescriptor(
            Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json"),
            sandbox.RootPath,
            fakeHost.BaseUrl,
            Process.GetCurrentProcess().Id,
            "fake-stale-surface-host",
            "fake-machine");

        var hostAssembly = typeof(Program).Assembly;
        var clientType = hostAssembly.GetType("Carves.Runtime.Host.LocalHostClient", throwOnError: true)
            ?? throw new InvalidOperationException("Expected LocalHostClient type.");
        var client = Activator.CreateInstance(clientType, sandbox.RootPath)
            ?? throw new InvalidOperationException("Expected LocalHostClient instance.");
        var invoke = clientType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected LocalHostClient.Invoke.");

        var result = (OperatorCommandResult)invoke.Invoke(client, ["api", new[] { "worker-automation-readiness" }, new[] { "runtime-status" }])!;

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Lines, line => line.Contains("surface_registry_stale", StringComparison.Ordinal));
        Assert.Contains(result.Lines, line => line.Contains("host_restart_required", StringComparison.Ordinal));
        Assert.Contains(result.Lines, line => line.Contains("Next action: carves gateway restart", StringComparison.Ordinal));
        Assert.Equal(0, fakeHost.InvokeRequestCount);
    }

    [Fact]
    public async Task HostStatusJson_ReportsSurfaceRegistryStaleForOldResidentHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        await using var fakeHost = DelayedAcceptedOperationHost.Start(initialInvokeDelayMs: 0, includeCommandSurfaceMetadata: false);
        WriteHostDescriptor(
            Path.Combine(sandbox.RootPath, ".carves-platform", "host", "descriptor.json"),
            sandbox.RootPath,
            fakeHost.BaseUrl,
            Process.GetCurrentProcess().Id,
            "fake-stale-status-host",
            "fake-machine");

        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--json");
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(status.StandardOutput) ? status.StandardError : status.StandardOutput);
        var root = document.RootElement;

        Assert.Equal(1, status.ExitCode);
        Assert.True(root.GetProperty("host_running").GetBoolean());
        Assert.Equal("surface_registry_stale", root.GetProperty("host_readiness").GetString());
        Assert.Equal("host_restart_required", root.GetProperty("host_operational_state").GetString());
        Assert.False(root.GetProperty("host_command_surface_compatible").GetBoolean());
        Assert.Equal("surface_registry_stale", root.GetProperty("host_command_surface_readiness").GetString());
        Assert.Equal("missing_command_surface_metadata", root.GetProperty("host_command_surface_reason").GetString());
        Assert.Equal("restart_for_surface_registry", root.GetProperty("recommended_action_kind").GetString());
        Assert.Equal("blocked", root.GetProperty("lifecycle").GetProperty("state").GetString());
        Assert.Equal("surface_registry_stale", root.GetProperty("lifecycle").GetProperty("reason").GetString());
        Assert.Contains("carves gateway restart", root.GetProperty("recommended_action").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SyncState_ReportsProjectionWritebackDegradedWhenMarkdownFileIsLocked()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-PROJECTION-DEGRADED");

        var statePath = Path.Combine(sandbox.RootPath, ".ai", "STATE.md");
        if (!File.Exists(statePath))
        {
            File.WriteAllText(statePath, "STATE");
        }

        using var locker = LockedFileProcess.Start(statePath);

        var sync = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "sync-state");
        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "status", "--summary");
        var healthPath = Path.Combine(sandbox.RootPath, ".carves-platform", "runtime-state", "markdown_projection_health.json");
        var health = JsonNode.Parse(File.ReadAllText(healthPath))!.AsObject();

        Assert.Equal(0, sync.ExitCode);
        Assert.Contains("Projection writeback: degraded", sync.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("degraded", health["state"]!.GetValue<string>());
        Assert.Contains(statePath.Replace('\\', '/').Split('/').Last(), health["summary"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, status.ExitCode);
        Assert.Contains("Projection writeback: degraded", status.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskRun_WithHost_ExistingActiveExecutionRunReturnsAlreadyRunningEnvelope()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        const string taskId = "T-INTEGRATION-HOST-ALREADY-RUNNING";
        const string runId = "RUN-T-INTEGRATION-HOST-ALREADY-RUNNING-001";
        sandbox.AddSyntheticPendingTask(taskId, scope: [".ai/tests/Host/Repeat"]);

        var nodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json");
        var taskNode = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        var metadata = taskNode["metadata"]?.AsObject() ?? new JsonObject();
        metadata["execution_run_latest_id"] = runId;
        metadata["execution_run_latest_status"] = "Running";
        metadata["execution_run_active_id"] = runId;
        metadata["execution_run_current_step_index"] = "1";
        metadata["execution_run_current_step_title"] = "Implement";
        metadata["execution_run_count"] = "1";
        taskNode["metadata"] = metadata;
        File.WriteAllText(nodePath, taskNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var runRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", taskId);
        Directory.CreateDirectory(runRoot);
        var run = new ExecutionRun
        {
            RunId = runId,
            TaskId = taskId,
            Status = ExecutionRunStatus.Running,
            CurrentStepIndex = 1,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            Steps =
            [
                new ExecutionStep
                {
                    StepId = $"{runId}-STEP-001",
                    Title = "Inspect task context and authoritative truth.",
                    Kind = ExecutionStepKind.Inspect,
                    Status = ExecutionStepStatus.Completed,
                    StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                    EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                },
                new ExecutionStep
                {
                    StepId = $"{runId}-STEP-002",
                    Title = "Implement the scoped change set.",
                    Kind = ExecutionStepKind.Implement,
                    Status = ExecutionStepStatus.InProgress,
                    StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                },
            ],
        };
        File.WriteAllText(Path.Combine(runRoot, $"{runId}.json"), JsonSerializer.Serialize(run, CamelCaseJsonOptions));

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var runResult = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", taskId);
            using var envelope = ParseJsonOutput(runResult.StandardOutput);
            var updatedTask = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
            var runFiles = Directory.EnumerateFiles(runRoot, "*.json").ToArray();

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, runResult.ExitCode);
            Assert.Contains("Connected to host:", runResult.StandardOutput, StringComparison.Ordinal);
            Assert.True(envelope.RootElement.GetProperty("accepted").GetBoolean());
            Assert.Equal("already_running", envelope.RootElement.GetProperty("outcome").GetString());
            Assert.Equal(runId, envelope.RootElement.GetProperty("execution_run_id").GetString());
            Assert.Equal(runId, updatedTask["metadata"]!["execution_run_active_id"]!.GetValue<string>());
            Assert.Single(runFiles);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    private sealed class DelayedAcceptedOperationHost : IAsyncDisposable
    {
        private static readonly TimeSpan CompletedOperationProjectionDelay = TimeSpan.FromMilliseconds(50);
        private readonly HttpListener listener;
        private readonly CancellationTokenSource cancellationSource = new();
        private readonly Task serverTask;
        private readonly int initialInvokeDelayMs;
        private readonly bool includeCommandSurfaceMetadata;
        private readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        };
        private readonly object gate = new();
        private readonly Dictionary<string, JsonObject> operations = new(StringComparer.Ordinal);

        private DelayedAcceptedOperationHost(string baseUrl, HttpListener listener, int initialInvokeDelayMs, bool includeCommandSurfaceMetadata)
        {
            BaseUrl = baseUrl;
            this.listener = listener;
            this.initialInvokeDelayMs = initialInvokeDelayMs;
            this.includeCommandSurfaceMetadata = includeCommandSurfaceMetadata;
            serverTask = ListenAsync();
        }

        public string BaseUrl { get; }

        public int InvokeRequestCount { get; private set; }

        public static DelayedAcceptedOperationHost Start(int initialInvokeDelayMs, bool includeCommandSurfaceMetadata = true)
        {
            var port = ReserveLoopbackPort();
            var baseUrl = $"http://127.0.0.1:{port}";
            var listener = new HttpListener();
            listener.Prefixes.Add($"{baseUrl}/");
            listener.Start();
            return new DelayedAcceptedOperationHost(baseUrl, listener, initialInvokeDelayMs, includeCommandSurfaceMetadata);
        }

        public async ValueTask DisposeAsync()
        {
            cancellationSource.Cancel();
            try
            {
                listener.Stop();
                listener.Close();
            }
            catch
            {
            }

            try
            {
                await serverTask.ConfigureAwait(false);
            }
            catch
            {
            }

            cancellationSource.Dispose();
        }

        private async Task ListenAsync()
        {
            while (!cancellationSource.IsCancellationRequested)
            {
                HttpListenerContext? context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (cancellationSource.IsCancellationRequested)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                QueueBackgroundWork(() => HandleRequest(context));
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (string.Equals(path, "/handshake", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new JsonObject
                {
                    ["host_running"] = true,
                    ["version"] = "1.0.0",
                    ["standard_version"] = "1.0.0",
                    ["stage"] = "Stage-8A fleet discovery and registry completed",
                    ["host_session_id"] = "fake-stability-session",
                    ["uptime_seconds"] = 5,
                    ["base_url"] = BaseUrl,
                    ["dashboard_url"] = $"{BaseUrl}/dashboard",
                    ["runtime_directory"] = @"C:\fake\stability",
                    ["deployment_directory"] = @"C:\fake\stability\deployment",
                    ["executable_path"] = @"C:\fake\stability\deployment\Carves.Runtime.Host.dll",
                    ["repo_count"] = 1,
                    ["attached_repo_count"] = 1,
                    ["active_session_count"] = 0,
                    ["planner_state"] = "idle",
                    ["worker_count"] = 0,
                    ["active_worker_count"] = 0,
                    ["actor_session_count"] = 0,
                    ["operator_os_event_count"] = 0,
                    ["rehydrated"] = true,
                    ["pending_approval_count"] = 0,
                    ["recent_incident_count"] = 0,
                    ["stale_marker_count"] = 0,
                    ["paused_runtime_count"] = 0,
                    ["rehydration_summary"] = "Delayed accepted-operation fake host is ready.",
                    ["host_control_state"] = "running",
                    ["host_control_action"] = "started",
                    ["host_control_reason"] = "Fake host started.",
                    ["host_control_at"] = DateTimeOffset.UtcNow.ToString("O"),
                    ["protocol_mode"] = "hosted",
                    ["conversation_phase"] = "execution",
                    ["intent_state"] = "active",
                    ["prompt_kernel"] = "fake-kernel@1.0.0",
                    ["project_understanding_state"] = "ready",
                    ["capabilities"] = new JsonArray("runtime-status", "control-plane-mutation", "dashboard", "workbench"),
                };
                if (includeCommandSurfaceMetadata)
                {
                    AddCurrentCommandSurfaceMetadata(payload);
                }

                WriteJson(context.Response, payload);
                return;
            }

            if (string.Equals(path, "/invoke", StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                InvokeRequestCount++;
                var request = JsonNode.Parse(new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd())!.AsObject();
                var operationId = request["operation_id"]?.GetValue<string>() ?? $"hostop-{Guid.NewGuid():N}";
                var now = DateTimeOffset.UtcNow;
                lock (gate)
                {
                    operations[operationId] = new JsonObject
                    {
                        ["operation_id"] = operationId,
                        ["command"] = request["command"]?.GetValue<string>() ?? "sync-state",
                        ["operation_state"] = "accepted",
                        ["completed"] = false,
                        ["exit_code"] = null,
                        ["lines"] = new JsonArray(),
                        ["accepted_at"] = now,
                        ["updated_at"] = now,
                        ["completed_at"] = null,
                        ["progress_marker"] = "accepted",
                        ["progress_ordinal"] = 0,
                        ["progress_at"] = now,
                    };
                }

                QueueBackgroundWork(() =>
                {
                    IntegrationTestWait.Delay(CompletedOperationProjectionDelay);
                    var completedAt = DateTimeOffset.UtcNow;
                    lock (gate)
                    {
                        operations[operationId] = new JsonObject
                        {
                            ["operation_id"] = operationId,
                            ["command"] = request["command"]?.GetValue<string>() ?? "sync-state",
                            ["operation_state"] = "completed",
                            ["completed"] = true,
                            ["exit_code"] = 0,
                            ["lines"] = new JsonArray("Recovered accepted operation completed successfully."),
                            ["accepted_at"] = now,
                            ["updated_at"] = completedAt,
                            ["completed_at"] = completedAt,
                            ["progress_marker"] = "completed",
                            ["progress_ordinal"] = 4,
                            ["progress_at"] = completedAt,
                        };
                    }
                });

                IntegrationTestWait.Delay(TimeSpan.FromMilliseconds(initialInvokeDelayMs));
                try
                {
                    WriteJson(context.Response, new JsonObject
                    {
                        ["exit_code"] = 0,
                        ["lines"] = new JsonArray($"Accepted host operation {operationId}."),
                        ["accepted"] = true,
                        ["completed"] = false,
                        ["operation_id"] = operationId,
                        ["operation_state"] = "accepted",
                        ["updated_at"] = now,
                        ["progress_marker"] = "accepted",
                        ["progress_ordinal"] = 0,
                        ["progress_at"] = now,
                    });
                }
                catch
                {
                }

                return;
            }

            if (path.StartsWith("/operations/", StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var operationId = Uri.UnescapeDataString(path["/operations/".Length..]);
                JsonObject? payload;
                lock (gate)
                {
                    payload = operations.TryGetValue(operationId, out var status) ? (JsonObject)status.DeepClone()! : null;
                }

                if (payload is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    return;
                }

                WriteJson(context.Response, payload);
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.Close();
        }

        private void WriteJson(HttpListenerResponse response, JsonObject payload)
        {
            var json = payload.ToJsonString(jsonOptions);
            var buffer = System.Text.Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private static void QueueBackgroundWork(Action work)
        {
            ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), work);
        }
    }
}
