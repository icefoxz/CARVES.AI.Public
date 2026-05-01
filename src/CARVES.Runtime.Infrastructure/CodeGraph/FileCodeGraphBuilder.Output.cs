using System.Text.Json;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Domain.CodeGraph;
using DomainCodeGraph = Carves.Runtime.Domain.CodeGraph.CodeGraph;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphBuilder
{
    private static List<CodeGraphNode> CreateModuleNodes(FileCodeGraphScanState state)
    {
        var moduleNodes = new List<CodeGraphNode>();
        foreach (var module in state.ModuleAccumulators.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))
        {
            var moduleSummary = BuildModuleSummary(module, state.FileAccumulators, state.CallableNodes);
            moduleNodes.Add(new CodeGraphNode(
                module.NodeId,
                "module",
                module.Name,
                module.PathPrefix,
                module.Name,
                moduleSummary,
                module.TopTokens
                    .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
                    .Take(64)
                    .ToArray(),
                QualifiedName: module.Name));
        }

        return moduleNodes;
    }

    private static IReadOnlyList<CodeGraphModuleEntry> CreateModuleEntries(FileCodeGraphScanState state)
    {
        return state.ModuleAccumulators.Values
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(module => new CodeGraphModuleEntry(
                module.NodeId,
                module.Name,
                module.PathPrefix,
                BuildModuleSummary(module, state.FileAccumulators, state.CallableNodes),
                module.FileIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                module.DependencyModules.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();
    }

    private static DomainCodeGraph BuildGraph(DateTimeOffset generatedAt, FileCodeGraphScanState state, IReadOnlyList<CodeGraphNode> moduleNodes)
    {
        return new DomainCodeGraph
        {
            GeneratedAt = generatedAt,
            Nodes = moduleNodes
                .Concat(state.FileNodes.Select(SlimDetailNode))
                .Concat(state.TypeNodes)
                .Concat(state.CallableNodes)
                .ToArray(),
            Edges = state.Edges.ToArray(),
        };
    }

    private static CodeGraphNode SlimDetailNode(CodeGraphNode node)
    {
        return string.Equals(node.Kind, "file", StringComparison.OrdinalIgnoreCase)
            ? node with { Tokens = Array.Empty<string>() }
            : node;
    }

    private static CodeGraphIndex BuildIndex(
        DateTimeOffset generatedAt,
        FileCodeGraphScanState state,
        IReadOnlyList<CodeGraphModuleEntry> moduleEntries,
        IReadOnlyList<CodeGraphNode> moduleNodes)
    {
        var typeEntries = state.TypeNodes
            .Select(node => new CodeGraphTypeEntry(
                node.NodeId,
                node.Name,
                node.QualifiedName ?? node.Name,
                node.Path,
                node.Module,
                node.LineStart ?? 0,
                node.LineEnd ?? node.LineStart ?? 0,
                node.ParentNodeId ?? string.Empty))
            .ToArray();
        var callableEntries = state.CallableNodes
            .Select(node => new CodeGraphCallableEntry(
                node.NodeId,
                node.Name,
                node.QualifiedName ?? node.Name,
                node.Path,
                node.Module,
                node.Language ?? "unknown",
                node.LineStart ?? 0,
                node.LineEnd ?? node.LineStart ?? 0,
                FindParentFileId(node, state.FileAccumulators),
                FindParentTypeId(node, typeEntries)))
            .ToArray();
        var fileEntries = state.FileAccumulators
            .Select(file => new CodeGraphFileEntry(
                file.NodeId,
                file.Path,
                file.Module,
                file.Language,
                file.Summary,
                file.TypeIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                file.CallableIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                file.DependencyModules.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                file.Tokens))
            .ToArray();

        return new CodeGraphIndex
        {
            Version = 1,
            GeneratedAt = generatedAt,
            Modules = moduleEntries,
            Files = fileEntries,
            Types = typeEntries,
            Callables = callableEntries,
            Dependencies = state.Edges
                .Where(edge => string.Equals(edge.Kind, "depends_on", StringComparison.Ordinal))
                .Select(edge => new CodeGraphDependencyEntry(
                    edge.Source,
                    ResolveNodeKind(edge.Source, moduleNodes, state.FileNodes, state.TypeNodes, state.CallableNodes),
                    edge.Target,
                    ResolveNodeKind(edge.Target, moduleNodes, state.FileNodes, state.TypeNodes, state.CallableNodes),
                    edge.Kind))
                .ToArray(),
        };
    }

    private void WriteOutputs(DomainCodeGraph graph, CodeGraphIndex index)
    {
        var indexPath = FileCodeGraphStorage.GetIndexPath(paths);
        var manifestPath = FileCodeGraphStorage.GetManifestPath(paths);
        var searchIndexPath = FileCodeGraphStorage.GetSearchIndexPath(paths);
        var summariesRoot = FileCodeGraphStorage.GetSummariesRoot(paths);
        var modulesRoot = FileCodeGraphStorage.GetModulesRoot(paths);
        var dependencyShardPath = FileCodeGraphStorage.GetDependencyShardPath(paths);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        Directory.CreateDirectory(FileCodeGraphStorage.GetSearchRoot(paths));
        Directory.CreateDirectory(summariesRoot);
        Directory.CreateDirectory(modulesRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(dependencyShardPath)!);

        var legacyGraphPath = Path.Combine(FileCodeGraphStorage.GetCodeGraphRoot(paths), "graph.json");
        if (File.Exists(legacyGraphPath))
        {
            File.Delete(legacyGraphPath);
        }

        var summaryIndex = CreateSummaryIndex(index);
        var dependencyShard = new CodeGraphDependencyShard
        {
            GeneratedAt = index.GeneratedAt,
            Entries = index.Dependencies,
        };
        var manifest = new CodeGraphManifest
        {
            GeneratedAt = index.GeneratedAt,
            ModuleCount = index.Modules.Count,
            FileCount = index.Files.Count,
            CallableCount = index.Callables.Count,
            DependencyCount = index.Dependencies.Count,
            IndexPath = FileCodeGraphStorage.GetRelativeCodeGraphPath(paths, indexPath),
            SearchIndexPath = FileCodeGraphStorage.GetRelativeCodeGraphPath(paths, searchIndexPath),
            ModulesPath = FileCodeGraphStorage.GetRelativeCodeGraphPath(paths, modulesRoot),
            DependenciesPath = FileCodeGraphStorage.GetRelativeCodeGraphPath(paths, dependencyShardPath),
        };

        File.WriteAllText(indexPath, JsonSerializer.Serialize(summaryIndex, JsonOptions));
        File.WriteAllText(searchIndexPath, JsonSerializer.Serialize(summaryIndex, JsonOptions));
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        File.WriteAllText(dependencyShardPath, JsonSerializer.Serialize(dependencyShard, DetailJsonOptions));

        foreach (var moduleShard in CreateModuleShards(index))
        {
            var shardPath = FileCodeGraphStorage.GetModuleShardPath(paths, moduleShard.Module.Name);
            File.WriteAllText(shardPath, JsonSerializer.Serialize(moduleShard, DetailJsonOptions));
        }

        foreach (var module in index.Modules)
        {
            var summaryPath = FileCodeGraphStorage.GetModuleSummaryMarkdownPath(paths, module.Name);
            File.WriteAllText(summaryPath, BuildModuleSummaryDocument(module, index.Files, index.Callables));
        }
    }

    private static CodeGraphIndex CreateSummaryIndex(CodeGraphIndex index)
    {
        return new CodeGraphIndex
        {
            Version = index.Version,
            GeneratedAt = index.GeneratedAt,
            Modules = index.Modules,
            Files = index.Files
                .Select(file => new CodeGraphFileEntry(
                    file.NodeId,
                    file.Path,
                    file.Module,
                    file.Language,
                    file.Summary,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    file.DependencyModules,
                    Array.Empty<string>()))
                .ToArray(),
            Types = Array.Empty<CodeGraphTypeEntry>(),
            Callables = Array.Empty<CodeGraphCallableEntry>(),
            Dependencies = index.Dependencies,
        };
    }

    private static IReadOnlyList<CodeGraphModuleShard> CreateModuleShards(CodeGraphIndex index)
    {
        return index.Modules
            .Select(module =>
            {
                var fileIds = module.FileIds.ToHashSet(StringComparer.Ordinal);
                var files = index.Files
                    .Where(file => fileIds.Contains(file.NodeId) || string.Equals(file.Module, module.Name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var typeIds = files.SelectMany(file => file.TypeIds).ToHashSet(StringComparer.Ordinal);
                var callableIds = files.SelectMany(file => file.CallableIds).ToHashSet(StringComparer.Ordinal);
                var types = index.Types
                    .Where(type => typeIds.Contains(type.NodeId) || string.Equals(type.Module, module.Name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(type => type.QualifiedName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var callables = index.Callables
                    .Where(callable => callableIds.Contains(callable.NodeId) || string.Equals(callable.Module, module.Name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(callable => callable.QualifiedName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new CodeGraphModuleShard
                {
                    GeneratedAt = index.GeneratedAt,
                    Module = module,
                    Files = files,
                    Types = types,
                    Callables = callables,
                };
            })
            .OrderBy(shard => shard.Module.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
