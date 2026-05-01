using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;
using TaskGraphModel = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Platform;

public sealed partial class WorkbenchSurfaceService
{
    public CardWorkbenchReadModel BuildCard(string cardId)
    {
        var graph = taskGraphService.Load();
        var completed = graph.CompletedTaskIds();
        var tasks = graph.ListTasks()
            .Where(task => string.Equals(task.CardId, cardId, StringComparison.Ordinal))
            .OrderBy(task => task.TaskId, StringComparer.Ordinal)
            .ToArray();
        var cardPath = Path.Combine(paths.CardsRoot, $"{cardId}.md");
        var draft = planningDraftService.TryGetCardDraft(cardId);
        var parsedCard = File.Exists(cardPath) ? plannerService.ParseCard(cardPath) : null;
        var status = ResolveCardStatus(tasks, draft, completed, formalPlanningExecutionGateService);
        var lifecycleState = (draft?.Status ?? Carves.Runtime.Domain.Planning.CardLifecycleState.Approved).ToString().ToLowerInvariant();
        var reality = ResolveCardReality(cardId, tasks, draft);
        var summary = tasks.Select(task => task.LastWorkerSummary).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? tasks.Select(task => task.PlannerReview.Reason).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? parsedCard?.Goal
            ?? draft?.Goal
            ?? "(none)";

        return new CardWorkbenchReadModel
        {
            CardId = cardId,
            Title = parsedCard?.Title ?? draft?.Title ?? tasks.FirstOrDefault()?.Title ?? "(unknown)",
            Goal = parsedCard?.Goal ?? draft?.Goal ?? "(none)",
            Status = status,
            LifecycleState = lifecycleState,
            Summary = summary,
            Reality = reality,
            BlockedReason = ResolveCardBlockedReason(tasks, completed, formalPlanningExecutionGateService),
            NextAction = ResolveCardNextAction(tasks, completed, formalPlanningExecutionGateService),
            Tasks = tasks.Select(task => ToTaskListItem(task, completed, formalPlanningExecutionGateService)).ToArray(),
            AvailableActions = tasks.Any(task => task.Status == DomainTaskStatus.Review)
                ? [BuildSyncAction()]
                : Array.Empty<WorkbenchActionDescriptor>(),
        };
    }

