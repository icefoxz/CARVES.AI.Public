namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAdapterHandoffContract(RuntimeAdapterHandoffContractSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime adapter handoff contract",
            $"Contract document: {surface.ContractDocumentPath}",
            $"Session-gateway document: {surface.SessionGatewayDocumentPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Baseline lane: {surface.BaselineLaneId}",
            $"Authority model: {surface.AuthorityModel}",
            $"Official truth ingress: {surface.OfficialTruthIngressPolicy}",
            $"Lanes: {surface.Lanes.Count}",
        };

        foreach (var lane in surface.Lanes)
        {
            lines.Add($"- lane: {lane.LaneId} | priority={lane.PriorityOrder} | transport={lane.TransportPosture} | status={lane.RuntimeStatus}");
            lines.Add($"  - display: {lane.DisplayName}");
            lines.Add($"  - required-inputs: {FormatList(lane.RequiredInputs)}");
            lines.Add($"  - required-outputs: {FormatList(lane.RequiredOutputs)}");
            lines.Add($"  - allowed-commands: {FormatList(lane.AllowedRuntimeCommands)}");
            lines.Add($"  - non-authority: {FormatList(lane.NonAuthorityBoundaries)}");
            lines.Add($"  - completion: {lane.CompletionSignal}");
        }

        lines.Add("Inspect commands:");
        lines.AddRange(surface.InspectCommands.Select(command => $"- {command}"));
        lines.Add($"Recommended next action: {surface.RecommendedNextAction}");
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
