using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    private static void AppendSessionGatewayOperatorProofContract(List<string> lines, SessionGatewayOperatorProofContractSurface contract)
    {
        lines.Add($"Operator proof current source: {contract.CurrentProofSource}");
        lines.Add($"Operator proof current state: {contract.CurrentOperatorState}");
        lines.Add($"Operator action required: {contract.OperatorActionRequired}");
        lines.Add($"Real-world proof missing: {contract.RealWorldProofMissing}");
        lines.Add($"Proof contract blocking summary: {contract.BlockingSummary}");
        lines.Add($"Supported proof sources: {string.Join(", ", contract.SupportedProofSources)}");
        lines.Add($"Blocking event kinds: {string.Join(", ", contract.BlockingEventKinds)}");
        lines.Add($"Shared required evidence: {string.Join(", ", contract.SharedRequiredEvidence)}");
        lines.Add($"Stage exit contracts: {contract.StageExitContracts.Count}");

        foreach (var contractStage in contract.StageExitContracts)
        {
            lines.Add($"- stage:{contractStage.StageId} state={contractStage.BlockingState}");
            lines.Add($"  accepted proof sources: {string.Join(", ", contractStage.AcceptedProofSources)}");
            lines.Add($"  required events: {string.Join(", ", contractStage.RequiredEventKinds)}");
            lines.Add($"  operator must do: {string.Join(" | ", contractStage.OperatorMustDo)}");
            lines.Add($"  AI may do: {string.Join(" | ", contractStage.AiMayDo)}");
            lines.Add($"  required evidence: {string.Join(", ", contractStage.RequiredEvidence)}");
            lines.Add($"  non-passing evidence: {string.Join(", ", contractStage.NonPassingEvidence)}");
            lines.Add($"  missing proof summary: {contractStage.MissingProofSummary}");
        }
    }
}
