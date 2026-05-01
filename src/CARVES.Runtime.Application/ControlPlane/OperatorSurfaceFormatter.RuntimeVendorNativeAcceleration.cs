namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeVendorNativeAcceleration(RuntimeVendorNativeAccelerationSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime vendor-native acceleration",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Codex governance document: {surface.CodexGovernanceDocumentPath}",
            $"Claude qualification document: {surface.ClaudeQualificationDocumentPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Current mode: {surface.CurrentMode}",
            $"Planning coupling posture: {surface.PlanningCouplingPosture}",
            $"Formal planning posture: {surface.FormalPlanningPosture}",
            $"Plan handle: {surface.PlanHandle ?? "(none)"}",
            $"Planning card: {surface.PlanningCardId ?? "(none)"}",
            $"Managed workspace posture: {surface.ManagedWorkspacePosture}",
            $"Portable foundation summary: {surface.PortableFoundationSummary}",
            $"Codex reinforcement: {surface.CodexReinforcementState}",
            $"Codex reinforcement summary: {surface.CodexReinforcementSummary}",
            $"Codex governance assets: {surface.CodexGovernanceAssets.Count}",
        };

        lines.AddRange(surface.CodexGovernanceAssets.Select(item => $"- codex-asset: {item}"));
        lines.Add($"Claude reinforcement: {surface.ClaudeReinforcementState}");
        lines.Add($"Claude reinforcement summary: {surface.ClaudeReinforcementSummary}");
        lines.Add($"Claude qualified routing intents: {(surface.ClaudeQualifiedRoutingIntents.Count == 0 ? "(none)" : string.Join(", ", surface.ClaudeQualifiedRoutingIntents))}");
        lines.Add($"Claude closed routing intents: {(surface.ClaudeClosedRoutingIntents.Count == 0 ? "(none)" : string.Join(", ", surface.ClaudeClosedRoutingIntents))}");
        lines.Add($"Recommended next action: {surface.RecommendedNextAction}");
        lines.Add($"Non-claims: {surface.NonClaims.Count}");
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
