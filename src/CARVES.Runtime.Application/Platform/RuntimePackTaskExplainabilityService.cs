using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackTaskExplainabilityService
{
    private const int SurfaceRunLimit = 10;
    private const int SurfaceReportLimit = 10;

    private readonly RuntimePackExecutionAttributionService executionAttributionService;
    private readonly ExecutionRunService executionRunService;
    private readonly ExecutionRunReportService executionRunReportService;
    private readonly RuntimePackReviewRubricProjectionService reviewRubricProjectionService;

    public RuntimePackTaskExplainabilityService(
        ControlPlanePaths paths,
        IRuntimeArtifactRepository artifactRepository,
        ExecutionRunService executionRunService)
    {
        executionAttributionService = new RuntimePackExecutionAttributionService(paths.RepoRoot, artifactRepository);
        this.executionRunService = executionRunService;
        executionRunReportService = new ExecutionRunReportService(paths);
        reviewRubricProjectionService = new RuntimePackReviewRubricProjectionService(paths.RepoRoot, artifactRepository);
    }

    public RuntimePackTaskExplainabilitySurface Build(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new InvalidOperationException("Task-scoped pack explainability requires a task id.");
        }

        var normalizedTaskId = taskId.Trim();
        var currentSelection = executionAttributionService.TryLoadCurrentSelectionReference();
        var recentRuns = executionRunService.ListRuns(normalizedTaskId)
            .Where(run => run.SelectedPack is not null)
            .OrderByDescending(run => run.EndedAtUtc ?? run.StartedAtUtc ?? run.CreatedAtUtc)
            .ThenByDescending(run => run.RunId, StringComparer.Ordinal)
            .Select(run => MapRun(run, currentSelection))
            .Take(SurfaceRunLimit)
            .ToArray();
        var recentReports = executionRunReportService.ListReports(normalizedTaskId)
            .Where(report => report.SelectedPack is not null)
            .OrderByDescending(report => report.RecordedAtUtc)
            .ThenByDescending(report => report.RunId, StringComparer.Ordinal)
            .Select(report => MapReport(report, currentSelection))
            .Take(SurfaceReportLimit)
            .ToArray();
        var coverage = BuildCoverage(currentSelection, recentRuns, recentReports);
        var reviewRubricProjection = reviewRubricProjectionService.TryBuildCurrentProjection();

        return new RuntimePackTaskExplainabilitySurface
        {
            TaskId = normalizedTaskId,
            CurrentSelection = currentSelection,
            Coverage = coverage,
            CurrentReviewRubricProjection = reviewRubricProjection,
            RecentRuns = recentRuns,
            RecentReports = recentReports,
            Summary = BuildSummary(normalizedTaskId, currentSelection, recentRuns.Length, recentReports.Length),
            Notes =
            [
                "This surface stays bounded to one task id and local runtime truth.",
                "It extends current selection, execution run, and execution run report truth rather than creating a parallel pack ledger.",
                "Registry, rollout, automatic activation, and multi-pack orchestration remain closed."
            ],
        };
    }

    private static RuntimePackExecutionAuditRunEntry MapRun(
        ExecutionRun run,
        RuntimePackExecutionAttribution? currentSelection)
    {
        var selectedPack = run.SelectedPack!;
        var occurredAt = run.EndedAtUtc ?? run.StartedAtUtc ?? run.CreatedAtUtc;
        var matchesCurrentSelection = MatchesCurrentSelection(selectedPack, currentSelection);
        return new RuntimePackExecutionAuditRunEntry
        {
            RunId = run.RunId,
            TaskId = run.TaskId,
            RunStatus = run.Status,
            PackId = selectedPack.PackId,
            PackVersion = selectedPack.PackVersion,
            Channel = selectedPack.Channel,
            ArtifactRef = selectedPack.ArtifactRef,
            SelectionMode = selectedPack.SelectionMode,
            OccurredAtUtc = occurredAt,
            MatchesCurrentSelection = matchesCurrentSelection,
            Summary = $"{run.RunId} used {selectedPack.PackId}@{selectedPack.PackVersion} ({selectedPack.Channel}) for task {run.TaskId}."
                + $" Declarative={selectedPack.DeclarativeContribution?.Summary ?? "none"}"
        };
    }

    private static RuntimePackExecutionAuditReportEntry MapReport(
        ExecutionRunReport report,
        RuntimePackExecutionAttribution? currentSelection)
    {
        var selectedPack = report.SelectedPack!;
        var matchesCurrentSelection = MatchesCurrentSelection(selectedPack, currentSelection);
        return new RuntimePackExecutionAuditReportEntry
        {
            RunId = report.RunId,
            TaskId = report.TaskId,
            RunStatus = report.RunStatus,
            PackId = selectedPack.PackId,
            PackVersion = selectedPack.PackVersion,
            Channel = selectedPack.Channel,
            ArtifactRef = selectedPack.ArtifactRef,
            SelectionMode = selectedPack.SelectionMode,
            RecordedAtUtc = report.RecordedAtUtc,
            MatchesCurrentSelection = matchesCurrentSelection,
            FilesChanged = report.FilesChanged,
            ModulesTouched = report.ModulesTouched,
            Summary = $"{report.RunId} report captured {selectedPack.PackId}@{selectedPack.PackVersion} ({selectedPack.Channel}) for task {report.TaskId}."
                + $" Declarative={selectedPack.DeclarativeContribution?.Summary ?? "none"}"
        };
    }

    private static RuntimePackTaskExplainabilityCoverage BuildCoverage(
        RuntimePackExecutionAttribution? currentSelection,
        IReadOnlyCollection<RuntimePackExecutionAuditRunEntry> recentRuns,
        IReadOnlyCollection<RuntimePackExecutionAuditReportEntry> recentReports)
    {
        var currentSelectionRunCount = recentRuns.Count(entry => entry.MatchesCurrentSelection);
        var currentSelectionReportCount = recentReports.Count(entry => entry.MatchesCurrentSelection);
        var divergentRunCount = recentRuns.Count - currentSelectionRunCount;
        var divergentReportCount = recentReports.Count - currentSelectionReportCount;
        return new RuntimePackTaskExplainabilityCoverage
        {
            HasCurrentSelection = currentSelection is not null,
            AttributedRunCount = recentRuns.Count,
            AttributedReportCount = recentReports.Count,
            CurrentSelectionRunCount = currentSelectionRunCount,
            CurrentSelectionReportCount = currentSelectionReportCount,
            DivergentRunCount = divergentRunCount,
            DivergentReportCount = divergentReportCount,
            LatestRunId = recentRuns.FirstOrDefault()?.RunId,
            LatestReportRunId = recentReports.FirstOrDefault()?.RunId,
            Summary = currentSelection is null
                ? $"No current selected pack is active. This task still has attributed runs={recentRuns.Count} and reports={recentReports.Count}."
                : $"Current selection {currentSelection.PackId}@{currentSelection.PackVersion} ({currentSelection.Channel}) matches runs={currentSelectionRunCount}/{recentRuns.Count} and reports={currentSelectionReportCount}/{recentReports.Count} for this task.",
        };
    }

    private static string BuildSummary(
        string taskId,
        RuntimePackExecutionAttribution? currentSelection,
        int recentRuns,
        int recentReports)
    {
        return currentSelection is null
            ? $"Runtime pack task explainability for {taskId} has no current selection; recent attributed runs={recentRuns}, reports={recentReports}."
            : $"Runtime pack task explainability for {taskId} compares current selection {currentSelection.PackId}@{currentSelection.PackVersion} ({currentSelection.Channel}) against recent attributed runs={recentRuns} and reports={recentReports}.";
    }

    private static bool MatchesCurrentSelection(
        RuntimePackExecutionAttribution selectedPack,
        RuntimePackExecutionAttribution? currentSelection)
    {
        return currentSelection is not null
               && string.Equals(selectedPack.PackId, currentSelection.PackId, StringComparison.Ordinal)
               && string.Equals(selectedPack.PackVersion, currentSelection.PackVersion, StringComparison.Ordinal)
               && string.Equals(selectedPack.Channel, currentSelection.Channel, StringComparison.Ordinal);
    }
}
