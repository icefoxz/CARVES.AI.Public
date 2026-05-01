namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAcceptanceContractIngressPolicy(RuntimeAcceptanceContractIngressPolicySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime acceptance contract ingress policy",
            $"Policy document: {surface.PolicyDocumentPath}",
            $"Schema path: {surface.SchemaPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Planning truth mutation policy: {surface.PlanningTruthMutationPolicy}",
            $"Execution dispatch policy: {surface.ExecutionDispatchPolicy}",
            $"Policy summary: {surface.PolicySummary}",
            $"Ingresses: {surface.Ingresses.Count}",
        };

        foreach (var ingress in surface.Ingresses)
        {
            lines.Add($"- ingress: {ingress.IngressId} | lane={ingress.LaneKind} | policy={ingress.ContractPolicy} | when_missing={ingress.MissingContractOutcome}");
            lines.AddRange(ingress.Triggers.Select(trigger => $"  - trigger: {trigger}"));
            lines.AddRange(ingress.SourceAnchors.Select(anchor => $"  - source-anchor: {anchor}"));
            lines.Add($"  - recommended-action: {ingress.RecommendedAction}");
        }

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
