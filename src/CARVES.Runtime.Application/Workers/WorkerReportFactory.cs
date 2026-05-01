using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

internal static class WorkerReportFactory
{
    public static TaskRunReport CreatePreliminary(
        WorkerRequest request,
        SystemConfig systemConfig,
        WorkerExecutionResult workerExecution,
        WorkerValidationOutcome validationOutcome,
        IReadOnlyList<string> evidence)
    {
        var session = request.Session with
        {
            RequestedWorkerThreadId = request.ExecutionRequest?.PriorThreadId ?? request.Session.RequestedWorkerThreadId,
            WorkerThreadId = workerExecution.ThreadId,
            WorkerThreadContinuity = workerExecution.ThreadContinuity,
        };

        return new TaskRunReport
        {
            TaskId = request.Task.TaskId,
            Request = request,
            Session = session,
            WorktreePath = request.Session.WorktreeRoot,
            DryRun = request.Session.DryRun,
            ResultCommit = null,
            SafetyDecision = SafetyDecision.Allow(request.Task.TaskId),
            Patch = BuildPatchSummary(request.Task, workerExecution),
            WorkerExecution = workerExecution,
            Notes =
            [
                $"repo id: {request.Session.RepoId ?? request.ExecutionRequest?.RepoId ?? "local-repo"}",
                $"current commit: {request.Session.CurrentCommit}",
                $"worktree root: {request.Session.WorktreeRoot}",
                $"default test command head: {systemConfig.DefaultTestCommand.FirstOrDefault() ?? "(none)"}",
                $"worker backend: {workerExecution.BackendId}",
                $"worker provider: {workerExecution.ProviderId}",
                $"worker success: {(workerExecution.Succeeded ? "yes" : "no")}",
            ],
            Validation = new ValidationResult
            {
                Passed = validationOutcome.Passed,
                Commands = validationOutcome.Commands,
                CommandResults = validationOutcome.Results,
                Evidence = evidence,
            },
        };
    }

    public static TaskRunReport CreateFinal(TaskRunReport preliminaryReport, SafetyDecision safetyDecision)
    {
        return new TaskRunReport
        {
            TaskId = preliminaryReport.TaskId,
            Request = preliminaryReport.Request,
            Session = preliminaryReport.Session,
            WorktreePath = preliminaryReport.WorktreePath,
            DryRun = preliminaryReport.DryRun,
            ResultCommit = preliminaryReport.ResultCommit,
            SafetyDecision = safetyDecision,
            Patch = preliminaryReport.Patch,
            WorkerExecution = preliminaryReport.WorkerExecution,
            Notes = preliminaryReport.Notes,
            Validation = new ValidationResult
            {
                Passed = preliminaryReport.Validation.Passed && safetyDecision.Allowed,
                Commands = preliminaryReport.Validation.Commands,
                CommandResults = preliminaryReport.Validation.CommandResults,
                Evidence = preliminaryReport.Validation.Evidence,
            },
        };
    }

    private static PatchSummary BuildPatchSummary(TaskNode task, WorkerExecutionResult workerExecution)
    {
        var paths = workerExecution.ChangedFiles
            .Select(NormalizePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new PatchSummary(
            paths.Length,
            0,
            0,
            true,
            paths);
    }

    private static string NormalizePath(string scopeEntry)
    {
        return scopeEntry
            .Trim()
            .Trim('`')
            .Replace('\\', '/');
    }
}
