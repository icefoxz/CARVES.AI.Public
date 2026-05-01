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
    public void PlanCardPersist_WithoutHost_RequiresExplicitHostEnsure_UnlessCold()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        StopHost(sandbox.RootPath);
        sandbox.MarkAllTasksCompleted();
        var cardPath = WriteHostRoutedPlannerCard(sandbox.RootPath);

        var gated = ProgramHarness.Run("--repo-root", sandbox.RootPath, "plan-card", cardPath, "--persist");
        var cold = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan-card", cardPath, "--persist");

        Assert.NotEqual(0, gated.ExitCode);
        Assert.Contains("host ensure --json", gated.StandardError, StringComparison.Ordinal);

        Assert.Equal(0, cold.ExitCode);
        Assert.Contains("Planned", cold.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void IntentAccept_WithoutHost_RequiresExplicitHostEnsure_UnlessCold()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        StopHost(sandbox.RootPath);
        sandbox.MarkAllTasksCompleted();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        var draft = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
        var gated = ProgramHarness.Run("--repo-root", sandbox.RootPath, "intent", "accept");
        var cold = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "accept");

        Assert.Equal(0, draft.ExitCode);
        Assert.NotEqual(0, gated.ExitCode);
        Assert.Contains("host ensure --json", gated.StandardError, StringComparison.Ordinal);
        Assert.Equal(0, cold.ExitCode);
        Assert.True(File.Exists(acceptedIntentPath));
    }


    [Fact]
    public void PlanCard_WithHost_RoutesThroughResidentHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var cardPath = WriteHostRoutedPlannerCard(sandbox.RootPath);
        const string cardId = "CARD-HOST-PLAN";
        var beforePreviewTaskCount = CountTasksForCard(sandbox.RootPath, cardId);

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var plannerInspectStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "planner-inspect");
            var preview = ProgramHarness.Run("--repo-root", sandbox.RootPath, "plan-card", cardPath);
            var afterPreviewTaskCount = CountTasksForCard(sandbox.RootPath, cardId);
            var persist = ProgramHarness.Run("--repo-root", sandbox.RootPath, "plan-card", cardPath, "--persist");
            var afterPersistTaskCount = CountTasksForCard(sandbox.RootPath, cardId);

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, plannerInspectStatus.ExitCode);
            Assert.Equal(0, preview.ExitCode);
            Assert.Equal(0, persist.ExitCode);
            Assert.Contains("Connected to host:", preview.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Connected to host:", persist.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, beforePreviewTaskCount);
            Assert.Equal(beforePreviewTaskCount, afterPreviewTaskCount);
            Assert.True(afterPersistTaskCount > afterPreviewTaskCount);
            Assert.Contains("Planned", persist.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void TaskIngestResult_WithoutHost_RequiresExplicitHostEnsureAndDoesNotMutateTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        StopHost(sandbox.RootPath);
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-HOST-INGEST-GATE";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);
        var taskNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json");
        var beforeTaskNodeJson = File.ReadAllText(taskNodePath);

        var ingest = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "ingest-result", taskId);
        var afterTaskNodeJson = File.ReadAllText(taskNodePath);

        Assert.NotEqual(0, ingest.ExitCode);
        Assert.Contains("host ensure --json", ingest.CombinedOutput, StringComparison.Ordinal);
        Assert.Equal(beforeTaskNodeJson, afterTaskNodeJson);
    }

    [Fact]
    public void TaskInspectWithRuns_WithHost_ProjectsCandidateReadBeforeDryRun()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-HOST-INSPECT-CANDIDATE";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--json", "--require-capability", "card-task-inspect");
            var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "inspect", taskId, "--runs");
            var taskNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json");
            var taskNode = JsonNode.Parse(File.ReadAllText(taskNodePath))!.AsObject();

            Assert.True(start.ExitCode == 0, start.CombinedOutput);
            Assert.True(status.ExitCode == 0, status.CombinedOutput);
            using var statusDocument = ParseJsonOutput(status.StandardOutput);
            var statusRoot = statusDocument.RootElement;
            var capabilities = statusRoot.GetProperty("capabilities").EnumerateArray().Select(item => item.GetString()).ToArray();
            Assert.Equal("card-task-inspect", statusRoot.GetProperty("required_capability").GetString());
            Assert.Contains("card-task-inspect", capabilities);

            Assert.True(inspect.ExitCode == 0, inspect.CombinedOutput);
            Assert.DoesNotContain("host ensure --json", inspect.CombinedOutput, StringComparison.OrdinalIgnoreCase);
            using var inspectDocument = ParseJsonOutput(inspect.StandardOutput);
            var root = inspectDocument.RootElement;
            Assert.Equal("task", root.GetProperty("kind").GetString());
            Assert.Equal(taskId, root.GetProperty("task_id").GetString());
            Assert.Equal("Pending", root.GetProperty("status").GetString());
            Assert.Equal("dispatchable", root.GetProperty("dispatch").GetProperty("state").GetString());
            Assert.Equal("ready for host dispatch", root.GetProperty("dispatch").GetProperty("reason").GetString());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("execution_run").ValueKind);
            Assert.Equal("pending", taskNode["status"]!.GetValue<string>());
            Assert.False(taskNode["metadata"]!.AsObject().ContainsKey("execution_run_active_id"));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void TaskRunDryRun_WithHost_RoutesThroughResidentHostAndPreservesRoleModeGate()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-HOST-RUN-DRY", scope: ["README.md"]);
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "delegated-execution");
            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", "T-INTEGRATION-HOST-RUN-DRY", "--dry-run");
            var taskNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-HOST-RUN-DRY.json");
            var taskNode = JsonNode.Parse(File.ReadAllText(taskNodePath))!.AsObject();

            Assert.True(start.ExitCode == 0, start.CombinedOutput);
            Assert.True(status.ExitCode == 0, status.CombinedOutput);
            Assert.Contains("CARVES Host summary", status.StandardOutput, StringComparison.Ordinal);
            Assert.True(run.ExitCode != 0, run.CombinedOutput);
            using var runDocument = ParseJsonOutput(run.CombinedOutput);
            var root = runDocument.RootElement;
            Assert.False(root.GetProperty("accepted").GetBoolean());
            Assert.Equal("role_mode_disabled", root.GetProperty("outcome").GetString());
            Assert.Equal("unchanged", root.GetProperty("task_status").GetString());
            Assert.Equal("unchanged", root.GetProperty("session_status").GetString());
            Assert.False(root.GetProperty("host_result_ingestion_attempted").GetBoolean());
            Assert.Null(root.GetProperty("run_id").GetString());
            Assert.Null(root.GetProperty("execution_run_id").GetString());
            Assert.Equal("pending", taskNode["status"]!.GetValue<string>());
            Assert.False(taskNode["metadata"]!.AsObject().ContainsKey("execution_run_active_id"));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void ReviewTask_WithHost_RoutesThroughResidentHostAndMutatesTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-HOST-REVIEW-MUTATION");

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "control-plane-mutation");
            var review = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "review-task",
                "T-INTEGRATION-HOST-REVIEW-MUTATION",
                "complete",
                "host-routed planner mutation proof");

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, status.ExitCode);
            Assert.Equal(0, review.ExitCode);
            Assert.Contains("Connected to host:", review.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Recorded review for T-INTEGRATION-HOST-REVIEW-MUTATION", review.StandardOutput, StringComparison.Ordinal);

            var nodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-HOST-REVIEW-MUTATION.json");
            var taskNode = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
            Assert.Equal("review", taskNode["status"]!.GetValue<string>());
            Assert.Equal("complete", taskNode["planner_review"]!["verdict"]!.GetValue<string>());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void ApproveReview_WithHost_RoutesThroughResidentHostAndMutatesTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-HOST-APPROVE-REVIEW";
        sandbox.AddSyntheticReviewTask(taskId);

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var approve = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "approve-review",
                taskId,
                "host-routed approval mutation proof");

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, approve.ExitCode);
            Assert.Contains("Connected to host:", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Approved review for {taskId}", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("merge-candidate evidence was emitted", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Equal("completed", ReadTaskStatus(sandbox.RootPath, taskId));

            var reviewArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json");
            var mergeCandidatePath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "merge-candidates", $"{taskId}.json");
            var reviewArtifact = JsonNode.Parse(File.ReadAllText(reviewArtifactPath))!.AsObject();
            var mergeCandidate = JsonNode.Parse(File.ReadAllText(mergeCandidatePath))!.AsObject();
            Assert.Equal("approved", reviewArtifact["decision_status"]!.GetValue<string>());
            Assert.Equal(taskId, mergeCandidate["task_id"]!.GetValue<string>());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public async Task HostInvoke_ControlPlaneMutationAcceptsAndExposesPollableCompletionState()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepoWithoutCodegraph();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-HOST-ACCEPTED-REVIEW";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            Assert.Equal(0, start.ExitCode);

            var baseUrl = ReadRepoHostBaseUrl(sandbox.RootPath);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var invokeResponse = await client.PostAsJsonAsync(
                $"{baseUrl}/invoke",
                new
                {
                    command = "review-task",
                    arguments = new[] { taskId, "complete", "accepted operation polling proof" },
                    repo_root = sandbox.RootPath,
                    prefer_accepted_operation_polling = true,
                });
            invokeResponse.EnsureSuccessStatusCode();

            using var invokeDocument = JsonDocument.Parse(await invokeResponse.Content.ReadAsStringAsync());
            var operationId = invokeDocument.RootElement.GetProperty("operation_id").GetString();
            Assert.True(invokeDocument.RootElement.GetProperty("accepted").GetBoolean());
            Assert.False(invokeDocument.RootElement.GetProperty("completed").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(operationId));
            Assert.Equal("accepted", invokeDocument.RootElement.GetProperty("progress_marker").GetString());
            Assert.Equal(0, invokeDocument.RootElement.GetProperty("progress_ordinal").GetInt32());

            var operationStatus = await PollAcceptedOperationStatus(client, baseUrl, operationId!);

            Assert.True(operationStatus.GetProperty("completed").GetBoolean());
            Assert.Equal("completed", operationStatus.GetProperty("operation_state").GetString());
            Assert.Equal("completed", operationStatus.GetProperty("progress_marker").GetString());
            Assert.Equal(0, operationStatus.GetProperty("exit_code").GetInt32());
            Assert.Contains(
                operationStatus.GetProperty("lines").EnumerateArray().Select(item => item.GetString()),
                line => line is not null && line.StartsWith($"Recorded review for {taskId}:", StringComparison.Ordinal));
            Assert.Equal("completed", ReadTaskStatus(sandbox.RootPath, taskId));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void SupersedeCardTasks_WithHost_RoutesThroughResidentHostAndClearsBlockedActionabilityWithoutTouchingCompletedLineage()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticTask("T-INTEGRATION-SUPERSEDE-001", "completed", "Completed lineage entry");
        sandbox.AddSyntheticTask("T-INTEGRATION-SUPERSEDE-002", "blocked", "Blocked stale lineage");
        sandbox.AddSyntheticTask("T-INTEGRATION-SUPERSEDE-003", "blocked", "Blocked dependent stale lineage", dependencies: ["T-INTEGRATION-SUPERSEDE-002"]);
        AssignSyntheticCardId(sandbox.RootPath, "CARD-SUPERSEDE-INTEGRATION", "T-INTEGRATION-SUPERSEDE-001", "T-INTEGRATION-SUPERSEDE-002", "T-INTEGRATION-SUPERSEDE-003");

        var beforeStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "status", "--summary");

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var supersede = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "supersede-card-tasks",
                "CARD-SUPERSEDE-INTEGRATION",
                "superseded",
                "stale",
                "blocked",
                "lineage");
            var afterStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "status", "--summary");
            var cardInspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "inspect-card", "CARD-SUPERSEDE-INTEGRATION");
            var taskInspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "inspect", "T-INTEGRATION-SUPERSEDE-002", "--runs");

            Assert.Equal(0, beforeStatus.ExitCode);
            Assert.Contains("Tasks: 0 running / 2 blocked / 0 review", beforeStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Actionability: blocked", beforeStatus.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, supersede.ExitCode);
            Assert.Contains("Connected to host:", supersede.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Superseded 2 non-finalized task(s) for CARD-SUPERSEDE-INTEGRATION", supersede.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Preserved finalized lineage: T-INTEGRATION-SUPERSEDE-001", supersede.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, afterStatus.ExitCode);
            Assert.Contains("Tasks: 0 running / 0 blocked / 0 review", afterStatus.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Actionability: blocked", afterStatus.StandardOutput, StringComparison.Ordinal);

            Assert.Equal(0, cardInspect.ExitCode);
            Assert.Equal(0, taskInspect.ExitCode);

            using var cardDocument = ParseJsonOutput(cardInspect.StandardOutput);
            using var taskDocument = ParseJsonOutput(taskInspect.StandardOutput);

            var cardRoot = cardDocument.RootElement;
            Assert.Equal("completed", cardRoot.GetProperty("status").GetString());
            Assert.Equal("(none)", cardRoot.GetProperty("blocked_reason").GetString());
            Assert.Equal("observe current state", cardRoot.GetProperty("next_action").GetString());
            Assert.Equal(2, cardRoot.GetProperty("task_statuses").GetProperty("Superseded").GetInt32());

            var taskRoot = taskDocument.RootElement;
            Assert.Equal("Superseded", taskRoot.GetProperty("status").GetString());
            Assert.Equal("(none)", taskRoot.GetProperty("blocked_reason").GetString());
            Assert.Equal("observe current state", taskRoot.GetProperty("next_action").GetString());
            Assert.Equal("dispatch_blocked", taskRoot.GetProperty("dispatch").GetProperty("state").GetString());
            Assert.Equal("task is finalized: Superseded", taskRoot.GetProperty("dispatch").GetProperty("reason").GetString());

            Assert.Equal("completed", ReadTaskStatus(sandbox.RootPath, "T-INTEGRATION-SUPERSEDE-001"));
            Assert.Equal("superseded", ReadTaskStatus(sandbox.RootPath, "T-INTEGRATION-SUPERSEDE-002"));
            Assert.Equal("superseded", ReadTaskStatus(sandbox.RootPath, "T-INTEGRATION-SUPERSEDE-003"));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void ReviewTaskComplete_ReconcilesManualFallbackCompletionWithoutDiscardingFailedRunHistory()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-MANUAL-FALLBACK-COMPLETE";
        const string completionReason = "manual fallback completed after delegated timeout";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["src/SyntheticManualFallback.cs"]);
        SeedFailedDelegatedRun(sandbox.RootPath, taskId);

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var review = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "review-task",
                taskId,
                "complete",
                completionReason);
            var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "inspect", taskId, "--runs");
            var explain = ProgramHarness.Run("--repo-root", sandbox.RootPath, "explain-task", taskId);
            var nodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json");
            using var nodeDocument = JsonDocument.Parse(File.ReadAllText(nodePath));
            using var inspectDocument = ParseJsonOutput(inspect.StandardOutput);

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, review.ExitCode);
            Assert.Equal(0, inspect.ExitCode);
            Assert.Equal(0, explain.ExitCode);

            Assert.Equal("completed", nodeDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("manual_fallback", nodeDocument.RootElement.GetProperty("metadata").GetProperty("completion_provenance").GetString());
            Assert.Equal("ManualFallbackCompleted", nodeDocument.RootElement.GetProperty("metadata").GetProperty("execution_run_latest_status").GetString());
            Assert.Equal("Failed", nodeDocument.RootElement.GetProperty("metadata").GetProperty("completion_historical_run_status").GetString());
            Assert.Equal("manual_fallback_review_boundary_receipt", nodeDocument.RootElement.GetProperty("metadata").GetProperty("fallback_run_packet_role_switch_receipt").GetString());
            Assert.Equal($"manual_fallback_execution_claim:{taskId}", nodeDocument.RootElement.GetProperty("metadata").GetProperty("fallback_run_packet_execution_claim").GetString());
            Assert.Contains($"{taskId}.json#closure_bundle", nodeDocument.RootElement.GetProperty("metadata").GetProperty("fallback_run_packet_review_bundle").GetString(), StringComparison.Ordinal);
            Assert.True(nodeDocument.RootElement.GetProperty("last_worker_run_id").ValueKind == JsonValueKind.Null);
            Assert.True(nodeDocument.RootElement.GetProperty("last_worker_summary").ValueKind == JsonValueKind.Null);
            Assert.True(nodeDocument.RootElement.GetProperty("retry_not_before").ValueKind == JsonValueKind.Null);

            var inspectRoot = inspectDocument.RootElement;
            Assert.Equal("Completed", inspectRoot.GetProperty("status").GetString());
            Assert.Contains(completionReason, inspectRoot.GetProperty("latest_summary").GetString(), StringComparison.Ordinal);
            Assert.Equal("manual_fallback", inspectRoot.GetProperty("completion").GetProperty("mode").GetString());
            Assert.Equal("ManualFallbackCompleted", inspectRoot.GetProperty("execution_run").GetProperty("latest_status").GetString());
            Assert.Equal("Failed", inspectRoot.GetProperty("execution_run").GetProperty("historical_latest_status").GetString());
            Assert.True(inspectRoot.GetProperty("latest_worker_route").GetProperty("historical").GetBoolean());
            Assert.Equal("Failed", inspectRoot.GetProperty("runs")[0].GetProperty("status").GetString());
            Assert.Contains("Completion provenance:", explain.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("manual_fallback", explain.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

}
