using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RejectReview(string taskId, string reason)
    {
        var task = taskGraphService.GetTask(taskId);
        if (task.Status != DomainTaskStatus.Review)
        {
            return OperatorCommandResult.Failure($"Task {taskId} is not in REVIEW state.");
        }

        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(taskId);
        if (reviewArtifact is null)
        {
            return OperatorCommandResult.Failure($"No review artifact exists for {taskId}.");
        }

        var review = new PlannerReview
        {
            Verdict = PlannerVerdict.Continue,
            Reason = reason,
            DecisionStatus = ReviewDecisionStatus.Rejected,
            AcceptanceMet = false,
            BoundaryPreserved = true,
            ScopeDriftDetected = false,
        };
        task = ApplyReviewDecision(task, DomainTaskStatus.Pending, review, AcceptanceContractHumanDecision.Reject);
        taskGraphService.ReplaceTask(task);

        var rejectedReview = reviewArtifactFactory.RecordDecision(
            reviewArtifact,
            task,
            review,
            DomainTaskStatus.Pending,
            reason);
        artifactRepository.SavePlannerReviewArtifact(rejectedReview);
        new RuntimeEvidenceStoreService(paths).RecordReview(
            task,
            rejectedReview,
            sourceEvidenceIds: ResolveReviewSourceEvidenceIds(taskId));
        var session = devLoopService.ResolveReviewDecision(taskId, $"Review rejected for {taskId}: {reason}");
        var failure = runtimeFailurePolicy.CreateReviewRejected(session, taskId, reason);
        artifactRepository.SaveRuntimeFailureArtifact(failure);
        _ = failureReportService.EmitRuntimeFailure(task, failure, session);
        task = taskGraphService.GetTask(taskId);
        task = new PlannerEmergenceService(paths, taskGraphService, executionRunService).CaptureReviewRejection(task, reason);
        taskGraphService.ReplaceTask(task);
        if (session is not null)
        {
            devLoopService.QueuePlannerWake(
                PlannerWakeReason.TaskFailed,
                PlannerWakeSourceKind.ReviewResolution,
                $"Review rejected for {taskId}: {reason}",
                $"{taskId} returned to pending after review rejection.",
                taskId);
        }

        markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
        return OperatorCommandResult.Success($"Rejected review for {taskId}; task returned to PENDING.");
    }
}
