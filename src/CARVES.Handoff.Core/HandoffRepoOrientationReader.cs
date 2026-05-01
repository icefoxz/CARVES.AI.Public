using System.Diagnostics;

namespace Carves.Handoff.Core;

public interface IHandoffRepoOrientationReader
{
    HandoffRepoOrientationSnapshot Read(string repoRoot);
}

public sealed class GitHandoffRepoOrientationReader : IHandoffRepoOrientationReader
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(5);

    public HandoffRepoOrientationSnapshot Read(string repoRoot)
    {
        var branch = RunGit(repoRoot, "rev-parse", "--abbrev-ref", "HEAD");
        var head = RunGit(repoRoot, "rev-parse", "HEAD");
        var status = RunGit(repoRoot, "status", "--porcelain");
        var available = branch.Succeeded || head.Succeeded || status.Succeeded;
        if (!available)
        {
            return new HandoffRepoOrientationSnapshot(
                null,
                null,
                "unknown",
                Available: false,
                "Unable to read git orientation for the current repository.");
        }

        var normalizedBranch = NormalizeBranch(branch.Output);
        var normalizedHead = NormalizeUnknown(head.Output);
        var dirtyState = status.Succeeded
            ? string.IsNullOrWhiteSpace(status.Output) ? "clean" : "dirty"
            : "unknown";

        return new HandoffRepoOrientationSnapshot(
            normalizedBranch,
            normalizedHead,
            dirtyState,
            Available: true,
            null);
    }

    private static GitOutput RunGit(string repoRoot, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new GitOutput(false, null);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)GitTimeout.TotalMilliseconds))
            {
                TryKill(process);
                return new GitOutput(false, null);
            }

            var output = outputTask.GetAwaiter().GetResult().Trim();
            _ = errorTask.GetAwaiter().GetResult();
            return new GitOutput(process.ExitCode == 0, output);
        }
        catch
        {
            return new GitOutput(false, null);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static string? NormalizeBranch(string? value)
    {
        var normalized = NormalizeUnknown(value);
        return string.Equals(normalized, "HEAD", StringComparison.Ordinal)
            ? "(detached)"
            : normalized;
    }

    private static string? NormalizeUnknown(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();
    }

    private sealed record GitOutput(bool Succeeded, string? Output);
}

public sealed record HandoffRepoOrientationSnapshot(
    string? Branch,
    string? HeadCommit,
    string DirtyState,
    bool Available,
    string? Diagnostic);
