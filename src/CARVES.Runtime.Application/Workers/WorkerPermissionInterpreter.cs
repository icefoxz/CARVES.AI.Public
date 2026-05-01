using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerPermissionInterpreter
{
    public IReadOnlyList<WorkerPermissionRequest> Interpret(WorkerRequest request, WorkerExecutionResult result)
    {
        var requests = new List<WorkerPermissionRequest>();
        foreach (var item in result.Events.Where(eventItem =>
                     eventItem.EventType == WorkerEventType.PermissionRequested
                     || (eventItem.EventType == WorkerEventType.RawError && MightDescribePermission(eventItem.Summary, eventItem.RawPayload))))
        {
            requests.Add(BuildRequest(request, result, item));
        }

        if (requests.Count == 0 && result.Status == WorkerExecutionStatus.ApprovalWait)
        {
            var approvalWaitEvents = result.Events
                .Where(eventItem => eventItem.EventType == WorkerEventType.ApprovalWait)
                .Select(item => BuildRequest(request, result, item))
                .ToArray();
            if (approvalWaitEvents.Length > 0)
            {
                requests.AddRange(approvalWaitEvents);
            }
            else
            {
                requests.Add(BuildFallbackRequest(request, result));
            }
        }

        return requests
            .GroupBy(item => $"{item.Kind}:{item.ScopeSummary}:{item.ResourcePath}:{item.Summary}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.RequestedAt).First())
            .OrderBy(item => item.RequestedAt)
            .ToArray();
    }

    private static WorkerPermissionRequest BuildRequest(WorkerRequest request, WorkerExecutionResult result, WorkerEvent item)
    {
        var rawPrompt = FirstNonEmpty(item.RawPayload, item.Summary, item.CommandText);
        var attributes = item.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var kind = InferKind(rawPrompt, attributes, item.FilePath);
        return new WorkerPermissionRequest
        {
            RunId = result.RunId,
            TaskId = request.Task.TaskId,
            BackendId = result.BackendId,
            ProviderId = result.ProviderId,
            AdapterId = result.AdapterId,
            ProfileId = result.ProfileId,
            Kind = kind,
            RiskLevel = InferRiskLevel(kind),
            ScopeSummary = InferScopeSummary(kind, request, attributes, item.FilePath),
            ResourcePath = FirstNonEmpty(
                item.FilePath,
                attributes.TryGetValue("resource_path", out var resourcePath) ? resourcePath : null,
                attributes.TryGetValue("path", out var path) ? path : null),
            Summary = SummarizePrompt(rawPrompt),
            RawPrompt = rawPrompt,
            RawPayload = item.RawPayload,
            Attributes = new Dictionary<string, string>(attributes, StringComparer.Ordinal),
            RequestedAt = item.OccurredAt,
        };
    }

    private static WorkerPermissionRequest BuildFallbackRequest(WorkerRequest request, WorkerExecutionResult result)
    {
        var rawPrompt = FirstNonEmpty(result.FailureReason, result.Summary, request.ExecutionRequest?.Input);
        var kind = InferKind(rawPrompt, new Dictionary<string, string>(StringComparer.Ordinal), filePath: null);
        return new WorkerPermissionRequest
        {
            RunId = result.RunId,
            TaskId = request.Task.TaskId,
            BackendId = result.BackendId,
            ProviderId = result.ProviderId,
            AdapterId = result.AdapterId,
            ProfileId = result.ProfileId,
            Kind = kind,
            RiskLevel = InferRiskLevel(kind),
            ScopeSummary = InferScopeSummary(kind, request, new Dictionary<string, string>(StringComparer.Ordinal), filePath: null),
            Summary = SummarizePrompt(rawPrompt),
            RawPrompt = rawPrompt,
            RawPayload = rawPrompt,
            RequestedAt = result.CompletedAt == default ? DateTimeOffset.UtcNow : result.CompletedAt,
        };
    }

    private static WorkerPermissionKind InferKind(string? rawPrompt, IReadOnlyDictionary<string, string> attributes, string? filePath)
    {
        if (attributes.TryGetValue("permission_kind", out var explicitKind))
        {
            return explicitKind.ToLowerInvariant() switch
            {
                "filesystem_write" => WorkerPermissionKind.FilesystemWrite,
                "filesystem_delete" => WorkerPermissionKind.FilesystemDelete,
                "outside_workspace_access" => WorkerPermissionKind.OutsideWorkspaceAccess,
                "network_access" => WorkerPermissionKind.NetworkAccess,
                "process_control" => WorkerPermissionKind.ProcessControl,
                "system_configuration" => WorkerPermissionKind.SystemConfiguration,
                "secret_access" => WorkerPermissionKind.SecretAccess,
                "elevated_privilege" => WorkerPermissionKind.ElevatedPrivilege,
                _ => WorkerPermissionKind.UnknownPermissionRequest,
            };
        }

        var text = $"{rawPrompt} {filePath}".ToLowerInvariant();
        if (text.Contains("outside workspace", StringComparison.Ordinal)
            || text.Contains("outside the workspace", StringComparison.Ordinal)
            || text.Contains("outside repository", StringComparison.Ordinal)
            || text.Contains("outside repo", StringComparison.Ordinal))
        {
            return WorkerPermissionKind.OutsideWorkspaceAccess;
        }

        if (text.Contains("delete", StringComparison.Ordinal)
            || text.Contains("remove", StringComparison.Ordinal)
            || text.Contains("rm ", StringComparison.Ordinal)
            || text.Contains("remove-item", StringComparison.Ordinal))
        {
            return WorkerPermissionKind.FilesystemDelete;
        }

        if (text.Contains("write", StringComparison.Ordinal)
            || text.Contains("edit", StringComparison.Ordinal)
            || text.Contains("create file", StringComparison.Ordinal)
            || text.Contains("modify file", StringComparison.Ordinal))
        {
            return WorkerPermissionKind.FilesystemWrite;
        }

        if (text.Contains("network", StringComparison.Ordinal)
            || text.Contains("internet", StringComparison.Ordinal)
            || text.Contains("download", StringComparison.Ordinal)
            || text.Contains("http", StringComparison.Ordinal))
        {
            return WorkerPermissionKind.NetworkAccess;
        }

        if (text.Contains("kill process", StringComparison.Ordinal)
            || text.Contains("spawn process", StringComparison.Ordinal)
            || text.Contains("process", StringComparison.Ordinal)
            || text.Contains("command", StringComparison.Ordinal))
        {
            return WorkerPermissionKind.ProcessControl;
        }

        if (text.Contains("secret", StringComparison.Ordinal)
            || text.Contains("token", StringComparison.Ordinal)
            || text.Contains("api key", StringComparison.Ordinal)
            || text.Contains("credential", StringComparison.Ordinal))
        {
            return WorkerPermissionKind.SecretAccess;
        }

        if (text.Contains("sudo", StringComparison.Ordinal)
            || text.Contains("administrator", StringComparison.Ordinal)
            || text.Contains("elevated", StringComparison.Ordinal)
            || text.Contains("privilege", StringComparison.Ordinal))
        {
            return WorkerPermissionKind.ElevatedPrivilege;
        }

        if (text.Contains("system configuration", StringComparison.Ordinal)
            || text.Contains("registry", StringComparison.Ordinal)
            || text.Contains("environment variable", StringComparison.Ordinal))
        {
            return WorkerPermissionKind.SystemConfiguration;
        }

        return WorkerPermissionKind.UnknownPermissionRequest;
    }

    private static WorkerPermissionRiskLevel InferRiskLevel(WorkerPermissionKind kind)
    {
        return kind switch
        {
            WorkerPermissionKind.FilesystemWrite => WorkerPermissionRiskLevel.Moderate,
            WorkerPermissionKind.FilesystemDelete => WorkerPermissionRiskLevel.High,
            WorkerPermissionKind.OutsideWorkspaceAccess => WorkerPermissionRiskLevel.Critical,
            WorkerPermissionKind.NetworkAccess => WorkerPermissionRiskLevel.High,
            WorkerPermissionKind.ProcessControl => WorkerPermissionRiskLevel.High,
            WorkerPermissionKind.SystemConfiguration => WorkerPermissionRiskLevel.Critical,
            WorkerPermissionKind.SecretAccess => WorkerPermissionRiskLevel.Critical,
            WorkerPermissionKind.ElevatedPrivilege => WorkerPermissionRiskLevel.Critical,
            _ => WorkerPermissionRiskLevel.High,
        };
    }

    private static string InferScopeSummary(WorkerPermissionKind kind, WorkerRequest request, IReadOnlyDictionary<string, string> attributes, string? filePath)
    {
        if (attributes.TryGetValue("scope", out var explicitScope) && !string.IsNullOrWhiteSpace(explicitScope))
        {
            return explicitScope;
        }

        var resourcePath = FirstNonEmpty(filePath, attributes.TryGetValue("path", out var path) ? path : null);
        if (!string.IsNullOrWhiteSpace(resourcePath))
        {
            try
            {
                var worktreeRoot = Path.GetFullPath(request.Session.WorktreeRoot);
                var fullPath = Path.GetFullPath(Path.IsPathRooted(resourcePath) ? resourcePath : Path.Combine(worktreeRoot, resourcePath));
                if (!fullPath.StartsWith(worktreeRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return "outside_workspace";
                }

                return "workspace";
            }
            catch
            {
            }
        }

        return kind == WorkerPermissionKind.OutsideWorkspaceAccess ? "outside_workspace" : request.ExecutionRequest?.Profile.WorkspaceBoundary ?? "workspace";
    }

    private static bool MightDescribePermission(string summary, string? rawPayload)
    {
        var text = $"{summary} {rawPayload}".ToLowerInvariant();
        return text.Contains("permission", StringComparison.Ordinal)
            || text.Contains("approval", StringComparison.Ordinal)
            || text.Contains("continue? [y/n]", StringComparison.Ordinal)
            || text.Contains("authorize", StringComparison.Ordinal);
    }

    private static string SummarizePrompt(string? rawPrompt)
    {
        var compact = FirstNonEmpty(rawPrompt, "(no permission prompt)")
            .Replace(Environment.NewLine, " ", StringComparison.Ordinal)
            .Trim();
        return compact.Length <= 180 ? compact : $"{compact[..177]}...";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
