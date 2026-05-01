using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase10TargetDecisionServiceTests
{
    [Fact]
    public void Persist_UsesTrustedTrustLineRecommendation()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var evidenceResult = CreateEvidenceResult("phase_0a_baseline", "proceed_renderer_shadow", "renderer_shadow_offline");
        var trustLineResult = new RuntimeTokenBaselineTrustLineResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            CohortId = "phase_0a_baseline",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase10TargetDecisionMayReferenceThisLine = true,
            TotalCostClaimAllowed = true,
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
        };

        var result = RuntimeTokenPhase10TargetDecisionService.Persist(
            paths,
            evidenceResult,
            trustLineResult,
            new DateOnly(2026, 4, 21),
            evaluatedAtUtc: new DateTimeOffset(2026, 4, 21, 14, 0, 0, TimeSpan.Zero));

        Assert.True(result.Phase10TargetDecisionMayReferenceThisLine);
        Assert.Equal("proceed_renderer_shadow", result.Decision);
        Assert.Equal("renderer_shadow_offline", result.NextTrack);
        Assert.Equal("goal", result.TargetSegment);
        Assert.Equal("renderer", result.TargetSegmentClass);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "docs", "runtime", "runtime-token-optimization-phase-1-target-decision-result-2026-04-21.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai", "runtime", "token-optimization", "phase-1", "target-decision-result-2026-04-21.json")));
    }

    [Fact]
    public void Persist_ForcesInsufficientDataWhenTrustLineIsBlocked()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var evidenceResult = CreateEvidenceResult("phase_0a_baseline", "proceed_renderer_shadow", "renderer_shadow_offline");
        var trustLineResult = new RuntimeTokenBaselineTrustLineResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            CohortId = "phase_0a_baseline",
            TrustLineClassification = "recomputed_but_insufficient_data_for_phase_1_target_decision",
            Phase10TargetDecisionMayReferenceThisLine = false,
            TotalCostClaimAllowed = false,
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            BlockingReasons = ["classified_segment_coverage_below_threshold"],
        };

        var result = RuntimeTokenPhase10TargetDecisionService.Persist(
            paths,
            evidenceResult,
            trustLineResult,
            new DateOnly(2026, 4, 21),
            evaluatedAtUtc: new DateTimeOffset(2026, 4, 21, 14, 5, 0, TimeSpan.Zero));

        Assert.False(result.Phase10TargetDecisionMayReferenceThisLine);
        Assert.Equal("insufficient_data", result.Decision);
        Assert.Equal("insufficient_data", result.NextTrack);
        Assert.Null(result.TargetSegment);
        Assert.Contains("classified_segment_coverage_below_threshold", result.BlockingReasons);
    }

    private static RuntimeTokenBaselineEvidenceResult CreateEvidenceResult(string cohortId, string decision, string nextTrack)
    {
        return new RuntimeTokenBaselineEvidenceResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            MarkdownArtifactPath = "docs/runtime/evidence.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/evidence.json",
            Aggregation = new RuntimeTokenBaselineAggregation
            {
                Cohort = new RuntimeTokenBaselineCohortFreeze
                {
                    CohortId = cohortId,
                },
            },
            DecisionInputs = new RuntimeTokenPhase10DecisionInputs
            {
                ContextPackExplicitShareP95 = 0.42,
                NonContextPackExplicitShareP95 = 0.18,
                StableExplicitShareP95 = 0.11,
                DynamicExplicitShareP95 = 0.25,
                RendererShareP95Proxy = 0.36,
                ToolSchemaShareP95Proxy = 0.12,
                WrapperPolicyShareP95Proxy = 0.06,
                OtherSegmentShareP95Proxy = 0.07,
                ParentResidualShareP95 = 0.03,
                KnownProviderOverheadShareP95 = 0.04,
                UnknownUnattributedShareP95 = 0.03,
                TopP95Contributors =
                [
                    new RuntimeTokenPhase10ContributorSummary
                    {
                        SegmentKind = "goal",
                        TargetSegmentClass = "renderer",
                        ShareP95 = 0.22,
                        ContextTokensP95 = 220,
                        BillableTokensP95 = 180,
                    },
                ],
                TopTrimmedContributors =
                [
                    new RuntimeTokenPhase10TrimmedContributorSummary
                    {
                        SegmentKind = "recall",
                        TargetSegmentClass = "renderer",
                        TrimmedTokensP95 = 80,
                        TrimmedShareProxyP95 = 0.45,
                    },
                ],
                HardCapTriggerSegments = ["recall"],
            },
            Recommendation = new RuntimeTokenPhase10TargetRecommendation
            {
                Decision = decision,
                NextTrack = nextTrack,
                TargetSegment = "goal",
                TargetSegmentClass = "renderer",
                TargetShareP95 = 0.22,
                TrimmedShareProxyP95 = 0.45,
                HardCapTriggerSegment = "recall",
                DominanceBasis = ["largest_context_share_p95"],
                Confidence = "medium",
            },
        };
    }
}
