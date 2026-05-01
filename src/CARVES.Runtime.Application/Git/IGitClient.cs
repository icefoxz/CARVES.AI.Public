namespace Carves.Runtime.Application.Git;

public interface IGitClient
{
    string TryGetCurrentCommit(string repoRoot);

    bool IsRepository(string repoRoot);

    bool HasUncommittedChanges(string repoRoot);

    IReadOnlyList<string> GetUncommittedPaths(string repoRoot);

    IReadOnlyList<string> GetUntrackedPaths(string repoRoot);

    IReadOnlyList<string> GetChangedPathsSince(string repoRoot, string baseCommit);

    string? TryGetUncommittedDiff(string repoRoot, IReadOnlyList<string>? paths = null);

    string? TryCreateScopedSnapshotCommit(string repoRoot, IReadOnlyList<string> paths, string message);

    bool TryCreateDetachedWorktree(string repoRoot, string worktreePath, string startPoint);

    void TryRemoveWorktree(string repoRoot, string worktreePath);
}
