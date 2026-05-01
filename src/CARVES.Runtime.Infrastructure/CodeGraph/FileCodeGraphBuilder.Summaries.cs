using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphBuilder
{
    private static string BuildModuleSummary(ModuleAccumulator module, IEnumerable<FileAccumulator> files, IEnumerable<CodeGraphNode> callables)
    {
        var moduleFiles = files.Where(file => string.Equals(file.Module, module.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        var moduleCallableCount = callables.Count(node => string.Equals(node.Module, module.Name, StringComparison.OrdinalIgnoreCase));
        var dependencyText = module.DependencyModules.Count == 0
            ? "no internal module dependencies"
            : $"depends on {string.Join(", ", module.DependencyModules.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Take(5))}";

        return $"{module.Name} spans {moduleFiles.Length} files and {moduleCallableCount} callables; {dependencyText}.";
    }

    private static string BuildModuleSummaryDocument(
        CodeGraphModuleEntry module,
        IReadOnlyList<CodeGraphFileEntry> files,
        IReadOnlyList<CodeGraphCallableEntry> callables)
    {
        var lines = new List<string>
        {
            $"# {module.Name}",
            string.Empty,
            module.Summary,
            string.Empty,
            $"Path prefix: `{module.PathPrefix}`",
            $"Files: {module.FileIds.Count}",
            $"Dependencies: {(module.DependencyModules.Count == 0 ? "(none)" : string.Join(", ", module.DependencyModules))}",
            string.Empty,
            "Files:",
        };

        var fileLookup = files.ToDictionary(file => file.NodeId, StringComparer.Ordinal);
        foreach (var fileId in module.FileIds.Take(20))
        {
            if (fileLookup.TryGetValue(fileId, out var file))
            {
                lines.Add($"- `{file.Path}`");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Callables:");
        foreach (var callable in callables.Where(item => string.Equals(item.Module, module.Name, StringComparison.OrdinalIgnoreCase)).Take(20))
        {
            lines.Add($"- `{callable.QualifiedName}`");
        }

        return string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine;
    }

    private static string ResolveNodeKind(
        string nodeId,
        IEnumerable<CodeGraphNode> moduleNodes,
        IEnumerable<CodeGraphNode> fileNodes,
        IEnumerable<CodeGraphNode> typeNodes,
        IEnumerable<CodeGraphNode> callableNodes)
    {
        if (moduleNodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            return "module";
        }

        if (fileNodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            return "file";
        }

        if (typeNodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            return "type";
        }

        if (callableNodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            return "callable";
        }

        return "unknown";
    }

    private static string FindParentFileId(CodeGraphNode node, IReadOnlyList<FileAccumulator> files)
    {
        if (files.Any(file => string.Equals(file.NodeId, node.ParentNodeId, StringComparison.Ordinal)))
        {
            return node.ParentNodeId ?? string.Empty;
        }

        var parentFile = files.FirstOrDefault(file => string.Equals(file.Path, node.Path, StringComparison.OrdinalIgnoreCase));
        return parentFile?.NodeId ?? string.Empty;
    }

    private static string? FindParentTypeId(CodeGraphNode node, IReadOnlyList<CodeGraphTypeEntry> types)
    {
        var parent = types.FirstOrDefault(type => string.Equals(type.NodeId, node.ParentNodeId, StringComparison.Ordinal));
        return parent?.NodeId;
    }
}
