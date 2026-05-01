using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase2ActiveCanaryResultReviewServiceTests
{
    [Fact]
    public void Persist_ReturnsInconclusiveWhenBehaviorEvidenceIsUnavailable()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var executionApproval = CreateExecutionApproval(resultDate);
        var canaryResult = CreateCanaryResult(resultDate) with
        {
            Decision = "inconclusive",
            BlockingReasons = ["behavioral_non_inferiority_metrics_not_observed", "low_base_count_requires_manual_review"],
            NonInferiority = CreateCanaryResult(resultDate).NonInferiority with
            {
                SampleSizeSufficient = false,
                ManualReviewRequired = true,
                Passed = false,
                UnavailableMetrics = ["task_success_rate", "review_admission_rate"]
            },
            Safety = CreateCanaryResult(resultDate).Safety with
            {
                ManualReviewRequired = true
            }
        };

        var result = RuntimeTokenPhase2ActiveCanaryResultReviewService.Persist(
            workspace.Paths,
            executionApproval,
            canaryResult,
            resultDate);

        Assert.Equal("inconclusive", result.ReviewVerdict);
        Assert.Equal("no_provider_agent_mediated", result.ExecutionTruthScope.ExecutionMode);
        Assert.Equal("null_worker", result.ExecutionTruthScope.WorkerBackend);
        Assert.True(result.CostSavingObserved);
        Assert.False(result.CostSavingProven);
        Assert.False(result.MainPathReplacementReviewEligible);
        Assert.Contains("behavioral_non_inferiority_metrics_not_observed", result.BlockingReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        var markdown = File.ReadAllText(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("## Execution Truth Scope", markdown, StringComparison.Ordinal);
        Assert.Contains("Worker backend: `null_worker`", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Persist_ReturnsPassWhenCanaryPassesAndCostDrops()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var executionApproval = CreateExecutionApproval(resultDate);
        var canaryResult = CreateCanaryResult(resultDate) with
        {
            Decision = "pass",
            BlockingReasons = [],
            NonInferiority = CreateCanaryResult(resultDate).NonInferiority with
            {
                SampleSizeSufficient = true,
                ManualReviewRequired = false,
                Passed = true,
                UnavailableMetrics = []
            },
            Safety = CreateCanaryResult(resultDate).Safety with
            {
                ManualReviewRequired = false
            }
        };

        var result = RuntimeTokenPhase2ActiveCanaryResultReviewService.Persist(
            workspace.Paths,
            executionApproval,
            canaryResult,
            resultDate);

        Assert.Equal("pass", result.ReviewVerdict);
        Assert.Equal("current_runtime_mode_only", result.ExecutionTruthScope.BehavioralNonInferiorityScope);
        Assert.Equal("not_claimed", result.ExecutionTruthScope.ProviderModelBehaviorClaim);
        Assert.Equal("not_applicable", result.ExecutionTruthScope.ProviderBilledCostClaim);
        Assert.True(result.CostSavingObserved);
        Assert.True(result.CostSavingProven);
        Assert.True(result.MainPathReplacementReviewEligible);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.False(result.FullRolloutAllowed);
        Assert.Empty(result.BlockingReasons);
        var markdown = File.ReadAllText(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("Behavioral non-inferiority scope: `current_runtime_mode_only`", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Persist_ReturnsFailWhenHardFailOccurs()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var executionApproval = CreateExecutionApproval(resultDate);
        var canaryResult = CreateCanaryResult(resultDate) with
        {
            Decision = "fail",
            Safety = CreateCanaryResult(resultDate).Safety with
            {
                HardFailCount = 1,
                HardFailConditionsTriggered = ["hard_fail_count_gt_0"]
            }
        };

        var result = RuntimeTokenPhase2ActiveCanaryResultReviewService.Persist(
            workspace.Paths,
            executionApproval,
            canaryResult,
            resultDate);

        Assert.Equal("fail", result.ReviewVerdict);
        Assert.False(result.MainPathReplacementReviewEligible);
        Assert.Contains("hard_fail_count_gt_0", result.BlockingReasons);
    }

    private static RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult CreateExecutionApproval(DateOnly resultDate)
    {
        return new RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-execution-approval-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-execution-approval-2026-04-21.json",
            TargetSurface = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ApprovalScope = "limited_explicit_allowlist",
            CanaryRequestKindAllowlist = ["worker"],
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            DefaultEnabled = false,
            RollbackPlanFrozen = true,
            NonInferiorityCohortFrozen = true,
            ReviewVerdict = "approved_for_active_canary_execution",
            ActiveCanaryApproved = true,
            CanaryExecutionAuthorized = true,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            ExpectedWholeRequestReductionP95 = 0.091,
            BlockingReasons = []
        };
    }

    private static RuntimeTokenPhase2ActiveCanaryResult CreateCanaryResult(DateOnly resultDate)
    {
        return new RuntimeTokenPhase2ActiveCanaryResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-result-2026-04-21.json",
            ExecutionApprovalMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-execution-approval-2026-04-21.md",
            ExecutionApprovalJsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-execution-approval-2026-04-21.json",
            TargetSurface = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ObservationMode = "controlled_worker_request_path_replay",
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
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = 20,
                SuccessfulAttemptedTaskCount = 20,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0,
                AttemptedTaskIds = Enumerable.Range(1, 20).Select(index => $"T-WORKER-{index:000}").ToArray(),
                Tasks = Enumerable.Range(1, 20).Select(index => new RuntimeTokenBaselineAttemptedTaskRecord
                {
                    TaskId = $"T-WORKER-{index:000}",
                    RunId = $"RUN-T-WORKER-{index:000}-001",
                    WorkerBackend = "null_worker",
                    TaskStatus = "Completed",
                    LatestRunStatus = "Completed",
                    Attempted = true,
                    SuccessfulAttempted = true,
                    ReviewAdmissionAccepted = true,
                    ConstraintViolationObserved = false,
                }).ToArray(),
            },
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
                ProviderContextCapHitRateDeltaPercentagePoints = 0,
                InternalPromptBudgetCapHitRateDeltaPercentagePoints = 0,
                SectionBudgetCapHitRateDeltaPercentagePoints = 0,
                TrimLoopCapHitRateDeltaPercentagePoints = 0,
                SampleSizeSufficient = true,
                ManualReviewRequired = false,
                Passed = true,
                ThresholdEvaluations = []
            },
            Safety = new RuntimeTokenPhase2ActiveCanarySafetyResult
            {
                HardFailCount = 0,
                RollbackTriggered = false,
                ManualReviewRequired = false,
                HardFailConditionsTriggered = []
            },
            Decision = "pass",
            BlockingReasons = [],
            NextRequiredActions = [],
            Notes = []
        };
    }
}
