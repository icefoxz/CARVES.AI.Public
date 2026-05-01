using System.Diagnostics;
using Carves.Runtime.Infrastructure.Git;
using Carves.Runtime.Infrastructure.Processes;

namespace Carves.Runtime.Application.Tests;

public sealed class GitClientTests
{
    [Fact]
    public void GetUncommittedPaths_PreservesFirstCharacterOfFirstUnstagedPath()
    {
        using var workspace = new TemporaryWorkspace();
        InitializeGitRepository(workspace.RootPath);
        var relativePath = "docs/agentcoach-quadrant-planner.html";
        var fullPath = Path.Combine(workspace.RootPath, "docs", "agentcoach-quadrant-planner.html");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "<!doctype html>");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "Add planner");

        File.WriteAllText(fullPath, "<!doctype html><title>changed</title>");

        var paths = new GitClient(new ProcessRunner()).GetUncommittedPaths(workspace.RootPath);

        Assert.Contains(relativePath, paths, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("ocs/agentcoach-quadrant-planner.html", paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetChangedPathsSince_NonRepository_ReturnsEmpty()
    {
        using var workspace = new TemporaryWorkspace();

        var paths = new GitClient(new ProcessRunner()).GetChangedPathsSince(workspace.RootPath, "abc123");

        Assert.Empty(paths);
    }

    private static void InitializeGitRepository(string repoRoot)
    {
        RunGit(repoRoot, "init");
        RunGit(repoRoot, "config", "user.email", "tests@example.com");
        RunGit(repoRoot, "config", "user.name", "CARVES Tests");
        File.WriteAllText(Path.Combine(repoRoot, "README.md"), "# Test repo");
        RunGit(repoRoot, "add", ".");
        RunGit(repoRoot, "commit", "-m", "Initial commit");
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {standardError}");
        }
    }
}
