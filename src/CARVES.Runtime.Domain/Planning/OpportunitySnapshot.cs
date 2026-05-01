namespace Carves.Runtime.Domain.Planning;

public sealed class OpportunitySnapshot
{
    public int Version { get; init; } = 1;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<Opportunity> Items { get; init; } = Array.Empty<Opportunity>();
}
