using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult ApproveTask(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        if (task.Status != DomainTaskStatus.Suggested)
        {
            return OperatorCommandResult.Failure($"Task {taskId} is not in SUGGESTED state.");
        }

        task.SetStatus(DomainTaskStatus.Pending);
        task.SetPlannerReview(new PlannerReview
        {
            Verdict = PlannerVerdict.Continue,
            Reason = "Human approved promotion from suggested to pending through operator surface.",
            DecisionStatus = ReviewDecisionStatus.NeedsAttention,
            AcceptanceMet = false,
            BoundaryPreserved = true,
        });
        taskGraphService.ReplaceTask(task);
        markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
        return OperatorCommandResult.Success($"Approved {taskId} and promoted it to PENDING.");
    }

    public OperatorCommandResult ReviewTask(string taskId, string verdictText, string reason)
    {
        var task = taskGraphService.GetTask(taskId);
        var verdict = PlannerVerdictParser.Parse(verdictText);
        var nextStatus = verdict switch
        {
            PlannerVerdict.Complete => DomainTaskStatus.Review,
            PlannerVerdict.Blocked => DomainTaskStatus.Blocked,
            PlannerVerdict.Superseded => DomainTaskStatus.Superseded,
            PlannerVerdict.PauseForReview => DomainTaskStatus.Review,
            PlannerVerdict.HumanDecisionRequired => DomainTaskStatus.Review,
            _ => DomainTaskStatus.Pending,
        };
        var decisionStatus = nextStatus switch
        {
            DomainTaskStatus.Review => ReviewDecisionStatus.PendingReview,
            DomainTaskStatus.Blocked => ReviewDecisionStatus.Blocked,
            DomainTaskStatus.Superseded => ReviewDecisionStatus.Superseded,
            _ => ReviewDecisionStatus.NeedsAttention,
        };

        task.SetPlannerReview(new PlannerReview
        {
            Verdict = verdict,
            Reason = reason,
            DecisionStatus = decisionStatus,
            AcceptanceMet = verdict == PlannerVerdict.Complete,
            BoundaryPreserved = true,
            ScopeDriftDetected = false,
        });
        task.SetStatus(nextStatus);
        task = new PlannerEmergenceService(paths, taskGraphService, executionRunService).CaptureReviewOutcome(task, verdict, reason);
        task = ReconcileManualFallbackCompletion(task, verdict, reason);
        taskGraphService.ReplaceTask(task);
        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(taskId);
        if (reviewArtifact is not null)
        {
            var recordedReview = reviewArtifactFactory.RecordDecision(
                reviewArtifact,
                task,
                task.PlannerReview,
                nextStatus,
                reason,
                resultCommit: task.ResultCommit);
            artifactRepository.SavePlannerReviewArtifact(recordedReview);
            new RuntimeEvidenceStoreService(paths).RecordReview(
                task,
                recordedReview,
                sourceEvidenceIds: ResolveReviewSourceEvidenceIds(taskId));
        }
        if (nextStatus == DomainTaskStatus.Blocked)
        {
            failureReportService.EmitTaskBlocked(task, reason);
        }

        var session = nextStatus is DomainTaskStatus.Completed or DomainTaskStatus.Blocked or DomainTaskStatus.Pending or DomainTaskStatus.Superseded
            ? devLoopService.ResolveReviewDecision(taskId, $"Review recorded for {taskId}: {reason}")
            : devLoopService.GetSession();
        markdownSyncService.Sync(taskGraphService.Load(), session: session);
        var summary = $"Recorded review for {taskId}: {verdict} -> {nextStatus}.";
        if (verdict == PlannerVerdict.Complete)
        {
            summary += " Completion still requires approve-review so evidence, writeback, and final validation gates remain authoritative.";
        }

        return OperatorCommandResult.Success(summary);
    }

    public OperatorCommandResult RetryTask(string taskId, string reason)
    {
        var task = taskGraphService.GetTask(taskId);
        if (task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded or DomainTaskStatus.Discarded)
        {
            return OperatorCommandResult.Failure($"Task {taskId} is already finalized and cannot be retried.");
        }

        var maxRetries = configRepository.LoadSafetyRules().MaxRetryCount;
        if (task.RetryCount >= maxRetries)
        {
            return OperatorCommandResult.Failure($"Task {taskId} has exhausted its retry budget ({task.RetryCount}/{maxRetries}).");
        }

        task.IncrementRetryCount();
        task.ClearRetryBackoff();
        task.SetPlannerReview(new PlannerReview
        {
            Verdict = PlannerVerdict.Continue,
            Reason = reason,
            DecisionStatus = ReviewDecisionStatus.NeedsAttention,
            AcceptanceMet = false,
            BoundaryPreserved = true,
            ScopeDriftDetected = false,
            FollowUpSuggestions = ["Inspect the execution trace before dispatching the retry through the host."],
        });
        task.SetStatus(DomainTaskStatus.Pending);
        taskGraphService.ReplaceTask(task);
        markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
        return OperatorCommandResult.Success($"Retry scheduled for {taskId} ({task.RetryCount}/{maxRetries}): {reason}");
    }
}
