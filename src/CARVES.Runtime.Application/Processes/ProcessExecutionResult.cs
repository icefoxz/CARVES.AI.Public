namespace Carves.Runtime.Application.Processes;

public sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);
