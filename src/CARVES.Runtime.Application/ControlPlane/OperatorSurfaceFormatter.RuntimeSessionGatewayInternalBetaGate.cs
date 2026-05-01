using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeSessionGatewayInternalBetaGate(RuntimeSessionGatewayInternalBetaGateSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime Session Gateway internal beta gate",
            $"Execution plan: {surface.ExecutionPlanPath}",
            $"Release surface: {surface.ReleaseSurfacePath}",
            $"Internal beta doc: {surface.InternalBetaGatePath}",
            $"Repeatability doc: {surface.RepeatabilityReadinessPath}",
            $"Operator proof contract doc: {surface.OperatorProofContractPath}",
            $"Alpha setup doc: {surface.AlphaSetupPath}",
            $"Alpha quickstart doc: {surface.AlphaQuickstartPath}",
            $"Successful proof packet: {surface.SuccessfulProofPacketPath}",
            $"Successful proof evidence: {surface.SuccessfulProofEvidencePath}",
            $"Previous failure packet: {surface.PreviousFailurePacketPath}",
            $"Previous failure evidence: {surface.PreviousFailureEvidencePath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Private alpha handoff posture: {surface.PrivateAlphaHandoffPosture}",
            $"Repeatability posture: {surface.RepeatabilityPosture}",
            $"Broker mode: {surface.BrokerMode}",
            $"Truth owner: {surface.TruthOwner}",
            $"Gate ownership: {surface.GateOwnership}",
            $"Thin shell route: {surface.ThinShellRoute}",
            $"Session collection route: {surface.SessionCollectionRoute}",
            $"Message route: {surface.MessageRouteTemplate}",
            $"Events route: {surface.EventsRouteTemplate}",
            $"Accepted operation route: {surface.AcceptedOperationRouteTemplate}",
            $"Supported intents: {string.Join(", ", surface.SupportedIntents)}",
            $"Recommended next action: {surface.RecommendedNextAction}",
        };

        AppendSessionGatewayOperatorProofContract(lines, surface.OperatorProofContract);
        lines.Add($"Included scope: {surface.IncludedScope.Count}");
        lines.AddRange(surface.IncludedScope.Select(item => $"- scope: {item}"));
        lines.Add($"Blocked claims: {surface.BlockedClaims.Count}");
        lines.AddRange(surface.BlockedClaims.Select(item => $"- blocked-claim: {item}"));
        lines.Add($"Required evidence bundle: {surface.RequiredEvidenceBundle.Count}");
        lines.AddRange(surface.RequiredEvidenceBundle.Select(item => $"- evidence: {item}"));
        lines.Add($"Entry commands: {surface.EntryCommands.Count}");
        lines.AddRange(surface.EntryCommands.Select(item => $"- entry: {item}"));
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
