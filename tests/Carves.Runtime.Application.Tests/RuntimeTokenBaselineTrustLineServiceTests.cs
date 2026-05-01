using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenBaselineTrustLineServiceTests
{
    [Fact]
    public void Persist_ClassifiesTrustedRecomputedLine()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var cohort = CreateCohort();
        var evidenceResult = CreateEvidenceResult(cohort);
        var readinessGateResult = new RuntimeTokenBaselineReadinessGateResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            Verdict = "ready_for_phase_1_target_work",
            UnlocksPhase10TargetDecision = true,
            Readiness = new RuntimeTokenBaselineReadinessDimensions
            {
                AttributionShareReady = true,
                TaskCostReady = true,
                RouteReinjectionReady = true,
                CapTruthReady = false,
                Phase10TargetDecisionAllowed = true,
                CapBasedTargetDecisionAllowed = false,
                TotalCostClaimAllowed = true,
            },
        };
        var recomputeResult = CreateRecomputeResult(cohort, phase10Allowed: true);

        var result = RuntimeTokenBaselineTrustLineService.Persist(
            paths,
            evidenceResult,
            readinessGateResult,
            recomputeResult,
            new DateOnly(2026, 4, 21),
            evaluatedAtUtc: new DateTimeOffset(2026, 4, 21, 13, 0, 0, TimeSpan.Zero));

        Assert.Equal("recomputed_trusted_for_phase_1_target_decision", result.TrustLineClassification);
        Assert.True(result.SupersedesPreLedgerLine);
        Assert.True(result.Phase10TargetDecisionMayReferenceThisLine);
        Assert.False(result.CapBasedTargetDecisionAllowed);
        Assert.True(result.TotalCostClaimAllowed);
        Assert.False(result.Phase12TargetedCompactCandidateAllowed);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "docs", "runtime", "runtime-token-optimization-phase-0a-trust-line-result-2026-04-21.md")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai", "runtime", "token-optimization", "phase-0a", "trust-line-result-2026-04-21.json")));
    }

    [Fact]
    public void Persist_KeepsInsufficientLineBlockedForPhase10()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var cohort = CreateCohort();
        var evidenceResult = CreateEvidenceResult(cohort);
        var readinessGateResult = new RuntimeTokenBaselineReadinessGateResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            Verdict = "insufficient_data",
            UnlocksPhase10TargetDecision = false,
            Readiness = new RuntimeTokenBaselineReadinessDimensions
            {
                AttributionShareReady = false,
                TaskCostReady = false,
                RouteReinjectionReady = false,
                CapTruthReady = false,
                Phase10TargetDecisionAllowed = false,
                CapBasedTargetDecisionAllowed = false,
                TotalCostClaimAllowed = false,
                AttributionShareBlockingReasons = ["classified_segment_coverage_below_threshold"],
            },
            BlockingReasons = ["classified_segment_coverage_below_threshold"],
        };
        var recomputeResult = CreateRecomputeResult(cohort, phase10Allowed: false);

        var result = RuntimeTokenBaselineTrustLineService.Persist(
            paths,
            evidenceResult,
            readinessGateResult,
            recomputeResult,
            new DateOnly(2026, 4, 21),
            evaluatedAtUtc: new DateTimeOffset(2026, 4, 21, 13, 5, 0, TimeSpan.Zero));

        Assert.Equal("recomputed_but_insufficient_data_for_phase_1_target_decision", result.TrustLineClassification);
        Assert.False(result.Phase10TargetDecisionMayReferenceThisLine);
        Assert.False(result.TotalCostClaimAllowed);
        Assert.Contains("classified_segment_coverage_below_threshold", result.BlockingReasons);
    }

    private static RuntimeTokenBaselineCohortFreeze CreateCohort()
    {
        return new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = "phase_0a_baseline",
            WindowStartUtc = new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 21, 23, 59, 0, TimeSpan.Zero),
            RequestKinds = ["worker"],
            TokenAccountingSourcePolicy = "provider_actual_preferred_with_reconciliation",
            ContextWindowView = "context_window_input_tokens_total",
            BillableCostView = "billable_input_tokens_uncached",
        };
    }

    private static RuntimeTokenBaselineEvidenceResult CreateEvidenceResult(RuntimeTokenBaselineCohortFreeze cohort)
    {
        return new RuntimeTokenBaselineEvidenceResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            MarkdownArtifactPath = "docs/runtime/evidence.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/evidence.json",
            Aggregation = new RuntimeTokenBaselineAggregation
            {
                Cohort = cohort,
                RequestCount = 1,
            },
        };
    }

    private static RuntimeTokenBaselineRecomputeResult CreateRecomputeResult(RuntimeTokenBaselineCohortFreeze cohort, bool phase10Allowed)
    {
        return new RuntimeTokenBaselineRecomputeResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            Cohort = cohort,
            ReadinessVerdict = phase10Allowed ? "ready_for_phase_1_target_work" : "insufficient_data",
            Phase10TargetDecisionAllowed = phase10Allowed,
            RecommendationDecision = phase10Allowed ? "proceed_renderer_shadow" : "insufficient_data",
            RecommendationNextTrack = phase10Allowed ? "renderer_shadow_offline" : "insufficient_data",
            MarkdownArtifactPath = "docs/runtime/recompute.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/recompute.json",
            CohortJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/cohort.json",
            EvidenceMarkdownArtifactPath = "docs/runtime/evidence.md",
            EvidenceJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/evidence.json",
            ReadinessMarkdownArtifactPath = "docs/runtime/readiness.md",
            ReadinessJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/readiness.json",
        };
    }
}
