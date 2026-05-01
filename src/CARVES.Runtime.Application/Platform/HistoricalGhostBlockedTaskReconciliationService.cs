using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class HistoricalGhostBlockedTaskReconciliationService
{
    private const string ShapePrefix = "Shape interfaces for ";

    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public HistoricalGhostBlockedTaskReconciliationService(
        TaskGraphService taskGraphService,
        IRuntimeArtifactRepository artifactRepository)
    {
        this.taskGraphService = taskGraphService;
        this.artifactRepository = artifactRepository;
    }

    public IReadOnlyList<string> Reconcile()
    {
        var graph = taskGraphService.Load();
        var tasks = graph.ListTasks();
        var reconciled = new List<string>();

        foreach (var task in tasks)
        {
            if (!IsGhostBlockedShapeTask(task, tasks))
            {
                continue;
            }

            if (artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId) is not null)
            {
                continue;
            }

            task.ClearRetryBackoff();
            task.SetPlannerReview(new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = $"Historical shape step was superseded after downstream implementation and validation for {task.CardId} completed under Stage-7C manual operator fallback.",
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
                FollowUpSuggestions =
                [
                    "Preserve the quarantined delegated lifecycle record as historical runtime evidence; do not reopen the original worktree.",
                ],
            });
            task.SetStatus(DomainTaskStatus.Superseded);
            taskGraphService.ReplaceTask(task);
            reconciled.Add(task.TaskId);
        }

        return reconciled;
    }

    private static bool IsGhostBlockedShapeTask(TaskNode task, IReadOnlyList<TaskNode> allTasks)
    {
        if (task.Status != DomainTaskStatus.Blocked
            || !task.Title.StartsWith(ShapePrefix, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(task.CardId)
            || task.LastRecoveryAction != WorkerRecoveryAction.RebuildWorktree
            || task.PlannerReview.Verdict != PlannerVerdict.HumanDecisionRequired)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(task.LastWorkerRunId)
            || !string.IsNullOrWhiteSpace(task.LastWorkerBackend)
            || task.LastWorkerFailureKind != WorkerFailureKind.None
            || !string.IsNullOrWhiteSpace(task.LastWorkerDetailRef)
            || !string.IsNullOrWhiteSpace(task.LastProviderDetailRef))
        {
            return false;
        }

        var siblingTasks = allTasks
            .Where(candidate => string.Equals(candidate.CardId, task.CardId, StringComparison.Ordinal))
            .ToArray();
        if (siblingTasks.Length < 2)
        {
            return false;
        }

        var downstreamTasks = siblingTasks
            .Where(candidate => !string.Equals(candidate.TaskId, task.TaskId, StringComparison.Ordinal))
            .ToArray();
        if (downstreamTasks.Length == 0
            || !downstreamTasks.All(IsFinalized)
            || !downstreamTasks.Any(candidate => candidate.Dependencies.Contains(task.TaskId, StringComparer.Ordinal))
            || !downstreamTasks.Any(candidate => candidate.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged))
        {
            return false;
        }

        return true;
    }

    private static bool IsFinalized(TaskNode task)
    {
        return task.Status is DomainTaskStatus.Completed
            or DomainTaskStatus.Merged
            or DomainTaskStatus.Superseded
            or DomainTaskStatus.Discarded;
    }
}
