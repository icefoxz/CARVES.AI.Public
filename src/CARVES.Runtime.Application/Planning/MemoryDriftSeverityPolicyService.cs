using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed record MemoryDriftSeverityEntry(
    string DriftCode,
    OpportunitySeverity Severity,
    string Behavior,
    bool BlocksRelease,
    bool BlocksHighRiskTask,
    bool SuggestionOnly);

public sealed class MemoryDriftSeverityPolicyService
{
    private static readonly IReadOnlyDictionary<string, MemoryDriftSeverityEntry> Entries =
        new Dictionary<string, MemoryDriftSeverityEntry>(StringComparer.Ordinal)
        {
            ["canonical_fact_without_evidence"] = new("canonical_fact_without_evidence", OpportunitySeverity.Critical, "release_blocker", BlocksRelease: true, BlocksHighRiskTask: false, SuggestionOnly: false),
            ["invalidated_fact_retrieved"] = new("invalidated_fact_retrieved", OpportunitySeverity.Critical, "release_blocker", BlocksRelease: true, BlocksHighRiskTask: false, SuggestionOnly: false),
            ["dependency_contradiction"] = new("dependency_contradiction", OpportunitySeverity.High, "block_high_risk_task", BlocksRelease: false, BlocksHighRiskTask: true, SuggestionOnly: false),
            ["stale_codegraph_high_risk_task"] = new("stale_codegraph_high_risk_task", OpportunitySeverity.High, "block_high_risk_task", BlocksRelease: false, BlocksHighRiskTask: true, SuggestionOnly: false),
            ["decision_supersession_missing"] = new("decision_supersession_missing", OpportunitySeverity.High, "review_required", BlocksRelease: false, BlocksHighRiskTask: true, SuggestionOnly: false),
            ["stale_command"] = new("stale_command", OpportunitySeverity.Medium, "warn_unless_used", BlocksRelease: false, BlocksHighRiskTask: false, SuggestionOnly: false),
            ["stale_path"] = new("stale_path", OpportunitySeverity.Medium, "warn_or_candidate_cleanup", BlocksRelease: false, BlocksHighRiskTask: false, SuggestionOnly: false),
            ["missing_module_memory"] = new("missing_module_memory", OpportunitySeverity.Low, "suggestion", BlocksRelease: false, BlocksHighRiskTask: false, SuggestionOnly: true),
            ["handoff_fact_not_promoted"] = new("handoff_fact_not_promoted", OpportunitySeverity.Low, "candidate_suggestion", BlocksRelease: false, BlocksHighRiskTask: false, SuggestionOnly: true),
        };

    public IReadOnlyList<MemoryDriftSeverityEntry> GetEntries()
    {
        return Entries.Values
            .OrderBy(entry => entry.DriftCode, StringComparer.Ordinal)
            .ToArray();
    }

    public MemoryDriftSeverityEntry GetEntry(string driftCode)
    {
        if (!Entries.TryGetValue(driftCode, out var entry))
        {
            throw new ArgumentOutOfRangeException(nameof(driftCode), driftCode, "Unknown memory drift code.");
        }

        return entry;
    }

    public OpportunitySeverity ResolveOpportunitySeverity(string driftCode)
    {
        return GetEntry(driftCode).Severity;
    }
}
