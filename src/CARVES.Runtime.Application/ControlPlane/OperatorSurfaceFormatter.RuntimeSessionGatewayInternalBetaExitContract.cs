using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeSessionGatewayInternalBetaExitContract(RuntimeSessionGatewayInternalBetaExitContractSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime Session Gateway internal beta exit contract",
            $"Exit contract doc: {surface.ExitContractPath}",
            $"Release surface doc: {surface.ReleaseSurfacePath}",
            $"Internal beta gate doc: {surface.InternalBetaGatePath}",
            $"First-run packet doc: {surface.FirstRunPacketPath}",
            $"Operator proof contract doc: {surface.OperatorProofContractPath}",
            $"Alpha setup doc: {surface.AlphaSetupPath}",
            $"Alpha quickstart doc: {surface.AlphaQuickstartPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Internal beta gate posture: {surface.InternalBetaGatePosture}",
            $"First-run packet posture: {surface.FirstRunPacketPosture}",
            $"Truth owner: {surface.TruthOwner}",
            $"Contract ownership: {surface.ContractOwnership}",
            $"Recommended next action: {surface.RecommendedNextAction}",
        };

        AppendSessionGatewayOperatorProofContract(lines, surface.OperatorProofContract);
        lines.Add($"Samples: {surface.Samples.Count}");
        lines.AddRange(surface.Samples.Select(sample =>
            $"- sample: {sample.SampleId} repo={sample.RepoId} class={sample.SampleClass} weight={sample.EvidenceWeight} representative={sample.CountsAsRepresentativeEvidence} verdict={sample.Verdict} bootstrap={sample.BootstrapBehaviorJudgment}"));
        lines.AddRange(surface.Samples.Select(sample => $"- sample-packet: {sample.PacketPath}"));
        lines.AddRange(surface.Samples.Select(sample => $"- sample-evidence: {sample.EvidencePath}"));
        lines.AddRange(surface.Samples.Select(sample => $"- sample-summary: {sample.Summary}"));
        lines.Add($"Representative evidence basis: {surface.RepresentativeEvidenceBasis.Count}");
        lines.AddRange(surface.RepresentativeEvidenceBasis.Select(item => $"- representative-basis: {item}"));
        lines.Add($"Exit criteria: {surface.ExitCriteria.Count}");
        lines.AddRange(surface.ExitCriteria.Select(item => $"- exit: {item}"));
        lines.Add($"Blocked claims: {surface.BlockedClaims.Count}");
        lines.AddRange(surface.BlockedClaims.Select(item => $"- blocked-claim: {item}"));
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
