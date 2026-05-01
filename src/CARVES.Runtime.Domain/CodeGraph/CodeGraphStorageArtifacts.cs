namespace Carves.Runtime.Domain.CodeGraph;

public sealed class CodeGraphManifest
{
    public string SchemaVersion { get; init; } = "codegraph-manifest.v1";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public int ModuleCount { get; init; }

    public int FileCount { get; init; }

    public int CallableCount { get; init; }

    public int DependencyCount { get; init; }

    public string IndexPath { get; init; } = "index.json";

    public string SearchIndexPath { get; init; } = "search/index.json";

    public string ModulesPath { get; init; } = "modules";

    public string DependenciesPath { get; init; } = "dependencies/module-deps.json";
}

public sealed class CodeGraphModuleShard
{
    public string SchemaVersion { get; init; } = "codegraph-module.v1";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public CodeGraphModuleEntry Module { get; init; } = new(string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>(), Array.Empty<string>());

    public IReadOnlyList<CodeGraphFileEntry> Files { get; init; } = Array.Empty<CodeGraphFileEntry>();

    public IReadOnlyList<CodeGraphTypeEntry> Types { get; init; } = Array.Empty<CodeGraphTypeEntry>();

    public IReadOnlyList<CodeGraphCallableEntry> Callables { get; init; } = Array.Empty<CodeGraphCallableEntry>();
}

public sealed class CodeGraphDependencyShard
{
    public string SchemaVersion { get; init; } = "codegraph-dependencies.v1";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<CodeGraphDependencyEntry> Entries { get; init; } = Array.Empty<CodeGraphDependencyEntry>();
}
