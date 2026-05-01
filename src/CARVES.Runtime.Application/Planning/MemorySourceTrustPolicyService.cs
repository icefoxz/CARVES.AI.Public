namespace Carves.Runtime.Application.Planning;

public enum MemorySourceTrustLevel
{
    High,
    Medium,
    Low,
    Unclassified,
}

public sealed record MemorySourceTrustRule(
    string SourceType,
    MemorySourceTrustLevel TrustLevel,
    string AllowedAuthorityCeiling,
    string DefaultDecision,
    bool RequiresClassification,
    bool RequiresOwnerReview,
    bool RequiresEvidence,
    bool CanBeEvidenceByItself);

public sealed class MemorySourceTrustPolicyService
{
    private static readonly IReadOnlyDictionary<string, MemorySourceTrustRule> Rules =
        new Dictionary<string, MemorySourceTrustRule>(StringComparer.Ordinal)
        {
            ["human_approved_architecture_decision"] = new("human_approved_architecture_decision", MemorySourceTrustLevel.High, "canonical_after_review", "review_required", false, true, true, true),
            ["review_record_with_owner"] = new("review_record_with_owner", MemorySourceTrustLevel.High, "canonical_after_review", "review_required", false, true, true, true),
            ["promotion_record"] = new("promotion_record", MemorySourceTrustLevel.High, "canonical_after_review", "review_required", false, true, true, true),
            ["test_result"] = new("test_result", MemorySourceTrustLevel.Medium, "provisional_after_review", "review_required", false, true, true, true),
            ["codegraph_snapshot"] = new("codegraph_snapshot", MemorySourceTrustLevel.Medium, "provisional_after_review", "review_required", false, true, true, true),
            ["task_result_with_evidence"] = new("task_result_with_evidence", MemorySourceTrustLevel.Medium, "provisional_after_review", "review_required", false, true, true, true),
            ["handoff_packet"] = new("handoff_packet", MemorySourceTrustLevel.Low, "candidate", "candidate_only", false, false, false, false),
            ["worker_self_claim"] = new("worker_self_claim", MemorySourceTrustLevel.Low, "candidate", "candidate_only", false, false, false, false),
            ["ai_generated_summary"] = new("ai_generated_summary", MemorySourceTrustLevel.Low, "candidate", "candidate_only", false, false, false, false),
            ["session_summary"] = new("session_summary", MemorySourceTrustLevel.Low, "candidate", "candidate_only", false, false, false, false),
            ["external_doc"] = new("external_doc", MemorySourceTrustLevel.Unclassified, "none_until_classified", "classification_required", true, true, true, false),
            ["copied_markdown"] = new("copied_markdown", MemorySourceTrustLevel.Unclassified, "none_until_classified", "classification_required", true, true, true, false),
            ["imported_memory"] = new("imported_memory", MemorySourceTrustLevel.Unclassified, "none_until_classified", "classification_required", true, true, true, false),
            ["legacy_markdown_claim_without_evidence"] = new("legacy_markdown_claim_without_evidence", MemorySourceTrustLevel.Low, "candidate", "candidate_only", false, true, false, false),
        };

    public IReadOnlyList<MemorySourceTrustRule> GetRules()
    {
        return Rules.Values
            .OrderBy(rule => rule.SourceType, StringComparer.Ordinal)
            .ToArray();
    }

    public MemorySourceTrustRule GetRule(string sourceType)
    {
        if (!Rules.TryGetValue(sourceType, out var rule))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, "Unknown memory source type.");
        }

        return rule;
    }

    public bool CanDirectlyCanonicalize(string sourceType)
    {
        var rule = GetRule(sourceType);
        return string.Equals(rule.AllowedAuthorityCeiling, "canonical_after_review", StringComparison.Ordinal);
    }
}
