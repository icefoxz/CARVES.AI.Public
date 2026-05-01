namespace Carves.Runtime.Domain.CodeGraph;

public sealed record CodeGraphNode(
    string NodeId,
    string Kind,
    string Name,
    string Path,
    string Module,
    string Summary,
    IReadOnlyList<string> Tokens,
    string? ParentNodeId = null,
    string? QualifiedName = null,
    string? Language = null,
    int? LineStart = null,
    int? LineEnd = null);
