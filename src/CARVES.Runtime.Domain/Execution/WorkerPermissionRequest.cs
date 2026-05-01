using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerPermissionRequest
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string PermissionRequestId { get; init; } = $"worker-permission-{Guid.NewGuid():N}";

    public string RunId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string AdapterId { get; init; } = string.Empty;

    public string ProfileId { get; init; } = string.Empty;

    public WorkerPermissionKind Kind { get; init; } = WorkerPermissionKind.UnknownPermissionRequest;

    public WorkerPermissionRiskLevel RiskLevel { get; init; } = WorkerPermissionRiskLevel.High;

    public string ScopeSummary { get; init; } = string.Empty;

    public string? ResourcePath { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string? RawPrompt { get; init; }

    public string? RawPayload { get; init; }

    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public WorkerPermissionDecision RecommendedDecision { get; set; } = WorkerPermissionDecision.Review;

    public string RecommendedReasonCode { get; set; } = string.Empty;

    public string RecommendedReason { get; set; } = string.Empty;

    public string RecommendedConsequenceSummary { get; set; } = string.Empty;

    public WorkerPermissionState State { get; set; } = WorkerPermissionState.Pending;

    public WorkerPermissionDecision? FinalDecision { get; set; }

    public WorkerPermissionDecisionActorKind? DecisionActorKind { get; set; }

    public string? DecisionIdentity { get; set; }

    public string? DecisionReason { get; set; }

    public string? ConsequenceSummary { get; set; }

    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ResolvedAt { get; set; }
}
