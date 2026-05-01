using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeKernelBoundary(RuntimeKernelBoundarySurface surface)
    {
        var lines = new List<string>
        {
            $"Runtime {surface.KernelId} kernel",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Summary: {surface.Summary}",
            "Truth roots:",
        };

        foreach (var root in surface.TruthRoots.OrderBy(item => item.Classification, StringComparer.Ordinal).ThenBy(item => item.RootId, StringComparer.Ordinal))
        {
            lines.Add($"- {root.RootId} [{root.Classification}]");
            lines.Add($"  summary: {root.Summary}");
            lines.Add($"  paths: {(root.PathRefs.Length == 0 ? "(none)" : string.Join(" | ", root.PathRefs))}");
            lines.Add($"  truth refs: {(root.TruthRefs.Length == 0 ? "(none)" : string.Join(" | ", root.TruthRefs))}");
            if (root.Notes.Length > 0)
            {
                lines.Add($"  notes: {string.Join(" | ", root.Notes)}");
            }
        }

        lines.Add("Boundary rules:");
        foreach (var rule in surface.BoundaryRules.OrderBy(item => item.RuleId, StringComparer.Ordinal))
        {
            lines.Add($"- {rule.RuleId}");
            lines.Add($"  summary: {rule.Summary}");
            lines.Add($"  allowed: {(rule.AllowedRefs.Length == 0 ? "(none)" : string.Join(" | ", rule.AllowedRefs))}");
            lines.Add($"  forbidden: {(rule.ForbiddenRefs.Length == 0 ? "(none)" : string.Join(" | ", rule.ForbiddenRefs))}");
            if (rule.Notes.Length > 0)
            {
                lines.Add($"  notes: {string.Join(" | ", rule.Notes)}");
            }
        }

        lines.Add("Governed read paths:");
        foreach (var readPath in surface.GovernedReadPaths.OrderBy(item => item.PathId, StringComparer.Ordinal))
        {
            lines.Add($"- {readPath.PathId}");
            lines.Add($"  entry: {readPath.EntryPoint}");
            lines.Add($"  summary: {readPath.Summary}");
            lines.Add($"  truth refs: {(readPath.TruthRefs.Length == 0 ? "(none)" : string.Join(" | ", readPath.TruthRefs))}");
            if (readPath.Notes.Length > 0)
            {
                lines.Add($"  notes: {string.Join(" | ", readPath.Notes)}");
            }
        }

        lines.Add("Success criteria:");
        foreach (var criterion in surface.SuccessCriteria)
        {
            lines.Add($"- {criterion}");
        }

        lines.Add("Stop conditions:");
        foreach (var stopCondition in surface.StopConditions)
        {
            lines.Add($"- {stopCondition}");
        }

        if (surface.Notes.Length > 0)
        {
            lines.Add("Notes:");
            foreach (var note in surface.Notes)
            {
                lines.Add($"- {note}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
