using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class WorkerSupervisorStateService
{
    private static readonly TimeSpan DefaultLaunchTokenTtl = TimeSpan.FromMinutes(10);

    private readonly IWorkerSupervisorStateRepository repository;

    public WorkerSupervisorStateService(IWorkerSupervisorStateRepository repository)
    {
        this.repository = repository;
    }

    public WorkerSupervisorPendingLaunch RegisterPendingLaunch(
        string repoId,
        string workerIdentity,
        string reason,
        string? workerInstanceId = null,
        string? hostSessionId = null,
        string? actorSessionId = null,
        string? providerProfile = null,
        string? capabilityProfile = null,
        string? scheduleBinding = null,
        TimeSpan? launchTokenTtl = null)
    {
        var now = DateTimeOffset.UtcNow;
        var token = IssueLaunchToken(now, launchTokenTtl ?? DefaultLaunchTokenTtl);
        var resolvedWorkerInstanceId = string.IsNullOrWhiteSpace(workerInstanceId)
            ? $"worker-instance-{Guid.NewGuid():N}"
            : workerInstanceId.Trim();
        var snapshot = repository.Load();
        var existing = snapshot.Entries.FirstOrDefault(item =>
            string.Equals(item.WorkerInstanceId, resolvedWorkerInstanceId, StringComparison.Ordinal));
        if (existing is not null)
        {
            throw new InvalidOperationException($"Worker supervisor instance '{resolvedWorkerInstanceId}' already exists with state {existing.State}; archive or replace it through a governed supervisor lifecycle command before reusing worker_instance_id.");
        }

        var entries = snapshot.Entries.ToList();
        var record = new WorkerSupervisorInstanceRecord
        {
            WorkerInstanceId = resolvedWorkerInstanceId,
            RepoId = repoId,
            WorkerIdentity = workerIdentity,
            OwnershipMode = WorkerSupervisorOwnershipMode.HostOwned,
            State = WorkerSupervisorInstanceState.Requested,
            HostSessionId = hostSessionId,
            ActorSessionId = actorSessionId,
            ProviderProfile = providerProfile,
            CapabilityProfile = capabilityProfile,
            ScheduleBinding = scheduleBinding,
            LaunchTokenId = token.TokenId,
            LaunchTokenHash = token.TokenHash,
            LaunchTokenIssuedAt = token.IssuedAt,
            LaunchTokenExpiresAt = token.ExpiresAt,
            LastReason = reason,
            CreatedAt = now,
            UpdatedAt = now,
        };
        entries.Add(record);
        repository.Save(new WorkerSupervisorStateSnapshot
        {
            Entries = entries
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
                .ToArray(),
            ArchivedEntries = snapshot.ArchivedEntries,
            UpdatedAt = now,
        });

        return new WorkerSupervisorPendingLaunch(
            record.WorkerInstanceId,
            record.RepoId,
            record.WorkerIdentity,
            token.TokenId,
            token.Token,
            token.ExpiresAt,
            record);
    }

    public bool VerifyLaunchToken(WorkerSupervisorInstanceRecord record, string launchToken)
    {
        if (string.IsNullOrWhiteSpace(record.LaunchTokenHash)
            || string.IsNullOrWhiteSpace(launchToken)
            || DateTimeOffset.UtcNow > record.LaunchTokenExpiresAt)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(record.LaunchTokenHash),
            Encoding.UTF8.GetBytes(HashToken(launchToken)));
    }

    public WorkerSupervisorInstanceRecord? TryGet(string workerInstanceId)
    {
        if (string.IsNullOrWhiteSpace(workerInstanceId))
        {
            return null;
        }

        return repository.Load().Entries.FirstOrDefault(item =>
            string.Equals(item.WorkerInstanceId, workerInstanceId.Trim(), StringComparison.Ordinal));
    }

    public IReadOnlyList<WorkerSupervisorInstanceRecord> List(string? repoId = null)
    {
        var entries = repository.Load().Entries;
        if (string.IsNullOrWhiteSpace(repoId))
        {
            return entries
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
                .ToArray();
        }

        return entries
            .Where(item => string.Equals(item.RepoId, repoId.Trim(), StringComparison.Ordinal))
            .OrderBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<WorkerSupervisorArchivedInstanceRecord> ListArchived(string? repoId = null)
    {
        var entries = repository.Load().ArchivedEntries;
        if (string.IsNullOrWhiteSpace(repoId))
        {
            return entries
                .OrderByDescending(item => item.ArchivedAt)
                .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
                .ToArray();
        }

        return entries
            .Where(item => string.Equals(item.RepoId, repoId.Trim(), StringComparison.Ordinal))
            .OrderByDescending(item => item.ArchivedAt)
            .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
            .ToArray();
    }

    public WorkerSupervisorInstanceRecord? TryGetByActorSessionId(string actorSessionId)
    {
        if (string.IsNullOrWhiteSpace(actorSessionId))
        {
            return null;
        }

        return repository.Load().Entries.FirstOrDefault(item =>
            string.Equals(item.ActorSessionId, actorSessionId.Trim(), StringComparison.Ordinal));
    }

    public WorkerSupervisorInstanceRecord BindActorSession(
        string workerInstanceId,
        string actorSessionId,
        int processId,
        DateTimeOffset processStartedAt,
        string reason)
    {
        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var record = entries.FirstOrDefault(item =>
            string.Equals(item.WorkerInstanceId, workerInstanceId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Worker supervisor instance '{workerInstanceId}' was not found.");
        record.ActorSessionId = actorSessionId;
        record.RecordProcess(processId, processStartedAt, reason);
        record.MarkState(WorkerSupervisorInstanceState.Running, reason);
        repository.Save(new WorkerSupervisorStateSnapshot
        {
            Entries = entries
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
                .ToArray(),
            ArchivedEntries = snapshot.ArchivedEntries,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        return record;
    }

    public WorkerSupervisorInstanceRecord? MarkActorSessionLost(
        string actorSessionId,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(actorSessionId))
        {
            return null;
        }

        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var record = entries.FirstOrDefault(item =>
            string.Equals(item.ActorSessionId, actorSessionId.Trim(), StringComparison.Ordinal));
        if (record is null)
        {
            return null;
        }

        record.MarkState(WorkerSupervisorInstanceState.Lost, reason);
        repository.Save(new WorkerSupervisorStateSnapshot
        {
            Entries = entries
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
                .ToArray(),
            ArchivedEntries = snapshot.ArchivedEntries,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        return record;
    }

    public WorkerSupervisorInstanceRecord? MarkActorSessionStopped(
        string actorSessionId,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(actorSessionId))
        {
            return null;
        }

        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var record = entries.FirstOrDefault(item =>
            string.Equals(item.ActorSessionId, actorSessionId.Trim(), StringComparison.Ordinal));
        if (record is null)
        {
            return null;
        }

        record.MarkState(WorkerSupervisorInstanceState.Stopped, reason);
        repository.Save(new WorkerSupervisorStateSnapshot
        {
            Entries = entries
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
                .ToArray(),
            ArchivedEntries = snapshot.ArchivedEntries,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        return record;
    }

    public WorkerSupervisorInstanceRecord RecordHostProcessHandle(
        string workerInstanceId,
        string hostProcessHandleId,
        string hostProcessHandleOwnerSessionId,
        string reason,
        string? stdoutLogPath = null,
        string? stderrLogPath = null)
    {
        if (string.IsNullOrWhiteSpace(workerInstanceId))
        {
            throw new InvalidOperationException("Worker supervisor process handle recording requires worker_instance_id.");
        }

        if (string.IsNullOrWhiteSpace(hostProcessHandleId))
        {
            throw new InvalidOperationException("Worker supervisor process handle recording requires host_process_handle_id.");
        }

        if (string.IsNullOrWhiteSpace(hostProcessHandleOwnerSessionId))
        {
            throw new InvalidOperationException("Worker supervisor process handle recording requires host_process_handle_owner_session_id.");
        }

        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var record = entries.FirstOrDefault(item =>
            string.Equals(item.WorkerInstanceId, workerInstanceId.Trim(), StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Worker supervisor instance '{workerInstanceId.Trim()}' was not found.");

        if (record.State is not WorkerSupervisorInstanceState.Running)
        {
            throw new InvalidOperationException($"Worker supervisor instance '{record.WorkerInstanceId}' cannot hold a Host process handle while it is {record.State}.");
        }

        if (!record.ProcessId.HasValue || !record.ProcessStartedAt.HasValue)
        {
            throw new InvalidOperationException($"Worker supervisor instance '{record.WorkerInstanceId}' cannot hold a Host process handle before process identity is recorded.");
        }

        if (!string.IsNullOrWhiteSpace(record.HostSessionId)
            && !string.Equals(record.HostSessionId, hostProcessHandleOwnerSessionId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Worker supervisor instance '{record.WorkerInstanceId}' can only hold a Host process handle owned by host session '{record.HostSessionId}'.");
        }

        record.RecordHostProcessHandle(
            hostProcessHandleId.Trim(),
            hostProcessHandleOwnerSessionId.Trim(),
            string.IsNullOrWhiteSpace(stdoutLogPath) ? null : stdoutLogPath.Trim(),
            string.IsNullOrWhiteSpace(stderrLogPath) ? null : stderrLogPath.Trim(),
            reason);
        repository.Save(new WorkerSupervisorStateSnapshot
        {
            Entries = entries
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
                .ToArray(),
            ArchivedEntries = snapshot.ArchivedEntries,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        return record;
    }

    public WorkerSupervisorArchiveOperation Archive(
        string workerInstanceId,
        string reason,
        bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(workerInstanceId))
        {
            throw new InvalidOperationException("Worker supervisor archive requires worker_instance_id.");
        }

        var snapshot = repository.Load();
        var entries = snapshot.Entries.ToList();
        var index = entries.FindIndex(item =>
            string.Equals(item.WorkerInstanceId, workerInstanceId.Trim(), StringComparison.Ordinal));
        if (index < 0)
        {
            throw new InvalidOperationException($"Worker supervisor instance '{workerInstanceId.Trim()}' was not found.");
        }

        var record = entries[index];
        if (!CanArchive(record.State))
        {
            throw new InvalidOperationException($"Worker supervisor instance '{record.WorkerInstanceId}' cannot be archived while it is {record.State}; stop or reconcile the worker before archiving.");
        }

        if (dryRun)
        {
            return new WorkerSupervisorArchiveOperation(record, null);
        }

        var archivedAt = DateTimeOffset.UtcNow;
        var tombstone = new WorkerSupervisorArchivedInstanceRecord
        {
            WorkerInstanceId = record.WorkerInstanceId,
            RepoId = record.RepoId,
            WorkerIdentity = record.WorkerIdentity,
            PreviousState = record.State,
            ActorSessionId = record.ActorSessionId,
            ScheduleBinding = record.ScheduleBinding,
            Reason = reason,
            ArchivedAt = archivedAt,
            OriginalCreatedAt = record.CreatedAt,
            OriginalUpdatedAt = record.UpdatedAt,
        };
        var archivedEntries = snapshot.ArchivedEntries
            .Append(tombstone)
            .OrderByDescending(item => item.ArchivedAt)
            .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
            .ToArray();
        entries.RemoveAt(index);
        repository.Save(new WorkerSupervisorStateSnapshot
        {
            Entries = entries
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ThenBy(item => item.WorkerInstanceId, StringComparer.Ordinal)
                .ToArray(),
            ArchivedEntries = archivedEntries,
            UpdatedAt = archivedAt,
        });

        return new WorkerSupervisorArchiveOperation(record, tombstone);
    }

    private static bool CanArchive(WorkerSupervisorInstanceState state)
    {
        return state is WorkerSupervisorInstanceState.Requested
            or WorkerSupervisorInstanceState.Lost
            or WorkerSupervisorInstanceState.Failed
            or WorkerSupervisorInstanceState.Stopped;
    }

    private static WorkerSupervisorLaunchToken IssueLaunchToken(DateTimeOffset issuedAt, TimeSpan ttl)
    {
        var tokenId = $"wlt-{Guid.NewGuid():N}";
        var token = $"{tokenId}.{CreateSecret()}";
        return new WorkerSupervisorLaunchToken(
            tokenId,
            token,
            HashToken(token),
            issuedAt,
            issuedAt.Add(ttl));
    }

    private static string CreateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record WorkerSupervisorArchiveOperation(
    WorkerSupervisorInstanceRecord Record,
    WorkerSupervisorArchivedInstanceRecord? Tombstone);

public sealed record WorkerSupervisorPendingLaunch(
    string WorkerInstanceId,
    string RepoId,
    string WorkerIdentity,
    string LaunchTokenId,
    string LaunchToken,
    DateTimeOffset LaunchTokenExpiresAt,
    WorkerSupervisorInstanceRecord Record);

public sealed record WorkerSupervisorLaunchToken(
    string TokenId,
    string Token,
    string TokenHash,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
