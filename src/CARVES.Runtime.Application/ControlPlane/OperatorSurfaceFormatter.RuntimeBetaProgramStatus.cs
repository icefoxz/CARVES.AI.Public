using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeBetaProgramStatus(RuntimeBetaProgramStatusSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime beta program status",
            $"Program status doc: {surface.ProgramStatusDocPath}",
            $"Routing map doc: {surface.RoutingMapPath}",
            $"Redundancy sweep doc: {surface.RedundancySweepDocPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Truth owner: {surface.TruthOwner}",
            $"Contract ownership: {surface.ContractOwnership}",
            $"Live entry command: {surface.LiveEntryCommand}",
            $"Consolidated surface count: {surface.ConsolidatedSurfaceCount}",
            $"Recommended next action: {surface.RecommendedNextAction}",
        };

        lines.Add($"Consolidated surfaces: {surface.ConsolidatedFromSurfaceIds.Count}");
        lines.AddRange(surface.ConsolidatedFromSurfaceIds.Select(item => $"- merged-surface: {item}"));
        lines.Add($"Runtime-owned program boundaries: {surface.RuntimeOwnedProgramBoundaries.Count}");
        lines.AddRange(surface.RuntimeOwnedProgramBoundaries.Select(item => $"- boundary: {item}"));
        lines.Add($"Program phases: {surface.Phases.Count}");
        foreach (var phase in surface.Phases)
        {
            lines.Add($"- phase: {phase.PhaseId} {phase.PhaseTitle} ({phase.OverallPosture})");
            lines.Add($"  supporting-doc: {phase.SupportingDocPath}");
            lines.Add($"  cross-repo-state: {phase.CrossRepoState}");
            lines.Add($"  runtime-owned-areas: {phase.RuntimeOwnedAreas.Count}");
            lines.AddRange(phase.RuntimeOwnedAreas.Select(item => $"    - runtime-area: {item}"));
            lines.Add($"  query-entry-commands: {phase.QueryEntryCommands.Count}");
            lines.AddRange(phase.QueryEntryCommands.Select(item => $"    - query-entry: {item}"));
            lines.Add($"  operator-owned-follow-on: {phase.OperatorOwnedFollowOn.Count}");
            lines.AddRange(phase.OperatorOwnedFollowOn.Select(item => $"    - operator-owned: {item}"));
            lines.Add($"  cloud-owned-follow-on: {phase.CloudOwnedFollowOn.Count}");
            lines.AddRange(phase.CloudOwnedFollowOn.Select(item => $"    - cloud-owned: {item}"));
            lines.Add($"  blocked-claims: {phase.BlockedClaims.Count}");
            lines.AddRange(phase.BlockedClaims.Select(item => $"    - blocked-claim: {item}"));
            lines.Add($"  supporting-refs: {phase.SupportingReferencePaths.Count}");
            lines.AddRange(phase.SupportingReferencePaths.Select(item => $"    - supporting-ref: {item}"));
        }

        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        lines.Add($"Non-claims: {surface.NonClaims.Count}");
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
