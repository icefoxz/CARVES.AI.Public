namespace Carves.Runtime.Application.Planning;

public sealed record OpportunityTaskMaterializationResult(
    IReadOnlyDictionary<string, IReadOnlyList<string>> MaterializedTaskIdsByOpportunity)
{
    public IReadOnlyList<string> MaterializedTaskIds => MaterializedTaskIdsByOpportunity.Values
        .SelectMany(ids => ids)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}
