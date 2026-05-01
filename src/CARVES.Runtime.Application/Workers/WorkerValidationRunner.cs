using Carves.Runtime.Application.Processes;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Workers;

internal sealed class WorkerValidationRunner
{
    private readonly IProcessRunner processRunner;

    public WorkerValidationRunner(IProcessRunner processRunner)
    {
        this.processRunner = processRunner;
    }

    public WorkerValidationOutcome Execute(WorkerRequest request, SafetyValidationMode validationMode, ICollection<string> evidence, WorkerExecutionResult workerExecution)
    {
        var commands = new List<IReadOnlyList<string>>();
        var results = new List<CommandExecutionRecord>();
        var passed = true;

        if (!workerExecution.Succeeded && workerExecution.Status != WorkerExecutionStatus.Skipped)
        {
            evidence.Add($"worker status '{workerExecution.Status}' skipped validation commands");
            return new WorkerValidationOutcome(workerExecution.Status == WorkerExecutionStatus.ApprovalWait, commands, results);
        }

        if (validationMode != SafetyValidationMode.Execution)
        {
            evidence.Add($"non-execution safety mode '{validationMode}' skipped build/test validation commands");
            return new WorkerValidationOutcome(true, commands, results);
        }

        if (request.Session.DryRun)
        {
            evidence.Add("dry-run mode: validation commands were not executed");
            foreach (var command in request.ValidationCommands)
            {
                commands.Add(command);
                results.Add(new CommandExecutionRecord(command, 0, string.Empty, string.Empty, true, request.Session.WorktreeRoot, "validation", DateTimeOffset.UtcNow));
            }

            return new WorkerValidationOutcome(true, commands, results);
        }

        foreach (var command in request.ValidationCommands)
        {
            commands.Add(command);
            var result = processRunner.Run(command.ToArray(), request.Session.WorktreeRoot);
            results.Add(new CommandExecutionRecord(command, result.ExitCode, result.StandardOutput, result.StandardError, false, request.Session.WorktreeRoot, "validation", DateTimeOffset.UtcNow));

            if (result.ExitCode != 0)
            {
                passed = false;
                evidence.Add($"command failed: {string.Join(" ", command)}");
                if (!string.IsNullOrWhiteSpace(result.StandardError))
                {
                    evidence.Add(result.StandardError);
                }

                continue;
            }

            evidence.Add($"command passed: {string.Join(" ", command)}");
        }

        return new WorkerValidationOutcome(passed, commands, results);
    }
}
