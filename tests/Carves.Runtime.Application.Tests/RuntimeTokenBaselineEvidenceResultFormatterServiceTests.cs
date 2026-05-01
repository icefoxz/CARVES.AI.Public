using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenBaselineEvidenceResultFormatterServiceTests
{
    [Fact]
    public void Persist_WritesMarkdownAndJsonArtifactsForFrozenCohort()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = ControlPlanePaths.FromRepoRoot(workspace.RootPath);
        var cohort = CreateCohort("phase_0a_baseline");
        var aggregation = CreateAggregation(cohort);
        var outcomeBinding = CreateOutcomeBinding(cohort);

        var result = RuntimeTokenBaselineEvidenceResultFormatterService.Persist(
            paths,
            aggregation,
            outcomeBinding,
            new DateOnly(2026, 4, 21),
            generatedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero));

        var markdownPath = Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        var jsonPath = Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(markdownPath));
        Assert.True(File.Exists(jsonPath));

        var markdown = File.ReadAllText(markdownPath);
        Assert.Contains("# Runtime Token Optimization Phase 0A Attribution Baseline Evidence Result", markdown, StringComparison.Ordinal);
        Assert.Contains("## Request Kind Breakdown", markdown, StringComparison.Ordinal);
        Assert.Contains("`context_window_input_tokens_total`", markdown, StringComparison.Ordinal);
        Assert.Contains("`billable_input_tokens_uncached`", markdown, StringComparison.Ordinal);
        Assert.Contains("## Decision Inputs Ready For Phase 1.0", markdown, StringComparison.Ordinal);
        Assert.Contains("Ready for Phase 1.0 target decision: `yes`", markdown, StringComparison.Ordinal);

        var json = JsonNode.Parse(File.ReadAllText(jsonPath))!.AsObject();
        Assert.Equal("phase_0a_baseline", json["aggregation"]!["cohort"]!["cohort_id"]!.GetValue<string>());
        Assert.True(json["decision_inputs_readiness"]!["ready_for_phase10_target_decision"]!.GetValue<bool>());
        Assert.True(json["decision_inputs_readiness"]!["phase10_target_decision_allowed"]!.GetValue<bool>());
        Assert.False(json["decision_inputs_readiness"]!["cap_based_target_decision_allowed"]!.GetValue<bool>());
        Assert.Equal(0.62d, json["decision_inputs"]!["context_pack_explicit_share_p95"]!.GetValue<double>());
        Assert.InRange(json["decision_inputs"]!["renderer_share_p95_proxy"]!.GetValue<double>(), 0.649d, 0.651d);
        Assert.Equal("proceed_renderer_shadow", json["recommendation"]!["decision"]!.GetValue<string>());
        Assert.Equal("stable_explicit", json["recommendation"]!["target_segment"]!.GetValue<string>());
        Assert.Equal("renderer", json["recommendation"]!["target_segment_class"]!.GetValue<string>());
        Assert.Equal("renderer_shadow_offline", json["recommendation"]!["next_track"]!.GetValue<string>());
        Assert.Equal("proxy_only", json["hard_cap_trigger_analysis"]!["status"]!.GetValue<string>());
        Assert.False(json["hard_cap_trigger_analysis"]!["cap_based_dominance_allowed"]!.GetValue<bool>());
    }

    [Fact]
    public void Persist_RejectsMismatchedCohortInputs()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = ControlPlanePaths.FromRepoRoot(workspace.RootPath);
        var aggregation = CreateAggregation(CreateCohort("phase_0a_baseline"));
        var outcomeBinding = CreateOutcomeBinding(CreateCohort("other_cohort"));

        var error = Assert.Throws<InvalidOperationException>(() =>
            RuntimeTokenBaselineEvidenceResultFormatterService.Persist(
                paths,
                aggregation,
                outcomeBinding,
                new DateOnly(2026, 4, 21)));

        Assert.Contains("matching cohort ids", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Persist_UsesDirectCapTruthWhenAggregationIncludesDirectMetrics()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = ControlPlanePaths.FromRepoRoot(workspace.RootPath);
        var cohort = CreateCohort("phase_0a_baseline");
        var aggregation = CreateAggregation(cohort) with
        {
            CapTruth = new RuntimeTokenCapTruthSummary
            {
                RequestCount = 3,
                RequestsWithDirectCapTruth = 2,
                InternalPromptBudgetCapHitCount = 2,
                SectionBudgetCapHitCount = 1,
                TrimLoopCapHitCount = 1,
                CapTriggerSegmentKindBreakdown =
                [
                    new RuntimeTokenCountBreakdown { Key = "recall", Count = 2 },
                ],
                CapTriggerSourceBreakdown =
                [
                    new RuntimeTokenCountBreakdown { Key = "context_pack_budget_contributors", Count = 2 },
                ],
            },
        };
        var outcomeBinding = CreateOutcomeBinding(cohort);

        var result = RuntimeTokenBaselineEvidenceResultFormatterService.Persist(
            paths,
            aggregation,
            outcomeBinding,
            new DateOnly(2026, 4, 21));

        Assert.Equal("direct", result.HardCapTriggerAnalysis.Status);
        Assert.True(result.HardCapTriggerAnalysis.DirectMetricsAvailable);
        Assert.True(result.HardCapTriggerAnalysis.CapBasedDominanceAllowed);
        Assert.Equal("recall", result.HardCapTriggerAnalysis.PrimaryCapTriggerSegmentKind);
        Assert.Equal("context_pack_budget_contributors", result.HardCapTriggerAnalysis.PrimaryCapTriggerSource);
        Assert.True(result.DecisionInputsReadiness.HasDirectHardCapTruth);
        Assert.True(result.DecisionInputsReadiness.CapBasedTargetDecisionAllowed);
    }

    [Fact]
    public void Persist_KeepsPhase10TargetDecisionAllowedWhenOnlyTaskCostViewIsUntrusted()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = ControlPlanePaths.FromRepoRoot(workspace.RootPath);
        var cohort = CreateCohort("phase_0a_baseline");
        var aggregation = CreateAggregation(cohort);
        var outcomeBinding = CreateOutcomeBinding(cohort) with
        {
            SuccessfulTaskCount = 0,
            TaskCostViewTrusted = false,
            TaskCostViewBlockingReasons = ["successful_task_denominator_missing"],
            ContextWindowView = CreateOutcomeBinding(cohort).ContextWindowView with { SuccessfulTaskCount = 0, TokensPerSuccessfulTask = null },
            BillableCostView = CreateOutcomeBinding(cohort).BillableCostView with { SuccessfulTaskCount = 0, TokensPerSuccessfulTask = null },
        };

        var result = RuntimeTokenBaselineEvidenceResultFormatterService.Persist(
            paths,
            aggregation,
            outcomeBinding,
            new DateOnly(2026, 4, 21));

        Assert.True(result.DecisionInputsReadiness.AttributionShareReady);
        Assert.False(result.DecisionInputsReadiness.TaskCostReady);
        Assert.True(result.DecisionInputsReadiness.Phase10TargetDecisionAllowed);
        Assert.False(result.DecisionInputsReadiness.TotalCostClaimAllowed);
        Assert.Contains("successful_task_cost_view_untrusted", result.DecisionInputsReadiness.TaskCostBlockingReasons);
        Assert.Empty(result.DecisionInputsReadiness.BlockingReasons);
        Assert.Equal("proceed_renderer_shadow", result.Recommendation.Decision);
    }

    [Fact]
    public void Persist_ReprioritizesToToolSchemaWhenNonRendererToolSurfaceDominates()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = ControlPlanePaths.FromRepoRoot(workspace.RootPath);
        var cohort = CreateCohort("phase_0a_baseline");
        var aggregation = CreateAggregation(cohort) with
        {
            SegmentKindShares =
            [
                new RuntimeTokenSegmentShareSummary
                {
                    SegmentKind = "tool_schema",
                    RequestCountWithSegment = 3,
                    P50ShareRatio = 0.34d,
                    P95ShareRatio = 0.42d,
                    P50ContextWindowContributionTokens = 41,
                    P95ContextWindowContributionTokens = 50,
                    P50BillableContributionTokens = 33,
                    P95BillableContributionTokens = 40,
                },
                new RuntimeTokenSegmentShareSummary
                {
                    SegmentKind = "recall",
                    RequestCountWithSegment = 2,
                    P50ShareRatio = 0.15d,
                    P95ShareRatio = 0.18d,
                    P50ContextWindowContributionTokens = 18,
                    P95ContextWindowContributionTokens = 22,
                    P50BillableContributionTokens = 15,
                    P95BillableContributionTokens = 18,
                },
            ],
            ContextPackVersusNonContextPack = new RuntimeTokenBucketShareGroup
            {
                SummaryId = "context_pack_vs_non_context_pack",
                Buckets =
                [
                    new RuntimeTokenBucketShareSummary { BucketId = "context_pack_explicit", P50ShareRatio = 0.30d, P95ShareRatio = 0.28d, P50ContributionTokens = 36, P95ContributionTokens = 34, P50BillableContributionTokens = 29, P95BillableContributionTokens = 27 },
                    new RuntimeTokenBucketShareSummary { BucketId = "non_context_pack_explicit", P50ShareRatio = 0.50d, P95ShareRatio = 0.54d, P50ContributionTokens = 60, P95ContributionTokens = 65, P50BillableContributionTokens = 48, P95BillableContributionTokens = 52 },
                    new RuntimeTokenBucketShareSummary { BucketId = "parent_residual", P50ShareRatio = 0.05d, P95ShareRatio = 0.05d, P50ContributionTokens = 6, P95ContributionTokens = 6, P50BillableContributionTokens = 5, P95BillableContributionTokens = 5 },
                    new RuntimeTokenBucketShareSummary { BucketId = "known_provider_overhead", P50ShareRatio = 0.05d, P95ShareRatio = 0.05d, P50ContributionTokens = 6, P95ContributionTokens = 6, P50BillableContributionTokens = 5, P95BillableContributionTokens = 5 },
                    new RuntimeTokenBucketShareSummary { BucketId = "unknown_unattributed", P50ShareRatio = 0.10d, P95ShareRatio = 0.08d, P50ContributionTokens = 12, P95ContributionTokens = 10, P50BillableContributionTokens = 9, P95BillableContributionTokens = 8 },
                ],
            },
            StableVersusDynamic = new RuntimeTokenBucketShareGroup
            {
                SummaryId = "stable_vs_dynamic",
                Buckets =
                [
                    new RuntimeTokenBucketShareSummary { BucketId = "stable_explicit", P50ShareRatio = 0.10d, P95ShareRatio = 0.10d, P50ContributionTokens = 12, P95ContributionTokens = 12, P50BillableContributionTokens = 10, P95BillableContributionTokens = 10 },
                    new RuntimeTokenBucketShareSummary { BucketId = "dynamic_explicit", P50ShareRatio = 0.15d, P95ShareRatio = 0.18d, P50ContributionTokens = 18, P95ContributionTokens = 22, P50BillableContributionTokens = 15, P95BillableContributionTokens = 18 },
                    new RuntimeTokenBucketShareSummary { BucketId = "other_classified_explicit", P50ShareRatio = 0.55d, P95ShareRatio = 0.59d, P50ContributionTokens = 66, P95ContributionTokens = 71, P50BillableContributionTokens = 53, P95BillableContributionTokens = 57 },
                    new RuntimeTokenBucketShareSummary { BucketId = "parent_residual", P50ShareRatio = 0.05d, P95ShareRatio = 0.05d, P50ContributionTokens = 6, P95ContributionTokens = 6, P50BillableContributionTokens = 5, P95BillableContributionTokens = 5 },
                    new RuntimeTokenBucketShareSummary { BucketId = "known_provider_overhead", P50ShareRatio = 0.05d, P95ShareRatio = 0.05d, P50ContributionTokens = 6, P95ContributionTokens = 6, P50BillableContributionTokens = 5, P95BillableContributionTokens = 5 },
                    new RuntimeTokenBucketShareSummary { BucketId = "unknown_unattributed", P50ShareRatio = 0.10d, P95ShareRatio = 0.08d, P50ContributionTokens = 12, P95ContributionTokens = 10, P50BillableContributionTokens = 9, P95BillableContributionTokens = 8 },
                ],
            },
            TopTrimmedContributors =
            [
                new RuntimeTokenTrimmedContributorSummary
                {
                    SegmentKind = "tool_schema",
                    RequestCountWithTrim = 2,
                    TotalTrimmedTokensEst = 40,
                    P95TrimmedTokensEst = 30,
                },
            ],
        };
        var outcomeBinding = CreateOutcomeBinding(cohort);

        var result = RuntimeTokenBaselineEvidenceResultFormatterService.Persist(
            paths,
            aggregation,
            outcomeBinding,
            new DateOnly(2026, 4, 21));

        Assert.Equal("reprioritize_to_tool_schema", result.Recommendation.Decision);
        Assert.Equal("tool_schema", result.Recommendation.TargetSegment);
        Assert.Equal("tool_schema", result.Recommendation.TargetSegmentClass);
        Assert.Equal("tool_schema_shadow_offline", result.Recommendation.NextTrack);
    }

    [Fact]
    public void Persist_ReturnsInsufficientDataWhenAttributionShareReadinessFails()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = ControlPlanePaths.FromRepoRoot(workspace.RootPath);
        var cohort = CreateCohort("phase_0a_baseline");
        var aggregation = CreateAggregation(cohort) with
        {
            AttributionQuality = CreateAggregation(cohort).AttributionQuality with
            {
                P95UnattributedShareRatio = 0.12d,
            },
            MassLedgerCoverage = CreateAggregation(cohort).MassLedgerCoverage with
            {
                P95ClassifiedSegmentCoverageRatio = 0.70d,
            },
        };
        var outcomeBinding = CreateOutcomeBinding(cohort);

        var result = RuntimeTokenBaselineEvidenceResultFormatterService.Persist(
            paths,
            aggregation,
            outcomeBinding,
            new DateOnly(2026, 4, 21));

        Assert.Equal("insufficient_data", result.Recommendation.Decision);
        Assert.Contains("unattributed_share_exceeds_bound", result.Recommendation.BlockedCriteria);
        Assert.Contains("classified_segment_coverage_below_threshold", result.Recommendation.BlockedCriteria);
    }

    private static RuntimeTokenBaselineCohortFreeze CreateCohort(string cohortId)
    {
        return new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = cohortId,
            WindowStartUtc = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero),
            RequestKinds = ["worker", "planner"],
            TokenAccountingSourcePolicy = "mixed_with_reconciliation",
            ContextWindowView = "context_window_input_tokens_total",
            BillableCostView = "billable_input_tokens_uncached",
        };
    }

    private static RuntimeTokenBaselineAggregation CreateAggregation(RuntimeTokenBaselineCohortFreeze cohort)
    {
        return new RuntimeTokenBaselineAggregation
        {
            Cohort = cohort,
            RequestCount = 3,
            UniqueTaskCount = 2,
            RequestKindBreakdown =
            [
                new RuntimeTokenRequestKindBreakdown { RequestKind = "planner", RequestCount = 1, UniqueTaskCount = 1 },
                new RuntimeTokenRequestKindBreakdown { RequestKind = "worker", RequestCount = 2, UniqueTaskCount = 2 },
            ],
            SegmentKindShares =
            [
                new RuntimeTokenSegmentShareSummary
                {
                    SegmentKind = "goal",
                    RequestCountWithSegment = 2,
                    P50ShareRatio = 0.15d,
                    P95ShareRatio = 0.20d,
                    P50ContextWindowContributionTokens = 18,
                    P95ContextWindowContributionTokens = 24,
                    P50BillableContributionTokens = 14,
                    P95BillableContributionTokens = 20,
                },
                new RuntimeTokenSegmentShareSummary
                {
                    SegmentKind = "recall",
                    RequestCountWithSegment = 2,
                    P50ShareRatio = 0.22d,
                    P95ShareRatio = 0.30d,
                    P50ContextWindowContributionTokens = 26,
                    P95ContextWindowContributionTokens = 36,
                    P50BillableContributionTokens = 21,
                    P95BillableContributionTokens = 29,
                },
            ],
            ContextPackVersusNonContextPack = new RuntimeTokenBucketShareGroup
            {
                SummaryId = "context_pack_vs_non_context_pack",
                Buckets =
                [
                    new RuntimeTokenBucketShareSummary { BucketId = "context_pack_explicit", P50ShareRatio = 0.55d, P95ShareRatio = 0.62d, P50ContributionTokens = 66, P95ContributionTokens = 74, P50BillableContributionTokens = 52, P95BillableContributionTokens = 60 },
                    new RuntimeTokenBucketShareSummary { BucketId = "non_context_pack_explicit", P50ShareRatio = 0.25d, P95ShareRatio = 0.20d, P50ContributionTokens = 30, P95ContributionTokens = 24, P50BillableContributionTokens = 24, P95BillableContributionTokens = 19 },
                    new RuntimeTokenBucketShareSummary { BucketId = "parent_residual", P50ShareRatio = 0.05d, P95ShareRatio = 0.06d, P50ContributionTokens = 6, P95ContributionTokens = 7, P50BillableContributionTokens = 5, P95BillableContributionTokens = 6 },
                    new RuntimeTokenBucketShareSummary { BucketId = "known_provider_overhead", P50ShareRatio = 0.05d, P95ShareRatio = 0.07d, P50ContributionTokens = 6, P95ContributionTokens = 8, P50BillableContributionTokens = 5, P95BillableContributionTokens = 6 },
                    new RuntimeTokenBucketShareSummary { BucketId = "unknown_unattributed", P50ShareRatio = 0.10d, P95ShareRatio = 0.05d, P50ContributionTokens = 12, P95ContributionTokens = 6, P50BillableContributionTokens = 9, P95BillableContributionTokens = 4 },
                ],
            },
            StableVersusDynamic = new RuntimeTokenBucketShareGroup
            {
                SummaryId = "stable_vs_dynamic",
                Buckets =
                [
                    new RuntimeTokenBucketShareSummary { BucketId = "stable_explicit", P50ShareRatio = 0.30d, P95ShareRatio = 0.35d, P50ContributionTokens = 36, P95ContributionTokens = 42, P50BillableContributionTokens = 29, P95BillableContributionTokens = 34 },
                    new RuntimeTokenBucketShareSummary { BucketId = "dynamic_explicit", P50ShareRatio = 0.22d, P95ShareRatio = 0.30d, P50ContributionTokens = 26, P95ContributionTokens = 36, P50BillableContributionTokens = 21, P95BillableContributionTokens = 29 },
                    new RuntimeTokenBucketShareSummary { BucketId = "other_classified_explicit", P50ShareRatio = 0.18d, P95ShareRatio = 0.12d, P50ContributionTokens = 22, P95ContributionTokens = 14, P50BillableContributionTokens = 17, P95BillableContributionTokens = 11 },
                    new RuntimeTokenBucketShareSummary { BucketId = "parent_residual", P50ShareRatio = 0.05d, P95ShareRatio = 0.06d, P50ContributionTokens = 6, P95ContributionTokens = 7, P50BillableContributionTokens = 5, P95BillableContributionTokens = 6 },
                    new RuntimeTokenBucketShareSummary { BucketId = "known_provider_overhead", P50ShareRatio = 0.05d, P95ShareRatio = 0.07d, P50ContributionTokens = 6, P95ContributionTokens = 8, P50BillableContributionTokens = 5, P95BillableContributionTokens = 6 },
                    new RuntimeTokenBucketShareSummary { BucketId = "unknown_unattributed", P50ShareRatio = 0.20d, P95ShareRatio = 0.10d, P50ContributionTokens = 24, P95ContributionTokens = 12, P50BillableContributionTokens = 18, P95BillableContributionTokens = 8 },
                ],
            },
            TopTrimmedContributors =
            [
                new RuntimeTokenTrimmedContributorSummary
                {
                    SegmentKind = "recall",
                    RequestCountWithTrim = 2,
                    TotalTrimmedTokensEst = 35,
                    P95TrimmedTokensEst = 22,
                },
            ],
            AttributionQuality = new RuntimeTokenAttributionQualitySummary
            {
                RequestCount = 3,
                P50UnattributedTokensEst = 2,
                P95UnattributedTokensEst = 5,
                P95UnattributedShareRatio = 0.04d,
                TokenAccountingSourceBreakdown =
                [
                    new RuntimeTokenCountBreakdown { Key = "provider_actual", Count = 3 },
                ],
                KnownProviderOverheadBreakdown =
                [
                    new RuntimeTokenCountBreakdown { Key = "provider_serialization_delta", Count = 2 },
                    new RuntimeTokenCountBreakdown { Key = "none", Count = 1 },
                ],
                P50AbsoluteProviderInputDelta = 1,
                P95AbsoluteProviderInputDelta = 3,
            },
            MassLedgerCoverage = new RuntimeTokenMassLedgerCoverageSummary
            {
                RequestCount = 3,
                P50ExplicitSegmentCoverageRatio = 0.85d,
                P95ExplicitSegmentCoverageRatio = 0.92d,
                P50ClassifiedSegmentCoverageRatio = 0.80d,
                P95ClassifiedSegmentCoverageRatio = 0.88d,
                P50ParentResidualShareRatio = 0.03d,
                P95ParentResidualShareRatio = 0.05d,
                P50KnownProviderOverheadShareRatio = 0.02d,
                P95KnownProviderOverheadShareRatio = 0.04d,
                P50UnknownUnattributedShareRatio = 0.02d,
                P95UnknownUnattributedShareRatio = 0.04d,
            },
            CapTruth = new RuntimeTokenCapTruthSummary
            {
                RequestCount = 3,
            },
            ContextWindowView = new RuntimeTokenViewSummary
            {
                ViewId = "context_window_input_tokens_total",
                RequestCount = 3,
                P50Tokens = 120,
                P95Tokens = 140,
                AverageTokens = 123.3d,
            },
            BillableCostView = new RuntimeTokenViewSummary
            {
                ViewId = "billable_input_tokens_uncached",
                RequestCount = 3,
                P50Tokens = 90,
                P95Tokens = 110,
                AverageTokens = 93.3d,
            },
        };
    }

    private static RuntimeTokenOutcomeBinding CreateOutcomeBinding(RuntimeTokenBaselineCohortFreeze cohort)
    {
        return new RuntimeTokenOutcomeBinding
        {
            Cohort = cohort,
            TaskCostScope = new RuntimeTokenTaskCostScopeSummary
            {
                IncludedByDefault = ["worker", "planner"],
            },
            IncludedRequestCount = 3,
            ExcludedRequestCount = 0,
            UnboundIncludedRequestCount = 0,
            UnboundIncludedMandatoryRequestCount = 0,
            UnboundIncludedOptionalRequestCount = 0,
            UnboundIncludedContextTokens = 0,
            UnboundIncludedBillableTokens = 0,
            AttemptedTaskCount = 2,
            SuccessfulTaskCount = 1,
            TaskCostViewTrusted = true,
            TaskOutcomeBreakdown =
            [
                new RuntimeTokenTaskOutcomeBreakdown { TaskStatus = DomainTaskStatus.Completed, Successful = true, TaskCount = 1 },
                new RuntimeTokenTaskOutcomeBreakdown { TaskStatus = DomainTaskStatus.Failed, Successful = false, TaskCount = 1 },
            ],
            RunReportCoverage = new RuntimeTokenRunReportCoverageSummary
            {
                IncludedRequestsWithRunId = 2,
                IncludedRequestsWithMatchingRunReport = 2,
                IncludedRequestsMissingMatchingRunReport = 0,
            },
            ContextWindowView = new RuntimeTokenTaskCostViewSummary
            {
                ViewId = "context_window_input_tokens_total",
                IncludedRequestCount = 3,
                AttemptedTaskCount = 2,
                SuccessfulTaskCount = 1,
                TotalInputTokens = 200,
                TotalCachedInputTokens = 20,
                TotalOutputTokens = 30,
                TotalReasoningTokens = 0,
                TotalTokens = 230,
                TokensPerSuccessfulTask = 230,
            },
            BillableCostView = new RuntimeTokenTaskCostViewSummary
            {
                ViewId = "billable_input_tokens_uncached",
                IncludedRequestCount = 3,
                AttemptedTaskCount = 2,
                SuccessfulTaskCount = 1,
                TotalInputTokens = 170,
                TotalCachedInputTokens = 20,
                TotalOutputTokens = 30,
                TotalReasoningTokens = 0,
                TotalTokens = 200,
                TokensPerSuccessfulTask = 200,
            },
        };
    }
}
