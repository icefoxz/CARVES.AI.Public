namespace Carves.Runtime.Domain.CodeGraph;

public sealed class CodeGraphIndex
{
    public int Version { get; init; } = 1;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<CodeGraphModuleEntry> Modules { get; init; } = Array.Empty<CodeGraphModuleEntry>();

    public IReadOnlyList<CodeGraphFileEntry> Files { get; init; } = Array.Empty<CodeGraphFileEntry>();

    public IReadOnlyList<CodeGraphTypeEntry> Types { get; init; } = Array.Empty<CodeGraphTypeEntry>();

    public IReadOnlyList<CodeGraphCallableEntry> Callables { get; init; } = Array.Empty<CodeGraphCallableEntry>();

    public IReadOnlyList<CodeGraphDependencyEntry> Dependencies { get; init; } = Array.Empty<CodeGraphDependencyEntry>();
}

public sealed record CodeGraphModuleEntry(
    string NodeId,
    string Name,
    string PathPrefix,
    string Summary,
    IReadOnlyList<string> FileIds,
    IReadOnlyList<string> DependencyModules);

public sealed record CodeGraphFileEntry(
    string NodeId,
    string Path,
    string Module,
    string Language,
    string Summary,
    IReadOnlyList<string> TypeIds,
    IReadOnlyList<string> CallableIds,
    IReadOnlyList<string> DependencyModules,
    IReadOnlyList<string> Tokens);

public sealed record CodeGraphTypeEntry(
    string NodeId,
    string Name,
    string QualifiedName,
    string Path,
    string Module,
    int LineStart,
    int LineEnd,
    string ParentFileId);

public sealed record CodeGraphCallableEntry(
    string NodeId,
    string Name,
    string QualifiedName,
    string Path,
    string Module,
    string Language,
    int LineStart,
    int LineEnd,
    string ParentFileId,
    string? ParentTypeId);

public sealed record CodeGraphDependencyEntry(
    string SourceId,
    string SourceKind,
    string TargetId,
    string TargetKind,
    string Relationship);
