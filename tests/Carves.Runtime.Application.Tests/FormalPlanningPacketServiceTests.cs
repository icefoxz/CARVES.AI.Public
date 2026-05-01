using System.Text.Json;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class FormalPlanningPacketServiceTests
{
    [Fact]
    public void BuildCurrentPacket_FromActivePlanningCard_ReturnsBriefingAndReplanRules()
    {
        using var workspace = new TemporaryWorkspace();
        var services = CreateServices(workspace);

        var initialized = InitializeFormalPlanning(services.IntentDiscoveryService);

        var packet = services.PacketService.BuildCurrentPacket();

        Assert.Equal(FormalPlanningPacketService.BuildPlanHandle(initialized.Draft!.ActivePlanningCard!), packet.PlanHandle);
        Assert.Equal(FormalPlanningState.Planning, packet.FormalPlanningState);
        Assert.Equal("not_bound_yet", packet.AcceptanceContractSummary.BindingState);
        Assert.Equal(FormalPlanningNextActionPosture.PlanExportRequired, packet.Briefing.NextActionPosture);
        Assert.False(packet.Briefing.ReplanRequired);
        Assert.Equal(5, packet.ReplanRules.Count);
        Assert.Contains(
            "An explicitly accepted PROJECT.md plus the first grounded card with reviewable acceptance criteria.",
            packet.EvidenceExpectations);
    }

    [Fact]
    public void BuildCurrentPacket_BindsLinkedTruthAcrossCardTaskGraphAndTaskTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var services = CreateServices(workspace);

        InitializeFormalPlanning(services.IntentDiscoveryService);
        var exportPath = Path.Combine(workspace.RootPath, "drafts", "plan-card.json");
        services.IntentDiscoveryService.ExportActivePlanningCardPayload(exportPath);
        var cardDraft = services.PlanningDraftService.CreateCardDraft(exportPath);
        services.PlanningDraftService.SetCardStatus(cardDraft.CardId, CardLifecycleState.Approved, "approved for phase-3 proof");
        var taskGraphPayloadPath = workspace.WriteFile(
            "drafts/taskgraph-phase3.json",
            JsonSerializer.Serialize(new
            {
                card_id = cardDraft.CardId,
                tasks = new object[]
                {
                    new
                    {
                        task_id = "T-CARD-697-FAKE-001",
                        title = "Bound planning packet proof task",
                        description = "Carry planning packet lineage into approved task truth.",
                        scope = new[] { "src/CARVES.Runtime.Application/Planning/FormalPlanningPacketService.cs" },
                        acceptance = new[] { "packet links approved task truth" },
                        proof_target = new
                        {
                            kind = "boundary",
                            description = "Phase-3 planning packet reads the same planning lineage from approved task truth.",
                        },
                    },
                },
            }));
        var taskGraphDraft = services.PlanningDraftService.CreateTaskGraphDraft(taskGraphPayloadPath);
        services.PlanningDraftService.ApproveTaskGraphDraft(taskGraphDraft.DraftId, "approve for phase-3 proof");

        var packet = services.PacketService.BuildCurrentPacket();

        Assert.Equal(FormalPlanningState.ExecutionBound, packet.FormalPlanningState);
        Assert.Equal("task_truth_bound", packet.AcceptanceContractSummary.BindingState);
        Assert.Contains(cardDraft.CardId, packet.LinkedTruth.CardDraftIds);
        Assert.Contains(taskGraphDraft.DraftId, packet.LinkedTruth.TaskGraphDraftIds);
        Assert.Contains("T-CARD-697-FAKE-001", packet.LinkedTruth.TaskIds);
        Assert.Contains(
            "src/CARVES.Runtime.Application/Planning/FormalPlanningPacketService.cs",
            packet.AllowedScopeSummary);
    }

    private static IntentDiscoveryStatus InitializeFormalPlanning(IntentDiscoveryService service)
    {
        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
        return service.InitializeFormalPlanning();
    }

    private static PlanningPacketTestServices CreateServices(TemporaryWorkspace workspace)
    {
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for planning packet tests.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var intentDiscoveryService = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);
        var planningDraftService = new PlanningDraftService(
            paths,
            taskGraphService,
            new JsonCardDraftRepository(paths),
            new JsonTaskGraphDraftRepository(paths));
        var packetService = new FormalPlanningPacketService(intentDiscoveryService, planningDraftService, taskGraphService);
        return new PlanningPacketTestServices(intentDiscoveryService, planningDraftService, packetService);
    }

    private sealed record PlanningPacketTestServices(
        IntentDiscoveryService IntentDiscoveryService,
        PlanningDraftService PlanningDraftService,
        FormalPlanningPacketService PacketService);
}
