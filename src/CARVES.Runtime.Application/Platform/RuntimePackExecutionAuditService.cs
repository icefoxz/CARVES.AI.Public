using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackExecutionAuditService
{
    private const int RunReadLimit = 30;
    private const int ReportReadLimit = 30;
    private const int SurfaceRunLimit = 10;
    private const int SurfaceReportLimit = 10;

    private readonly RuntimePackExecutionAttributionService executionAttributionService;
    private readonly ExecutionRunService executionRunService;
    private readonly ExecutionRunReportService executionRunReportService;

    public RuntimePackExecutionAuditService(
        ControlPlanePaths paths,
        IRuntimeArtifactRepository artifactRepository,
        ExecutionRunService executionRunService)
    {
        executionAttributionService = new RuntimePackExecutionAttributionService(paths.RepoRoot, artifactRepository);
        this.executionRunService = executionRunService;
        executionRunReportService = new ExecutionRunReportService(paths);
    }

    public RuntimePackExecutionAuditSurface Build()
    {
        var currentSelection = executionAttributionService.TryLoadCurrentSelectionReference();
        var recentRuns = executionRunService.ListRecentRuns(RunReadLimit)
            .Where(run => run.SelectedPack is not null)
            .Select(run => MapRun(run, currentSelection))
            .Take(SurfaceRunLimit)
            .ToArray();
        var recentReports = executionRunReportService.ListRecentReports(ReportReadLimit)
            .Where(report => report.SelectedPack is not null)
            .Select(report => MapReport(report, currentSelection))
            .Take(SurfaceReportLimit)
            .ToArray();
        var coverage = BuildCoverage(currentSelection, recentRuns, recentReports);

        return new RuntimePackExecutionAuditSurface
        {
            CurrentSelection = currentSelection,
            Coverage = coverage,
            RecentRuns = recentRuns,
            RecentReports = recentReports,
            Summary = BuildSummary(currentSelection, recentRuns.Length, recentReports.Length),
            Notes =
            [
                "This surface is summary-first local runtime truth over execution runs and run reports that already carry selected pack references.",
                "Audit output is by bounded reference and does not embed full pack blobs or registry inventory.",
                "Registry, rollout, automatic activation, and multi-pack orchestration remain outside the slice."
            ],
        };
    }

    private static RuntimePackExecutionAuditRunEntry MapRun(
        ExecutionRun run,
        RuntimePackExecutionAttribution? currentSelection)
    {
        var selectedPack = run.SelectedPack!;
        var matchesCurrentSelection = MatchesCurrentSelection(selectedPack, currentSelection);
        var occurredAt = run.EndedAtUtc ?? run.StartedAtUtc ?? run.CreatedAtUtc;

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
            DeclarativeContribution = selectedPack.DeclarativeContribution,
            MatchesCurrentDeclarativeContribution = MatchesCurrentDeclarativeContribution(selectedPack, currentSelection),
            Summary = $"{run.RunId} [{run.Status}] used {selectedPack.PackId}@{selectedPack.PackVersion} ({selectedPack.Channel}); declarative={selectedPack.DeclarativeContribution?.Summary ?? "none"}."
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
            DeclarativeContribution = selectedPack.DeclarativeContribution,
            MatchesCurrentDeclarativeContribution = MatchesCurrentDeclarativeContribution(selectedPack, currentSelection),
            Summary = $"{report.RunId} report captured {selectedPack.PackId}@{selectedPack.PackVersion} ({selectedPack.Channel}); files_changed={report.FilesChanged}; declarative={selectedPack.DeclarativeContribution?.Summary ?? "none"}."
        };
    }

    private static RuntimePackExecutionAuditCoverage BuildCoverage(
        RuntimePackExecutionAttribution? currentSelection,
        IReadOnlyCollection<RuntimePackExecutionAuditRunEntry> recentRuns,
        IReadOnlyCollection<RuntimePackExecutionAuditReportEntry> recentReports)
    {
        var currentRunCount = recentRuns.Count(entry => entry.MatchesCurrentSelection);
        var currentReportCount = recentReports.Count(entry => entry.MatchesCurrentSelection);
        var declarativeRunCount = recentRuns.Count(entry => entry.DeclarativeContribution is not null);
        var declarativeReportCount = recentReports.Count(entry => entry.DeclarativeContribution is not null);
        var currentContributionRunCount = recentRuns.Count(entry => entry.MatchesCurrentDeclarativeContribution);
        var currentContributionReportCount = recentReports.Count(entry => entry.MatchesCurrentDeclarativeContribution);
        var divergentContributionRunCount = declarativeRunCount - currentContributionRunCount;
        var divergentContributionReportCount = declarativeReportCount - currentContributionReportCount;

        return new RuntimePackExecutionAuditCoverage
        {
            HasCurrentSelection = currentSelection is not null,
            RecentAttributedRunCount = recentRuns.Count,
            RecentAttributedReportCount = recentReports.Count,
            CurrentSelectionRunCount = currentRunCount,
            CurrentSelectionReportCount = currentReportCount,
            RecentDeclarativeRunCount = declarativeRunCount,
            RecentDeclarativeReportCount = declarativeReportCount,
            CurrentSelectionContributionRunCount = currentContributionRunCount,
            CurrentSelectionContributionReportCount = currentContributionReportCount,
            DivergentContributionRunCount = divergentContributionRunCount,
            DivergentContributionReportCount = divergentContributionReportCount,
            LatestRunId = recentRuns.FirstOrDefault()?.RunId,
            LatestReportRunId = recentReports.FirstOrDefault()?.RunId,
            Summary = currentSelection is null
                ? $"No current selected pack is recorded. Recent attributed runs={recentRuns.Count}, reports={recentReports.Count}."
                : $"Current selection {currentSelection.PackId}@{currentSelection.PackVersion} ({currentSelection.Channel}) appears in runs={currentRunCount}/{recentRuns.Count}, reports={currentReportCount}/{recentReports.Count}; declarative contribution matches runs={currentContributionRunCount}/{declarativeRunCount}, reports={currentContributionReportCount}/{declarativeReportCount}.",
        };
    }

    private static string BuildSummary(
        RuntimePackExecutionAttribution? currentSelection,
        int recentRuns,
        int recentReports)
    {
        return currentSelection is null
            ? $"Runtime pack execution audit has no current selection; recent attributed runs={recentRuns}, reports={recentReports}."
            : $"Runtime pack execution audit tracks current selection {currentSelection.PackId}@{currentSelection.PackVersion} ({currentSelection.Channel}) with recent attributed runs={recentRuns} and reports={recentReports}.";
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

    private static bool MatchesCurrentDeclarativeContribution(
        RuntimePackExecutionAttribution selectedPack,
        RuntimePackExecutionAttribution? currentSelection)
    {
        var selectedContribution = selectedPack.DeclarativeContribution;
        var currentContribution = currentSelection?.DeclarativeContribution;
        if (selectedContribution is null || currentContribution is null)
        {
            return selectedContribution is null && currentContribution is null;
        }

        return string.Equals(
            selectedContribution.ContributionFingerprint,
            currentContribution.ContributionFingerprint,
            StringComparison.Ordinal);
    }
}
