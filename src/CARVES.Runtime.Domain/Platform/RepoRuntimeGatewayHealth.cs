namespace Carves.Runtime.Domain.Platform;

public sealed record RepoRuntimeGatewayHealth(
    RepoRuntimeGatewayMode Mode,
    RepoRuntimeGatewayHealthState State,
    bool Reachable,
    string Reason,
    DateTimeOffset ObservedAt);
