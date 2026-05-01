using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeMinimalWorkerBaseline(RuntimeMinimalWorkerBaselineSurface surface)
    {
        var policy = surface.Policy;
        var lines = new List<string>
        {
            "Runtime minimal-worker baseline",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Policy version: {policy.PolicyVersion}",
            $"Summary: {policy.Summary}",
            "Extraction boundary:",
            $"- direct absorptions: {string.Join(" | ", policy.ExtractionBoundary.DirectAbsorptions)}",
            $"  translated into CARVES: {string.Join(" | ", policy.ExtractionBoundary.TranslatedIntoCarves)}",
            $"  rejected anchors: {string.Join(" | ", policy.ExtractionBoundary.RejectedAnchors)}",
            "Linear loop baseline:",
            $"- summary: {policy.LinearLoop.Summary}",
            $"  loop phases: {(policy.LinearLoop.LoopPhases.Length == 0 ? "(none)" : string.Join(" | ", policy.LinearLoop.LoopPhases))}",
            $"  ordering compatibility: {(policy.LinearLoop.OrderingCompatibility.Length == 0 ? "(none)" : string.Join(" | ", policy.LinearLoop.OrderingCompatibility))}",
            $"  outside worker loop: {(policy.LinearLoop.OutsideWorkerLoop.Length == 0 ? "(none)" : string.Join(" | ", policy.LinearLoop.OutsideWorkerLoop))}",
            "Subprocess boundary:",
            $"- summary: {policy.SubprocessBoundary.Summary}",
            $"  benefits: {(policy.SubprocessBoundary.Benefits.Length == 0 ? "(none)" : string.Join(" | ", policy.SubprocessBoundary.Benefits))}",
            $"  guarded separations: {(policy.SubprocessBoundary.GuardedSeparations.Length == 0 ? "(none)" : string.Join(" | ", policy.SubprocessBoundary.GuardedSeparations))}",
            $"  required governed state: {(policy.SubprocessBoundary.RequiredGovernedState.Length == 0 ? "(none)" : string.Join(" | ", policy.SubprocessBoundary.RequiredGovernedState))}",
            "Weak lane:",
            $"- lane id: {policy.WeakLane.LaneId}",
            $"  summary: {policy.WeakLane.Summary}",
            $"  execution style: {policy.WeakLane.ExecutionStyle}",
            $"  model profile: {policy.WeakLane.ModelProfileId}",
            $"  task types: {(policy.WeakLane.AllowedTaskTypes.Length == 0 ? "(none)" : string.Join(" | ", policy.WeakLane.AllowedTaskTypes))}",
            $"  startup surfaces: {(policy.WeakLane.RequiredStartupSurfaces.Length == 0 ? "(none)" : string.Join(" | ", policy.WeakLane.RequiredStartupSurfaces))}",
            $"  runtime surfaces: {(policy.WeakLane.RequiredRuntimeSurfaces.Length == 0 ? "(none)" : string.Join(" | ", policy.WeakLane.RequiredRuntimeSurfaces))}",
            $"  required verification: {(policy.WeakLane.RequiredVerification.Length == 0 ? "(none)" : string.Join(" | ", policy.WeakLane.RequiredVerification))}",
            $"  scope ceiling: entries={policy.WeakLane.ScopeCeiling.MaxScopeEntries}; relevant_files={policy.WeakLane.ScopeCeiling.MaxRelevantFiles}; files_changed={policy.WeakLane.ScopeCeiling.MaxFilesChanged}; lines_changed={policy.WeakLane.ScopeCeiling.MaxLinesChanged}",
            $"  stop conditions: {(policy.WeakLane.StopConditions.Length == 0 ? "(none)" : string.Join(" | ", policy.WeakLane.StopConditions))}",
            "Qualification:",
            $"- summary: {policy.Qualification.Summary}",
            $"  gains: {(policy.Qualification.Gains.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.Gains))}",
            $"  success criteria: {(policy.Qualification.SuccessCriteria.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.SuccessCriteria))}",
            $"  intentionally outside scope: {(policy.Qualification.IntentionallyOutsideScope.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.IntentionallyOutsideScope))}",
            $"  stop conditions: {(policy.Qualification.StopConditions.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.StopConditions))}",
            "Truth roots:",
        };

        foreach (var root in policy.TruthRoots.OrderBy(item => item.RootId, StringComparer.Ordinal))
        {
            lines.Add($"- {root.RootId} [{root.Classification}]");
            lines.Add($"  summary: {root.Summary}");
            lines.Add($"  paths: {(root.PathRefs.Length == 0 ? "(none)" : string.Join(" | ", root.PathRefs))}");
            lines.Add($"  truth refs: {(root.TruthRefs.Length == 0 ? "(none)" : string.Join(" | ", root.TruthRefs))}");
        }

        lines.Add("Boundary rules:");
        foreach (var rule in policy.BoundaryRules.OrderBy(item => item.RuleId, StringComparer.Ordinal))
        {
            lines.Add($"- {rule.RuleId}");
            lines.Add($"  summary: {rule.Summary}");
            lines.Add($"  allowed: {(rule.AllowedRefs.Length == 0 ? "(none)" : string.Join(" | ", rule.AllowedRefs))}");
            lines.Add($"  forbidden: {(rule.ForbiddenRefs.Length == 0 ? "(none)" : string.Join(" | ", rule.ForbiddenRefs))}");
        }

        lines.Add("Governed read paths:");
        foreach (var readPath in policy.GovernedReadPaths.OrderBy(item => item.PathId, StringComparer.Ordinal))
        {
            lines.Add($"- {readPath.PathId}");
            lines.Add($"  entry: {readPath.EntryPoint}");
            lines.Add($"  summary: {readPath.Summary}");
            lines.Add($"  truth refs: {(readPath.TruthRefs.Length == 0 ? "(none)" : string.Join(" | ", readPath.TruthRefs))}");
        }

        if (policy.Notes.Length > 0)
        {
            lines.Add("Notes:");
            foreach (var note in policy.Notes)
            {
                lines.Add($"- {note}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
