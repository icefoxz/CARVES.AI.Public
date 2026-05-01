using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Failures;

public sealed class FailureReportService
{
    private readonly string repoRoot;
    private readonly IFailureReportRepository repository;
    private readonly FailureClassificationService classificationService;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public FailureReportService(
        string repoRoot,
        IFailureReportRepository repository,
        FailureClassificationService classificationService,
        IRuntimeArtifactRepository artifactRepository)
    {
        this.repoRoot = repoRoot;
        this.repository = repository;
        this.classificationService = classificationService;
        this.artifactRepository = artifactRepository;
    }

    public FailureReport EmitRuntimeFailure(TaskNode? task, RuntimeFailureRecord failure, RuntimeSessionState? session)
    {
        var workerArtifact = task is null ? null : artifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId);
        var workerResult = workerArtifact?.Result;
        var classification = classificationService.Classify(failure, workerResult);
        var attribution = classificationService.BuildAttribution(failure, workerResult);
        var previousFailures = task is null
            ? 0
            : repository.LoadAll().Count(item => string.Equals(item.TaskId, task.TaskId, StringComparison.Ordinal));
        var report = new FailureReport
        {
            Id = CreateFailureId(),
            Timestamp = DateTimeOffset.UtcNow,
            CardId = task?.CardId,
            TaskId = task?.TaskId ?? failure.TaskId,
            Repo = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Branch = string.Empty,
            Worktree = ResolveWorktree(workerResult),
            Provider = workerResult?.ProviderId,
            ModelProfile = workerResult?.ProfileId,
            Objective = task?.Description ?? task?.Title ?? failure.Reason,
            InputSummary = new FailureInputSummary
            {
                FilesInvolved = task?.Scope ?? Array.Empty<string>(),
                EstimatedScope = EstimateScope(task?.Scope),
            },
            Execution = new FailureExecutionSummary
            {
                PatchFiles = workerResult?.ChangedFiles.Count ?? 0,
                PatchLines = 0,
                CommandsRun = workerResult?.CommandTrace
                    .Select(record => string.Join(' ', record.Command))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray() ?? Array.Empty<string>(),
                DurationSeconds = workerResult is null ? 0 : (int)Math.Max(0, (workerResult.CompletedAt - workerResult.StartedAt).TotalSeconds),
            },
            Result = new FailureResultSummary
            {
                Status = ResolveFailureStatus(task),
                StopReason = failure.FailureType.ToString(),
            },
            Failure = new FailureDetails
            {
                Type = classification,
                Message = failure.Reason,
                Details = failure.Source,
                StackTrace = failure.ExceptionType,
            },
            Attribution = attribution,
            Review = new FailureReviewSummary
            {
                Required = task?.RequiresReviewBoundary ?? false,
                Rejected = failure.FailureType == RuntimeFailureType.ReviewRejected,
                Reason = task?.PlannerReview.Reason,
            },
            ContextSnapshot = new FailureContextSnapshot
            {
                State = task?.Status.ToString() ?? failure.SessionStatus.ToString(),
                PreviousFailures = previousFailures,
                RetryCount = task?.RetryCount ?? 0,
            },
        };

