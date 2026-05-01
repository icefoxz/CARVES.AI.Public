using System.Diagnostics;
using Carves.Runtime.Application.Processes;

namespace Carves.Runtime.Infrastructure.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    public ProcessExecutionResult Run(IReadOnlyList<string> command, string workingDirectory)
    {
        if (command.Count == 0)
        {
            throw new ArgumentException("Command cannot be empty.", nameof(command));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = command[0],
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in command.Skip(1))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start '{command[0]}'.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();
        return new ProcessExecutionResult(process.ExitCode, standardOutput.Trim(), standardError.Trim());
    }
}
