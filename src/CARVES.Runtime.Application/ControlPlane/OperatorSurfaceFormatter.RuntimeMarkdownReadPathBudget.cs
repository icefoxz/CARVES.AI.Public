namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeMarkdownReadPathBudget(RuntimeMarkdownReadPathBudgetSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime Markdown read-path budget",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Within budget: {surface.WithinBudget}",
            $"Policy id: {surface.PolicyId}",
            $"Estimator: {surface.EstimatorVersion}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Requested task: {surface.RequestedTaskId}",
            $"Resolved task: {surface.ResolvedTaskId}",
            $"Resolved task source: {surface.ResolvedTaskSource}",
            FormatBudgetSummary("Cold initialization", surface.ColdInitialization),
            FormatBudgetSummary("Post-initialization default", surface.PostInitializationDefault),
            FormatBudgetSummary("Generated Markdown views", surface.GeneratedMarkdownViews),
            FormatBudgetSummary("Task-scoped Markdown", surface.TaskScopedMarkdown),
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Default machine read path:",
        };

        AppendItems(lines, surface.DefaultMachineReadPath);
        lines.Add("Deferred Markdown sources:");
        AppendItems(lines, surface.DeferredMarkdownSources);
        lines.Add("Escalation triggers:");
        AppendItems(lines, surface.EscalationTriggers);
        lines.Add("Budget items:");
        AppendBudgetItems(lines, surface.Items);
        lines.Add("Gaps:");
        AppendItems(lines, surface.Gaps);
        lines.Add("Non-claims:");
        AppendItems(lines, surface.NonClaims);
        return new OperatorCommandResult(surface.Gaps.Count == 0 ? 0 : 1, lines);
    }

    private static string FormatBudgetSummary(string label, MarkdownReadPathBudgetSummary summary)
    {
        return $"{label}: default={summary.EstimatedDefaultMarkdownTokens}/{summary.MaxDefaultMarkdownTokens} tokens; deferred={summary.DeferredMarkdownTokens}; sources={summary.DefaultMarkdownSourceCount}/{summary.MaxDefaultMarkdownSources}; largest={summary.LargestItemTokens}; within_budget={summary.WithinBudget}; reasons={string.Join(",", summary.ReasonCodes)}";
    }

    private static void AppendBudgetItems(List<string> lines, IReadOnlyList<MarkdownReadPathBudgetItem> items)
    {
        if (items.Count == 0)
        {
            lines.Add("- none");
            return;
        }

        foreach (var item in items.OrderBy(static item => item.TierId, StringComparer.Ordinal).ThenBy(static item => item.Path, StringComparer.Ordinal))
        {
            lines.Add($"- {item.Path} [{item.TierId}] action={item.ReadAction}; tokens={item.EstimatedTokens}; bytes={item.ByteSize}; exists={item.Exists}; over_single_file_budget={item.OverSingleFileBudget}");
            lines.Add($"  replacement: {item.ReplacementSurface}");
            lines.Add($"  summary: {item.Summary}");
        }
    }
}
