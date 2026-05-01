using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform;

public sealed record RepoRuntimeSummary(
    string RepoId,
    string RepoPath,
    string Stage,
    RuntimeSessionStatus? SessionStatus,
    RuntimeActionability Actionability,
    int OpenOpportunityCount,
    int OpenTaskCount,
    int ReviewTaskCount,
    string? StopReason,
    string? WaitingReason,
    string? ActiveSessionId,
    DateTimeOffset ObservedAt);
