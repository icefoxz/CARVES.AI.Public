using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostServer
{
    private void ExecuteAcceptedOperation(
        string operationId,
        RuntimeServices scopedServices,
        string command,
        IReadOnlyList<string> commandArguments,
        string requestId)
    {
        WriteGatewayEvent(
            GatewayActivityEventKinds.AcceptedOperationRunning,
            GatewayField("request_id", requestId),
            GatewayField("operation_id", operationId),
            GatewayField("command", command),
            GatewayField("arguments", string.Join(' ', commandArguments)));
        acceptedOperationStore.MarkDispatching(operationId);
        acceptedOperationStore.MarkRunning(operationId);
        using var heartbeatTimer = new Timer(
            static state =>
            {
                var payload = (HeartbeatPayload)state!;
                payload.Store.RecordHeartbeat(payload.OperationId);
            },
            new HeartbeatPayload(acceptedOperationStore, operationId),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1));

        try
        {
            var result = LocalHostCommandDispatcher.Dispatch(scopedServices, command, commandArguments);

            acceptedOperationStore.MarkWriteback(operationId);
            acceptedOperationStore.Complete(operationId, result);
            WriteGatewayEvent(
                GatewayActivityEventKinds.AcceptedOperationCompleted,
                GatewayField("request_id", requestId),
                GatewayField("operation_id", operationId),
                GatewayField("command", command),
                GatewayField("exit_code", result.ExitCode),
                GatewayField("line_count", result.Lines.Count));
            SaveSnapshot(HostRuntimeSnapshotState.Live, $"Completed accepted host operation {operationId} for {command}.");
        }
        catch (Exception exception)
        {
            acceptedOperationStore.Fail(operationId, exception.Message);
            WriteGatewayEvent(
                GatewayActivityEventKinds.AcceptedOperationFailed,
                GatewayField("request_id", requestId),
                GatewayField("operation_id", operationId),
                GatewayField("command", command),
                GatewayField("error", exception.Message));
            SaveSnapshot(HostRuntimeSnapshotState.Live, $"Accepted host operation {operationId} for {command} failed: {exception.Message}");
        }
        finally
        {
            heartbeatTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private sealed record HeartbeatPayload(HostAcceptedOperationStore Store, string OperationId);
}
