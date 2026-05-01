using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Infrastructure.ControlPlane;

public sealed class ControlPlaneLockService : IControlPlaneLockService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly string repoRoot;

    public ControlPlaneLockService(string repoRoot)
    {
        this.repoRoot = repoRoot;
    }

    public ControlPlaneLockHandle Acquire(string scope, TimeSpan? timeout = null, ControlPlaneLockOptions? options = null)
    {
        var wait = timeout ?? TimeSpan.FromSeconds(10);
        if (TryAcquire(scope, wait, out var handle, options))
        {
            return handle!;
        }

        throw new TimeoutException(BuildTimeoutMessage(scope, wait, InspectLease(scope)));
    }

    public bool TryAcquire(string scope, TimeSpan timeout, out ControlPlaneLockHandle? handle, ControlPlaneLockOptions? options = null)
    {
        options ??= new ControlPlaneLockOptions();
        var mutex = new Mutex(false, BuildMutexName(scope));
        var acquired = false;
        var leasePath = ControlPlaneLockLeasePaths.GetLeasePath(repoRoot, scope);
        DeleteStaleLeaseIfPresent(scope, leasePath);

        try
        {
            acquired = mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }

        if (!acquired)
        {
            DeleteStaleLeaseIfPresent(scope, leasePath);
            mutex.Dispose();
            handle = null;
            return false;
        }

        var lease = PersistLease(scope, leasePath, options);
        handle = new ControlPlaneLockHandle(scope, () =>
        {
            try
            {
                DeleteOwnedLease(leasePath, lease.OwnerId);
            }
            finally
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
        }, lease);

        return true;
    }

    public ControlPlaneLockLeaseSnapshot? InspectLease(string scope)
    {
        var leasePath = ControlPlaneLockLeasePaths.GetLeasePath(repoRoot, scope);
        var leaseRecord = ReadLeaseRecord(leasePath);
        if (leaseRecord is null)
        {
            return null;
        }

        return ToSnapshot(scope, leasePath, leaseRecord);
    }

    private string BuildMutexName(string scope)
    {
        var payload = Encoding.UTF8.GetBytes($"{repoRoot}|{scope}");
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        return $"carves-runtime-{hash}";
    }

    private static string BuildTimeoutMessage(string scope, TimeSpan timeout, ControlPlaneLockLeaseSnapshot? lease)
    {
        if (lease is null)
        {
            return $"Timed out after {timeout.TotalSeconds:0.#}s while waiting for control-plane lock '{scope}'. The lock is currently occupied. No holder metadata was available, so poll the matching ownership or lease surface and retry after release.";
        }

        var taskSegment = string.IsNullOrWhiteSpace(lease.TaskId)
            ? string.Empty
            : $"; task={lease.TaskId}";
        var workspaceSegment = string.IsNullOrWhiteSpace(lease.WorkspacePath)
            ? string.Empty
            : $"; workspace={lease.WorkspacePath}";
        return $"Timed out after {timeout.TotalSeconds:0.#}s while waiting for control-plane lock '{scope}'. The lock is currently occupied by {lease.OwnerId}; poll the matching ownership or lease surface and retry after release. holder={lease.OwnerId}; resource={lease.Resource ?? "(unknown)"}; operation={lease.Operation ?? "(unknown)"}; state={lease.State}{taskSegment}{workspaceSegment}; last_heartbeat={lease.LastHeartbeat:O}; ttl={lease.Ttl?.TotalSeconds:0.#}s; lease={lease.LeasePath}.";
    }

    private ControlPlaneLockLeaseSnapshot PersistLease(string scope, string leasePath, ControlPlaneLockOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(leasePath)!);
        var now = DateTimeOffset.UtcNow;
        var ownerId = $"{Environment.ProcessId}:{Environment.ProcessPath ?? AppContext.BaseDirectory}";
        var record = new LeaseRecord
        {
            Scope = scope,
            LeaseId = $"cpl-{Guid.NewGuid():N}",
            Resource = options.Resource,
            Operation = options.Operation,
            Mode = string.IsNullOrWhiteSpace(options.Mode) ? "write" : options.Mode.Trim(),
            OwnerId = ownerId,
            OwnerProcessId = Environment.ProcessId,
            OwnerProcessName = Environment.ProcessPath is { Length: > 0 } processPath ? Path.GetFileName(processPath) : "dotnet",
            TaskId = NormalizeOptional(options.TaskId),
            WorkspacePath = NormalizePath(options.WorkspacePath),
            AllowedWritablePaths = NormalizeList(options.AllowedWritablePaths),
            AllowedOperationClasses = NormalizeList(options.AllowedOperationClasses),
            AllowedToolsOrAdapters = NormalizeList(options.AllowedToolsOrAdapters),
            CleanupPosture = string.IsNullOrWhiteSpace(options.CleanupPosture)
                ? ControlPlaneResidueContract.NoCleanupRequiredPosture
                : options.CleanupPosture.Trim(),
            AcquiredAt = now,
            LastHeartbeat = now,
            TtlSeconds = Math.Max(1, options.LeaseTtl.TotalSeconds),
            ExpiresAt = now.AddSeconds(Math.Max(1, options.LeaseTtl.TotalSeconds)),
        };

        WriteLeaseRecord(leasePath, record);
        return ToSnapshot(scope, leasePath, record);
    }

    private void DeleteStaleLeaseIfPresent(string scope, string leasePath)
    {
        var record = ReadLeaseRecord(leasePath);
        if (record is null)
        {
            return;
        }

        var snapshot = ToSnapshot(scope, leasePath, record);
        if (!string.Equals(snapshot.State, "stale", StringComparison.Ordinal))
        {
            return;
        }

        TryDeleteLeaseFile(leasePath);
    }

    private static void DeleteOwnedLease(string leasePath, string ownerId)
    {
        var record = ReadLeaseRecord(leasePath);
        if (record is null || !string.Equals(record.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return;
        }

        TryDeleteLeaseFile(leasePath);
    }

    private static void TryDeleteLeaseFile(string leasePath)
    {
        if (!File.Exists(leasePath))
        {
            return;
        }

        try
        {
            File.Delete(leasePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static LeaseRecord? ReadLeaseRecord(string leasePath)
    {
        if (!File.Exists(leasePath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(leasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<LeaseRecord>(stream, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void WriteLeaseRecord(string leasePath, LeaseRecord record)
    {
        var directory = Path.GetDirectoryName(leasePath) ?? throw new InvalidOperationException($"Lease path '{leasePath}' has no parent directory.");
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(leasePath)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempPath, JsonSerializer.Serialize(record, JsonOptions));
        File.Move(tempPath, leasePath, overwrite: true);
    }

    private static ControlPlaneLockLeaseSnapshot ToSnapshot(string scope, string leasePath, LeaseRecord record)
    {
        var ownerAlive = record.OwnerProcessId is int ownerProcessId && IsProcessAlive(ownerProcessId);
        var lastHeartbeat = record.LastHeartbeat ?? record.AcquiredAt;
        var ttl = record.TtlSeconds > 0 ? TimeSpan.FromSeconds(record.TtlSeconds) : TimeSpan.FromMinutes(2);
        var heartbeatExpired = lastHeartbeat is not null && DateTimeOffset.UtcNow - lastHeartbeat.Value > ttl;
        var stale = !ownerAlive || heartbeatExpired;

        var summary = stale
            ? $"Stale lease recorded for {record.OwnerId}; resource={record.Resource ?? "(unknown)"}; operation={record.Operation ?? "(unknown)"}."
            : $"Active lease held by {record.OwnerId} for {record.Operation ?? "(unknown)"} on {record.Resource ?? "(unknown)"}";

        return new ControlPlaneLockLeaseSnapshot
        {
            Scope = scope,
            LeaseId = string.IsNullOrWhiteSpace(record.LeaseId)
                ? Path.GetFileNameWithoutExtension(leasePath)
                : record.LeaseId,
            LeasePath = leasePath.Replace('\\', '/'),
            State = stale ? "stale" : "active",
            Status = stale ? "stale" : "active",
            Resource = record.Resource,
            Operation = record.Operation,
            Mode = string.IsNullOrWhiteSpace(record.Mode) ? "write" : record.Mode,
            OwnerId = record.OwnerId ?? string.Empty,
            OwnerProcessId = record.OwnerProcessId,
            OwnerProcessName = record.OwnerProcessName,
            TaskId = NormalizeOptional(record.TaskId),
            WorkspacePath = NormalizePath(record.WorkspacePath),
            AllowedWritablePaths = NormalizeList(record.AllowedWritablePaths),
            AllowedOperationClasses = NormalizeList(record.AllowedOperationClasses),
            AllowedToolsOrAdapters = NormalizeList(record.AllowedToolsOrAdapters),
            CleanupPosture = string.IsNullOrWhiteSpace(record.CleanupPosture)
                ? ControlPlaneResidueContract.NoCleanupRequiredPosture
                : record.CleanupPosture.Trim(),
            AcquiredAt = record.AcquiredAt,
            LastHeartbeat = lastHeartbeat,
            ExpiresAt = record.ExpiresAt ?? lastHeartbeat?.Add(ttl),
            Ttl = ttl,
            Summary = summary,
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string? NormalizePath(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Replace('\\', '/').Trim();
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values)
    {
        return values is null
            ? Array.Empty<string>()
            : values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Replace('\\', '/').Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class LeaseRecord
    {
        public string Scope { get; init; } = string.Empty;

        public string LeaseId { get; init; } = string.Empty;

        public string? Resource { get; init; }

        public string? Operation { get; init; }

        public string Mode { get; init; } = "write";

        public string OwnerId { get; init; } = string.Empty;

        public int? OwnerProcessId { get; init; }

        public string? OwnerProcessName { get; init; }

        public string? TaskId { get; init; }

        public string? WorkspacePath { get; init; }

        public IReadOnlyList<string> AllowedWritablePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> AllowedOperationClasses { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> AllowedToolsOrAdapters { get; init; } = Array.Empty<string>();

        public string CleanupPosture { get; init; } = ControlPlaneResidueContract.NoCleanupRequiredPosture;

        public DateTimeOffset? AcquiredAt { get; init; }

        public DateTimeOffset? LastHeartbeat { get; init; }

        public double TtlSeconds { get; init; }

        public DateTimeOffset? ExpiresAt { get; init; }
    }
}
