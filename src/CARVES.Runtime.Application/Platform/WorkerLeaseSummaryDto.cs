namespace Carves.Runtime.Application.Platform;

public sealed record WorkerLeaseSummaryDto(
    string LeaseId,
    string RepoPath,
    string TaskId,
    string NodeId,
    string Status,
    string OnExpiry,
    DateTimeOffset ExpiresAt,
    string? CompletionReason);
