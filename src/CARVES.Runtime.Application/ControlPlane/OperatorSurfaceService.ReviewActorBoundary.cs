using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private static readonly ActorReviewWritebackPolicyService ReviewWritebackActorPolicy = new();

    public OperatorCommandResult ReviewTaskAsActor(
        string taskId,
        string verdictText,
        string reason,
        ActorSessionKind actorKind,
        string actorIdentity)
    {
        var operation = string.Equals(verdictText, "complete", StringComparison.OrdinalIgnoreCase)
            ? "review_task_complete"
            : "review_task";
        return ResolveReviewWritebackActorAction(
            taskId,
            operation,
            actorKind,
            actorIdentity,
            () => ReviewTask(taskId, verdictText, reason));
    }

    public OperatorCommandResult ApproveReviewAsActor(
        string taskId,
        string reason,
        ActorSessionKind actorKind,
        string actorIdentity,
        bool autoContinueAfterApprove = true,
        bool provisional = false)
    {
        return ResolveReviewWritebackActorAction(
            taskId,
            provisional ? "approve_review_provisional" : "approve_review",
            actorKind,
            actorIdentity,
            () => ApproveReview(taskId, reason, autoContinueAfterApprove, provisional));
    }

    public OperatorCommandResult RejectReviewAsActor(
        string taskId,
        string reason,
        ActorSessionKind actorKind,
        string actorIdentity)
    {
        return ResolveReviewWritebackActorAction(
            taskId,
            "reject_review",
            actorKind,
            actorIdentity,
            () => RejectReview(taskId, reason));
    }

    public OperatorCommandResult ReopenReviewAsActor(
        string taskId,
        string reason,
        ActorSessionKind actorKind,
        string actorIdentity)
    {
        return ResolveReviewWritebackActorAction(
            taskId,
            "reopen_review",
            actorKind,
            actorIdentity,
            () => ReopenReview(taskId, reason));
    }

    private OperatorCommandResult ResolveReviewWritebackActorAction(
        string taskId,
        string operation,
        ActorSessionKind actorKind,
        string actorIdentity,
        Func<OperatorCommandResult> action)
    {
        var decision = ReviewWritebackActorPolicy.Evaluate(actorKind, actorIdentity, operation, taskId);
        if (!decision.Allowed)
        {
            return new OperatorCommandResult(
                1,
                [
                    $"Actor review/writeback denied: {decision.ReasonCode}",
                    $"Actor: {actorKind}:{actorIdentity}",
                    $"Task: {taskId}",
                    $"Operation: {operation}",
                    $"Summary: {decision.Summary}",
                    $"Allowed worker operations: {string.Join(", ", decision.AllowedWorkerOperations)}",
                    $"Next action: {decision.NextAction}",
                ]);
        }

        var reason = $"{operation} requested for {taskId} by {actorKind} session '{actorIdentity}'.";
        var actorSession = EnsureControlActorSession(
            actorKind,
            actorIdentity,
            reason,
            OwnershipScope.ApprovalDecision,
            taskId,
            operationClass: "review_writeback",
            operation: operation);
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.ApprovalDecision,
            taskId,
            reason,
            action);
    }
}
