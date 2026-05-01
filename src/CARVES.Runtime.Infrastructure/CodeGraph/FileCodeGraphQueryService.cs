using System.Text.Json;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphQueryService : ICodeGraphQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ControlPlanePaths paths;
    private readonly ICodeGraphBuilder codeGraphBuilder;

    public FileCodeGraphQueryService(ControlPlanePaths paths, ICodeGraphBuilder codeGraphBuilder)
    {
        this.paths = paths;
        this.codeGraphBuilder = codeGraphBuilder;
    }

    public CodeGraphManifest LoadManifest()
    {
        var manifestPath = FileCodeGraphStorage.GetManifestPath(paths);
        if (!File.Exists(manifestPath))
        {
            codeGraphBuilder.Build();
        }

        var manifest = ReadJson<CodeGraphManifest>(manifestPath);
        if (manifest is not null)
        {
            return manifest;
        }

        var summary = LoadSummaryIndex();
        return new CodeGraphManifest
        {
            GeneratedAt = summary.GeneratedAt,
            ModuleCount = summary.Modules.Count,
            FileCount = summary.Files.Count,
            CallableCount = summary.Callables.Count,
            DependencyCount = summary.Dependencies.Count,
            IndexPath = FileCodeGraphStorage.GetRelativeCodeGraphPath(paths, FileCodeGraphStorage.GetIndexPath(paths)),
            SearchIndexPath = FileCodeGraphStorage.GetRelativeCodeGraphPath(paths, FileCodeGraphStorage.GetSearchIndexPath(paths)),
            ModulesPath = FileCodeGraphStorage.GetRelativeCodeGraphPath(paths, FileCodeGraphStorage.GetModulesRoot(paths)),
            DependenciesPath = FileCodeGraphStorage.GetRelativeCodeGraphPath(paths, FileCodeGraphStorage.GetDependencyShardPath(paths)),
        };
    }

    public IReadOnlyList<CodeGraphModuleEntry> LoadModuleSummaries()
    {
        return LoadSummaryIndex().Modules;
    }

    public CodeGraphIndex LoadIndex()
    {
        var summaryIndex = LoadSummaryIndex();
        if (summaryIndex.Callables.Count > 0 || summaryIndex.Types.Count > 0)
        {
            return summaryIndex;
        }

        var moduleShards = LoadModuleShards(summaryIndex.Modules.Select(module => module.Name));
        var dependencyShard = ReadJson<CodeGraphDependencyShard>(FileCodeGraphStorage.GetDependencyShardPath(paths));

        var files = moduleShards
            .SelectMany(shard => shard.Files)
            .GroupBy(file => file.NodeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var types = moduleShards
            .SelectMany(shard => shard.Types)
            .GroupBy(type => type.NodeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(type => type.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var callables = moduleShards
            .SelectMany(shard => shard.Callables)
            .GroupBy(callable => callable.NodeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(callable => callable.QualifiedName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CodeGraphIndex
        {
            Version = summaryIndex.Version,
            GeneratedAt = summaryIndex.GeneratedAt,
            Modules = summaryIndex.Modules,
            Files = files.Length == 0 ? summaryIndex.Files : files,
            Types = types,
            Callables = callables,
            Dependencies = dependencyShard?.Entries ?? summaryIndex.Dependencies,
        };
    }

    private CodeGraphIndex LoadSummaryIndex()
    {
        var searchIndexPath = FileCodeGraphStorage.GetSearchIndexPath(paths);
        var legacyIndexPath = FileCodeGraphStorage.GetIndexPath(paths);
        if (!File.Exists(searchIndexPath) && !File.Exists(legacyIndexPath))
        {
            return codeGraphBuilder.Build().Index;
        }

        var index = ReadJson<CodeGraphIndex>(searchIndexPath) ?? ReadJson<CodeGraphIndex>(legacyIndexPath);
        return index ?? new CodeGraphIndex();
    }

    private IReadOnlyList<CodeGraphModuleShard> LoadModuleShards(IEnumerable<string> moduleNames)
    {
        var shards = new List<CodeGraphModuleShard>();
        foreach (var moduleName in moduleNames
                     .Where(name => !string.IsNullOrWhiteSpace(name))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            var shardPath = FileCodeGraphStorage.GetModuleShardPath(paths, moduleName);
            if (!File.Exists(shardPath))
            {
                codeGraphBuilder.Build();
            }

            var shard = ReadJson<CodeGraphModuleShard>(shardPath);
            if (shard is not null)
            {
                shards.Add(shard);
            }
        }

        return shards;
    }

    private static T? ReadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
    }
}
