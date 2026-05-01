using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Tests;

public sealed class WorkerCompletionClaimExtractorTests
{
    [Fact]
    public void Extract_ParsesRequiredCompletionClaimFieldsFromWorkerResponse()
    {
        var result = new WorkerExecutionResult
        {
            Status = WorkerExecutionStatus.Succeeded,
            ChangedFiles = ["src/Fallback.cs"],
            Rationale = string.Join(
                Environment.NewLine,
                "- changed_files: `src/Feature.cs`, `tests/FeatureTests.cs`",
                "- contract_items_satisfied: patch_scope_recorded; validation_recorded",
                "- tests_run: dotnet test tests/FeatureTests.csproj",
                "- evidence_paths: .ai/artifacts/worker-executions/T-CLAIM.json",
                "- known_limitations: none",
                "- next_recommendation: approve after Host validation"),
        };

        var claim = WorkerCompletionClaimExtractor.Extract(result, new ExecutionPacketClosureContract
        {
            CompletionClaimRequired = true,
            CompletionClaimFields =
            [
                "changed_files",
                "contract_items_satisfied",
                "tests_run",
                "evidence_paths",
                "known_limitations",
                "next_recommendation",
            ],
        });

        Assert.True(claim.Required);
        Assert.Equal("present", claim.Status);
        Assert.Empty(claim.MissingFields);
        Assert.Equal(["src/Feature.cs", "tests/FeatureTests.cs"], claim.ChangedFiles);
        Assert.Contains("validation_recorded", claim.ContractItemsSatisfied);
        Assert.Equal(["dotnet test tests/FeatureTests.csproj"], claim.TestsRun);
        Assert.Equal([".ai/artifacts/worker-executions/T-CLAIM.json"], claim.EvidencePaths);
        Assert.Equal(["none"], claim.KnownLimitations);
        Assert.Equal("approve after Host validation", claim.NextRecommendation);
        Assert.False(string.IsNullOrWhiteSpace(claim.RawClaimHash));
    }

    [Fact]
    public void Extract_MarksSuccessfulWorkerResponsePartialWhenRequiredFieldsAreMissing()
    {
        var result = new WorkerExecutionResult
        {
            Status = WorkerExecutionStatus.Succeeded,
            Rationale = "- changed_files: src/Feature.cs",
        };

        var claim = WorkerCompletionClaimExtractor.Extract(result, new ExecutionPacketClosureContract
        {
            CompletionClaimRequired = true,
            CompletionClaimFields = ["changed_files", "tests_run", "next_recommendation"],
        });

        Assert.Equal("partial", claim.Status);
        Assert.Equal(["changed_files"], claim.PresentFields);
        Assert.Equal(["tests_run", "next_recommendation"], claim.MissingFields);
    }

    [Fact]
    public void Extract_ParsesEscapedNewlineClaimFromCliJsonlText()
    {
        var result = new WorkerExecutionResult
        {
            Status = WorkerExecutionStatus.Succeeded,
            Summary = "Codex CLI worker completed.",
            ResponsePreview = "- changed_files: src/Feature.cs\\n- contract_items_satisfied: patch_scope_recorded; scope_hygiene\\n- tests_run: dotnet test\\n- evidence_paths: worker execution artifact\\n- known_limitations: none\\n- next_recommendation: submit for Host review",
        };

        var claim = WorkerCompletionClaimExtractor.Extract(result, new WorkerExecutionPacket
        {
            PacketId = "WEP-T-CLI-CLAIM-v1",
            SourceExecutionPacketId = "EP-T-CLI-CLAIM-v1",
            TaskId = "T-CLI-CLAIM",
            AllowedFiles = ["src/"],
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
            },
        });

