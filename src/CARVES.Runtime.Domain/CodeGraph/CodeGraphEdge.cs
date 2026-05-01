namespace Carves.Runtime.Domain.CodeGraph;

public sealed record CodeGraphEdge(string Source, string Target, string Kind, string? Description = null);
