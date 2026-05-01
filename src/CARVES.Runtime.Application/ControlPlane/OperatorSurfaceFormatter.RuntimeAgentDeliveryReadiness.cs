namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentDeliveryReadiness(RuntimeAgentDeliveryReadinessSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent delivery readiness",
            $"Boundary document: {surface.BoundaryDocumentPath}",
            $"Guide path: {surface.GuidePath}",
            $"Packaging maturity path: {surface.PackagingMaturityPath}",
            $"First-run packet path: {surface.FirstRunPacketPath}",
            $"Validation bundle guide path: {surface.ValidationBundleGuidePath}",
            $"Trial wrapper path: {surface.TrialWrapperPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Delivery ownership: {surface.DeliveryOwnership}",
            $"Entry lane: {surface.EntryLaneId}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            $"Entry commands: {surface.EntryCommands.Count}",
        };

        lines.AddRange(surface.EntryCommands.Select(item => $"- entry-command: {item}"));
        lines.Add($"Runtime truth files: {surface.RuntimeTruthFiles.Count}");
        lines.AddRange(surface.RuntimeTruthFiles.Select(item => $"- truth-file: {item}"));
        lines.Add($"Derived packaging artifacts: {surface.DerivedPackagingArtifacts.Count}");
        lines.AddRange(surface.DerivedPackagingArtifacts.Select(item => $"- derived-artifact: {item}"));
        lines.Add($"Related surfaces: {surface.RelatedSurfaceRefs.Count}");
        lines.AddRange(surface.RelatedSurfaceRefs.Select(item => $"- related-surface: {item}"));
        lines.Add($"Delivery claims: {surface.DeliveryClaims.Count}");
        lines.AddRange(surface.DeliveryClaims.Select(item => $"- delivery-claim: {item}"));
        lines.Add($"Blocked claims: {surface.BlockedClaims.Count}");
        lines.AddRange(surface.BlockedClaims.Select(item => $"- blocked-claim: {item}"));
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
