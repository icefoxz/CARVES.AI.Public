using System.Text.Json.Nodes;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenBaselineReadinessGateServiceTests
{
    [Fact]
    public void Persist_EmitsReadyVerdictWhenEvidenceResultSatisfiesGate()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = ControlPlanePaths.FromRepoRoot(workspace.RootPath);
        var result = CreateEvidenceResult(
            CreateCohort("phase_0a_baseline"),
            p95UnattributedShareRatio: 0.04d,
            readyForPhase10: true,
            successfulTaskCount: 1);

        var gate = RuntimeTokenBaselineReadinessGateService.Persist(
            paths,
            result,
            result.ResultDate,
            evaluatedAtUtc: new DateTimeOffset(2026, 4, 21, 14, 0, 0, TimeSpan.Zero));

        Assert.Equal("ready_for_phase_1_target_work", gate.Verdict);
        Assert.True(gate.UnlocksPhase10TargetDecision);
        Assert.Empty(gate.BlockingReasons);
        Assert.True(gate.Readiness.AttributionShareReady);
        Assert.True(gate.Readiness.Phase10TargetDecisionAllowed);
        Assert.False(gate.Readiness.CapBasedTargetDecisionAllowed);
        Assert.Contains(gate.Checks, check => check.CheckId == "cap_based_dominance_truth_ready" && !check.Blocking && !check.Passed);

        var markdownPath = Path.Combine(workspace.RootPath, "docs", "runtime", "runtime-token-optimization-phase-0a-readiness-gate-result-2026-04-21.md");
        var jsonPath = Path.Combine(workspace.RootPath, ".ai", "runtime", "token-optimization", "phase-0a", "readiness-gate-result-2026-04-21.json");
        Assert.True(File.Exists(markdownPath));
        Assert.True(File.Exists(jsonPath));

        var markdown = File.ReadAllText(markdownPath);
        Assert.Contains("Verdict: `ready_for_phase_1_target_work`", markdown, StringComparison.Ordinal);

        var json = JsonNode.Parse(File.ReadAllText(jsonPath))!.AsObject();
        Assert.Equal("ready_for_phase_1_target_work", json["verdict"]!.GetValue<string>());
        Assert.True(json["unlocks_phase10_target_decision"]!.GetValue<bool>());
        Assert.True(json["readiness"]!["phase10_target_decision_allowed"]!.GetValue<bool>());
        Assert.False(json["readiness"]!["cap_based_target_decision_allowed"]!.GetValue<bool>());
    }

    [Fact]
    public void Persist_EmitsInsufficientDataWhenUnattributedShareExceedsBoundWithoutProviderClassification()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = ControlPlanePaths.FromRepoRoot(workspace.RootPath);
        var result = CreateEvidenceResult(
            CreateCohort("phase_0a_baseline"),
            p95UnattributedShareRatio: 0.12d,
            readyForPhase10: true,
            successfulTaskCount: 1,
            knownProviderOverheadBreakdown:
            [
                new RuntimeTokenCountBreakdown { Key = "none", Count = 3 },
            ]);

        var gate = RuntimeTokenBaselineReadinessGateService.Persist(paths, result, result.ResultDate);

        Assert.Equal("insufficient_data", gate.Verdict);
        Assert.False(gate.UnlocksPhase10TargetDecision);
        Assert.Contains("unattributed_tokens_within_bound_or_classified", gate.BlockingReasons);
        Assert.Contains(gate.Checks, check => check.CheckId == "unattributed_tokens_within_bound_or_classified" && !check.Passed);
    }

    [Fact]
    public void Persist_KeepsPhase10VerdictReadyWhenOnlyTaskCostClaimIsBlocked()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = ControlPlanePaths.FromRepoRoot(workspace.RootPath);
        var result = CreateEvidenceResult(
            CreateCohort("phase_0a_baseline"),
            p95UnattributedShareRatio: 0.04d,
            readyForPhase10: true,
            successfulTaskCount: 0,
            taskCostBlockingReasons:
            [
                "successful_task_cost_view_untrusted",
            ]);

        var gate = RuntimeTokenBaselineReadinessGateService.Persist(paths, result, result.ResultDate);

        Assert.Equal("ready_for_phase_1_target_work", gate.Verdict);
        Assert.Empty(gate.BlockingReasons);
        Assert.True(gate.Readiness.Phase10TargetDecisionAllowed);
        Assert.False(gate.Readiness.TaskCostReady);
        Assert.False(gate.Readiness.TotalCostClaimAllowed);
        Assert.Contains("successful_task_cost_view_untrusted", gate.Readiness.TaskCostBlockingReasons);
    }

    private static RuntimeTokenBaselineEvidenceResult CreateEvidenceResult(
        RuntimeTokenBaselineCohortFreeze cohort,
        double p95UnattributedShareRatio,
        bool readyForPhase10,
        int successfulTaskCount,
        IReadOnlyList<RuntimeTokenCountBreakdown>? knownProviderOverheadBreakdown = null,
        IReadOnlyList<string>? formatterBlockingReasons = null,
        IReadOnlyList<string>? taskCostBlockingReasons = null)
    {
        return new RuntimeTokenBaselineEvidenceResult
        {
            ResultDate = new DateOnly(2026, 4, 21),
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-attribution-baseline-evidence-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/attribution-baseline-evidence-result-2026-04-21.json",
            Aggregation = new RuntimeTokenBaselineAggregation
            {
                Cohort = cohort,
                RequestCount = 3,
                UniqueTaskCount = 2,
                RequestKindBreakdown =
                [
                    new RuntimeTokenRequestKindBreakdown { RequestKind = "worker", RequestCount = 2, UniqueTaskCount = 2 },
                    new RuntimeTokenRequestKindBreakdown { RequestKind = "planner", RequestCount = 1, UniqueTaskCount = 1 },
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
                    P95UnattributedShareRatio = p95UnattributedShareRatio,
                    TokenAccountingSourceBreakdown =
                    [
                        new RuntimeTokenCountBreakdown { Key = "provider_actual", Count = 3 },
                    ],
                    KnownProviderOverheadBreakdown = knownProviderOverheadBreakdown ??
                    [
                        new RuntimeTokenCountBreakdown { Key = "provider_serialization_delta", Count = 3 },
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
            },
            OutcomeBinding = new RuntimeTokenOutcomeBinding
            {
                Cohort = cohort,
                IncludedRequestCount = 3,
                ExcludedRequestCount = 0,
                UnboundIncludedRequestCount = 0,
                UnboundIncludedMandatoryRequestCount = 0,
                UnboundIncludedOptionalRequestCount = 0,
                UnboundIncludedContextTokens = 0,
                UnboundIncludedBillableTokens = 0,
                AttemptedTaskCount = 2,
                SuccessfulTaskCount = successfulTaskCount,
                TaskCostViewTrusted = successfulTaskCount > 0,
                TaskCostViewBlockingReasons = successfulTaskCount > 0 ? Array.Empty<string>() : ["successful_task_denominator_missing"],
                TaskOutcomeBreakdown =
                [
                    new RuntimeTokenTaskOutcomeBreakdown { TaskStatus = DomainTaskStatus.Completed, Successful = true, TaskCount = successfulTaskCount > 0 ? 1 : 0 },
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
                    SuccessfulTaskCount = successfulTaskCount,
                    TotalInputTokens = 200,
                    TotalCachedInputTokens = 20,
                    TotalOutputTokens = 30,
                    TotalReasoningTokens = 0,
                    TotalTokens = 230,
                    TokensPerSuccessfulTask = successfulTaskCount > 0 ? 230 : null,
                },
                BillableCostView = new RuntimeTokenTaskCostViewSummary
                {
                    ViewId = "billable_input_tokens_uncached",
                    IncludedRequestCount = 3,
                    AttemptedTaskCount = 2,
                    SuccessfulTaskCount = successfulTaskCount,
                    TotalInputTokens = 170,
                    TotalCachedInputTokens = 20,
                    TotalOutputTokens = 30,
                    TotalReasoningTokens = 0,
                    TotalTokens = 200,
                    TokensPerSuccessfulTask = successfulTaskCount > 0 ? 200 : null,
                },
            },
            HardCapTriggerAnalysis = new RuntimeTokenHardCapTriggerAnalysis
            {
                Status = "proxy_only",
                DirectMetricsAvailable = false,
                ProxyMetricsAvailable = true,
                CapBasedDominanceAllowed = false,
                PrimaryTrimPressureSegmentKind = "recall",
                PrimaryTrimmedTokensP95 = 22,
                UsesTrimPressureProxy = true,
                Notes = ["trim proxy only"],
            },
            DecisionInputsReadiness = new RuntimeTokenDecisionInputsReadiness
            {
                HasRequestKindBreakdown = true,
                HasP95SegmentShares = true,
                HasRendererVsNonRendererSplit = true,
                HasStableVsDynamicSplit = true,
                HasTrimPressureVisibility = true,
                HasAttributionQuality = true,
                HasContextWindowView = true,
                HasBillableCostView = true,
                HasSuccessfulTaskCostView = successfulTaskCount > 0,
                HasExplicitHardCapTriggerAnalysis = true,
                HasDirectHardCapTruth = false,
                CapBasedTargetDecisionAllowed = false,
                AttributionShareReady = readyForPhase10,
                TaskCostReady = successfulTaskCount > 0 && (taskCostBlockingReasons?.Count ?? 0) == 0,
                RouteReinjectionReady = true,
                CapTruthReady = false,
                Phase10TargetDecisionAllowed = readyForPhase10,
                TotalCostClaimAllowed = successfulTaskCount > 0 && (taskCostBlockingReasons?.Count ?? 0) == 0,
                ActiveCanaryAllowed = false,
                ReadyForPhase10TargetDecision = readyForPhase10,
                BlockingReasons = formatterBlockingReasons ?? Array.Empty<string>(),
                AttributionShareBlockingReasons = formatterBlockingReasons ?? Array.Empty<string>(),
                TaskCostBlockingReasons = taskCostBlockingReasons ?? Array.Empty<string>(),
                RouteReinjectionBlockingReasons = Array.Empty<string>(),
                CapTruthBlockingReasons = ["direct_cap_truth_missing"],
                ActiveCanaryBlockingReasons = ["phase_0a_cannot_unlock_active_canary"],
            },
        };
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
}
