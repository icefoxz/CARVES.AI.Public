using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class HostRoutedWorkerSurfaceTests
{
    [Fact]
    public void TaskRun_WithoutHost_RequiresExplicitHostEnsure()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-DELEGATION-HOST-REQUIRED", scope: ["README.md"]);

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", "T-DELEGATION-HOST-REQUIRED");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Delegated execution requires a resident host", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("host ensure --json", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void HostRoutedTaskRun_UsesDelegatedEnvelopeAndCliBackend()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var timeoutScope = DelegatedExecutionTimeoutScope.Apply(workerRequestTimeoutCapSeconds: 60);
        const string changedPath = "docs/runtime/delegation-host-run.md";
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-DELEGATION-HOST-RUN", scope: [changedPath]);
        ClearRuntimePackSelection(sandbox.RootPath);
        sandbox.WriteDelegatedExecutionHostInvokePolicy(requestTimeoutSeconds: 90);
        sandbox.WriteAiProviderConfig("""
{
  "provider": "codex",
  "enabled": true,
  "model": "gpt-5-codex",
  "base_url": "https://api.openai.com/v1",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "medium",
  "organization": null,
  "project": null
}
""");

        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliScript(changedPath, includeCompletionClaim: false);
        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-delegation-cli");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-delegation-cli", "codex-worker-local-cli");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", "T-DELEGATION-HOST-RUN");

            Assert.True(result.ExitCode == 0, result.CombinedOutput);
            Assert.Contains("\"accepted\": true", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"task_id\": \"T-DELEGATION-HOST-RUN\"", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"backend_id\": \"codex_cli\"", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"summary\":", result.StandardOutput, StringComparison.Ordinal);
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
    public void HostRoutedWorkerDispatchPilot_ProjectsResultIngestionReviewBundleAndClosureDecision()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var timeoutScope = DelegatedExecutionTimeoutScope.Apply(workerRequestTimeoutCapSeconds: 60);
        const string taskId = "T-WORKER-DISPATCH-PILOT";
        const string changedPath = "docs/runtime/worker-dispatch-pilot.md";
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask(taskId, scope: [changedPath]);
        ClearRuntimePackSelection(sandbox.RootPath);
        sandbox.WriteDelegatedExecutionHostInvokePolicy(requestTimeoutSeconds: 90);
        sandbox.WriteAiProviderConfig("""
{
  "provider": "codex",
  "enabled": true,
  "model": "gpt-5-codex",
  "base_url": "https://api.openai.com/v1",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "medium",
  "organization": null,
  "project": null
}
""");

        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliScript(changedPath, includeCompletionClaim: false);
        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-dispatch-pilot");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-dispatch-pilot", "codex-worker-local-cli");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", taskId);
            Assert.True(run.ExitCode == 0, run.CombinedOutput);
            using var runDocument = ParseJsonOutput(run.StandardOutput);
            var executionRunId = runDocument.RootElement.GetProperty("execution_run_id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(executionRunId));
            Assert.Equal("ingested", runDocument.RootElement.GetProperty("result_submission_status").GetString());
            Assert.True(runDocument.RootElement.GetProperty("host_result_ingestion_attempted").GetBoolean());
            Assert.True(runDocument.RootElement.GetProperty("host_result_ingestion_applied").GetBoolean());
            Assert.True(runDocument.RootElement.GetProperty("safety_artifact_present").GetBoolean());
            Assert.Equal("allow", runDocument.RootElement.GetProperty("safety_gate_status").GetString());
            Assert.True(runDocument.RootElement.GetProperty("safety_gate_allowed").GetBoolean());
            Assert.Empty(runDocument.RootElement.GetProperty("safety_gate_issues").EnumerateArray());
            Assert.EndsWith(
                $".ai/execution/{taskId}/result.json",
                runDocument.RootElement.GetProperty("result_envelope_path").GetString(),
                StringComparison.Ordinal);
            Assert.EndsWith(
                "review-submission.json",
                runDocument.RootElement.GetProperty("review_submission_path").GetString(),
                StringComparison.Ordinal);

            var generatedResultPath = Path.Combine(sandbox.RootPath, ".ai", "execution", taskId, "result.json");
            Assert.True(File.Exists(generatedResultPath), $"Expected Host-generated result envelope at {generatedResultPath}.");
            using var generatedResult = JsonDocument.Parse(File.ReadAllText(generatedResultPath));
            Assert.Equal(executionRunId, generatedResult.RootElement.GetProperty("executionRunId").GetString());
            Assert.Equal("success", generatedResult.RootElement.GetProperty("status").GetString());
            Assert.Equal(
                "Host generated delegated result envelope from worker execution packet and evidence.",
                generatedResult.RootElement.GetProperty("telemetry").GetProperty("summary").GetString());

            var apiEvidence = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "worker-dispatch-pilot-evidence", taskId);
            var inspectEvidence = ProgramHarness.Run("--repo-root", sandbox.RootPath, "inspect", "worker-dispatch-pilot-evidence", taskId);

            Assert.True(apiEvidence.ExitCode == 0, apiEvidence.CombinedOutput);
            Assert.True(inspectEvidence.ExitCode == 0, inspectEvidence.CombinedOutput);
            using var evidenceDocument = ParseJsonOutput(apiEvidence.StandardOutput);
            var root = evidenceDocument.RootElement;
            var workerArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-executions", $"{taskId}.json");
            var workerArtifact = File.ReadAllText(workerArtifactPath);
            Assert.Contains("\"source\": \"worker_execution_packet_adapter_generated\"", workerArtifact, StringComparison.Ordinal);
            Assert.Contains("\"packet_validation_status\": \"passed\"", workerArtifact, StringComparison.Ordinal);
            Assert.Equal("worker_dispatch_pilot_evidence", root.GetProperty("kind").GetString());
            Assert.True(root.GetProperty("pilot_chain_complete").GetBoolean(), root.ToString());
            Assert.True(root.GetProperty("readback_only").GetBoolean());
            Assert.False(root.GetProperty("creates_task_queue").GetBoolean());
            Assert.False(root.GetProperty("creates_execution_truth_root").GetBoolean());
            Assert.False(root.GetProperty("writes_task_truth").GetBoolean());
            var callbackReadback = root.GetProperty("schedule_callback_readback");
            Assert.Equal("worker_automation_schedule_callback_readback", callbackReadback.GetProperty("kind").GetString());
            Assert.Equal("evidence_chain_complete", callbackReadback.GetProperty("status").GetString());
            Assert.Equal($"task run {taskId}", callbackReadback.GetProperty("task_run_command").GetString());
            Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", callbackReadback.GetProperty("evidence_command").GetString());
            Assert.True(callbackReadback.GetProperty("run_observed").GetBoolean());
            Assert.Equal(executionRunId, callbackReadback.GetProperty("observed_execution_run_id").GetString());
            Assert.Equal(executionRunId, callbackReadback.GetProperty("result_execution_run_id").GetString());
            Assert.True(callbackReadback.GetProperty("result_matches_known_host_run").GetBoolean());
            Assert.Equal("valid", callbackReadback.GetProperty("host_validation_status").GetString());
            Assert.True(callbackReadback.GetProperty("host_validation_valid").GetBoolean());
            Assert.Equal("valid", callbackReadback.GetProperty("host_validation_reason_code").GetString());
            Assert.True(callbackReadback.GetProperty("evidence_chain_complete").GetBoolean());
            Assert.True(callbackReadback.GetProperty("callback_can_report_run_id").GetBoolean());
            Assert.True(callbackReadback.GetProperty("callback_can_report_closure").GetBoolean());
            Assert.False(callbackReadback.GetProperty("writes_truth").GetBoolean());
            Assert.False(callbackReadback.GetProperty("marks_task_completed").GetBoolean());
            Assert.Empty(callbackReadback.GetProperty("missing_links").EnumerateArray());
            Assert.Equal(executionRunId, root.GetProperty("dispatch").GetProperty("latest_run_id").GetString());
            Assert.EndsWith($".ai/execution/{taskId}/result.json", root.GetProperty("dispatch").GetProperty("latest_run_result_envelope_path").GetString(), StringComparison.Ordinal);
            Assert.True(root.GetProperty("dispatch").GetProperty("host_dispatch_observed").GetBoolean());
            Assert.True(root.GetProperty("worker_evidence").GetProperty("present").GetBoolean());
            Assert.Equal("codex_cli", root.GetProperty("worker_evidence").GetProperty("backend_id").GetString());
            Assert.True(root.GetProperty("safety_gate").GetProperty("safety_artifact_present").GetBoolean());
            Assert.Equal("allow", root.GetProperty("safety_gate").GetProperty("safety_gate_status").GetString());
            Assert.True(root.GetProperty("safety_gate").GetProperty("safety_gate_allowed").GetBoolean());
            Assert.Empty(root.GetProperty("safety_gate").GetProperty("safety_issues").EnumerateArray());
            Assert.True(root.GetProperty("result_ingestion").GetProperty("result_envelope_present").GetBoolean());
            Assert.Equal(executionRunId, root.GetProperty("result_ingestion").GetProperty("result_execution_run_id").GetString());
            Assert.True(root.GetProperty("result_ingestion").GetProperty("result_matches_known_host_run").GetBoolean());
            Assert.True(root.GetProperty("result_ingestion").GetProperty("review_submission_present").GetBoolean());
            Assert.True(root.GetProperty("result_ingestion").GetProperty("effect_ledger_present").GetBoolean());
            var hostValidation = root.GetProperty("host_validation");
            Assert.Equal("worker_dispatch_host_validation", hostValidation.GetProperty("kind").GetString());
            Assert.Equal("valid", hostValidation.GetProperty("status").GetString());
            Assert.True(hostValidation.GetProperty("valid").GetBoolean());
            Assert.Equal("valid", hostValidation.GetProperty("reason_code").GetString());
            Assert.True(hostValidation.GetProperty("writeback_allowed_by_host_validator").GetBoolean());
            Assert.True(hostValidation.GetProperty("worker_claim_is_not_truth").GetBoolean());
            Assert.True(hostValidation.GetProperty("worker_claim_host_validation_required").GetBoolean());
            Assert.Equal("present", hostValidation.GetProperty("completion_claim_status").GetString());
            Assert.Equal("worker_execution_packet_adapter_generated", hostValidation.GetProperty("completion_claim_source").GetString());
            Assert.Equal("passed", hostValidation.GetProperty("completion_claim_packet_validation_status").GetString());
            Assert.Empty(hostValidation.GetProperty("blockers").EnumerateArray());
            Assert.True(root.GetProperty("review").GetProperty("review_artifact_present").GetBoolean());
            Assert.True(root.GetProperty("review").GetProperty("review_bundle_present").GetBoolean());
            Assert.True(root.GetProperty("review").GetProperty("closure_decision_present").GetBoolean());
            Assert.True(root.GetProperty("review").GetProperty("closure_decision").TryGetProperty("decision", out _));
            Assert.Empty(root.GetProperty("completeness").GetProperty("missing_links").EnumerateArray());
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
    public async Task HostRoutedTaskRun_WithActiveRoutingProfile_ProjectsLatestWorkerRoute()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var timeoutScope = DelegatedExecutionTimeoutScope.Apply(workerRequestTimeoutCapSeconds: 60);
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticRoutingPilotTask(
            "T-ACTIVE-ROUTING-PILOT",
            pilotTaskFamily: "analysis.summary",
            routingIntent: "failure_summary",
            moduleId: "Execution/ResultEnvelope",
            scope: [".ai/STATE.md"]);
        ClearRuntimePackSelection(sandbox.RootPath);
        sandbox.WriteDelegatedExecutionHostInvokePolicy(requestTimeoutSeconds: 90);
        var bundledCodexBridgePath = Path.Combine(sandbox.RootPath, "scripts", "codex-worker-bridge", "bridge.mjs");
        if (File.Exists(bundledCodexBridgePath))
        {
            File.Delete(bundledCodexBridgePath);
        }
        sandbox.WriteActiveRoutingProfile(
            profileId: "candidate-current-connected-lanes",
            ruleId: "failure-summary-execution-resultenvelope",
            routingIntent: "failure_summary",
            moduleId: "Execution/ResultEnvelope",
            providerId: "openai",
            backendId: "openai_api",
            routingProfileId: "worker-codegen-fast",
            model: "llama-3.3-70b-versatile",
            fallbackRoutes:
            [
                ("openai", "openai_api", "worker-codegen-fast", "gpt-4.1"),
            ]);
        await using var server = new StubResponsesApiServer("""
{
  "id": "chatcmpl_route_123",
  "model": "llama-3.3-70b-versatile",
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": "Minimal active-routing pilot output."
      }
    }
  ],
  "usage": {
    "prompt_tokens": 17,
    "completion_tokens": 9
  }
}
""");
        sandbox.WriteAiProviderConfig($$"""
{
  "provider": "openai",
  "enabled": true,
  "model": "gpt-4.1",
  "base_url": "{{server.Url}}",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "low",
  "request_family": "chat_completions",
  "organization": null,
  "project": null
}
""");

        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-route-key");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-active-routing");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-active-routing", "worker-codegen-fast");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", "T-ACTIVE-ROUTING-PILOT");
            var inspectTask = ProgramHarness.Run("--repo-root", sandbox.RootPath, "inspect", "task", "T-ACTIVE-ROUTING-PILOT");
            var inspectTrace = ProgramHarness.Run("--repo-root", sandbox.RootPath, "inspect", "execution-trace", "T-ACTIVE-ROUTING-PILOT");

            Assert.Equal(0, run.ExitCode);
            Assert.Equal(0, inspectTask.ExitCode);
            Assert.Equal(0, inspectTrace.ExitCode);

            using var taskDocument = ParseJsonOutput(inspectTask.StandardOutput);
            var latestRoute = taskDocument.RootElement.GetProperty("latest_worker_route");
            Assert.Equal("openai_api", latestRoute.GetProperty("backend_id").GetString());
            Assert.Equal("openai", latestRoute.GetProperty("provider_id").GetString());
            Assert.Equal("llama-3.3-70b-versatile", latestRoute.GetProperty("selected_model").GetString());
            Assert.Equal("llama-3.3-70b-versatile", latestRoute.GetProperty("model").GetString());
            Assert.Equal("candidate-current-connected-lanes", latestRoute.GetProperty("active_routing_profile_id").GetString());
            Assert.Equal("worker-codegen-fast", latestRoute.GetProperty("selected_routing_profile_id").GetString());
            Assert.Equal("failure-summary-execution-resultenvelope", latestRoute.GetProperty("routing_rule_id").GetString());
            Assert.Equal("failure_summary", latestRoute.GetProperty("routing_intent").GetString());
            Assert.Equal("Execution/ResultEnvelope", latestRoute.GetProperty("routing_module_id").GetString());
            Assert.Equal("active_profile_preferred", latestRoute.GetProperty("route_source").GetString());
            Assert.Equal("openai_compatible", latestRoute.GetProperty("protocol_family").GetString());
            Assert.Equal("chat_completions", latestRoute.GetProperty("request_family").GetString());

            using var traceDocument = ParseJsonOutput(inspectTrace.StandardOutput);
            var traceRoute = traceDocument.RootElement.GetProperty("route");
            Assert.Equal("openai_api", traceRoute.GetProperty("backend_id").GetString());
            Assert.Equal("openai", traceRoute.GetProperty("provider_id").GetString());
            Assert.Equal("llama-3.3-70b-versatile", traceRoute.GetProperty("model").GetString());
            Assert.Equal("chat_completions", traceRoute.GetProperty("request_family").GetString());
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void AgentGateway_RunTask_ReturnsDelegatedExecutionEnvelope()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var timeoutScope = DelegatedExecutionTimeoutScope.Apply(
            workerRequestTimeoutCapSeconds: 60,
            agentRequestTimeoutCapSeconds: 90);
        const string changedPath = "docs/runtime/delegation-agent-run.md";
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-DELEGATION-AGENT-RUN", scope: [changedPath]);
        ClearRuntimePackSelection(sandbox.RootPath);
        sandbox.WriteAiProviderConfig("""
{
  "provider": "codex",
  "enabled": true,
  "model": "gpt-5-codex",
  "base_url": "https://api.openai.com/v1",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "medium",
  "organization": null,
  "project": null
}
""");

        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliScript(changedPath);
        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-agent-delegation");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-agent-delegation", "codex-worker-local-cli");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "request", "run_task", "T-DELEGATION-AGENT-RUN");

            Assert.True(result.ExitCode == 0, result.CombinedOutput);
            Assert.Contains("\"accepted\": true", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"outcome\": \"run_task\"", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"backend_id\": \"codex_cli\"", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"task_id\": \"T-DELEGATION-AGENT-RUN\"", result.StandardOutput, StringComparison.Ordinal);
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

    private static JsonDocument ParseJsonOutput(string output)
    {
        var index = output.IndexOf('{');
        return index >= 0
            ? JsonDocument.Parse(output[index..])
            : throw new InvalidOperationException($"Command output did not contain JSON: {output}");
    }

    private static void ClearRuntimePackSelection(string repoRoot)
    {
        var selectionRoot = Path.Combine(repoRoot, ".ai", "artifacts", "runtime-pack-selection");
        if (Directory.Exists(selectionRoot))
        {
            Directory.Delete(selectionRoot, recursive: true);
        }
    }

    private static string CreateCodexCliScript(
        string changedPath = "README.md",
        bool includeCompletionClaim = true)
    {
        var agentMessage = includeCompletionClaim
            ? "- changed_files: __CHANGED_PATH__\\n- contract_items_satisfied: patch_scope_recorded; scope_hygiene\\n- tests_run: dotnet test\\n- evidence_paths: worker execution artifact\\n- known_limitations: none\\n- next_recommendation: submit for Host review"
            : "Completed scoped edits for __CHANGED_PATH__.";
        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(Path.GetTempPath(), $"carves-codex-cli-{Guid.NewGuid():N}.cmd");
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
  echo {"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"__CHANGED_PATH__","kind":"modify"}]}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"__AGENT_MESSAGE__"}}
  echo {"type":"turn.completed","usage":{"input_tokens":13,"output_tokens":6}}
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
"""
                .Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal)
                .Replace("__AGENT_MESSAGE__", agentMessage.Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal), StringComparison.Ordinal));
            return path;
        }

        var scriptPath = Path.Combine(Path.GetTempPath(), $"carves-codex-cli-{Guid.NewGuid():N}.sh");
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
  echo '{"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"__CHANGED_PATH__","kind":"modify"}]}}'
  printf '%s\n' '{"type":"item.completed","item":{"type":"agent_message","text":"__AGENT_MESSAGE__"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":13,"output_tokens":6}}'
  exit 0
fi
echo unsupported >&2
exit 1
"""
            .Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal)
            .Replace("__AGENT_MESSAGE__", agentMessage.Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal), StringComparison.Ordinal));
        File.SetUnixFileMode(scriptPath, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return scriptPath;
    }

    private static void StopHost(string repoRoot)
    {
        ProgramHarness.Run("--repo-root", repoRoot, "host", "stop", "integration cleanup");
    }
}
