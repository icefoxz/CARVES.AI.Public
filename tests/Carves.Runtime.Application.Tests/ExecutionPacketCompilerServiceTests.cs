using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionPacketCompilerServiceTests
{
    [Fact]
    public void CompileAndPersist_UsesExplicitContextOrderAndPlannerIntent()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-PACKET-001",
            CardId = "CARD-301",
            Title = "Compile packet",
            Description = "Compile packet from task truth.",
            TaskType = TaskType.Execution,
            Scope = ["src/CARVES.Runtime.Application/Planning/PlannerContextAssembler.cs"],
            Constraints = ["do not widen scope"],
            Validation = new ValidationPlan
            {
                Commands =
                [
                    ["dotnet", "build", "src/CARVES.Runtime.Host/Carves.Runtime.Host.csproj"],
                ],
            },
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = "AC-T-PACKET-001",
                Title = "Packet contract",
                Status = AcceptanceContractLifecycleStatus.Compiled,
                Intent = new AcceptanceContractIntent
                {
                    Goal = "Carry acceptance contract truth into the execution packet.",
                    BusinessValue = "Workers should receive governed acceptance semantics before execution begins.",
                },
                NonGoals = ["Do not create a second worker control plane."],
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement
                    {
                        Type = "result_commit",
                        Description = "Writeback must produce a result commit.",
                    },
                ],
            },
        };

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var memoryService = new MemoryService(new PacketMemoryRepository(), new ExecutionContextBuilder());
        var service = new ExecutionPacketCompilerService(
            workspace.Paths,
            taskGraphService,
            new PacketCodeGraphQueryService(),
            memoryService,
            new PlannerIntentRoutingService());
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths);

        var packet = service.CompileAndPersist(task, memoryService.BundleForTask(task));

        Assert.Equal(PlannerIntent.Execution, packet.PlannerIntent);
        Assert.Equal(["Architecture", "RelevantModules", "CurrentTaskFiles"], packet.Context.AssemblyOrder);
        Assert.Equal(".ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md", packet.Context.MemoryBundleRefs[0]);
        Assert.Contains("module:CARVES.Runtime.Application", packet.Context.CodegraphQueries);
        Assert.Contains("src/CARVES.Runtime.Application/Planning/PlannerContextAssembler.cs", packet.Context.RelevantFiles);
        Assert.Equal("AC-T-PACKET-001", packet.AcceptanceContract?.ContractId);
        Assert.Contains("result_commit", packet.AcceptanceContract?.EvidenceRequired.Select(item => item.Type) ?? Array.Empty<string>());
        Assert.Equal("result_closure_protocol_v1", packet.ClosureContract.ProtocolId);
        Assert.Equal("generic_review_closure_v1", packet.ClosureContract.ContractMatrixProfileId);
        Assert.Contains("patch_scope_recorded", packet.ClosureContract.RequiredContractChecks);
        Assert.Contains("focused_required_validation", packet.ClosureContract.RequiredValidationGates);
        Assert.Contains("changed_files", packet.ClosureContract.CompletionClaimFields);
        Assert.Equal("worker-execution-packet.v1", packet.WorkerExecutionPacket.SchemaVersion);
        Assert.Equal("WEP-T-PACKET-001-v1", packet.WorkerExecutionPacket.PacketId);
        Assert.Equal(packet.PacketId, packet.WorkerExecutionPacket.SourceExecutionPacketId);
        Assert.Equal(task.TaskId, packet.WorkerExecutionPacket.TaskId);
        Assert.Contains("src/CARVES.Runtime.Application/Planning/PlannerContextAssembler.cs", packet.WorkerExecutionPacket.AllowedFiles);
        Assert.Contains("carves.submit_result", packet.WorkerExecutionPacket.AllowedActions);
        Assert.Contains("patch_scope_recorded", packet.WorkerExecutionPacket.RequiredContractMatrix);
        Assert.Contains("dotnet build src/CARVES.Runtime.Host/Carves.Runtime.Host.csproj", packet.WorkerExecutionPacket.RequiredValidation);
        Assert.Contains("focused_required_validation", packet.WorkerExecutionPacket.RequiredValidationGates);
        Assert.Contains(".ai/execution/T-PACKET-001/result.json", packet.WorkerExecutionPacket.EvidenceRequired);
        Assert.Contains(".ai/artifacts/worker-executions/T-PACKET-001.json", packet.WorkerExecutionPacket.EvidenceRequired);
        Assert.True(packet.WorkerExecutionPacket.CompletionClaimSchema.Required);
        Assert.False(packet.WorkerExecutionPacket.CompletionClaimSchema.ClaimIsTruth);
        Assert.True(packet.WorkerExecutionPacket.CompletionClaimSchema.HostValidationRequired);
        Assert.Contains("contract_items_satisfied", packet.WorkerExecutionPacket.CompletionClaimSchema.Fields);
        Assert.Equal(".ai/execution/T-PACKET-001/result.json", packet.WorkerExecutionPacket.ResultSubmission.CandidateResultChannel);
        Assert.Equal("task ingest-result T-PACKET-001", packet.WorkerExecutionPacket.ResultSubmission.HostIngestCommand);
        Assert.True(packet.WorkerExecutionPacket.ResultSubmission.CandidateOnly);
        Assert.True(packet.WorkerExecutionPacket.ResultSubmission.ReviewBundleRequired);
        Assert.True(packet.WorkerExecutionPacket.ResultSubmission.SubmittedByHostOrAdapter);
        Assert.False(packet.WorkerExecutionPacket.ResultSubmission.WorkerDirectTruthWriteAllowed);
        Assert.False(packet.WorkerExecutionPacket.GrantsLifecycleTruthAuthority);
        Assert.False(packet.WorkerExecutionPacket.GrantsTruthWriteAuthority);
        Assert.False(packet.WorkerExecutionPacket.CreatesTaskQueue);
        Assert.False(packet.WorkerExecutionPacket.WritesTruthRoots);
        Assert.Contains("predicted_patch_exceeds_budget", packet.StopConditions);
        Assert.Contains("requires_split_to_stay_within_patch_budget", packet.StopConditions);
        Assert.True(File.Exists(service.GetPacketPath(task.TaskId)));
        Assert.True(File.Exists(truthStore.GetExecutionPacketPath(task.TaskId)));
        Assert.Contains("\"acceptanceContract\"", File.ReadAllText(service.GetPacketPath(task.TaskId)), StringComparison.Ordinal);
        Assert.Contains("\"closureContract\"", File.ReadAllText(service.GetPacketPath(task.TaskId)), StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_ProjectsNarrativeClosureContractForSummaryRoutingIntent()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-PACKET-NARRATIVE-001",
            CardId = "CARD-ROUTING",
            Title = "Summarize active route failure",
            Description = "Produce a failure summary for active routing validation.",
            TaskType = TaskType.Execution,
            Scope = [".ai/STATE.md"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "failure_summary",
                ["module_id"] = "Execution/ResultEnvelope",
            },
        };

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var memoryService = new MemoryService(new PacketMemoryRepository(), new ExecutionContextBuilder());
        var service = new ExecutionPacketCompilerService(
            workspace.Paths,
            taskGraphService,
            new PacketCodeGraphQueryService(),
            memoryService,
            new PlannerIntentRoutingService());

        var packet = service.Compile(task, memoryService.BundleForTask(task));

        Assert.Equal("narrative_result_closure_v1", packet.ClosureContract.ContractMatrixProfileId);
        Assert.DoesNotContain("patch_scope_recorded", packet.ClosureContract.RequiredContractChecks);
        Assert.Contains("completion_claim_recorded", packet.ClosureContract.RequiredContractChecks);
        Assert.Contains("result_channel_recorded", packet.ClosureContract.RequiredContractChecks);
        Assert.Contains("scope_hygiene", packet.ClosureContract.RequiredContractChecks);
        Assert.DoesNotContain("patch_scope_recorded", packet.WorkerExecutionPacket.RequiredContractMatrix);
        Assert.Contains("completion_claim_recorded", packet.WorkerExecutionPacket.RequiredContractMatrix);
        Assert.Contains("result_channel_recorded", packet.WorkerExecutionPacket.RequiredContractMatrix);
        Assert.Equal([".ai/STATE.md"], packet.WorkerExecutionPacket.AllowedFiles);
    }

    [Fact]
    public void Compile_ProjectsRecoverableResidueClosureContractForResidueTasks()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-CARD-972-001",
            CardId = "CARD-972",
            Title = "Close recoverable residue readback",
            Description = "Implement the recoverable residue contract and readback behavior.",
            TaskType = TaskType.Execution,
            Scope =
            [
                "src/CARVES.Runtime.Domain/Runtime/ControlPlaneResidueContract.cs",
                "src/CARVES.Runtime.Application/Platform/ManagedWorkspaceLeaseService.cs",
            ],
            Validation = new ValidationPlan
            {
                ExpectedEvidence =
                [
                    "recoverable residue fields survive persistence and readback",
                ],
            },
        };

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var memoryService = new MemoryService(new PacketMemoryRepository(), new ExecutionContextBuilder());
        var service = new ExecutionPacketCompilerService(
            workspace.Paths,
            taskGraphService,
            new PacketCodeGraphQueryService(),
            memoryService,
            new PlannerIntentRoutingService());

        var packet = service.Compile(task, memoryService.BundleForTask(task));

        Assert.Equal("runtime_recoverable_residue_contract_v1", packet.ClosureContract.ContractMatrixProfileId);
        Assert.Contains("residue_contract_schema_presence", packet.ClosureContract.RequiredContractChecks);
        Assert.Contains("persistence_readback_wiring", packet.ClosureContract.RequiredContractChecks);
        Assert.Contains("roundtrip_validation_evidence", packet.ClosureContract.RequiredContractChecks);
        Assert.Contains("contract_roundtrip_required", packet.ClosureContract.RequiredValidationGates);
        Assert.Contains("task_expected_evidence_required", packet.ClosureContract.RequiredValidationGates);
        Assert.Equal(["low", "medium", "high"], packet.ClosureContract.ForbiddenVocabulary);
        Assert.Contains("residue_contract_schema_presence", packet.WorkerExecutionPacket.RequiredContractMatrix);
        Assert.Contains("persistence_readback_wiring", packet.WorkerExecutionPacket.RequiredContractMatrix);
        Assert.Contains("contract_roundtrip_required", packet.WorkerExecutionPacket.RequiredValidationGates);
        Assert.Equal(["low", "medium", "high"], packet.WorkerExecutionPacket.ForbiddenVocabulary);
        Assert.Contains("recoverable residue fields survive persistence and readback", packet.WorkerExecutionPacket.EvidenceRequired);
    }

    [Fact]
    public void CompileAndPersist_RejectsExecutionTaskWithoutAcceptanceContract()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-PACKET-MISSING-CONTRACT",
            CardId = "CARD-711",
            Title = "Reject missing contract",
            Description = "Execution packet compilation must not bypass Mode C/D entry prerequisites.",
            TaskType = TaskType.Execution,
            Scope = ["src/CARVES.Runtime.Application/Planning/ExecutionPacketCompilerService.cs"],
        };

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var memoryService = new MemoryService(new PacketMemoryRepository(), new ExecutionContextBuilder());
        var service = new ExecutionPacketCompilerService(
            workspace.Paths,
            taskGraphService,
            new PacketCodeGraphQueryService(),
            memoryService,
            new PlannerIntentRoutingService());

        var error = Assert.Throws<InvalidOperationException>(() => service.CompileAndPersist(task, memoryService.BundleForTask(task)));

        Assert.Contains("acceptance contract", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inspect task T-PACKET-MISSING-CONTRACT", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_ProjectsWindowedReadsAndContextCompactionForLargeFiles()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/LargePacketContext.cs", string.Join(Environment.NewLine, Enumerable.Range(1, 220).Select(index => $"public string PacketValue{index:D3} => \"Line{index:D3}\";")));
        var task = new TaskNode
        {
            TaskId = "T-PACKET-WINDOW-001",
            CardId = "CARD-360",
            Title = "Compile packet with bounded large-file context",
            Description = "Execution packet should project large-file windowing metadata.",
            TaskType = TaskType.Execution,
            Scope = ["src/LargePacketContext.cs"],
            Constraints = ["do not widen scope"],
        };

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var memoryService = new MemoryService(new PacketMemoryRepository(), new ExecutionContextBuilder());
        var service = new ExecutionPacketCompilerService(
            workspace.Paths,
            taskGraphService,
            new PacketWindowCodeGraphQueryService(),
            memoryService,
            new PlannerIntentRoutingService());

        var packet = service.Compile(task, memoryService.BundleForTask(task));

        Assert.Equal(".ai/runtime/context-packs/tasks/T-PACKET-WINDOW-001.json", packet.Context.ContextPackRef);
        Assert.Equal("bounded_scope_windowing", packet.Context.Compaction.Strategy);
        Assert.Equal(1, packet.Context.Compaction.CandidateFileCount);
        Assert.Equal(1, packet.Context.Compaction.RelevantFileCount);
        Assert.Equal(1, packet.Context.Compaction.WindowedReadCount);
        var window = Assert.Single(packet.Context.WindowedReads);
        Assert.Equal("src/LargePacketContext.cs", window.Path);
        Assert.Equal(1, window.StartLine);
        Assert.Equal(80, window.EndLine);
        Assert.Equal(220, window.TotalLines);
        Assert.True(window.Truncated);
    }

    [Theory]
    [InlineData(PlannerWakeReason.NewGoalArrived, PlannerIntent.Planning)]
    [InlineData(PlannerWakeReason.ExecutionBacklogCleared, PlannerIntent.Execution)]
    [InlineData(PlannerWakeReason.TaskFailed, PlannerIntent.Maintenance)]
    [InlineData(PlannerWakeReason.WorkerResultReturned, PlannerIntent.Writeback)]
    public void PlannerIntentRoutingService_ClassifiesWakeReasons(PlannerWakeReason wakeReason, PlannerIntent expected)
    {
        var service = new PlannerIntentRoutingService();

        var intent = service.Classify(wakeReason);

        Assert.Equal(expected, intent);
    }

    private sealed class PacketMemoryRepository : IMemoryRepository
    {
        public IReadOnlyList<MemoryDocument> LoadCategory(string category)
        {
            return category switch
            {
                "architecture" =>
                [
                    new MemoryDocument(".ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md", "architecture", "AI Entry", "Entry protocol"),
                ],
                "project" =>
                [
                    new MemoryDocument(".ai/PROJECT_BOUNDARY.md", "project", "Project Boundary", "Boundary"),
                ],
                _ => Array.Empty<MemoryDocument>(),
            };
        }

        public IReadOnlyList<MemoryDocument> LoadRelevantModules(IReadOnlyList<string> moduleNames)
        {
            return
            [
                new MemoryDocument(".ai/memory/modules/CARVES.Runtime.Application.md", "modules", "CARVES.Runtime.Application", "Application module"),
            ];
        }
    }

    private sealed class PacketCodeGraphQueryService : ICodeGraphQueryService
    {
        public CodeGraphManifest LoadManifest()
        {
            return new CodeGraphManifest();
        }

        public IReadOnlyList<CodeGraphModuleEntry> LoadModuleSummaries()
        {
            return
            [
                new CodeGraphModuleEntry(
                    "module-app",
                    "CARVES.Runtime.Application",
                    "src/CARVES.Runtime.Application/",
                    "Application module",
                    [],
                    []),
            ];
        }

        public CodeGraphIndex LoadIndex()
        {
            return new CodeGraphIndex();
        }

        public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries)
        {
            return new CodeGraphScopeAnalysis(
                scopeEntries.ToArray(),
                ["CARVES.Runtime.Application"],
                ["src/CARVES.Runtime.Application/Planning/PlannerContextAssembler.cs"],
                ["PlannerContextAssembler.Build"],
                ["CARVES.Runtime.Domain"],
                ["CARVES.Runtime.Application: planner context assembler"]);
        }

        public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries)
        {
            return CodeGraphImpactAnalysis.Empty;
        }
    }

    private sealed class PacketWindowCodeGraphQueryService : ICodeGraphQueryService
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
            return new CodeGraphScopeAnalysis(
                scopeEntries.ToArray(),
                ["CARVES.Runtime.Application"],
                ["src/LargePacketContext.cs"],
                [],
                [],
                ["LargePacketContext: oversized context file"]);
        }

        public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries)
        {
            return CodeGraphImpactAnalysis.Empty;
        }
    }
}
