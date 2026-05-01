using System.Text.Json;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class PlanningDraftServiceTests
{
    [Fact]
    public void CreateCardDraft_NormalizesFieldsAndWritesMarkdown()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace, new DomainTaskGraph());
        var payloadPath = workspace.WriteFile("drafts/card.json", JsonSerializer.Serialize(new
        {
            title = "Draft execution contract",
            goal = "Persist a governed draft card.",
            acceptance = new[] { "draft exists" },
        }));

        var draft = service.CreateCardDraft(payloadPath);

        Assert.Equal(CardLifecycleState.Draft, draft.Status);
        Assert.StartsWith("CARD-", draft.CardId, StringComparison.Ordinal);
        Assert.True(File.Exists(draft.MarkdownPath));
        Assert.Contains("LifecycleState: draft", File.ReadAllText(draft.MarkdownPath), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateCardDraft_PersistsRealityModelAndRendersMarkdown()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace, new DomainTaskGraph());
        var payloadPath = workspace.WriteFile("drafts/card-reality.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-312A",
            title = "Reality-aware draft",
            goal = "Persist a draft with a minimum reality model.",
            acceptance = new[] { "draft exists" },
            reality_model = new
            {
                outer_vision = "Integrate reality gradient governance into runtime planning.",
                current_solid_scope = "Runtime planning and review still use legacy draft semantics.",
                next_real_slice = "Allow card drafts and templates to carry a minimum reality model.",
                reality_state = "bounded",
                solidity_class = "ghost",
                proof_target = new
                {
                    kind = "boundary",
                    description = "Persist a minimal reality model in governed card draft truth.",
                },
                non_goals = new[] { "Do not add review promotion writeback yet." },
                illusion_risk = new
                {
                    level = "medium",
                    reasons = new[] { "Review and projection work land in later tasks." },
                },
                promotion_gate = "card_draft_reality_model_persisted",
            },
        }));

        var draft = service.CreateCardDraft(payloadPath);
        var markdown = File.ReadAllText(draft.MarkdownPath);

        Assert.NotNull(draft.RealityModel);
        Assert.Equal("Integrate reality gradient governance into runtime planning.", draft.RealityModel!.OuterVision);
        Assert.Equal(RealityState.Bounded, draft.RealityModel.RealityState);
        Assert.Equal(SolidityClass.Ghost, draft.RealityModel.SolidityClass);
        Assert.Equal(ProofTargetKind.Boundary, draft.RealityModel.ProofTarget.Kind);
        Assert.Contains("## Reality Model", markdown, StringComparison.Ordinal);
        Assert.Contains("- outer_vision: Integrate reality gradient governance into runtime planning.", markdown, StringComparison.Ordinal);
        Assert.Contains("- proof_target.kind: boundary", markdown, StringComparison.Ordinal);
        Assert.Contains("- promotion_gate: card_draft_reality_model_persisted", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateCardDraft_PersistsPlanningLineage()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace, new DomainTaskGraph());
        var payloadPath = workspace.WriteFile("drafts/card-lineage.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-PLAN-201",
            title = "Planning-lineage draft",
            goal = "Persist planning lineage on the card draft.",
            acceptance = new[] { "lineage exists" },
            planning_lineage = new
            {
                planning_slot_id = "primary_formal_planning",
                active_planning_card_id = "PLANCARD-20260410-120000",
                source_intent_draft_id = "intent-draft-123",
                source_candidate_card_id = "candidate-first-slice",
                formal_planning_state = "plan_bound",
            },
        }));

        var draft = service.CreateCardDraft(payloadPath);

        Assert.NotNull(draft.PlanningLineage);
        Assert.Equal("primary_formal_planning", draft.PlanningLineage!.PlanningSlotId);
        Assert.Equal("PLANCARD-20260410-120000", draft.PlanningLineage.ActivePlanningCardId);
        Assert.Equal(FormalPlanningState.PlanBound, draft.PlanningLineage.FormalPlanningState);
    }

    [Fact]
    public void CreateCardDraft_SynthesizesAcceptanceContractAndRendersMarkdown()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace, new DomainTaskGraph());
        var payloadPath = workspace.WriteFile("drafts/card-contract.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-AC-001",
            title = "Acceptance-contract draft",
            goal = "Carry a bounded acceptance contract through planning ingress.",
            acceptance = new[] { "contract is persisted", "markdown projects the contract" },
            constraints = new[] { "do not create a second planner" },
        }));

        var draft = service.CreateCardDraft(payloadPath);
        var markdown = File.ReadAllText(draft.MarkdownPath);
        var parsed = new CardParser().Parse(draft.MarkdownPath);

        Assert.NotNull(draft.AcceptanceContract);
        Assert.Equal("AC-CARD-AC-001", draft.AcceptanceContract!.ContractId);
        Assert.Equal(AcceptanceContractLifecycleStatus.Draft, draft.AcceptanceContract.Status);
        Assert.Equal("Carry a bounded acceptance contract through planning ingress.", draft.AcceptanceContract.Intent.Goal);
        Assert.NotNull(parsed.AcceptanceContract);
        Assert.Equal("AC-CARD-AC-001", parsed.AcceptanceContract!.ContractId);
        Assert.Contains("## Acceptance Contract", markdown, StringComparison.Ordinal);
        Assert.Contains("\"contract_id\": \"AC-CARD-AC-001\"", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproveTaskGraphDraft_PromotesTasksToSuggested()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(new DomainTaskGraph());
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = CreateService(workspace, repository.Load(), taskGraphService);
        var cardPayloadPath = workspace.WriteFile("drafts/card.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-200",
            title = "Draft execution contract",
            goal = "Persist a governed draft card.",
            acceptance = new[] { "draft exists" },
        }));
        var payloadPath = workspace.WriteFile("drafts/taskgraph.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-200",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-200-001",
                    title = "Create first task",
                    description = "first",
                    acceptance = new[] { "done" },
                },
                new
                {
                    task_id = "T-CARD-200-002",
                    title = "Create dependent task",
                    description = "second",
                    dependencies = new[] { "T-CARD-200-001" },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-200", CardLifecycleState.Approved, "approved for planning");
        var draft = service.CreateTaskGraphDraft(payloadPath);
        var approved = service.ApproveTaskGraphDraft(draft.DraftId, "approve");

        Assert.Equal(PlanningDraftStatus.Approved, approved.Status);
        Assert.Equal(2, taskGraphService.Load().Tasks.Count);
        Assert.All(taskGraphService.Load().Tasks.Values, task => Assert.Equal(Carves.Runtime.Domain.Tasks.TaskStatus.Pending, task.Status));
        Assert.All(taskGraphService.Load().Tasks.Values, task => Assert.NotNull(task.AcceptanceContract));
        Assert.All(taskGraphService.Load().Tasks.Values, task =>
            Assert.Equal(
                TaskGraphAcceptanceContractMaterializationGuard.SynthesizedMinimumProjectionSource,
                task.Metadata[TaskGraphAcceptanceContractMaterializationGuard.MetadataProjectionSourceKey]));
    }

    [Fact]
    public void CreateTaskGraphDraft_RecordsSynthesizedMinimumContractSource()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(new DomainTaskGraph());
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = CreateService(workspace, repository.Load(), taskGraphService);
        var cardPayloadPath = workspace.WriteFile("drafts/card-synthesized-contract.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-206",
            title = "Synthesized contract source",
            goal = "Record minimum contract synthesis at taskgraph materialization.",
            acceptance = new[] { "source is recorded" },
        }));
        var payloadPath = workspace.WriteFile("drafts/taskgraph-synthesized-contract.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-206",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-206-001",
                    title = "Record synthesized source",
                    description = "Materialize a task without explicit contract payload.",
                    acceptance = new[] { "metadata records synthesized source" },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-206", CardLifecycleState.Approved, "approved for planning");
        var draft = service.CreateTaskGraphDraft(payloadPath);
        var report = TaskGraphAcceptanceContractMaterializationGuard.Evaluate(draft);
        service.ApproveTaskGraphDraft(draft.DraftId, "approve");
        var task = taskGraphService.Load().Tasks["T-CARD-206-001"];

        Assert.Equal(TaskGraphAcceptanceContractMaterializationGuard.ReadyState, report.State);
        Assert.Equal(1, report.SynthesizedMinimumContractCount);
        Assert.Equal(TaskGraphAcceptanceContractMaterializationGuard.SynthesizedMinimumProjectionSource, Assert.Single(draft.Tasks).AcceptanceContractProjectionSource);
        Assert.Equal(TaskGraphAcceptanceContractMaterializationGuard.ProjectedState, task.Metadata[TaskGraphAcceptanceContractMaterializationGuard.MetadataStateKey]);
        Assert.Equal(TaskGraphAcceptanceContractMaterializationGuard.SynthesizedMinimumProjectionSource, task.Metadata[TaskGraphAcceptanceContractMaterializationGuard.MetadataProjectionSourceKey]);
        Assert.Equal(TaskGraphAcceptanceContractMaterializationGuard.AutoMinimumContractPolicy, task.Metadata[TaskGraphAcceptanceContractMaterializationGuard.MetadataProjectionPolicyKey]);
    }

    [Fact]
    public void CreateTaskGraphDraft_InheritsPlanningLineageFromCardDraft()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace, new DomainTaskGraph());
        var cardPayloadPath = workspace.WriteFile("drafts/card-inherit-lineage.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-PLAN-202",
            title = "Planning-lineage inheritance draft",
            goal = "Carry planning lineage from card draft to taskgraph draft.",
            acceptance = new[] { "lineage exists" },
            planning_lineage = new
            {
                planning_slot_id = "primary_formal_planning",
                active_planning_card_id = "PLANCARD-20260410-120500",
                source_intent_draft_id = "intent-draft-456",
                source_candidate_card_id = "candidate-first-slice",
                formal_planning_state = "plan_bound",
            },
        }));
        var payloadPath = workspace.WriteFile("drafts/taskgraph-inherit-lineage.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-PLAN-202",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-PLAN-202-001",
                    title = "Bound task",
                    description = "Bound task",
                    acceptance = new[] { "done" },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-PLAN-202", CardLifecycleState.Approved, "approved for planning");
        var draft = service.CreateTaskGraphDraft(payloadPath);

        Assert.NotNull(draft.PlanningLineage);
        Assert.Equal("PLANCARD-20260410-120500", draft.PlanningLineage!.ActivePlanningCardId);
        Assert.Equal("intent-draft-456", draft.PlanningLineage.SourceIntentDraftId);
    }

    [Fact]
    public void CreateTaskGraphDraft_RejectsFormalPlanningLineageWithoutActivePlanHandle()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo");
        var builder = new Carves.Runtime.Infrastructure.CodeGraph.FileCodeGraphBuilder(workspace.RootPath, workspace.Paths, TestSystemConfigFactory.Create());
        var query = new Carves.Runtime.Infrastructure.CodeGraph.FileCodeGraphQueryService(workspace.Paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, workspace.Paths, TestSystemConfigFactory.Create(), builder, query);
        var intentDiscoveryService = new Carves.Runtime.Application.Interaction.IntentDiscoveryService(
            workspace.RootPath,
            workspace.Paths,
            new Carves.Runtime.Infrastructure.Persistence.JsonIntentDraftRepository(workspace.Paths),
            understanding);
        var gateService = new FormalPlanningExecutionGateService(intentDiscoveryService, new InMemoryManagedWorkspaceLeaseRepository());
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = new PlanningDraftService(
            workspace.Paths,
            taskGraphService,
            new Carves.Runtime.Infrastructure.Persistence.JsonCardDraftRepository(workspace.Paths),
            new Carves.Runtime.Infrastructure.Persistence.JsonTaskGraphDraftRepository(workspace.Paths),
            formalPlanningExecutionGateService: gateService);
        var cardPayloadPath = workspace.WriteFile("drafts/card-formal-lineage.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-PLAN-205",
            title = "Formal planning lineage draft",
            goal = "Reject taskgraph persistence when no active plan handle exists.",
            acceptance = new[] { "gate rejects draft persistence" },
            planning_lineage = new
            {
                planning_slot_id = "primary_formal_planning",
                active_planning_card_id = "PLANCARD-20260410-121500",
                source_intent_draft_id = "intent-draft-999",
                source_candidate_card_id = "candidate-first-slice",
                formal_planning_state = "plan_bound",
            },
        }));
        var taskGraphPayloadPath = workspace.WriteFile("drafts/taskgraph-formal-lineage.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-PLAN-205",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-PLAN-205-001",
                    title = "Formal planning gated task",
                    description = "This draft should stay blocked until plan init is active.",
                    acceptance = new[] { "done" },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-PLAN-205", CardLifecycleState.Approved, "approved for planning");

        var error = Assert.Throws<InvalidOperationException>(() => service.CreateTaskGraphDraft(taskGraphPayloadPath));

        Assert.Contains("no active formal planning card exists", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApproveTaskGraphDraft_ProjectsPlanningLineageIntoTaskMetadata()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(new DomainTaskGraph());
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = CreateService(workspace, repository.Load(), taskGraphService);
        var cardPayloadPath = workspace.WriteFile("drafts/card-lineage-approved.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-PLAN-203",
            title = "Planning-lineage approval draft",
            goal = "Project planning lineage into approved task truth.",
            acceptance = new[] { "lineage exists" },
            planning_lineage = new
            {
                planning_slot_id = "primary_formal_planning",
                active_planning_card_id = "PLANCARD-20260410-121000",
                source_intent_draft_id = "intent-draft-789",
                source_candidate_card_id = "candidate-first-slice",
                formal_planning_state = "plan_bound",
            },
        }));
        var payloadPath = workspace.WriteFile("drafts/taskgraph-lineage-approved.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-PLAN-203",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-PLAN-203-001",
                    title = "Project lineage",
                    description = "Project lineage",
                    acceptance = new[] { "done" },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-PLAN-203", CardLifecycleState.Approved, "approved for planning");
        var draft = service.CreateTaskGraphDraft(payloadPath);
        service.ApproveTaskGraphDraft(draft.DraftId, "approve");
        var task = taskGraphService.Load().Tasks["T-CARD-PLAN-203-001"];

        Assert.Equal("primary_formal_planning", task.Metadata[PlanningLineageMetadata.PlanningSlotIdKey]);
        Assert.Equal("PLANCARD-20260410-121000", task.Metadata[PlanningLineageMetadata.ActivePlanningCardIdKey]);
        Assert.Equal("intent-draft-789", task.Metadata[PlanningLineageMetadata.SourceIntentDraftIdKey]);
        Assert.Equal("candidate-first-slice", task.Metadata[PlanningLineageMetadata.SourceCandidateCardIdKey]);
        Assert.Equal("plan_bound", task.Metadata[PlanningLineageMetadata.FormalPlanningStateKey]);
    }

    [Fact]
    public void CreateTaskGraphDraft_RejectsScopedExecutionTaskWithoutProofTarget()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace, new DomainTaskGraph());
        var cardPayloadPath = workspace.WriteFile("drafts/card.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-203",
            title = "Reality-aware planning",
            goal = "Require proof target for scoped execution work.",
            acceptance = new[] { "approved first" },
        }));
        var payloadPath = workspace.WriteFile("drafts/taskgraph-proof-target.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-203",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-203-001",
                    title = "Add guard",
                    description = "Land a scoped execution task without proof target.",
                    scope = new[] { "src/CARVES.Runtime.Application/Planning/PlannerProposalValidator.cs" },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-203", CardLifecycleState.Approved, "approved for planning");

        var error = Assert.Throws<InvalidOperationException>(() => service.CreateTaskGraphDraft(payloadPath));
        Assert.Contains("requires proof_target", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApproveTaskGraphDraft_PersistsProofTargetMetadataForScopedExecutionTask()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(new DomainTaskGraph());
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = CreateService(workspace, repository.Load(), taskGraphService);
        var cardPayloadPath = workspace.WriteFile("drafts/card.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-204",
            title = "Proof-target draft",
            goal = "Persist proof target into approved task truth.",
            acceptance = new[] { "task exists" },
        }));
        var payloadPath = workspace.WriteFile("drafts/taskgraph-proof-target-approved.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-204",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-204-001",
                    title = "Land guard",
                    description = "Add proof-target guard.",
                    scope = new[] { "src/CARVES.Runtime.Application/Planning/PlannerProposalValidator.cs" },
                    proof_target = new
                    {
                        kind = "boundary",
                        description = "Task admission records a bounded proof target for scoped execution work.",
                    },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-204", CardLifecycleState.Approved, "approved for planning");
        var draft = service.CreateTaskGraphDraft(payloadPath);

        service.ApproveTaskGraphDraft(draft.DraftId, "approve");
        var task = taskGraphService.Load().Tasks["T-CARD-204-001"];

        Assert.Equal("boundary", task.Metadata[PlanningProofTargetMetadata.KindKey]);
        Assert.Equal(
            "Task admission records a bounded proof target for scoped execution work.",
            task.Metadata[PlanningProofTargetMetadata.DescriptionKey]);
    }

    [Fact]
    public void CreateTaskGraphDraft_PersistsExplicitAcceptanceContract()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(new DomainTaskGraph());
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = CreateService(workspace, repository.Load(), taskGraphService);
        var cardPayloadPath = workspace.WriteFile("drafts/card-acceptance-contract.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-205",
            title = "Task contract draft",
            goal = "Persist explicit task acceptance contract payloads.",
            acceptance = new[] { "task draft exists" },
        }));
        var payloadPath = workspace.WriteFile("drafts/taskgraph-acceptance-contract.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-205",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-205-001",
                    title = "Persist explicit task contract",
                    description = "Carry explicit contract fields into taskgraph draft truth.",
                    acceptance_contract = new
                    {
                        contract_id = "AC-T-CARD-205-001",
                        title = "Explicit task contract",
                        status = "compiled",
                        owner = "planner",
                        created_at_utc = "2026-04-09T00:00:00Z",
                        intent = new
                        {
                            goal = "Carry explicit contract fields into taskgraph draft truth.",
                            business_value = "Prevent acceptance semantics from drifting before task truth exists",
                        },
                        acceptance_examples = new[]
                        {
                            new
                            {
                                given = "a taskgraph draft payload includes acceptance_contract",
                                when = "the draft is created",
                                then = "the contract is stored on the draft task",
                            },
                        },
                        checks = new
                        {
                            policy_checks = new[] { "No second control plane" },
                        },
                        constraints = new
                        {
                            must_not = new[] { "Do not create parallel planning truth" },
                        },
                        human_review = new
                        {
                            required = true,
                            provisional_allowed = false,
                            decisions = new[] { "accept", "reject", "reopen" },
                        },
                        traceability = new
                        {
                            derived_task_ids = Array.Empty<string>(),
                            related_artifacts = Array.Empty<string>(),
                        },
                    },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-205", CardLifecycleState.Approved, "approved for planning");
        var draft = service.CreateTaskGraphDraft(payloadPath);
        service.ApproveTaskGraphDraft(draft.DraftId, "approve");

        var task = Assert.Single(draft.Tasks);
        var materializedTask = taskGraphService.Load().Tasks["T-CARD-205-001"];
        Assert.NotNull(task.AcceptanceContract);
        Assert.Equal("AC-T-CARD-205-001", task.AcceptanceContract!.ContractId);
        Assert.Equal(TaskGraphAcceptanceContractMaterializationGuard.ExplicitProjectionSource, task.AcceptanceContractProjectionSource);
        Assert.Equal(TaskGraphAcceptanceContractMaterializationGuard.ExplicitProjectionSource, materializedTask.Metadata[TaskGraphAcceptanceContractMaterializationGuard.MetadataProjectionSourceKey]);
        Assert.Single(task.AcceptanceContract.AcceptanceExamples);
        Assert.Contains("No second control plane", task.AcceptanceContract.Checks.PolicyChecks);
    }

    [Fact]
    public void ApproveTaskGraphDraft_RejectsExecutableTaskWithoutContractProjection()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(new DomainTaskGraph());
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var cardDraftRepository = new Carves.Runtime.Infrastructure.Persistence.JsonCardDraftRepository(workspace.Paths);
        var taskGraphDraftRepository = new Carves.Runtime.Infrastructure.Persistence.JsonTaskGraphDraftRepository(workspace.Paths);
        var service = new PlanningDraftService(
            workspace.Paths,
            taskGraphService,
            cardDraftRepository,
            taskGraphDraftRepository);
        var cardPayloadPath = workspace.WriteFile("drafts/card-missing-contract.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-207",
            title = "Missing contract guard",
            goal = "Block malformed executable draft tasks.",
            acceptance = new[] { "approval fails before task truth mutation" },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-207", CardLifecycleState.Approved, "approved for planning");
        taskGraphDraftRepository.Save(new TaskGraphDraftRecord
        {
            DraftId = "TG-CARD-207-BLOCKED",
            CardId = "CARD-207",
            Status = PlanningDraftStatus.Draft,
            Tasks =
            [
                new TaskGraphDraftTask
                {
                    TaskId = "T-CARD-207-001",
                    Title = "Malformed executable draft task",
                    Description = "This task bypassed planning ingress normalization.",
                    TaskType = TaskType.Execution,
                    Priority = "P1",
                },
            ],
        });

        var error = Assert.Throws<InvalidOperationException>(() => service.ApproveTaskGraphDraft("TG-CARD-207-BLOCKED", "approve"));

        Assert.Contains("acceptance_contract_missing", error.Message, StringComparison.Ordinal);
        Assert.Empty(taskGraphService.Load().Tasks);
    }

    [Fact]
    public void TaskGraphMaterializationGuard_DoesNotRequireContractForNonExecutableTasks()
    {
        var report = TaskGraphAcceptanceContractMaterializationGuard.Evaluate(new TaskGraphDraftRecord
        {
            DraftId = "TG-CARD-208-PLANNING",
            CardId = "CARD-208",
            Status = PlanningDraftStatus.Draft,
            Tasks =
            [
                new TaskGraphDraftTask
                {
                    TaskId = "T-CARD-208-PLANNING-001",
                    Title = "Planning follow-up",
                    Description = "Planning task without worker execution.",
                    TaskType = TaskType.Planning,
                    Priority = "P2",
                },
            ],
        });

        var task = Assert.Single(report.Tasks);
        Assert.Equal(TaskGraphAcceptanceContractMaterializationGuard.ReadyState, report.State);
        Assert.False(report.BlocksMaterialization);
        Assert.Equal(0, report.ExecutableTaskCount);
        Assert.Equal(TaskGraphAcceptanceContractMaterializationGuard.NotRequiredState, task.State);
        Assert.False(task.RequiresContract);
    }

    [Fact]
    public void ApproveTaskGraphDraft_PersistsRoleBindingMetadataAndDefaults()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new InMemoryTaskGraphRepository(new DomainTaskGraph());
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = CreateService(workspace, repository.Load(), taskGraphService);
        var cardPayloadPath = workspace.WriteFile("drafts/card-role-binding.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-ROLE-201",
            title = "Role binding draft",
            goal = "Persist role binding defaults into approved task truth.",
            acceptance = new[] { "role binding exists" },
        }));
        var payloadPath = workspace.WriteFile("drafts/taskgraph-role-binding.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-ROLE-201",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-ROLE-201-001",
                    title = "Persist role binding",
                    description = "Add explicit role binding fields for governed task truth.",
                    scope = new[] { "src/CARVES.Runtime.Domain/Tasks/TaskRoleBinding.cs" },
                    proof_target = new
                    {
                        kind = "boundary",
                        description = "Approved task truth carries explicit role-binding semantics.",
                    },
                    role_binding = new
                    {
                        producer = "planner",
                        approver = "operator",
                        scope_steward = "scope_custodian",
                    },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-ROLE-201", CardLifecycleState.Approved, "approved for planning");
        var draft = service.CreateTaskGraphDraft(payloadPath);

        service.ApproveTaskGraphDraft(draft.DraftId, "approve");
        var task = taskGraphService.Load().Tasks["T-CARD-ROLE-201-001"];

        Assert.Equal("planner", task.Metadata[TaskRoleBindingMetadata.ProducerKey]);
        Assert.Equal("worker", task.Metadata[TaskRoleBindingMetadata.ExecutorKey]);
        Assert.Equal("planner", task.Metadata[TaskRoleBindingMetadata.ReviewerKey]);
        Assert.Equal("operator", task.Metadata[TaskRoleBindingMetadata.ApproverKey]);
        Assert.Equal("scope_custodian", task.Metadata[TaskRoleBindingMetadata.ScopeStewardKey]);
        Assert.Equal("operator", task.Metadata[TaskRoleBindingMetadata.PolicyOwnerKey]);
    }

    [Fact]
    public void ApproveTaskGraphDraft_RejectsCycles()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace, new DomainTaskGraph());
        var cardPayloadPath = workspace.WriteFile("drafts/card.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-200",
            title = "Draft execution contract",
            goal = "Persist a governed draft card.",
            acceptance = new[] { "draft exists" },
        }));
        var payloadPath = workspace.WriteFile("drafts/cycle.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-200",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-200-001",
                    title = "A",
                    description = "A",
                    dependencies = new[] { "T-CARD-200-002" },
                },
                new
                {
                    task_id = "T-CARD-200-002",
                    title = "B",
                    description = "B",
                    dependencies = new[] { "T-CARD-200-001" },
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);
        service.SetCardStatus("CARD-200", CardLifecycleState.Approved, "approved for planning");
        var draft = service.CreateTaskGraphDraft(payloadPath);

        var error = Assert.Throws<InvalidOperationException>(() => service.ApproveTaskGraphDraft(draft.DraftId, "approve"));
        Assert.Contains("dependency cycle", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateTaskGraphDraft_RejectsManagedCardThatIsNotApproved()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace, new DomainTaskGraph());
        var cardPayloadPath = workspace.WriteFile("drafts/card.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-201",
            title = "Card draft",
            goal = "Gate taskgraph creation.",
            acceptance = new[] { "approved first" },
        }));
        var taskGraphPayloadPath = workspace.WriteFile("drafts/taskgraph.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-201",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-201-001",
                    title = "Create first task",
                    description = "first",
                },
            },
        }));

        service.CreateCardDraft(cardPayloadPath);

        var error = Assert.Throws<InvalidOperationException>(() => service.CreateTaskGraphDraft(taskGraphPayloadPath));
        Assert.Contains("cannot enter planning", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateCardDraft_AndLifecycleStatusPersist()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace, new DomainTaskGraph());
        var createPayloadPath = workspace.WriteFile("drafts/card-create.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-202",
            title = "Initial title",
            goal = "Initial goal.",
            acceptance = new[] { "exists" },
        }));
        var updatePayloadPath = workspace.WriteFile("drafts/card-update.json", JsonSerializer.Serialize(new
        {
            title = "Updated title",
            notes = new[] { "keep bounded" },
        }));

        service.CreateCardDraft(createPayloadPath);
        var updated = service.UpdateCardDraft("CARD-202", updatePayloadPath);
        var approved = service.SetCardStatus("CARD-202", CardLifecycleState.Approved, "looks good");

        Assert.Equal("Updated title", updated.Title);
        Assert.Single(updated.Notes);
        Assert.Equal(CardLifecycleState.Approved, approved.Status);
        Assert.Equal(CardLifecycleState.Approved, service.ResolveCardLifecycleState("CARD-202"));
    }

    private static PlanningDraftService CreateService(TemporaryWorkspace workspace, DomainTaskGraph graph, TaskGraphService? taskGraphService = null)
    {
        var service = taskGraphService ?? new TaskGraphService(new InMemoryTaskGraphRepository(graph), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        return new PlanningDraftService(
            workspace.Paths,
            service,
            new Carves.Runtime.Infrastructure.Persistence.JsonCardDraftRepository(workspace.Paths),
            new Carves.Runtime.Infrastructure.Persistence.JsonTaskGraphDraftRepository(workspace.Paths));
    }
}
