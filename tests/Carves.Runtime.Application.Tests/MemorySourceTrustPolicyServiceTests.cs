using System.Text.Json;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.Tests;

public sealed class MemorySourceTrustPolicyServiceTests
{
    [Fact]
    public void SourceTrustRules_FixTrustLevelsAndCanonicalCeilings()
    {
        var service = new MemorySourceTrustPolicyService();

        Assert.Equal(MemorySourceTrustLevel.High, service.GetRule("human_approved_architecture_decision").TrustLevel);
        Assert.Equal("canonical_after_review", service.GetRule("human_approved_architecture_decision").AllowedAuthorityCeiling);
        Assert.Equal(MemorySourceTrustLevel.Medium, service.GetRule("test_result").TrustLevel);
        Assert.Equal("provisional_after_review", service.GetRule("test_result").AllowedAuthorityCeiling);
        Assert.Equal(MemorySourceTrustLevel.Low, service.GetRule("handoff_packet").TrustLevel);
        Assert.Equal("candidate", service.GetRule("handoff_packet").AllowedAuthorityCeiling);
        Assert.Equal(MemorySourceTrustLevel.Unclassified, service.GetRule("external_doc").TrustLevel);
        Assert.Equal("none_until_classified", service.GetRule("external_doc").AllowedAuthorityCeiling);
    }

    [Fact]
    public void PoisoningBoundaries_BlockDirectCanonicalizationForLowTrustAndUnclassifiedInputs()
    {
        var service = new MemorySourceTrustPolicyService();

        Assert.False(service.CanDirectlyCanonicalize("handoff_packet"));
        Assert.Equal("candidate_only", service.GetRule("handoff_packet").DefaultDecision);
        Assert.False(service.CanDirectlyCanonicalize("worker_self_claim"));
        Assert.Equal("candidate_only", service.GetRule("worker_self_claim").DefaultDecision);
        Assert.False(service.GetRule("ai_generated_summary").CanBeEvidenceByItself);
        Assert.Equal("classification_required", service.GetRule("external_doc").DefaultDecision);
        Assert.False(service.CanDirectlyCanonicalize("legacy_markdown_claim_without_evidence"));
    }

    [Fact]
    public void SourceTrustDocument_MatchesRuntimeRules()
    {
        var service = new MemorySourceTrustPolicyService();
        var json = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "docs", "runtime", "memory-gate", "MEMORY_SOURCE_TRUST_CLASSIFICATION_V1.json")));
        using var document = JsonDocument.Parse(json);

        var rules = document.RootElement.GetProperty("rules")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("source_type").GetString()!, item => item, StringComparer.Ordinal);

        Assert.Equal("memory-source-trust-classification.v1", document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memory-gate.v1", document.RootElement.GetProperty("policy_version").GetString());
        Assert.Equal(service.GetRules().Count, rules.Count);

        foreach (var entry in service.GetRules())
        {
            var rule = rules[entry.SourceType];
            Assert.Equal(entry.TrustLevel.ToString().ToLowerInvariant(), rule.GetProperty("trust_level").GetString());
            Assert.Equal(entry.AllowedAuthorityCeiling, rule.GetProperty("allowed_authority_ceiling").GetString());
            Assert.Equal(entry.DefaultDecision, rule.GetProperty("default_decision").GetString());
            Assert.Equal(entry.RequiresClassification, rule.GetProperty("requires_classification").GetBoolean());
            Assert.Equal(entry.RequiresOwnerReview, rule.GetProperty("requires_owner_review").GetBoolean());
            Assert.Equal(entry.RequiresEvidence, rule.GetProperty("requires_evidence").GetBoolean());
            Assert.Equal(entry.CanBeEvidenceByItself, rule.GetProperty("can_be_evidence_by_itself").GetBoolean());
        }
    }

    [Fact]
    public void PoisoningBoundariesDocument_MatchesFocusedFixtures()
    {
        var service = new MemorySourceTrustPolicyService();
        var json = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "docs", "runtime", "memory-gate", "MEMORY_POISONING_BOUNDARIES_V1.json")));
        using var document = JsonDocument.Parse(json);

        var fixtures = document.RootElement.GetProperty("fixtures")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("fixture_id").GetString()!, item => item, StringComparer.Ordinal);

        Assert.Equal("memory-poisoning-boundaries.v1", document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memory-gate.v1", document.RootElement.GetProperty("policy_version").GetString());

        var handoff = fixtures["poisoned_handoff_not_canonical"];
        Assert.Equal("candidate_only", handoff.GetProperty("expected_decision").GetString());
        Assert.Equal(service.GetRule("handoff_packet").DefaultDecision, handoff.GetProperty("expected_decision").GetString());

        var aiSummary = fixtures["ai_summary_not_evidence"];
        Assert.False(aiSummary.GetProperty("expected_can_be_evidence_by_itself").GetBoolean());
        Assert.False(service.GetRule("ai_generated_summary").CanBeEvidenceByItself);

        var externalDoc = fixtures["external_doc_requires_classification"];
        Assert.Equal("classification_required", externalDoc.GetProperty("expected_decision").GetString());
        Assert.Equal(service.GetRule("external_doc").AllowedAuthorityCeiling, externalDoc.GetProperty("expected_authority_ceiling").GetString());
    }
}
