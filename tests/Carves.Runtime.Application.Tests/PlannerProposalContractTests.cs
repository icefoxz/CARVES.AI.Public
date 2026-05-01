using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class PlannerProposalContractTests
{
    [Fact]
    public void Validator_RejectsReviewTasksAndUnknownDependencies()
    {
        var proposal = new PlannerProposal
        {
            ProposalId = "planner-proposal",
            PlannerBackend = "claude_messages",
            GoalSummary = "close a governed planning gap",
            RecommendedAction = PlannerRecommendedAction.ProposeWork,
            ProposedTasks =
            [
                new PlannerProposedTask
                {
                    TempId = "tmp-review",
                    Title = "Invalid review task",
                    Description = "This should not be planner-generated.",
                    TaskType = TaskType.Review,
                    Priority = "P1",
                    ProposalSource = "planner_gap_detection",
                    ProposalReason = "bad contract example",
                    Confidence = 0.8,
                },
            ],
            Dependencies =
            [
                new PlannerProposedDependency
                {
                    FromTaskId = "missing",
                    ToTaskId = "tmp-review",
                },
            ],
            Confidence = 0.8,
            Rationale = "validator should reject this proposal",
        };

        var result = new PlannerProposalValidator().Validate(proposal);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("cannot be of task type 'review'", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("references unknown task ids", StringComparison.Ordinal));
    }

    [Fact]
    public void Validator_WarnsWhenScopedExecutionTaskIsMissingProofTarget()
    {
        var proposal = new PlannerProposal
        {
            ProposalId = "planner-proof-target-warning",
            PlannerBackend = "claude_messages",
            GoalSummary = "require explicit proof for scoped execution work",
            RecommendedAction = PlannerRecommendedAction.ProposeWork,
            ProposedTasks =
            [
                new PlannerProposedTask
                {
                    TempId = "tmp-execution",
                    Title = "Land runtime guard",
                    Description = "Update runtime planning admission.",
                    TaskType = TaskType.Execution,
                    Priority = "P1",
                    ProposalSource = "planner_gap_detection",
                    ProposalReason = "scoped execution example",
                    Confidence = 0.8,
                    Scope = ["src/CARVES.Runtime.Application/Planning/PlannerProposalValidator.cs"],
                },
            ],
            Confidence = 0.8,
            Rationale = "validator should require proof_target metadata for scoped execution work",
        };

        var result = new PlannerProposalValidator().Validate(proposal);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, warning => warning.Contains("without proof_target metadata", StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptanceService_PersistsProofTargetMetadataOnAcceptedTasks()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(new DomainTaskGraph());
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var opportunityRepository = new JsonOpportunityRepository(workspace.Paths);
        var service = new PlannerProposalAcceptanceService(taskGraphService, opportunityRepository);
        var envelope = new PlannerProposalEnvelope
        {
            ProposalId = "planner-proof-target-acceptance",
            AdapterId = "StubPlannerAdapter",
            ProviderId = "local",
            Configured = true,
            UsedFallback = false,
            WakeReason = PlannerWakeReason.ExplicitHumanWake,
            WakeDetail = "operator requested planner pass",
            Proposal = new PlannerProposal
            {
                ProposalId = "planner-proof-target-acceptance",
                PlannerBackend = "stub",
                GoalSummary = "persist proof-target metadata",
                RecommendedAction = PlannerRecommendedAction.ProposeWork,
                ProposedTasks =
                [
                    new PlannerProposedTask
                    {
                        TempId = "tmp-proof-target",
                        Title = "Add scoped execution guard",
                        Description = "Persist proof-target metadata onto task truth.",
                        TaskType = TaskType.Execution,
                        Priority = "P1",
                        ProposalSource = "planner_gap_detection",
                        ProposalReason = "guard scoped execution work",
                        Confidence = 0.9,
                        Scope = ["src/CARVES.Runtime.Application/Planning/PlannerProposalValidator.cs"],
                        ProofTarget = new RealityProofTarget
                        {
                            Kind = ProofTargetKind.Boundary,
                            Description = "Planner admission records an explicit proof target for scoped execution work.",
                        },
                    },
                ],
                Confidence = 0.9,
                Rationale = "accepted planner work should preserve proof-target metadata",
            },
        };

        var accepted = service.Accept(envelope);
        var acceptedTask = taskGraphService.Load().ListTasks().Single();

        Assert.Equal(PlannerProposalAcceptanceStatus.Accepted, accepted.AcceptanceStatus);
        Assert.Equal("boundary", acceptedTask.Metadata[PlanningProofTargetMetadata.KindKey]);
        Assert.Equal(
            "Planner admission records an explicit proof target for scoped execution work.",
            acceptedTask.Metadata[PlanningProofTargetMetadata.DescriptionKey]);
    }

    [Fact]
    public void AcceptanceService_PartiallyAcceptsAndMarksOriginOpportunityMaterialized()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(
            new DomainTaskGraph(
            [
                new TaskNode
                {
                    TaskId = "T-EXISTING",
                    Title = "Existing planner task",
                    Description = "duplicate fixture",
                    Status = DomainTaskStatus.Suggested,
                    TaskType = TaskType.Planning,
                    Priority = "P2",
                    Source = "PLANNER_ADAPTER",
                    Scope = [".ai/opportunities/index.json"],
                    Acceptance = ["already exists"],
                },
            ]));
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var opportunityRepository = new JsonOpportunityRepository(workspace.Paths);
        var opportunity = new Opportunity
        {
            OpportunityId = "OPP-test-origin",
            Source = OpportunitySource.MemoryDrift,
            Fingerprint = "memory:planner",
            Title = "Audit planner memory",
            Description = "module memory is stale",
            Reason = "memory drift detected",
            Severity = OpportunitySeverity.Medium,
            Confidence = 0.7,
            RelatedFiles = [".ai/memory/modules/planner.md"],
        };
        opportunityRepository.Save(new OpportunitySnapshot
        {
            Version = 1,
            GeneratedAt = DateTimeOffset.UtcNow,
            Items = [opportunity],
        });

        var service = new PlannerProposalAcceptanceService(taskGraphService, opportunityRepository);
        var envelope = new PlannerProposalEnvelope
        {
            ProposalId = "planner-proposal-acceptance",
            AdapterId = "StubPlannerAdapter",
            ProviderId = "local",
            Configured = true,
            UsedFallback = false,
            WakeReason = PlannerWakeReason.ExplicitHumanWake,
            WakeDetail = "operator requested planner pass",
            Proposal = new PlannerProposal
            {
                ProposalId = "planner-proposal-acceptance",
                PlannerBackend = "stub",
                GoalSummary = "materialize governed work",
                RecommendedAction = PlannerRecommendedAction.ProposeWork,
                ProposedTasks =
                [
                    new PlannerProposedTask
                    {
                        TempId = "tmp-duplicate",
                        TaskId = "T-EXISTING",
                        Title = "Existing planner task",
                        Description = "duplicate fixture",
                        TaskType = TaskType.Planning,
                        Priority = "P2",
                        ProposalSource = "memory_audit",
                        ProposalReason = "duplicate path",
                        Confidence = 0.6,
                    },
                    new PlannerProposedTask
                    {
                        TempId = "tmp-new",
                        Title = "Audit planner memory",
                        Description = "Refresh planner memory truth.",
                        TaskType = TaskType.Planning,
                        Priority = "P2",
                        ProposalSource = "memory_audit",
                        ProposalReason = "memory drift detected",
                        Confidence = 0.85,
                        Scope = [".ai/memory/modules/planner.md"],
                        Acceptance = ["memory audit task exists in governed truth"],
                        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["origin_opportunity_id"] = "OPP-test-origin",
                        },
                    },
                ],
                Confidence = 0.8,
                Rationale = "planner suggested governed follow-up work",
            },
        };

        var accepted = service.Accept(envelope);
        var graph = taskGraphService.Load();
        var snapshot = opportunityRepository.Load();
        var materialized = snapshot.Items.Single(item => item.OpportunityId == "OPP-test-origin");

        Assert.Equal(PlannerProposalAcceptanceStatus.PartiallyAccepted, accepted.AcceptanceStatus);
        Assert.Single(accepted.AcceptedTaskIds);
        Assert.Single(accepted.RejectedTaskIds);
        Assert.Contains("T-EXISTING", accepted.RejectedTaskIds);
        Assert.Contains(graph.ListTasks(), task => accepted.AcceptedTaskIds.Contains(task.TaskId, StringComparer.Ordinal));
        Assert.Equal(OpportunityStatus.Materialized, materialized.Status);
        Assert.Equal(accepted.AcceptedTaskIds, materialized.MaterializedTaskIds);
    }

    [Fact]
    public void AcceptanceService_SynthesizesAcceptanceContractForAcceptedTask()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(new DomainTaskGraph());
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var opportunityRepository = new JsonOpportunityRepository(workspace.Paths);
        var service = new PlannerProposalAcceptanceService(taskGraphService, opportunityRepository);
        var envelope = new PlannerProposalEnvelope
        {
            ProposalId = "planner-proposal-acceptance-contract",
            AdapterId = "StubPlannerAdapter",
            ProviderId = "local",
            Configured = true,
            UsedFallback = false,
            WakeReason = PlannerWakeReason.ExplicitHumanWake,
            WakeDetail = "operator requested planner pass",
            Proposal = new PlannerProposal
            {
                ProposalId = "planner-proposal-acceptance-contract",
                PlannerBackend = "stub",
                GoalSummary = "materialize task with synthesized contract",
                RecommendedAction = PlannerRecommendedAction.ProposeWork,
                ProposedTasks =
                [
                    new PlannerProposedTask
                    {
                        TempId = "tmp-contract",
                        Title = "Add acceptance contract projection",
                        Description = "Synthesize a minimal acceptance contract when planner payload omits one.",
                        TaskType = TaskType.Planning,
                        Priority = "P2",
                        ProposalSource = "planner_gap_detection",
                        ProposalReason = "contract-first planning slice",
                        Confidence = 0.9,
                        Acceptance = ["accepted task carries an acceptance contract"],
                        Constraints = ["do not create parallel planning truth"],
                    },
                ],
                Confidence = 0.9,
                Rationale = "accepted planner work should always carry a minimal acceptance contract",
            },
        };

        var accepted = service.Accept(envelope);
        var task = taskGraphService.Load().ListTasks().Single();

        Assert.Equal(PlannerProposalAcceptanceStatus.Accepted, accepted.AcceptanceStatus);
        Assert.NotNull(task.AcceptanceContract);
        Assert.Equal($"AC-{task.TaskId}", task.AcceptanceContract!.ContractId);
        Assert.Equal(AcceptanceContractLifecycleStatus.Compiled, task.AcceptanceContract.Status);
        Assert.Contains("do not create parallel planning truth", task.AcceptanceContract.Constraints.MustNot);
    }
}
