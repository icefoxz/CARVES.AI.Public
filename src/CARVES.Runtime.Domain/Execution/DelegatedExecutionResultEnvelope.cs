using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed record DelegatedExecutionResultEnvelope
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string TaskId { get; init; } = string.Empty;

    public bool Accepted { get; init; }

    public string Outcome { get; init; } = string.Empty;

    public string ActorKind { get; init; } = string.Empty;

    public string ActorIdentity { get; init; } = string.Empty;

    public bool ManualFallback { get; init; }

    public string? RuntimeSessionId { get; init; }

    public string TaskStatus { get; init; } = string.Empty;

    public string SessionStatus { get; init; } = string.Empty;

    public string? ProviderId { get; init; }

    public string? BackendId { get; init; }

    public string? ProfileId { get; init; }

    public string? RunId { get; init; }

    public string? ExecutionRunId { get; init; }

    public string? ResultEnvelopePath { get; init; }

    public bool HostResultIngestionAttempted { get; init; }

    public bool HostResultIngestionApplied { get; init; }

    public bool HostResultIngestionAlreadyApplied { get; init; }

    public string ResultSubmissionStatus { get; init; } = "not_attempted";

    public bool SafetyArtifactPresent { get; init; }

    public string SafetyGateStatus { get; init; } = "not_evaluated";

    public bool SafetyGateAllowed { get; init; }

    public IReadOnlyList<string> SafetyGateIssues { get; init; } = Array.Empty<string>();

    public string? ReviewSubmissionPath { get; init; }

    public string? EffectLedgerPath { get; init; }

    public string? ResultCommit { get; init; }

    public int? ExitCode { get; init; }

    public long? DurationMs { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string? FailureKind { get; init; }

    public bool Retryable { get; init; }

    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();

    public string NextAction { get; init; } = string.Empty;

    public IReadOnlyList<string> Guidance { get; init; } = Array.Empty<string>();

    public FallbackRunPacket? FallbackRunPacket { get; init; }

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    public static DelegatedExecutionResultEnvelope Rejected(
        string taskId,
        ActorSessionKind actorKind,
        string actorIdentity,
        string outcome,
        string summary,
        string nextAction,
        params IReadOnlyList<string> guidance)
    {
        return new DelegatedExecutionResultEnvelope
        {
            TaskId = taskId,
            Accepted = false,
            Outcome = outcome,
            ActorKind = actorKind.ToString(),
            ActorIdentity = actorIdentity,
            Summary = summary,
            NextAction = nextAction,
            Guidance = guidance,
            TaskStatus = "unchanged",
            SessionStatus = "unchanged",
        };
    }

    public static DelegatedExecutionResultEnvelope RejectedWithFallbackPacket(
        string taskId,
        ActorSessionKind actorKind,
        string actorIdentity,
        string outcome,
        string summary,
        string nextAction,
        bool manualFallback,
        FallbackRunPacket? fallbackRunPacket,
        params IReadOnlyList<string> guidance)
    {
        return new DelegatedExecutionResultEnvelope
        {
            TaskId = taskId,
            Accepted = false,
            Outcome = outcome,
            ActorKind = actorKind.ToString(),
            ActorIdentity = actorIdentity,
            ManualFallback = manualFallback,
            Summary = summary,
            NextAction = nextAction,
            Guidance = guidance,
            TaskStatus = "unchanged",
            SessionStatus = "unchanged",
            FallbackRunPacket = fallbackRunPacket,
        };
    }
}

public sealed record FallbackRunPacket
{
    public string SchemaVersion { get; init; } = "fallback-run-packet.v1";

    public string PacketId { get; init; } = $"fallback-run-packet-{Guid.NewGuid():N}";

    public string TaskId { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public string ActorSessionId { get; init; } = string.Empty;

    public string ActorKind { get; init; } = string.Empty;

    public string ActorIdentity { get; init; } = string.Empty;

    public string Trigger { get; init; } = "manual_fallback";

    public string Status { get; init; } = "incomplete";

    public bool StrictlyRequired { get; init; }

    public bool ClosureBlockerWhenIncomplete { get; init; }

    public IReadOnlyList<string> RequiredReceipts { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PresentReceipts { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MissingReceipts { get; init; } = Array.Empty<string>();

    public string? RoleSwitchReceiptRef { get; init; }

    public string? ContextReceiptRef { get; init; }

    public string? ExecutionClaimRef { get; init; }

    public string? ReviewBundleRef { get; init; }

    public bool GrantsExecutionAuthority { get; init; }

    public bool GrantsTruthWriteAuthority { get; init; }

    public bool CreatesTaskQueue { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
