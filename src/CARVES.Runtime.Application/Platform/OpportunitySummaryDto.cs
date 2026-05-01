namespace Carves.Runtime.Application.Platform;

public sealed record OpportunitySummaryDto(
    string OpportunityId,
    string Source,
    string Severity,
    double Confidence,
    string Status,
    IReadOnlyList<string> RelatedFiles,
    IReadOnlyList<string> MaterializedTaskIds);
