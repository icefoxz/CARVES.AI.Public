namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeFrozenDistTargetReadbackProof(RuntimeFrozenDistTargetReadbackProofSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime frozen dist target readback proof",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Guide document: {surface.GuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Alias command entry: {surface.AliasCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Frozen dist target readback proof complete: {surface.FrozenDistTargetReadbackProofComplete}",
            $"CLI invocation posture: {surface.CliInvocationPosture}",
            $"CLI invocation contract complete: {surface.CliInvocationContractComplete}",
            $"CLI activation posture: {surface.CliActivationPosture}",
            $"CLI activation plan complete: {surface.CliActivationPlanComplete}",
            $"Target agent bootstrap posture: {surface.TargetAgentBootstrapPosture}",
            $"Target agent bootstrap ready: {surface.TargetAgentBootstrapReady}",
            $"Local dist freshness smoke posture: {surface.LocalDistFreshnessSmokePosture}",
            $"Local dist freshness smoke ready: {surface.LocalDistFreshnessSmokeReady}",
            $"Local dist freshness smoke source commit: {EmptyAsNone(surface.LocalDistFreshnessSmokeSourceCommit)}",
            $"Target dist binding plan posture: {surface.TargetDistBindingPlanPosture}",
            $"Target dist binding plan complete: {surface.TargetDistBindingPlanComplete}",
            $"Target bound to local dist: {surface.TargetBoundToLocalDist}",
            $"Target dist recommended binding mode: {surface.TargetDistRecommendedBindingMode}",
            $"Local dist handoff posture: {surface.LocalDistHandoffPosture}",
            $"Stable external consumption ready: {surface.StableExternalConsumptionReady}",
            $"Runtime root kind: {surface.RuntimeRootKind}",
            $"Runtime dist manifest version: {EmptyAsNone(surface.RuntimeDistManifestVersion)}",
            $"Runtime dist manifest source commit: {EmptyAsNone(surface.RuntimeDistManifestSourceCommit)}",
            $"Runtime initialized: {surface.RuntimeInitialized}",
            $"Git repository detected: {surface.GitRepositoryDetected}",
            $"Target git worktree clean: {surface.TargetGitWorktreeClean}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Required source readback commands:",
        };

        lines.AddRange(surface.RequiredSourceReadbackCommands.Count == 0
            ? ["- none"]
            : surface.RequiredSourceReadbackCommands.Select(command => $"- {command}"));
        lines.Add("Required target readback commands:");
        lines.AddRange(surface.RequiredTargetReadbackCommands.Count == 0
            ? ["- none"]
            : surface.RequiredTargetReadbackCommands.Select(command => $"- {command}"));
        lines.Add("Boundary rules:");
        lines.AddRange(surface.BoundaryRules.Count == 0
            ? ["- none"]
            : surface.BoundaryRules.Select(rule => $"- {rule}"));
        lines.Add("Gaps:");
        lines.AddRange(surface.Gaps.Count == 0
            ? ["- none"]
            : surface.Gaps.Select(gap => $"- {gap}"));
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