        repository.Append(report);
        return report;
    }

    public FailureReport EmitTaskBlocked(TaskNode task, string reason)
    {
        var previousFailures = repository.LoadAll().Count(item => string.Equals(item.TaskId, task.TaskId, StringComparison.Ordinal));
        var report = new FailureReport
        {
            Id = CreateFailureId(),
            Timestamp = DateTimeOffset.UtcNow,
            CardId = task.CardId,
            TaskId = task.TaskId,
            Repo = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Branch = string.Empty,
            Objective = task.Description ?? task.Title,
            InputSummary = new FailureInputSummary
            {
                FilesInvolved = task.Scope,
                EstimatedScope = EstimateScope(task.Scope),
            },
            Result = new FailureResultSummary
            {
                Status = "blocked",
                StopReason = "task_blocked",
            },
            Failure = new FailureDetails
            {
                Type = FailureType.Unknown,
                Message = reason,
                Details = "Task was explicitly blocked by review or operator boundary.",
            },
            Attribution = new FailureAttribution
            {
                Layer = FailureAttributionLayer.Task,
                Confidence = 0.55,
                Notes = "Blocked state was imposed from control-plane review rather than delegated worker output.",
            },
            Review = new FailureReviewSummary
            {
                Required = task.RequiresReviewBoundary,
                Rejected = false,
                Reason = reason,
            },
            ContextSnapshot = new FailureContextSnapshot
            {
                State = task.Status.ToString(),
                PreviousFailures = previousFailures,
                RetryCount = task.RetryCount,
            },
        };

        repository.Append(report);
        return report;
    }

    public FailureReport EmitResultFailure(TaskNode task, ResultEnvelope result, WorkerExecutionArtifact? workerArtifact = null)
    {
        var previousFailures = repository.LoadAll().Count(item => string.Equals(item.TaskId, task.TaskId, StringComparison.Ordinal));
        var classification = classificationService.Classify(result);
        var workerClassification = workerArtifact is null ? null : WorkerFailureSemantics.Classify(workerArtifact.Result);
        var report = new FailureReport
        {
            Id = CreateFailureId(),
            Timestamp = DateTimeOffset.UtcNow,
            CardId = task.CardId,
            TaskId = task.TaskId,
            Repo = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Branch = string.Empty,
            Objective = task.Description ?? task.Title,
            InputSummary = new FailureInputSummary
            {
                FilesInvolved = task.Scope,
                EstimatedScope = EstimateScope(task.Scope),
            },
            Execution = new FailureExecutionSummary
            {
                PatchFiles = result.Changes.FilesModified.Concat(result.Changes.FilesAdded).Distinct(StringComparer.Ordinal).Count(),
                PatchLines = result.Changes.LinesChanged,
                CommandsRun = result.Validation.CommandsRun,
                DurationSeconds = 0,
            },
            Result = new FailureResultSummary
            {
                Status = result.Status,
                StopReason = result.Result.StopReason,
            },
            Failure = new FailureDetails
            {
                Type = classification,
                Lane = workerClassification?.Lane.ToString().ToLowerInvariant(),
                ReasonCode = workerClassification?.ReasonCode,
                Message = result.Failure.Message ?? result.Result.StopReason,
                Details = result.Next.Suggested,
                StackTrace = null,
                NextAction = workerClassification?.NextAction,
            },
            Attribution = workerArtifact is null
                ? classificationService.BuildAttribution(result)
                : classificationService.BuildAttribution(
                    new RuntimeFailureRecord
                    {
                        FailureType = RuntimeFailureType.WorkerExecutionFailure,
                        TaskId = task.TaskId,
                        Reason = result.Failure.Message ?? result.Result.StopReason,
                    },
                    workerArtifact.Result),
            Review = new FailureReviewSummary
            {
                Required = task.RequiresReviewBoundary,
                Rejected = classification == FailureType.ReviewRejected,
                Reason = task.PlannerReview.Reason,
            },
            ContextSnapshot = new FailureContextSnapshot
            {
                State = task.Status.ToString(),
                PreviousFailures = previousFailures,
                RetryCount = task.RetryCount,
            },
        };

        repository.Append(report);
        return report;
    }

    public IReadOnlyList<FailureReport> LoadAll()
    {
        return repository.LoadAll();
    }

    private static string ResolveFailureStatus(TaskNode? task)
    {
        return task?.Status switch
        {
            Carves.Runtime.Domain.Tasks.TaskStatus.Blocked => "blocked",
            Carves.Runtime.Domain.Tasks.TaskStatus.Failed => "failed",
            Carves.Runtime.Domain.Tasks.TaskStatus.Review => "rejected",
            _ => "failed",
        };
    }

    private static string? ResolveWorktree(WorkerExecutionResult? workerResult)
    {
        return workerResult?.CommandTrace
            .Select(record => record.WorkingDirectory)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
    }

    private static string EstimateScope(IReadOnlyList<string>? scope)
    {
        var count = scope?.Count ?? 0;
        return count switch
        {
            0 => "unknown",
            <= 2 => "small",
            <= 5 => "medium",
            _ => "large",
        };
    }

    private static string CreateFailureId()
    {
        return $"FAIL-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}"[..33];
    }
}
