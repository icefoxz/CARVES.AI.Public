namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeExternalConsumerResourcePack(RuntimeExternalConsumerResourcePackSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime external consumer resource pack",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Resource pack guide document: {surface.ResourcePackGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Resource pack complete: {surface.ResourcePackComplete}",
            $"Runtime-owned resources: {surface.RuntimeOwnedResourceCount}",
            $"Target-generated resources: {surface.TargetGeneratedResourceCount}",
            $"Command entries: {surface.CommandEntryCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Runtime-owned resource list:",
        };

        lines.AddRange(surface.RuntimeOwnedResources.Count == 0
            ? ["- none"]
            : surface.RuntimeOwnedResources.Select(resource => $"- {resource.Path} | class={resource.ResourceClass} | use={resource.ConsumerUse} | boundary={resource.Boundary}"));
        lines.Add("Target-generated resource list:");
        lines.AddRange(surface.TargetGeneratedResources.Count == 0
            ? ["- none"]
            : surface.TargetGeneratedResources.Select(resource => $"- {resource.Path} | class={resource.ResourceClass} | command={resource.MaterializationCommand} | boundary={resource.Boundary}"));
        lines.Add("Command entry list:");
        lines.AddRange(surface.CommandEntries.Count == 0
            ? ["- none"]
            : surface.CommandEntries.Select(entry => $"- {entry.Command} | surface={entry.SurfaceId} | authority={entry.AuthorityClass} | use={entry.ConsumerUse}"));
        lines.Add("Boundary rules:");
        lines.AddRange(surface.BoundaryRules.Count == 0
            ? ["- none"]
            : surface.BoundaryRules.Select(rule => $"- {rule}"));
        lines.Add("Required readback commands:");
        lines.AddRange(surface.RequiredReadbackCommands.Count == 0
            ? ["- none"]
            : surface.RequiredReadbackCommands.Select(command => $"- {command}"));
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
