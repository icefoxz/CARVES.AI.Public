using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerContextAssembler
{
    private readonly TaskGraphService taskGraphService;
    private readonly ICodeGraphQueryService codeGraphQueryService;
    private readonly MemoryService memoryService;
    private readonly ContextPackService contextPackService;
    private readonly CarvesCodeStandard carvesCodeStandard;
    private readonly PlannerAutonomyPolicy plannerAutonomyPolicy;
    private readonly PlannerIntentRoutingService plannerIntentRoutingService;

    public PlannerContextAssembler(
        TaskGraphService taskGraphService,
        ICodeGraphQueryService codeGraphQueryService,
        MemoryService memoryService,
        ContextPackService contextPackService,
        CarvesCodeStandard carvesCodeStandard,
        PlannerAutonomyPolicy plannerAutonomyPolicy,
        PlannerIntentRoutingService plannerIntentRoutingService)
    {
        this.taskGraphService = taskGraphService;
        this.codeGraphQueryService = codeGraphQueryService;
        this.memoryService = memoryService;
        this.contextPackService = contextPackService;
        this.carvesCodeStandard = carvesCodeStandard;
        this.plannerAutonomyPolicy = plannerAutonomyPolicy;
        this.plannerIntentRoutingService = plannerIntentRoutingService;
    }

    public PlannerRunRequest Build(
        RuntimeSessionState session,
        PlannerWakeReason wakeReason,
        string wakeDetail,
        OpportunitySnapshot snapshot,
        IReadOnlyList<Opportunity> selectedOpportunities,
        OpportunityTaskPreviewResult? preview)
    {
        var graph = taskGraphService.Load();
        var codeGraphManifest = codeGraphQueryService.LoadManifest();
        var projectDocs = memoryService.LoadProjectMemoryDocuments();
        var moduleDocs = memoryService.LoadModuleMemoryDocuments();
        var contextPack = contextPackService.BuildForPlanner(session, wakeReason, wakeDetail, snapshot, selectedOpportunities, preview, model: null);

        return new PlannerRunRequest
        {
            ProposalId = $"planner-{Guid.NewGuid():N}",
            RepoRoot = session.AttachedRepoRoot,
            Session = session,
            WakeReason = wakeReason,
            PlannerIntent = plannerIntentRoutingService.Classify(wakeReason),
            WakeDetail = wakeDetail,
            NextPlannerRound = session.PlannerRound + 1,
            GoalSummary = $"runtime stage {RuntimeStageInfo.CurrentStage}; wake reason {PlannerLifecycleSemantics.DescribeWakeReason(wakeReason)}",
            CurrentStage = RuntimeStageInfo.CurrentStage,
            TaskGraphSummary = $"tasks={graph.Tasks.Count}; pending={graph.Tasks.Values.Count(task => task.Status == Carves.Runtime.Domain.Tasks.TaskStatus.Pending)}; review={graph.Tasks.Values.Count(task => task.Status == Carves.Runtime.Domain.Tasks.TaskStatus.Review)}; suggested={graph.Tasks.Values.Count(task => task.Status == Carves.Runtime.Domain.Tasks.TaskStatus.Suggested)}",
            BlockedTaskSummary = BuildBlockedTaskSummary(graph),
            OpportunitySummary = BuildOpportunitySummary(snapshot, selectedOpportunities),
            MemorySummary = $"project_docs={projectDocs.Count}; module_docs={moduleDocs.Count}",
            CodeGraphSummary = $"modules={codeGraphManifest.ModuleCount}; files={codeGraphManifest.FileCount}; callables={codeGraphManifest.CallableCount}; dependencies={codeGraphManifest.DependencyCount}",
            GovernanceSummary = $"planner_intent={plannerIntentRoutingService.Classify(wakeReason)}; planner_round_cap={plannerAutonomyPolicy.MaxPlannerRounds}; generated_task_cap={plannerAutonomyPolicy.MaxGeneratedTasks}; opportunities_per_round={plannerAutonomyPolicy.MaxOpportunitiesPerRound}",
            NamingSummary = $"grammar={carvesCodeStandard.ExtremeNaming.NamingGrammar}; canonical_vocabulary_required={carvesCodeStandard.ExtremeNaming.CanonicalVocabularyRequired}",
            DependencySummary = $"one_way={carvesCodeStandard.DependencyContract.DependencyDirectionOneWay}; recorder_model={carvesCodeStandard.DependencyContract.RecorderAccessModel}",
            FailureSummary = BuildFailureSummary(contextPack),
            ContextPack = contextPack,
            SelectedOpportunities = selectedOpportunities,
            PreviewTasks = preview?.ProposedTasks.Select(task => new PlannerProposedTask
            {
                TempId = task.TaskId,
                TaskId = task.TaskId,
                Title = task.Title,
                Description = task.Description,
                TaskType = task.TaskType,
                Priority = task.Priority,
                DependsOn = task.Dependencies,
                Scope = task.Scope,
                ProposalSource = task.ProposalSource.ToString(),
                ProposalReason = task.ProposalReason ?? wakeDetail,
                Confidence = task.ProposalConfidence ?? selectedOpportunities.DefaultIfEmpty().Average(item => item?.Confidence ?? 0),
                Acceptance = task.Acceptance,
                Constraints = task.Constraints,
                AcceptanceContract = task.AcceptanceContract,
                ProofTarget = PlanningProofTargetMetadata.TryRead(task.Metadata),
                Metadata = task.Metadata,
            }).ToArray() ?? Array.Empty<PlannerProposedTask>(),
            PreviewDependencies = preview?.ProposedTasks.SelectMany(task => task.Dependencies.Select(dependency => new PlannerProposedDependency
            {
                FromTaskId = dependency,
                ToTaskId = task.TaskId,
            })).ToArray() ?? Array.Empty<PlannerProposedDependency>(),
        };
    }

    private static string BuildBlockedTaskSummary(Carves.Runtime.Domain.Tasks.TaskGraph graph)
    {
        var blocked = graph.Tasks.Values
            .Where(task => task.Status == Carves.Runtime.Domain.Tasks.TaskStatus.Blocked)
            .Take(5)
            .Select(task => $"{task.TaskId}:{task.Title}")
            .ToArray();
        return blocked.Length == 0 ? "no blocked tasks" : string.Join(" | ", blocked);
    }

    private static string BuildOpportunitySummary(OpportunitySnapshot snapshot, IReadOnlyList<Opportunity> selectedOpportunities)
    {
        var focus = selectedOpportunities
            .Take(5)
            .Select(item => $"{item.OpportunityId}:{item.Source}:{item.Severity}")
            .ToArray();
        return focus.Length == 0
            ? $"open={snapshot.Items.Count(item => item.Status == OpportunityStatus.Open)}; selected=0"
            : $"open={snapshot.Items.Count(item => item.Status == OpportunityStatus.Open)}; selected={selectedOpportunities.Count}; focus={string.Join(" | ", focus)}";
    }

    private static string BuildFailureSummary(Carves.Runtime.Domain.AI.ContextPack contextPack)
    {
        if (contextPack.LastFailureSummary is null)
        {
            return "no recorded failures";
        }

        var summary = contextPack.LastFailureSummary;
        return $"last_failure_type={summary.FailureType}; lane={summary.FailureLane}; build={summary.BuildStatus}; tests={summary.TestStatus}; runtime={summary.RuntimeStatus}";
    }
}
