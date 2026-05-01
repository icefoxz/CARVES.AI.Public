using System.Text.Json.Nodes;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeConsistencyCommandTests
{
    [Fact]
    public void VerifyRuntime_DetectsDelegatedRunDriftFromCurrentTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.AddSyntheticPendingTask("T-DRIFT-VERIFY-001");
        RewriteTaskNodeForDrift(sandbox.RootPath, "T-DRIFT-VERIFY-001");
        WriteWorkerExecutionArtifact(sandbox.RootPath, "T-DRIFT-VERIFY-001");
        WriteExpiredLease(sandbox.RootPath, "T-DRIFT-VERIFY-001");

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "verify", "runtime");
        var output = result.CombinedOutput;

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Runtime consistency check", output, StringComparison.Ordinal);
        Assert.Contains("expired_run_collapsed_to_pending", output, StringComparison.Ordinal);
        Assert.Contains("T-DRIFT-VERIFY-001", output, StringComparison.Ordinal);
        Assert.Contains(".carves-platform\\runtime-state\\workers\\leases.json", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReconcileRuntime_ConvertsExpiredPendingRunIntoExplicitRecoveryState()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.AddSyntheticPendingTask("T-DRIFT-RECON-001");
        RewriteTaskNodeForDrift(sandbox.RootPath, "T-DRIFT-RECON-001");
        WriteWorkerExecutionArtifact(sandbox.RootPath, "T-DRIFT-RECON-001", retryable: false, failureKind: "task_logic_failed");
        WriteExpiredLease(sandbox.RootPath, "T-DRIFT-RECON-001");
        WriteWorktreeRuntimeState(sandbox.RootPath, "T-DRIFT-RECON-001");

        var reconcile = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "reconcile", "runtime");
        var sync = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "sync-state");
        var taskNode = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-DRIFT-RECON-001.json")))!.AsObject();

        Assert.Equal(1, reconcile.ExitCode);
        Assert.Contains("T-DRIFT-RECON-001: state=Quarantined", reconcile.CombinedOutput, StringComparison.Ordinal);
        Assert.True(sync.ExitCode == 0, sync.CombinedOutput);
        Assert.Equal("blocked", taskNode["status"]?.GetValue<string>());
    }

    [Fact]
    public void SyncState_SupersedesHistoricalGhostBlockedShapeTaskWhenDownstreamWorkCompleted()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticTask("T-GHOST-001", "blocked", "Shape interfaces for Synthetic Historical Card");
        sandbox.AddSyntheticTask("T-GHOST-002", "completed", "Implement Synthetic Historical Card", dependencies: ["T-GHOST-001"]);
        sandbox.AddSyntheticTask("T-GHOST-003", "completed", "Validate Synthetic Historical Card", dependencies: ["T-GHOST-002"]);
        RewriteSyntheticGhostTaskForReconciliation(sandbox.RootPath, "T-GHOST-001", "T-GHOST-002", "T-GHOST-003");

        var sync = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "sync-state");
        var taskNode = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-GHOST-001.json")))!.AsObject();

        Assert.True(sync.ExitCode == 0, sync.CombinedOutput);
        Assert.Contains("Superseded 1 historical ghost blocker task(s): T-GHOST-001", sync.CombinedOutput, StringComparison.Ordinal);
        Assert.Equal("superseded", taskNode["status"]?.GetValue<string>());
        Assert.Equal("complete", taskNode["planner_review"]?["verdict"]?.GetValue<string>());
    }

    private static void RewriteTaskNodeForDrift(string repoRoot, string taskId)
    {
        var nodePath = Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"{taskId}.json");
        var node = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        node["last_worker_run_id"] = "run-verify-001";
        node["last_worker_backend"] = "codex_cli";
        node["last_worker_failure_kind"] = "invalid_output";
        node["last_worker_retryable"] = true;
        node["last_worker_summary"] = "Synthetic verify runtime worker failure.";
        node["last_recovery_action"] = "retry";
        node["last_recovery_reason"] = "Retryable worker failure.";
        node["retry_not_before"] = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O");
        File.WriteAllText(nodePath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteWorkerExecutionArtifact(string repoRoot, string taskId, bool retryable = true, string failureKind = "invalid_output")
    {
        var artifactPath = Path.Combine(repoRoot, ".ai", "artifacts", "worker-executions", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
        File.WriteAllText(artifactPath, $$"""
{
  "schema_version": 1,
  "captured_at": "{{DateTimeOffset.UtcNow:O}}",
  "task_id": "{{taskId}}",
  "result": {
    "schema_version": 1,
    "run_id": "run-verify-001",
    "task_id": "{{taskId}}",
    "backend_id": "codex_cli",
    "provider_id": "codex",
    "adapter_id": "CodexCliWorkerAdapter",
    "adapter_reason": "Synthetic integration adapter",
    "profile_id": "extended_dev_ops",
    "trusted_profile": true,
    "status": "failed",
    "failure_kind": "{{failureKind}}",
    "retryable": {{retryable.ToString().ToLowerInvariant()}},
    "configured": true,
    "model": "gpt-5-codex",
    "request_preview": "preview",
    "request_hash": "hash",
    "summary": "Synthetic verify runtime worker failure.",
    "changed_files": [],
    "events": [],
    "permission_requests": [],
    "command_trace": [],
    "started_at": "{{DateTimeOffset.UtcNow.AddMinutes(-2):O}}",
    "completed_at": "{{DateTimeOffset.UtcNow.AddMinutes(-1):O}}"
  }
}
""");
    }

    private static void WriteExpiredLease(string repoRoot, string taskId)
    {
        var leasesPath = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "workers", "leases.json");
        Directory.CreateDirectory(Path.GetDirectoryName(leasesPath)!);
        File.WriteAllText(leasesPath, $$"""
[
  {
    "schema_version": 1,
    "lease_id": "lease-verify-001",
    "node_id": "local-default",
    "repo_path": "{{repoRoot.Replace("\\", "\\\\")}}",
    "repo_id": null,
    "session_id": "default",
    "task_id": "{{taskId}}",
    "status": "expired",
    "on_expiry": "return_to_dispatchable",
    "acquired_at": "{{DateTimeOffset.UtcNow.AddMinutes(-3):O}}",
    "last_heartbeat_at": "{{DateTimeOffset.UtcNow.AddMinutes(-3):O}}",
    "expires_at": "{{DateTimeOffset.UtcNow.AddMinutes(-2):O}}",
    "completed_at": "{{DateTimeOffset.UtcNow.AddMinutes(-1):O}}",
    "completion_reason": "Lease expired after heartbeat timeout."
  }
]
""");
    }

    private static void WriteWorktreeRuntimeState(string repoRoot, string taskId)
    {
        var worktreePath = Path.Combine(repoRoot, ".carves-worktrees", taskId);
        Directory.CreateDirectory(worktreePath);
        File.WriteAllText(Path.Combine(worktreePath, "README.md"), "orphaned worktree");

        var worktreeStatePath = Path.Combine(repoRoot, ".ai", "runtime", "live-state", "worktrees.json");
        Directory.CreateDirectory(Path.GetDirectoryName(worktreeStatePath)!);
        File.WriteAllText(worktreeStatePath, $$"""
{
  "schema_version": 1,
  "records": [
    {
      "record_id": "record-{{taskId}}",
      "task_id": "{{taskId}}",
      "worktree_path": "{{worktreePath.Replace("\\", "\\\\")}}",
      "repo_root": "{{repoRoot.Replace("\\", "\\\\")}}",
      "base_commit": "abc123",
      "state": "active",
      "rebuild_from_worktree_path": null,
      "worker_run_id": "run-verify-001",
      "created_at": "{{DateTimeOffset.UtcNow.AddMinutes(-3):O}}",
      "updated_at": "{{DateTimeOffset.UtcNow.AddMinutes(-3):O}}"
    }
  ],
  "pending_rebuilds": []
}
""");
    }

    private static void RewriteSyntheticGhostTaskForReconciliation(string repoRoot, string blockedTaskId, string implementationTaskId, string validationTaskId)
    {
        var graphPath = Path.Combine(repoRoot, ".ai", "tasks", "graph.json");
        var graph = JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        foreach (var taskNode in graph["tasks"]!.AsArray())
        {
            var taskObject = taskNode!.AsObject();
            var taskId = taskObject["task_id"]!.GetValue<string>();
            if (string.Equals(taskId, blockedTaskId, StringComparison.Ordinal)
                || string.Equals(taskId, implementationTaskId, StringComparison.Ordinal)
                || string.Equals(taskId, validationTaskId, StringComparison.Ordinal))
            {
                taskObject["card_id"] = "CARD-GHOST";
            }
        }

        File.WriteAllText(graphPath, graph.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        RewriteNode(blockedTaskId, "blocked", "Shape interfaces for Synthetic Historical Card", Array.Empty<string>(), true);
        RewriteNode(implementationTaskId, "completed", "Implement Synthetic Historical Card", [blockedTaskId], false);
        RewriteNode(validationTaskId, "completed", "Validate Synthetic Historical Card", [implementationTaskId], false);

        void RewriteNode(string taskId, string status, string title, IReadOnlyList<string> dependencies, bool blockedShape)
        {
            var nodePath = Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"{taskId}.json");
            var node = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
            node["card_id"] = "CARD-GHOST";
            node["status"] = status;
            node["title"] = title;
            node["dependencies"] = new JsonArray(dependencies.Select(item => (JsonNode)item).ToArray());
            node["last_worker_run_id"] = null;
            node["last_worker_backend"] = null;
            node["last_worker_failure_kind"] = blockedShape ? "none" : node["last_worker_failure_kind"];
            node["last_worker_detail_ref"] = null;
            node["last_provider_detail_ref"] = null;
            node["last_recovery_action"] = blockedShape ? "rebuild_worktree" : null;
            node["last_recovery_reason"] = blockedShape
                ? $"Delegated worker worktree for {taskId} is quarantined and requires explicit recovery planning."
                : "Synthetic downstream task completed.";
            node["planner_review"] = blockedShape
                ? new JsonObject
                {
                    ["verdict"] = "human_decision_required",
                    ["reason"] = $"Delegated worker worktree for {taskId} is quarantined and requires explicit recovery planning.",
                    ["acceptance_met"] = false,
                    ["boundary_preserved"] = true,
                    ["scope_drift_detected"] = true,
                    ["follow_up_suggestions"] = new JsonArray(),
                }
                : new JsonObject
                {
                    ["verdict"] = "continue",
                    ["reason"] = "Synthetic downstream task completed.",
                    ["acceptance_met"] = true,
                    ["boundary_preserved"] = true,
                    ["scope_drift_detected"] = false,
                    ["follow_up_suggestions"] = new JsonArray(),
                };
            File.WriteAllText(nodePath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
