using System.Collections.Concurrent;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeSessionGatewayService
{
    private const string AcceptedStage = "accepted";
    private const string CompletedStage = "completed";
    private const string FailedStage = "failed";
    private const string OperationAcceptedReasonPrefix = "session_gateway_operation_accepted:";
    private const string OperationProgressedReasonPrefix = "session_gateway_operation_progressed:";
    private const string ReviewResolvedReasonPrefix = "session_gateway_review_resolved:";
    private const string ReplanRequestedReasonPrefix = "session_gateway_replan_requested:";
    private const string ReplanProjectedReasonPrefix = "session_gateway_replan_projected:";
    private const string OperationCompletedReasonPrefix = "session_gateway_operation_completed:";
    private const string OperationFailedReasonPrefix = "session_gateway_operation_failed:";
    private const string OperatorActionRequiredReasonPrefix = "session_gateway_operator_action_required:";
    private const string OperatorProjectRequiredReasonPrefix = "session_gateway_operator_project_required:";
    private const string OperatorEvidenceRequiredReasonPrefix = "session_gateway_operator_evidence_required:";
    private const string RealWorldProofMissingReasonPrefix = "session_gateway_real_world_proof_missing:";
    private readonly ConcurrentDictionary<string, SessionGatewayOperationBindingSurface> operationBindings = new(StringComparer.Ordinal);

    public string? BindGovernedOperation(string sessionId, string turnId, string messageId, SessionGatewayMessageRequest request, string classifiedIntent)
    {
        var targetTaskId = NormalizeOptional(request.TargetTaskId);
        if (!string.Equals(classifiedIntent, "governed_run", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(targetTaskId))
        {
            return null;
        }

        var binding = new SessionGatewayOperationBindingSurface
        {
            OperationId = $"sgwop-{Guid.NewGuid():N}",
            SessionId = sessionId,
            TurnId = turnId,
            MessageId = messageId,
            TaskId = targetTaskId,
            ClassifiedIntent = classifiedIntent,
            AcceptedAt = DateTimeOffset.UtcNow,
        };
        operationBindings[binding.OperationId] = binding;

        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayOperationAccepted,
            RepoId = repoId,
            ActorSessionId = sessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = targetTaskId,
            RunId = messageId,
            ReferenceId = binding.OperationId,
            ReasonCode = $"{OperationAcceptedReasonPrefix}{turnId}",
            Summary = $"Accepted governed Session Gateway operation {binding.OperationId} for task {targetTaskId}.",
        });
        RecordInitialOperatorProofContract(binding);

        return binding.OperationId;
    }

    public SessionGatewayOperationBindingSurface? TryGetOperationBinding(string operationId)
    {
        if (operationBindings.TryGetValue(operationId, out var binding))
        {
            return binding;
        }

        var restored = RestoreOperationBinding(operationId);
        if (restored is not null)
        {
            operationBindings[operationId] = restored;
        }

        return restored;
    }

    public SessionGatewayOperationBindingSurface? SetRequestedAction(string operationId, string requestedAction)
    {
        var binding = TryGetOperationBinding(operationId);
        if (binding is null)
        {
            return null;
        }

        var updated = binding with { RequestedAction = requestedAction };
        operationBindings[operationId] = updated;
        return updated;
    }

    public void RecordOperationProgress(string operationId, string progressMarker, string? requestedAction, string summary)
    {
        var binding = RequireOperationBinding(operationId, requestedAction);
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayOperationProgressed,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = operationId,
            ReasonCode = $"{OperationProgressedReasonPrefix}{progressMarker}:{requestedAction ?? "none"}",
            Summary = summary,
        });
    }

    public void RecordReviewResolved(string operationId, string requestedAction, string reason)
    {
        var binding = RequireOperationBinding(operationId, requestedAction);
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayReviewResolved,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = operationId,
            ReasonCode = $"{ReviewResolvedReasonPrefix}{requestedAction}",
            Summary = $"Resolved Session Gateway review forwarding for task {binding.TaskId} via {requestedAction}: {reason}",
        });
    }

    public void RecordReplanRequested(string operationId, string reason)
    {
        var binding = RequireOperationBinding(operationId, "replan");
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayReplanRequested,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = operationId,
            ReasonCode = ReplanRequestedReasonPrefix,
            Summary = $"Requested Runtime-owned replan for task {binding.TaskId}: {reason}",
        });
    }

    public void RecordReplanProjected(string operationId, string reason)
    {
        var binding = RequireOperationBinding(operationId, "replan");
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayReplanProjected,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = operationId,
            ReasonCode = ReplanProjectedReasonPrefix,
            Summary = $"Projected Runtime-owned replan for task {binding.TaskId}: {reason}",
        });
    }

    public void RecordOperationCompleted(string operationId, string? requestedAction, OperatorCommandResult result)
    {
        var binding = RequireOperationBinding(operationId, requestedAction);
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayOperationCompleted,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = operationId,
            ReasonCode = $"{OperationCompletedReasonPrefix}{requestedAction ?? "none"}",
            Summary = result.Lines.Count == 0
                ? $"Completed Session Gateway governed mutation {requestedAction ?? "operation"} for task {binding.TaskId}."
                : result.Lines[0],
        });
    }

    public void RecordOperationFailed(string operationId, string? requestedAction, string failureMessage)
    {
        var binding = RequireOperationBinding(operationId, requestedAction);
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayOperationFailed,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = operationId,
            ReasonCode = $"{OperationFailedReasonPrefix}{requestedAction ?? "none"}",
            Summary = failureMessage,
        });
    }

    private static bool IsSessionGatewayEvent(OperatorOsEventKind eventKind)
    {
        return eventKind is OperatorOsEventKind.SessionGatewaySessionCreated
            or OperatorOsEventKind.SessionGatewayTurnAccepted
            or OperatorOsEventKind.SessionGatewayTurnClassified
            or OperatorOsEventKind.SessionGatewayOperationAccepted
            or OperatorOsEventKind.SessionGatewayOperationProgressed
            or OperatorOsEventKind.SessionGatewayReviewResolved
            or OperatorOsEventKind.SessionGatewayReplanRequested
            or OperatorOsEventKind.SessionGatewayReplanProjected
            or OperatorOsEventKind.SessionGatewayOperationCompleted
            or OperatorOsEventKind.SessionGatewayOperationFailed
            or OperatorOsEventKind.SessionGatewayOperatorActionRequired
            or OperatorOsEventKind.SessionGatewayOperatorProjectRequired
            or OperatorOsEventKind.SessionGatewayOperatorEvidenceRequired
            or OperatorOsEventKind.SessionGatewayRealWorldProofMissing;
    }

    private SessionGatewayOperationBindingSurface RequireOperationBinding(string operationId, string? requestedAction)
    {
        var binding = TryGetOperationBinding(operationId)
            ?? throw new InvalidOperationException($"Session Gateway operation '{operationId}' was not found.");
        if (!string.IsNullOrWhiteSpace(requestedAction)
            && !string.Equals(binding.RequestedAction, requestedAction, StringComparison.Ordinal))
        {
            binding = binding with { RequestedAction = requestedAction };
            operationBindings[operationId] = binding;
        }

        return binding;
    }

    private SessionGatewayOperationBindingSurface? RestoreOperationBinding(string operationId)
    {
        var acceptedRecord = operatorOsEventStreamService.Load()
            .FirstOrDefault(record => record.EventKind == OperatorOsEventKind.SessionGatewayOperationAccepted
                && string.Equals(record.ReferenceId, operationId, StringComparison.Ordinal));
        if (acceptedRecord is null || string.IsNullOrWhiteSpace(acceptedRecord.ActorSessionId))
        {
            return null;
        }

        var requestedAction = operatorOsEventStreamService.Load(actorSessionId: acceptedRecord.ActorSessionId)
            .Where(record => string.Equals(record.ReferenceId, operationId, StringComparison.Ordinal))
            .Select(record => TryReadRequestedAction(record.ReasonCode))
            .FirstOrDefault(action => !string.IsNullOrWhiteSpace(action));

        return new SessionGatewayOperationBindingSurface
        {
            OperationId = operationId,
            SessionId = acceptedRecord.ActorSessionId!,
            TurnId = TryReadTurnId(acceptedRecord.ReasonCode),
            MessageId = acceptedRecord.RunId,
            TaskId = acceptedRecord.TaskId,
            ClassifiedIntent = "governed_run",
            RequestedAction = requestedAction,
            AcceptedAt = acceptedRecord.OccurredAt,
        };
    }

    private static string? ResolveOperationId(OperatorOsEventRecord record)
    {
        return record.EventKind is OperatorOsEventKind.SessionGatewayOperationAccepted
            or OperatorOsEventKind.SessionGatewayOperationProgressed
            or OperatorOsEventKind.SessionGatewayReviewResolved
            or OperatorOsEventKind.SessionGatewayReplanRequested
            or OperatorOsEventKind.SessionGatewayReplanProjected
            or OperatorOsEventKind.SessionGatewayOperationCompleted
            or OperatorOsEventKind.SessionGatewayOperationFailed
            or OperatorOsEventKind.SessionGatewayOperatorActionRequired
            or OperatorOsEventKind.SessionGatewayOperatorProjectRequired
            or OperatorOsEventKind.SessionGatewayOperatorEvidenceRequired
            or OperatorOsEventKind.SessionGatewayRealWorldProofMissing
            ? record.ReferenceId
            : null;
    }

    private static string? TryReadTurnId(string reasonCode)
    {
        return reasonCode.StartsWith(OperationAcceptedReasonPrefix, StringComparison.Ordinal)
            ? reasonCode[OperationAcceptedReasonPrefix.Length..]
            : null;
    }

    private static string? TryReadProgressMarker(string reasonCode)
    {
        if (!reasonCode.StartsWith(OperationProgressedReasonPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var payload = reasonCode[OperationProgressedReasonPrefix.Length..];
        var separatorIndex = payload.IndexOf(':');
        return separatorIndex >= 0 ? payload[..separatorIndex] : payload;
    }

    private static string? TryReadRequestedAction(string reasonCode)
    {
        if (reasonCode.StartsWith(OperationProgressedReasonPrefix, StringComparison.Ordinal))
        {
            var payload = reasonCode[OperationProgressedReasonPrefix.Length..];
            var separatorIndex = payload.IndexOf(':');
            return separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : null;
        }

        if (reasonCode.StartsWith(ReviewResolvedReasonPrefix, StringComparison.Ordinal))
        {
            return reasonCode[ReviewResolvedReasonPrefix.Length..];
        }

        if (reasonCode.StartsWith(OperationCompletedReasonPrefix, StringComparison.Ordinal))
        {
            return reasonCode[OperationCompletedReasonPrefix.Length..];
        }

        if (reasonCode.StartsWith(OperationFailedReasonPrefix, StringComparison.Ordinal))
        {
            return reasonCode[OperationFailedReasonPrefix.Length..];
        }

        return reasonCode switch
        {
            ReplanRequestedReasonPrefix => "replan",
            ReplanProjectedReasonPrefix => "replan",
            _ => null,
        };
    }

    private void RecordInitialOperatorProofContract(SessionGatewayOperationBindingSurface binding)
    {
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayOperatorActionRequired,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = binding.OperationId,
            ReasonCode = $"{OperatorActionRequiredReasonPrefix}{SessionGatewayOperatorWaitStates.WaitingOperatorSetup}:{SessionGatewayProofSources.RepoLocalProof}",
            Summary = "Operator action required: select or create a real project before this Session Gateway lane can claim real-world proof.",
        });
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayOperatorProjectRequired,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = binding.OperationId,
            ReasonCode = $"{OperatorProjectRequiredReasonPrefix}{SessionGatewayOperatorWaitStates.WaitingOperatorSetup}",
            Summary = "Operator project required: declare the real repo path and startup command for this bounded scenario.",
        });
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayOperatorEvidenceRequired,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = binding.OperationId,
            ReasonCode = $"{OperatorEvidenceRequiredReasonPrefix}{SessionGatewayOperatorWaitStates.WaitingOperatorEvidence}:{SessionGatewayProofSources.OperatorRunProof}",
            Summary = "Operator evidence required: attach logs, operation identifiers, and result artifacts before claiming real-world completion.",
        });
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayRealWorldProofMissing,
            RepoId = repoId,
            ActorSessionId = binding.SessionId,
            ActorKind = ActorSessionKind.Agent,
            ActorIdentity = SessionGatewayActorIdentity,
            TaskId = binding.TaskId,
            RunId = binding.MessageId,
            ReferenceId = binding.OperationId,
            ReasonCode = $"{RealWorldProofMissingReasonPrefix}{SessionGatewayProofSources.RepoLocalProof}",
            Summary = "Real-world proof is missing: current Session Gateway posture is repo-local only until an operator run and evidence bundle exist.",
        });
    }

    private static string? TryReadOperatorRequiredState(string reasonCode)
    {
        return reasonCode.StartsWith(OperatorActionRequiredReasonPrefix, StringComparison.Ordinal)
            ? ReadOperatorStatePayload(reasonCode[OperatorActionRequiredReasonPrefix.Length..])
            : reasonCode.StartsWith(OperatorProjectRequiredReasonPrefix, StringComparison.Ordinal)
                ? ReadOperatorStatePayload(reasonCode[OperatorProjectRequiredReasonPrefix.Length..])
                : reasonCode.StartsWith(OperatorEvidenceRequiredReasonPrefix, StringComparison.Ordinal)
                    ? ReadOperatorStatePayload(reasonCode[OperatorEvidenceRequiredReasonPrefix.Length..])
                    : null;
    }

    private static string? TryReadProofSource(string reasonCode)
    {
        return reasonCode.StartsWith(OperatorActionRequiredReasonPrefix, StringComparison.Ordinal)
            ? ReadProofSourcePayload(reasonCode[OperatorActionRequiredReasonPrefix.Length..])
            : reasonCode.StartsWith(OperatorEvidenceRequiredReasonPrefix, StringComparison.Ordinal)
                ? ReadProofSourcePayload(reasonCode[OperatorEvidenceRequiredReasonPrefix.Length..])
                : reasonCode.StartsWith(RealWorldProofMissingReasonPrefix, StringComparison.Ordinal)
                    ? reasonCode[RealWorldProofMissingReasonPrefix.Length..]
                    : null;
    }

    private static string? ReadOperatorStatePayload(string payload)
    {
        var separatorIndex = payload.IndexOf(':');
        return separatorIndex >= 0 ? payload[..separatorIndex] : payload;
    }

    private static string? ReadProofSourcePayload(string payload)
    {
        var separatorIndex = payload.IndexOf(':');
        return separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : null;
    }

    private static string TryReadReviewResolutionStage(string reasonCode)
    {
        return TryReadRequestedAction(reasonCode) switch
        {
            "approve" => "approved",
            "reject" => "rejected",
            _ => "resolved",
        };
    }
}
