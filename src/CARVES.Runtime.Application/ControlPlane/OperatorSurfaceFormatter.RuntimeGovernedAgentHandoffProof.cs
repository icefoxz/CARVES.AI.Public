namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeGovernedAgentHandoffProof(RuntimeGovernedAgentHandoffProofSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime governed agent handoff proof",
            $"Proof document: {surface.ProofDocumentPath}",
            $"Session-gateway document: {surface.SessionGatewayDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Product closure baseline: {surface.ProductClosureBaselineDocumentPath}",
            $"Product closure current: {surface.ProductClosureCurrentDocumentPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Proof scope: {surface.ProofScope}",
            $"Control-plane policy: {surface.ActiveControlPlanePolicy}",
            $"Adapter contract posture: {surface.AdapterContractPosture}",
            $"Protected truth-root posture: {surface.ProtectedTruthRootPosture}",
            $"Working-mode recommendation posture: {surface.WorkingModeRecommendationPosture}",
            "Proof stages:",
        };

        foreach (var stage in surface.ProofStages)
        {
            lines.Add($"- stage {stage.Order}: {stage.StageId} | command={stage.RequiredSurfaceOrCommand} | gate={stage.Gate}");
            lines.Add($"  - evidence: {stage.EvidenceProjected}");
        }

        lines.Add("Constraint classes:");
        foreach (var constraint in surface.ConstraintClasses)
        {
            lines.Add($"- constraint: {constraint.ClassId} | enforcement={constraint.EnforcementLevel} | applies={constraint.AppliesTo}");
            lines.Add($"  - summary: {constraint.Summary}");
        }

        lines.Add("Required cold readbacks:");
        lines.AddRange(surface.RequiredColdReadbacks.Select(readback => $"- {readback}"));
        lines.Add($"Recommended next action: {surface.RecommendedNextAction}");
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
