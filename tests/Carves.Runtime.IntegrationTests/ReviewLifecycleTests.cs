using Carves.Runtime.Host;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Infrastructure.Persistence;
using Carves.Runtime.Application.ControlPlane;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class ReviewLifecycleTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [Fact]
    public void ApproveReview_CompletesTaskAndClearsReviewWait()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-APPROVE");

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", "T-INTEGRATION-APPROVE", "Human", "approved", "the", "change");

        Assert.True(approve.ExitCode == 0, approve.CombinedOutput);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-APPROVE.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", "T-INTEGRATION-APPROVE.json"));
        var mergeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "merge-candidates", "T-INTEGRATION-APPROVE.json"));
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));

        Assert.Contains("\"status\": \"completed\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"approved\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"task_id\": \"T-INTEGRATION-APPROVE\"", mergeJson, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"idle\"", sessionJson, StringComparison.Ordinal);
        Assert.Contains("Review approved for T-INTEGRATION-APPROVE", sessionJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewWritebackCommands_DenyExplicitWorkerActorBeforeTruthMutation()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-WORKER-REVIEW-DENIED";
        sandbox.AddSyntheticReviewTask(taskId);

        var reviewTask = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "review-task",
            taskId,
            "complete",
            "--actor-kind",
            "worker",
            "--actor-identity",
            "codex-cli-worker",
            "Worker",
            "claimed",
            "done");
        var approveReview = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "approve-review",
            taskId,
            "--actor-kind",
            "worker",
            "--actor-identity",
            "codex-cli-worker",
            "Worker",
            "approved");
        var rejectReview = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "reject-review",
            taskId,
            "--actor-kind",
            "worker",
            "--actor-identity",
            "codex-cli-worker",
            "Worker",
            "rejected");
        var reopenReview = ProgramHarness.Run(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "reopen-review",
            taskId,
            "--actor-kind",
            "worker",
            "--actor-identity",
            "codex-cli-worker",
            "Worker",
            "reopened");

        var deniedResults = new[] { reviewTask, approveReview, rejectReview, reopenReview };
        Assert.All(deniedResults, result =>
        {
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("worker_review_writeback_denied", result.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Allowed worker operations: submit_execution_result, submit_completion_claim", result.CombinedOutput, StringComparison.Ordinal);
        });

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var mergeCandidatePath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "merge-candidates", $"{taskId}.json");
        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"status\": \"completed\"", taskNodeJson, StringComparison.Ordinal);
        Assert.False(File.Exists(mergeCandidatePath));
    }

    [Fact]
    public void ApproveReview_MaterializesApprovedDelegatedFilesAndPersistsWritebackTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-APPROVE-WRITEBACK";
        var relativePath = "src/Synthetic/ApprovedWriteback.cs";
        sandbox.AddSyntheticReviewTask(taskId, scope: [relativePath]);
        var worktreePath = Path.Combine(sandbox.RootPath, ".carves-worktrees", taskId);
        var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "ApprovedWriteback.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class ApprovedWriteback {}");

        var artifactRepository = new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath));
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                WorktreePath = worktreePath,
                FilesWritten = [relativePath],
            },
        });

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "delegated", "writeback");
        Assert.True(approve.ExitCode == 0, approve.CombinedOutput);

        var targetPath = Path.Combine(sandbox.RootPath, "src", "Synthetic", "ApprovedWriteback.cs");
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));
        var mergeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "merge-candidates", $"{taskId}.json"));
        var taskJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewNode = JsonNode.Parse(reviewJson)!.AsObject();
        var mergeNode = JsonNode.Parse(mergeJson)!.AsObject();
        var taskNode = JsonNode.Parse(taskJson)!.AsObject();
        var reviewResultCommit = reviewNode["result_commit"]?.GetValue<string?>();
        var mergeResultCommit = mergeNode["result_commit"]?.GetValue<string>() ?? string.Empty;
        var taskResultCommit = taskNode["result_commit"]?.GetValue<string?>();
        var reviewWritebackCommit = reviewNode["writeback"]?["result_commit"]?.GetValue<string?>();

        Assert.True(File.Exists(targetPath));
        Assert.Equal("namespace Synthetic; public sealed class ApprovedWriteback {}", File.ReadAllText(targetPath));
        Assert.Contains("\"writeback\": {", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"applied\": true", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"files\": [", reviewJson, StringComparison.Ordinal);
        Assert.Contains(relativePath, reviewJson, StringComparison.Ordinal);
        Assert.Equal(reviewResultCommit, taskResultCommit);
        Assert.Equal(reviewResultCommit, reviewWritebackCommit);
        Assert.Equal(reviewResultCommit ?? string.Empty, mergeResultCommit);
        Assert.Contains("\"writeback\": {", mergeJson, StringComparison.Ordinal);
        Assert.Contains("\"applied\": true", mergeJson, StringComparison.Ordinal);
        Assert.Contains("Materialized 1 approved file(s)", approve.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectReview_ReturnsTaskToPendingAndClearsReviewWait()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-REJECT-LIFECYCLE");

        var reject = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "reject-review", "T-INTEGRATION-REJECT-LIFECYCLE", "Needs", "another", "pass");

        Assert.True(reject.ExitCode == 0, reject.CombinedOutput);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-REJECT-LIFECYCLE.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", "T-INTEGRATION-REJECT-LIFECYCLE.json"));
        var failureJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "last_failure.json"));
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));

        Assert.Contains("\"status\": \"pending\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"rejected\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"failure_type\": \"review_rejected\"", failureJson, StringComparison.Ordinal);
        Assert.Contains("\"action\": \"retry_task\"", failureJson, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"idle\"", sessionJson, StringComparison.Ordinal);
        Assert.Contains("Review rejected for T-INTEGRATION-REJECT-LIFECYCLE", sessionJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewBlock_SettlesTaskToBlockedAndClearsReviewWait()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-REVIEW-BLOCK";
        sandbox.AddSyntheticReviewTask(taskId);

        var block = CliProgramHarness.RunInDirectory(sandbox.RootPath, "--repo-root", sandbox.RootPath, "--cold", "review", "block", taskId, "bounded", "review", "blocked", "the", "line");

        Assert.True(block.ExitCode == 0, block.CombinedOutput);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));

        Assert.Contains("\"status\": \"blocked\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"blocked\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"blocked\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"resulting_status\": \"blocked\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"idle\"", sessionJson, StringComparison.Ordinal);
        Assert.Contains("Review recorded for T-INTEGRATION-REVIEW-BLOCK", sessionJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewSupersede_SettlesTaskToSupersededAndClearsReviewWait()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-REVIEW-SUPERSEDE";
        sandbox.AddSyntheticReviewTask(taskId);
        const string readyTaskId = "T-INTEGRATION-READY-AFTER-SUPERSEDE";
        sandbox.AddSyntheticPendingTask(readyTaskId);

        var supersede = CliProgramHarness.RunInDirectory(sandbox.RootPath, "--repo-root", sandbox.RootPath, "--cold", "review", "supersede", taskId, "replaced", "by", "governed", "aggregation");
        var inspect = CliProgramHarness.RunInDirectory(
            sandbox.RootPath,
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "task",
            "inspect",
            readyTaskId);

        Assert.True(supersede.ExitCode == 0, supersede.CombinedOutput);
        Assert.True(inspect.ExitCode == 0, inspect.CombinedOutput);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));
        using var sessionDocument = JsonDocument.Parse(sessionJson);
        var reviewPendingTaskIds = sessionDocument.RootElement.GetProperty("review_pending_task_ids")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(static item => item is not null)
            .Cast<string>()
            .ToArray();

        Assert.Contains("\"status\": \"superseded\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"superseded\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"superseded\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"resulting_status\": \"superseded\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"idle\"", sessionJson, StringComparison.Ordinal);
        Assert.Contains("Review recorded for T-INTEGRATION-REVIEW-SUPERSEDE", sessionJson, StringComparison.Ordinal);
        Assert.DoesNotContain(taskId, reviewPendingTaskIds, StringComparer.Ordinal);
        Assert.DoesNotContain("waiting for review", inspect.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"blocker_scope\": \"local_review_boundary\"", inspect.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApproveReview_WithProvisionalFlag_PersistsDebtAndKeepsTaskInReview()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-PROVISIONAL";
        sandbox.AddSyntheticReviewTask(taskId);
        WriteAcceptanceContract(sandbox.RootPath, taskId, provisionalAllowed: true, evidenceTypes: ["test_output"]);

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "--provisional", "Bounded", "follow-up", "remains");

        Assert.True(approve.ExitCode == 0, approve.CombinedOutput);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));
        var mergeCandidatePath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "merge-candidates", $"{taskId}.json");

        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"provisional_accepted\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_debt\":", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"provisional_accepted\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"provisional_accepted\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_debt\":", reviewJson, StringComparison.Ordinal);
        Assert.Contains("test_output", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"resulting_status\": \"review\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"review_wait\"", sessionJson, StringComparison.Ordinal);
        Assert.False(File.Exists(mergeCandidatePath));
        Assert.Contains("Provisionally accepted review", approve.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveReview_BlocksWhenAcceptanceContractEvidenceIsMissing()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-EVIDENCE-GATE";
        sandbox.AddSyntheticReviewTask(taskId);
        WriteAcceptanceContract(sandbox.RootPath, taskId, evidenceTypes: ["test_output"]);

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "without", "required", "evidence");

        Assert.NotEqual(0, approve.ExitCode);
        Assert.Contains("acceptance contract evidence is missing", approve.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test_output", approve.CombinedOutput, StringComparison.Ordinal);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));

        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"pending_review\"", reviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveReview_BlocksWhenNoWorkerExecutionEvidenceIsRecorded()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-NO-WORKER-EVIDENCE";
        sandbox.AddSyntheticReviewTask(taskId);

        var workerArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-executions", $"{taskId}.json");
        File.Delete(workerArtifactPath);

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "without", "worker", "evidence");

        Assert.NotEqual(0, approve.ExitCode);
        Assert.Contains("worker_execution_evidence", approve.CombinedOutput, StringComparison.Ordinal);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));

        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"pending_review\"", reviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveReview_BlocksWhenClosureDecisionDoesNotAllowWriteback()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-CLOSURE-GATE";
        sandbox.AddSyntheticReviewTask(taskId, scope: ["src/Synthetic/ClosureGate.cs"]);

        var reviewPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json");
        var reviewArtifact = JsonNode.Parse(File.ReadAllText(reviewPath))!.AsObject();
        reviewArtifact["patch_summary"] = "files=0; lines=0; paths=(none)";
        File.WriteAllText(reviewPath, reviewArtifact.ToJsonString(JsonOptions));

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "without", "closure", "evidence");

        Assert.NotEqual(0, approve.ExitCode);
        Assert.Contains("closure decision blocks writeback_allowed=false", approve.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("contract_matrix", approve.CombinedOutput, StringComparison.OrdinalIgnoreCase);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(reviewPath);

        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"pending_review\"", reviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveReview_BlocksWhenFinalHostValidationRejectsPacketBoundResult()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-FINAL-HOST-VALIDATION";
        const string runId = "RUN-T-INTEGRATION-FINAL-HOST-VALIDATION-001";
        const string relativePath = "src/Synthetic/FinalHostValidation.cs";
        sandbox.AddSyntheticReviewTask(taskId, scope: [relativePath]);
        WriteResultEnvelope(sandbox.RootPath, taskId, runId, [relativePath]);

        new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath)).SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = runId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
                ChangedFiles = [relativePath],
                CompletionClaim = new WorkerCompletionClaim
                {
                    Required = true,
                    Status = "present",
                    PacketId = $"WEP-{taskId}-v1",
                    SourceExecutionPacketId = $"EP-{taskId}-v1",
                    ClaimIsTruth = false,
                    HostValidationRequired = true,
                    PacketValidationStatus = "passed",
                    PresentFields =
                    [
                        "changed_files",
                        "contract_items_satisfied",
                        "tests_run",
                        "evidence_paths",
                        "known_limitations",
                        "next_recommendation",
                    ],
                    ChangedFiles = [relativePath],
                    ContractItemsSatisfied = ["patch_scope_recorded", "scope_hygiene"],
                    RequiredContractItems = ["patch_scope_recorded", "scope_hygiene"],
                    TestsRun = ["dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj --filter FullyQualifiedName~ReviewLifecycleTests"],
                    EvidencePaths = [$".ai/artifacts/worker-executions/{runId}/evidence.json"],
                    KnownLimitations = ["none"],
                    NextRecommendation = "submit for Host review",
                },
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = runId,
                EvidenceSource = ExecutionEvidenceSource.Host,
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                EvidenceStrength = ExecutionEvidenceStrength.Replayable,
                EvidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json",
                CommandLogRef = $".ai/artifacts/worker-executions/{runId}/command.log",
                PatchRef = $".ai/artifacts/worker-executions/{runId}/patch.diff",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedAt = DateTimeOffset.UtcNow,
                ExitStatus = 0,
                CommandsExecuted = ["dotnet test"],
            },
        });

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "packet", "result");

        Assert.NotEqual(0, approve.ExitCode);
        Assert.Contains("closure decision blocks writeback_allowed=false", approve.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("host_validation_not_passed:failed", approve.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("host_validation:evidence_artifact_missing", approve.CombinedOutput, StringComparison.OrdinalIgnoreCase);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));

        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"pending_review\"", reviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveReview_BlocksWhenManualReviewNoteRequirementIsMissing()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-MANUAL-REVIEW-NOTE";
        sandbox.AddSyntheticReviewTask(taskId);
        WriteAcceptanceContract(sandbox.RootPath, taskId, evidenceTypes: ["manual_review_note"]);

        var reviewPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json");
        var reviewArtifact = JsonNode.Parse(File.ReadAllText(reviewPath))!.AsObject();
        reviewArtifact["review"]!["reason"] = string.Empty;
        reviewArtifact["planner_comment"] = string.Empty;
        reviewArtifact["decision_reason"] = null;
        File.WriteAllText(reviewPath, reviewArtifact.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "without", "review", "note");

        Assert.NotEqual(0, approve.ExitCode);
        Assert.Contains("manual_review_note", approve.CombinedOutput, StringComparison.Ordinal);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(reviewPath);

        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"pending_review\"", reviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveReview_BlocksWhenNullWorkerProvidesOnlyDiagnosticEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-NULL-WORKER-REVIEW";
        sandbox.AddSyntheticReviewTask(taskId);

        new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath)).SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                BackendId = "null_worker",
                ProviderId = "null",
                AdapterId = "NullWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                EvidenceStrength = ExecutionEvidenceStrength.Observed,
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Partial,
            },
        });

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "null", "worker", "result");

        Assert.NotEqual(0, approve.ExitCode);
        Assert.Contains("null_worker", approve.CombinedOutput, StringComparison.OrdinalIgnoreCase);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));

        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"pending_review\"", reviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveReview_BlocksBeforeMaterializingFilesWhenResultCommitCannotBeProduced()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-RESULT-COMMIT-GATE";
        var relativePath = "src/Synthetic/BlockedWriteback.cs";
        sandbox.AddSyntheticReviewTask(taskId, scope: [relativePath]);
        WriteAcceptanceContract(sandbox.RootPath, taskId, evidenceTypes: ["result_commit"]);

        var worktreePath = Path.Combine(sandbox.RootPath, ".carves-worktrees", taskId);
        var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "BlockedWriteback.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class BlockedWriteback {}");

        var artifactRepository = new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath));
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                WorktreePath = worktreePath,
                FilesWritten = [relativePath],
            },
        });

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "without", "git", "commit");

        Assert.NotEqual(0, approve.ExitCode);
        Assert.Contains("result_commit", approve.CombinedOutput, StringComparison.Ordinal);

        var targetPath = Path.Combine(sandbox.RootPath, "src", "Synthetic", "BlockedWriteback.cs");
        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));

        Assert.False(File.Exists(targetPath));
        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"pending_review\"", reviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskInspectAndWorkbenchReview_ProjectPredictedMissingEvidenceBeforeApproval()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-EVIDENCE-PROJECTION";
        var relativePath = "src/Synthetic/ProjectionSurface.cs";
        sandbox.AddSyntheticReviewTask(taskId, scope: [relativePath]);
        WriteAcceptanceContract(sandbox.RootPath, taskId, evidenceTypes: ["result_commit"]);
        WriteReviewCompletionClaim(
            sandbox.RootPath,
            taskId,
            status: "partial",
            missingFields: ["tests_run", "next_recommendation"],
            evidencePaths: [$".ai/artifacts/worker-executions/{taskId}.json"]);

        var worktreePath = Path.Combine(sandbox.RootPath, ".carves-worktrees", taskId);
        var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "ProjectionSurface.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class ProjectionSurface {}");

        var artifactRepository = new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath));
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                WorktreePath = worktreePath,
                FilesWritten = [relativePath],
            },
        });

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "task", "inspect", taskId);
        var workbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "workbench", "review");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, workbench.ExitCode);

        var inspectNode = JsonNode.Parse(inspect.StandardOutput)!.AsObject();
        var reviewEvidenceGate = inspectNode["review_evidence_gate"]!.AsObject();

        Assert.Equal("post_writeback_gap", reviewEvidenceGate["status"]!.GetValue<string>());
        Assert.False(reviewEvidenceGate["can_final_approve"]!.GetValue<bool>());
        Assert.True(reviewEvidenceGate["will_apply_writeback"]!.GetValue<bool>());
        Assert.False(reviewEvidenceGate["will_capture_result_commit"]!.GetValue<bool>());
        Assert.Contains("result_commit", reviewEvidenceGate["summary"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("partial", reviewEvidenceGate["completion_claim_status"]!.GetValue<string>());
        Assert.True(reviewEvidenceGate["completion_claim_required"]!.GetValue<bool>());
        Assert.Contains("tests_run", reviewEvidenceGate["completion_claim_missing_fields"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.Contains("evidence=post_writeback_gap", workbench.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("result_commit", workbench.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Worker completion claim: status=partial; required=True", workbench.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Completion claim missing fields: tests_run, next_recommendation", workbench.StandardOutput, StringComparison.Ordinal);
        Assert.Contains($".ai/artifacts/worker-executions/{taskId}.json", workbench.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainTask_ProjectsPredictedReviewEvidenceBeforeApproval()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-EVIDENCE-EXPLAIN";
        var relativePath = "src/Synthetic/ExplainProjection.cs";
        sandbox.AddSyntheticReviewTask(taskId, scope: [relativePath]);
        WriteAcceptanceContract(sandbox.RootPath, taskId, evidenceTypes: ["result_commit"]);

        var worktreePath = Path.Combine(sandbox.RootPath, ".carves-worktrees", taskId);
        var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "ExplainProjection.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class ExplainProjection {}");

        var artifactRepository = new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath));
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                WorktreePath = worktreePath,
                FilesWritten = [relativePath],
            },
        });

        var explain = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "explain-task", taskId);

        Assert.Equal(0, explain.ExitCode);
        Assert.Contains("Review evidence:", explain.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- status: post_writeback_gap", explain.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- can final approve: False", explain.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- will capture result commit: False", explain.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("result_commit", explain.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ReopenReview_ReturnsAcceptedTaskToReviewWaitAndInvalidatesMergeCandidate()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-REOPEN";
        sandbox.AddSyntheticReviewTask(taskId);
        WriteAcceptanceContract(sandbox.RootPath, taskId);

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "the", "change");
        Assert.True(approve.ExitCode == 0, approve.CombinedOutput);

        var reopen = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "reopen-review", taskId, "Need", "one", "more", "review", "pass");
        Assert.True(reopen.ExitCode == 0, reopen.CombinedOutput);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));
        var mergeCandidatePath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "merge-candidates", $"{taskId}.json");

        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"reopened\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"reopened\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"reopened\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"resulting_status\": \"review\"", reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"review_wait\"", sessionJson, StringComparison.Ordinal);
        Assert.Contains("Review reopened for T-INTEGRATION-REOPEN", sessionJson, StringComparison.Ordinal);
        Assert.False(File.Exists(mergeCandidatePath));
        Assert.Contains("Reopened review for T-INTEGRATION-REOPEN", reopen.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveReview_BlocksWhenRoleBindingViolatesProducerCannotSelfApprove()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-ROLE-GOVERNANCE";
        sandbox.AddSyntheticReviewTask(taskId);
        sandbox.SetTaskMetadata(taskId, TaskRoleBindingMetadata.ProducerKey, "operator");
        sandbox.SetTaskMetadata(taskId, TaskRoleBindingMetadata.ApproverKey, "operator");

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "attempted", "approval");

        Assert.NotEqual(0, approve.ExitCode);
        Assert.Contains("producer_cannot_self_approve", approve.CombinedOutput, StringComparison.OrdinalIgnoreCase);

        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));

        Assert.Contains("\"status\": \"review\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"pending_review\"", reviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveReview_BlocksModeEPacketMismatchBeforeWriteback()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var taskId = "T-INTEGRATION-MODE-E-PREFLIGHT-APPROVE";
        var offPacketPath = "docs/ModeEOffPacket.md";
        sandbox.AddSyntheticReviewTask(taskId, scope: ["src/CARVES.Runtime.Host/Program.cs"]);
        WriteExecutionPacket(sandbox.RootPath, taskId);
        WriteResultEnvelope(sandbox.RootPath, taskId, $"RUN-{taskId}-001", [offPacketPath]);

        var worktreePath = Path.Combine(sandbox.RootPath, ".carves-worktrees", taskId);
        var sourcePath = Path.Combine(worktreePath, offPacketPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "# off-packet");
        new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath)).SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                WorktreePath = worktreePath,
                FilesWritten = [offPacketPath],
            },
        });

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "off", "packet");

        Assert.NotEqual(0, approve.ExitCode);
        Assert.Contains("Mode E review preflight is blocked before writeback", approve.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("packet_scope_mismatch", approve.CombinedOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(sandbox.RootPath, offPacketPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void ApproveReview_AllowsGovernedPatternMarkdownWritebackForAuthorizedRoute()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-PATTERN-WRITEBACK";
        const string targetPath = ".ai/memory/patterns/benchmark_memory_update_promotion_gate.md";
        const string routePath = "artifacts/bench/memory-maturity/integration-pattern-writeback-001/pattern-writeback-route/memory_pattern_writeback_route.json";
        const string draftPath = "artifacts/bench/memory-maturity/integration-pattern-writeback-001/pattern-markdown/benchmark_memory_update_promotion_gate.draft.md";
        const string markdownBody = """
# Benchmark Memory Update Promotion Gate

## Rule
- Treat benchmark-derived memory promotion as a governed host-routed route, not as direct worker truth writeback.

## Boundaries
- Benchmark uplift claims remain blocked.
        - Durable markdown writeback stays host-routed.
""";

        sandbox.AddSyntheticReviewTask(taskId, scope: ["src/CARVES.Runtime.Host/Program.cs"]);
        WriteAcceptanceContract(sandbox.RootPath, taskId, evidenceTypes: ["memory_write"]);
        WriteExecutionPacket(sandbox.RootPath, taskId);
        WriteResultEnvelope(sandbox.RootPath, taskId, $"RUN-{taskId}-001", [targetPath]);

        var repoDraftPath = Path.Combine(sandbox.RootPath, draftPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(repoDraftPath)!);
        File.WriteAllText(repoDraftPath, markdownBody);

        var repoRoutePath = Path.Combine(sandbox.RootPath, routePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(repoRoutePath)!);
        File.WriteAllText(
            repoRoutePath,
            JsonSerializer.Serialize(
                new
                {
                    schema_version = "benchmark-memory-pattern-writeback-route.v1",
                    writeback_route_id = "mempattern-writeback-route-integration",
                    draft_id = "mempattern-draft-integration",
                    canonical_fact_id = "MEMFACT-003",
                    canonical_promotion_record_id = "MEMPROM-003",
                    category = "patterns",
                    target_memory_path = targetPath,
                    source = new
                    {
                        benchmark = "swebench",
                        phase5_run_id = "integration-pattern-writeback-001",
                        draft_markdown_artifact_path = draftPath,
                    },
                    route_status = "completed",
                    route_decision = "host_writeback_line_required",
                    current_posture = "durable_markdown_writeback_input_ready",
                    requested_host_action = "prepare_host_pattern_markdown_writeback",
                    next_action = "create_host_pattern_markdown_writeback_line",
                    durable_markdown_writeback_input_ready = true,
                    durable_markdown_write_authorized = false,
                    benchmark_uplift_claim_authorized = false,
                    canonical_fact_rewrite_authorized = false,
                },
                JsonOptions));

        var worktreePath = Path.Combine(sandbox.RootPath, ".carves-worktrees", taskId);
        var worktreeTargetPath = Path.Combine(worktreePath, targetPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(worktreeTargetPath)!);
        File.WriteAllText(worktreeTargetPath, markdownBody);

        new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath)).SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                WorktreePath = worktreePath,
                FilesRead = [draftPath],
                FilesWritten = [targetPath],
            },
        });

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", taskId, "Human", "approved", "governed", "pattern", "writeback");

        Assert.True(approve.ExitCode == 0, approve.CombinedOutput);

        var targetFilePath = Path.Combine(sandbox.RootPath, targetPath.Replace('/', Path.DirectorySeparatorChar));
        var reviewJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json"));
        var taskJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));

        Assert.True(File.Exists(targetFilePath));
        Assert.Equal(markdownBody, File.ReadAllText(targetFilePath));
        Assert.Contains("\"applied\": true", reviewJson, StringComparison.Ordinal);
        Assert.Contains(targetPath, reviewJson, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"completed\"", taskJson, StringComparison.Ordinal);
        Assert.Contains("Materialized 1 approved file(s)", approve.StandardOutput, StringComparison.Ordinal);
    }

    private static void WriteAcceptanceContract(
        string repoRoot,
        string taskId,
        bool provisionalAllowed = false,
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
                ["description"] = $"Integration proof requires {evidenceType}.",
            });
        }

        taskNode["acceptance_contract"] = new JsonObject
        {
            ["contract_id"] = $"AC-{taskId}",
            ["title"] = $"Acceptance contract for {taskId}",
            ["status"] = "human_review",
            ["owner"] = "planner",
            ["created_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["intent"] = new JsonObject
            {
                ["goal"] = "Allow provisional acceptance for a bounded follow-up.",
                ["business_value"] = "Capture debt explicitly instead of forcing a false green decision.",
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
            ["non_goals"] = new JsonArray(),
            ["evidence_required"] = evidenceRequired,
            ["human_review"] = new JsonObject
            {
                ["required"] = true,
                ["provisional_allowed"] = provisionalAllowed,
                ["decisions"] = provisionalAllowed
                    ? new JsonArray("accept", "provisional_accept", "reject", "reopen")
                    : new JsonArray("accept", "reject", "reopen"),
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

    private static void WriteExecutionPacket(string repoRoot, string taskId)
    {
        var packet = new ExecutionPacket
        {
            PacketId = $"EP-{taskId}-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-INTEGRATION",
                TaskId = taskId,
                TaskRevision = 1,
            },
            Goal = "Synthetic Mode E review preflight fixture.",
            PlannerIntent = PlannerIntent.Execution,
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = ["src/"],
                ReadOnlyRoots = ["docs/"],
                TruthRoots = ["carves://truth/tasks", "carves://truth/runtime"],
                RepoMirrorRoots = [".ai/", ".carves-platform/"],
            },
            WorkerAllowedActions = ["read", "edit", "build", "test", "carves.submit_result", "carves.request_replan"],
            PlannerOnlyActions = ["carves.review_task", "carves.sync_state"],
        };

        var packetPath = Path.Combine(repoRoot, ".ai", "runtime", "execution-packets", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(packetPath)!);
        File.WriteAllText(packetPath, JsonSerializer.Serialize(packet, JsonOptions));
    }

    private static void WriteReviewCompletionClaim(
        string repoRoot,
        string taskId,
        string status,
        IReadOnlyList<string> missingFields,
        IReadOnlyList<string> evidencePaths)
    {
        var reviewArtifactPath = Path.Combine(repoRoot, ".ai", "artifacts", "reviews", $"{taskId}.json");
        var reviewArtifact = JsonNode.Parse(File.ReadAllText(reviewArtifactPath))!.AsObject();
        reviewArtifact["closure_bundle"] = new JsonObject
        {
            ["schema_version"] = "review-closure-bundle.v1",
            ["completion_claim"] = new JsonObject
            {
                ["status"] = status,
                ["required"] = true,
                ["present_fields"] = new JsonArray("changed_files", "evidence_paths"),
                ["missing_fields"] = ToJsonArray(missingFields),
                ["evidence_paths"] = ToJsonArray(evidencePaths),
                ["next_recommendation"] = "ask worker to resubmit completion claim fields",
                ["notes"] = new JsonArray("Worker completion claim is readback evidence only; Host validation and Review closure remain authoritative."),
            },
        };
        File.WriteAllText(reviewArtifactPath, reviewArtifact.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static void WriteResultEnvelope(string repoRoot, string taskId, string runId, IReadOnlyList<string> changedFiles)
    {
        var resultPath = Path.Combine(repoRoot, ".ai", "execution", taskId, "result.json");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        File.WriteAllText(
            resultPath,
            JsonSerializer.Serialize(
                new ResultEnvelope
                {
                    TaskId = taskId,
                    ExecutionRunId = runId,
                    ExecutionEvidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json",
                    Status = "success",
                    Changes = new ResultEnvelopeChanges
                    {
                        FilesModified = changedFiles,
                    },
                    Validation = new ResultEnvelopeValidation
                    {
                        Build = "success",
                        Tests = "success",
                    },
                    Next = new ResultEnvelopeNextAction
                    {
                        Suggested = "submit_result",
                    },
                },
                JsonOptions));
    }
}
