using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskNode = Carves.Runtime.Domain.Tasks.TaskNode;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class Stage4PlanningTests
{
    [Fact]
    public void OpportunityRepository_RoundTripsSnapshot()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonOpportunityRepository(workspace.Paths);
        var snapshot = new OpportunitySnapshot
        {
            Items =
            [
                new Opportunity
                {
                    OpportunityId = "OPP-1",
                    Source = OpportunitySource.TestCoverage,
                    Fingerprint = "coverage:TaskScheduler",
                    Title = "Add tests for TaskScheduler",
                    Description = "TaskScheduler has no tests.",
                    Reason = "test coverage gap",
                    Severity = OpportunitySeverity.Medium,
                    Confidence = 0.7,
                    Status = OpportunityStatus.Open,
                    RelatedFiles = ["src/CARVES.Runtime.Application/TaskGraph/TaskScheduler.cs"],
                },
            ],
        };

        repository.Save(snapshot);
        var loaded = repository.Load();

        Assert.Single(loaded.Items);
        Assert.Equal("OPP-1", loaded.Items[0].OpportunityId);
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(workspace.Paths.OpportunitiesFile), StringComparison.Ordinal);
    }

    [Fact]
    public void ControlPlaneConfigRepository_LoadsPlannerAutonomyPolicy()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/config/planner_autonomy.json", """
{
  "max_planner_rounds": 4,
  "max_generated_tasks": 6,
  "max_refactor_scope_files": 3,
  "max_opportunities_per_round": 2
}
""");

        var repository = new FileControlPlaneConfigRepository(workspace.Paths);
        var policy = repository.LoadPlannerAutonomyPolicy();

        Assert.Equal(4, policy.MaxPlannerRounds);
        Assert.Equal(6, policy.MaxGeneratedTasks);
        Assert.Equal(3, policy.MaxRefactorScopeFiles);
        Assert.Equal(2, policy.MaxOpportunitiesPerRound);
    }

    [Fact]
    public void OpportunityDetectorService_DeduplicatesObservationsBySourceAndFingerprint()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonOpportunityRepository(workspace.Paths);
        var observation = new OpportunityObservation(
            OpportunitySource.CodeGraph,
            "cycle:A|B",
            "Break cycle",
            "A and B depend on each other.",
            "cycle detected",
            OpportunitySeverity.High,
            0.8,
            ["src/A.cs", "src/B.cs"],
            new Dictionary<string, string>(StringComparer.Ordinal));
        var service = new OpportunityDetectorService(
            repository,
            [new StubOpportunityDetector(observation), new StubOpportunityDetector(observation)]);

        var result = service.DetectAndStore();

        Assert.Single(result.Snapshot.Items);
        Assert.Equal(OpportunityStatus.Open, result.Snapshot.Items[0].Status);
    }

    [Fact]
    public void OpportunityDetectorService_ReconcilesMemoryDriftMaterializationFromExistingTaskGraph()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonOpportunityRepository(workspace.Paths);
        var graph = new DomainTaskGraph();
        graph.AddOrReplace(new DomainTaskNode
        {
            TaskId = "T-PLAN-audit-runtime-memory-for-carves-runtime-cli-7cd38586",
            Title = "Plan Audit runtime memory for carves_runtime_cli",
            Description = "Plan Audit runtime memory for carves_runtime_cli",
            Status = DomainTaskStatus.Pending,
            TaskType = Carves.Runtime.Domain.Tasks.TaskType.Planning,
            Scope = [".ai/memory/modules/carves_runtime_cli.md"],
        });
        graph.AddOrReplace(new DomainTaskNode
        {
            TaskId = "T-PLAN-audit-runtime-memory-for-carves-runtime-cli-7cd38586-001",
            Title = "Shape implementation for Audit runtime memory for carves_runtime_cli",
            Description = "Shape implementation for Audit runtime memory for carves_runtime_cli",
            Status = DomainTaskStatus.Pending,
            TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
            Scope = [".ai/memory/modules/carves_runtime_cli.md"],
            Dependencies = ["T-PLAN-audit-runtime-memory-for-carves-runtime-cli-7cd38586"],
        });

        var observation = new OpportunityObservation(
            OpportunitySource.MemoryDrift,
            "memory:carves_runtime_cli",
            "Audit runtime memory for carves_runtime_cli",
            "CodeGraph knows module 'carves_runtime_cli' but no module memory document was found.",
            "module memory is missing for an active codegraph module",
            OpportunitySeverity.Medium,
            0.75,
            [".ai/memory/modules/carves_runtime_cli.md"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["module"] = "carves_runtime_cli",
            });

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(graph), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = new OpportunityDetectorService(
            repository,
            [new StubOpportunityDetector(observation)],
            taskGraphService: taskGraphService);

        var result = service.DetectAndStore();
        var opportunity = result.Snapshot.Items.Single();

        Assert.Equal(OpportunityStatus.Materialized, opportunity.Status);
        Assert.Equal(
            [
                "T-PLAN-audit-runtime-memory-for-carves-runtime-cli-7cd38586",
                "T-PLAN-audit-runtime-memory-for-carves-runtime-cli-7cd38586-001",
            ],
            opportunity.MaterializedTaskIds);
    }

    [Fact]
    public void PlannerOpportunityEvaluator_RespectsPlannerRoundCap()
    {
        using var workspace = new TemporaryWorkspace();
        var opportunityRepository = new JsonOpportunityRepository(workspace.Paths);
        opportunityRepository.Save(new OpportunitySnapshot
        {
            Items =
            [
                BuildOpenOpportunity(),
            ],
        });

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var evaluator = new PlannerOpportunityEvaluator(
            new PlannerAutonomyPolicy(1, 8, 5, 3),
            new OpportunityTaskPipeline(workspace.RootPath, taskGraphService, new TaskDecomposer(), new StubGitClient(), TestSystemConfigFactory.Create(["src", "tests"])),
            opportunityRepository);
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: true);
        session.PlannerRound = 1;

        var result = evaluator.Evaluate(session, opportunityRepository.Load());

        Assert.Equal(PlannerAutonomyLimit.PlannerRoundCap, result.AutonomyLimit);
        Assert.False(result.ProducedWork);
    }

    [Fact]
    public void PlannerOpportunityEvaluator_MaterializesPlanningAndExecutionTasks()
    {
        using var workspace = new TemporaryWorkspace();
        var opportunityRepository = new JsonOpportunityRepository(workspace.Paths);
        opportunityRepository.Save(new OpportunitySnapshot
        {
            Items =
            [
                BuildOpenOpportunity(),
            ],
        });

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var evaluator = new PlannerOpportunityEvaluator(
            PlannerAutonomyPolicy.CreateDefault(),
            new OpportunityTaskPipeline(workspace.RootPath, taskGraphService, new TaskDecomposer(), new StubGitClient(), TestSystemConfigFactory.Create(["src", "tests"])),
            opportunityRepository);
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: true);

        var result = evaluator.Evaluate(session, opportunityRepository.Load());
        var graph = taskGraphService.Load();
        var opportunity = opportunityRepository.Load().Items.Single();

        Assert.True(result.ProducedWork);
        Assert.Equal(4, result.MaterializedTaskIds.Count);
        Assert.Equal(4, graph.Tasks.Count);
        Assert.All(graph.Tasks.Values, task => Assert.Equal(DomainTaskStatus.Suggested, task.Status));
        Assert.Equal(OpportunityStatus.Materialized, opportunity.Status);
        Assert.Equal(4, opportunity.MaterializedTaskIds.Count);
    }

    private static Opportunity BuildOpenOpportunity()
    {
        return new Opportunity
        {
            OpportunityId = "OPP-TaskScheduler-coverage",
            Source = OpportunitySource.TestCoverage,
            Fingerprint = "coverage:TaskScheduler",
            Title = "Add tests for TaskScheduler",
            Description = "TaskScheduler currently has no matching tests.",
            Reason = "module appears uncovered by tests",
            Severity = OpportunitySeverity.Medium,
            Confidence = 0.75,
            Status = OpportunityStatus.Open,
            RelatedFiles = ["src/CARVES.Runtime.Application/TaskGraph/TaskScheduler.cs"],
        };
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
}
