using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public SessionGatewayOperationSurface? TryGetSessionGatewayOperation(string operationId)
    {
        var binding = services.RuntimeSessionGatewayService.TryGetOperationBinding(operationId);
        var accepted = acceptedOperationStore?.TryGet(operationId);
        if (binding is null && accepted is null)
        {
            return null;
        }

        return BuildSessionGatewayOperationSurface(operationId, binding, accepted);
    }

    public SessionGatewayOperationSurface ApproveSessionGatewayOperation(string operationId, SessionGatewayOperationMutationRequest request)
    {
        return QueueSessionGatewayOperation(operationId, NormalizeRequestedAction("approve"), request.Reason);
    }

    public SessionGatewayOperationSurface RejectSessionGatewayOperation(string operationId, SessionGatewayOperationMutationRequest request)
    {
        return QueueSessionGatewayOperation(operationId, NormalizeRequestedAction("reject"), request.Reason);
    }

    public SessionGatewayOperationSurface ReplanSessionGatewayOperation(string operationId, SessionGatewayOperationMutationRequest request)
    {
        return QueueSessionGatewayOperation(operationId, NormalizeRequestedAction("replan"), request.Reason);
    }

    private SessionGatewayOperationSurface QueueSessionGatewayOperation(string operationId, string requestedAction, string reason)
    {
        if (acceptedOperationStore is null)
        {
            throw new InvalidOperationException("Session Gateway mutation forwarding requires resident host accepted-operation state.");
        }

        var binding = services.RuntimeSessionGatewayService.SetRequestedAction(operationId, requestedAction)
            ?? throw new InvalidOperationException($"Session Gateway operation '{operationId}' was not found.");
        var taskId = binding.TaskId;
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new InvalidOperationException($"Session Gateway operation '{operationId}' is not bound to Runtime task truth.");
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? $"Session Gateway requested {requestedAction} for {binding.TaskId}."
            : reason.Trim();
        var current = acceptedOperationStore.Accept($"session-gateway-{requestedAction}", operationId);
        if (current.Completed)
        {
            throw new InvalidOperationException($"Session Gateway operation '{operationId}' is already completed.");
        }

        if (!string.Equals(current.ProgressMarker, HostAcceptedOperationProgressMarkers.Accepted, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Session Gateway operation '{operationId}' is already in progress.");
        }

        var surface = BuildSessionGatewayOperationSurface(operationId, binding, current);
        StartBackgroundOperation(() => ExecuteQueuedSessionGatewayOperation(operationId, requestedAction, normalizedReason), $"CARVES-SessionGateway-{requestedAction}-{operationId}");
        return surface;
    }

    private void ExecuteQueuedSessionGatewayOperation(string operationId, string requestedAction, string normalizedReason)
    {
        if (acceptedOperationStore is null)
        {
            return;
        }

        try
        {
            var binding = services.RuntimeSessionGatewayService.TryGetOperationBinding(operationId)
                ?? throw new InvalidOperationException($"Session Gateway operation '{operationId}' was not found.");
            var taskId = binding.TaskId
                ?? throw new InvalidOperationException($"Session Gateway operation '{operationId}' is not bound to Runtime task truth.");
            services.RuntimeSessionGatewayService.RecordOperationProgress(
                operationId,
                HostAcceptedOperationProgressMarkers.Dispatching,
                requestedAction,
                $"Forwarding {requestedAction} for {binding.TaskId} through the Runtime-owned Session Gateway lane.");
            acceptedOperationStore.MarkDispatching(operationId);

            services.RuntimeSessionGatewayService.RecordOperationProgress(
                operationId,
                HostAcceptedOperationProgressMarkers.Running,
                requestedAction,
                $"Running Runtime-owned {requestedAction} forwarding for {binding.TaskId}.");
            acceptedOperationStore.MarkRunning(operationId);

            OperatorCommandResult result;
            switch (requestedAction)
            {
                case "approve":
                    result = services.OperatorSurfaceService.ApproveReview(taskId, normalizedReason, autoContinueAfterApprove: false);
                    if (result.ExitCode == 0)
                    {
                        services.RuntimeSessionGatewayService.RecordReviewResolved(operationId, requestedAction, normalizedReason);
                    }

                    break;

                case "reject":
                    result = services.OperatorSurfaceService.RejectReview(taskId, normalizedReason);
                    if (result.ExitCode == 0)
                    {
                        services.RuntimeSessionGatewayService.RecordReviewResolved(operationId, requestedAction, normalizedReason);
                    }

                    break;

                case "replan":
                    services.RuntimeSessionGatewayService.RecordReplanRequested(operationId, normalizedReason);
                    result = services.OperatorSurfaceService.RetryTask(taskId, normalizedReason);
                    if (result.ExitCode == 0)
                    {
                        services.RuntimeSessionGatewayService.RecordReplanProjected(operationId, normalizedReason);
                    }

                    break;

                default:
                    throw new InvalidOperationException($"Unsupported Session Gateway mutation action '{requestedAction}'.");
            }

            services.RuntimeSessionGatewayService.RecordOperationProgress(
                operationId,
                HostAcceptedOperationProgressMarkers.Writeback,
                requestedAction,
                $"Writing back Runtime-owned {requestedAction} outcome for {binding.TaskId}.");
            acceptedOperationStore.MarkWriteback(operationId);
            acceptedOperationStore.Complete(operationId, result);

            if (result.ExitCode == 0)
            {
                services.RuntimeSessionGatewayService.RecordOperationCompleted(operationId, requestedAction, result);
            }
            else
            {
                services.RuntimeSessionGatewayService.RecordOperationFailed(operationId, requestedAction, result.Lines.FirstOrDefault() ?? $"{requestedAction} failed.");
            }
        }
        catch (Exception exception)
        {
            acceptedOperationStore.Fail(operationId, exception.Message);
            services.RuntimeSessionGatewayService.RecordOperationFailed(operationId, requestedAction, exception.Message);
        }
    }

    private static SessionGatewayOperationSurface BuildSessionGatewayOperationSurface(
        string operationId,
        SessionGatewayOperationBindingSurface? binding,
        HostAcceptedOperationStatusResponse? accepted)
    {
        var acceptedAt = binding?.AcceptedAt ?? accepted?.AcceptedAt ?? DateTimeOffset.UtcNow;
        var updatedAt = accepted?.UpdatedAt ?? acceptedAt;
        return new SessionGatewayOperationSurface
        {
            OperationId = operationId,
            SessionId = binding?.SessionId ?? string.Empty,
            TurnId = binding?.TurnId,
            MessageId = binding?.MessageId,
            TaskId = binding?.TaskId,
            RequestedAction = binding?.RequestedAction,
            OperationState = accepted?.OperationState ?? HostAcceptedOperationProgressMarkers.Accepted,
            Completed = accepted?.Completed ?? false,
            ExitCode = accepted?.ExitCode,
            Lines = accepted?.Lines ?? Array.Empty<string>(),
            AcceptedAt = acceptedAt,
            UpdatedAt = updatedAt,
            CompletedAt = accepted?.CompletedAt,
            ProgressMarker = accepted?.ProgressMarker ?? HostAcceptedOperationProgressMarkers.Accepted,
            ProgressOrdinal = accepted?.ProgressOrdinal ?? HostAcceptedOperationProgressMarkers.ResolveOrdinal(HostAcceptedOperationProgressMarkers.Accepted),
            ProgressAt = accepted?.ProgressAt ?? acceptedAt,
            OperatorProofContract = RuntimeSessionGatewayOperatorProofContractCatalog.BuildPrivateAlphaContract(),
        };
    }

    private static string NormalizeRequestedAction(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "approve" => "approve",
            "reject" => "reject",
            "replan" => "replan",
            _ => throw new InvalidOperationException($"Unsupported Session Gateway mutation action '{value}'."),
        };
    }

    private static void StartBackgroundOperation(Action work, string name)
    {
        var thread = new Thread(() => work())
        {
            IsBackground = true,
            Name = name,
        };
        thread.Start();
    }

}
