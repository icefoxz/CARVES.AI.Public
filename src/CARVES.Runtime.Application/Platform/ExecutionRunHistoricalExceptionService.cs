using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class ExecutionRunHistoricalExceptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly ExecutionRunService executionRunService;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public ExecutionRunHistoricalExceptionService(
        string repoRoot,
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        ExecutionRunService executionRunService,
        IRuntimeArtifactRepository artifactRepository)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.executionRunService = executionRunService;
        this.artifactRepository = artifactRepository;
    }

    public ExecutionRunHistoricalExceptionReport Build()
    {
        var entries = taskGraphService.Load()
            .ListTasks()
            .Select(BuildEntry)
            .Where(static entry => entry is not null)
            .Cast<ExecutionRunHistoricalExceptionEntry>()
            .OrderBy(entry => entry.TaskId, StringComparer.Ordinal)
            .ToArray();

        var report = new ExecutionRunHistoricalExceptionReport
        {
            Entries = entries,
            Summary = entries.Length == 0
                ? "No residual historical execution-run exceptions were detected."
                : $"Detected {entries.Length} residual historical execution-run exception(s).",
        };
        Persist(report);
        return report;
    }

    public ExecutionRunHistoricalExceptionReport? TryLoadLatest()
    {
        var path = GetLatestReportPath(paths);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ExecutionRunHistoricalExceptionReport>(File.ReadAllText(path), JsonOptions);
    }

    public ExecutionRunHistoricalExceptionEntry? TryGet(string taskId)
    {
        var report = TryLoadLatest() ?? Build();
        return report.Entries.FirstOrDefault(entry => string.Equals(entry.TaskId, taskId, StringComparison.Ordinal));
    }

    public static string GetLatestReportPath(ControlPlanePaths paths)
    {
        return Path.Combine(RuntimeArtifactCatalogService.GetSustainabilityRoot(paths), "execution-run-exceptions.json");
    }

    private ExecutionRunHistoricalExceptionEntry? BuildEntry(TaskNode task)
    {
        if (task.Status is not (DomainTaskStatus.Review or DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded))
        {
            return null;
        }

        var latestRun = executionRunService.ListRuns(task.TaskId).LastOrDefault();
        if (latestRun is null || latestRun.Status != ExecutionRunStatus.Failed)
        {
            return null;
        }

        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId);
        if (reviewArtifact is null)
        {
            return null;
        }

        var autoReconcileEligible = IsAutoReconcileEligible(task, reviewArtifact);
        if (autoReconcileEligible)
        {
            return null;
        }

        var categories = Classify(task, reviewArtifact).Distinct().ToArray();
        if (categories.Length == 0)
        {
            categories = [ExecutionRunHistoricalExceptionCategory.HistoricalReviewStateMismatch];
        }

        return new ExecutionRunHistoricalExceptionEntry
        {
            TaskId = task.TaskId,
            CardId = task.CardId,
            TaskStatus = task.Status,
            LatestRunId = latestRun.RunId,
            LatestRunStatus = latestRun.Status.ToString(),
            ReviewDecisionStatus = reviewArtifact.DecisionStatus,
            ReviewResultingStatus = reviewArtifact.ResultingStatus,
            ValidationPassed = reviewArtifact.ValidationPassed,
            SafetyOutcome = reviewArtifact.SafetyOutcome,
            Categories = categories,
            AutoReconcileEligible = false,
            Summary = BuildSummary(task, reviewArtifact, categories),
            RecommendedAction = BuildRecommendedAction(categories, task, reviewArtifact),
            ReviewArtifactRef = ToRepoRelative(Path.Combine(paths.ReviewArtifactsRoot, $"{task.TaskId}.json")),
            RunArtifactRef = ToRepoRelative(Path.Combine(paths.RuntimeRoot, "runs", task.TaskId, $"{latestRun.RunId}.json")),
        };
    }

    private static IEnumerable<ExecutionRunHistoricalExceptionCategory> Classify(TaskNode task, PlannerReviewArtifact reviewArtifact)
    {
        if (reviewArtifact.DecisionStatus == ReviewDecisionStatus.Approved && !reviewArtifact.ValidationPassed)
        {
            yield return ExecutionRunHistoricalExceptionCategory.ApprovedWithoutValidation;
        }

        if (reviewArtifact.SafetyOutcome == SafetyOutcome.Blocked)
        {
            yield return ExecutionRunHistoricalExceptionCategory.SafetyBlockedReviewOverride;
        }

        var substrateFailure = string.Equals(task.Metadata.GetValueOrDefault("execution_substrate_failure"), "true", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(task.Metadata.GetValueOrDefault("execution_failure_lane"), "substrate", StringComparison.OrdinalIgnoreCase)
                               || !string.IsNullOrWhiteSpace(task.Metadata.GetValueOrDefault("execution_substrate_category"));
        if (substrateFailure)
        {
            yield return ExecutionRunHistoricalExceptionCategory.SubstrateFailureReviewOverride;
        }

        if (task.Status != reviewArtifact.ResultingStatus)
        {
            yield return ExecutionRunHistoricalExceptionCategory.HistoricalReviewStateMismatch;
        }
    }

    private static bool IsAutoReconcileEligible(TaskNode task, PlannerReviewArtifact reviewArtifact)
    {
        if (!reviewArtifact.ValidationPassed || reviewArtifact.SafetyOutcome != SafetyOutcome.Allow)
        {
            return false;
        }

        return task.Status switch
        {
            DomainTaskStatus.Review => reviewArtifact.DecisionStatus == ReviewDecisionStatus.PendingReview
                                       && reviewArtifact.ResultingStatus == DomainTaskStatus.Review,
            DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded
                => reviewArtifact.DecisionStatus == ReviewDecisionStatus.Approved,
            _ => false,
        };
    }

    private static string BuildSummary(TaskNode task, PlannerReviewArtifact reviewArtifact, IReadOnlyList<ExecutionRunHistoricalExceptionCategory> categories)
    {
        var categorySummary = string.Join(", ", categories.Select(static category => category.ToString()));
        return $"{task.TaskId} remains {task.Status} while its latest execution run is failed; review decision={reviewArtifact.DecisionStatus}, validation_passed={reviewArtifact.ValidationPassed}, safety_outcome={reviewArtifact.SafetyOutcome}, categories={categorySummary}.";
    }

    private static string BuildRecommendedAction(IReadOnlyList<ExecutionRunHistoricalExceptionCategory> categories, TaskNode task, PlannerReviewArtifact reviewArtifact)
    {
        if (categories.Contains(ExecutionRunHistoricalExceptionCategory.SubstrateFailureReviewOverride))
        {
            return "Preserve failed run truth; inspect substrate failure lineage and record an explicit operator override only if the task truth should remain completed.";
        }

        if (categories.Contains(ExecutionRunHistoricalExceptionCategory.SafetyBlockedReviewOverride))
        {
            return "Preserve failed run truth; inspect the blocked review artifact and either reopen the task or record a bounded human override rationale.";
        }

        if (categories.Contains(ExecutionRunHistoricalExceptionCategory.ApprovedWithoutValidation))
        {
            return "Do not auto-reconcile; the task was approved without validation passing. Inspect the review artifact and decide whether the task truth or run truth should be corrected manually.";
        }

        if (categories.Contains(ExecutionRunHistoricalExceptionCategory.HistoricalReviewStateMismatch))
        {
            return $"Inspect task/review truth mismatch: task status is {task.Status} while review resulting status is {reviewArtifact.ResultingStatus}.";
        }

        return "Inspect the historical task, review artifact, and latest run before making any manual correction.";
    }

    private void Persist(ExecutionRunHistoricalExceptionReport report)
    {
        var path = GetLatestReportPath(paths);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(repoRoot, Path.GetFullPath(path)).Replace('\\', '/');
    }
}
