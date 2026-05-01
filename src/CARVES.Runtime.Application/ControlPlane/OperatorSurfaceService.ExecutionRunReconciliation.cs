using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private IReadOnlyList<string> ReconcileExecutionRunTruth(DomainTaskGraph graph)
    {
        var reconciled = new List<string>();
        foreach (var task in graph.ListTasks())
        {
            var reconciledTask = executionRunService.TryReconcileInactiveTaskRun(task, "Reconciled stale active execution run during sync-state.");
            if (reconciledTask is not null)
            {
                taskGraphService.ReplaceTask(reconciledTask);
                reconciled.Add(task.TaskId);
                continue;
            }

            var latestRun = executionRunService.ListRuns(task.TaskId).LastOrDefault();
            if (latestRun is null)
            {
                continue;
            }

            if (latestRun.Status != ExecutionRunStatus.Failed)
            {
                continue;
            }

            if (!ShouldReconcileRunToCompleted(task))
            {
                continue;
            }

            var completedRun = executionRunService.CompleteRun(latestRun, latestRun.ResultEnvelopePath);
            var updatedTask = executionRunService.ApplyTaskMetadata(task, completedRun, activeRunId: null);
            taskGraphService.ReplaceTask(updatedTask);
            reconciled.Add(task.TaskId);
        }

        return reconciled;
    }

    private bool ShouldReconcileRunToCompleted(TaskNode task)
    {
        if (task.Status is not (DomainTaskStatus.Review or DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded))
        {
            return false;
        }

        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId);
        if (reviewArtifact is null)
        {
            return false;
        }

        if (!reviewArtifact.ValidationPassed || reviewArtifact.SafetyOutcome != SafetyOutcome.Allow)
        {
            return false;
        }

        return task.Status switch
        {
            DomainTaskStatus.Review => reviewArtifact.DecisionStatus == ReviewDecisionStatus.PendingReview
                                       && reviewArtifact.ResultingStatus == DomainTaskStatus.Review,
            DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded
                => reviewArtifact.DecisionStatus == ReviewDecisionStatus.Approved,
            _ => false,
        };
    }
}
