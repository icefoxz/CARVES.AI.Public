using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter
{
    private static ProcessExecutionResult ExecuteProcess(Process process, string prompt, WorkerExecutionRequest request)
    {
        using var stdoutCapture = ProcessStreamCapture.Start(process.StandardOutput, "codex-cli-stdout");
        using var stderrCapture = ProcessStreamCapture.Start(process.StandardError, "codex-cli-stderr");

        var stdinWrite = Task.Run(() => WritePrompt(process, prompt));

        var effectiveTimeoutSeconds = ResolveTimeoutSeconds(request);
        var stdinWriteGraceSeconds = Math.Clamp(effectiveTimeoutSeconds / 10, 1, 5);
        var stdinWriteCompleted = stdinWrite.Wait(TimeSpan.FromSeconds(stdinWriteGraceSeconds));
        if (!stdinWriteCompleted)
        {
            TryCloseStandardInput(process);
        }

        var timedOut = !process.WaitForExit(effectiveTimeoutSeconds * 1000);
        if (timedOut)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            process.WaitForExit();
        }

        if (!stdinWriteCompleted)
        {
            stdinWrite.Wait(TimeSpan.FromSeconds(5));
        }

        var stdout = stdoutCapture.Complete();
        var stderr = stderrCapture.Complete();
        if (timedOut)
        {
            stderr = string.IsNullOrWhiteSpace(stderr)
                ? $"Codex CLI timed out after {effectiveTimeoutSeconds} second(s)."
                : $"{stderr}{Environment.NewLine}Codex CLI timed out after {effectiveTimeoutSeconds} second(s).";
        }

        return new ProcessExecutionResult(
            timedOut ? 124 : process.ExitCode,
            stdout,
            stderr,
            timedOut,
            effectiveTimeoutSeconds);
    }

    private static void WritePrompt(Process process, string prompt)
    {
        try
        {
            process.StandardInput.Write(prompt);
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private static void TryCloseStandardInput(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string MapSandboxMode(WorkerSandboxMode mode)
    {
        return mode switch
        {
            WorkerSandboxMode.ReadOnly => "read-only",
            WorkerSandboxMode.WorkspaceWrite => "workspace-write",
            WorkerSandboxMode.DangerFullAccess => "danger-full-access",
            _ => "workspace-write",
        };
    }

    private static string MapApprovalMode(WorkerApprovalMode mode)
    {
        return mode switch
        {
            WorkerApprovalMode.Never => "never",
            WorkerApprovalMode.OnRequest => "on-request",
            WorkerApprovalMode.OnFailure => "on-failure",
            WorkerApprovalMode.Untrusted => "untrusted",
            _ => "untrusted",
        };
    }

    private static int ResolveTimeoutSeconds(WorkerExecutionRequest request)
    {
        if (request.RequestBudget.TimeoutSeconds > 0)
        {
            return request.RequestBudget.TimeoutSeconds;
        }

        return request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30;
    }

    private static string? NormalizeReasoningEffort(string? reasoningEffort)
    {
        if (string.IsNullOrWhiteSpace(reasoningEffort))
        {
            return null;
        }

        var normalized = reasoningEffort.Trim().ToLowerInvariant();
        if (SupportedReasoningEfforts.Contains(normalized, StringComparer.Ordinal))
        {
            return normalized;
        }

        return normalized switch
        {
            "minimal" => "low",
            "xhigh" => "high",
            _ => "medium",
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.ToString();
    }

    private sealed class ProcessStreamCapture : IDisposable
    {
        private readonly TextReader reader;
        private readonly StringBuilder buffer = new();
        private readonly Thread thread;
        private Exception? failure;

        private ProcessStreamCapture(TextReader reader, string threadName)
        {
            this.reader = reader;
            thread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = threadName,
            };
            thread.Start();
        }

        public static ProcessStreamCapture Start(TextReader reader, string threadName)
        {
            return new ProcessStreamCapture(reader, threadName);
        }

        public string Complete()
        {
            thread.Join();
            if (failure is not null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return buffer.ToString();
        }

        public void Dispose()
        {
            if (thread.IsAlive)
            {
                thread.Join(TimeSpan.FromSeconds(5));
            }
        }

        private void ReadLoop()
        {
            try
            {
                var chunk = new char[1024];
                int read;
                while ((read = reader.Read(chunk, 0, chunk.Length)) > 0)
                {
                    buffer.Append(chunk, 0, read);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }
    }
}
