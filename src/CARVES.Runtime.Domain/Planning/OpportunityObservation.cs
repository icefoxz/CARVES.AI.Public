namespace Carves.Runtime.Domain.Planning;

public sealed record OpportunityObservation(
    OpportunitySource Source,
    string Fingerprint,
    string Title,
    string Description,
    string Reason,
    OpportunitySeverity Severity,
    double Confidence,
    IReadOnlyList<string> RelatedFiles,
    IReadOnlyDictionary<string, string> Metadata);
