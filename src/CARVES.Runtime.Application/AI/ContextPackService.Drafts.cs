using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.AI;

public sealed partial class ContextPackService
{
    private ContextPackDraft BuildTaskDraft(TaskNode task)
    {
        var activeMemoryReadService = new ActiveMemoryReadService(paths);
        var graph = taskGraphService.Load();
        var scopeAnalysis = codeGraphQueryService.AnalyzeScope(task.Scope);
        var moduleSummaries = codeGraphQueryService.LoadModuleSummaries();
        var activeMemoryScopeHints = BuildTaskActiveMemoryScopeHints(task, scopeAnalysis);
        var moduleMemory = NarrowModuleMemory(activeMemoryReadService.LoadCompatibleDocuments("modules"), scopeAnalysis.Modules, task.Scope);
        var projectMemory = activeMemoryReadService.LoadProjectDocumentsWithProjectedFacts(activeMemoryScopeHints);
        var projectUnderstandingSelection = BuildProjectUnderstandingSelection(
            scopeAnalysis.Files.Count == 0
                ? task.Scope
                : scopeAnalysis.Files);
        var boundedRead = BoundedReadProjectionBuilder.Build(
            paths.RepoRoot,
            projectUnderstandingSelection.CandidateFiles);
        var localProjection = BuildLocalTaskProjection(graph, task);
        var relevantModules = BuildModuleProjection(scopeAnalysis, moduleSummaries, moduleMemory, top: 5);
        var facetNarrowing = BuildTaskFacetNarrowing(task, scopeAnalysis, projectUnderstandingSelection.ScopeFiles, projectUnderstandingSelection.HasRuntimePackInfluence);
        var recall = BuildTaskRecall(task, scopeAnalysis, moduleMemory, projectMemory);
        var codeHints = scopeAnalysis.SummaryLines
            .Take(3)
            .Concat(projectUnderstandingSelection.CodeHints)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var lastRunSummary = BuildLastRunSummary(task.TaskId);
        var lastFailureSummary = failureSummaryProjectionService.BuildForTask(task.TaskId);
        var expandableReferences = BuildTaskReferences(task, moduleMemory, lastFailureSummary, lastRunSummary)
            .Concat(projectUnderstandingSelection.ExpandableReferences)
            .DistinctBy(reference => reference.Path, StringComparer.Ordinal)
            .ToArray();
        var constraints = task.Constraints
            .Concat(task.Scope.Select(scope => $"scope:{scope}"))
            .Concat(projectUnderstandingSelection.Constraints)
            .Distinct(StringComparer.Ordinal)
            .Take(10)
            .ToArray();

        return new ContextPackDraft(
            PackId: $"task-{task.TaskId}",
            Audience: ContextPackAudience.Worker,
            TaskId: task.TaskId,
            ArtifactPath: ToRuntimeRelativePath(GetTaskPackPath(task.TaskId)),
            Goal: BuildGoal(task),
            Task: BuildTaskSummary(task),
            Constraints: constraints,
            AcceptanceContract: task.AcceptanceContract,
            LocalTaskGraph: localProjection,
            RelevantModules: relevantModules,
            FacetNarrowing: facetNarrowing,
            Recall: recall,
            CodeHints: codeHints,
            WindowedReads: ToWindowedReads(boundedRead),
            Compaction: NewCompaction(boundedRead),
            LastFailureSummary: lastFailureSummary,
            LastRunSummary: lastRunSummary,
            ExpandableReferences: expandableReferences);
    }

