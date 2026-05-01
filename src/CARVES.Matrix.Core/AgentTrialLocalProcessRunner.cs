using System.Diagnostics;
using System.Text;

namespace Carves.Matrix.Core;

internal sealed record AgentTrialProcessResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    long DurationMs);

internal static class AgentTrialLocalProcessRunner
{
    private const int TimeoutExitCode = 124;
    private const int MaxCapturedChars = 1_000_000;
    private static readonly TimeSpan KillGraceTimeout = TimeSpan.FromSeconds(2);

    public static AgentTrialProcessResult RunShell(string command, string workingDirectory, TimeSpan timeout)
    {
        if (OperatingSystem.IsWindows())
        {
            return Run("cmd.exe", ["/c", command], workingDirectory, timeout);
        }

        return Run("/bin/bash", ["-lc", command], workingDirectory, timeout);
    }

    public static AgentTrialProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Command timeout must be positive.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = MatrixProcessCommandResolver.ResolveForProcessStart(fileName),
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var startedAt = DateTimeOffset.UtcNow;
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start command: {fileName}");
        }

        using var drainCancellation = new CancellationTokenSource();
        var stdout = new CapturedOutput(MaxCapturedChars);
        var stderr = new CapturedOutput(MaxCapturedChars);
        var stdoutDrain = DrainAsync(process.StandardOutput, stdout, drainCancellation.Token);
        var stderrDrain = DrainAsync(process.StandardError, stderr, drainCancellation.Token);
        var waitForExit = process.WaitForExitAsync();
        var timedOut = !WaitForTask(waitForExit, timeout);
        var exited = !timedOut;
        if (timedOut)
        {
            TryKillProcessTree(process);
            exited = WaitForTask(waitForExit, KillGraceTimeout);
            if (!exited)
            {
                drainCancellation.Cancel();
            }
        }

        if (!WaitForTask(Task.WhenAll(stdoutDrain, stderrDrain), exited ? KillGraceTimeout : TimeSpan.FromMilliseconds(100)))
        {
            drainCancellation.Cancel();
            WaitForTask(Task.WhenAll(stdoutDrain, stderrDrain), TimeSpan.FromMilliseconds(100));
        }

        var completedAt = DateTimeOffset.UtcNow;
        return new AgentTrialProcessResult(
            timedOut ? TimeoutExitCode : process.ExitCode,
            stdout.ToString(),
            stderr.ToString(),
            timedOut,
            startedAt,
            completedAt,
            Math.Max(0, (long)(completedAt - startedAt).TotalMilliseconds));
    }

    private static async Task DrainAsync(StreamReader reader, CapturedOutput output, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new char[4096];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                output.Append(buffer.AsSpan(0, read));
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }

    private static bool WaitForTask(Task task, TimeSpan timeout)
    {
        if (task.IsCompleted)
        {
            task.GetAwaiter().GetResult();
            return true;
        }

        var completed = Task.WhenAny(task, Task.Delay(timeout)).GetAwaiter().GetResult() == task;
        if (completed)
        {
            task.GetAwaiter().GetResult();
        }

        return completed;
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private sealed class CapturedOutput
    {
        private readonly int _maxChars;
        private readonly StringBuilder _builder = new();

        public CapturedOutput(int maxChars)
        {
            _maxChars = maxChars;
        }

        public void Append(ReadOnlySpan<char> value)
        {
            var remaining = _maxChars - _builder.Length;
            if (remaining <= 0)
            {
                return;
            }

            _builder.Append(value.Length > remaining ? value[..remaining] : value);
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }
}
