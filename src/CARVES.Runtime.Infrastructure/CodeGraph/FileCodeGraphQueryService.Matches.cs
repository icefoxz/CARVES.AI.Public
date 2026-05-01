using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphQueryService
{
    private static string NormalizeScope(string scope)
    {
        return scope.Trim().Trim('`').Replace('\\', '/');
    }

    private static QueryMatches ResolveMatches(CodeGraphIndex index, IEnumerable<string> scopeEntries)
    {
        var requestedScopes = scopeEntries
            .Select(NormalizeScope)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (requestedScopes.Length == 0)
        {
            return QueryMatches.Empty;
        }

        var matchedFiles = index.Files
            .Where(file => requestedScopes.Any(scope => MatchesScope(scope, file.Path, file.Module)))
            .ToArray();
        var matchedModules = index.Modules
            .Where(module => requestedScopes.Any(scope => MatchesModule(scope, module)))
            .Concat(index.Modules.Where(module => matchedFiles.Any(file => string.Equals(file.Module, module.Name, StringComparison.OrdinalIgnoreCase))))
            .DistinctBy(module => module.NodeId, StringComparer.Ordinal)
            .ToArray();
        var matchedFileIds = matchedFiles.Select(file => file.NodeId).ToHashSet(StringComparer.Ordinal);
        var matchedCallables = index.Callables
            .Where(callable => matchedFileIds.Contains(callable.ParentFileId) || requestedScopes.Any(scope => string.Equals(scope, callable.QualifiedName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return new QueryMatches(
            requestedScopes,
            matchedModules,
            matchedFiles,
            matchedCallables,
            matchedModules.Select(module => module.NodeId).ToHashSet(StringComparer.Ordinal),
            matchedFileIds);
    }

    private static bool MatchesScope(string scope, string path, string module)
    {
        return string.Equals(scope, path, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(scope.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scope, module, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesModule(string scope, CodeGraphModuleEntry module)
    {
        return string.Equals(scope, module.Name, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrWhiteSpace(module.PathPrefix) && scope.StartsWith(module.PathPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(module.PathPrefix) && module.PathPrefix.StartsWith(scope.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }

    private sealed record QueryMatches(
        IReadOnlyList<string> RequestedScopes,
        IReadOnlyList<CodeGraphModuleEntry> Modules,
        IReadOnlyList<CodeGraphFileEntry> Files,
        IReadOnlyList<CodeGraphCallableEntry> Callables,
        IReadOnlySet<string> ModuleIds,
        IReadOnlySet<string> FileIds)
    {
        public static QueryMatches Empty { get; } = new(
            Array.Empty<string>(),
            Array.Empty<CodeGraphModuleEntry>(),
            Array.Empty<CodeGraphFileEntry>(),
            Array.Empty<CodeGraphCallableEntry>(),
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));

        public bool HasMatches => RequestedScopes.Count > 0;
    }
}
