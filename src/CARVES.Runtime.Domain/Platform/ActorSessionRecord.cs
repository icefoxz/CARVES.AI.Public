using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Platform;

public sealed class ActorSessionRecord
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string ActorSessionId { get; init; } = $"actor-session-{Guid.NewGuid():N}";

    public ActorSessionKind Kind { get; init; } = ActorSessionKind.Operator;

    public string ActorIdentity { get; init; } = string.Empty;

    public string RepoId { get; init; } = string.Empty;

    public string? RuntimeSessionId { get; set; }

    public int? ProcessId { get; set; }

    public DateTimeOffset? ProcessStartedAt { get; set; }

    public ActorSessionState State { get; set; } = ActorSessionState.Active;

    public ActorSessionRegistrationMode RegistrationMode { get; set; } = ActorSessionRegistrationMode.Manual;

    public string? WorkerInstanceId { get; set; }

    public string? SupervisorLaunchTokenId { get; set; }

    public string? ProviderProfile { get; set; }

    public string? CapabilityProfile { get; set; }

    public string? SessionScope { get; set; }

    public string? BudgetProfile { get; set; }

    public string? ScheduleBinding { get; set; }

    public string? LastContextReceipt { get; set; }

    public bool LeaseEligible { get; set; }

    public string? HealthPosture { get; set; }

    public string? LastOperationClass { get; set; }

    public string? LastOperation { get; set; }

    public string? CurrentTaskId { get; set; }

    public string? CurrentRunId { get; set; }

    public string? LastPermissionRequestId { get; set; }

    public OwnershipScope? CurrentOwnershipScope { get; set; }

    public string? CurrentOwnershipTargetId { get; set; }

    public string LastReason { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch(
        ActorSessionState state,
        string reason,
        string? runtimeSessionId = null,
        string? operationClass = null,
        string? operation = null,
        string? taskId = null,
        string? runId = null,
        string? permissionRequestId = null)
    {
        State = state;
        LastReason = reason;
        RuntimeSessionId = runtimeSessionId ?? RuntimeSessionId;
        LastOperationClass = operationClass ?? LastOperationClass;
        LastOperation = operation ?? LastOperation;
        CurrentTaskId = taskId ?? CurrentTaskId;
        CurrentRunId = runId ?? CurrentRunId;
        LastPermissionRequestId = permissionRequestId ?? LastPermissionRequestId;
        LastSeenAt = DateTimeOffset.UtcNow;
        UpdatedAt = LastSeenAt;
    }

    public void RecordRegistrationMetadata(
        string? providerProfile = null,
        string? capabilityProfile = null,
        string? sessionScope = null,
        string? budgetProfile = null,
        string? scheduleBinding = null,
        string? lastContextReceipt = null,
        bool? leaseEligible = null,
        string? healthPosture = null,
        int? processId = null,
        DateTimeOffset? processStartedAt = null,
        ActorSessionRegistrationMode? registrationMode = null,
        string? workerInstanceId = null,
        string? supervisorLaunchTokenId = null)
    {
        if (processId is not null)
        {
            ProcessId = processId;
            ProcessStartedAt = processStartedAt;
        }

        if (registrationMode is ActorSessionRegistrationMode.Manual)
        {
            RegistrationMode = ActorSessionRegistrationMode.Manual;
            WorkerInstanceId = null;
            SupervisorLaunchTokenId = null;
        }
        else
        {
            RegistrationMode = registrationMode ?? RegistrationMode;
            WorkerInstanceId = workerInstanceId ?? WorkerInstanceId;
            SupervisorLaunchTokenId = supervisorLaunchTokenId ?? SupervisorLaunchTokenId;
        }
        ProviderProfile = providerProfile ?? ProviderProfile;
        CapabilityProfile = capabilityProfile ?? CapabilityProfile;
        SessionScope = sessionScope ?? SessionScope;
        BudgetProfile = budgetProfile ?? BudgetProfile;
        ScheduleBinding = scheduleBinding ?? ScheduleBinding;
        LastContextReceipt = lastContextReceipt ?? LastContextReceipt;
        LeaseEligible = leaseEligible ?? LeaseEligible;
        HealthPosture = healthPosture ?? HealthPosture;
        LastSeenAt = DateTimeOffset.UtcNow;
        UpdatedAt = LastSeenAt;
    }

    public void RecordOwnership(OwnershipScope scope, string targetId, string reason)
    {
        CurrentOwnershipScope = scope;
        CurrentOwnershipTargetId = targetId;
        LastReason = reason;
        LastSeenAt = DateTimeOffset.UtcNow;
        UpdatedAt = LastSeenAt;
    }

    public void ClearOwnership(string reason)
    {
        CurrentOwnershipScope = null;
        CurrentOwnershipTargetId = null;
        LastReason = reason;
        LastSeenAt = DateTimeOffset.UtcNow;
        UpdatedAt = LastSeenAt;
    }

    public void ClearCurrentWork(ActorSessionState state, string reason)
    {
        State = state;
        CurrentTaskId = null;
        CurrentRunId = null;
        LastPermissionRequestId = null;
        CurrentOwnershipScope = null;
        CurrentOwnershipTargetId = null;
        LastReason = reason;
        LastSeenAt = DateTimeOffset.UtcNow;
        UpdatedAt = LastSeenAt;
    }
}
