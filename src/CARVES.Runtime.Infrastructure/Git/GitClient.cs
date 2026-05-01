using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Processes;

namespace Carves.Runtime.Infrastructure.Git;

public sealed class GitClient : IGitClient
{
    private readonly IProcessRunner processRunner;

    public GitClient(IProcessRunner processRunner)
    {
        this.processRunner = processRunner;
    }

    public string TryGetCurrentCommit(string repoRoot)
    {
        try
        {
            var result = processRunner.Run(["git", "rev-parse", "HEAD"], repoRoot);
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput)
                ? result.StandardOutput
                : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    public bool IsRepository(string repoRoot)
    {
        try
        {
            var result = processRunner.Run(["git", "rev-parse", "--is-inside-work-tree"], repoRoot);
            return result.ExitCode == 0 && string.Equals(result.StandardOutput, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public bool HasUncommittedChanges(string repoRoot)
    {
        try
        {
            var result = processRunner.Run(["git", "status", "--porcelain"], repoRoot);
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<string> GetUncommittedPaths(string repoRoot)
    {
        try
        {
            var result = processRunner.Run(["git", "status", "--porcelain"], repoRoot);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Array.Empty<string>();
            }

            return result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(ParsePorcelainPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> GetUntrackedPaths(string repoRoot)
    {
        try
        {
            var result = processRunner.Run(["git", "status", "--porcelain"], repoRoot);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Array.Empty<string>();
            }

            return result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith("?? ", StringComparison.Ordinal))
                .Select(ParsePorcelainPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<string> GetChangedPathsSince(string repoRoot, string baseCommit)
    {
        if (string.IsNullOrWhiteSpace(baseCommit)
            || string.Equals(baseCommit, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        try
        {
            if (!IsRepository(repoRoot))
            {
                return Array.Empty<string>();
            }

            var result = processRunner.Run(["git", "diff", "--name-only", "--relative", $"{baseCommit}..HEAD"], repoRoot);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Array.Empty<string>();
            }

            return result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(path => path.Replace('\\', '/').Trim())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string? TryGetUncommittedDiff(string repoRoot, IReadOnlyList<string>? paths = null)
    {
        try
        {
            var command = new List<string> { "git", "diff", "--no-ext-diff", "--binary", "--relative" };
            if (paths is not null && paths.Count > 0)
            {
                command.Add("--");
                command.AddRange(paths.Where(path => !string.IsNullOrWhiteSpace(path)));
            }

            var result = processRunner.Run(command, repoRoot);
            return result.ExitCode == 0
                ? result.StandardOutput
                : null;
        }
        catch
        {
            return null;
        }
    }

    public string? TryCreateScopedSnapshotCommit(string repoRoot, IReadOnlyList<string> paths, string message)
    {
        var normalizedPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedPaths.Length == 0 || !IsRepository(repoRoot))
        {
            return null;
        }

        try
        {
            var addCommand = new List<string> { "git", "add", "--" };
            addCommand.AddRange(normalizedPaths);
            var addResult = processRunner.Run(addCommand, repoRoot);
            if (addResult.ExitCode != 0)
            {
                return null;
            }

            var diffCommand = new List<string> { "git", "diff", "--cached", "--quiet", "--" };
            diffCommand.AddRange(normalizedPaths);
            var diffResult = processRunner.Run(diffCommand, repoRoot);
            if (diffResult.ExitCode == 0)
            {
                var currentCommit = TryGetCurrentCommit(repoRoot);
                return string.Equals(currentCommit, "unknown", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : currentCommit;
            }

            if (diffResult.ExitCode != 1)
            {
                return null;
            }

            var commitCommand = new List<string>
            {
                "git",
                "-c",
                "user.name=CARVES Runtime",
                "-c",
                "user.email=carves-runtime@example.invalid",
                "commit",
                "--only",
                "-m",
                message,
                "--",
            };
            commitCommand.AddRange(normalizedPaths);
            var commitResult = processRunner.Run(commitCommand, repoRoot);
            if (commitResult.ExitCode != 0)
            {
                return null;
            }

            var resultCommit = TryGetCurrentCommit(repoRoot);
            return string.Equals(resultCommit, "unknown", StringComparison.OrdinalIgnoreCase)
                ? null
                : resultCommit;
        }
        catch
        {
            return null;
        }
    }

    public bool TryCreateDetachedWorktree(string repoRoot, string worktreePath, string startPoint)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(worktreePath)!);
            if (Directory.Exists(worktreePath))
            {
                Directory.Delete(worktreePath, true);
            }

            var result = processRunner.Run(["git", "worktree", "add", "--detach", worktreePath, startPoint], repoRoot);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public void TryRemoveWorktree(string repoRoot, string worktreePath)
    {
        try
        {
            if (IsRepository(repoRoot))
            {
                var result = processRunner.Run(["git", "worktree", "remove", "--force", worktreePath], repoRoot);
                if (result.ExitCode == 0)
                {
                    DeleteDirectoryIfPresent(worktreePath);
                    return;
                }
            }
        }
        catch
        {
        }

        DeleteDirectoryIfPresent(worktreePath);
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private static string ParsePorcelainPath(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var trimmedLine = line.TrimEnd();
        string path;
        if (trimmedLine.Length > 3 && trimmedLine[2] == ' ')
        {
            path = trimmedLine[3..].Trim();
        }
        else if (trimmedLine.Length > 2 && trimmedLine[1] == ' ')
        {
            path = trimmedLine[2..].Trim();
        }
        else if (trimmedLine.Length > 3)
        {
            path = trimmedLine[3..].Trim();
        }
        else
        {
            return string.Empty;
        }

        var renameSeparator = " -> ";
        var renameIndex = path.IndexOf(renameSeparator, StringComparison.Ordinal);
        if (renameIndex >= 0)
        {
            path = path[(renameIndex + renameSeparator.Length)..];
        }

        return path.Replace('\\', '/');
    }
}
