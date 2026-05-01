namespace Carves.Runtime.Domain.Memory;

public sealed class MemoryBundle
{
    public IReadOnlyList<MemoryDocument> Architecture { get; init; } = Array.Empty<MemoryDocument>();

    public IReadOnlyList<MemoryDocument> Modules { get; init; } = Array.Empty<MemoryDocument>();

    public IReadOnlyList<MemoryDocument> Patterns { get; init; } = Array.Empty<MemoryDocument>();

    public IReadOnlyList<MemoryDocument> Project { get; init; } = Array.Empty<MemoryDocument>();

    public IReadOnlyDictionary<string, object> ExecutionContext { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);
}
