using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class WorkerAdapterSurfaceTests
{
    [Fact]
    public async Task ClaudeWorkerAdapter_ProducesAuditableProviderArtifact()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-CLAUDE-ADAPTER", scope: [".ai/STATE.md"]);
        ClearRuntimePackSelection(sandbox.RootPath);
        WriteWorkerSelectionPolicy(sandbox.RootPath, "claude_api", ["claude_api", "null_worker"]);
        sandbox.SetTaskMetadata("T-CLAUDE-ADAPTER", "worker_backend", "claude_api");
        sandbox.SetTaskMetadata("T-CLAUDE-ADAPTER", "routing_intent", "review_summary");
        sandbox.SetTaskMetadata("T-CLAUDE-ADAPTER", "module_id", ".ai/STATE.md");
        await using var server = new StubResponsesApiServer("""
{
  "id": "msg_product_123",
  "model": "claude-sonnet-4-5",
  "content": [
    {
      "type": "text",
      "text": "scope: tests only\nrisks: none\nvalidation: dotnet test"
    }
  ],
  "usage": {
    "input_tokens": 7,
    "output_tokens": 11
  }
}
""");
        sandbox.WriteAiProviderConfig($$"""
{
  "provider": "claude",
  "enabled": true,
  "model": "claude-sonnet-4-5",
  "base_url": "{{server.Url}}",
  "api_key_environment_variable": "ANTHROPIC_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "low",
  "organization": null,
  "project": null
}
""");

        var originalApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-anthropic-key");
        try
        {
            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");
            var explain = ProgramHarness.Run("--repo-root", sandbox.RootPath, "explain-task", "T-CLAUDE-ADAPTER");
            var providerArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "provider", "T-CLAUDE-ADAPTER.json");

            Assert.Equal(0, run.ExitCode);
            Assert.Equal(0, explain.ExitCode);
            Assert.Contains("\"worker_adapter\": \"ClaudeWorkerAdapter\"", File.ReadAllText(providerArtifactPath), StringComparison.Ordinal);
            Assert.Contains("\"provider\": \"claude\"", File.ReadAllText(providerArtifactPath), StringComparison.Ordinal);
            Assert.Contains("Worker adapter:", explain.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("ClaudeWorkerAdapter", explain.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public async Task GeminiWorkerAdapter_ProducesAuditableProviderArtifactThroughGeneralizedBoundary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-GEMINI-ADAPTER", scope: [".ai/STATE.md"]);
        ClearRuntimePackSelection(sandbox.RootPath);
        WriteWorkerSelectionPolicy(sandbox.RootPath, "gemini_api", ["gemini_api", "null_worker"]);
        sandbox.SetTaskMetadata("T-GEMINI-ADAPTER", "worker_backend", "gemini_api");
        sandbox.SetTaskMetadata("T-GEMINI-ADAPTER", "routing_intent", "structured_output");
        sandbox.SetTaskMetadata("T-GEMINI-ADAPTER", "module_id", ".ai/STATE.md");
        await using var server = new StubResponsesApiServer("""
{
  "candidates": [
    {
      "content": {
        "parts": [
          { "text": "{\"summary\":\"Gemini structured output\"}" }
        ]
      }
    }
  ],
  "usageMetadata": {
    "promptTokenCount": 10,
    "candidatesTokenCount": 12
  },
  "modelVersion": "gemini-2.5-pro"
}
""");
        sandbox.WriteAiProviderConfig($$"""
{
  "provider": "gemini",
  "enabled": true,
  "model": "gemini-2.5-pro",
  "base_url": "{{server.Url}}",
  "api_key_environment_variable": "GEMINI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "low",
  "organization": null,
  "project": null
}
""");

        var originalApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-gemini-key");
        try
        {
            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");
            var explain = ProgramHarness.Run("--repo-root", sandbox.RootPath, "explain-task", "T-GEMINI-ADAPTER");
            var inspectQualification = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-remote-worker-qualification");
            var providerArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "provider", "T-GEMINI-ADAPTER.json");

            Assert.Equal(0, run.ExitCode);
            Assert.Equal(0, explain.ExitCode);
            Assert.Equal(0, inspectQualification.ExitCode);
            Assert.Contains("\"worker_adapter\": \"GeminiWorkerAdapter\"", File.ReadAllText(providerArtifactPath), StringComparison.Ordinal);
            Assert.Contains("\"provider\": \"gemini\"", File.ReadAllText(providerArtifactPath), StringComparison.Ordinal);
            Assert.Contains("gemini/gemini_api", inspectQualification.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public async Task RealWorkerAdapter_ProviderFailureWritesRuntimeFailureArtifact()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-PROVIDER-FAIL");
        ClearRuntimePackSelection(sandbox.RootPath);
        WriteWorkerSelectionPolicy(sandbox.RootPath, "openai_api", ["openai_api", "null_worker"]);
        sandbox.SetTaskMetadata("T-PROVIDER-FAIL", "worker_backend", "openai_api");
        await using var server = new StubResponsesApiServer("""
{
  "error": "failure"
}
""", statusCode: 500);
        sandbox.WriteAiProviderConfig($$"""
{
  "provider": "openai",
  "enabled": true,
  "model": "gpt-5-mini",
  "base_url": "{{server.Url}}",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "low",
  "organization": null,
  "project": null
}
""");

        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        try
        {
            var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");
            var runtimeFailurePath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "last_failure.json");

            Assert.NotEqual(0, result.ExitCode);
            Assert.True(File.Exists(runtimeFailurePath));
            Assert.Contains("OpenAI Responses API returned 500", File.ReadAllText(runtimeFailurePath), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void CodexWorkerAdapter_UsesBridgeScriptAndPersistsWorkerExecutionArtifact()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-CODEX-WORKER", scope: ["README.md"]);
        ClearRuntimePackSelection(sandbox.RootPath);
        WriteWorkerSelectionPolicy(sandbox.RootPath, "codex_sdk", ["codex_sdk", "null_worker"]);
        sandbox.SetTaskMetadata("T-CODEX-WORKER", "worker_backend", "codex_sdk");
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
        var bridgeScript = CreateBridgeScript();
        var originalBridge = Environment.GetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", bridgeScript);
            sandbox.SetTaskMetadata("T-CODEX-WORKER", "codex_resume_thread_id", "stub-codex-thread");
            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");
            var explain = ProgramHarness.Run("--repo-root", sandbox.RootPath, "explain-task", "T-CODEX-WORKER");
            var workerArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-executions", "T-CODEX-WORKER.json");

            Assert.Equal(0, run.ExitCode);
            Assert.True(File.Exists(workerArtifactPath));
            Assert.Contains("\"backend_id\": \"codex_sdk\"", File.ReadAllText(workerArtifactPath), StringComparison.Ordinal);
            Assert.Contains("Worker execution:", explain.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("codex_sdk", explain.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("thread id: stub-codex-thread", explain.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("thread continuity: ResumedThread", explain.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stub codex success", explain.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
            Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", originalBridge);
            if (File.Exists(bridgeScript))
            {
                File.Delete(bridgeScript);
            }
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_UsesLocalCliAndPersistsWorkerExecutionArtifact()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-CODEX-CLI-WORKER", scope: ["README.md"]);
        ClearRuntimePackSelection(sandbox.RootPath);
        WriteWorkerSelectionPolicy(sandbox.RootPath, "codex_cli", ["codex_cli", "null_worker"]);
        sandbox.SetTaskMetadata("T-CODEX-CLI-WORKER", "worker_backend", "codex_cli");
        WriteAcceptanceContract(sandbox.RootPath, "T-CODEX-CLI-WORKER", evidenceTypes: ["result_commit"]);
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
        var cliPath = CreateCodexCliScript(includeCompletionClaim: false);
        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-codex-cli");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-codex-cli", "codex-worker-local-cli");

            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");
            var inspectTask = ProgramHarness.Run("--repo-root", sandbox.RootPath, "inspect", "task", "T-CODEX-CLI-WORKER");
            var inspectTrace = ProgramHarness.Run("--repo-root", sandbox.RootPath, "inspect", "execution-trace", "T-CODEX-CLI-WORKER");
            var workerArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-executions", "T-CODEX-CLI-WORKER.json");
            var workerArtifact = File.ReadAllText(workerArtifactPath);

            Assert.Equal(0, run.ExitCode);
            Assert.Equal(0, inspectTask.ExitCode);
            Assert.Equal(0, inspectTrace.ExitCode);
            Assert.True(File.Exists(workerArtifactPath));
            Assert.Contains("\"backend_id\": \"codex_cli\"", workerArtifact, StringComparison.Ordinal);
            Assert.Contains("contract_items_satisfied", workerArtifact, StringComparison.Ordinal);
            Assert.Contains("\"source\": \"worker_execution_packet_adapter_generated\"", workerArtifact, StringComparison.Ordinal);
            Assert.Contains("\"status\": \"present\"", workerArtifact, StringComparison.Ordinal);
            Assert.Contains($".ai/execution/T-CODEX-CLI-WORKER/result.json", workerArtifact, StringComparison.Ordinal);
            Assert.Contains(".ai/artifacts/worker-executions/", workerArtifact, StringComparison.Ordinal);

            using var taskDocument = ParseJsonOutput(inspectTask.StandardOutput);
            var latestRoute = taskDocument.RootElement.GetProperty("latest_worker_route");
            Assert.Equal(120, latestRoute.GetProperty("request_timeout_seconds").GetInt32());
            Assert.Contains("Selected 120s delegated request budget", latestRoute.GetProperty("request_budget_summary").GetString(), StringComparison.Ordinal);
            var latestRouteContract = latestRoute.GetProperty("acceptance_contract");
            Assert.Equal("AC-T-CODEX-CLI-WORKER", latestRouteContract.GetProperty("contract_id").GetString());
            Assert.Equal("Compiled", latestRouteContract.GetProperty("status").GetString());
            Assert.True(latestRouteContract.GetProperty("human_review_required").GetBoolean());
            Assert.False(latestRouteContract.GetProperty("provisional_allowed").GetBoolean());
            Assert.Contains(
                latestRouteContract.GetProperty("evidence_required").EnumerateArray().Select(item => item.GetString()),
                value => string.Equals(value, "result_commit", StringComparison.Ordinal));

            using var traceDocument = ParseJsonOutput(inspectTrace.StandardOutput);
            var requestBudget = traceDocument.RootElement.GetProperty("request_budget");
            Assert.Equal("runtime_governed_dynamic_request_budget_v1", requestBudget.GetProperty("policy_id").GetString());
            Assert.Equal(120, requestBudget.GetProperty("timeout_seconds").GetInt32());
            Assert.True(requestBudget.GetProperty("repo_truth_guidance_required").GetBoolean());
            var traceRouteContract = traceDocument.RootElement.GetProperty("route").GetProperty("acceptance_contract");
            Assert.Equal("AC-T-CODEX-CLI-WORKER", traceRouteContract.GetProperty("contract_id").GetString());
            Assert.Contains(
                traceRouteContract.GetProperty("decisions").EnumerateArray().Select(item => item.GetString()),
                value => string.Equals(value, "Accept", StringComparison.Ordinal));
        }
        finally
        {
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

    private static void WriteAcceptanceContract(
        string repoRoot,
        string taskId,
        IReadOnlyList<string>? evidenceTypes = null)
    {
        var nodePath = Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"{taskId}.json");
        var taskNode = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        var evidenceRequired = new JsonArray();
        foreach (var evidenceType in evidenceTypes ?? Array.Empty<string>())
        {
            evidenceRequired.Add(new JsonObject
            {
                ["type"] = evidenceType,
                ["description"] = $"Product-shell integration proof requires {evidenceType}.",
            });
        }

        taskNode["acceptance_contract"] = new JsonObject
        {
            ["contract_id"] = $"AC-{taskId}",
            ["title"] = $"Acceptance contract for {taskId}",
            ["status"] = "compiled",
            ["owner"] = "planner",
            ["created_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["intent"] = new JsonObject
            {
                ["goal"] = "Project acceptance contract binding into delegated worker inspect surfaces.",
                ["business_value"] = "Runtime should expose the exact contract used by the worker run.",
            },
            ["acceptance_examples"] = new JsonArray(),
            ["checks"] = new JsonObject
            {
                ["unit_tests"] = new JsonArray(),
                ["integration_tests"] = new JsonArray(),
                ["regression_tests"] = new JsonArray(),
                ["policy_checks"] = new JsonArray(),
                ["additional_checks"] = new JsonArray(),
            },
            ["constraints"] = new JsonObject
            {
                ["must_not"] = new JsonArray(),
                ["architecture"] = new JsonArray(),
                ["scope_limit"] = null,
            },
            ["non_goals"] = new JsonArray("Do not hide worker-bound acceptance semantics."),
            ["evidence_required"] = evidenceRequired,
            ["human_review"] = new JsonObject
            {
                ["required"] = true,
                ["provisional_allowed"] = false,
                ["decisions"] = new JsonArray("accept", "reject", "reopen"),
            },
            ["traceability"] = new JsonObject
            {
                ["source_card_id"] = "CARD-INTEGRATION",
                ["source_task_id"] = taskId,
                ["derived_task_ids"] = new JsonArray(),
                ["related_artifacts"] = new JsonArray(),
            },
        };
        File.WriteAllText(nodePath, taskNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ClearRuntimePackSelection(string repoRoot)
    {
        var selectionRoot = Path.Combine(repoRoot, ".ai", "artifacts", "runtime-pack-selection");
        if (Directory.Exists(selectionRoot))
        {
            Directory.Delete(selectionRoot, recursive: true);
        }
    }

    private static void WriteWorkerSelectionPolicy(
        string repoRoot,
        string preferredBackendId,
        IReadOnlyList<string> allowedBackendIds)
    {
        var policyPath = Path.Combine(repoRoot, ".carves-platform", "policies", "worker-selection.policy.json");
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllText(
            policyPath,
            new JsonObject
            {
                ["version"] = "1.0",
                ["preferred_backend_id"] = preferredBackendId,
                ["default_trust_profile_id"] = "workspace_build_test",
                ["allow_routing_fallback"] = true,
                ["fallback_backend_ids"] = new JsonArray("null_worker"),
                ["allowed_backend_ids"] = new JsonArray(allowedBackendIds.Select(item => (JsonNode?)JsonValue.Create(item)).ToArray()),
            }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string CreateBridgeScript()
    {
        var path = Path.Combine(Path.GetTempPath(), $"carves-codex-bridge-{Guid.NewGuid():N}.mjs");
        File.WriteAllText(path, """
import process from "node:process";

const chunks = [];
for await (const chunk of process.stdin) {
  chunks.push(chunk);
}

const request = JSON.parse(Buffer.concat(chunks.map((chunk) => Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk))).toString("utf8").replace(/^\uFEFF/, ""));
const now = new Date().toISOString();
process.stdout.write(JSON.stringify({
  runId: "stub-codex-run",
  requestId: request.requestId,
  requestedPriorThreadId: request.priorThreadId ?? null,
  threadId: request.priorThreadId ?? "stub-codex-thread",
  threadContinuity: request.priorThreadId ? "resumed_thread" : "new_thread",
  status: "succeeded",
  failureKind: "none",
  retryable: false,
  summary: "stub codex success",
  rationale: "Stub bridge completed the worker turn.",
  failureReason: null,
  model: request.model || "gpt-5-codex",
  changedFiles: ["README.md"],
  events: [
    {
      runId: "stub-codex-run",
      taskId: request.taskId,
      eventType: "run_started",
      summary: "Stub bridge started.",
      itemType: "bridge",
      attributes: {},
      occurredAt: now
    },
    {
      runId: "stub-codex-run",
      taskId: request.taskId,
      eventType: "final_summary",
      summary: "stub codex success",
      itemType: "bridge",
      rawPayload: "Stub bridge completed the worker turn.",
      attributes: {},
      occurredAt: now
    }
  ],
  commandTrace: [
    {
      command: ["node", "-e", "console.log('stub')"],
      exitCode: 0,
      standardOutput: "stub",
      standardError: "",
      workingDirectory: request.worktreeRoot,
      category: "worker",
      capturedAt: now
    }
  ],
  startedAt: now,
  completedAt: now,
  inputTokens: 11,
  outputTokens: 7
}));
""");
        return path;
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
}
