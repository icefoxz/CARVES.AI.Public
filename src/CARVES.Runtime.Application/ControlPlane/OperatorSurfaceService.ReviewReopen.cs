using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult ReopenReview(string taskId, string reason)
    {
        var task = taskGraphService.GetTask(taskId);
        if (task.Status is not (DomainTaskStatus.Review or DomainTaskStatus.Completed or DomainTaskStatus.Merged))
        {
            return OperatorCommandResult.Failure(
                $"Task {taskId} is not in a reopenable review state. Current status: {task.Status}.");
        }

        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(taskId);
        if (reviewArtifact is null)
        {
            return OperatorCommandResult.Failure($"No review artifact exists for {taskId}.");
        }

        if (!AllowsReviewReopen(task, reviewArtifact))
        {
            return OperatorCommandResult.Failure(
                $"Task {taskId} does not have an accepted review decision that can be reopened.");
        }

        var review = new PlannerReview
        {
            Verdict = PlannerVerdict.PauseForReview,
            Reason = reason,
            DecisionStatus = ReviewDecisionStatus.Reopened,
            AcceptanceMet = false,
            BoundaryPreserved = true,
            ScopeDriftDetected = false,
            FollowUpSuggestions =
            [
                "Resolve the reopened review boundary before treating the task as accepted truth again.",
            ],
        };

        task = ApplyReviewDecision(task, DomainTaskStatus.Review, review, AcceptanceContractHumanDecision.Reopen);
        task = new PlannerEmergenceService(paths, taskGraphService, executionRunService).CaptureReviewOutcome(
            task,
            PlannerVerdict.PauseForReview,
            reason);
        taskGraphService.ReplaceTask(task);

        var reopenedReview = reviewArtifactFactory.RecordDecision(
            reviewArtifact,
            task,
            review,
            DomainTaskStatus.Review,
            reason,
            task.ResultCommit,
            reviewArtifact.Writeback);
        artifactRepository.SavePlannerReviewArtifact(reopenedReview);
        artifactRepository.DeleteMergeCandidateArtifact(taskId);
        new RuntimeEvidenceStoreService(paths).RecordReview(
            task,
            reopenedReview,
            sourceEvidenceIds: ResolveReviewSourceEvidenceIds(taskId));

        var session = devLoopService.MarkReviewPending(taskId, $"Review reopened for {taskId}: {reason}");
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
        return OperatorCommandResult.Success(
            $"Reopened review for {taskId}; task returned to REVIEW and the acceptance contract was reopened.");
    }
}
