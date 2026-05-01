namespace Carves.Runtime.Application.Configuration;

public sealed record SystemConfig(
    string RepoName,
    string WorktreeRoot,
    int MaxParallelTasks,
    IReadOnlyList<string> DefaultTestCommand,
    IReadOnlyList<string> CodeDirectories,
    IReadOnlyList<string> ExcludedDirectories,
    bool SyncMarkdownViews,
    bool RemoveWorktreeOnSuccess)
{
    public static SystemConfig CreateDefault(string repoName)
    {
        return new SystemConfig(
            repoName,
            $"../.carves-worktrees/{repoName}",
            4,
            new[] { "dotnet", "test", "CARVES.Runtime.sln" },
            new[] { "src", "tests" },
            new[] { ".git", ".nuget", ".venv", "__pycache__", "bin", "obj", "TestResults", "coverage", ".ai/logs", ".ai/patches", ".ai/worktrees" },
            true,
            true);
    }
}
