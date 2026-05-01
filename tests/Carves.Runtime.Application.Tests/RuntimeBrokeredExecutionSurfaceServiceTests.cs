using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeBrokeredExecutionSurfaceServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [Fact]
    public void Build_WithPersistedPacketAndNoResult_ProjectsAwaitingBrokeredResult()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# Mode E");
        var task = CreateTask("T-MODE-E-001");
        var service = CreateService(workspace, task);
        WriteExecutionPacket(workspace, task.TaskId);

        var surface = service.Build(task.TaskId);

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-brokered-execution", surface.SurfaceId);
        Assert.Equal("mode_e_brokered_execution", surface.ModeEProfileId);
        Assert.Equal("awaiting_brokered_result", surface.BrokeredExecutionState);
        Assert.True(surface.PacketPersisted);
        Assert.Equal("pending_execution", surface.PacketEnforcementVerdict);
        Assert.Equal($".ai/execution/{task.TaskId}/result.json", surface.ResultReturnChannel);
        Assert.Equal("missing", surface.ResultReturn.PayloadStatus);
        Assert.Contains("result_envelope", surface.ResultReturn.MissingEvidence);
        Assert.Contains("worker_execution_artifact", surface.ResultReturn.MissingEvidence);
        Assert.Contains(surface.BrokeredChecks, check => check.CheckId == "submit_result_channel_declared" && check.State == "satisfied");
        Assert.Contains(surface.BrokeredChecks, check => check.CheckId == "planner_owned_lifecycle" && check.State == "satisfied");
        Assert.Contains(surface.BrokeredChecks, check => check.CheckId == "truth_roots_not_worker_editable" && check.State == "satisfied");
        Assert.Contains(surface.BrokeredChecks, check => check.CheckId == "result_return_expected_evidence" && check.State == "missing");
    }

    [Fact]
    public void Build_WithAllowedResult_ProjectsReadyForReview()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# Mode E");
        var task = CreateTask("T-MODE-E-002");
        var service = CreateService(workspace, task);
        WriteExecutionPacket(workspace, task.TaskId);
        WriteResultEnvelope(workspace, task.TaskId, "RUN-T-MODE-E-002-001");
        WriteWorkerExecutionArtifact(workspace, task.TaskId, "RUN-T-MODE-E-002-001");

        var surface = service.Build(task.TaskId);

        Assert.True(surface.IsValid);
        Assert.Equal("result_ready_for_review", surface.BrokeredExecutionState);
        Assert.Equal("allow", surface.PacketEnforcementVerdict);
        Assert.Equal("returned_ready_for_review", surface.ResultReturn.PayloadStatus);
        Assert.Equal("returned_material_not_approved_truth", surface.ResultReturn.OfficialTruthState);
        Assert.Empty(surface.ResultReturn.MissingEvidence);
        Assert.Contains("host writeback", surface.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("planner review", surface.RecommendedNextAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(surface.PacketEnforcement.Record.ReasonCodes, code => code == "packet_scope_respected");
    }

    [Fact]
    public void Build_WithMalformedResult_ProjectsPayloadBlockerBeforeReview()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# Mode E");
        var task = CreateTask("T-MODE-E-003");
        var service = CreateService(workspace, task);
        WriteExecutionPacket(workspace, task.TaskId);
        workspace.WriteFile($".ai/execution/{task.TaskId}/result.json", "{ this is not valid json");

        var surface = service.Build(task.TaskId);

        Assert.True(surface.IsValid);
        Assert.Equal("result_blocked_by_result_return_payload", surface.BrokeredExecutionState);
        Assert.Equal("result_return_payload_invalid", surface.PacketEnforcementVerdict);
        Assert.True(surface.ResultReturn.PayloadPresent);
        Assert.True(surface.ResultReturn.PayloadMalformed);
        Assert.False(surface.ResultReturn.PayloadValid);
        Assert.Equal("malformed", surface.ResultReturn.PayloadStatus);
        Assert.Contains("result_envelope_malformed_json", surface.ResultReturn.PayloadIssues);
        Assert.Contains("valid_result_envelope", surface.ResultReturn.MissingEvidence);
        Assert.Contains(surface.BrokeredChecks, check => check.CheckId == "result_return_payload_shape" && check.State == "missing");
    }

    [Fact]
    public void Build_WithResultEnvelopeButNoWorkerArtifact_ProjectsMissingWritebackEvidence()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# Mode E");
        var task = CreateTask("T-MODE-E-004");
        var service = CreateService(workspace, task);
        WriteExecutionPacket(workspace, task.TaskId);
        WriteResultEnvelope(workspace, task.TaskId, "RUN-T-MODE-E-004-001");

        var surface = service.Build(task.TaskId);

        Assert.True(surface.IsValid);
        Assert.Equal("result_waiting_for_writeback_evidence", surface.BrokeredExecutionState);
        Assert.Equal("allow", surface.PacketEnforcementVerdict);
        Assert.Equal("returned_without_worker_artifact", surface.ResultReturn.PayloadStatus);
        Assert.Equal("returned_material_not_approved_truth", surface.ResultReturn.OfficialTruthState);
        Assert.Contains("worker_execution_artifact", surface.ResultReturn.MissingEvidence);
        Assert.Contains(surface.BrokeredChecks, check => check.CheckId == "result_return_expected_evidence" && check.State == "missing");
    }

    [Fact]
    public void Build_WithOffPacketResult_ProjectsReviewPreflightPacketMismatch()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# Mode E");
        var task = CreateTask("T-MODE-E-005");
        var service = CreateService(workspace, task);
        WriteExecutionPacket(workspace, task.TaskId, editableRoots: ["src/"]);
        WriteResultEnvelope(workspace, task.TaskId, "RUN-T-MODE-E-005-001", changedFiles: ["docs/OffPacket.md"]);
        WriteWorkerExecutionArtifact(workspace, task.TaskId, "RUN-T-MODE-E-005-001", filesWritten: ["docs/OffPacket.md"]);

        var surface = service.Build(task.TaskId);

        Assert.Equal("result_blocked_by_packet_enforcement", surface.BrokeredExecutionState);
        Assert.Equal("quarantine", surface.PacketEnforcementVerdict);
        Assert.Equal("blocked", surface.ReviewPreflight.Status);
        Assert.Equal("mismatch", surface.ReviewPreflight.PacketScopeStatus);
        Assert.Contains("docs/OffPacket.md", surface.ReviewPreflight.PacketScopeMismatchFiles);
        Assert.Contains(surface.ReviewPreflight.Blockers, blocker => blocker.BlockerId == "packet_scope_mismatch");
        Assert.Contains(surface.BrokeredChecks, check => check.CheckId == "mode_e_review_preflight_packet_scope" && check.State == "missing");
    }

    [Fact]
    public void Build_WithMissingAcceptanceEvidence_ProjectsReviewPreflightEvidenceBlocker()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# Mode E");
        var task = CreateTask("T-MODE-E-006", status: DomainTaskStatus.Review, evidenceTypes: ["test_output"]);
        var service = CreateService(workspace, task);
        WriteExecutionPacket(workspace, task.TaskId);
        WriteResultEnvelope(workspace, task.TaskId, "RUN-T-MODE-E-006-001");
        WriteWorkerExecutionArtifact(workspace, task.TaskId, "RUN-T-MODE-E-006-001");
        WriteReviewArtifact(workspace, task.TaskId);

        var surface = service.Build(task.TaskId);

        Assert.Equal("result_blocked_by_review_preflight", surface.BrokeredExecutionState);
        Assert.Equal("allow", surface.PacketEnforcementVerdict);
        Assert.Equal("blocked", surface.ReviewPreflight.Status);
        Assert.Equal("missing", surface.ReviewPreflight.AcceptanceEvidenceStatus);
        Assert.Contains(surface.ReviewPreflight.MissingAcceptanceEvidence, item => item.Contains("test_output", StringComparison.Ordinal));
        Assert.Contains(surface.ReviewPreflight.Blockers, blocker => blocker.BlockerId == "acceptance_evidence_missing");
        Assert.Contains(surface.BrokeredChecks, check => check.CheckId == "mode_e_review_preflight_acceptance_evidence" && check.State == "missing");
    }

    [Fact]
    public void Build_WithNullWorkerDiagnosticResult_ProjectsReviewPreflightEvidenceBlocker()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# Mode E");
        var task = CreateTask("T-MODE-E-006B", status: DomainTaskStatus.Review);
        var service = CreateService(workspace, task);
        WriteExecutionPacket(workspace, task.TaskId);
        WriteResultEnvelope(workspace, task.TaskId, "RUN-T-MODE-E-006B-001");
        WriteWorkerExecutionArtifact(workspace, task.TaskId, "RUN-T-MODE-E-006B-001", backendId: "null_worker");
        WriteReviewArtifact(workspace, task.TaskId);

        var surface = service.Build(task.TaskId);

        Assert.Equal("result_blocked_by_review_preflight", surface.BrokeredExecutionState);
        Assert.Equal("blocked", surface.ReviewPreflight.Status);
        Assert.Equal("missing", surface.ReviewPreflight.AcceptanceEvidenceStatus);
        Assert.Contains(surface.ReviewPreflight.MissingAcceptanceEvidence, item => item.Contains("null_worker", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(surface.ReviewPreflight.Blockers, blocker => blocker.BlockerId == "acceptance_evidence_missing");
    }

    [Fact]
    public void Build_WithProtectedPathResult_ProjectsReviewPreflightPathPolicyBlocker()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# Mode E");
        var task = CreateTask("T-MODE-E-007");
        var service = CreateService(workspace, task);
        WriteExecutionPacket(workspace, task.TaskId, editableRoots: ["src/"]);
        WriteResultEnvelope(workspace, task.TaskId, "RUN-T-MODE-E-007-001", changedFiles: [".ai/tasks/graph.json"]);
        WriteWorkerExecutionArtifact(workspace, task.TaskId, "RUN-T-MODE-E-007-001", filesWritten: [".ai/tasks/graph.json"]);

        var surface = service.Build(task.TaskId);

        Assert.Equal("result_blocked_by_packet_enforcement", surface.BrokeredExecutionState);
        Assert.Equal("quarantine", surface.PacketEnforcementVerdict);
        Assert.Equal("protected_path_violation", surface.ReviewPreflight.PathPolicyStatus);
        Assert.Contains(".ai/tasks/graph.json", surface.ReviewPreflight.ProtectedPathViolations);
        Assert.Contains(surface.ReviewPreflight.Blockers, blocker => blocker.BlockerId == "protected_path_policy_violation");
        Assert.Contains(surface.BrokeredChecks, check => check.CheckId == "mode_e_review_preflight_path_policy" && check.State == "missing");
    }

    private static RuntimeBrokeredExecutionSurfaceService CreateService(TemporaryWorkspace workspace, TaskNode task)
    {
        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var memoryService = new MemoryService(new EmptyMemoryRepository(), new ExecutionContextBuilder());
        var packetCompiler = new ExecutionPacketCompilerService(
            workspace.Paths,
            taskGraphService,
            new EmptyCodeGraphQueryService(),
            memoryService,
            new PlannerIntentRoutingService());
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var packetEnforcement = new PacketEnforcementService(workspace.Paths, taskGraphService, artifactRepository);
        return new RuntimeBrokeredExecutionSurfaceService(
            workspace.RootPath,
            taskGraphService,
            packetCompiler,
            packetEnforcement,
            artifactRepository,
            new ReviewEvidenceGateService());
    }

    private static TaskNode CreateTask(
        string taskId,
        DomainTaskStatus status = DomainTaskStatus.Pending,
        IReadOnlyList<string>? evidenceTypes = null)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-MODE-E",
            Title = "Mode E brokered execution test",
            Description = "Validate the task-scoped Mode E surface.",
            Status = status,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["src/CARVES.Runtime.Host/Program.cs"],
            Acceptance = ["brokered execution surface is projected"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
            AcceptanceContract = BuildAcceptanceContract(taskId, evidenceTypes ?? Array.Empty<string>()),
        };
    }

    private static AcceptanceContract BuildAcceptanceContract(string taskId, IReadOnlyList<string> evidenceTypes)
    {
        return new AcceptanceContract
        {
            ContractId = $"AC-{taskId}",
            Title = $"Acceptance contract for {taskId}",
            Status = AcceptanceContractLifecycleStatus.HumanReview,
            EvidenceRequired = evidenceTypes
                .Select(evidenceType => new AcceptanceContractEvidenceRequirement
                {
                    Type = evidenceType,
                    Description = $"Projection requires {evidenceType}.",
                })
                .ToArray(),
        };
    }

    private static void WriteExecutionPacket(
        TemporaryWorkspace workspace,
        string taskId,
        IReadOnlyList<string>? editableRoots = null)
    {
        var packet = new ExecutionPacket
        {
            PacketId = $"EP-{taskId}-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-MODE-E",
                TaskId = taskId,
                TaskRevision = 1,
            },
            Goal = "Prove Mode E brokered execution surface.",
            PlannerIntent = PlannerIntent.Execution,
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = editableRoots ?? ["src/"],
                ReadOnlyRoots = ["docs/"],
                TruthRoots = ["carves://truth/tasks", "carves://truth/runtime"],
                RepoMirrorRoots = [".ai/", ".carves-platform/"],
            },
            WorkerAllowedActions = ["read", "edit", "build", "test", "carves.submit_result", "carves.request_replan"],
            PlannerOnlyActions = ["carves.review_task", "carves.sync_state"],
        };

        workspace.WriteFile(
            $".ai/runtime/execution-packets/{taskId}.json",
            JsonSerializer.Serialize(packet, JsonOptions));
    }

    private static void WriteResultEnvelope(
        TemporaryWorkspace workspace,
        string taskId,
        string runId,
        IReadOnlyList<string>? changedFiles = null)
    {
        workspace.WriteFile(
            $".ai/execution/{taskId}/result.json",
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
        TemporaryWorkspace workspace,
        string taskId,
        string runId,
        IReadOnlyList<string>? filesWritten = null,
        string? testOutputRef = null,
        string backendId = "codex_cli")
    {
        var evidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json";
        workspace.WriteFile(evidencePath, "{\"taskId\":\"" + taskId + "\"}");
        new JsonRuntimeArtifactRepository(workspace.Paths).SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = runId,
                BackendId = backendId,
                ProviderId = string.Equals(backendId, "null_worker", StringComparison.Ordinal) ? "null" : "codex",
                AdapterId = string.Equals(backendId, "null_worker", StringComparison.Ordinal) ? "NullWorkerAdapter" : "CodexCliWorkerAdapter",
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
                TestOutputRef = testOutputRef,
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                EvidenceStrength = ExecutionEvidenceStrength.Replayable,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedAt = DateTimeOffset.UtcNow,
                ExitStatus = 0,
            },
        });
    }

    private static void WriteReviewArtifact(TemporaryWorkspace workspace, string taskId)
    {
        new JsonRuntimeArtifactRepository(workspace.Paths).SavePlannerReviewArtifact(new PlannerReviewArtifact
        {
            TaskId = taskId,
            Review = new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                Reason = "Waiting for Mode E review preflight.",
                DecisionStatus = ReviewDecisionStatus.PendingReview,
                AcceptanceMet = true,
            },
            ResultingStatus = DomainTaskStatus.Review,
            TransitionReason = "Validated work stopped at review.",
            PlannerComment = "Validated work stopped at review.",
            PatchSummary = "files=1; paths=src/CARVES.Runtime.Host/Program.cs",
            ValidationPassed = true,
            ValidationEvidence = ["mode e preflight review fixture"],
            SafetyOutcome = SafetyOutcome.Allow,
        });
    }

    private sealed class EmptyMemoryRepository : IMemoryRepository
    {
        public IReadOnlyList<MemoryDocument> LoadCategory(string category)
        {
            return [];
        }

        public IReadOnlyList<MemoryDocument> LoadRelevantModules(IReadOnlyList<string> moduleNames)
        {
            return [];
        }
    }

    private sealed class EmptyCodeGraphQueryService : ICodeGraphQueryService
    {
        public CodeGraphManifest LoadManifest()
        {
            return new CodeGraphManifest();
        }

        public IReadOnlyList<CodeGraphModuleEntry> LoadModuleSummaries()
        {
            return [];
        }

        public CodeGraphIndex LoadIndex()
        {
            return new CodeGraphIndex();
        }

        public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries)
        {
            return new CodeGraphScopeAnalysis(scopeEntries.ToArray(), [], scopeEntries.ToArray(), [], [], []);
        }

        public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries)
        {
            return CodeGraphImpactAnalysis.Empty;
        }
    }
}
