using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Application.Git;

public interface IWorktreeManager
{
    string ResolveWorktreeRoot(SystemConfig systemConfig, string repoRoot);

    string PrepareWorktree(SystemConfig systemConfig, string repoRoot, string taskId, string? startPoint);

    void CleanupWorktree(string worktreePath);
}
