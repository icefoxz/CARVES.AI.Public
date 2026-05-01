using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Memory;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class PlannerReentryServiceTests
{
    [Fact]
    public void Reenter_MaterializesGovernedPipelineFromDetectedOpportunity()
    {
        using var workspace = new TemporaryWorkspace();
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var opportunityRepository = new JsonOpportunityRepository(workspace.Paths);
        var detectorService = new OpportunityDetectorService(
            opportunityRepository,
            [new StubOpportunityDetector(
                new OpportunityObservation(
                    OpportunitySource.TestCoverage,
                    "coverage:TaskScheduler",
                    "Add tests for TaskScheduler",
                    "TaskScheduler currently has no matching tests.",
                    "module appears uncovered by tests",
                    OpportunitySeverity.Medium,
                    0.75,
                    ["src/CARVES.Runtime.Application/TaskGraph/TaskScheduler.cs"],
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["module"] = "TaskScheduler",
                    }))]);
        var evaluator = new PlannerOpportunityEvaluator(
            PlannerAutonomyPolicy.CreateDefault(),
            new OpportunityTaskPipeline(workspace.RootPath, taskGraphService, new TaskDecomposer(), new StubGitClient(), TestSystemConfigFactory.Create(["src", "tests"])),
            opportunityRepository);
        var service = CreatePlannerReentryService(workspace, taskGraphService, opportunityRepository, detectorService, evaluator);
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: false);
        session.BeginTick(dryRun: false);

        var result = service.Reenter(session);
        var suggested = taskGraphService.Load().ByStatus(DomainTaskStatus.Suggested);

        Assert.Equal(PlannerReentryOutcome.SuggestedPlanningWork, result.Outcome);
        Assert.True(result.ProducedWork);
        Assert.Equal(1, result.PlannerRound);
        Assert.Equal(1, result.DetectedOpportunityCount);
        Assert.Equal(1, result.EvaluatedOpportunityCount);
        Assert.Contains("TestCoverage", result.OpportunitySourceSummary, StringComparison.Ordinal);
        Assert.Equal(4, suggested.Count);
        Assert.Contains(suggested, task => task.TaskType == TaskType.Planning);
        Assert.All(suggested.Where(task => task.TaskType == TaskType.Execution), task => Assert.Equal(DomainTaskStatus.Suggested, task.Status));
    }

    [Fact]
    public void Reenter_ReusesExistingGovernedWorkInsteadOfDuplicatingSuggestions()
    {
        var repository = new InMemoryTaskGraphRepository(
            new DomainTaskGraph(
            [
                new TaskNode
                {
                    TaskId = "T-PLAN-existing",
                    Title = "Existing governed work",
                    Status = DomainTaskStatus.Suggested,
                    TaskType = TaskType.Planning,
                    Source = "PLANNER_OPPORTUNITY",
                    Priority = "P2",
                    Scope = [".ai/opportunities/index.json"],
                    Acceptance = ["handled"],
                },
            ]));
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        using var workspace = new TemporaryWorkspace();
        var opportunityRepository = new JsonOpportunityRepository(workspace.Paths);
        var detectorService = new OpportunityDetectorService(opportunityRepository, [new StubOpportunityDetector()]);
        var evaluator = new PlannerOpportunityEvaluator(
            PlannerAutonomyPolicy.CreateDefault(),
            new OpportunityTaskPipeline(workspace.RootPath, taskGraphService, new TaskDecomposer(), new StubGitClient(), TestSystemConfigFactory.Create(["src", "tests"])),
            opportunityRepository);
        var service = CreatePlannerReentryService(workspace, taskGraphService, opportunityRepository, detectorService, evaluator);
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: false);
        session.BeginTick(dryRun: false);

        var result = service.Reenter(session);

        Assert.Equal(PlannerReentryOutcome.ExistingGovernedWork, result.Outcome);
        Assert.Equal(["T-PLAN-existing"], result.ProposedTaskIds);
        Assert.Single(taskGraphService.Load().ByStatus(DomainTaskStatus.Suggested));
    }

    private sealed class StubOpportunityDetector : IOpportunityDetector
    {
        private readonly IReadOnlyList<OpportunityObservation> observations;

        public StubOpportunityDetector(params OpportunityObservation[] observations)
        {
            this.observations = observations;
        }

        public string Name => "stub";

        public IReadOnlyList<OpportunityObservation> Detect()
        {
            return observations;
        }
    }

    private static PlannerReentryService CreatePlannerReentryService(
        TemporaryWorkspace workspace,
        TaskGraphService taskGraphService,
        IOpportunityRepository opportunityRepository,
        OpportunityDetectorService detectorService,
        PlannerOpportunityEvaluator evaluator)
    {
        var memoryService = new MemoryService(new FileMemoryRepository(workspace.Paths), new ExecutionContextBuilder());
        var contextPackService = new ContextPackService(
            workspace.Paths,
            taskGraphService,
            new StubCodeGraphQueryService(),
            memoryService,
            new FailureSummaryProjectionService(workspace.Paths, new FailureContextService(new JsonFailureReportRepository(workspace.Paths)), new ExecutionRunService(workspace.Paths)),
            new ExecutionRunService(workspace.Paths));
        var contextAssembler = new PlannerContextAssembler(
            taskGraphService,
            new StubCodeGraphQueryService(),
            memoryService,
            contextPackService,
            CarvesCodeStandard.CreateDefault(),
            PlannerAutonomyPolicy.CreateDefault(),
            new PlannerIntentRoutingService());
        var host = new PlannerHostService(
            taskGraphService,
            detectorService,
            evaluator,
            new PlannerAdapterRegistry(
                [new StubPlannerAdapter()],
                new StubPlannerAdapter()),
            contextAssembler,
            new PlannerProposalValidator(),
            new PlannerProposalAcceptanceService(taskGraphService, opportunityRepository),
            new JsonRuntimeArtifactRepository(workspace.Paths));
        return new PlannerReentryService(host);
    }

    private sealed class StubPlannerAdapter : IPlannerAdapter
    {
        public string AdapterId => "StubPlannerAdapter";

        public string ProviderId => "local";

        public string? ProfileId => "test";

        public bool IsConfigured => true;

        public bool IsRealAdapter => false;

        public string SelectionReason => "test deterministic planner";

        public PlannerProposalEnvelope Run(PlannerRunRequest request)
        {
            var proposal = new PlannerProposal
            {
                ProposalId = request.ProposalId,
                PlannerBackend = "stub",
                GoalSummary = request.GoalSummary,
                RecommendedAction = request.PreviewTasks.Count == 0 ? PlannerRecommendedAction.Sleep : PlannerRecommendedAction.ProposeWork,
                SleepRecommendation = request.PreviewTasks.Count == 0 ? PlannerSleepReason.NoOpenOpportunities : PlannerSleepReason.ExistingGovernedWork,
                ProposedTasks = request.PreviewTasks,
                Dependencies = request.PreviewDependencies,
                Confidence = 0.75,
                Rationale = "test proposal",
            };

            return new PlannerProposalEnvelope
            {
                ProposalId = request.ProposalId,
                AdapterId = AdapterId,
                ProviderId = ProviderId,
                ProfileId = ProfileId,
                Configured = true,
                UsedFallback = false,
                WakeReason = request.WakeReason,
                WakeDetail = request.WakeDetail,
                Proposal = proposal,
                RawResponsePreview = "test proposal",
            };
        }
    }
}
