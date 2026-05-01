using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class ResourceLeaseService
{
    public const string ConflictStopReason = "SC-RESOURCE-LEASE-CONFLICT";
    public const string ActualWriteSetEscalationStopReason = "SC-ACTUAL-WRITE-SET-ESCALATION";
    public const string LeaseNotFoundStopReason = "SC-RESOURCE-LEASE-NOT-FOUND";
    public const string StaleRecoveredReason = "SC-RESOURCE-LEASE-STALE-RECOVERED";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(30);

    private readonly ControlPlanePaths paths;

    public ResourceLeaseService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public string SnapshotPath => Path.Combine(paths.RuntimeLiveStateRoot, "resource_leases.json");

    public ResourceLeaseIssueResult TryAcquire(ResourceLeaseRequest request)
    {
        return Evaluate(request, ResourceLeaseStatus.Active, persist: true);
    }

    public ResourceLeaseIssueResult ProjectAcquire(ResourceLeaseRequest request)
    {
        return Evaluate(request, ResourceLeaseStatus.Projected, persist: false);
    }

    private ResourceLeaseIssueResult Evaluate(
        ResourceLeaseRequest request,
        ResourceLeaseStatus noConflictStatus,
        bool persist)
    {
        var now = request.Now ?? DateTimeOffset.UtcNow;
        var snapshot = LoadSnapshot();
        var recovered = MarkStale(snapshot.Leases, now);
        var activeLeases = snapshot.Leases
            .Where(lease => lease.Status is ResourceLeaseStatus.Active or ResourceLeaseStatus.Queued)
            .ToArray();
        var normalizedWriteSet = Normalize(WithTaskId(request.DeclaredWriteSet, request.TaskId));
        var conflicts = activeLeases
            .SelectMany(lease => DetectConflicts(normalizedWriteSet, lease))
            .ToArray();
        var blockingLeaseIds = conflicts
            .Select(conflict => conflict.BlockingLeaseId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var truthOnlyConflicts = conflicts.Length > 0
            && conflicts.All(static conflict => conflict.Kind == ResourceLeaseConflictKind.SameTruthOperation);
        var status = conflicts.Length == 0
            ? noConflictStatus
            : ResolveConflictStatus(request.ConflictPolicy, truthOnlyConflicts);
        var lease = new ResourceLeaseRecord
        {
            LeaseId = $"rl-{Guid.NewGuid():N}",
            WorkOrderId = NormalizeOptional(request.WorkOrderId),
            ParentWorkOrderId = NormalizeOptional(request.ParentWorkOrderId),
            TaskGraphId = NormalizeOptional(request.TaskGraphId),
            TaskId = NormalizeOptional(request.TaskId),
            DeclaredWriteSet = normalizedWriteSet,
            Status = status,
            ConflictPolicy = request.ConflictPolicy,
            ConflictResolution = ResolveConflictResolution(status, request.ConflictPolicy, truthOnlyConflicts),
            ConflictReasons = conflicts.Select(static conflict => conflict.Reason).ToArray(),
            StopReasons = conflicts.Length == 0 ? [] : [ConflictStopReason],
            BlockingLeaseIds = blockingLeaseIds,
            AcquiredAtUtc = now,
            ExpiresAtUtc = request.ValidUntil ?? now.Add(DefaultLeaseDuration),
            UpdatedAtUtc = now,
        };

        if (persist)
        {
            snapshot.Leases = snapshot.Leases
                .Append(lease)
                .OrderBy(static item => item.AcquiredAtUtc)
                .ToArray();
            SaveSnapshot(snapshot);
        }

        return new ResourceLeaseIssueResult(
            status == ResourceLeaseStatus.Active,
            status == ResourceLeaseStatus.Queued,
            lease,
            conflicts,
            recovered,
            status == ResourceLeaseStatus.Projected
                ? "Resource lease projection is available."
                : status == ResourceLeaseStatus.Active
                ? "Resource lease acquired."
                : status == ResourceLeaseStatus.Queued
                    ? "Resource lease queued by conflict policy."
                    : "Resource lease blocked by conflict policy.");
    }

    public ResourceLeaseReconcileResult ReconcileActualWriteSet(
        string leaseId,
        ResourceWriteSet actualWriteSet,
        ResourceLeaseActualWriteSetPolicy policy = ResourceLeaseActualWriteSetPolicy.Stop)
    {
        var snapshot = LoadSnapshot();
        var lease = snapshot.Leases.FirstOrDefault(item => string.Equals(item.LeaseId, leaseId, StringComparison.Ordinal));
        if (lease is null)
        {
            return ResourceLeaseReconcileResult.Block(
                leaseId,
                [LeaseNotFoundStopReason],
                $"Resource lease '{leaseId}' was not found.");
        }

        var normalizedActual = Normalize(actualWriteSet);
        var escalations = ResolveActualWriteSetEscalations(lease.DeclaredWriteSet, normalizedActual);
        lease.ActualWriteSet = normalizedActual;
        lease.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (escalations.Count != 0)
        {
            lease.Status = policy == ResourceLeaseActualWriteSetPolicy.RequestEscalation
                ? ResourceLeaseStatus.EscalationRequired
                : ResourceLeaseStatus.Stopped;
            lease.StopReasons = [ActualWriteSetEscalationStopReason];
            lease.ConflictReasons = escalations;
            lease.ConflictResolution = policy == ResourceLeaseActualWriteSetPolicy.RequestEscalation
                ? "operator_escalation_required"
                : "stopped_on_actual_write_set_expansion";
            SaveSnapshot(snapshot);
            return new ResourceLeaseReconcileResult(
                lease.LeaseId,
                false,
                policy == ResourceLeaseActualWriteSetPolicy.RequestEscalation,
                lease,
                [ActualWriteSetEscalationStopReason],
                escalations,
                lease.ConflictResolution);
        }

        lease.ConflictResolution = "actual_write_set_within_declared_scope";
        SaveSnapshot(snapshot);
        return new ResourceLeaseReconcileResult(
            lease.LeaseId,
            true,
            false,
            lease,
            [],
            [],
            "actual_write_set_within_declared_scope");
    }

    public ResourceLeaseReleaseResult Release(string leaseId, string reason)
    {
        var snapshot = LoadSnapshot();
        var lease = snapshot.Leases.FirstOrDefault(item => string.Equals(item.LeaseId, leaseId, StringComparison.Ordinal));
        if (lease is null)
        {
            return new ResourceLeaseReleaseResult(false, leaseId, LeaseNotFoundStopReason);
        }

        lease.Status = ResourceLeaseStatus.Released;
        lease.ReleasedAtUtc = DateTimeOffset.UtcNow;
        lease.ReleaseReason = string.IsNullOrWhiteSpace(reason) ? "released" : reason.Trim();
        lease.UpdatedAtUtc = lease.ReleasedAtUtc.Value;
        SaveSnapshot(snapshot);
        return new ResourceLeaseReleaseResult(true, leaseId, lease.ReleaseReason);
    }

    public ResourceLeaseRecoveryResult RecoverStale(DateTimeOffset? now = null)
    {
        var snapshot = LoadSnapshot();
        var recovered = MarkStale(snapshot.Leases, now ?? DateTimeOffset.UtcNow);
        if (recovered.Count > 0)
        {
            SaveSnapshot(snapshot);
        }

        return new ResourceLeaseRecoveryResult(recovered, recovered.Count == 0
            ? "No stale resource leases were found."
            : $"Recovered {recovered.Count} stale resource lease(s).");
    }

    public ResourceLeaseSnapshot LoadSnapshot()
    {
        if (!File.Exists(SnapshotPath))
        {
            return new ResourceLeaseSnapshot();
        }

        return JsonSerializer.Deserialize<ResourceLeaseSnapshot>(File.ReadAllText(SnapshotPath), JsonOptions)
            ?? new ResourceLeaseSnapshot();
    }

    public IReadOnlyList<ResourceLeaseRecord> LoadActive(DateTimeOffset? now = null)
    {
        var snapshot = LoadSnapshot();
        var recovered = MarkStale(snapshot.Leases, now ?? DateTimeOffset.UtcNow);
        if (recovered.Count > 0)
        {
            SaveSnapshot(snapshot);
        }

        return snapshot.Leases
            .Where(static lease => lease.Status == ResourceLeaseStatus.Active)
            .OrderBy(static lease => lease.AcquiredAtUtc)
            .ToArray();
    }

    private void SaveSnapshot(ResourceLeaseSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath)!);
        File.WriteAllText(SnapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));
    }

    private static IReadOnlyList<string> MarkStale(IReadOnlyList<ResourceLeaseRecord> leases, DateTimeOffset now)
    {
        var recovered = new List<string>();
        foreach (var lease in leases)
        {
            if (lease.Status is not ResourceLeaseStatus.Active and not ResourceLeaseStatus.Queued
                || lease.ExpiresAtUtc > now)
            {
                continue;
            }

            lease.Status = ResourceLeaseStatus.Expired;
            lease.UpdatedAtUtc = now;
            lease.ReleaseReason = StaleRecoveredReason;
            recovered.Add(lease.LeaseId);
        }

        return recovered;
    }

    private static ResourceWriteSet Normalize(ResourceWriteSet writeSet)
    {
        return new ResourceWriteSet
        {
            TaskIds = NormalizeTokens(writeSet.TaskIds, StringComparer.OrdinalIgnoreCase),
            Paths = NormalizePaths(writeSet.Paths),
            Modules = NormalizeTokens(writeSet.Modules, StringComparer.OrdinalIgnoreCase),
            TruthOperations = NormalizeTokens(writeSet.TruthOperations, StringComparer.Ordinal),
            TargetBranches = NormalizeTokens(writeSet.TargetBranches, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static ResourceWriteSet WithTaskId(ResourceWriteSet writeSet, string? taskId)
    {
        var normalizedTaskId = NormalizeOptional(taskId);
        if (string.IsNullOrWhiteSpace(normalizedTaskId))
        {
            return writeSet;
        }

        return new ResourceWriteSet
        {
            TaskIds = writeSet.TaskIds.Append(normalizedTaskId).ToArray(),
            Paths = writeSet.Paths,
            Modules = writeSet.Modules,
            TruthOperations = writeSet.TruthOperations,
            TargetBranches = writeSet.TargetBranches,
        };
    }

    private static IReadOnlyList<string> NormalizePaths(IReadOnlyList<string> paths)
    {
        return paths
            .Select(static path => path.Trim().Trim('`').Replace('\\', '/'))
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.TrimStart('.', '/').TrimEnd('/'))
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeTokens(
        IReadOnlyList<string> tokens,
        IEqualityComparer<string> comparer)
    {
        return tokens
            .Select(static token => token.Trim())
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(comparer)
            .OrderBy(static token => token, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ResourceLeaseConflict> DetectConflicts(
        ResourceWriteSet requested,
        ResourceLeaseRecord activeLease)
    {
        var conflicts = new List<ResourceLeaseConflict>();
        foreach (var taskId in requested.TaskIds)
        {
            if (activeLease.DeclaredWriteSet.TaskIds.Contains(taskId, StringComparer.OrdinalIgnoreCase))
            {
                conflicts.Add(new ResourceLeaseConflict(
                    ResourceLeaseConflictKind.SameTask,
                    activeLease.LeaseId,
                    taskId,
                    $"Task '{taskId}' is already leased by {activeLease.LeaseId}."));
            }
        }

        foreach (var path in requested.Paths)
        {
            var blockingPath = activeLease.DeclaredWriteSet.Paths.FirstOrDefault(existing => PathsOverlap(path, existing));
            if (!string.IsNullOrWhiteSpace(blockingPath))
            {
                conflicts.Add(new ResourceLeaseConflict(
                    ResourceLeaseConflictKind.SamePathWrite,
                    activeLease.LeaseId,
                    path,
                    $"Path '{path}' overlaps active lease {activeLease.LeaseId} path '{blockingPath}'."));
            }
        }

        foreach (var module in requested.Modules)
        {
            if (activeLease.DeclaredWriteSet.Modules.Contains(module, StringComparer.OrdinalIgnoreCase))
            {
                conflicts.Add(new ResourceLeaseConflict(
                    ResourceLeaseConflictKind.SameModule,
                    activeLease.LeaseId,
                    module,
                    $"Module '{module}' is already leased by {activeLease.LeaseId}."));
            }
        }

        foreach (var truthOperation in requested.TruthOperations)
        {
            if (activeLease.DeclaredWriteSet.TruthOperations.Contains(truthOperation, StringComparer.Ordinal))
            {
                conflicts.Add(new ResourceLeaseConflict(
                    ResourceLeaseConflictKind.SameTruthOperation,
                    activeLease.LeaseId,
                    truthOperation,
                    $"Truth operation '{truthOperation}' is already leased by {activeLease.LeaseId}."));
            }
        }

        foreach (var targetBranch in requested.TargetBranches)
        {
            if (activeLease.DeclaredWriteSet.TargetBranches.Contains(targetBranch, StringComparer.OrdinalIgnoreCase))
            {
                conflicts.Add(new ResourceLeaseConflict(
                    ResourceLeaseConflictKind.SameTargetBranch,
                    activeLease.LeaseId,
                    targetBranch,
                    $"Target branch '{targetBranch}' is already leased by {activeLease.LeaseId}."));
            }
        }

        return conflicts;
    }

    private static IReadOnlyList<string> ResolveActualWriteSetEscalations(
        ResourceWriteSet declared,
        ResourceWriteSet actual)
    {
        var reasons = new List<string>();
        foreach (var taskId in actual.TaskIds)
        {
            if (!declared.TaskIds.Contains(taskId, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add($"Actual task '{taskId}' was not in the declared write set.");
            }
        }

        foreach (var path in actual.Paths)
        {
            if (!declared.Paths.Any(declaredPath => IsPathWithinDeclaredScope(path, declaredPath)))
            {
                reasons.Add($"Actual path '{path}' was not in the declared write set.");
            }
        }

        foreach (var module in actual.Modules)
        {
            if (!declared.Modules.Any(declaredModule => ModuleWithinDeclaredScope(module, declaredModule)))
            {
                reasons.Add($"Actual module '{module}' was not in the declared write set.");
            }
        }

        foreach (var truthOperation in actual.TruthOperations)
        {
            if (!declared.TruthOperations.Contains(truthOperation, StringComparer.Ordinal))
            {
                reasons.Add($"Actual truth operation '{truthOperation}' was not in the declared write set.");
            }
        }

        foreach (var targetBranch in actual.TargetBranches)
        {
            if (!declared.TargetBranches.Contains(targetBranch, StringComparer.OrdinalIgnoreCase))
            {
                reasons.Add($"Actual target branch '{targetBranch}' was not in the declared write set.");
            }
        }

        return reasons;
    }

    private static bool PathsOverlap(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            || left.StartsWith($"{right}/", StringComparison.OrdinalIgnoreCase)
            || right.StartsWith($"{left}/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathWithinDeclaredScope(string actualPath, string declaredPath)
    {
        return string.Equals(actualPath, declaredPath, StringComparison.OrdinalIgnoreCase)
            || actualPath.StartsWith($"{declaredPath}/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ModuleWithinDeclaredScope(string actualModule, string declaredModule)
    {
        if (string.Equals(actualModule, declaredModule, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var actualLooksPathLike = actualModule.Contains('/', StringComparison.Ordinal);
        var declaredLooksPathLike = declaredModule.Contains('/', StringComparison.Ordinal);
        return actualLooksPathLike
            && declaredLooksPathLike
            && IsPathWithinDeclaredScope(actualModule, declaredModule);
    }

    private static ResourceLeaseStatus ResolveConflictStatus(
        ResourceLeaseConflictPolicy policy,
        bool truthOnlyConflicts)
    {
        return policy switch
        {
            ResourceLeaseConflictPolicy.Queue => ResourceLeaseStatus.Queued,
            ResourceLeaseConflictPolicy.SerializeTruthOperations when truthOnlyConflicts => ResourceLeaseStatus.Queued,
            _ => ResourceLeaseStatus.Stopped,
        };
    }

    private static string ResolveConflictResolution(
        ResourceLeaseStatus status,
        ResourceLeaseConflictPolicy policy,
        bool truthOnlyConflicts)
    {
        return status switch
        {
            ResourceLeaseStatus.Projected => "projected",
            ResourceLeaseStatus.Active => "active",
            ResourceLeaseStatus.Queued when policy == ResourceLeaseConflictPolicy.SerializeTruthOperations && truthOnlyConflicts => "serialized_truth_operation_queue",
            ResourceLeaseStatus.Queued => "queued",
            _ => "stopped_on_conflict",
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed class ResourceLeaseSnapshot
{
    public string Schema { get; init; } = "carves.resource_lease_snapshot.v0.98-rc.p7";

    public IReadOnlyList<ResourceLeaseRecord> Leases { get; set; } = [];
}

public sealed class ResourceLeaseRecord
{
    public string Schema { get; init; } = "carves.resource_lease.v0.98-rc.p7";

    public string LeaseId { get; init; } = string.Empty;

    public string? WorkOrderId { get; init; }

    public string? ParentWorkOrderId { get; init; }

    public string? TaskGraphId { get; init; }

    public string? TaskId { get; init; }

    public ResourceWriteSet DeclaredWriteSet { get; init; } = new();

    public ResourceWriteSet ActualWriteSet { get; set; } = new();

    public ResourceLeaseStatus Status { get; set; } = ResourceLeaseStatus.Active;

    public ResourceLeaseConflictPolicy ConflictPolicy { get; init; } = ResourceLeaseConflictPolicy.Stop;

    public string ConflictResolution { get; set; } = "active";

    public IReadOnlyList<string> ConflictReasons { get; set; } = [];

    public IReadOnlyList<string> StopReasons { get; set; } = [];

    public IReadOnlyList<string> BlockingLeaseIds { get; init; } = [];

    public DateTimeOffset AcquiredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAtUtc { get; init; } = DateTimeOffset.UtcNow.AddMinutes(30);

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReleasedAtUtc { get; set; }

    public string? ReleaseReason { get; set; }
}

public sealed class ResourceWriteSet
{
    public IReadOnlyList<string> TaskIds { get; init; } = [];

    public IReadOnlyList<string> Paths { get; init; } = [];

    public IReadOnlyList<string> Modules { get; init; } = [];

    public IReadOnlyList<string> TruthOperations { get; init; } = [];

    public IReadOnlyList<string> TargetBranches { get; init; } = [];
}

public enum ResourceLeaseStatus
{
    Projected,
    Active,
    Queued,
    Released,
    Expired,
    EscalationRequired,
    Stopped,
}

public enum ResourceLeaseConflictPolicy
{
    Stop,
    Queue,
    SerializeTruthOperations,
}

public enum ResourceLeaseActualWriteSetPolicy
{
    Stop,
    RequestEscalation,
}

public enum ResourceLeaseConflictKind
{
    SameTask,
    SamePathWrite,
    SameModule,
    SameTruthOperation,
    SameTargetBranch,
}

public sealed record ResourceLeaseRequest
{
    public string? WorkOrderId { get; init; }

    public string? ParentWorkOrderId { get; init; }

    public string? TaskGraphId { get; init; }

    public string? TaskId { get; init; }

    public ResourceWriteSet DeclaredWriteSet { get; init; } = new();

    public ResourceLeaseConflictPolicy ConflictPolicy { get; init; } = ResourceLeaseConflictPolicy.Stop;

    public DateTimeOffset? Now { get; init; }

    public DateTimeOffset? ValidUntil { get; init; }
}

public sealed record ResourceLeaseIssueResult(
    bool Acquired,
    bool Queued,
    ResourceLeaseRecord Lease,
    IReadOnlyList<ResourceLeaseConflict> Conflicts,
    IReadOnlyList<string> RecoveredLeaseIds,
    string Summary);

public sealed record ResourceLeaseConflict(
    ResourceLeaseConflictKind Kind,
    string BlockingLeaseId,
    string Resource,
    string Reason);

public sealed record ResourceLeaseReconcileResult(
    string LeaseId,
    bool WithinDeclaredWriteSet,
    bool EscalationRequired,
    ResourceLeaseRecord? Lease,
    IReadOnlyList<string> StopReasons,
    IReadOnlyList<string> EscalationReasons,
    string Summary)
{
    public static ResourceLeaseReconcileResult Block(
        string leaseId,
        IReadOnlyList<string> stopReasons,
        string summary)
    {
        return new ResourceLeaseReconcileResult(
            leaseId,
            false,
            false,
            null,
            stopReasons,
            [],
            summary);
    }
}

public sealed record ResourceLeaseReleaseResult(bool Released, string LeaseId, string Summary);

public sealed record ResourceLeaseRecoveryResult(IReadOnlyList<string> RecoveredLeaseIds, string Summary);
