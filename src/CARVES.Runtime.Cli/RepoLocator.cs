namespace Carves.Runtime.Cli;

internal static class RepoLocator
{
    public static string? Resolve(string? explicitRepoRoot = null, string? startDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRepoRoot))
        {
            var explicitPath = Path.GetFullPath(explicitRepoRoot);
            return Directory.Exists(explicitPath) ? explicitPath : null;
        }

        return FindRepositoryRoot(startDirectory ?? Directory.GetCurrentDirectory());
    }

    public static bool IsGitRepository(string path)
    {
        var gitPath = Path.Combine(Path.GetFullPath(path), ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    public static bool IsRepositoryWorkspace(string path)
    {
        return IsGitRepository(path) || LooksLikeRuntimeWorkspace(path);
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (IsRepositoryWorkspace(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool LooksLikeRuntimeWorkspace(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(Path.Combine(fullPath, ".ai")))
        {
            return false;
        }

        var hasBootstrapDocument =
            File.Exists(Path.Combine(fullPath, "README.md"))
            || File.Exists(Path.Combine(fullPath, "AGENTS.md"));
        if (!hasBootstrapDocument)
        {
            return false;
        }

        return Directory.Exists(Path.Combine(fullPath, "src"))
            || Directory.Exists(Path.Combine(fullPath, "tests"))
            || Directory.Exists(Path.Combine(fullPath, "docs"))
            || Directory.EnumerateFiles(fullPath, "*.sln", SearchOption.TopDirectoryOnly).Any();
    }
}
