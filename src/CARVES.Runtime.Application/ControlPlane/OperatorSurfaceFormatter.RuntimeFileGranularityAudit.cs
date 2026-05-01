namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeFileGranularityAudit(RuntimeFileGranularityAuditSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime file granularity audit",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Repo root: {surface.RepoRoot}",
            $"Lifecycle class: {surface.LifecycleClass}",
            $"Read-path class: {surface.ReadPathClass}",
            $"Default-path participation: {surface.DefaultPathParticipation}",
            $"Overall posture: {surface.OverallPosture}",
            $"Valid: {surface.IsValid}",
            $"Scan roots: {string.Join(", ", surface.ScanRoots)}",
            $"Excluded directories: {string.Join(", ", surface.ExcludedDirectoryNames)}",
            $"Thresholds: tiny<={surface.Thresholds.TinyFileLineThreshold}; small<={surface.Thresholds.SmallFileLineThreshold}; large>={surface.Thresholds.LargeFileLineThreshold}; huge>={surface.Thresholds.HugeFileLineThreshold}",
            $"Counts: files={surface.Counts.TotalFileCount}; src={surface.Counts.SourceFileCount}; tests={surface.Counts.TestFileCount}; lines={surface.Counts.TotalPhysicalLineCount}; non_blank={surface.Counts.TotalNonBlankLineCount}; avg={surface.Counts.AveragePhysicalLinesPerFile}; median={surface.Counts.MedianPhysicalLinesPerFile}",
            $"Granularity classes: tiny={surface.Counts.TinyFileCount}; small={surface.Counts.SmallFileCount}; medium={surface.Counts.MediumFileCount}; large={surface.Counts.LargeFileCount}; huge={surface.Counts.HugeFileCount}; tiny_ratio={surface.Counts.TinyFileRatio}; small_ratio={surface.Counts.SmallFileRatio}",
            $"Clusters: tiny_clusters={surface.Counts.TinyClusterCount}; partial_family_clusters={surface.Counts.PartialFamilyClusterCount}; directory_pressure={surface.Counts.DirectoryPressureCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Findings:",
        };

        lines.AddRange(surface.Findings.Count == 0 ? ["- none"] : surface.Findings.Select(finding => $"- {finding}"));
        lines.Add("Project rollups:");
        lines.AddRange(surface.ProjectRollups.Count == 0
            ? ["- none"]
            : surface.ProjectRollups.Select(rollup => $"- {rollup.ProjectRoot}: files={rollup.FileCount}; lines={rollup.TotalPhysicalLineCount}; avg={rollup.AveragePhysicalLinesPerFile}; tiny={rollup.TinyFileCount}; small={rollup.SmallFileCount}; large={rollup.LargeFileCount}; huge={rollup.HugeFileCount}"));
        lines.Add("Directory pressure:");
        lines.AddRange(surface.DirectoryPressure.Count == 0
            ? ["- none"]
            : surface.DirectoryPressure.Select(rollup => $"- {rollup.DirectoryPath}: score={rollup.PressureScore}; files={rollup.FileCount}; tiny={rollup.TinyFileCount}; small={rollup.SmallFileCount}; large={rollup.LargeFileCount}; huge={rollup.HugeFileCount}; examples={FormatExamples(rollup.ExampleFiles)}"));
        lines.Add("Tiny clusters:");
        lines.AddRange(surface.TinyClusters.Count == 0
            ? ["- none"]
            : surface.TinyClusters.Select(cluster => $"- {cluster.DirectoryPath}: tiny={cluster.TinyFileCount}; small={cluster.SmallFileCount}; avg_tiny={cluster.AveragePhysicalLinesPerTinyFile}; posture={cluster.ReviewPosture}; examples={FormatExamples(cluster.ExampleFiles)}"));
        lines.Add("Partial-family clusters:");
        lines.AddRange(surface.PartialFamilyClusters.Count == 0
            ? ["- none"]
            : surface.PartialFamilyClusters.Select(cluster => $"- {cluster.FamilyName} @ {cluster.DirectoryPath}: files={cluster.FileCount}; lines={cluster.TotalPhysicalLineCount}; avg={cluster.AveragePhysicalLinesPerFile}; tiny={cluster.TinyFileCount}; posture={cluster.ReviewPosture}"));
        lines.Add("Cleanup candidates:");
        lines.AddRange(surface.CleanupCandidates.Count == 0
            ? ["- none"]
            : surface.CleanupCandidates.Select(candidate => $"- {candidate.CandidateId}: type={candidate.CandidateType}; priority={candidate.Priority}; score={candidate.PressureScore}; scope={candidate.ScopePath}; action={candidate.RecommendedAction}; files={FormatExamples(candidate.Files)}"));
        lines.Add("Cleanup selection:");
        lines.Add($"- strategy={surface.CleanupSelection.Strategy}; selected={surface.CleanupSelection.SelectedCandidateCount}/{surface.CleanupSelection.MaxBatchSize}; candidates={surface.CleanupSelection.CandidateCount}; deferred={surface.CleanupSelection.DeferredCandidateCount}");
        lines.AddRange(surface.CleanupSelection.SelectedCandidates.Count == 0
            ? ["- selected: none"]
            : surface.CleanupSelection.SelectedCandidates.Select(candidate => $"- selected[{candidate.SelectionRank}] {candidate.CandidateId}: type={candidate.CandidateType}; risk={candidate.RiskClass}; score={candidate.PressureScore}; scope={candidate.ScopePath}; reason={candidate.SelectionReason}"));
        lines.Add("Cleanup selection rules:");
        lines.AddRange(surface.CleanupSelection.EligibilityRules.Select(rule => $"- eligible: {rule}"));
        lines.AddRange(surface.CleanupSelection.DeferralRules.Select(rule => $"- defer: {rule}"));
        lines.Add("Largest files:");
        lines.AddRange(surface.LargestFiles.Count == 0
            ? ["- none"]
            : surface.LargestFiles.Select(file => $"- {file.Path}: lines={file.PhysicalLineCount}; class={file.FileClass}; review={file.SuggestedReviewPosture}"));
        lines.Add("Smallest files:");
        lines.AddRange(surface.SmallestFiles.Count == 0
            ? ["- none"]
            : surface.SmallestFiles.Select(file => $"- {file.Path}: lines={file.PhysicalLineCount}; class={file.FileClass}; review={file.SuggestedReviewPosture}"));
        lines.Add("Errors:");
        lines.AddRange(surface.Errors.Count == 0 ? ["- none"] : surface.Errors.Select(error => $"- {error}"));
        lines.Add("Warnings:");
        lines.AddRange(surface.Warnings.Count == 0 ? ["- none"] : surface.Warnings.Select(warning => $"- {warning}"));
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));

        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }

    private static string FormatExamples(IReadOnlyCollection<string> examples)
    {
        return examples.Count == 0 ? "none" : string.Join(", ", examples);
    }
}
