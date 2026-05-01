using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeSessionGatewayService
{
    private static readonly TimeSpan ResourceLeaseDuration = TimeSpan.FromMinutes(30);

    private SessionGatewayResourceLeaseSurface IssueResourceLeaseDryRun(
        string workOrderId,
        SessionGatewayMessageRequest request,
        SessionGatewayCapabilityLeaseSurface capabilityLease)
    {
        var now = DateTimeOffset.UtcNow;
        var declaredWriteSet = BuildDeclaredResourceWriteSet(request);
        var result = resourceLeaseService.ProjectAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = workOrderId,
            TaskGraphId = NormalizeOptional(request.TargetTaskGraphId),
            TaskId = NormalizeOptional(request.TargetTaskId),
            DeclaredWriteSet = declaredWriteSet,
            ConflictPolicy = ResourceLeaseConflictPolicy.Stop,
            Now = now,
            ValidUntil = capabilityLease.ValidUntil ?? now.Add(ResourceLeaseDuration),
        });

        return ProjectResourceLease(result.Lease);
    }

    private static SessionGatewayResourceLeaseSurface BuildNotRequiredResourceLease()
    {
        return new SessionGatewayResourceLeaseSurface
        {
            LeaseState = "not_required",
            ConflictPolicy = "stop",
            ConflictResolution = "not_required",
            CanRunInParallel = false,
            DeclaredWriteSetWithinLease = false,
        };
    }

    private ResourceWriteSet BuildDeclaredResourceWriteSet(SessionGatewayMessageRequest request)
    {
        var hostDerived = BuildHostDeclaredResourceWriteSet(request);
        var taskIds = new List<string>();
        AddIfPresent(taskIds, request.TargetTaskId);
        var targetTaskId = NormalizeOptional(request.TargetTaskId);

        return new ResourceWriteSet
        {
            TaskIds = hostDerived.TaskIds
                .Concat(taskIds)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Paths = hostDerived.Paths
                .Concat(ReadStringArray(request.ClientCapabilities, "declared_write_paths", "write_paths", "paths"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Modules = hostDerived.Modules
                .Concat(ReadStringArray(request.ClientCapabilities, "declared_modules", "modules"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TruthOperations = hostDerived.TruthOperations
                .Concat(ReadStringArray(request.ClientCapabilities, "declared_truth_operations", "truth_operations"))
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            TargetBranches = hostDerived.TargetBranches
                .Concat(ReadStringArray(request.ClientCapabilities, "target_branches", "branches"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };
    }

    private ResourceWriteSet BuildHostDeclaredResourceWriteSet(SessionGatewayMessageRequest request)
    {
        var taskIds = new List<string>();
        var paths = new List<string>();
        var modules = new List<string>();
        var truthOperations = new List<string>();
        var targetTaskId = NormalizeOptional(request.TargetTaskId);
        if (!string.IsNullOrWhiteSpace(targetTaskId))
        {
            taskIds.Add(targetTaskId);
            paths.Add($".ai/execution/{targetTaskId}");
            paths.Add($".ai/artifacts/worker-executions/{targetTaskId}");
            paths.Add($".ai/runtime/boundary/decisions/{targetTaskId}.json");
            paths.Add($".ai/runtime/run-reports/{targetTaskId}");
            paths.Add($".ai/tasks/nodes/{targetTaskId}.json");
            truthOperations.Add($"task_status_to_review:{targetTaskId}");
            AddHostTaskScope(paths, modules, targetTaskId);
        }

        var targetTaskGraphId = NormalizeOptional(request.TargetTaskGraphId);
        if (!string.IsNullOrWhiteSpace(targetTaskGraphId))
        {
            paths.Add($".ai/runtime/planning/taskgraph-drafts/{targetTaskGraphId}");
            truthOperations.Add($"taskgraph_review_submission:{targetTaskGraphId}");
            AddTaskGraphScope(paths, modules, targetTaskGraphId);
        }

        var targetCardId = NormalizeOptional(request.TargetCardId);
        if (!string.IsNullOrWhiteSpace(targetCardId))
        {
            paths.Add($".ai/tasks/cards/{targetCardId}.json");
            truthOperations.Add($"card_review_submission:{targetCardId}");
        }

        return new ResourceWriteSet
        {
            TaskIds = taskIds
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Paths = paths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Modules = modules
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TruthOperations = truthOperations
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            TargetBranches = [],
        };
    }

    private void AddHostTaskScope(
        ICollection<string> paths,
        ICollection<string> modules,
        string taskId)
    {
        var taskNodePath = Path.Combine(this.paths.TaskNodesRoot, $"{taskId}.json");
        if (!File.Exists(taskNodePath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(taskNodePath));
        var root = document.RootElement;
        if (root.TryGetProperty("scope", out var scopeElement) && scopeElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var path in ReadScopeEntries(scopeElement))
            {
                paths.Add(path);
                AddModuleHint(modules, path);
            }
        }

        if (root.TryGetProperty("metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object)
        {
            AddMetadataModules(metadataElement, modules, "codegraph_modules");
            AddMetadataModules(metadataElement, modules, "codegraph_dependency_modules");
            AddMetadataModules(metadataElement, modules, "codegraph_impacted_modules");
        }
    }

    private void AddTaskGraphScope(ICollection<string> paths, ICollection<string> modules, string taskGraphId)
    {
        var taskGraphPath = Path.Combine(this.paths.PlanningTaskGraphDraftsRoot, $"{taskGraphId}.json");
        if (!File.Exists(taskGraphPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(taskGraphPath));
        var root = document.RootElement;
        if (!root.TryGetProperty("tasks", out var tasksElement) || tasksElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var taskElement in tasksElement.EnumerateArray())
        {
            if (!taskElement.TryGetProperty("scope", out var scopeElement) || scopeElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var path in ReadScopeEntries(scopeElement))
            {
                paths.Add(path);
                AddModuleHint(modules, path);
            }
        }
    }

    private static IEnumerable<string> ReadScopeEntries(JsonElement scopeElement)
    {
        foreach (var item in scopeElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var normalized = NormalizeScopeEntry(item.GetString());
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static void AddMetadataModules(JsonElement metadataElement, ICollection<string> modules, string propertyName)
    {
        if (!metadataElement.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return;
        }

        foreach (var module in property.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            AddModuleHint(modules, module);
        }
    }

    private static string? NormalizeScopeEntry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value
            .Trim()
            .Trim('`')
            .Replace('\\', '/')
            .TrimStart("./".ToCharArray());
    }

    private static void AddModuleHint(ICollection<string> modules, string? value)
    {
        var normalized = NormalizeModuleHint(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            modules.Add(normalized);
        }
    }

    private static string? NormalizeModuleHint(string? value)
    {
        var normalized = NormalizeScopeEntry(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            var directory = Path.GetDirectoryName(normalized.Replace('/', Path.DirectorySeparatorChar));
            return directory?.Replace('\\', '/').Trim().ToLowerInvariant();
        }

        return normalized.TrimEnd('/').ToLowerInvariant();
    }

    private static SessionGatewayResourceLeaseSurface ProjectResourceLease(ResourceLeaseRecord lease)
    {
        var leaseState = ToSnakeCase(lease.Status);
        return new SessionGatewayResourceLeaseSurface
        {
            LeaseId = lease.LeaseId,
            WorkOrderId = lease.WorkOrderId,
            ParentWorkOrderId = lease.ParentWorkOrderId,
            TaskGraphId = lease.TaskGraphId,
            TaskId = lease.TaskId,
            LeaseState = leaseState,
            ConflictPolicy = ToSnakeCase(lease.ConflictPolicy),
            ConflictResolution = lease.ConflictResolution,
            CanRunInParallel = lease.Status is ResourceLeaseStatus.Active or ResourceLeaseStatus.Projected
                && lease.ConflictReasons.Count == 0
                && lease.StopReasons.Count == 0,
            DeclaredWriteSetWithinLease = lease.Status is ResourceLeaseStatus.Active or ResourceLeaseStatus.Projected or ResourceLeaseStatus.Queued,
            AcquiredAt = lease.AcquiredAtUtc,
            ValidUntil = lease.ExpiresAtUtc,
            DeclaredWriteSet = ProjectWriteSet(lease.DeclaredWriteSet),
            ActualWriteSet = ProjectWriteSet(lease.ActualWriteSet),
            ConflictReasons = lease.ConflictReasons,
            StopReasons = lease.StopReasons,
            BlockingLeaseIds = lease.BlockingLeaseIds,
        };
    }

    private static SessionGatewayResourceWriteSetSurface ProjectWriteSet(ResourceWriteSet writeSet)
    {
        return new SessionGatewayResourceWriteSetSurface
        {
            TaskIds = writeSet.TaskIds,
            Paths = writeSet.Paths,
            Modules = writeSet.Modules,
            TruthOperations = writeSet.TruthOperations,
            TargetBranches = writeSet.TargetBranches,
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject? capabilities, params string[] keys)
    {
        if (capabilities is null)
        {
            return [];
        }

        var values = new List<string>();
        foreach (var key in keys)
        {
            if (!capabilities.TryGetPropertyValue(key, out var node) || node is null)
            {
                continue;
            }

            switch (node)
            {
                case JsonArray array:
                    foreach (var item in array)
                    {
                        AddJsonString(values, item);
                    }

                    break;
                case JsonValue:
                    AddJsonString(values, node);
                    break;
            }
        }

        return values
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddJsonString(ICollection<string> values, JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            AddIfPresent(values, text);
        }
    }

    private static void AddIfPresent(ICollection<string> values, string? value)
    {
        var normalized = NormalizeOptional(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            values.Add(normalized);
        }
    }

    private static string ToSnakeCase(ResourceLeaseStatus status)
    {
        return status switch
        {
            ResourceLeaseStatus.Projected => "projected",
            ResourceLeaseStatus.Active => "active",
            ResourceLeaseStatus.Queued => "queued",
            ResourceLeaseStatus.Released => "released",
            ResourceLeaseStatus.Expired => "expired",
            ResourceLeaseStatus.EscalationRequired => "escalation_required",
            ResourceLeaseStatus.Stopped => "stopped",
            _ => status.ToString().ToLowerInvariant(),
        };
    }

    private static string ToSnakeCase(ResourceLeaseConflictPolicy policy)
    {
        return policy switch
        {
            ResourceLeaseConflictPolicy.Stop => "stop",
            ResourceLeaseConflictPolicy.Queue => "queue",
            ResourceLeaseConflictPolicy.SerializeTruthOperations => "serialize_truth_operations",
            _ => policy.ToString().ToLowerInvariant(),
        };
    }
}
