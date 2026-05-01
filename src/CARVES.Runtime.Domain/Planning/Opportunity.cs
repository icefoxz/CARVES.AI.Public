using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Planning;

public sealed class Opportunity
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string OpportunityId { get; init; } = string.Empty;

    public OpportunitySource Source { get; init; } = OpportunitySource.Refactoring;

    public string Fingerprint { get; init; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public OpportunitySeverity Severity { get; set; } = OpportunitySeverity.Medium;

    public double Confidence { get; set; }

    public OpportunityStatus Status { get; set; } = OpportunityStatus.Open;

    public IReadOnlyList<string> RelatedFiles { get; set; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<string> MaterializedTaskIds { get; set; } = Array.Empty<string>();

    public string? LastEvaluationReason { get; set; }

    public DateTimeOffset FirstDetectedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastDetectedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void MarkObserved(OpportunityObservation observation, DateTimeOffset observedAt)
    {
        Title = observation.Title;
        Description = observation.Description;
        Reason = observation.Reason;
        Severity = observation.Severity;
        Confidence = observation.Confidence;
        RelatedFiles = observation.RelatedFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Metadata = new Dictionary<string, string>(observation.Metadata, StringComparer.Ordinal);
        LastDetectedAt = observedAt;

        if (Status is OpportunityStatus.Resolved or OpportunityStatus.Dismissed)
        {
            Status = MaterializedTaskIds.Count == 0 ? OpportunityStatus.Open : OpportunityStatus.Materialized;
        }

        Touch();
    }

    public void MarkMaterialized(IReadOnlyList<string> taskIds, string evaluationReason, DateTimeOffset observedAt)
    {
        MaterializedTaskIds = taskIds.Distinct(StringComparer.Ordinal).ToArray();
        LastEvaluationReason = evaluationReason;
        Status = OpportunityStatus.Materialized;
        LastDetectedAt = observedAt;
        Touch();
    }

    public void MarkResolved(string reason, DateTimeOffset observedAt)
    {
        Status = OpportunityStatus.Resolved;
        LastEvaluationReason = reason;
        LastDetectedAt = observedAt;
        Touch();
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
