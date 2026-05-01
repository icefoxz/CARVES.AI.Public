using System.Diagnostics;
using System.Text;

namespace Carves.Runtime.IntegrationTests;

internal static class HostScriptHarness
{
    public static CommandResult Run(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(Path.Combine(workingDirectory, "scripts", "carves-host.ps1"));
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start CARVES host script.");
        using var standardOutputClosed = new ManualResetEventSlim();
        using var standardErrorClosed = new ManualResetEventSlim();
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => AppendProcessLine(standardOutput, standardOutputClosed, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => AppendProcessLine(standardError, standardErrorClosed, eventArgs.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(180000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch
            {
            }

            throw new TimeoutException("CARVES host script did not exit within the expected timeout.");
        }

        standardOutputClosed.Wait(TimeSpan.FromSeconds(5));
        standardErrorClosed.Wait(TimeSpan.FromSeconds(5));
        return new CommandResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
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

    internal sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Concat(StandardOutput, StandardError);
    }
}
