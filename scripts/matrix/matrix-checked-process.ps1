Set-StrictMode -Version Latest

if ($null -eq ("Carves.Matrix.PowerShell.MatrixCheckedProcess" -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Carves.Matrix.PowerShell
{
    public sealed class MatrixCheckedProcessResult
    {
        public string Command { get; set; } = "";
        public int ExitCode { get; set; }
        public bool TimedOut { get; set; }
        public int TimeoutSeconds { get; set; }
        public bool ProcessTreeKillAttempted { get; set; }
        public string KillError { get; set; }
        public string Stdout { get; set; } = "";
        public string Stderr { get; set; } = "";
        public bool StdoutTruncated { get; set; }
        public bool StderrTruncated { get; set; }
    }

    public static class MatrixCheckedProcess
    {
        public static MatrixCheckedProcessResult Invoke(
            string fileName,
            string[] arguments,
            string workingDirectory,
            int timeoutSeconds,
            int maxOutputChars)
        {
            using var process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            var stdout = new BoundedCapture(maxOutputChars);
            var stderr = new BoundedCapture(maxOutputChars);
            var result = new MatrixCheckedProcessResult
            {
                Command = fileName + " " + string.Join(" ", arguments),
                TimeoutSeconds = timeoutSeconds
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start command: " + fileName);
            }

            var stdoutTask = Task.Run(() => Pump(process.StandardOutput, stdout));
            var stderrTask = Task.Run(() => Pump(process.StandardError, stderr));
            var timeoutMilliseconds = checked(timeoutSeconds * 1000);

            if (!process.WaitForExit(timeoutMilliseconds))
            {
                result.TimedOut = true;
                result.ProcessTreeKillAttempted = true;
                result.KillError = TryKillProcessTree(process);
                if (process.WaitForExit(1000))
                {
                    Task.WaitAll(new[] { stdoutTask, stderrTask }, 1000);
                }
            }
            else
            {
                Task.WaitAll(new[] { stdoutTask, stderrTask }, 1000);
            }

            result.ExitCode = process.HasExited ? process.ExitCode : -1;
            result.Stdout = stdout.Text;
            result.Stderr = stderr.Text;
            result.StdoutTruncated = stdout.Truncated;
            result.StderrTruncated = stderr.Truncated;
            return result;
        }

        private static void Pump(TextReader reader, BoundedCapture capture)
        {
            var buffer = new char[4096];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                capture.Append(buffer, read);
            }
        }

        private static string TryKillProcessTree(Process process)
        {
            if (process.HasExited)
            {
                return null;
            }

            try
            {
                process.Kill(entireProcessTree: true);
                return null;
            }
            catch (Exception ex)
            {
                var killError = ex.Message;
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception fallback)
                {
                    killError += "; fallback kill failed: " + fallback.Message;
                }

                return killError;
            }
        }

        private sealed class BoundedCapture
        {
            private readonly int maxChars;
            private readonly StringBuilder builder = new();

            public BoundedCapture(int maxChars)
            {
                this.maxChars = maxChars;
            }

            public bool Truncated { get; private set; }
            public string Text => builder.ToString();

            public void Append(char[] buffer, int count)
            {
                var remaining = maxChars - builder.Length;
                if (remaining <= 0)
                {
                    Truncated = true;
                    return;
                }

                if (count <= remaining)
                {
                    builder.Append(buffer, 0, count);
                    return;
                }

                builder.Append(buffer, 0, remaining);
                Truncated = true;
            }
        }
    }
}
'@
}

function Invoke-MatrixCheckedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $FileName,
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $WorkingDirectory,
        [int[]] $AllowedExitCodes = @(0),
        [ValidateRange(1, 3600)][int] $TimeoutSeconds = 300,
        [ValidateRange(1, 10485760)][int] $MaxOutputChars = 1048576
    )

    # Process-tree kill is best effort across PowerShell/.NET hosts; callers get kill_error when the platform refuses it.
    $result = [Carves.Matrix.PowerShell.MatrixCheckedProcess]::Invoke(
        $FileName,
        $Arguments,
        $WorkingDirectory,
        $TimeoutSeconds,
        $MaxOutputChars)

    return [pscustomobject]@{
        command = $result.Command
        exit_code = $result.ExitCode
        passed = (-not $result.TimedOut) -and ($result.ExitCode -in $AllowedExitCodes)
        timed_out = $result.TimedOut
        timeout_seconds = $result.TimeoutSeconds
        process_tree_kill_attempted = $result.ProcessTreeKillAttempted
        kill_error = $result.KillError
        stdout = $result.Stdout
        stderr = $result.Stderr
        stdout_truncated = $result.StdoutTruncated
        stderr_truncated = $result.StderrTruncated
    }
}
