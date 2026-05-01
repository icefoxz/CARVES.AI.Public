using System.Text.Json;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class FormalPlanningExecutionGateServiceTests
{
    [Fact]
    public void Evaluate_PlanBoundTaskWithoutActiveLease_ReturnsWorkspaceRequired()
    {
        using var fixture = Phase5Fixture.Create();

        var projection = fixture.GateService.Evaluate(fixture.Task);

        Assert.True(projection.BlocksExecution);
        Assert.True(projection.WorkspaceRequired);
        Assert.Equal("workspace_required", projection.Status);
        Assert.Equal("managed_workspace_required", projection.ReasonCode);
        Assert.Contains("managed workspace lease", projection.Summary, StringComparison.OrdinalIgnoreCase);

        var entryGate = new ModeExecutionEntryGateService(fixture.GateService).Evaluate(fixture.Task);

        Assert.True(entryGate.BlocksExecution);
        Assert.Equal("mode_c_task_bound_workspace", entryGate.TargetMode);
        Assert.Equal("managed_workspace_lease_available", entryGate.FirstBlockingCheckId);
        Assert.Equal($"plan issue-workspace {fixture.Task.TaskId}", entryGate.FirstBlockingCheckRequiredCommand);
        Assert.Equal($"plan issue-workspace {fixture.Task.TaskId}", entryGate.RecommendedNextCommand);
        Assert.True(entryGate.WorkspaceRequired);
    }

    [Fact]
    public void CompileAndPersist_RejectsPlanBoundTaskWithoutActiveLease()
    {
        using var fixture = Phase5Fixture.Create();
        var service = new ExecutionPacketCompilerService(
            fixture.Workspace.Paths,
            fixture.TaskGraphService,
            new PacketCodeGraphQueryService(),
            new MemoryService(new PacketMemoryRepository(), new ExecutionContextBuilder()),
            new PlannerIntentRoutingService(),
            formalPlanningExecutionGateService: fixture.GateService);

        var error = Assert.Throws<InvalidOperationException>(() => service.CompileAndPersist(fixture.Task));

        Assert.Contains("managed workspace lease", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ClassifiesWorkspaceRequiredForPlanBoundTaskWithoutActiveLease()
    {
        using var fixture = Phase5Fixture.Create();
        var service = new DispatchProjectionService(formalPlanningExecutionGateService: fixture.GateService);

        var projection = service.Build(fixture.TaskGraphService.Load(), null, maxWorkers: 1);

        Assert.Equal("dispatch_blocked", projection.State);
        Assert.Equal("WORKSPACE_REQUIRED", projection.IdleReason);
        Assert.Equal(0, projection.ReadyTaskCount);
        Assert.Equal(0, projection.PlanRequiredBlockCount);
        Assert.Equal(1, projection.WorkspaceRequiredBlockCount);
        Assert.Equal(0, projection.AcceptanceContractGapCount);
        Assert.Equal(fixture.Task.TaskId, projection.FirstBlockedTaskId);
        Assert.Equal("managed_workspace_lease_available", projection.FirstBlockingCheckId);
        Assert.Equal($"plan issue-workspace {fixture.Task.TaskId}", projection.FirstBlockingCheckRequiredCommand);
        Assert.Equal($"plan issue-workspace {fixture.Task.TaskId}", projection.RecommendedNextCommand);
    }

    private sealed class Phase5Fixture : IDisposable
    {
        private Phase5Fixture(
            TemporaryWorkspace workspace,
            TaskGraphService taskGraphService,
            FormalPlanningExecutionGateService gateService,
            TaskNode task)
        {
            Workspace = workspace;
            TaskGraphService = taskGraphService;
            GateService = gateService;
            Task = task;
        }

        public TemporaryWorkspace Workspace { get; }

        public TaskGraphService TaskGraphService { get; }

        public FormalPlanningExecutionGateService GateService { get; }

        public TaskNode Task { get; }

        public static Phase5Fixture Create()
        {
            var workspace = new TemporaryWorkspace();
            workspace.WriteFile("README.md", "# Sample Repo");
            workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
            var paths = workspace.Paths;
            var systemConfig = TestSystemConfigFactory.Create();
            var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
            var query = new FileCodeGraphQueryService(paths, builder);
            var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
            var intentDiscoveryService = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);
            InitializeFormalPlanning(intentDiscoveryService);
            var leaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
            var gateService = new FormalPlanningExecutionGateService(intentDiscoveryService, leaseRepository);
            var task = CreatePlanBoundTask(intentDiscoveryService);
            var taskGraphService = new TaskGraphService(
                new InMemoryTaskGraphRepository(new DomainTaskGraph([task])),
                new Carves.Runtime.Application.TaskGraph.TaskScheduler(formalPlanningExecutionGateService: gateService));
            return new Phase5Fixture(workspace, taskGraphService, gateService, task);
        }

        public void Dispose()
        {
            Workspace.Dispose();
        }
    }

    private static void InitializeFormalPlanning(IntentDiscoveryService service)
    {
        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
        service.InitializeFormalPlanning();
    }

    private static TaskNode CreatePlanBoundTask(IntentDiscoveryService intentDiscoveryService)
    {
        var activePlanningCard = intentDiscoveryService.GetStatus().Draft?.ActivePlanningCard
            ?? throw new InvalidOperationException("Expected an active planning card.");
        var lineage = new PlanningLineage
        {
            PlanningSlotId = activePlanningCard.PlanningSlotId,
            ActivePlanningCardId = activePlanningCard.PlanningCardId,
            SourceIntentDraftId = activePlanningCard.SourceIntentDraftId,
            SourceCandidateCardId = activePlanningCard.SourceCandidateCardId,
            FormalPlanningState = FormalPlanningState.ExecutionBound,
        };

        return new TaskNode
        {
            TaskId = "T-PHASE5-001",
            CardId = "CARD-PHASE5",
            Title = "Phase 5 task",
            Description = "Prove managed workspace gating.",
            TaskType = TaskType.Execution,
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = ["src/Sample.cs"],
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = "AC-T-PHASE5-001",
                Title = "Phase 5 contract",
                Status = AcceptanceContractLifecycleStatus.Compiled,
                Traceability = new AcceptanceContractTraceability
                {
                    SourceTaskId = "T-PHASE5-001",
                },
            },
            Metadata = new Dictionary<string, string>(PlanningLineageMetadata.Merge(new Dictionary<string, string>(StringComparer.Ordinal), lineage), StringComparer.Ordinal),
        };
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
        public CodeGraphManifest LoadManifest() => new();

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

        public CodeGraphIndex LoadIndex() => new();

        public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries)
        {
            return new CodeGraphScopeAnalysis(
                scopeEntries.ToArray(),
                ["CARVES.Runtime.Application"],
                ["src/Sample.cs"],
                [],
                [],
                ["Sample: phase-5 gate proof"]);
        }

        public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries) => CodeGraphImpactAnalysis.Empty;
    }
}
