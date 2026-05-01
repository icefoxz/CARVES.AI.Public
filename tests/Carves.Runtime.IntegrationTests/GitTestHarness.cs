using System.Diagnostics;
using System.Text;

namespace Carves.Runtime.IntegrationTests;

internal static class GitTestHarness
{
    private static readonly TimeSpan GitCommandTimeout = TimeSpan.FromSeconds(30);
    private static readonly string EmptyGlobalConfigPath = EnsureEmptyGlobalConfig();
    private static readonly string DisabledHooksPath = EnsureDisabledHooksPath();

    public static void InitializeRepository(string rootPath)
    {
        Directory.CreateDirectory(rootPath);
        Run(rootPath, "init");
        Run(rootPath, "config", "user.email", "tests@example.com");
        Run(rootPath, "config", "user.name", "CARVES Tests");
        Run(rootPath, "config", "commit.gpgsign", "false");
        Run(rootPath, "config", "tag.gpgsign", "false");
        Run(rootPath, "config", "core.hooksPath", DisabledHooksPath);
    }

    public static void Run(string workingDirectory, params string[] arguments)
    {
        var result = RunWithOutput(workingDirectory, arguments);
        if (result.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(BuildFailureMessage(workingDirectory, arguments, result.ExitCode, result.StandardError));
    }

    public static string RunForStandardOutput(string workingDirectory, params string[] arguments)
    {
        var result = RunWithOutput(workingDirectory, arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildFailureMessage(workingDirectory, arguments, result.ExitCode, result.StandardError));
        }

        return result.StandardOutput;
    }

    private static GitCommandResult RunWithOutput(string workingDirectory, IReadOnlyList<string> arguments)
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

        // Ignore host-machine git config so test repos do not block on signing, hooks, or prompts.
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = EmptyGlobalConfigPath;
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        startInfo.Environment["GIT_AUTHOR_NAME"] = "CARVES Tests";
        startInfo.Environment["GIT_AUTHOR_EMAIL"] = "tests@example.com";
        startInfo.Environment["GIT_COMMITTER_NAME"] = "CARVES Tests";
        startInfo.Environment["GIT_COMMITTER_EMAIL"] = "tests@example.com";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");
        using var standardOutputClosed = new ManualResetEventSlim();
        using var standardErrorClosed = new ManualResetEventSlim();
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => AppendProcessLine(standardOutput, standardOutputClosed, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => AppendProcessLine(standardError, standardErrorClosed, eventArgs.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit((int)GitCommandTimeout.TotalMilliseconds))
        {
            TryKill(process);
            WaitForReaders(standardOutputClosed, standardErrorClosed);
            throw new TimeoutException(
                $"git {string.Join(' ', arguments)} timed out after {GitCommandTimeout.TotalSeconds:0}s in '{workingDirectory}'.");
        }

        WaitForReaders(standardOutputClosed, standardErrorClosed);
        return new GitCommandResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private static string BuildFailureMessage(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        int exitCode,
        string standardError)
    {
        var error = string.IsNullOrWhiteSpace(standardError) ? "(no stderr)" : standardError.Trim();
        return $"git {string.Join(' ', arguments)} failed with exit code {exitCode} in '{workingDirectory}': {error}";
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
        }
    }

    private static void WaitForReaders(ManualResetEventSlim standardOutputClosed, ManualResetEventSlim standardErrorClosed)
    {
        standardOutputClosed.Wait(TimeSpan.FromSeconds(5));
        standardErrorClosed.Wait(TimeSpan.FromSeconds(5));
    }

    private static void AppendProcessLine(StringBuilder builder, ManualResetEventSlim completed, string? line)
    {
        if (line is null)
        {
            completed.Set();
            return;
        }

        builder.AppendLine(line);
    }

    private static string EnsureEmptyGlobalConfig()
    {
        var path = Path.Combine(Path.GetTempPath(), "carves-runtime-test.gitconfig");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, string.Empty);
        }

        return path;
    }

    private static string EnsureDisabledHooksPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "carves-runtime-test-hooks");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
}
