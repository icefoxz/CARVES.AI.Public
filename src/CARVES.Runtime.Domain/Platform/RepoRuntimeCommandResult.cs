namespace Carves.Runtime.Domain.Platform;

public sealed record RepoRuntimeCommandResult(
    bool Succeeded,
    RepoRuntimeGatewayMode GatewayMode,
    RepoRuntimeGatewayHealthState HealthState,
    string Reason,
    string? SessionId,
    string? TaskId,
    string? OutcomeStatus);
