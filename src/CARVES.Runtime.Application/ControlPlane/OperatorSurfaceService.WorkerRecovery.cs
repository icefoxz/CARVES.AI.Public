namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult WorkerHealth(string? backendId, bool refresh)
    {
        var records = operatorApiService.GetWorkerHealth(refresh, backendId);
        var lines = new List<string> { $"Worker provider health records: {records.Count}" };
        if (records.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var record in records)
        {
            lines.Add($"- {record.BackendId} [{record.State}] latency={record.LatencyMs?.ToString() ?? "(none)"}ms failures={record.ConsecutiveFailureCount}");
            lines.Add($"  provider/adapter: {record.ProviderId}/{record.AdapterId}");
            lines.Add($"  checked: {record.CheckedAt:O}");
            lines.Add($"  summary: {record.Summary}");
            lines.Add($"  degradation: {record.DegradationReason ?? "(none)"}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult WorkerIncidents(string? taskId, string? runId)
    {
        var incidents = operatorApiService.GetRuntimeIncidents(taskId, runId);
        var lines = new List<string> { $"Runtime incidents: {incidents.Count}" };
        if (incidents.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var incident in incidents.Take(50))
        {
            lines.Add($"- {incident.IncidentType} [{incident.OccurredAt:O}] task={incident.TaskId ?? "(none)"} run={incident.RunId ?? "(none)"} backend={incident.BackendId ?? "(none)"} protocol={incident.ProtocolFamily ?? "(none)"} failure={incident.FailureKind}/{incident.FailureLayer} action={incident.RecoveryAction} actor={incident.ActorKind}:{incident.ActorIdentity}");
            lines.Add($"  summary: {incident.Summary}");
            lines.Add($"  consequence: {incident.ConsequenceSummary}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult ApiWorkerHealth(string? backendId, bool refresh)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetWorkerHealth(refresh, backendId)));
    }

    public OperatorCommandResult ApiWorkerIncidents(string? taskId, string? runId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetRuntimeIncidents(taskId, runId)));
    }
}
