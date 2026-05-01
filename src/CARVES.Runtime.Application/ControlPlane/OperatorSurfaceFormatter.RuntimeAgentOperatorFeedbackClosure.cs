namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentOperatorFeedbackClosure(RuntimeAgentOperatorFeedbackClosureSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent operator feedback closure",
            $"Boundary document: {surface.BoundaryDocumentPath}",
            $"Guide path: {surface.GuidePath}",
            $"Delivery readiness guide path: {surface.DeliveryReadinessGuidePath}",
            $"First-run packet path: {surface.FirstRunPacketPath}",
            $"Validation bundle guide path: {surface.ValidationBundleGuidePath}",
            $"Failure recovery path: {surface.FailureRecoveryPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Feedback ownership: {surface.FeedbackOwnership}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            $"Feedback bundles: {surface.FeedbackBundles.Count}",
        };

        foreach (var bundle in surface.FeedbackBundles)
        {
            lines.Add($"- bundle: {bundle.BundleId}");
            lines.Add($"  trigger-state: {bundle.TriggerState}");
            lines.Add($"  summary: {bundle.Summary}");
            lines.Add($"  commands: {(bundle.Commands.Count == 0 ? "(none)" : string.Join(", ", bundle.Commands))}");
            lines.AddRange(bundle.Notes.Select(item => $"  note: {item}"));
        }

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
