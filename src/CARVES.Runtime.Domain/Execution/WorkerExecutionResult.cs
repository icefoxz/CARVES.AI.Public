using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed record WorkerExecutionResult
{
    public static WorkerExecutionResult None { get; } = Skipped(
        taskId: string.Empty,
        backendId: "none",
        providerId: "none",
        adapterId: "none",
        profile: WorkerExecutionProfile.UntrustedDefault,
        summary: "No worker execution result was recorded.",
        requestPreview: string.Empty,
        requestHash: string.Empty);

    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string RunId { get; init; } = $"worker-run-{Guid.NewGuid():N}";

    public string TaskId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string AdapterId { get; init; } = string.Empty;

    public string AdapterReason { get; init; } = string.Empty;

    public string? ProtocolFamily { get; init; }

    public string? RequestFamily { get; init; }

    public string ProfileId { get; init; } = string.Empty;

    public bool TrustedProfile { get; init; }

    public WorkerExecutionStatus Status { get; init; } = WorkerExecutionStatus.Skipped;

    public WorkerFailureKind FailureKind { get; init; } = WorkerFailureKind.None;

    public WorkerFailureLayer FailureLayer { get; init; } = WorkerFailureLayer.None;

    public bool Retryable { get; init; }

    public bool Configured { get; init; }

    public string Model { get; init; } = string.Empty;

    public string? RequestId { get; init; }

    public string? PriorThreadId { get; init; }

    public string? ThreadId { get; init; }

    public WorkerThreadContinuity ThreadContinuity { get; init; } = WorkerThreadContinuity.None;

    public string RequestPreview { get; init; } = string.Empty;

    public string RequestHash { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string? Rationale { get; init; }

    public string? FailureReason { get; init; }

    public string? TimeoutPhase { get; init; }

    public string? TimeoutEvidence { get; init; }

    public string? ResponsePreview { get; init; }

    public string? ResponseHash { get; init; }

    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ObservedChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<WorkerEvent> Events { get; init; } = Array.Empty<WorkerEvent>();

    public IReadOnlyList<WorkerPermissionRequest> PermissionRequests { get; init; } = Array.Empty<WorkerPermissionRequest>();

    public IReadOnlyList<CommandExecutionRecord> CommandTrace { get; init; } = Array.Empty<CommandExecutionRecord>();

    public WorkerCompletionClaim CompletionClaim { get; init; } = WorkerCompletionClaim.None;

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? ProviderStatusCode { get; init; }

    public long? ProviderLatencyMs { get; init; }

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool Succeeded => Status == WorkerExecutionStatus.Succeeded;

    public static WorkerExecutionResult Skipped(
        string taskId,
        string backendId,
        string providerId,
        string adapterId,
        WorkerExecutionProfile profile,
        string summary,
        string requestPreview,
        string requestHash)
    {
        return new WorkerExecutionResult
        {
            TaskId = taskId,
            BackendId = backendId,
            ProviderId = providerId,
            AdapterId = adapterId,
            AdapterReason = summary,
            ProfileId = profile.ProfileId,
            TrustedProfile = profile.Trusted,
            Status = WorkerExecutionStatus.Skipped,
            FailureKind = WorkerFailureKind.None,
            Retryable = false,
            Configured = false,
            Summary = summary,
            RequestPreview = requestPreview,
            RequestHash = requestHash,
        };
    }

    public static WorkerExecutionResult Blocked(
        string taskId,
        string backendId,
        string providerId,
        string adapterId,
        WorkerExecutionProfile profile,
        WorkerFailureKind failureKind,
        string failureReason,
        string requestPreview,
        string requestHash,
        WorkerFailureLayer? failureLayer = null,
        string? protocolFamily = null,
        string? requestFamily = null,
        IReadOnlyList<WorkerEvent>? events = null,
        IReadOnlyList<CommandExecutionRecord>? commandTrace = null)
    {
        return new WorkerExecutionResult
        {
            TaskId = taskId,
            BackendId = backendId,
            ProviderId = providerId,
            AdapterId = adapterId,
            AdapterReason = failureReason,
            ProtocolFamily = protocolFamily,
            RequestFamily = requestFamily,
            ProfileId = profile.ProfileId,
            TrustedProfile = profile.Trusted,
            Status = WorkerExecutionStatus.Blocked,
            FailureKind = failureKind,
            FailureLayer = failureLayer ?? MapDefaultFailureLayer(failureKind),
            FailureReason = failureReason,
            Summary = failureReason,
            Retryable = false,
            Configured = true,
            RequestPreview = requestPreview,
            RequestHash = requestHash,
            Events = events ?? Array.Empty<WorkerEvent>(),
            CommandTrace = commandTrace ?? Array.Empty<CommandExecutionRecord>(),
        };
    }

    private static WorkerFailureLayer MapDefaultFailureLayer(WorkerFailureKind failureKind)
    {
        return failureKind switch
        {
            WorkerFailureKind.EnvironmentBlocked or WorkerFailureKind.PolicyDenied or WorkerFailureKind.ApprovalRequired => WorkerFailureLayer.Environment,
            WorkerFailureKind.LaunchFailure or WorkerFailureKind.AttachFailure or WorkerFailureKind.WrapperFailure or WorkerFailureKind.ArtifactFailure => WorkerFailureLayer.Environment,
            WorkerFailureKind.Timeout or WorkerFailureKind.TransientInfra => WorkerFailureLayer.Transport,
            WorkerFailureKind.InvalidOutput or WorkerFailureKind.ContractFailure => WorkerFailureLayer.Protocol,
            WorkerFailureKind.TaskLogicFailed or WorkerFailureKind.BuildFailure or WorkerFailureKind.TestFailure or WorkerFailureKind.PatchFailure => WorkerFailureLayer.WorkerSemantic,
            _ => WorkerFailureLayer.None,
        };
    }
}

public sealed record WorkerCompletionClaim
{
    public static WorkerCompletionClaim None { get; } = new()
    {
        Required = false,
        Status = "not_required",
        Source = "none",
        Notes = ["No worker completion claim was required or recorded."],
    };

    public string SchemaVersion { get; init; } = "worker-completion-claim.v1";

    public bool Required { get; init; }

    public string Status { get; init; } = "not_required";

    public string Source { get; init; } = "worker_response";

    public string? PacketId { get; init; }

    public string? SourceExecutionPacketId { get; init; }

    public bool ClaimIsTruth { get; init; }

    public bool HostValidationRequired { get; init; } = true;

    public string PacketValidationStatus { get; init; } = "not_evaluated";

    public IReadOnlyList<string> PacketValidationBlockers { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PresentFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ContractItemsSatisfied { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredContractItems { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingContractItems { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TestsRun { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EvidencePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DisallowedChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ForbiddenVocabularyHits { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> KnownLimitations { get; init; } = Array.Empty<string>();

    public string NextRecommendation { get; init; } = string.Empty;

    public string? RawClaimPreview { get; init; }

    public string? RawClaimHash { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
