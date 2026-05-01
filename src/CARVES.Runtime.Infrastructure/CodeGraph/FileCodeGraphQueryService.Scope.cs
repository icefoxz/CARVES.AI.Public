using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphQueryService
{
    public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries)
    {
        var summaryIndex = LoadSummaryIndex();
        var matches = ResolveMatches(summaryIndex, scopeEntries);
        if (!matches.HasMatches)
        {
            return CodeGraphScopeAnalysis.Empty;
        }

        var dependencyEntries = ReadJson<CodeGraphDependencyShard>(FileCodeGraphStorage.GetDependencyShardPath(paths))?.Entries
            ?? summaryIndex.Dependencies;
        var dependencyModuleIds = dependencyEntries
            .Where(entry => string.Equals(entry.Relationship, "depends_on", StringComparison.OrdinalIgnoreCase))
            .Where(entry => matches.ModuleIds.Contains(entry.SourceId) || matches.FileIds.Contains(entry.SourceId))
            .Where(entry => string.Equals(entry.TargetKind, "module", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        var dependencyModules = summaryIndex.Modules
            .Where(module => dependencyModuleIds.Contains(module.NodeId))
            .Select(module => module.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var matchedModuleShards = LoadModuleShards(matches.Modules.Select(module => module.Name));
        var matchedCallableIds = matches.Files
            .SelectMany(file => matchedModuleShards
                .Where(shard => string.Equals(shard.Module.Name, file.Module, StringComparison.OrdinalIgnoreCase))
                .SelectMany(shard => shard.Callables)
                .Where(callable => string.Equals(callable.ParentFileId, file.NodeId, StringComparison.Ordinal)))
            .Select(callable => callable.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var matchedCallables = matchedModuleShards
            .SelectMany(shard => shard.Callables)
            .Where(callable =>
                matchedCallableIds.Contains(callable.NodeId)
                || matches.RequestedScopes.Any(scope => string.Equals(scope, callable.QualifiedName, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(callable => callable.NodeId, StringComparer.Ordinal)
            .ToArray();
        var summaryLines = matches.Modules
            .Select(module => $"{module.Name}: {module.Summary}")
            .Concat(matches.Files.Take(5).Select(file => $"{file.Path}: {file.Summary}"))
            .Take(10)
            .ToArray();

        return new CodeGraphScopeAnalysis(
            matches.RequestedScopes,
            matches.Modules.Select(module => module.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            matches.Files.Select(file => file.Path).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
            matchedCallables.Select(callable => callable.QualifiedName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
            dependencyModules,
            summaryLines);
    }
}
