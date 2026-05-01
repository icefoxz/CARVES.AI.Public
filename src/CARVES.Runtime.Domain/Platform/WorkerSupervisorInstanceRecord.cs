using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Platform;

public sealed class WorkerSupervisorInstanceRecord
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string WorkerInstanceId { get; init; } = $"worker-instance-{Guid.NewGuid():N}";

    public string RepoId { get; init; } = string.Empty;

    public string WorkerIdentity { get; init; } = string.Empty;

    public WorkerSupervisorOwnershipMode OwnershipMode { get; set; } = WorkerSupervisorOwnershipMode.HostOwned;

    public WorkerSupervisorInstanceState State { get; set; } = WorkerSupervisorInstanceState.Requested;

    public string? HostSessionId { get; set; }

    public string? ActorSessionId { get; set; }

    public string? ProviderProfile { get; set; }

    public string? CapabilityProfile { get; set; }

    public string? ScheduleBinding { get; set; }

    public int? ProcessId { get; set; }

    public DateTimeOffset? ProcessStartedAt { get; set; }

    public string? HostProcessHandleId { get; set; }

    public string? HostProcessHandleOwnerSessionId { get; set; }

    public DateTimeOffset? HostProcessHandleAcquiredAt { get; set; }

    public string? HostProcessStdoutLogPath { get; set; }

    public string? HostProcessStderrLogPath { get; set; }

    public string LaunchTokenId { get; set; } = string.Empty;

    public string LaunchTokenHash { get; set; } = string.Empty;

    public DateTimeOffset LaunchTokenIssuedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LaunchTokenExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(10);

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public string LastReason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void MarkState(WorkerSupervisorInstanceState state, string reason)
    {
        State = state;
        LastReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordProcess(int processId, DateTimeOffset processStartedAt, string reason)
    {
        ProcessId = processId;
        ProcessStartedAt = processStartedAt;
        LastReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordHostProcessHandle(
        string hostProcessHandleId,
        string hostProcessHandleOwnerSessionId,
        string? stdoutLogPath,
        string? stderrLogPath,
        string reason)
    {
        HostProcessHandleId = hostProcessHandleId;
        HostProcessHandleOwnerSessionId = hostProcessHandleOwnerSessionId;
        HostProcessHandleAcquiredAt = DateTimeOffset.UtcNow;
        HostProcessStdoutLogPath = stdoutLogPath;
        HostProcessStderrLogPath = stderrLogPath;
        LastReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordHeartbeat(DateTimeOffset heartbeatAt, string reason)
    {
        LastHeartbeatAt = heartbeatAt;
        LastReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