    private ContextPackDraft BuildPlannerDraft(
        RuntimeSessionState session,
        PlannerWakeReason wakeReason,
        string wakeDetail,
        OpportunitySnapshot snapshot,
        IReadOnlyList<Opportunity> selectedOpportunities,
        OpportunityTaskPreviewResult? preview)
    {
        var activeMemoryReadService = new ActiveMemoryReadService(paths);
        var previewTasks = preview?.ProposedTasks ?? Array.Empty<TaskNode>();
        var combinedScope = previewTasks.SelectMany(task => task.Scope).Distinct(StringComparer.Ordinal).ToArray();
        var scopeAnalysis = codeGraphQueryService.AnalyzeScope(combinedScope);
        var moduleSummaries = codeGraphQueryService.LoadModuleSummaries();
        var activeMemoryScopeHints = BuildPlannerActiveMemoryScopeHints(previewTasks, scopeAnalysis);
        var projectMemory = activeMemoryReadService.LoadProjectDocumentsWithProjectedFacts(activeMemoryScopeHints);
        var projectUnderstandingSelection = BuildProjectUnderstandingSelection(
            scopeAnalysis.Files.Count == 0
                ? combinedScope
                : scopeAnalysis.Files);
        var boundedRead = BoundedReadProjectionBuilder.Build(
            paths.RepoRoot,
            projectUnderstandingSelection.CandidateFiles);
        var relevantModules = BuildModuleProjection(
            scopeAnalysis,
            moduleSummaries,
            Array.Empty<Carves.Runtime.Domain.Memory.MemoryDocument>(),
            top: 5);
        var facetNarrowing = BuildPlannerFacetNarrowing(session, previewTasks, scopeAnalysis, projectUnderstandingSelection.ScopeFiles, projectUnderstandingSelection.HasRuntimePackInfluence);
        var recall = BuildPlannerRecall(session, wakeDetail, selectedOpportunities, previewTasks, scopeAnalysis, projectMemory);
        var lastFailure = failureSummaryProjectionService.BuildLatest();
        var lastRunSummary = previewTasks
            .Select(task => BuildLastRunSummary(task.TaskId))
            .FirstOrDefault(summary => summary is not null);
        var packId = $"planner-{session.SessionId}-{session.PlannerRound + 1:000}";

        return new ContextPackDraft(
            PackId: packId,
            Audience: ContextPackAudience.Planner,
            TaskId: null,
            ArtifactPath: ToRuntimeRelativePath(GetPlannerPackPath(packId)),
            Goal: $"Runtime stage {RuntimeStageInfo.CurrentStage}; wake reason {PlannerLifecycleSemantics.DescribeWakeReason(wakeReason)}",
            Task: BuildPlannerTaskSummary(wakeDetail, snapshot, selectedOpportunities, previewTasks),
            Constraints:
            [
                $"planner_round={session.PlannerRound + 1}",
                $"selected_opportunities={selectedOpportunities.Count}",
                $"preview_tasks={previewTasks.Count}",
                $"review_pending={session.ReviewPendingTaskIds.Count}",
                ..projectUnderstandingSelection.Constraints,
            ],
            AcceptanceContract: null,
            LocalTaskGraph: BuildPlannerLocalProjection(session, previewTasks),
            RelevantModules: relevantModules,
            FacetNarrowing: facetNarrowing,
            Recall: recall,
            CodeHints: scopeAnalysis.SummaryLines
                .Take(3)
                .Concat(projectUnderstandingSelection.CodeHints)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            WindowedReads: ToWindowedReads(boundedRead),
            Compaction: NewCompaction(boundedRead),
            LastFailureSummary: lastFailure,
            LastRunSummary: lastRunSummary,
            ExpandableReferences: projectUnderstandingSelection.ExpandableReferences);
    }

    private IReadOnlyList<string> BuildTaskActiveMemoryScopeHints(TaskNode task, CodeGraphScopeAnalysis scopeAnalysis)
    {
        return
        [
            $"repo:{GetRepoLabel()}",
            task.TaskId,
            ..scopeAnalysis.Modules.Select(module => $"module:{module}"),
            ..task.Scope,
        ];
    }

    private IReadOnlyList<string> BuildPlannerActiveMemoryScopeHints(
        IReadOnlyList<TaskNode> previewTasks,
        CodeGraphScopeAnalysis scopeAnalysis)
    {
        return
        [
            $"repo:{GetRepoLabel()}",
            ..previewTasks.Select(task => task.TaskId),
            ..scopeAnalysis.Modules.Select(module => $"module:{module}"),
            ..previewTasks.SelectMany(task => task.Scope),
        ];
    }

    private TaskGraphLocalProjection BuildPlannerLocalProjection(RuntimeSessionState session, IReadOnlyList<TaskNode> previewTasks)
    {
        return new TaskGraphLocalProjection
        {
            CurrentTaskId = session.SessionId,
            CurrentTaskTitle = "planner_context",
            Dependencies = previewTasks.Take(3).Select(ToScopeItem).ToArray(),
            Blockers = taskGraphService.Load()
                .ListTasks()
                .Where(task => task.Status == Carves.Runtime.Domain.Tasks.TaskStatus.Blocked)
                .Take(3)
                .Select(ToScopeItem)
                .ToArray(),
        };
    }
}
