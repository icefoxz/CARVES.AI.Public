using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerPermissionPolicyDecision
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public WorkerPermissionDecision Decision { get; init; } = WorkerPermissionDecision.Review;

    public string ReasonCode { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string ConsequenceSummary { get; init; } = string.Empty;
}
