using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeFirstRunOperatorPacket(RuntimeFirstRunOperatorPacketSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime first-run operator packet",
            $"Packet doc: {surface.PacketPath}",
            $"Internal beta gate doc: {surface.InternalBetaGatePath}",
            $"Trusted bootstrap truth doc: {surface.TrustedBootstrapTruthPath}",
            $"Onboarding acceleration doc: {surface.OnboardingAccelerationContractPath}",
            $"Alpha setup doc: {surface.AlphaSetupPath}",
            $"Alpha quickstart doc: {surface.AlphaQuickstartPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Overall posture: {surface.OverallPosture}",
            $"Internal beta gate posture: {surface.InternalBetaGatePosture}",
            $"Current proof source: {surface.CurrentProofSource}",
            $"Current operator state: {surface.CurrentOperatorState}",
            $"Truth owner: {surface.TruthOwner}",
            $"Packet ownership: {surface.PacketOwnership}",
            $"Recommended next action: {surface.RecommendedNextAction}",
        };

        lines.Add($"Project identification fields: {surface.ProjectIdentification.Count}");
        lines.AddRange(surface.ProjectIdentification.Select(item => $"- project-id: {item}"));
        lines.Add($"Bootstrap truth families: {surface.BootstrapTruthFamilies.Count}");
        lines.AddRange(surface.BootstrapTruthFamilies.Select(item => $"- bootstrap-family: {item}"));
        lines.Add($"Required operator actions: {surface.RequiredOperatorActions.Count}");
        lines.AddRange(surface.RequiredOperatorActions.Select(item => $"- operator-action: {item}"));
        lines.Add($"Allowed AI assistance: {surface.AllowedAiAssistance.Count}");
        lines.AddRange(surface.AllowedAiAssistance.Select(item => $"- ai-assist: {item}"));
        lines.Add($"Exit criteria: {surface.ExitCriteria.Count}");
        lines.AddRange(surface.ExitCriteria.Select(item => $"- exit: {item}"));
        lines.Add($"Required evidence bundle: {surface.RequiredEvidenceBundle.Count}");
        lines.AddRange(surface.RequiredEvidenceBundle.Select(item => $"- evidence: {item}"));
        lines.Add($"Entry commands: {surface.EntryCommands.Count}");
        lines.AddRange(surface.EntryCommands.Select(item => $"- entry: {item}"));
        lines.Add($"Minimum onboarding reads: {surface.MinimumOnboardingReads.Count}");
        lines.AddRange(surface.MinimumOnboardingReads.Select(item => $"- onboarding-read: {item}"));
        lines.Add($"Minimum onboarding next steps: {surface.MinimumOnboardingNextSteps.Count}");
        lines.AddRange(surface.MinimumOnboardingNextSteps.Select(item => $"- onboarding-next: {item}"));
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
