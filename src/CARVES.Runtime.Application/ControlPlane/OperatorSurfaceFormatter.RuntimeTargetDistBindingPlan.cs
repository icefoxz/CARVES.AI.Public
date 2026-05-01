namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeTargetDistBindingPlan(RuntimeTargetDistBindingPlanSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime target dist binding plan",
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
            $"Dist binding plan complete: {surface.DistBindingPlanComplete}",
            $"Recommended binding mode: {surface.RecommendedBindingMode}",
            $"Runtime root kind: {surface.RuntimeRootKind}",
            $"Target runtime initialized: {surface.TargetRuntimeInitialized}",
            $"Target bound to local dist: {surface.TargetBoundToLocalDist}",
            $"Target bound to live source: {surface.TargetBoundToLiveSource}",
            $"Candidate dist root: {surface.CandidateDistRoot}",
            $"Candidate dist exists: {surface.CandidateDistExists}",
            $"Candidate dist has manifest: {surface.CandidateDistHasManifest}",
            $"Candidate dist has version: {surface.CandidateDistHasVersion}",
            $"Candidate dist has wrapper: {surface.CandidateDistHasWrapper}",
            $"Candidate dist version: {surface.CandidateDistVersion}",
            $"Candidate dist source commit: {surface.CandidateDistSourceCommit}",
            $"Current Runtime root matches candidate dist: {surface.CurrentRuntimeRootMatchesCandidateDist}",
            $"Attach handshake Runtime root: {EmptyAsNone(surface.AttachHandshakeRuntimeRoot)}",
            $"Runtime manifest Runtime root: {EmptyAsNone(surface.RuntimeManifestRuntimeRoot)}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Operator binding commands:",
        };

        lines.AddRange(surface.OperatorBindingCommands.Count == 0
            ? ["- none"]
            : surface.OperatorBindingCommands.Select(command => $"- {command}"));
        lines.Add("Required readback commands:");
        lines.AddRange(surface.RequiredReadbackCommands.Count == 0
            ? ["- none"]
            : surface.RequiredReadbackCommands.Select(command => $"- {command}"));
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

    private static string EmptyAsNone(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    }
}
