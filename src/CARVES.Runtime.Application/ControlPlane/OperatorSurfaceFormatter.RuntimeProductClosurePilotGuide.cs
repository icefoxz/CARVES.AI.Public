namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeProductClosurePilotGuide(RuntimeProductClosurePilotGuideSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime product closure pilot guide",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Previous proof document: {surface.PreviousProofDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Status command entry: {surface.StatusCommandEntry}",
            $"Authority model: {surface.AuthorityModel}",
            $"Official truth ingress policy: {surface.OfficialTruthIngressPolicy}",
            "Pilot stages:",
        };

        foreach (var step in surface.Steps)
        {
            lines.Add($"- stage {step.Order}: {step.StageId} | authority={step.AuthorityClass}");
            lines.Add($"  - command: {step.Command}");
            lines.Add($"  - purpose: {step.Purpose}");
            lines.Add($"  - exit: {step.ExitSignal}");
        }

        lines.Add("Commit hygiene:");
        lines.AddRange(surface.CommitHygieneRules.Select(rule => $"- {rule}"));
        lines.Add($"Recommended next action: {surface.RecommendedNextAction}");
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
