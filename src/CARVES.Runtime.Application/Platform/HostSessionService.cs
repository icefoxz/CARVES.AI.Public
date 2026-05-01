using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class HostSessionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;

    public HostSessionService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public string SessionPath => paths.PlatformHostSessionLiveStateFile;

    public static string BuildSessionId(string hostId, DateTimeOffset startedAt)
    {
        return $"host-{hostId}-{startedAt:yyyyMMddHHmmss}";
    }

    public HostSessionRecord? Load()
    {
        var path = File.Exists(SessionPath)
            ? SessionPath
            : Path.Combine(paths.PlatformRuntimeRoot, "host_session.json");

        return File.Exists(path)
            ? JsonSerializer.Deserialize<HostSessionRecord>(SharedFileAccess.ReadAllText(path), JsonOptions)
            : null;
    }

    public HostSessionRecord Ensure(string sessionId, string hostId, string repoRoot, string? baseUrl, string stage, DateTimeOffset startedAt)
    {
        var existing = Load();
        if (existing is not null
            && string.Equals(existing.SessionId, sessionId, StringComparison.Ordinal)
            && existing.Status == HostSessionStatus.Active)
        {
            return existing;
        }

        var created = new HostSessionRecord
        {
            SessionId = sessionId,
            HostId = hostId,
            RepoRoot = repoRoot,
            BaseUrl = baseUrl,
            Stage = stage,
            Status = HostSessionStatus.Active,
            ControlState = HostControlState.Running,
            StartedAt = startedAt,
            LastControlAction = HostControlAction.Started,
            LastControlReason = "Resident host session started.",
            LastControlAt = startedAt,
            AttachedRepos = existing?.AttachedRepos ?? Array.Empty<HostSessionRepoBinding>(),
        };
        Save(created);
        return created;
    }

    public HostSessionRecord BindRepo(string sessionId, string repoId, string repoPath, string clientRepoRoot, string attachMode, string runtimeHealth)
    {
        var session = Load() ?? throw new InvalidOperationException("Host session does not exist.");
        if (!string.Equals(session.SessionId, sessionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Host session mismatch. Expected '{session.SessionId}', received '{sessionId}'.");
        }

        var binding = new HostSessionRepoBinding
        {
            RepoId = repoId,
            RepoPath = Path.GetFullPath(repoPath),
            ClientRepoRoot = Path.GetFullPath(clientRepoRoot),
            AttachMode = attachMode,
            RuntimeHealth = runtimeHealth,
            AttachedAt = DateTimeOffset.UtcNow,
        };

        var updated = session with
        {
            AttachedRepos = session.AttachedRepos
                .Where(item => !string.Equals(item.RepoId, repoId, StringComparison.Ordinal))
                .Append(binding)
                .OrderBy(item => item.RepoId, StringComparer.Ordinal)
                .ToArray(),
        };
        Save(updated);
        return updated;
    }

    public HostSessionRecord Stop(string stopReason)
    {
        var session = Load() ?? throw new InvalidOperationException("Host session does not exist.");
        if (session.Status == HostSessionStatus.Stopped && !string.IsNullOrWhiteSpace(session.StopReason))
        {
            return session;
        }

        var updated = session with
        {
            Status = HostSessionStatus.Stopped,
            ControlState = HostControlState.Stopped,
            EndedAt = DateTimeOffset.UtcNow,
            StopReason = stopReason,
            LastControlAction = HostControlAction.TerminateRequested,
            LastControlReason = stopReason,
            LastControlAt = DateTimeOffset.UtcNow,
        };
        Save(updated);
        return updated;
    }

    public HostSessionRecord Pause(string reason)
    {
        var session = Load() ?? throw new InvalidOperationException("Host session does not exist.");
        var updated = session with
        {
            ControlState = HostControlState.Paused,
            LastControlAction = HostControlAction.PauseRequested,
            LastControlReason = reason,
            LastControlAt = DateTimeOffset.UtcNow,
        };
        Save(updated);
        return updated;
    }

    public HostSessionRecord Resume(string reason)
    {
        var session = Load() ?? throw new InvalidOperationException("Host session does not exist.");
        var updated = session with
        {
            Status = HostSessionStatus.Active,
            ControlState = HostControlState.Running,
            EndedAt = null,
            StopReason = null,
            LastControlAction = HostControlAction.ResumeRequested,
            LastControlReason = reason,
            LastControlAt = DateTimeOffset.UtcNow,
        };
        Save(updated);
        return updated;
    }

    private void Save(HostSessionRecord session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
        WriteAtomically(SessionPath, JsonSerializer.Serialize(session, JsonOptions));
    }

    private static void WriteAtomically(string path, string content)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"Path '{path}' has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporaryPath, content);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
