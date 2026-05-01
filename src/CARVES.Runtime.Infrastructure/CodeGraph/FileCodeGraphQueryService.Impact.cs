using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphQueryService
{
    public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries)
    {
        var index = LoadSummaryIndex();
        var matches = ResolveMatches(index, scopeEntries);
        if (!matches.HasMatches)
        {
            return CodeGraphImpactAnalysis.Empty;
        }

        var reverseDependencies = (ReadJson<CodeGraphDependencyShard>(FileCodeGraphStorage.GetDependencyShardPath(paths))?.Entries ?? index.Dependencies)
            .Where(entry => string.Equals(entry.Relationship, "depends_on", StringComparison.OrdinalIgnoreCase))
            .Where(entry => string.Equals(entry.TargetKind, "module", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var impactedModuleIds = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(matches.ModuleIds);

        while (pending.Count > 0)
        {
            var currentTarget = pending.Dequeue();
            foreach (var entry in reverseDependencies.Where(edge => string.Equals(edge.SourceKind, "module", StringComparison.OrdinalIgnoreCase) && string.Equals(edge.TargetId, currentTarget, StringComparison.Ordinal)))
            {
                if (matches.ModuleIds.Contains(entry.SourceId) || !impactedModuleIds.Add(entry.SourceId))
                {
                    continue;
                }

                pending.Enqueue(entry.SourceId);
            }
        }

        var impactedModules = index.Modules
            .Where(module => impactedModuleIds.Contains(module.NodeId))
            .OrderBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var impactedFiles = index.Files
            .Where(file => reverseDependencies.Any(entry =>
                string.Equals(entry.SourceKind, "file", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.SourceId, file.NodeId, StringComparison.Ordinal) &&
                matches.ModuleIds.Contains(entry.TargetId)))
            .Where(file => !matches.FileIds.Contains(file.NodeId))
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var summaryLines = impactedModules
            .Select(module => $"Dependent module: {module.Name}")
            .Concat(impactedFiles.Take(5).Select(file => $"Dependent file: {file.Path}"))
            .Take(10)
            .ToArray();

        return new CodeGraphImpactAnalysis(
            matches.RequestedScopes,
            impactedModules.Select(module => module.Name).ToArray(),
            impactedFiles.Select(file => file.Path).ToArray(),
            summaryLines);
    }
}
