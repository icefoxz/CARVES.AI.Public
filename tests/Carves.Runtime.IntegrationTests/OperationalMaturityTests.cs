using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.IntegrationTests;

public sealed class OperationalMaturityTests
{
    [Fact]
    public void CleanupCommand_RemovesStaleWorktreesAndPreservesActiveOnes()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticTask("T-CLEANUP-ACTIVE", "running", scope: ["README.md"]);
        sandbox.AddSyntheticPendingTask("T-CLEANUP-STALE", scope: ["README.md"]);

        var worktreeRoot = ResolveWorktreeRoot(sandbox.RootPath);
        var activeWorktree = Path.Combine(worktreeRoot, "T-CLEANUP-ACTIVE");
        var staleWorktree = Path.Combine(worktreeRoot, "T-CLEANUP-STALE");
        Directory.CreateDirectory(activeWorktree);
        Directory.CreateDirectory(staleWorktree);
        File.WriteAllText(Path.Combine(activeWorktree, "README.md"), "active");
        File.WriteAllText(Path.Combine(staleWorktree, "README.md"), "stale");

        WriteWorktreeRuntimeSnapshot(
            sandbox.RootPath,
            ("T-CLEANUP-ACTIVE", activeWorktree, "active-run", "active"),
            ("T-CLEANUP-STALE", staleWorktree, "stale-run", "active"));
        WriteLeases(
            sandbox.RootPath,
            """
[
  {
    "schema_version": 1,
    "lease_id": "lease-cleanup-active",
    "node_id": "local-default",
    "repo_path": "__REPO_ROOT__",
    "repo_id": null,
    "session_id": "default",
    "task_id": "T-CLEANUP-ACTIVE",
    "status": "active",
    "on_expiry": "return_to_dispatchable",
    "acquired_at": "__NOW_MINUS_5__",
    "last_heartbeat_at": "__NOW_MINUS_1__",
    "expires_at": "__NOW_PLUS_5__",
    "completed_at": null,
    "completion_reason": null
  }
]
""");

        var runtimeResidueRoot = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var residueDirectory = Path.Combine(runtimeResidueRoot, "stale-run");
        var residueFile = Path.Combine(runtimeResidueRoot, "temp", "leftover.tmp");
        var coldCommandBuildDirectory = Path.Combine(runtimeResidueRoot, "cold-commands", "cold-build-test");
        Directory.CreateDirectory(residueDirectory);
        Directory.CreateDirectory(coldCommandBuildDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(residueFile)!);
        File.WriteAllText(Path.Combine(residueDirectory, "marker.txt"), "stale");
        File.WriteAllText(Path.Combine(coldCommandBuildDirectory, "Carves.Runtime.Host.dll"), "stale cold build");
        File.WriteAllText(residueFile, "temp");

