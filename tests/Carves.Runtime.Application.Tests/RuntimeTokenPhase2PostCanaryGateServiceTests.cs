using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase2PostCanaryGateServiceTests
{
    [Fact]
    public void Persist_BlocksWhenCanaryReviewIsInconclusive()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var review = CreateResultReview(resultDate) with
        {
            ReviewVerdict = "inconclusive",
            CostSavingProven = false,
            NonInferiorityPassed = false,
            MainPathReplacementReviewEligible = false,
            BlockingReasons = ["behavioral_non_inferiority_metrics_not_observed", "low_base_count_requires_manual_review"]
        };

        var result = RuntimeTokenPhase2PostCanaryGateService.Persist(workspace.Paths, review, resultDate);

        Assert.Equal("blocked_pending_post_canary_evidence", result.GateVerdict);
        Assert.Equal("no_provider_agent_mediated", result.ExecutionTruthScope.ExecutionMode);
        Assert.Equal("null_worker", result.ExecutionTruthScope.WorkerBackend);
        Assert.False(result.MainPathReplacementReviewAllowed);
        Assert.Contains("main_path_replacement_review_not_eligible", result.BlockingReasons);
        Assert.Contains("cost_saving_not_proven", result.BlockingReasons);
        Assert.Contains("non_inferiority_not_passed", result.BlockingReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_AllowsSeparateReplacementReviewOnlyWhenCanaryPasses()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var review = CreateResultReview(resultDate) with
        {
            ReviewVerdict = "pass",
            CostSavingProven = true,
            NonInferiorityPassed = true,
            MainPathReplacementReviewEligible = true,
            BlockingReasons = []
        };

        var result = RuntimeTokenPhase2PostCanaryGateService.Persist(workspace.Paths, review, resultDate);

        Assert.Equal("eligible_for_main_path_replacement_review", result.GateVerdict);
        Assert.Equal("current_runtime_mode_only", result.ExecutionTruthScope.BehavioralNonInferiorityScope);
        Assert.Equal("not_claimed", result.ExecutionTruthScope.ProviderModelBehaviorClaim);
        Assert.Equal("not_applicable", result.ExecutionTruthScope.ProviderBilledCostClaim);
        Assert.True(result.MainPathReplacementReviewAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.False(result.FullRolloutAllowed);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void Persist_BlocksAfterCanaryFailure()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var review = CreateResultReview(resultDate) with
        {
            ReviewVerdict = "fail",
            CostSavingProven = false,
            NonInferiorityPassed = false,
            MainPathReplacementReviewEligible = false,
            BlockingReasons = ["hard_fail_count_gt_0"],
            Safety = CreateResultReview(resultDate).Safety with
            {
                HardFailCount = 1,
                HardFailConditionsTriggered = ["hard_fail_count_gt_0"]
            }
        };

        var result = RuntimeTokenPhase2PostCanaryGateService.Persist(workspace.Paths, review, resultDate);

        Assert.Equal("blocked_after_canary_failure", result.GateVerdict);
        Assert.False(result.MainPathReplacementReviewAllowed);
        Assert.Contains("hard_fail_count_gt_0", result.BlockingReasons);
    }

    private static RuntimeTokenPhase2ActiveCanaryResultReviewResult CreateResultReview(DateOnly resultDate)
    {
        return new RuntimeTokenPhase2ActiveCanaryResultReviewResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-result-review-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-result-review-2026-04-21.json",
            ExecutionApprovalMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-execution-approval-2026-04-21.md",
            ExecutionApprovalJsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-execution-approval-2026-04-21.json",
            CanaryResultMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-result-2026-04-21.md",
            CanaryResultJsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-result-2026-04-21.json",
            TargetSurface = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ApprovalScope = "limited_explicit_allowlist",
            CanaryScope = new RuntimeTokenPhase2ActiveCanaryScope
            {
                RequestKinds = ["worker"],
                SurfaceAllowlist = ["worker:system:$.instructions"],
                DefaultEnabled = false,
                AllowlistMode = "explicit"
            },
            ExecutionTruthScope = new RuntimeTokenPhase2ExecutionTruthScope
            {
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkExecutionRequired = false,
                ProviderModelBehaviorClaim = "not_claimed",
                BehavioralNonInferiorityScope = "current_runtime_mode_only",
                ProviderBilledCostClaim = "not_applicable",
            },
            ObservationMode = "controlled_worker_request_path_replay",
            ReviewVerdict = "inconclusive",
            CanaryResultDecision = "inconclusive",
            CanaryExecutionAuthorized = true,
            CostSavingObserved = true,
            CostSavingProven = false,
            NonInferiorityPassed = false,
            MainPathReplacementReviewEligible = false,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            FullRolloutAllowed = false,
            TokenMetrics = new RuntimeTokenPhase2ActiveCanaryTokenMetrics
            {
                BaselineRequestCount = 5,
                CandidateRequestCount = 5,
                TargetSurfaceReductionRatioP95 = 0.288,
                TargetSurfaceShareP95 = 0.316,
                ExpectedWholeRequestReductionP95 = 0.091,
                ObservedWholeRequestReductionP95 = 0.082,
                BaselineTotalTokensPerSuccessfulTask = 2142.8,
                CandidateTotalTokensPerSuccessfulTask = 1956.8,
                DeltaTotalTokensPerSuccessfulTask = -186.0,
                RelativeChangeTotalTokensPerSuccessfulTask = -0.0868,
                BaselineContextWindowInputTokensP95 = 2278.6,
                CandidateContextWindowInputTokensP95 = 2092.6,
                DeltaContextWindowInputTokensP95 = -186.0,
                BaselineBillableInputTokensUncachedP95 = 2278.6,
                CandidateBillableInputTokensUncachedP95 = 2092.6,
                DeltaBillableInputTokensUncachedP95 = -186.0
            },
            NonInferiority = new RuntimeTokenPhase2ActiveCanaryNonInferiorityResult
            {
                SampleSizeSufficient = false,
                ManualReviewRequired = true,
                Passed = false,
                UnavailableMetrics = ["task_success_rate"],
                ThresholdEvaluations = []
            },
            Safety = new RuntimeTokenPhase2ActiveCanarySafetyResult
            {
                HardFailCount = 0,
                RollbackTriggered = false,
                ManualReviewRequired = true,
                HardFailConditionsTriggered = []
            },
            BlockingReasons = ["behavioral_non_inferiority_metrics_not_observed", "low_base_count_requires_manual_review"]
        };
    }
}
