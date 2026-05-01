using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Tasks;
using DomainCodeGraph = Carves.Runtime.Domain.CodeGraph.CodeGraph;

namespace Carves.Runtime.Application.Memory;

public sealed class ExecutionContextBuilder
{
    public IReadOnlyDictionary<string, object> Build(TaskNode task, DomainCodeGraph? codeGraph = null)
    {
        var context = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["task_id"] = task.TaskId,
            ["title"] = task.Title,
            ["scope"] = task.Scope.ToArray(),
            ["acceptance"] = task.Acceptance.ToArray(),
            ["dependencies"] = task.Dependencies.ToArray(),
            ["task_type"] = task.TaskType.ToString(),
        };

        if (codeGraph is not null)
        {
            var relatedNodes = codeGraph.Nodes
                .Where(node => task.Scope.Any(scope => node.Path.Contains(scope, StringComparison.OrdinalIgnoreCase)))
                .Select(node => node.Path)
                .Take(20)
                .ToArray();
            context["related_code_nodes"] = relatedNodes;
        }

        return context;
    }
}
