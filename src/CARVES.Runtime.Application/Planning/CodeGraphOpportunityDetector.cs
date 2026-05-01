using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed class CodeGraphOpportunityDetector : IOpportunityDetector
{
    private readonly ICodeGraphQueryService codeGraphQueryService;

    public CodeGraphOpportunityDetector(ICodeGraphQueryService codeGraphQueryService)
    {
        this.codeGraphQueryService = codeGraphQueryService;
    }

    public string Name => "codegraph";

    public IReadOnlyList<OpportunityObservation> Detect()
    {
        var index = codeGraphQueryService.LoadIndex();
        var observations = new List<OpportunityObservation>();
        observations.AddRange(DetectDependencyCycles(index));
        observations.AddRange(DetectDeepDependencyModules(index));
        return observations;
    }

    private static IEnumerable<OpportunityObservation> DetectDependencyCycles(CodeGraphIndex index)
    {
        var moduleMap = index.Modules.ToDictionary(module => module.Name, module => module, StringComparer.OrdinalIgnoreCase);
        var filePathMap = index.Files.ToDictionary(file => file.NodeId, file => file.Path, StringComparer.OrdinalIgnoreCase);
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in index.Modules)
        {
            foreach (var dependency in module.DependencyModules)
            {
                if (!moduleMap.TryGetValue(dependency, out var dependencyModule))
                {
                    continue;
                }

                if (!dependencyModule.DependencyModules.Contains(module.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fingerprint = string.Compare(module.Name, dependency, StringComparison.OrdinalIgnoreCase) <= 0
                    ? $"{module.Name}|{dependency}"
                    : $"{dependency}|{module.Name}";
                if (!emitted.Add(fingerprint))
                {
                    continue;
                }

                yield return new OpportunityObservation(
                    OpportunitySource.CodeGraph,
                    $"cycle:{fingerprint}",
                    $"Break codegraph dependency cycle between {module.Name} and {dependency}",
                    $"CodeGraph detected a mutual dependency between {module.Name} and {dependency}.",
                    "module dependency cycle detected",
                    OpportunitySeverity.High,
                    0.8,
                    ResolvePaths(module.FileIds.Concat(dependencyModule.FileIds), filePathMap),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["cycle_a"] = module.Name,
                        ["cycle_b"] = dependency,
                    });
            }
        }
    }

    private static IEnumerable<OpportunityObservation> DetectDeepDependencyModules(CodeGraphIndex index)
    {
        var filePathMap = index.Files.ToDictionary(file => file.NodeId, file => file.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var module in index.Modules.Where(module => module.DependencyModules.Count >= 5).Take(3))
        {
            yield return new OpportunityObservation(
                OpportunitySource.CodeGraph,
                $"fanout:{module.Name}",
                $"Reduce dependency fan-out in {module.Name}",
                $"{module.Name} depends on {module.DependencyModules.Count} modules and may benefit from a split or boundary cleanup.",
                "module dependency fan-out exceeds stage-4 threshold",
                OpportunitySeverity.Medium,
                0.65,
                ResolvePaths(module.FileIds, filePathMap),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["module"] = module.Name,
                    ["dependency_count"] = module.DependencyModules.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
        }
    }

    private static IReadOnlyList<string> ResolvePaths(IEnumerable<string> fileIds, IReadOnlyDictionary<string, string> filePathMap)
    {
        return fileIds
            .Select(fileId => filePathMap.TryGetValue(fileId, out var path) ? path : fileId)
            .Where(path => !IsGeneratedPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsGeneratedPath(string path)
    {
        var normalized = path.Replace("\\", "/", StringComparison.Ordinal);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }
}
