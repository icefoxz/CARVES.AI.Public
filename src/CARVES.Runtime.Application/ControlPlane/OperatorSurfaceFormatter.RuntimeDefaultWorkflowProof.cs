namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeDefaultWorkflowProof(RuntimeDefaultWorkflowProofSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime default workflow proof",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Workflow proof complete: {surface.WorkflowProofComplete}",
            $"Current runtime ready: {surface.CurrentRuntimeReady}",
            $"Current runtime posture: {surface.CurrentRuntimePosture}",
            $"Current stage: {surface.CurrentStageId}; status={surface.CurrentStageStatus}",
            $"Next governed command: {surface.NextGovernedCommand}",
            $"Next command source: {surface.NextCommandSource}",
            $"Default first-thread commands: {surface.DefaultFirstThreadCommandCount}",
            $"Default warm-reorientation commands: {surface.DefaultWarmReorientationCommandCount}",
            $"Optional troubleshooting/proof commands: {surface.OptionalTroubleshootingCommandCount}",
            $"Post-init Markdown tokens: {surface.PostInitializationMarkdownTokens}/{surface.PostInitializationMarkdownTokenBudget}",
            $"Deferred generated Markdown tokens: {surface.DeferredGeneratedMarkdownTokens}",
            $"Short context ready: {surface.ShortContextReady}",
            $"Markdown read-path within budget: {surface.MarkdownReadPathWithinBudget}",
            $"Governance surface coverage complete: {surface.GovernanceSurfaceCoverageComplete}",
            $"Resource pack covers default commands: {surface.ResourcePackCoversDefaultCommands}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Default first-thread path:",
        };

        AppendWorkflowSteps(lines, surface.DefaultPath);
        lines.Add("Default warm path:");
        AppendWorkflowSteps(lines, surface.WarmPath);
        lines.Add("Optional proof/troubleshooting path:");
        AppendWorkflowSteps(lines, surface.OptionalProofAndTroubleshootingPath);
        lines.Add("Checks:");
        lines.AddRange(surface.Checks.Count == 0
            ? ["- none"]
            : surface.Checks.Select(check => $"- {check.CheckId}: passed={check.Passed}; blocking={check.Blocking}; {check.Summary}"));
        lines.Add("Structural gaps:");
        lines.AddRange(surface.StructuralGaps.Count == 0
            ? ["- none"]
            : surface.StructuralGaps.Select(gap => $"- {gap}"));
        lines.Add("Current runtime blockers:");
        lines.AddRange(surface.CurrentRuntimeBlockers.Count == 0
            ? ["- none"]
            : surface.CurrentRuntimeBlockers.Select(gap => $"- {gap}"));
        lines.Add("Evidence source paths:");
        lines.AddRange(surface.EvidenceSourcePaths.Count == 0
            ? ["- none"]
            : surface.EvidenceSourcePaths.Select(path => $"- {path}"));
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));

        return new OperatorCommandResult(surface.WorkflowProofComplete ? 0 : 1, lines);
    }

    private static void AppendWorkflowSteps(List<string> lines, IReadOnlyList<RuntimeDefaultWorkflowProofStep> steps)
    {
        lines.AddRange(steps.Count == 0
            ? ["- none"]
            : steps.Select(step => $"- {step.StepId}: {step.Command} | surface={step.SurfaceId} | lane={step.Lane} | required={step.RequiredInDefaultPath} | status={step.Status} | evidence={step.Evidence}"));
    }
}
