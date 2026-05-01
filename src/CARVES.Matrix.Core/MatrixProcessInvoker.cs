using System.Diagnostics;
using System.Text;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static readonly TimeSpan DefaultProcessTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ProcessKillGraceTimeout = TimeSpan.FromSeconds(2);
    private const int ProcessTimeoutExitCode = 124;
    private const int MaxCapturedProcessOutputChars = 1_000_000;

    private static ScriptResult InvokeProcess(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        return InvokeProcess(fileName, arguments, workingDirectory, DefaultProcessTimeout, MaxCapturedProcessOutputChars);
    }

    private static ScriptResult InvokeProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        int maxCapturedOutputChars)
    {
        return InvokeProcess(
            fileName,
            arguments,
            workingDirectory,
            timeout,
            maxCapturedOutputChars,
            ProcessKillGraceTimeout,
            TryKillProcessTree);
    }

    private static ScriptResult InvokeProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        int maxCapturedOutputChars,
        TimeSpan killGraceTimeout,
        Action<Process> killProcessTree)
    {
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

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Matrix process timeout must be positive.");
        }

        if (maxCapturedOutputChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCapturedOutputChars), maxCapturedOutputChars, "Matrix process output capture limit must be positive.");
        }

        if (killGraceTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(killGraceTimeout), killGraceTimeout, "Matrix process kill grace timeout must be positive.");
        }

        ArgumentNullException.ThrowIfNull(killProcessTree);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start command: {fileName}");
        }

        using var drainCancellation = new CancellationTokenSource();
        var stdout = new CapturedProcessOutput(maxCapturedOutputChars);
        var stderr = new CapturedProcessOutput(maxCapturedOutputChars);
        var stdoutDrain = DrainProcessStream(process.StandardOutput, stdout, drainCancellation.Token);
        var stderrDrain = DrainProcessStream(process.StandardError, stderr, drainCancellation.Token);
        var waitForExit = process.WaitForExitAsync();
        var timedOut = !WaitForTask(waitForExit, timeout);
        var processExited = !timedOut;
        if (timedOut)
        {
            killProcessTree(process);
            processExited = WaitForTask(waitForExit, killGraceTimeout);
            if (!processExited)
            {
                drainCancellation.Cancel();
            }
        }

        if (!WaitForProcessStreams(stdoutDrain, stderrDrain, processExited ? killGraceTimeout : TimeSpan.FromMilliseconds(100)))
        {
            drainCancellation.Cancel();
            WaitForProcessStreams(stdoutDrain, stderrDrain, TimeSpan.FromMilliseconds(100));
        }

        return new ScriptResult(
            timedOut ? ProcessTimeoutExitCode : process.ExitCode,
            stdout.ToString(),
            stderr.ToString(),
            $"{fileName} {string.Join(' ', arguments)}",
            timedOut,
            stdout.Truncated,
            stderr.Truncated);
    }

    private static async Task DrainProcessStream(StreamReader reader, CapturedProcessOutput output, CancellationToken cancellationToken)
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

    private static bool WaitForProcessStreams(Task stdoutDrain, Task stderrDrain, TimeSpan timeout)
    {
        return WaitForTask(Task.WhenAll(stdoutDrain, stderrDrain), timeout);
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

    private sealed class CapturedProcessOutput
    {
        private readonly int _maxChars;
        private readonly StringBuilder _builder = new();

        public CapturedProcessOutput(int maxChars)
        {
            _maxChars = maxChars;
        }

        public bool Truncated { get; private set; }

        public void Append(ReadOnlySpan<char> value)
        {
            var remaining = _maxChars - _builder.Length;
            if (remaining <= 0)
            {
                Truncated = true;
                return;
            }

            if (value.Length > remaining)
            {
                _builder.Append(value[..remaining]);
                Truncated = true;
                return;
            }

            _builder.Append(value);
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }
}
