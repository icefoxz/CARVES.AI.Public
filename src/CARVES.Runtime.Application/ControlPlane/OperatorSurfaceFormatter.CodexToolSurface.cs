using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult CodexToolSurface(CodexToolSurfaceSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "Codex tool surface",
            $"Surface id: {snapshot.SurfaceId}",
            $"Generated at: {snapshot.GeneratedAt:O}",
            $"Summary: {snapshot.Summary}",
        };

        foreach (var group in snapshot.Tools
                     .GroupBy(tool => tool.ActionClass)
                     .OrderBy(group => group.Key))
        {
            lines.Add($"{DescribeActionClass(group.Key)}:");
            foreach (var tool in group.OrderBy(item => item.ToolId, StringComparer.Ordinal))
            {
                var commandSummary = tool.CurrentCommandMappings.Length == 0
                    ? "(none)"
                    : string.Join(" | ", tool.CurrentCommandMappings);
                var dependencySummary = tool.DependsOnCards.Length == 0
                    ? "(none)"
                    : string.Join(", ", tool.DependsOnCards);
                lines.Add($"- {tool.ToolId} [{tool.Availability}] truth_affecting={tool.TruthAffecting}");
                lines.Add($"  summary: {tool.Summary}");
                lines.Add($"  commands: {commandSummary}");
                lines.Add($"  depends_on_cards: {dependencySummary}");
                if (tool.Notes.Length > 0)
                {
                    lines.Add($"  notes: {string.Join(" | ", tool.Notes)}");
                }
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    private static string DescribeActionClass(CodexToolActionClass actionClass)
    {
        return actionClass switch
        {
            CodexToolActionClass.PlannerOnly => "Planner-only actions",
            CodexToolActionClass.WorkerAllowed => "Worker-allowed actions",
            CodexToolActionClass.LocalEphemeral => "Local ephemeral actions",
            _ => actionClass.ToString(),
        };
    }
}
