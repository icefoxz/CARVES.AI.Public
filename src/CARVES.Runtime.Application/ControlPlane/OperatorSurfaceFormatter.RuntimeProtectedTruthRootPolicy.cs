namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeProtectedTruthRootPolicy(RuntimeProtectedTruthRootPolicySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime protected truth-root policy",
            $"Policy document: {surface.PolicyDocumentPath}",
            $"Project boundary: {surface.ProjectBoundaryPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Enforcement anchor: {surface.EnforcementAnchor}",
            $"Baseline portability: {surface.BaselinePortability}",
            $"Protected roots: {surface.ProtectedRoots.Count}",
        };

        foreach (var root in surface.ProtectedRoots)
        {
            lines.Add($"- protected-root: {root.Root} | classification={root.Classification} | outcome={root.UnauthorizedMutationOutcome}");
            lines.Add($"  - allowed-channel: {root.AllowedMutationChannel}");
            lines.Add($"  - remediation: {root.RemediationAction}");
            lines.Add($"  - examples: {FormatList(root.Examples)}");
        }

        lines.Add($"Denied roots: {surface.DeniedRoots.Count}");
        foreach (var root in surface.DeniedRoots)
        {
            lines.Add($"- denied-root: {root.Root} | classification={root.Classification} | outcome={root.UnauthorizedMutationOutcome}");
            lines.Add($"  - remediation: {root.RemediationAction}");
        }

        lines.Add($"Recommended next action: {surface.RecommendedNextAction}");
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
