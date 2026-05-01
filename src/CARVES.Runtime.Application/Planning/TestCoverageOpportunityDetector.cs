using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed class TestCoverageOpportunityDetector : IOpportunityDetector
{
    private readonly ICodeGraphQueryService codeGraphQueryService;

    public TestCoverageOpportunityDetector(ICodeGraphQueryService codeGraphQueryService)
    {
        this.codeGraphQueryService = codeGraphQueryService;
    }

    public string Name => "test-coverage";

    public IReadOnlyList<OpportunityObservation> Detect()
    {
        var index = codeGraphQueryService.LoadIndex();
        var filePathMap = index.Files.ToDictionary(file => file.NodeId, file => file.Path, StringComparer.OrdinalIgnoreCase);
        var testPaths = index.Files
            .Where(file => file.Path.Contains("/tests/", StringComparison.OrdinalIgnoreCase) || file.Path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Path)
            .ToArray();

        return index.Modules
            .Where(module => module.PathPrefix.StartsWith("src/", StringComparison.OrdinalIgnoreCase))
            .Where(module => !HasRelatedTests(module.Name, testPaths))
            .Take(5)
            .Select(module => new OpportunityObservation(
                OpportunitySource.TestCoverage,
                $"coverage:{module.Name}",
                $"Add tests for {module.Name}",
                $"No matching test files were found for module {module.Name}.",
                "module appears uncovered by tests",
                OpportunitySeverity.Medium,
                0.7,
                module.FileIds
                    .Select(fileId => filePathMap.TryGetValue(fileId, out var path) ? path : fileId)
                    .Where(path => !IsGeneratedPath(path))
                    .ToArray(),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["module"] = module.Name,
                }))
            .ToArray();
    }

    private static bool HasRelatedTests(string moduleName, IReadOnlyList<string> testPaths)
    {
        var normalized = Normalize(moduleName);
        return testPaths.Any(path => Normalize(path).Contains(normalized, StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        return value
            .ToLowerInvariant()
            .Replace("\\", "/", StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);
    }

    private static bool IsGeneratedPath(string path)
    {
        var normalized = path.Replace("\\", "/", StringComparison.Ordinal);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }
}
