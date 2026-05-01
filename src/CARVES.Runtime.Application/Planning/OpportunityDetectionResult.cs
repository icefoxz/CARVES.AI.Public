using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed record OpportunityDetectionResult(
    OpportunitySnapshot Snapshot,
    IReadOnlyList<OpportunityDetectorContribution> Contributions)
{
    public int TotalDetected => Contributions.Sum(contribution => contribution.OpportunityIds.Count);

    public int OpenCount => Snapshot.Items.Count(item => item.Status == OpportunityStatus.Open);
}

public sealed record OpportunityDetectorContribution(
    string DetectorName,
    IReadOnlyList<string> OpportunityIds);