        Assert.Equal("present", claim.Status);
        Assert.Equal("passed", claim.PacketValidationStatus);
        Assert.Equal(["src/Feature.cs"], claim.ChangedFiles);
        Assert.Contains("scope_hygiene", claim.ContractItemsSatisfied);
    }

    [Fact]
    public void Attach_CompletesMissingWorkerClaimFromPacketAndAdapterObservations()
    {
        const string taskId = "T-AUTO-CLAIM";
        const string runId = "RUN-AUTO-CLAIM-001";
        var packet = new WorkerExecutionPacket
        {
            PacketId = $"WEP-{taskId}-v1",
            SourceExecutionPacketId = $"EP-{taskId}-v1",
            TaskId = taskId,
            AllowedFiles = ["src/"],
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
        };
        var request = new WorkerRequest
        {
            ExecutionRequest = new WorkerExecutionRequest
            {
                TaskId = taskId,
                WorkerExecutionPacket = packet,
                Packet = new ExecutionPacket
                {
                    PacketId = $"EP-{taskId}-v1",
                    WorkerExecutionPacket = packet,
                    ClosureContract = new ExecutionPacketClosureContract
                    {
                        CompletionClaimRequired = true,
                        CompletionClaimFields = packet.CompletionClaimSchema.Fields,
                    },
                },
            },
            ValidationCommands = [["dotnet", "test", "tests/FeatureTests.csproj"]],
        };
        var result = new WorkerExecutionResult
        {
            TaskId = taskId,
            RunId = runId,
            Status = WorkerExecutionStatus.Succeeded,
            Summary = "Worker completed without a structured completion claim.",
            ChangedFiles = ["src/Feature.cs"],
            CommandTrace =
            [
                new CommandExecutionRecord(
                    ["dotnet", "test", "tests/FeatureTests.csproj"],
                    0,
                    "ok",
                    string.Empty,
                    false,
                    "/tmp/worktree",
                    "worker",
                    DateTimeOffset.UtcNow),
            ],
        };

        var attached = WorkerCompletionClaimExtractor.Attach(request, result);
        var claim = attached.CompletionClaim;

        Assert.Equal("present", claim.Status);
        Assert.Equal("worker_execution_packet_adapter_generated", claim.Source);
        Assert.Equal($"WEP-{taskId}-v1", claim.PacketId);
        Assert.Equal($"EP-{taskId}-v1", claim.SourceExecutionPacketId);
        Assert.Equal("passed", claim.PacketValidationStatus);
        Assert.Empty(claim.MissingFields);
        Assert.Equal(["src/Feature.cs"], claim.ChangedFiles);
        Assert.Contains("patch_scope_recorded", claim.ContractItemsSatisfied);
        Assert.Contains("scope_hygiene", claim.ContractItemsSatisfied);
        Assert.Equal(["dotnet test tests/FeatureTests.csproj"], claim.TestsRun);
        Assert.Contains($".ai/artifacts/worker-executions/{runId}/evidence.json", claim.EvidencePaths);
        Assert.Contains($".ai/artifacts/worker-executions/{runId}/command.log", claim.EvidencePaths);
        Assert.Contains($".ai/artifacts/worker-executions/{runId}/test.log", claim.EvidencePaths);
        Assert.Contains($".ai/artifacts/worker-executions/{runId}/patch.diff", claim.EvidencePaths);
        Assert.Contains($".ai/execution/{taskId}/result.json", claim.EvidencePaths);
        Assert.Equal(["not_declared"], claim.KnownLimitations);
        Assert.Equal("submit for Host review", claim.NextRecommendation);
        Assert.False(claim.ClaimIsTruth);
        Assert.True(claim.HostValidationRequired);
    }

    [Fact]
    public void Attach_CompletesNarrativeClaimWithoutSyntheticNoneChangedFile()
    {
        const string taskId = "T-NARRATIVE-CLAIM";
        const string runId = "RUN-NARRATIVE-CLAIM-001";
        var packet = new WorkerExecutionPacket
        {
            PacketId = $"WEP-{taskId}-v1",
            SourceExecutionPacketId = $"EP-{taskId}-v1",
            TaskId = taskId,
            AllowedFiles = [".ai/STATE.md"],
            RequiredContractMatrix =
            [
                "completion_claim_recorded",
                "result_channel_recorded",
                "scope_hygiene",
            ],
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
        };
        var request = new WorkerRequest
        {
            ExecutionRequest = new WorkerExecutionRequest
            {
                TaskId = taskId,
                RoutingIntent = "failure_summary",
                WorkerExecutionPacket = packet,
                Packet = new ExecutionPacket
                {
                    PacketId = $"EP-{taskId}-v1",
                    WorkerExecutionPacket = packet,
                    ClosureContract = new ExecutionPacketClosureContract
                    {
                        CompletionClaimRequired = true,
                        CompletionClaimFields = packet.CompletionClaimSchema.Fields,
                    },
                },
            },
        };
        var result = new WorkerExecutionResult
        {
            TaskId = taskId,
            RunId = runId,
            Status = WorkerExecutionStatus.Succeeded,
            Summary = "Narrative failure summary completed without file materialization.",
            CommandTrace =
            [
                new CommandExecutionRecord(
                    ["remote-api", "openai", "chat_completions"],
                    0,
                    "Narrative failure summary completed.",
                    string.Empty,
                    false,
                    "/tmp/worktree",
                    "remote_api",
                    DateTimeOffset.UtcNow),
            ],
        };

        var attached = WorkerCompletionClaimExtractor.Attach(request, result);
        var claim = attached.CompletionClaim;

        Assert.Equal("present", claim.Status);
        Assert.Equal("worker_execution_packet_adapter_generated", claim.Source);
        Assert.Equal("passed", claim.PacketValidationStatus);
        Assert.Empty(claim.ChangedFiles);
        Assert.Empty(claim.DisallowedChangedFiles);
        Assert.Contains("completion_claim_recorded", claim.ContractItemsSatisfied);
        Assert.Contains("result_channel_recorded", claim.ContractItemsSatisfied);
        Assert.Contains("scope_hygiene", claim.ContractItemsSatisfied);
        Assert.DoesNotContain("none", claim.ChangedFiles);
    }

    [Fact]
    public void Extract_ValidatesCompletionClaimAgainstWorkerExecutionPacket()
    {
        var result = new WorkerExecutionResult
        {
            Status = WorkerExecutionStatus.Succeeded,
            Rationale = string.Join(
                Environment.NewLine,
                "- changed_files: `src/Feature.cs`, `docs/OffScope.md`",
                "- contract_items_satisfied: patch_scope_recorded",
                "- tests_run: dotnet test tests/FeatureTests.csproj",
                "- evidence_paths: .ai/artifacts/worker-executions/T-CLAIM-PACKET.json",
                "- known_limitations: high severity wording is intentionally invalid",
                "- next_recommendation: approve after Host validation"),
        };

        var claim = WorkerCompletionClaimExtractor.Extract(result, new WorkerExecutionPacket
        {
            PacketId = "WEP-T-CLAIM-PACKET-v1",
            SourceExecutionPacketId = "EP-T-CLAIM-PACKET-v1",
            TaskId = "T-CLAIM-PACKET",
            AllowedFiles = ["src/"],
            RequiredContractMatrix =
            [
                "patch_scope_recorded",
                "scope_hygiene",
            ],
            ForbiddenVocabulary = ["high"],
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
        });

        Assert.Equal("invalid", claim.Status);
        Assert.Equal("worker_execution_packet", claim.Source);
        Assert.Equal("WEP-T-CLAIM-PACKET-v1", claim.PacketId);
        Assert.Equal("EP-T-CLAIM-PACKET-v1", claim.SourceExecutionPacketId);
        Assert.False(claim.ClaimIsTruth);
        Assert.True(claim.HostValidationRequired);
        Assert.Equal("failed", claim.PacketValidationStatus);
        Assert.Contains("scope_hygiene", claim.MissingContractItems);
        Assert.Contains("docs/OffScope.md", claim.DisallowedChangedFiles);
        Assert.Contains("high", claim.ForbiddenVocabularyHits);
        Assert.Contains("completion_claim_missing_contract_item:scope_hygiene", claim.PacketValidationBlockers);
        Assert.Contains("completion_claim_disallowed_changed_file:docs/OffScope.md", claim.PacketValidationBlockers);
        Assert.Contains("completion_claim_forbidden_vocabulary:high", claim.PacketValidationBlockers);
    }
}
