using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Host;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class HostContractTests
{
    [Fact]
    public async Task TaskRun_ReviewBoundary_PersistsCompletedExecutionRunStatus()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-REVIEW-RUN", scope: [".ai/tests/Host/ReviewRun"]);
        await using var server = new StubResponsesApiServer("""
{
  "id": "resp_test_review_123",
  "model": "gpt-4.1",
  "output": [
    {
      "type": "message",
      "content": [
        {
          "type": "output_text",
          "text": "Prompt-safe default read surface verified. Keep summaries compact and preserve explicit detail refs."
        }
      ]
    }
  ],
  "usage": {
    "input_tokens": 24,
    "output_tokens": 18
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
  "organization": null,
  "project": null
}
""");

        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        try
        {
            var run = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "run", "T-INTEGRATION-REVIEW-RUN");
            var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "inspect", "T-INTEGRATION-REVIEW-RUN");
            var taskNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-REVIEW-RUN.json");
            var taskNodeJson = File.ReadAllText(taskNodePath);

            Assert.Equal(0, run.ExitCode);
            Assert.Equal(0, inspect.ExitCode);
            Assert.Contains("\"task_status\": \"review\"", run.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
            Assert.Contains("\"execution_run_latest_status\": \"Completed\"", taskNodeJson, StringComparison.Ordinal);
            Assert.Contains("\"latest_status\": \"Completed\"", inspect.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void SyncState_ReconcilesHistoricalReviewRunStatus()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-SYNC-RUN");

        var runId = "RUN-T-INTEGRATION-SYNC-RUN-001";
        var runRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", "T-INTEGRATION-SYNC-RUN");
        Directory.CreateDirectory(runRoot);
        var runPath = Path.Combine(runRoot, $"{runId}.json");
        File.WriteAllText(
            runPath,
            JsonSerializer.Serialize(
                new ExecutionRun
                {
                    RunId = runId,
                    TaskId = "T-INTEGRATION-SYNC-RUN",
                    Status = ExecutionRunStatus.Failed,
                    CurrentStepIndex = 4,
                    Steps =
                    [
                        new ExecutionStep { StepId = $"{runId}-STEP-001", Title = "Inspect", Kind = ExecutionStepKind.Inspect, Status = ExecutionStepStatus.Completed },
                        new ExecutionStep { StepId = $"{runId}-STEP-002", Title = "Implement", Kind = ExecutionStepKind.Implement, Status = ExecutionStepStatus.Completed },
                        new ExecutionStep { StepId = $"{runId}-STEP-003", Title = "Verify", Kind = ExecutionStepKind.Verify, Status = ExecutionStepStatus.Completed },
                        new ExecutionStep { StepId = $"{runId}-STEP-004", Title = "Writeback", Kind = ExecutionStepKind.Writeback, Status = ExecutionStepStatus.Completed },
                        new ExecutionStep { StepId = $"{runId}-STEP-005", Title = "Cleanup", Kind = ExecutionStepKind.Cleanup, Status = ExecutionStepStatus.Completed },
                    ],
                },
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }));

        var sync = RunProgram("--repo-root", sandbox.RootPath, "--cold", "sync-state");
        var taskNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-SYNC-RUN.json");
        var storedRun = File.ReadAllText(runPath);
        var storedTask = File.ReadAllText(taskNodePath);

        Assert.True(sync.ExitCode == 0, sync.CombinedOutput);
        Assert.Contains("\"status\": \"Completed\"", storedRun, StringComparison.Ordinal);
        Assert.Contains("\"execution_run_latest_status\": \"Completed\"", storedTask, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncState_PreservesHistoricalOverrideWithoutValidation_AndProjectsExceptionSurface()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticHistoricalExecutionRunExceptionTask(
            "T-INTEGRATION-HISTORICAL-EXCEPTION",
            taskStatus: "completed",
            validationPassed: false,
            safetyOutcome: "blocked",
            decisionStatus: "approved",
            reviewResultingStatus: "pending",
            substrateFailure: true,
            substrateCategory: "delegated_worker_launch_failed",
            workerSummary: "Synthetic substrate failure remained as historical override.");

        var sync = RunProgram("--repo-root", sandbox.RootPath, "--cold", "sync-state");
        var inspectExceptions = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "execution-run-exceptions");
        var inspectTask = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "inspect", "T-INTEGRATION-HISTORICAL-EXCEPTION");
        var runPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", "T-INTEGRATION-HISTORICAL-EXCEPTION", "RUN-T-INTEGRATION-HISTORICAL-EXCEPTION-001.json");
        var runJson = File.ReadAllText(runPath);

        Assert.True(sync.ExitCode == 0, sync.CombinedOutput);
        Assert.Equal(0, inspectExceptions.ExitCode);
        Assert.Equal(0, inspectTask.ExitCode);
        Assert.Contains("\"status\": \"Failed\"", runJson, StringComparison.Ordinal);
        Assert.Contains("T-INTEGRATION-HISTORICAL-EXCEPTION", inspectExceptions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("ApprovedWithoutValidation", inspectExceptions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SubstrateFailureReviewOverride", inspectExceptions.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"execution_run_exception\":", inspectTask.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"auto_reconcile_eligible\": false", inspectTask.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectReview_ReturnsTaskToPending()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-REJECT");

        var rejectExitCode = Program.Main(["--repo-root", sandbox.RootPath, "--cold", "reject-review", "T-INTEGRATION-REJECT", "Needs", "another", "pass"]);
        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-REJECT.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", "T-INTEGRATION-REJECT.json"));

        Assert.Equal(0, rejectExitCode);
        Assert.Contains("\"status\": \"pending\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"rejected\"", reviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskRun_RejectedBlockedTask_DoesNotCreateActiveExecutionRun()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        sandbox.AddSyntheticTask("T-INTEGRATION-BLOCKED", "blocked", "Synthetic blocked task", [".ai/runtime/sustainability"]);

        var result = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "run", "T-INTEGRATION-BLOCKED", "--dry-run");
        using var envelope = ParseJsonOutput(result.CombinedOutput);
        var root = envelope.RootElement;
        var taskNode = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-BLOCKED.json")))!.AsObject();
        var runRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", "T-INTEGRATION-BLOCKED");

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(root.GetProperty("accepted").GetBoolean());
        Assert.Equal("failed", root.GetProperty("outcome").GetString());
        Assert.Contains("is not pending and cannot be delegated", root.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Equal("unchanged", root.GetProperty("task_status").GetString());
        Assert.Equal("unchanged", root.GetProperty("session_status").GetString());
        Assert.False(root.GetProperty("host_result_ingestion_attempted").GetBoolean());
        Assert.Null(root.GetProperty("run_id").GetString());
        Assert.Null(root.GetProperty("execution_run_id").GetString());
        Assert.Equal("blocked", taskNode["status"]!.GetValue<string>());
        Assert.False(taskNode["metadata"]!.AsObject().ContainsKey("execution_run_active_id"));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("fallback_run_packet").ValueKind);
        Assert.False(Directory.Exists(runRoot));
    }

    [Fact]
    public void TaskRun_RejectedMissingAcceptanceContract_ProjectsExecutionGate()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-MISSING-CONTRACT", includeAcceptanceContract: false);

        var result = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "run", "T-INTEGRATION-MISSING-CONTRACT");
        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "inspect", "T-INTEGRATION-MISSING-CONTRACT");
        var explain = RunProgram("--repo-root", sandbox.RootPath, "--cold", "explain-task", "T-INTEGRATION-MISSING-CONTRACT");
        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-MISSING-CONTRACT.json"));
        var runRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", "T-INTEGRATION-MISSING-CONTRACT");
        using var document = JsonDocument.Parse(inspect.StandardOutput);
        var root = document.RootElement;
        var gate = root.GetProperty("acceptance_contract_gate");
        var combined = string.Concat(result.StandardOutput, result.StandardError);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, explain.ExitCode);
        Assert.Contains("cannot execute because acceptance contract projection is missing", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("\"execution_run_active_id\":", taskNodeJson, StringComparison.Ordinal);
        Assert.False(Directory.Exists(runRoot));
        Assert.Equal("missing", gate.GetProperty("status").GetString());
        Assert.Equal("acceptance_contract_missing", gate.GetProperty("reason_code").GetString());
        Assert.True(gate.GetProperty("blocks_execution").GetBoolean());
        Assert.Equal("dispatch_blocked", root.GetProperty("dispatch").GetProperty("state").GetString());
        Assert.Contains("missing an acceptance contract", root.GetProperty("blocked_reason").GetString(), StringComparison.Ordinal);
        Assert.Equal("project a minimum acceptance contract onto task truth before dispatch", root.GetProperty("next_action").GetString());
        Assert.Contains("Acceptance contract gate:", explain.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- status: missing", explain.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskRun_RejectedForeignReviewBoundary_DoesNotCreateActiveExecutionRun()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-PENDING-RERUN");
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-FOREIGN-REVIEW");

        var result = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "run", "T-INTEGRATION-PENDING-RERUN");
        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-PENDING-RERUN.json"));
        var runRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", "T-INTEGRATION-PENDING-RERUN");
        var combined = string.Concat(result.StandardOutput, result.StandardError);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Runtime session is waiting at the governance boundary for other review task(s) [T-INTEGRATION-FOREIGN-REVIEW]", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("\"execution_run_active_id\":", taskNodeJson, StringComparison.Ordinal);
        Assert.False(Directory.Exists(runRoot));
    }

    [Fact]
    public void TaskRun_ReconcilesStaleForeignReviewBoundaryBeforeDryRunExecution()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-STALE-REVIEW");
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-READY");

        var staleNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-STALE-REVIEW.json");
        var staleNode = JsonNode.Parse(File.ReadAllText(staleNodePath))!.AsObject();
        staleNode["status"] = "pending";
        staleNode["planner_review"] = new JsonObject
        {
            ["verdict"] = "continue",
            ["reason"] = "Synthetic stale review boundary should reconcile before delegated execution.",
            ["acceptance_met"] = false,
            ["boundary_preserved"] = true,
            ["scope_drift_detected"] = false,
            ["follow_up_suggestions"] = new JsonArray(),
        };
        File.WriteAllText(staleNodePath, staleNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var result = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "run", "T-INTEGRATION-READY", "--dry-run");
        using var envelope = JsonDocument.Parse(result.StandardOutput);
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();
        var readyNode = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-READY.json")))!.AsObject();
        var reviewPendingTaskIds = sessionJson["review_pending_task_ids"]!
            .AsArray()
            .Select(item => item!.GetValue<string>())
            .ToArray();

        Assert.Equal(0, result.ExitCode);
        Assert.True(envelope.RootElement.GetProperty("accepted").GetBoolean());
        Assert.DoesNotContain("governance boundary", string.Concat(result.StandardOutput, result.StandardError), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("T-INTEGRATION-STALE-REVIEW", reviewPendingTaskIds, StringComparer.Ordinal);
        Assert.Equal("Abandoned", readyNode["metadata"]!["execution_run_latest_status"]!.GetValue<string>());
        Assert.Null(readyNode["metadata"]!["execution_run_active_id"]);
    }

    [Fact]
    public void TaskRun_ReconcilesForeignQuarantinedLifecycleBeforeDryRunExecution()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-FOREIGN-QUARANTINE");
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-READY-AFTER-QUARANTINE");

        var foreignNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-FOREIGN-QUARANTINE.json");
        var foreignNode = JsonNode.Parse(File.ReadAllText(foreignNodePath))!.AsObject();
        foreignNode["status"] = "blocked";
        foreignNode["last_worker_run_id"] = "RUN-T-INTEGRATION-FOREIGN-QUARANTINE-001";
        foreignNode["last_worker_backend"] = "codex_cli";
        foreignNode["last_recovery_reason"] = "Delegated worker worktree for T-INTEGRATION-FOREIGN-QUARANTINE is quarantined and requires explicit recovery planning.";
        foreignNode["planner_review"] = new JsonObject
        {
            ["verdict"] = "continue",
            ["reason"] = "Synthetic quarantined delegated lifecycle should not keep an unrelated review boundary active.",
            ["acceptance_met"] = false,
            ["boundary_preserved"] = true,
            ["scope_drift_detected"] = false,
            ["follow_up_suggestions"] = new JsonArray(),
        };
        File.WriteAllText(foreignNodePath, foreignNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var worktreeRepository = new JsonWorktreeRuntimeRepository(paths);
        worktreeRepository.Save(new WorktreeRuntimeSnapshot
        {
            Records =
            [
                new WorktreeRuntimeRecord
                {
                    TaskId = "T-INTEGRATION-FOREIGN-QUARANTINE",
                    WorktreePath = Path.Combine(sandbox.RootPath, ".carves-worktrees", "T-INTEGRATION-FOREIGN-QUARANTINE"),
                    RepoRoot = sandbox.RootPath,
                    BaseCommit = "abc123",
                    State = WorktreeRuntimeState.Quarantined,
                    QuarantineReason = "Synthetic foreign quarantine should not block unrelated rerun.",
                    WorkerRunId = "RUN-T-INTEGRATION-FOREIGN-QUARANTINE-001",
                },
            ],
        });

        var result = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "run", "T-INTEGRATION-READY-AFTER-QUARANTINE", "--dry-run");
        using var envelope = JsonDocument.Parse(result.StandardOutput);
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();
        var readyNode = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-READY-AFTER-QUARANTINE.json")))!.AsObject();
        var reviewPendingTaskIds = sessionJson["review_pending_task_ids"]!
            .AsArray()
            .Select(item => item!.GetValue<string>())
            .ToArray();

        Assert.Equal(0, result.ExitCode);
        Assert.True(envelope.RootElement.GetProperty("accepted").GetBoolean());
        Assert.DoesNotContain("governance boundary", string.Concat(result.StandardOutput, result.StandardError), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("T-INTEGRATION-FOREIGN-QUARANTINE", reviewPendingTaskIds, StringComparer.Ordinal);
        Assert.Equal("Abandoned", readyNode["metadata"]!["execution_run_latest_status"]!.GetValue<string>());
        Assert.Null(readyNode["metadata"]!["execution_run_active_id"]);
    }

    [Fact]
    public void TaskInspect_ReportsExternalResidueBlockerTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticTask("T-INTEGRATION-FOREIGN-RESIDUE", "blocked", "Synthetic foreign residue");
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-INSPECT-BLOCKED");

        var foreignNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-FOREIGN-RESIDUE.json");
        var foreignNode = JsonNode.Parse(File.ReadAllText(foreignNodePath))!.AsObject();
        foreignNode["last_worker_run_id"] = "RUN-T-INTEGRATION-FOREIGN-RESIDUE-001";
        foreignNode["last_worker_backend"] = "codex_cli";
        foreignNode["last_recovery_reason"] = "Delegated worker worktree for T-INTEGRATION-FOREIGN-RESIDUE is quarantined and requires explicit recovery planning.";
        foreignNode["planner_review"] = new JsonObject
        {
            ["verdict"] = "continue",
            ["reason"] = "Synthetic foreign quarantined lifecycle remains under operator control.",
            ["acceptance_met"] = false,
            ["boundary_preserved"] = true,
            ["scope_drift_detected"] = false,
            ["follow_up_suggestions"] = new JsonArray(),
        };
        File.WriteAllText(foreignNodePath, foreignNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var targetNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-INSPECT-BLOCKED.json");
        var targetNode = JsonNode.Parse(File.ReadAllText(targetNodePath))!.AsObject();
        targetNode["planner_review"] = new JsonObject
        {
            ["verdict"] = "continue",
            ["reason"] = "Synthetic dispatch blocker references T-INTEGRATION-FOREIGN-RESIDUE quarantine for operator follow-up.",
            ["acceptance_met"] = false,
            ["boundary_preserved"] = true,
            ["scope_drift_detected"] = false,
            ["follow_up_suggestions"] = new JsonArray(),
        };
        File.WriteAllText(targetNodePath, targetNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var worktreeRepository = new JsonWorktreeRuntimeRepository(paths);
        worktreeRepository.Save(new WorktreeRuntimeSnapshot
        {
            Records =
            [
                new WorktreeRuntimeRecord
                {
                    TaskId = "T-INTEGRATION-FOREIGN-RESIDUE",
                    WorktreePath = Path.Combine(sandbox.RootPath, ".carves-worktrees", "T-INTEGRATION-FOREIGN-RESIDUE"),
                    RepoRoot = sandbox.RootPath,
                    BaseCommit = "abc123",
                    State = WorktreeRuntimeState.Quarantined,
                    QuarantineReason = "Synthetic foreign quarantine should surface as external residue.",
                    WorkerRunId = "RUN-T-INTEGRATION-FOREIGN-RESIDUE-001",
                },
            ],
        });

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "inspect", "T-INTEGRATION-INSPECT-BLOCKED");
        using var document = JsonDocument.Parse(inspect.StandardOutput);
        var root = document.RootElement;

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal("T-INTEGRATION-FOREIGN-RESIDUE", root.GetProperty("blocker_task_id").GetString());
        Assert.Equal("RUN-T-INTEGRATION-FOREIGN-RESIDUE-001", root.GetProperty("blocker_lifecycle_id").GetString());
        Assert.Equal("external_residue", root.GetProperty("blocker_scope").GetString());
        Assert.True(root.GetProperty("blocked_by_external_residue").GetBoolean());
        Assert.Equal("dispatch_blocked", root.GetProperty("dispatch").GetProperty("state").GetString());
        Assert.Contains("T-INTEGRATION-FOREIGN-RESIDUE", root.GetProperty("blocked_reason").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorCommands_CoverApprovalReviewExplainAndInspection()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var planExitCode = Program.Main(["--repo-root", sandbox.RootPath, "plan-card", ".ai/tasks/cards/CARD-009.md"]);
        sandbox.AddSyntheticTask("T-INTEGRATION-SUGGESTED", "suggested", "Synthetic operator task");
        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-task", "T-INTEGRATION-SUGGESTED");
        var approveExitCode = approve.ExitCode;
        var explainExitCode = Program.Main(["--repo-root", sandbox.RootPath, "explain-task", "T-INTEGRATION-SUGGESTED"]);
        var graphExitCode = Program.Main(["--repo-root", sandbox.RootPath, "show-graph"]);
        var backlogExitCode = Program.Main(["--repo-root", sandbox.RootPath, "show-backlog"]);
        var reviewExitCode = Program.Main(["--repo-root", sandbox.RootPath, "--cold", "review-task", "T-INTEGRATION-SUGGESTED", "complete", "Human", "accepted", "the", "task"]);
        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-SUGGESTED.json"));

        Assert.Equal(0, planExitCode);
        Assert.True(approveExitCode == 0, approve.CombinedOutput);
        Assert.Equal(0, explainExitCode);
        Assert.Equal(0, graphExitCode);
        Assert.Equal(0, backlogExitCode);
        Assert.Equal(0, reviewExitCode);
        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"verdict\": \"complete\"", taskNodeJson, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanCard_RejectsInvalidCardBeforePlanner()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var invalidCardPath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "cards", "CARD-INVALID.md");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidCardPath)!);
        File.WriteAllText(
            invalidCardPath,
            """
            # CARD-INVALID
            Title: Invalid card missing acceptance

            ## Goal
            Prove that plan-card now validates card authoring before planning.
            """);

        var result = RunProgram("--repo-root", sandbox.RootPath, "plan-card", ".ai/tasks/cards/CARD-INVALID.md");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Validation failed.", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("card_acceptance_missing", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void RunCommand_BindsToExternalRepoAndWritesIntoTargetControlPlane()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-EXTERNAL");

        var result = RunProgram("run", sandbox.RootPath, "--dry-run");
        var artifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker", "T-INTEGRATION-EXTERNAL.json");
        var artifactJson = JsonNode.Parse(File.ReadAllText(artifactPath))!.AsObject();

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"Target repo: {sandbox.RootPath}", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(sandbox.RootPath, artifactJson["report"]?["session"]?["repo_root"]?.GetValue<string>());
    }

    [Fact]
    public void RunCommand_RejectsUnsafeTargetWorktreeBinding()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.WriteSystemConfig("""
{
  "repo_name": "UnsafeRepo",
  "worktree_root": "worktrees",
  "max_parallel_tasks": 1,
  "default_test_command": ["dotnet", "test", "CARVES.Runtime.sln"],
  "code_directories": ["src", "tests"],
  "excluded_directories": [".git", "bin", "obj"],
  "sync_markdown_views": true,
  "remove_worktree_on_success": true
}
""");

        var result = RunProgram("run", sandbox.RootPath, "--dry-run");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Configured worktree root must stay outside the target repository", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowGraphAndExplainTask_ExposeCodeGraphContext()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var scan = RunProgram("--repo-root", sandbox.RootPath, "scan-code");
        var graph = RunProgram("--repo-root", sandbox.RootPath, "show-graph");
        var explain = RunProgram("--repo-root", sandbox.RootPath, "explain-task", "T-CARD-014-002");

        Assert.Equal(0, scan.ExitCode);
        Assert.Equal(0, graph.ExitCode);
        Assert.Equal(0, explain.ExitCode);
        Assert.Contains("CodeGraph:", graph.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Read path: summary-first", graph.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Dependencies:", graph.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CodeGraph scope:", explain.StandardOutput, StringComparison.Ordinal);
    }

    private static void WriteEnabledRoleGovernancePolicy(string repoRoot)
    {
        var policyPath = Path.Combine(repoRoot, ".carves-platform", "policies", "role-governance.policy.json");
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllText(policyPath, """
{
  "version": "1.0",
  "controlled_mode_default": false,
  "producer_cannot_self_approve": true,
  "reviewer_cannot_approve_same_task": true,
  "default_role_binding": {
    "producer": "planner",
    "executor": "worker",
    "reviewer": "planner",
    "approver": "operator",
    "scope_steward": "operator",
    "policy_owner": "operator"
  },
  "validation_lab_follow_on_lanes": [
    "approval_recovery",
    "controlled_mode_governance"
  ],
  "role_mode": "enabled",
  "planner_worker_split_enabled": true,
  "worker_delegation_enabled": true,
  "scheduler_auto_dispatch_enabled": true
}
""");
    }

    private static JsonDocument ParseJsonOutput(string output)
    {
        var index = output.IndexOf('{');
        return index >= 0
            ? JsonDocument.Parse(output[index..])
            : throw new InvalidOperationException($"Command output did not contain JSON: {output}");
    }
}
