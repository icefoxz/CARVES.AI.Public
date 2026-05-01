namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAlphaExternalUseReadiness(RuntimeAlphaExternalUseReadinessSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime alpha external-use readiness",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Alpha version: {surface.AlphaVersion}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Alias command entry: {surface.AliasCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Alpha external use ready: {surface.AlphaExternalUseReady}",
            $"Frozen local dist ready: {surface.FrozenLocalDistReady}",
            $"External consumer resource pack ready: {surface.ExternalConsumerResourcePackReady}",
            $"Governed agent handoff ready: {surface.GovernedAgentHandoffReady}",
            $"Productized pilot guide ready: {surface.ProductizedPilotGuideReady}",
            $"Session Gateway private alpha ready: {surface.SessionGatewayPrivateAlphaReady}",
            $"Session Gateway repeatability ready: {surface.SessionGatewayRepeatabilityReady}",
            $"Product pilot proof required per target: {surface.ProductPilotProofRequiredPerTarget}",
            $"Candidate dist root: {surface.CandidateDistRoot}",
            $"Dist manifest source commit: {surface.DistManifestSourceCommit}",
            $"Source git head: {surface.SourceGitHead}",
            $"Source git worktree clean: {surface.SourceGitWorktreeClean}",
            $"Dist manifest matches source head: {surface.DistManifestMatchesSourceHead}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Readiness checks:",
        };

        lines.AddRange(surface.ReadinessChecks.Count == 0
            ? ["- none"]
            : surface.ReadinessChecks.Select(check => $"- {check.CheckId}: ready={check.Ready}; blocks_alpha_use={check.BlocksAlphaUse}; surface={check.SurfaceId}; posture={check.Posture}; summary={check.Summary}"));
        lines.Add("Minimum operator readbacks:");
        lines.AddRange(surface.MinimumOperatorReadbacks.Count == 0
            ? ["- none"]
            : surface.MinimumOperatorReadbacks.Select(command => $"- {command}"));
        lines.Add("External target start commands:");
        lines.AddRange(surface.ExternalTargetStartCommands.Count == 0
            ? ["- none"]
            : surface.ExternalTargetStartCommands.Select(command => $"- {command}"));
        lines.Add("Boundary rules:");
        lines.AddRange(surface.BoundaryRules.Count == 0
            ? ["- none"]
            : surface.BoundaryRules.Select(rule => $"- {rule}"));
        lines.Add("Gaps:");
        lines.AddRange(surface.Gaps.Count == 0
            ? ["- none"]
            : surface.Gaps.Select(gap => $"- {gap}"));
        lines.Add("Warnings:");
        lines.AddRange(surface.Warnings.Count == 0
            ? ["- none"]
            : surface.Warnings.Select(warning => $"- warning: {warning}"));
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
