using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeWorkerExecutionAudit(string? query = null)
    {
        return FormatRuntimeWorkerExecutionAudit(CreateRuntimeWorkerExecutionAuditService().Build(query));
    }

    public OperatorCommandResult ApiRuntimeWorkerExecutionAudit(string? query = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimeWorkerExecutionAuditService().Build(query)));
    }

    private RuntimeWorkerExecutionAuditService CreateRuntimeWorkerExecutionAuditService()
    {
        return new RuntimeWorkerExecutionAuditService(paths, workerExecutionAuditReadModel);
    }

    private static OperatorCommandResult FormatRuntimeWorkerExecutionAudit(RuntimeWorkerExecutionAuditSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime worker execution audit",
            surface.Summary,
            $"Storage path: {surface.StoragePath}",
            $"Read model configured: {surface.ReadModelConfigured}",
            $"Storage exists: {surface.StorageExists}",
            $"Available: {surface.Available}",
            $"Availability status: {surface.AvailabilityStatus}",
            "Non-canonical sidecar: True",
            $"Query: requested={surface.Query.RequestedQuery}; effective={surface.Query.EffectiveQuery}; mode={surface.Query.QueryMode}; limit={surface.Query.Limit}",
            $"Total executions: {surface.Counts.TotalExecutions}",
            $"Succeeded executions: {surface.Counts.SucceededExecutions}",
            $"Failed executions: {surface.Counts.FailedExecutions}",
            $"Blocked executions: {surface.Counts.BlockedExecutions}",
            $"Query matched executions: {surface.QueryCounts.TotalExecutions}",
            $"Query matched failed: {surface.QueryCounts.FailedExecutions}",
            $"Query matched safety blocked: {surface.QueryCounts.SafetyBlockedExecutions}",
            $"Permission requests: {surface.Counts.PermissionRequestCount}",
            $"Changed files: {surface.Counts.ChangedFilesCount}",
            $"Latest task: {surface.Counts.LatestTaskId ?? "(none)"}",
            "Recent entries:",
        };

        if (surface.Query.UnsupportedTerms.Count > 0)
        {
            lines.Add($"Ignored query terms: {string.Join(", ", surface.Query.UnsupportedTerms)}");
        }

        if (surface.RecentEntries.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var entry in surface.RecentEntries.Take(10))
            {
                lines.Add($"- {entry.TaskId}: run={entry.RunId}; status={entry.Status}; backend={entry.BackendId}; provider={entry.ProviderId}; files={entry.ChangedFilesCount}; safety={entry.SafetyOutcome}");
            }
        }

        lines.Add("Supported query fields:");
        foreach (var field in surface.SupportedQueryFields)
        {
            lines.Add($"- {field}");
        }

        lines.Add("Query examples:");
        foreach (var example in surface.QueryExamples)
        {
            lines.Add($"- {example}");
        }

        lines.Add("Notes:");
        foreach (var note in surface.Notes)
        {
            lines.Add($"- {note}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