        var codeGraphTmpDirectory = Path.Combine(sandbox.RootPath, ".ai", "codegraph", "tmp", "stale");
        var runtimeTmpDirectory = Path.Combine(sandbox.RootPath, ".ai", "runtime", "tmp", "stale");
        var runtimeStagingDirectory = Path.Combine(sandbox.RootPath, ".ai", "runtime", "staging", "stale");
        var artifactsTmpDirectory = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "tmp", "stale");
        var codeGraphTmpFile = Path.Combine(sandbox.RootPath, ".ai", "codegraph", "leftover.tmp");
        var runtimeTmpFile = Path.Combine(sandbox.RootPath, ".ai", "runtime", "leftover.tmp");
        var artifactsTmpFile = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "leftover.tmp");
        var taskTmpFile = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "stale-task.tmp");
        Directory.CreateDirectory(codeGraphTmpDirectory);
        Directory.CreateDirectory(runtimeTmpDirectory);
        Directory.CreateDirectory(runtimeStagingDirectory);
        Directory.CreateDirectory(artifactsTmpDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(taskTmpFile)!);
        File.WriteAllText(Path.Combine(codeGraphTmpDirectory, "marker.txt"), "codegraph residue");
        File.WriteAllText(Path.Combine(runtimeTmpDirectory, "marker.txt"), "runtime residue");
        File.WriteAllText(Path.Combine(runtimeStagingDirectory, "marker.txt"), "runtime staging");
        File.WriteAllText(Path.Combine(artifactsTmpDirectory, "marker.txt"), "artifact residue");
        File.WriteAllText(codeGraphTmpFile, "codegraph temp");
        File.WriteAllText(runtimeTmpFile, "runtime temp");
        File.WriteAllText(artifactsTmpFile, "artifact temp");
        File.WriteAllText(taskTmpFile, "task temp");

        var cleanup = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "cleanup");

        Assert.True(cleanup.ExitCode == 0, cleanup.CombinedOutput);
        Assert.True(Directory.Exists(activeWorktree));
        Assert.False(Directory.Exists(staleWorktree));
        Assert.False(Directory.Exists(residueDirectory));
        Assert.False(Directory.Exists(coldCommandBuildDirectory));
        Assert.False(File.Exists(residueFile));
        Assert.False(Directory.Exists(codeGraphTmpDirectory));
        Assert.False(Directory.Exists(runtimeTmpDirectory));
        Assert.False(Directory.Exists(runtimeStagingDirectory));
        Assert.False(Directory.Exists(artifactsTmpDirectory));
        Assert.False(File.Exists(codeGraphTmpFile));
        Assert.False(File.Exists(runtimeTmpFile));
        Assert.False(File.Exists(artifactsTmpFile));
        Assert.False(File.Exists(taskTmpFile));
        Assert.Contains("Runtime resource cleanup", cleanup.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Commit hygiene:", cleanup.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("EphemeralResidue", cleanup.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Removed ephemeral residue:", cleanup.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Preserved active worktrees:", cleanup.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("cleanup prunes EphemeralResidue only", cleanup.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanupCommand_PrunesPlanningDraftSpillButPreservesExecutionMemoryTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var cardDraftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "planning", "card-drafts", "CARD-351.json");
        var taskgraphDraftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "planning", "taskgraph-drafts", "TG-CARD-351-001.json");
        var executionMemoryPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "execution", "T-CARD-351-001", "MEM-T-CARD-351-001-001.json");

        Directory.CreateDirectory(Path.GetDirectoryName(cardDraftPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(taskgraphDraftPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(executionMemoryPath)!);
        File.WriteAllText(cardDraftPath, """{"card_id":"CARD-351"}""");
        File.WriteAllText(taskgraphDraftPath, """{"draft_id":"TG-CARD-351-001"}""");
        File.WriteAllText(executionMemoryPath, """{"memory_id":"MEM-T-CARD-351-001-001"}""");

        var cleanup = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "cleanup");
        var gitStatus = RunGitStatusShort(sandbox.RootPath);

        Assert.True(cleanup.ExitCode == 0, cleanup.CombinedOutput);
        Assert.DoesNotContain("CARD-351.json", gitStatus, StringComparison.Ordinal);
        Assert.DoesNotContain("TG-CARD-351-001.json", gitStatus, StringComparison.Ordinal);
        Assert.True(File.Exists(executionMemoryPath));
        Assert.Contains("EphemeralResidue", cleanup.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RebuildCommand_PerformsCleanCodeGraphRebuildAndStrictAudit()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        ResetSustainabilityRoots(sandbox.RootPath);

        var codeGraphRoot = Path.Combine(sandbox.RootPath, ".ai", "codegraph");
        Directory.CreateDirectory(codeGraphRoot);
        File.WriteAllText(Path.Combine(codeGraphRoot, "index.json"), "stale-index");
        File.WriteAllText(Path.Combine(codeGraphRoot, "audit.json"), """{ "strict_passed": false }""");

        var rebuild = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "rebuild");

        var indexPath = Path.Combine(sandbox.RootPath, ".ai", "codegraph", "index.json");
        var auditPath = Path.Combine(sandbox.RootPath, ".ai", "codegraph", "audit.json");
        var indexJson = File.ReadAllText(indexPath);
        var auditJson = JsonNode.Parse(File.ReadAllText(auditPath))!.AsObject();

        Assert.True(rebuild.ExitCode == 0, rebuild.CombinedOutput);
        Assert.False(File.Exists(Path.Combine(codeGraphRoot, "graph.json")));
        Assert.True(File.Exists(indexPath));
        Assert.True(File.Exists(auditPath));
        Assert.Contains("\"modules\"", indexJson, StringComparison.Ordinal);
        Assert.DoesNotContain("stale-index", indexJson, StringComparison.Ordinal);
        Assert.DoesNotContain("project.assets.json", indexJson, StringComparison.Ordinal);
        Assert.True(auditJson["strict_passed"]?.GetValue<bool>() ?? false);
        Assert.Contains("CodeGraph rebuilt: true", rebuild.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CodeGraph audit strict passed: True", rebuild.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void InspectAndAuditSustainability_ProjectRuntimeBudgetTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        ResetSustainabilityRoots(sandbox.RootPath);

        var pollutedTruth = Path.Combine(sandbox.RootPath, ".ai", "tasks", "raw.log");
        Directory.CreateDirectory(Path.GetDirectoryName(pollutedTruth)!);
        File.WriteAllText(pollutedTruth, "polluted canonical truth");

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "sustainability");
        var audit = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "audit", "sustainability");

        Assert.True(inspect.ExitCode == 0, inspect.CombinedOutput);
        Assert.Contains("Runtime artifact catalog", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Commit classes:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("truth_checkpoint", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("local_residue", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("GovernedMirror", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("LiveState", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("validation_trace_history", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("OnDemandDetail", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("closure=cleanup_only", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("archive=cleanup_only_not_archive", inspect.StandardOutput, StringComparison.Ordinal);

        Assert.True(audit.ExitCode == 1, audit.CombinedOutput);
        Assert.Contains("Runtime sustainability audit", audit.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Commit classes:", audit.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("commit=truth_checkpoint", audit.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Strict passed: False", audit.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("canonical_truth_pollution", audit.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("task_truth", audit.CombinedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void CompactHistoryCommand_ArchivesOperationalHistoryAndPersistsReport()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        ResetSustainabilityRoots(sandbox.RootPath);

        var validationTraceRoot = Path.Combine(sandbox.RootPath, ".ai", "validation", "traces");
        var runRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", "T-CARD-290-INTEGRATION");
        var runReportRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "run-reports", "T-CARD-317-INTEGRATION");
        var planningTaskgraphRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "planning", "taskgraph-drafts", "compact-history");
        var executionRoot = Path.Combine(sandbox.RootPath, ".ai", "execution", "T-CARD-317-INTEGRATION");
        var contextPackRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "context-packs", "tasks");
        var executionPacketRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "execution-packets");
        var failureRoot = Path.Combine(sandbox.RootPath, ".ai", "failures");
        var runtimeFailureArtifactRoot = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "runtime-failures");
        Directory.CreateDirectory(validationTraceRoot);
        Directory.CreateDirectory(runRoot);
        Directory.CreateDirectory(runReportRoot);
        Directory.CreateDirectory(planningTaskgraphRoot);
        Directory.CreateDirectory(executionRoot);
        Directory.CreateDirectory(contextPackRoot);
        Directory.CreateDirectory(executionPacketRoot);
        Directory.CreateDirectory(failureRoot);
        Directory.CreateDirectory(runtimeFailureArtifactRoot);

        for (var index = 0; index < 31; index++)
        {
            var tracePath = Path.Combine(validationTraceRoot, $"trace-{index:00}.json");
            File.WriteAllText(tracePath, $$"""{"trace_id":"trace-{{index:00}}"}""");
            File.SetLastWriteTimeUtc(tracePath, DateTime.UtcNow.AddMinutes(-index));
        }

        for (var index = 0; index < 7; index++)
        {
            var runPath = Path.Combine(runRoot, $"run-{index:00}.json");
            File.WriteAllText(runPath, $$"""{"run_id":"run-{{index:00}}"}""");
            File.SetLastWriteTimeUtc(runPath, DateTime.UtcNow.AddMinutes(-(index + 100)));
        }

        for (var index = 0; index < 91; index++)
        {
            var reportPath = Path.Combine(runReportRoot, $"run-report-{index:00}.json");
            File.WriteAllText(reportPath, $$"""{"run_id":"run-report-{{index:00}}"}""");
            File.SetLastWriteTimeUtc(reportPath, DateTime.UtcNow.AddMinutes(-(index + 200)));
        }

        for (var index = 0; index < 301; index++)
        {
            var taskgraphPath = Path.Combine(planningTaskgraphRoot, $"TG-{index:000}.json");
            var executionPath = Path.Combine(executionRoot, $"execution-{index:000}.json");
            File.WriteAllText(taskgraphPath, $$"""{"draft_id":"TG-{{index:000}}"}""");
            File.WriteAllText(executionPath, $$"""{"task_id":"T-CARD-317-INTEGRATION","entry_id":"{{index:000}}"}""");
            var timestamp = DateTime.UtcNow.AddMinutes(-(index + 300));
            File.SetLastWriteTimeUtc(taskgraphPath, timestamp);
            File.SetLastWriteTimeUtc(executionPath, timestamp);
        }

        for (var index = 0; index < 121; index++)
        {
            var contextPackPath = Path.Combine(contextPackRoot, $"T-CTX-{index:000}.json");
            var executionPacketPath = Path.Combine(executionPacketRoot, $"T-PACKET-{index:000}.json");
            var failurePath = Path.Combine(failureRoot, $"FAIL-{index:000}.json");
            var runtimeFailurePath = Path.Combine(runtimeFailureArtifactRoot, $"FAIL-{index:000}.json");
            File.WriteAllText(contextPackPath, $$"""{"task_id":"T-CTX-{{index:000}}"}""");
            File.WriteAllText(executionPacketPath, $$"""{"task_id":"T-PACKET-{{index:000}}"}""");
            File.WriteAllText(failurePath, $$"""{"failure_id":"FAIL-{{index:000}}"}""");
            File.WriteAllText(runtimeFailurePath, $$"""{"failure_id":"FAIL-{{index:000}}","kind":"runtime"}""");
            var timestamp = DateTime.UtcNow.AddMinutes(-(index + 700));
            File.SetLastWriteTimeUtc(contextPackPath, timestamp);
            File.SetLastWriteTimeUtc(executionPacketPath, timestamp);
            File.SetLastWriteTimeUtc(failurePath, timestamp);
            File.SetLastWriteTimeUtc(runtimeFailurePath, timestamp);
        }

        var workerRoot = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker");
        var workerExecutionRoot = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-executions");
        var providerRoot = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "provider");
        var reviewRoot = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews");
        Directory.CreateDirectory(workerRoot);
        Directory.CreateDirectory(workerExecutionRoot);
        Directory.CreateDirectory(providerRoot);
        Directory.CreateDirectory(reviewRoot);

        var oldWorker = Path.Combine(workerRoot, "T-OLD.json");
        var oldExecution = Path.Combine(workerExecutionRoot, "T-OLD.json");
        var oldProvider = Path.Combine(providerRoot, "T-OLD.json");
        var oldReview = Path.Combine(reviewRoot, "T-OLD.json");
        File.WriteAllBytes(oldWorker, new byte[4 * 1024 * 1024]);
        File.WriteAllBytes(oldExecution, new byte[3 * 1024 * 1024]);
        File.WriteAllBytes(oldProvider, new byte[2 * 1024 * 1024]);
        File.WriteAllText(oldReview, """{"task_id":"T-OLD"}""");
        File.SetLastWriteTimeUtc(oldWorker, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldExecution, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldProvider, DateTime.UtcNow.AddDays(-10));
        File.SetLastWriteTimeUtc(oldReview, DateTime.UtcNow.AddDays(-10));

        for (var index = 0; index < 40; index++)
        {
            var worker = Path.Combine(workerRoot, $"T-NEW-{index:00}.json");
            var provider = Path.Combine(providerRoot, $"T-NEW-{index:00}.json");
            File.WriteAllText(worker, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"worker"}""");
            File.WriteAllText(provider, $$"""{"task_id":"T-NEW-{{index:00}}","kind":"provider"}""");
            var timestamp = DateTime.UtcNow.AddMinutes(-index);
            File.SetLastWriteTimeUtc(worker, timestamp);
            File.SetLastWriteTimeUtc(provider, timestamp);
        }

        var compact = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "compact-history");
        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "history-compaction");
        var audit = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "audit", "sustainability");
        var archiveReadiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "archive-readiness");
        var archiveFollowUp = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "archive-followup");

        Assert.Contains("Runtime maintenance: compact-history", compact.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("Commit hygiene:", compact.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("History archive root:", compact.CombinedOutput, StringComparison.Ordinal);

        Assert.True(inspect.ExitCode == 0, inspect.CombinedOutput);
        Assert.Contains("Operational history compaction", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("validation_trace_history", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("execution_run_detail_history", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("planning_runtime_history", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("execution_surface_history", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("execution_run_report_history", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("context_pack_projection", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("execution_packet_mirror", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_failure_detail_history", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("worker_execution_artifact_history", inspect.StandardOutput, StringComparison.Ordinal);

        Assert.True(archiveReadiness.ExitCode == 0, archiveReadiness.CombinedOutput);
        Assert.Contains("Operational history archive readiness", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("worker_execution_artifact_history", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("planning_runtime_history", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("context_pack_projection", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("execution_packet_mirror", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_failure_detail_history", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("retention=rolling_window_hot_and_age_bound", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("closure=compact_history", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("archive=archive_ready_after_hot_window_with_followup", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("fell outside the configured hot window", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".ai/artifacts/provider/T-OLD.json", archiveReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".ai/artifacts/reviews/T-OLD.json", archiveReadiness.StandardOutput, StringComparison.Ordinal);

        Assert.True(archiveFollowUp.ExitCode == 0, archiveFollowUp.CombinedOutput);
        Assert.Contains("Operational history archive follow-up", archiveFollowUp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("provider_evidence", archiveFollowUp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("review_evidence", archiveFollowUp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("task=T-OLD", archiveFollowUp.StandardOutput, StringComparison.Ordinal);

        Assert.True(audit.ExitCode == 0, audit.CombinedOutput);
        Assert.DoesNotContain("size_budget_exceeded: worker_execution_artifact_history", audit.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("read_path_pressure: worker_execution_artifact_history", audit.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("size_budget_exceeded: planning_runtime_history", audit.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("size_budget_exceeded: execution_surface_history", audit.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("size_budget_exceeded: execution_run_report_history", audit.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("size_budget_exceeded: runtime_failure_detail_history", audit.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(15, Directory.EnumerateFiles(validationTraceRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(3, Directory.EnumerateFiles(runRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(30, Directory.EnumerateFiles(runReportRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(80, Directory.EnumerateFiles(planningTaskgraphRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(60, Directory.EnumerateFiles(executionRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(20, Directory.EnumerateFiles(contextPackRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(12, Directory.EnumerateFiles(executionPacketRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(20, Directory.EnumerateFiles(failureRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.Equal(20, Directory.EnumerateFiles(runtimeFailureArtifactRoot, "*.json", SearchOption.TopDirectoryOnly).Count());
        Assert.False(File.Exists(oldWorker));
        Assert.False(File.Exists(oldExecution));
        Assert.False(File.Exists(oldProvider));
        Assert.False(File.Exists(oldReview));
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "runtime", "sustainability", "archive", "index.json")));
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "runtime", "sustainability", "compaction.json")));
    }

    [Fact]
    public void HostRestart_ReconcilesStaleExecutingSessionIntoBlockedRecoveryState()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticTask("T-REHYDRATE-STALE", "running", scope: ["README.md"]);

        var worktreeRoot = ResolveWorktreeRoot(sandbox.RootPath);
        var runningWorktree = Path.Combine(worktreeRoot, "T-REHYDRATE-STALE");
        Directory.CreateDirectory(runningWorktree);
        File.WriteAllText(Path.Combine(runningWorktree, "README.md"), "running");

        WriteSession(
            sandbox.RootPath,
            """
{
  "schema_version": 1,
  "session_id": "default",
  "attached_repo_root": "__REPO_ROOT__",
  "status": "executing",
  "loop_mode": "manual_tick",
  "dry_run": false,
  "active_worker_count": 1,
  "tick_count": 1,
  "active_task_ids": ["T-REHYDRATE-STALE"],
  "review_pending_task_ids": [],
  "pending_permission_request_ids": [],
  "current_task_id": "T-REHYDRATE-STALE",
  "last_task_id": "T-REHYDRATE-STALE",
  "last_reason": "Synthetic stale execution.",
  "loop_reason": "Synthetic stale execution.",
  "loop_actionability": "worker_actionable",
  "started_at": "__NOW_MINUS_5__",
  "updated_at": "__NOW_MINUS_1__"
}
""");
        WriteLeases(
            sandbox.RootPath,
            """
[
  {
    "schema_version": 1,
    "lease_id": "lease-rehydrate-stale",
    "node_id": "local-default",
    "repo_path": "__REPO_ROOT__",
    "repo_id": null,
    "session_id": "default",
    "task_id": "T-REHYDRATE-STALE",
    "status": "active",
    "on_expiry": "return_to_dispatchable",
    "acquired_at": "__NOW_MINUS_10__",
    "last_heartbeat_at": "__NOW_MINUS_2__",
    "expires_at": "__NOW_PLUS_1__",
    "completed_at": null,
    "completion_reason": null
  }
]
""");
        WriteWorktreeRuntimeSnapshot(
            sandbox.RootPath,
            ("T-REHYDRATE-STALE", runningWorktree, "run-rehydrate-stale", "active"));

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status");
            var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "inspect", "T-REHYDRATE-STALE");
            var verify = ProgramHarness.Run("--repo-root", sandbox.RootPath, "verify", "runtime");

            Assert.True(start.ExitCode == 0, start.CombinedOutput);
            Assert.Contains("Rehydrated: True", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("reconciled 1 task", status.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.True(inspect.ExitCode == 0, inspect.CombinedOutput);
            Assert.Contains("\"status\": \"Blocked\"", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("reconciled delegated worker lifecycle truth", inspect.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.True(verify.ExitCode == 0, verify.CombinedOutput);
            Assert.Contains("Findings: 0", verify.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void StatusSummaryAndReports_ProjectOperationalTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticTask("T-SUMMARY-BLOCKED", "blocked", scope: ["README.md"]);
        SeedOperatorOsEvents(sandbox.RootPath);
        SeedApprovalAudit(sandbox.RootPath);
        SeedIncidentTimeline(sandbox.RootPath);

        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "status", "--summary");
        var delegation = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "report", "delegation");
        var approvals = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "report", "approvals");

        Assert.True(status.ExitCode == 0, status.CombinedOutput);
        Assert.Contains("Workers:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, delegation.ExitCode);
        Assert.Contains("Delegation report window: last 24h", delegation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Delegation requested:", delegation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Delegation completed:", delegation.StandardOutput, StringComparison.Ordinal);
        Assert.True(approvals.ExitCode == 0, approvals.CombinedOutput);
        Assert.Contains("Approval report window: last 24h", approvals.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Decisions:", approvals.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleRepoOperatorOsProof_IsRepeatableAcrossRestart()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var timeoutScope = DelegatedExecutionTimeoutScope.Apply(workerRequestTimeoutCapSeconds: 60);
        sandbox.ResetHistoricalDelegatedExecutionTruth();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-OS-PROOF-001", scope: ["README.md"]);
        sandbox.WriteDelegatedExecutionHostInvokePolicy(requestTimeoutSeconds: 90);
        sandbox.WriteAiProviderConfig(
            """
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
        var cliPath = CreateCodexCliScript();
        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-operator-proof");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-operator-proof", "codex-worker-local-cli");

            var hostStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var intent = ProgramHarness.Run("--repo-root", sandbox.RootPath, "intent", "status");
            var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "inspect", "T-OS-PROOF-001");
            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", "T-OS-PROOF-001");
            var summary = ProgramHarness.Run("--repo-root", sandbox.RootPath, "status", "--summary");

            Assert.True(hostStart.ExitCode == 0, hostStart.CombinedOutput);
            Assert.True(intent.ExitCode == 0, intent.CombinedOutput);
            Assert.Contains("\"kind\": \"intent_status\"", intent.StandardOutput, StringComparison.Ordinal);
            Assert.True(inspect.ExitCode == 0, inspect.CombinedOutput);
            Assert.Contains("\"task_id\": \"T-OS-PROOF-001\"", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.True(run.ExitCode == 0, run.CombinedOutput);
            Assert.Contains("\"accepted\": true", run.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"backend_id\": \"codex_cli\"", run.StandardOutput, StringComparison.Ordinal);
            Assert.True(summary.ExitCode == 0, summary.CombinedOutput);
            Assert.Contains("Last delegation:", summary.StandardOutput, StringComparison.Ordinal);

            StopHost(sandbox.RootPath);

            var restart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var verify = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "verify", "runtime");
            var report = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "report", "delegation");

            Assert.True(restart.ExitCode == 0, restart.CombinedOutput);
            Assert.True(verify.ExitCode == 0, verify.CombinedOutput);
            Assert.Contains("Findings: 0", verify.StandardOutput, StringComparison.Ordinal);
            Assert.True(report.ExitCode == 0, report.CombinedOutput);
            Assert.Contains("Delegation completed:", report.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            StopHost(sandbox.RootPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    private static string ResolveWorktreeRoot(string repoRoot)
    {
        var systemConfigPath = Path.Combine(repoRoot, ".ai", "config", "system.json");
        var systemConfig = JsonNode.Parse(File.ReadAllText(systemConfigPath))!.AsObject();
        var relativeRoot = systemConfig["worktree_root"]?.GetValue<string>() ?? ".carves-worktrees";
        return Path.GetFullPath(Path.Combine(repoRoot, relativeRoot));
    }

    private static string ResolveHostRuntimeDirectory(string repoRoot)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(repoRoot))))
            .ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "carves-runtime-host", hash[..16]);
    }

    private static void WriteWorktreeRuntimeSnapshot(string repoRoot, params (string TaskId, string WorktreePath, string WorkerRunId, string State)[] records)
    {
        var path = Path.Combine(repoRoot, ".ai", "runtime", "live-state", "worktrees.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var payload = new JsonObject
        {
            ["schema_version"] = 1,
            ["records"] = new JsonArray(records.Select(record => new JsonObject
            {
                ["schema_version"] = 1,
                ["record_id"] = $"record-{record.TaskId}",
                ["task_id"] = record.TaskId,
                ["worktree_path"] = record.WorktreePath,
                ["repo_root"] = repoRoot,
                ["base_commit"] = "abc123",
                ["state"] = record.State,
                ["quarantine_reason"] = null,
                ["rebuilt_from_worktree_path"] = null,
                ["worker_run_id"] = record.WorkerRunId,
                ["created_at"] = DateTimeOffset.UtcNow.AddMinutes(-5),
                ["updated_at"] = DateTimeOffset.UtcNow.AddMinutes(-1),
            }).ToArray()),
            ["pending_rebuilds"] = new JsonArray(),
        };
        File.WriteAllText(path, payload.ToJsonString(new() { WriteIndented = true }));
    }

    private static void WriteLeases(string repoRoot, string template)
    {
        var path = ControlPlanePaths.FromRepoRoot(repoRoot).PlatformWorkerLeasesLiveStateFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ReplaceTemplateTokens(template, repoRoot));
    }

    private static void WriteSession(string repoRoot, string template)
    {
        var path = Path.Combine(repoRoot, ".ai", "runtime", "live-state", "session.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ReplaceTemplateTokens(template, repoRoot));
    }

    private static void SeedOperatorOsEvents(string repoRoot)
    {
        var path = ControlPlanePaths.FromRepoRoot(repoRoot).PlatformOperatorOsEventsRuntimeFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ReplaceTemplateTokens(
            """
{
  "entries": [
    {
      "schema_version": 1,
      "event_id": "event-delegation-requested",
      "event_kind": "delegation_requested",
      "repo_id": "local-repo",
      "actor_session_id": "actor-operator",
      "actor_kind": "operator",
      "actor_identity": "operator",
      "task_id": "T-SUMMARY-DELEGATION",
      "run_id": "run-summary-delegation",
      "backend_id": "codex_cli",
      "provider_id": "codex",
      "permission_request_id": null,
      "ownership_scope": null,
      "ownership_target_id": null,
      "incident_id": null,
      "reference_id": null,
      "reason_code": "delegation_requested",
      "summary": "Delegation started for T-SUMMARY-DELEGATION.",
      "occurred_at": "__NOW_MINUS_5__"
    },
    {
      "schema_version": 1,
      "event_id": "event-delegation-completed",
      "event_kind": "delegation_completed",
      "repo_id": "local-repo",
      "actor_session_id": "actor-operator",
      "actor_kind": "operator",
      "actor_identity": "operator",
      "task_id": "T-SUMMARY-DELEGATION",
      "run_id": "run-summary-delegation",
      "backend_id": "codex_cli",
      "provider_id": "codex",
      "permission_request_id": null,
      "ownership_scope": null,
      "ownership_target_id": null,
      "incident_id": null,
      "reference_id": null,
      "reason_code": "delegation_completed",
      "summary": "Delegation completed for T-SUMMARY-DELEGATION.",
      "occurred_at": "__NOW_MINUS_4__"
    }
  ]
}
""", repoRoot));
    }

    private static void SeedApprovalAudit(string repoRoot)
    {
        var path = ControlPlanePaths.FromRepoRoot(repoRoot).PlatformPermissionAuditRuntimeFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ReplaceTemplateTokens(
            """
[
  {
    "schema_version": 1,
    "audit_id": "approval-audit-allow",
    "repo_id": "local-repo",
    "permission_request_id": "perm-summary-001",
    "run_id": "run-summary-approval",
    "task_id": "T-SUMMARY-APPROVAL",
    "backend_id": "codex_cli",
    "provider_id": "codex",
    "permission_kind": "filesystem_write",
    "risk_level": "moderate",
    "event_kind": "human_allowed",
    "decision": "allow",
    "actor_kind": "human",
    "actor_identity": "operator",
    "reason_code": "operator_allowed",
    "reason": "Operator approved summary request.",
    "consequence_summary": "Task returned to dispatchable execution.",
    "occurred_at": "__NOW_MINUS_3__"
  }
]
""", repoRoot));
    }

    private static void SeedIncidentTimeline(string repoRoot)
    {
        var path = ControlPlanePaths.FromRepoRoot(repoRoot).PlatformIncidentTimelineRuntimeFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ReplaceTemplateTokens(
            """
[
  {
    "schema_version": 1,
    "incident_id": "incident-summary-001",
    "incident_type": "worker_failed",
    "repo_id": "local-repo",
    "task_id": "T-SUMMARY-BLOCKED",
    "run_id": "run-summary-incident",
    "backend_id": "codex_cli",
    "provider_id": "codex",
    "permission_request_id": null,
    "failure_kind": "environment_blocked",
    "recovery_action": "retry",
    "actor_kind": "system",
    "actor_identity": "runtime",
    "reason_code": "synthetic_incident",
    "summary": "Synthetic summary incident.",
    "consequence_summary": "Task requires retry.",
    "reference_id": null,
    "occurred_at": "__NOW_MINUS_2__"
  }
]
""", repoRoot));
    }

    private static string ReplaceTemplateTokens(string template, string repoRoot)
    {
        return template
            .Replace("__REPO_ROOT__", repoRoot.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace("__NOW_MINUS_10__", DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O"), StringComparison.Ordinal)
            .Replace("__NOW_MINUS_5__", DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"), StringComparison.Ordinal)
            .Replace("__NOW_MINUS_4__", DateTimeOffset.UtcNow.AddMinutes(-4).ToString("O"), StringComparison.Ordinal)
            .Replace("__NOW_MINUS_3__", DateTimeOffset.UtcNow.AddMinutes(-3).ToString("O"), StringComparison.Ordinal)
            .Replace("__NOW_MINUS_2__", DateTimeOffset.UtcNow.AddMinutes(-2).ToString("O"), StringComparison.Ordinal)
            .Replace("__NOW_MINUS_1__", DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"), StringComparison.Ordinal)
            .Replace("__NOW_PLUS_1__", DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"), StringComparison.Ordinal)
            .Replace("__NOW_PLUS_5__", DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"), StringComparison.Ordinal);
    }

    private static void ResetSustainabilityRoots(string repoRoot)
    {
        ResetDirectory(Path.Combine(repoRoot, ".ai", "runtime", "sustainability"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "validation", "traces"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "execution"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "failures"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "runtime", "context-packs"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "runtime", "execution-packets"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "runtime", "planning"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "runtime", "runs"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "runtime", "run-reports"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "codegraph", "tmp"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "runtime", "tmp"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "runtime", "staging"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "artifacts", "tmp"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "artifacts", "runtime-failures"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "artifacts", "worker"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "artifacts", "worker-executions"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "artifacts", "provider"));
        ResetDirectory(Path.Combine(repoRoot, ".ai", "artifacts", "reviews"));
        ResetDirectory(ResolveWorktreeRoot(repoRoot));
        PruneTemporaryTaskFiles(repoRoot);
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static void PruneTemporaryTaskFiles(string repoRoot)
    {
        var taskRoot = Path.Combine(repoRoot, ".ai", "tasks");
        if (!Directory.Exists(taskRoot))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(taskRoot, "*.tmp", SearchOption.AllDirectories))
        {
            File.Delete(path);
        }
    }

    private static void StopHost(string repoRoot)
    {
        ProgramHarness.Run("--repo-root", repoRoot, "host", "stop", "operational maturity cleanup");
    }

    private static string RunGitStatusShort(string repoRoot)
    {
        return GitTestHarness.RunForStandardOutput(repoRoot, "status", "--short");
    }

    private static string CreateCodexCliScript()
    {
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
  echo {"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"README.md","kind":"modify"}]}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"cli worker success"}}
  echo {"type":"turn.completed","usage":{"input_tokens":13,"output_tokens":6}}
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
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
  echo '{"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"README.md","kind":"modify"}]}}'
  echo '{"type":"item.completed","item":{"type":"agent_message","text":"cli worker success"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":13,"output_tokens":6}}'
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(scriptPath, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return scriptPath;
    }
}
