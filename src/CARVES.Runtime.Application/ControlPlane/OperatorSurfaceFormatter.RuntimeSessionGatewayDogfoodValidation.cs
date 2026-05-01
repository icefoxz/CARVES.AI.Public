using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeSessionGatewayDogfoodValidation(RuntimeSessionGatewayDogfoodValidationSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime Session Gateway dogfood validation",
            $"Boundary document: {surface.BoundaryDocumentPath}",
            $"Execution plan: {surface.ExecutionPlanPath}",
            $"Release surface: {surface.ReleaseSurfacePath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Program closure verdict: {surface.ProgramClosureVerdict}",
            $"Continuation gate outcome: {surface.ContinuationGateOutcome}",
            $"Broker mode: {surface.BrokerMode}",
            $"Truth owner: {surface.TruthOwner}",
            $"Thin shell posture: {surface.ThinShellPosture}",
            $"Mutation forwarding posture: {surface.MutationForwardingPosture}",
            $"Private alpha posture: {surface.PrivateAlphaPosture}",
            $"Thin shell route: {surface.ThinShellRoute}",
            $"Session collection route: {surface.SessionCollectionRoute}",
            $"Message route: {surface.MessageRouteTemplate}",
            $"Events route: {surface.EventsRouteTemplate}",
            $"Accepted operation route: {surface.AcceptedOperationRouteTemplate}",
            $"Supported intents: {string.Join(", ", surface.SupportedIntents)}",
            $"Validated scenarios: {string.Join(", ", surface.ValidatedScenarios)}",
            $"Deferred follow-ons: {string.Join(", ", surface.DeferredFollowOns)}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            $"Non-claims: {surface.NonClaims.Count}",
        };

        lines.Add($"Operator proof contract doc: {surface.OperatorProofContractPath}");
        AppendSessionGatewayOperatorProofContract(lines, surface.OperatorProofContract);
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
