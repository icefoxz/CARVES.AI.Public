using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.IntegrationTests;

public sealed class PermissionApprovalFlowTests
{
    [Fact]
    public void PermissionApprovalWait_PersistsArtifactAuditAndOperatorSurface()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-PERMISSION-WAIT", scope: ["README.md"]);
        sandbox.SetTaskMetadata("T-PERMISSION-WAIT", "worker_backend", "codex_sdk");
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
        sandbox.WriteWorkerOperationalPolicy(CodexSdkWorkerOperationalPolicyJson);
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-permission-wait");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-permission-wait", "codex-worker-trusted");

        var bridgeScript = CreateApprovalBridgeScript();
        var originalBridge = Environment.GetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", bridgeScript);

            var selection = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "select", "repo-permission-wait");
            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");
            var approvals = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "approvals");
            var apiRequests = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "worker-permissions");
            var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "inspect", "T-PERMISSION-WAIT");

            var permissionArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-permissions", "T-PERMISSION-WAIT.json");
            var permissionArtifact = JsonNode.Parse(File.ReadAllText(permissionArtifactPath))!.AsObject();
            var requestId = RequireFirstPermissionRequestId(permissionArtifact, selection, run, approvals, apiRequests, inspect);
            var auditPath = ControlPlanePaths.FromRepoRoot(sandbox.RootPath).PlatformPermissionAuditRuntimeFile;
            var sessionPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json");

            Assert.NotEqual(0, run.ExitCode);
            Assert.True(File.Exists(permissionArtifactPath));
            Assert.True(File.Exists(auditPath));
            Assert.Contains("Pending permission requests:", approvals.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(requestId, apiRequests.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("filesystem_write", apiRequests.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"status\": \"ApprovalWait\"", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(requestId, inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"status\": \"approval_wait\"", File.ReadAllText(sessionPath), StringComparison.Ordinal);
            Assert.Contains(requestId, File.ReadAllText(auditPath), StringComparison.Ordinal);
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
    public void WorkerApprove_ReturnsTaskToPendingAndClearsApprovalWait()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-PERMISSION-APPROVE", scope: ["README.md"]);
        sandbox.SetTaskMetadata("T-PERMISSION-APPROVE", "worker_backend", "codex_sdk");
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
        sandbox.WriteWorkerOperationalPolicy(CodexSdkWorkerOperationalPolicyJson);
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-permission-approve");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-permission-approve", "codex-worker-trusted");

        var bridgeScript = CreateApprovalBridgeScript();
        var originalBridge = Environment.GetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", bridgeScript);
            var selection = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "select", "repo-permission-approve");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");

            var permissionArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-permissions", "T-PERMISSION-APPROVE.json");
            var permissionArtifact = JsonNode.Parse(File.ReadAllText(permissionArtifactPath))!.AsObject();
            var requestId = RequireFirstPermissionRequestId(permissionArtifact, selection);

            var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "approve", requestId, "integration-test");
            var taskPath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-PERMISSION-APPROVE.json");
            var sessionPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json");
            var approvals = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "approvals");

            Assert.Equal(0, approve.ExitCode);
            Assert.Contains("Approved permission request", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"status\": \"pending\"", File.ReadAllText(taskPath), StringComparison.Ordinal);
            Assert.Contains("\"status\": \"idle\"", File.ReadAllText(sessionPath), StringComparison.Ordinal);
            Assert.Contains("Pending permission requests: 0", approvals.StandardOutput, StringComparison.Ordinal);
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
    public void WorkerTimeout_BlocksTaskAndWritesAuditDecision()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-PERMISSION-TIMEOUT", scope: ["README.md"]);
        sandbox.SetTaskMetadata("T-PERMISSION-TIMEOUT", "worker_backend", "codex_sdk");
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
        sandbox.WriteWorkerOperationalPolicy(CodexSdkWorkerOperationalPolicyJson);
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-permission-timeout");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-permission-timeout", "codex-worker-trusted");

        var bridgeScript = CreateApprovalBridgeScript();
        var originalBridge = Environment.GetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", bridgeScript);
            var selection = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "select", "repo-permission-timeout");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");

            var permissionArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-permissions", "T-PERMISSION-TIMEOUT.json");
            var permissionArtifact = JsonNode.Parse(File.ReadAllText(permissionArtifactPath))!.AsObject();
            var requestId = RequireFirstPermissionRequestId(permissionArtifact, selection);

            var timeout = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "timeout", requestId, "integration-test");
            var audit = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "audit", "--request-id", requestId);
            var taskPath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-PERMISSION-TIMEOUT.json");
            var auditPath = ControlPlanePaths.FromRepoRoot(sandbox.RootPath).PlatformPermissionAuditRuntimeFile;

            Assert.Equal(0, timeout.ExitCode);
            Assert.Contains("Timed out permission request", timeout.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"status\": \"blocked\"", File.ReadAllText(taskPath), StringComparison.Ordinal);
            Assert.Contains("TimedOut", audit.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("timed_out", File.ReadAllText(auditPath), StringComparison.Ordinal);
            Assert.Contains(requestId, File.ReadAllText(auditPath), StringComparison.Ordinal);
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
    public void OperationalSummaryAndGovernanceReport_ProjectPermissionAndIncidentTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-PERMISSION-REPORT", scope: ["README.md"]);
        sandbox.SetTaskMetadata("T-PERMISSION-REPORT", "worker_backend", "codex_sdk");
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
        sandbox.WriteWorkerOperationalPolicy(CodexSdkWorkerOperationalPolicyJson);
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-permission-report");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-permission-report", "codex-worker-trusted");

        var bridgeScript = CreateApprovalBridgeScript();
        var originalBridge = Environment.GetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", bridgeScript);
            var selection = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "select", "repo-permission-report");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");

            var permissionArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-permissions", "T-PERMISSION-REPORT.json");
            var permissionArtifact = JsonNode.Parse(File.ReadAllText(permissionArtifactPath))!.AsObject();
            var requestId = RequireFirstPermissionRequestId(permissionArtifact, selection);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "timeout", requestId, "integration-test");

            var operational = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "summary");
            var operationalApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "operational-summary");
            var governance = ProgramHarness.Run("--repo-root", sandbox.RootPath, "governance", "report", "--hours", "24");
            var governanceApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "governance-report", "--hours", "24");

            Assert.Equal(0, operational.ExitCode);
            Assert.Contains("Operational summary", operational.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Pending approvals:", operational.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Blocked tasks:", operational.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, operationalApi.ExitCode);
            Assert.Contains("\"blocked_task_count\":", operationalApi.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"approval_queue\":", operationalApi.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, governance.ExitCode);
            Assert.Contains("Governance report window: last 24h", governance.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Approval decisions:", governance.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Permission-blocked task classes:", governance.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Repo incident density:", governance.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, governanceApi.ExitCode);
            Assert.Contains("\"window_hours\": 24", governanceApi.StandardOutput, StringComparison.Ordinal);
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

    private static string CreateApprovalBridgeScript()
    {
        var path = Path.Combine(Path.GetTempPath(), $"carves-codex-approval-{Guid.NewGuid():N}.mjs");
        File.WriteAllText(path, """
import process from "node:process";

const chunks = [];
for await (const chunk of process.stdin) {
  chunks.push(chunk);
}

const request = JSON.parse(Buffer.concat(chunks.map((chunk) => Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk))).toString("utf8").replace(/^\uFEFF/, ""));
const now = new Date().toISOString();
process.stdout.write(JSON.stringify({
  runId: "stub-codex-approval",
  requestId: request.requestId,
  status: "approval_wait",
  failureKind: "approval_required",
  retryable: false,
  summary: "worker is waiting for permission approval",
  rationale: "The worker requested permission before writing a file.",
  failureReason: "Permission approval is required before writing README.md.",
  model: request.model || "gpt-5-codex",
  changedFiles: [],
  events: [
    {
      runId: "stub-codex-approval",
      taskId: request.taskId,
      eventType: "permission_requested",
      summary: "Permission required to write README.md",
      itemType: "approval",
      filePath: "README.md",
      rawPayload: "Permission required to write README.md",
      attributes: {
        permission_kind: "filesystem_write",
        scope: "workspace"
      },
      occurredAt: now
    },
    {
      runId: "stub-codex-approval",
      taskId: request.taskId,
      eventType: "approval_wait",
      summary: "Awaiting operator approval",
      itemType: "approval",
      rawPayload: "Permission required before continuing.",
      attributes: {
        permission_kind: "filesystem_write"
      },
      occurredAt: now
    }
  ],
  commandTrace: [],
  startedAt: now,
  completedAt: now,
  inputTokens: 11,
  outputTokens: 7
}));
""");
        return path;
    }

    private static string RequireFirstPermissionRequestId(
        JsonObject permissionArtifact,
        params ProgramHarness.ProgramRunResult[] diagnostics)
    {
        var requests = permissionArtifact["requests"]?.AsArray();
        Assert.True(requests is not null && requests.Count > 0, BuildPermissionArtifactFailure(permissionArtifact, diagnostics));
        return requests[0]!["permission_request_id"]!.GetValue<string>();
    }

    private static string BuildPermissionArtifactFailure(
        JsonObject permissionArtifact,
        IReadOnlyList<ProgramHarness.ProgramRunResult> diagnostics)
    {
        var sections = diagnostics
            .Select((result, index) =>
                $"diagnostic[{index}] exit={result.ExitCode}{Environment.NewLine}stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{result.StandardError}")
            .ToArray();
        return $"Permission artifact did not contain any requests.{Environment.NewLine}artifact:{Environment.NewLine}{permissionArtifact.ToJsonString(new() { WriteIndented = true })}{Environment.NewLine}{string.Join(Environment.NewLine + Environment.NewLine, sections)}";
    }

    private const string CodexSdkWorkerOperationalPolicyJson = """
{
  "version": "1.0",
  "preferred_backend_id": "codex_sdk",
  "preferred_trust_profile_id": "workspace_build_test",
  "approval": {
    "outside_workspace_requires_review": true,
    "high_risk_requires_review": true,
    "manual_approval_mode_requires_review": true,
    "auto_allow_categories": ["filesystem_write", "process_control"],
    "auto_deny_categories": ["secret_access", "elevated_privilege", "system_configuration"],
    "force_review_categories": ["filesystem_delete", "outside_workspace_access", "network_access", "unknown_permission_request"]
  },
  "recovery": {
    "max_retry_count": 2,
    "transient_infra_backoff_seconds": 5,
    "timeout_backoff_seconds": 10,
    "invalid_output_backoff_seconds": 3,
    "environment_rebuild_backoff_seconds": 5,
    "switch_provider_on_environment_blocked": true,
    "switch_provider_on_unavailable_backend": true
  },
  "observability": {
    "provider_degraded_latency_ms": 1500,
    "approval_queue_preview_limit": 8,
    "blocked_queue_preview_limit": 8,
    "incident_preview_limit": 10,
    "governance_report_default_hours": 24
  }
}
""";
}
