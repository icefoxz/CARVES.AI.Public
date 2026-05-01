using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Tests;

public sealed class HostResultValidatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Evaluate_AllowsResultWhenWorkerPacketAndCompletionClaimPass()
    {
        using var workspace = new TemporaryWorkspace();
        const string taskId = "T-HOST-VALIDATOR-ALLOW";
        const string runId = "RUN-T-HOST-VALIDATOR-ALLOW-001";
        const string changedPath = "src/Feature.cs";
        WriteExecutionPacket(workspace, taskId);
        WriteEvidenceFiles(workspace, runId);
        var policy = new ResultValidityPolicy(workspace.Paths);

        var decision = policy.Evaluate(
            taskId,
            CreateEnvelope(taskId, runId, changedPath),
            CreateWorkerArtifact(taskId, runId, changedPath, CreateClaim(taskId, changedPath, status: "present", packetValidationStatus: "passed")),
            runId);

        Assert.True(decision.Valid);
        Assert.Equal("valid", decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_RejectsResultWhenWorkerPacketCompletionClaimFails()
    {
        using var workspace = new TemporaryWorkspace();
        const string taskId = "T-HOST-VALIDATOR-CLAIM-FAIL";
        const string runId = "RUN-T-HOST-VALIDATOR-CLAIM-FAIL-001";
        const string changedPath = "src/Feature.cs";
        WriteExecutionPacket(workspace, taskId);
        WriteEvidenceFiles(workspace, runId);
        var policy = new ResultValidityPolicy(workspace.Paths);

        var decision = policy.Evaluate(
            taskId,
            CreateEnvelope(taskId, runId, changedPath),
            CreateWorkerArtifact(
                taskId,
                runId,
                changedPath,
                CreateClaim(
                    taskId,
                    changedPath,
                    status: "invalid",
                    packetValidationStatus: "failed",
                    packetValidationBlockers: ["completion_claim_missing_contract_item:scope_hygiene"])),
            runId);

        Assert.False(decision.Valid);
        Assert.Equal("completion_claim_not_present", decision.ReasonCode);
        Assert.Contains("completion_claim_packet_validation_not_passed:failed", decision.Message, StringComparison.Ordinal);
        Assert.Contains("completion_claim_packet:completion_claim_missing_contract_item:scope_hygiene", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_RejectsResultWhenHostSeesChangedFileMissingFromClaim()
    {
        using var workspace = new TemporaryWorkspace();
        const string taskId = "T-HOST-VALIDATOR-MISMATCH";
        const string runId = "RUN-T-HOST-VALIDATOR-MISMATCH-001";
        const string changedPath = "src/Feature.cs";
        WriteExecutionPacket(workspace, taskId);
        WriteEvidenceFiles(workspace, runId);
        var policy = new ResultValidityPolicy(workspace.Paths);

        var decision = policy.Evaluate(
            taskId,
            CreateEnvelope(taskId, runId, changedPath),
            CreateWorkerArtifact(taskId, runId, changedPath, CreateClaim(taskId, "src/Other.cs", status: "present", packetValidationStatus: "passed")),
            runId);

        Assert.False(decision.Valid);
        Assert.Equal("completion_claim_missing_changed_file", decision.ReasonCode);
        Assert.Contains("completion_claim_missing_changed_file:src/Feature.cs", decision.Message, StringComparison.Ordinal);
    }

    private static void WriteExecutionPacket(TemporaryWorkspace workspace, string taskId)
    {
        var packet = new ExecutionPacket
        {
            PacketId = $"EP-{taskId}-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-HOST-VALIDATOR",
                TaskId = taskId,
                TaskRevision = 1,
            },
            PlannerIntent = PlannerIntent.Execution,
            WorkerExecutionPacket = new WorkerExecutionPacket
            {
                PacketId = $"WEP-{taskId}-v1",
                SourceExecutionPacketId = $"EP-{taskId}-v1",
                TaskId = taskId,
                AllowedFiles = ["src/"],
                AllowedActions = ["read", "edit", "carves.submit_result"],
                RequiredContractMatrix = ["patch_scope_recorded", "scope_hygiene"],
                CompletionClaimSchema = new WorkerCompletionClaimSchema
                {
                    Required = true,
                    Fields =
                    [
                        "changed_files",
                        "contract_items_satisfied",
                        "tests_run",
                        "evidence_paths",
                        "known_limitations",
                        "next_recommendation",
                    ],
                    ClaimIsTruth = false,
                    HostValidationRequired = true,
                },
                ResultSubmission = new WorkerResultSubmissionContract
                {
                    CandidateResultChannel = $".ai/execution/{taskId}/result.json",
                    HostIngestCommand = $"task ingest-result {taskId}",
                    CandidateOnly = true,
                    ReviewBundleRequired = true,
                    SubmittedByHostOrAdapter = true,
                    WorkerDirectTruthWriteAllowed = false,
                },
            },
        };

        workspace.WriteFile(
            $".ai/runtime/execution-packets/{taskId}.json",
            JsonSerializer.Serialize(packet, JsonOptions));
    }

    private static ResultEnvelope CreateEnvelope(string taskId, string runId, string changedPath)
    {
        return new ResultEnvelope
        {
            TaskId = taskId,
            ExecutionRunId = runId,
            ExecutionEvidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json",
            Status = "success",
            Changes = new ResultEnvelopeChanges
            {
                FilesModified = [changedPath],
                LinesChanged = 4,
            },
            Validation = new ResultEnvelopeValidation
            {
                Build = "not_run",
                Tests = "not_run",
            },
        };
    }

    private static WorkerExecutionArtifact CreateWorkerArtifact(
        string taskId,
        string runId,
        string changedPath,
        WorkerCompletionClaim claim)
    {
        return new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = runId,
                Status = WorkerExecutionStatus.Succeeded,
                ChangedFiles = [changedPath],
                CompletionClaim = claim,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = runId,
                EvidenceSource = ExecutionEvidenceSource.Host,
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                EvidenceStrength = ExecutionEvidenceStrength.Replayable,
                FilesWritten = [changedPath],
                EvidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json",
                CommandLogRef = $".ai/artifacts/worker-executions/{runId}/command.log",
                PatchRef = $".ai/artifacts/worker-executions/{runId}/patch.diff",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedAt = DateTimeOffset.UtcNow,
                ExitStatus = 0,
            },
        };
    }

    private static WorkerCompletionClaim CreateClaim(
        string taskId,
        string changedPath,
        string status,
        string packetValidationStatus,
        IReadOnlyList<string>? packetValidationBlockers = null)
    {
        return new WorkerCompletionClaim
        {
            Required = true,
            Status = status,
            PacketId = $"WEP-{taskId}-v1",
            SourceExecutionPacketId = $"EP-{taskId}-v1",
            ClaimIsTruth = false,
            HostValidationRequired = true,
            PacketValidationStatus = packetValidationStatus,
            PacketValidationBlockers = packetValidationBlockers ?? Array.Empty<string>(),
            PresentFields =
            [
                "changed_files",
                "contract_items_satisfied",
                "tests_run",
                "evidence_paths",
                "known_limitations",
                "next_recommendation",
            ],
            ChangedFiles = [changedPath],
            ContractItemsSatisfied = ["patch_scope_recorded", "scope_hygiene"],
            RequiredContractItems = ["patch_scope_recorded", "scope_hygiene"],
            TestsRun = ["host focused validation pending"],
            EvidencePaths = [$".ai/artifacts/worker-executions/{taskId}.json"],
            KnownLimitations = ["none"],
            NextRecommendation = "submit for Host review",
        };
    }

    private static void WriteEvidenceFiles(TemporaryWorkspace workspace, string runId)
    {
        workspace.WriteFile($".ai/artifacts/worker-executions/{runId}/evidence.json", "{}");
        workspace.WriteFile($".ai/artifacts/worker-executions/{runId}/command.log", "ok");
        workspace.WriteFile($".ai/artifacts/worker-executions/{runId}/patch.diff", "diff --git a/src/Feature.cs b/src/Feature.cs");
    }
}
