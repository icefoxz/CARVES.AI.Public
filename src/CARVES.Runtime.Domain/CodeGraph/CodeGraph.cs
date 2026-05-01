namespace Carves.Runtime.Domain.CodeGraph;

public sealed class CodeGraph
{
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<CodeGraphNode> Nodes { get; init; } = Array.Empty<CodeGraphNode>();

    public IReadOnlyList<CodeGraphEdge> Edges { get; init; } = Array.Empty<CodeGraphEdge>();
}
