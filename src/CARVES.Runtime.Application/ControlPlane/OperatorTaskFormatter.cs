using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorTaskFormatter
{
    public static OperatorCommandResult ShowGraph(
        DomainTaskGraph graph,
        CodeGraphManifest codeGraphManifest,
        IReadOnlyList<CodeGraphModuleEntry> moduleSummaries)
    {
        var lines = new List<string>
        {
            "TaskGraph:",
            $"Cards: {graph.Cards.Count}",
            $"Tasks: {graph.Tasks.Count}",
        };
        lines.AddRange(graph.ListTasks().Select(task => $"- {task.TaskId} [{task.Status}/{task.TaskType}] {task.Priority} deps={task.Dependencies.Count} dispatch={task.TaskType.DescribeDispatchEligibility()}"));
        lines.Add("CodeGraph:");
        lines.Add("Read path: summary-first (manifest + module summaries)");
        lines.Add($"Modules: {codeGraphManifest.ModuleCount}");
        lines.Add($"Files: {codeGraphManifest.FileCount}");
        lines.Add($"Callables: {codeGraphManifest.CallableCount}");
        lines.Add($"Dependencies: {codeGraphManifest.DependencyCount}");
        lines.AddRange(moduleSummaries
            .Take(8)
            .Select(module => $"- {module.Name} -> {(module.DependencyModules.Count == 0 ? "(none)" : string.Join(", ", module.DependencyModules))}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult ShowBacklog(RefactoringBacklogSnapshot backlog)
    {
        var lines = new List<string> { $"Backlog items: {backlog.Items.Count}" };
        lines.AddRange(backlog.Items.Select(item => $"- {item.ItemId} [{item.Status}] {item.Kind} {item.Path}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult ShowOpportunities(OpportunitySnapshot snapshot)
    {
        var lines = new List<string> { $"Opportunities: {snapshot.Items.Count}" };
        lines.AddRange(snapshot.Items.Select(item =>
            $"- {item.OpportunityId} [{item.Status}/{item.Source}/{item.Severity}] confidence={item.Confidence:0.00} tasks={(item.MaterializedTaskIds.Count == 0 ? "(none)" : string.Join(", ", item.MaterializedTaskIds))}"));
        return new OperatorCommandResult(0, lines);
    }

    private static string JoinOrNone(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "(none)" : string.Join(", ", values.Take(8));
    }
}
