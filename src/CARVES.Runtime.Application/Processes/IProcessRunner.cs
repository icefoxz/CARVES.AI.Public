namespace Carves.Runtime.Application.Processes;

public interface IProcessRunner
{
    ProcessExecutionResult Run(IReadOnlyList<string> command, string workingDirectory);
}
