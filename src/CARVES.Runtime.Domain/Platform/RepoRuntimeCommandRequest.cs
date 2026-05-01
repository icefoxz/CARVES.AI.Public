namespace Carves.Runtime.Domain.Platform;

public sealed record RepoRuntimeCommandRequest(
    RepoRuntimeGatewayOperation Operation,
    string RepoPath,
    string? RepoId,
    bool DryRun,
    string? Reason,
    string? SessionId,
    string? TaskId,
    WorkerLeaseDisposition? LeaseDisposition);
