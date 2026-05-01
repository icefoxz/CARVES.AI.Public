using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeBrokeredExecutionHostContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [Fact]
    public void InspectAndApiRuntimeBrokeredExecution_ProjectPacketResultAndEnforcementState()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask(
            "T-INTEGRATION-BROKERED-EXECUTION",
            scope: ["src/CARVES.Runtime.Host/Program.cs"]);

        const string taskId = "T-INTEGRATION-BROKERED-EXECUTION";
        const string runId = "RUN-T-INTEGRATION-BROKERED-EXECUTION-001";
        WriteExecutionPacket(sandbox.RootPath, taskId);
        WriteResultEnvelope(sandbox.RootPath, taskId, runId);
        WriteWorkerExecutionArtifact(sandbox.RootPath, taskId, runId);

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-brokered-execution", taskId);
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-brokered-execution", taskId);

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime brokered execution", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E profile: mode_e_brokered_execution", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Brokered execution state: result_ready_for_review", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains($"Result return channel: .ai/execution/{taskId}/result.json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Result return payload status: returned_ready_for_review", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Result return official truth state: returned_material_not_approved_truth", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Result return missing evidence: (none)", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Packet enforcement verdict: allow", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E review preflight status: ready_for_review_approval", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E review preflight can approve: True", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- check: submit_result_channel_declared | state=satisfied", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("planner review and host writeback", inspect.StandardOutput, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-brokered-execution", root.GetProperty("surface_id").GetString());
        Assert.Equal(taskId, root.GetProperty("task_id").GetString());
        Assert.Equal("mode_e_brokered_execution", root.GetProperty("mode_e_profile_id").GetString());
        Assert.Equal("result_ready_for_review", root.GetProperty("brokered_execution_state").GetString());
        Assert.Equal("returned_ready_for_review", root.GetProperty("result_return_payload_status").GetString());
        Assert.Equal("returned_material_not_approved_truth", root.GetProperty("result_return_official_truth_state").GetString());
        Assert.Equal("allow", root.GetProperty("packet_enforcement_verdict").GetString());
        Assert.True(root.GetProperty("packet_persisted").GetBoolean());
        Assert.Equal($".ai/execution/{taskId}/result.json", root.GetProperty("result_return_channel").GetString());
        Assert.Equal("returned_ready_for_review", root.GetProperty("result_return").GetProperty("payload_status").GetString());
        Assert.False(root.GetProperty("result_return").GetProperty("payload_malformed").GetBoolean());
        Assert.True(root.GetProperty("result_return").GetProperty("payload_valid").GetBoolean());
        Assert.Empty(root.GetProperty("result_return").GetProperty("missing_evidence").EnumerateArray());
        Assert.Contains(root.GetProperty("brokered_checks").EnumerateArray(), check =>
            check.GetProperty("check_id").GetString() == "packet_enforcement_available"
            && check.GetProperty("state").GetString() == "satisfied");
        Assert.Equal("allow", root.GetProperty("packet_enforcement").GetProperty("record").GetProperty("verdict").GetString());
        Assert.Equal("ready_for_review_approval", root.GetProperty("review_preflight").GetProperty("status").GetString());
        Assert.True(root.GetProperty("review_preflight").GetProperty("can_proceed_to_review_approval").GetBoolean());
    }

    [Fact]
    public void InspectAndApiRuntimeBrokeredExecution_ProjectMalformedResultBeforeReviewApproval()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask(
            "T-INTEGRATION-BROKERED-MALFORMED",
            scope: ["src/CARVES.Runtime.Host/Program.cs"]);

        const string taskId = "T-INTEGRATION-BROKERED-MALFORMED";
        WriteExecutionPacket(sandbox.RootPath, taskId);
        var resultPath = Path.Combine(sandbox.RootPath, ".ai", "execution", taskId, "result.json");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        File.WriteAllText(resultPath, "{ malformed brokered result");

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-brokered-execution", taskId);
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-brokered-execution", taskId);

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Brokered execution state: result_blocked_by_result_return_payload", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Result return payload status: malformed", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Result return payload valid: False", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Packet enforcement verdict: result_return_payload_invalid", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- check: result_return_payload_shape | state=missing", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("result_blocked_by_result_return_payload", root.GetProperty("brokered_execution_state").GetString());
        Assert.Equal("malformed", root.GetProperty("result_return_payload_status").GetString());
        Assert.Equal("result_return_payload_invalid", root.GetProperty("packet_enforcement_verdict").GetString());
        Assert.True(root.GetProperty("result_return").GetProperty("payload_malformed").GetBoolean());
        Assert.False(root.GetProperty("result_return").GetProperty("payload_valid").GetBoolean());
        Assert.Contains(root.GetProperty("result_return").GetProperty("payload_issues").EnumerateArray(), item =>
            item.GetString() == "result_envelope_malformed_json");
    }

    [Fact]
    public void InspectAndApiRuntimeBrokeredExecution_ProjectReviewPreflightPacketAndPathBlockers()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask(
            "T-INTEGRATION-BROKERED-PREFLIGHT",
            scope: ["src/CARVES.Runtime.Host/Program.cs"]);

        const string taskId = "T-INTEGRATION-BROKERED-PREFLIGHT";
        const string runId = "RUN-T-INTEGRATION-BROKERED-PREFLIGHT-001";
        WriteExecutionPacket(sandbox.RootPath, taskId);
        WriteResultEnvelope(sandbox.RootPath, taskId, runId, [".ai/tasks/graph.json"]);
        WriteWorkerExecutionArtifact(sandbox.RootPath, taskId, runId, [".ai/tasks/graph.json"]);

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-brokered-execution", taskId);
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-brokered-execution", taskId);

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Brokered execution state: result_blocked_by_packet_enforcement", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Packet enforcement verdict: quarantine", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E review preflight status: blocked", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E review preflight packet scope: mismatch", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mode E review preflight path policy: protected_path_violation", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("packet_scope_mismatch", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("protected_path_policy_violation", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        var preflight = root.GetProperty("review_preflight");
        Assert.Equal("blocked", preflight.GetProperty("status").GetString());
        Assert.False(preflight.GetProperty("can_proceed_to_review_approval").GetBoolean());
        Assert.Equal("mismatch", preflight.GetProperty("packet_scope_status").GetString());
        Assert.Equal("protected_path_violation", preflight.GetProperty("path_policy_status").GetString());
        Assert.Contains(preflight.GetProperty("blockers").EnumerateArray(), blocker =>
            blocker.GetProperty("blocker_id").GetString() == "packet_scope_mismatch");
        Assert.Contains(preflight.GetProperty("blockers").EnumerateArray(), blocker =>
            blocker.GetProperty("blocker_id").GetString() == "protected_path_policy_violation");
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
            Goal = "Synthetic brokered execution fixture.",
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

    private static void WriteResultEnvelope(
        string repoRoot,
        string taskId,
        string runId,
        IReadOnlyList<string>? changedFiles = null)
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
                        FilesModified = changedFiles ?? ["src/CARVES.Runtime.Host/Program.cs"],
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

    private static void WriteWorkerExecutionArtifact(
        string repoRoot,
        string taskId,
        string runId,
        IReadOnlyList<string>? filesWritten = null)
    {
        var evidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json";
        var fullEvidencePath = Path.Combine(repoRoot, evidencePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullEvidencePath)!);
        File.WriteAllText(fullEvidencePath, "{\"taskId\":\"" + taskId + "\"}");
        new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(repoRoot)).SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
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
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = runId,
                WorkerId = "CodexCliWorkerAdapter",
                EvidenceSource = ExecutionEvidenceSource.Host,
                FilesWritten = filesWritten ?? ["src/CARVES.Runtime.Host/Program.cs"],
                EvidencePath = evidencePath,
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                EvidenceStrength = ExecutionEvidenceStrength.Replayable,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedAt = DateTimeOffset.UtcNow,
                ExitStatus = 0,
            },
        });
    }
}
