using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ActorReviewWritebackPolicyService
{
    public ActorReviewWritebackDecision Evaluate(
        ActorSessionKind actorKind,
        string actorIdentity,
        string operation,
        string taskId)
    {
        if (actorKind == ActorSessionKind.Worker)
        {
            return new ActorReviewWritebackDecision
            {
                Allowed = false,
                ReasonCode = "worker_review_writeback_denied",
                Summary = $"WorkerSession '{actorIdentity}' is execution-only and cannot perform {operation} for {taskId}.",
                NextAction = "Route review/writeback through an OperatorSession or governed approval surface.",
                AllowedWorkerOperations =
                [
                    "submit_execution_result",
                    "submit_completion_claim",
                    "heartbeat",
                    "release_lease",
                ],
            };
        }

        return new ActorReviewWritebackDecision
        {
            Allowed = true,
            ReasonCode = "review_writeback_actor_allowed",
            Summary = $"{actorKind} session '{actorIdentity}' may request {operation} for {taskId} through approval arbitration.",
            NextAction = "Continue through approval arbitration.",
            AllowedWorkerOperations = [],
        };
    }
}

public sealed class ActorReviewWritebackDecision
{
    public bool Allowed { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string NextAction { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedWorkerOperations { get; init; } = [];
}
