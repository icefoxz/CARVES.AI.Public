namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentShortContext(RuntimeAgentShortContextSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent short context",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Short context ready: {surface.ShortContextReady}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Requested task: {surface.RequestedTaskId}",
            $"Resolved task: {surface.ResolvedTaskId}",
            $"Task resolution source: {surface.TaskResolutionSource}",
            $"Thread start: ready={surface.ThreadStart.ThreadStartReady}; next={surface.ThreadStart.NextGovernedCommand}; source={surface.ThreadStart.NextCommandSource}",
            $"Thread stage: {surface.ThreadStart.CurrentStageId}; status={surface.ThreadStart.CurrentStageStatus}",
            $"Bootstrap route: {surface.Bootstrap.RecommendedStartupRoute}; startup_mode={surface.Bootstrap.StartupMode}",
            $"Bootstrap current task: {surface.Bootstrap.CurrentTaskId}; active_tasks={surface.Bootstrap.ActiveTaskCount}",
            $"Markdown post-init mode: {surface.Bootstrap.MarkdownPostInitializationMode}",
            $"Governance boundary: {surface.Bootstrap.GovernanceBoundary}",
            $"Task state: {surface.Task.State}",
            $"Task: {surface.Task.TaskId}",
            $"Card: {surface.Task.CardId}",
            $"Title: {surface.Task.Title}",
            $"Task status: {surface.Task.Status}",
            $"Acceptance contract status: {surface.Task.AcceptanceContractStatus}",
            $"Scope file count: {surface.Task.ScopeFileCount}",
            $"Safety layer summary: {surface.Task.SafetyLayerSummary}",
            $"Context pack: {surface.ContextPack.State}; command={surface.ContextPack.Command}",
            $"Context pack budget: {surface.ContextPack.BudgetPosture}; used={surface.ContextPack.UsedTokens}/{surface.ContextPack.MaxContextTokens}",
            $"Markdown read-path budget: {surface.MarkdownBudget.OverallPosture}; default={surface.MarkdownBudget.PostInitializationDefaultTokens}/{surface.MarkdownBudget.PostInitializationMaxTokens}; deferred={surface.MarkdownBudget.DeferredMarkdownTokens}; task_scoped={surface.MarkdownBudget.TaskScopedMarkdownTokens}/{surface.MarkdownBudget.TaskScopedMaxTokens}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Primary commands:",
        };

        AppendCommandRefs(lines, surface.PrimaryCommands);
        lines.Add("Detail refs:");
        AppendCommandRefs(lines, surface.DetailRefs);
        lines.Add("Initialization read sources:");
        AppendItems(lines, surface.InitializationReadSources);
        lines.Add("Minimal agent rules:");
        AppendItems(lines, surface.MinimalAgentRules);
        lines.Add("Task editable roots:");
        AppendItems(lines, surface.Task.EditableRoots);
        lines.Add("Task read-only roots:");
        AppendItems(lines, surface.Task.ReadOnlyRoots);
        lines.Add("Task truth roots:");
        AppendItems(lines, surface.Task.TruthRoots);
        lines.Add("Safety non-claims:");
        AppendItems(lines, surface.Task.SafetyNonClaims);
        lines.Add("Required verification:");
        AppendItems(lines, surface.Task.RequiredVerification);
        lines.Add("Validation commands:");
        AppendItems(lines, surface.Task.ValidationCommands);
        lines.Add("Stop conditions:");
        AppendItems(lines, surface.Task.StopConditions);
        lines.Add("Task-scoped Markdown refs:");
        AppendItems(lines, surface.Task.TaskScopedMarkdownRefs);
        lines.Add("Context pack budget reasons:");
        AppendItems(lines, surface.ContextPack.BudgetReasonCodes);
        lines.Add("Context pack top sources:");
        AppendItems(lines, surface.ContextPack.TopSources);
        lines.Add("Markdown escalation triggers:");
        AppendItems(lines, surface.MarkdownEscalationTriggers);
        lines.Add("Gaps:");
        AppendItems(lines, surface.Gaps);
        lines.Add("Non-claims:");
        AppendItems(lines, surface.NonClaims);
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }

    private static void AppendCommandRefs(List<string> lines, IReadOnlyList<RuntimeAgentShortContextCommandRef> refs)
    {
        if (refs.Count == 0)
        {
            lines.Add("- none");
            return;
        }

        foreach (var item in refs)
        {
            lines.Add($"- {item.Command} [{item.SurfaceId}]");
            lines.Add($"  purpose: {item.Purpose}");
            lines.Add($"  when: {item.When}");
        }
    }

    private static void AppendItems(List<string> lines, IReadOnlyList<string> items)
    {
        lines.AddRange(items.Count == 0 ? ["- none"] : items.Select(item => $"- {item}"));
    }
}