    public TaskWorkbenchReadModel BuildTask(string taskId)
    {
        var graph = taskGraphService.Load();
        var completed = graph.CompletedTaskIds();
        var task = taskGraphService.GetTask(taskId);
        var draft = string.IsNullOrWhiteSpace(task.CardId) ? null : planningDraftService.TryGetCardDraft(task.CardId!);
        var unresolvedDependencies = task.Dependencies.Where(dependency => !completed.Contains(dependency)).ToArray();
        var relatedTasks = graph.ListTasks()
            .Where(item =>
                !string.Equals(item.TaskId, task.TaskId, StringComparison.Ordinal)
                && (string.Equals(item.CardId, task.CardId, StringComparison.Ordinal)
                    || item.Dependencies.Contains(task.TaskId, StringComparer.Ordinal)
                    || task.Dependencies.Contains(item.TaskId, StringComparer.Ordinal)))
            .OrderBy(item => item.TaskId, StringComparer.Ordinal)
            .Select(item => ToTaskListItem(item, completed, formalPlanningExecutionGateService))
            .ToArray();
        var runs = executionRunService.ListRuns(taskId);
        var latestRun = runs.LastOrDefault();
        var artifacts = BuildArtifacts(task, latestRun);
        var reality = ResolveTaskReality(task, draft);
        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId);
        var workerArtifact = artifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId);

        return new TaskWorkbenchReadModel
        {
            TaskId = task.TaskId,
            CardId = task.CardId ?? string.Empty,
            Title = task.Title,
            Status = task.Status.ToString(),
            Summary = task.LastWorkerSummary ?? task.PlannerReview.Reason ?? "(none)",
            Reality = reality,
            BlockedReason = ResolveTaskBlockedReason(task, unresolvedDependencies, formalPlanningExecutionGateService),
            NextAction = ResolveTaskNextAction(task, unresolvedDependencies, formalPlanningExecutionGateService),
            ReviewEvidence = BuildReviewEvidence(task, reviewArtifact, workerArtifact),
            Dependencies = task.Dependencies.ToArray(),
            UnresolvedDependencies = unresolvedDependencies,
            ExecutionRun = latestRun is null
                ? null
                : new WorkbenchRunSummary
                {
                    RunId = latestRun.RunId,
                    Status = latestRun.Status.ToString(),
                    RunCount = runs.Count,
                    CurrentStepIndex = latestRun.CurrentStepIndex,
                    CurrentStepTitle = latestRun.Steps.Count == 0
                        ? "(none)"
                        : latestRun.Steps[Math.Clamp(latestRun.CurrentStepIndex, 0, latestRun.Steps.Count - 1)].Title,
                },
            Artifacts = artifacts,
            RelatedTasks = relatedTasks,
            AvailableActions = BuildTaskActions(task),
        };
    }

    public ReviewWorkbenchReadModel BuildReview()
    {
        var graph = taskGraphService.Load();
        var completed = graph.CompletedTaskIds();
        var session = devLoopService.GetSession();
        var liveSessionReviewPendingCount = 0;
        var staleSessionReviewPendingCount = 0;
        var reviewQueue = graph.ListTasks()
            .Where(task => task.Status == DomainTaskStatus.Review)
            .OrderBy(task => task.TaskId, StringComparer.Ordinal)
            .Select(task => ToReviewQueueItem(task, [BuildApproveAction(task.TaskId), BuildRejectAction(task.TaskId)]))
            .ToList();
        if (session is not null)
        {
            foreach (var reviewPendingTaskId in session.ReviewPendingTaskIds)
            {
                if (!graph.Tasks.TryGetValue(reviewPendingTaskId, out var task))
                {
                    staleSessionReviewPendingCount++;
                    continue;
                }

                liveSessionReviewPendingCount++;
                if (reviewQueue.Any(item => string.Equals(item.TaskId, reviewPendingTaskId, StringComparison.Ordinal)))
                {
                    continue;
                }

                reviewQueue.Add(ToReviewQueueItem(task, [BuildApproveAction(task.TaskId), BuildRejectAction(task.TaskId)]));
            }
        }

        var taskActionQueue = graph.ListTasks()
            .Where(task => IsWorkbenchActionable(task.Status))
            .OrderBy(task => RankTask(task, completed, formalPlanningExecutionGateService))
            .ThenBy(task => task.TaskId, StringComparer.Ordinal)
            .Take(12)
            .Select(task => ToReviewQueueItem(task, BuildTaskActions(task)))
            .ToArray();

        return new ReviewWorkbenchReadModel
        {
            Summary = BuildReviewSummary(
                reviewQueue,
                taskActionQueue.Length,
                liveSessionReviewPendingCount,
                staleSessionReviewPendingCount),
            ReviewQueue = reviewQueue.OrderBy(item => item.TaskId, StringComparer.Ordinal).ToArray(),
            TaskActionQueue = taskActionQueue,
            GlobalActions = [BuildSyncAction()],
        };
    }

    private static string BuildReviewSummary(
        IReadOnlyList<WorkbenchReviewQueueItem> reviewQueue,
        int taskActionCount,
        int liveSessionReviewPendingCount,
        int staleSessionReviewPendingCount)
    {
        var finalReadyCount = reviewQueue.Count(item => item.ReviewEvidence.CanFinalApprove);
        var finalBlockedCount = reviewQueue.Count(item => !item.ReviewEvidence.CanFinalApprove && item.ReviewEvidence.Status != "unavailable");
        var summary = $"pending reviews={reviewQueue.Count}; final ready={finalReadyCount}; final blocked={finalBlockedCount}; task actions={taskActionCount}; session review pending={liveSessionReviewPendingCount}";
        if (staleSessionReviewPendingCount > 0)
        {
            summary += $"; stale session refs={staleSessionReviewPendingCount}";
        }

        return summary + ".";
    }
}
