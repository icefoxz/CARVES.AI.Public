namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeConsistencyReport
{
    public string RepoRoot { get; init; } = string.Empty;

    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;

    public string SessionStatus { get; init; } = "none";

    public int ActiveLeaseCount { get; init; }

    public int RunningTaskCount { get; init; }

    public int PendingApprovalCount { get; init; }

    public RuntimeConsistencyHostSnapshot? HostSnapshot { get; init; }

    public IReadOnlyList<RuntimeConsistencyFinding> Findings { get; init; } = Array.Empty<RuntimeConsistencyFinding>();

    public bool IsConsistent => Findings.Count == 0;
}
