using System.Text.Json;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeMethodologyComplianceServiceTests
{
    [Fact]
    public void CreateCardDraft_RuntimeScopedDraft_NormalizesMethodologyAcknowledgment()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreatePlanningDraftService(workspace);
        var payloadPath = workspace.WriteFile("drafts/runtime-card.json", JsonSerializer.Serialize(new
        {
            card_id = "CARD-245-METHOD",
            title = "Runtime host governance surface",
            goal = "Add host pause and dashboard run drilldown.",
            acceptance = new[] { "host control is explicit", "dashboard explains recent runs" },
        }));

        var draft = service.CreateCardDraft(payloadPath);

        Assert.True(draft.MethodologyRequired);
        Assert.True(draft.MethodologyAcknowledged);
        Assert.Equal("new_delta", draft.MethodologyCoverageStatus);
        Assert.Contains(
            "05_EXECUTION_OS_METHODOLOGY.md",
            File.ReadAllText(draft.MarkdownPath),
            StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureAsyncResumeGate_WritesOrderedDeferredLineage()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeMethodologyComplianceService(workspace.Paths);

        var gate = service.EnsureAsyncResumeGate();

        Assert.Equal("async-multi-worker-resume-gate.v1", gate.SchemaVersion);
        Assert.Equal("CARD-152", gate.ResumeOrder[0].CardIds[0]);
        Assert.Contains("CARD-136", gate.ResumeOrder[1].CardIds);
        Assert.Contains("CARD-141", gate.ResumeOrder[2].CardIds);
        Assert.Contains("CARD-150", gate.ResumeOrder[3].CardIds);
        Assert.True(File.Exists(service.AsyncResumeGatePath));
    }

    private static PlanningDraftService CreatePlanningDraftService(TemporaryWorkspace workspace)
    {
        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph()),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        return new PlanningDraftService(
            workspace.Paths,
            taskGraphService,
            new Carves.Runtime.Infrastructure.Persistence.JsonCardDraftRepository(workspace.Paths),
            new Carves.Runtime.Infrastructure.Persistence.JsonTaskGraphDraftRepository(workspace.Paths));
    }
}
