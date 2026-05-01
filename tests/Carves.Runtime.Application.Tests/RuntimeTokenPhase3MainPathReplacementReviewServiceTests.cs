using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase3MainPathReplacementReviewServiceTests
{
    [Fact]
    public void Persist_ApprovesLimitedMainPathReplacementWhenPostCanaryGatePasses()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var service = new RuntimeTokenPhase3MainPathReplacementReviewService(
            workspace.Paths,
            new RuntimeTokenWorkerWrapperCanaryService());

        var result = service.Persist(
            CreateExecutionApproval(resultDate),
            CreateCanaryResult(resultDate),
            CreateCanaryResultReview(resultDate),
            CreatePostCanaryGate(resultDate));

        Assert.Equal("approve_limited_main_path_replacement", result.ReviewVerdict);
        Assert.True(result.MainPathReplacementAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.FullRolloutAllowed);
        Assert.Equal("no_provider_agent_mediated", result.ExecutionTruthScope.ExecutionMode);
        Assert.Equal("null_worker", result.ExecutionTruthScope.WorkerBackend);
        Assert.Equal("not_claimed", result.ExecutionTruthScope.ProviderModelBehaviorClaim);
        Assert.Equal("not_applicable", result.ExecutionTruthScope.ProviderBilledCostClaim);
        Assert.True(result.Controls.GlobalKillSwitchRetained);
        Assert.True(result.Controls.PerRequestKindFallbackRetained);
        Assert.True(result.Controls.PerSurfaceFallbackRetained);
        Assert.True(result.Controls.PostRolloutAuditRequired);
        Assert.Empty(result.BlockingReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_RequiresMoreEvidenceWhenPostCanaryGateIsNotOpen()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var service = new RuntimeTokenPhase3MainPathReplacementReviewService(
            workspace.Paths,
            new RuntimeTokenWorkerWrapperCanaryService());
        var postGate = CreatePostCanaryGate(resultDate) with
        {
            GateVerdict = "blocked_pending_post_canary_evidence",
            MainPathReplacementReviewAllowed = false,
            BlockingReasons = ["low_base_count_requires_manual_review"]
        };

        var result = service.Persist(
            CreateExecutionApproval(resultDate),
            CreateCanaryResult(resultDate),
            CreateCanaryResultReview(resultDate),
            postGate);

        Assert.Equal("require_more_evidence", result.ReviewVerdict);
        Assert.False(result.MainPathReplacementAllowed);
        Assert.Contains("post_canary_gate_not_open", result.BlockingReasons);
    }

    [Fact]
    public void Persist_RejectsWhenCanaryReviewFailed()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var service = new RuntimeTokenPhase3MainPathReplacementReviewService(
            workspace.Paths,
            new RuntimeTokenWorkerWrapperCanaryService());
        var canaryResult = CreateCanaryResult(resultDate) with
        {
            Decision = "fail",
            Safety = CreateSafety() with
            {
                HardFailCount = 1,
                HardFailConditionsTriggered = ["hard_fail_count_gt_0"]
            }
        };
        var canaryReview = CreateCanaryResultReview(resultDate) with
        {
            ReviewVerdict = "fail",
            CanaryResultDecision = "fail",
            CostSavingProven = false,
            NonInferiorityPassed = false,
            MainPathReplacementReviewEligible = false,
            BlockingReasons = ["hard_fail_count_gt_0"],
            Safety = CreateSafety() with
            {
                HardFailCount = 1,
                HardFailConditionsTriggered = ["hard_fail_count_gt_0"]
            }
        };
        var postGate = CreatePostCanaryGate(resultDate) with
        {
            GateVerdict = "blocked_after_canary_failure",
            MainPathReplacementReviewAllowed = false,
            CostSavingProven = false,
            NonInferiorityPassed = false,
            BlockingReasons = ["hard_fail_count_gt_0"]
        };

        var result = service.Persist(
            CreateExecutionApproval(resultDate),
            canaryResult,
            canaryReview,
            postGate);

        Assert.Equal("reject", result.ReviewVerdict);
        Assert.False(result.MainPathReplacementAllowed);
        Assert.Contains("active_canary_result_not_pass", result.BlockingReasons);
        Assert.Contains("active_canary_result_review_not_pass", result.BlockingReasons);
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
            ApprovalReviewMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-approval-2026-04-21.md",
            ApprovalReviewJsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-approval-2026-04-21.json",
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
            BaselineEvidenceMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-attribution-baseline-evidence-result-2026-04-21.md",
            BaselineEvidenceJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/attribution-baseline-evidence-result-2026-04-21.json",
            NonInferiorityCohortMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-non-inferiority-cohort-2026-04-21.md",
            NonInferiorityCohortJsonArtifactPath = ".ai/runtime/token-optimization/phase-2/non-inferiority-cohort-2026-04-21.json",
            WorkerRecollectMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-worker-recollect-result-2026-04-21.md",
            WorkerRecollectJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/worker-recollect-result-2026-04-21.json",
            TargetSurface = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ObservationMode = "controlled_worker_request_path_replay_with_null_worker_execution_truth",
            CanaryScope = CreateCanaryScope(),
            ExecutionTruthScope = CreateExecutionTruthScope(),
            AttemptedTaskCohort = CreateAttemptedTaskCohort(),
            TokenMetrics = CreateTokenMetrics(),
            NonInferiority = CreateNonInferiority(),
            Safety = CreateSafety(),
            Decision = "pass",
            BlockingReasons = [],
            NextRequiredActions = ["open a separate main-path replacement review"],
            Notes = ["pass under current runtime mode only"]
        };
    }

    private static RuntimeTokenPhase2ActiveCanaryResultReviewResult CreateCanaryResultReview(DateOnly resultDate)
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
            CanaryScope = CreateCanaryScope(),
            ExecutionTruthScope = CreateExecutionTruthScope(),
            ObservationMode = "controlled_worker_request_path_replay_with_null_worker_execution_truth",
            AttemptedTaskCohort = CreateAttemptedTaskCohort(),
            ReviewVerdict = "pass",
            CanaryResultDecision = "pass",
            CanaryExecutionAuthorized = true,
            CostSavingObserved = true,
            CostSavingProven = true,
            NonInferiorityPassed = true,
            MainPathReplacementReviewEligible = true,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            FullRolloutAllowed = false,
            TokenMetrics = CreateTokenMetrics(),
            NonInferiority = CreateNonInferiority(),
            Safety = CreateSafety(),
            BlockingReasons = []
        };
    }

    private static RuntimeTokenPhase2PostCanaryGateResult CreatePostCanaryGate(DateOnly resultDate)
    {
        return new RuntimeTokenPhase2PostCanaryGateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-post-canary-gate-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/post-canary-gate-2026-04-21.json",
            CanaryResultReviewMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-result-review-2026-04-21.md",
            CanaryResultReviewJsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-result-review-2026-04-21.json",
            TargetSurface = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ApprovalScope = "limited_explicit_allowlist",
            CanaryScope = CreateCanaryScope(),
            ExecutionTruthScope = CreateExecutionTruthScope(),
            AttemptedTaskCohort = CreateAttemptedTaskCohort(),
            GateVerdict = "eligible_for_main_path_replacement_review",
            CanaryResultReviewVerdict = "pass",
            MainPathReplacementReviewAllowed = true,
            MainRendererReplacementAllowed = false,
            RuntimeShadowExecutionAllowed = false,
            FullRolloutAllowed = false,
            CostSavingProven = true,
            NonInferiorityPassed = true,
            TokenMetrics = CreateTokenMetrics(),
            NonInferiority = CreateNonInferiority(),
            Safety = CreateSafety(),
            BlockingReasons = []
        };
    }

    private static RuntimeTokenPhase2ActiveCanaryScope CreateCanaryScope()
    {
        return new RuntimeTokenPhase2ActiveCanaryScope
        {
            RequestKinds = ["worker"],
            SurfaceAllowlist = ["worker:system:$.instructions"],
            DefaultEnabled = false,
            AllowlistMode = "explicit"
        };
    }

    private static RuntimeTokenPhase2ExecutionTruthScope CreateExecutionTruthScope()
    {
        return new RuntimeTokenPhase2ExecutionTruthScope
        {
            ExecutionMode = "no_provider_agent_mediated",
            WorkerBackend = "null_worker",
            ProviderSdkExecutionRequired = false,
            ProviderModelBehaviorClaim = "not_claimed",
            BehavioralNonInferiorityScope = "current_runtime_mode_only",
            ProviderBilledCostClaim = "not_applicable"
        };
    }

    private static RuntimeTokenBaselineAttemptedTaskCohort CreateAttemptedTaskCohort()
    {
        return new RuntimeTokenBaselineAttemptedTaskCohort
        {
            SelectionMode = "frozen_worker_recollect_task_set",
            CoversFrozenReplayTaskSet = true,
            AttemptedTaskCount = 20,
            SuccessfulAttemptedTaskCount = 20,
            FailedAttemptedTaskCount = 0,
            IncompleteAttemptedTaskCount = 0,
            AttemptedTaskIds = ["task-001", "task-002"]
        };
    }

    private static RuntimeTokenPhase2ActiveCanaryTokenMetrics CreateTokenMetrics()
    {
        return new RuntimeTokenPhase2ActiveCanaryTokenMetrics
        {
            BaselineRequestCount = 20,
            CandidateRequestCount = 20,
            TargetSurfaceReductionRatioP95 = 0.288,
            TargetSurfaceShareP95 = 0.316,
            ExpectedWholeRequestReductionP95 = 0.091,
            ObservedWholeRequestReductionP95 = 0.056,
            BaselineTotalTokensPerSuccessfulTask = 2445.35,
            CandidateTotalTokensPerSuccessfulTask = 2259.35,
            DeltaTotalTokensPerSuccessfulTask = -186.0,
            RelativeChangeTotalTokensPerSuccessfulTask = -0.0761,
            BaselineContextWindowInputTokensP95 = 2278.6,
            CandidateContextWindowInputTokensP95 = 2092.6,
            DeltaContextWindowInputTokensP95 = -186.0,
            BaselineBillableInputTokensUncachedP95 = 2278.6,
            CandidateBillableInputTokensUncachedP95 = 2092.6,
            DeltaBillableInputTokensUncachedP95 = -186.0
        };
    }

    private static RuntimeTokenPhase2ActiveCanaryNonInferiorityResult CreateNonInferiority()
    {
        return new RuntimeTokenPhase2ActiveCanaryNonInferiorityResult
        {
            TaskSuccessRateDeltaPercentagePoints = 0,
            ReviewAdmissionRateDeltaPercentagePoints = 0,
            ConstraintViolationRateDeltaPercentagePoints = 0,
            RetryCountPerTaskRelativeDelta = 0,
            RepairCountPerTaskRelativeDelta = 0,
            ProviderContextCapHitRateDeltaPercentagePoints = 0,
            InternalPromptBudgetCapHitRateDeltaPercentagePoints = 0,
            SectionBudgetCapHitRateDeltaPercentagePoints = 0,
            TrimLoopCapHitRateDeltaPercentagePoints = 0,
            SampleSizeSufficient = true,
            ManualReviewRequired = false,
            Passed = true,
            UnavailableMetrics = [],
            ThresholdEvaluations = []
        };
    }

    private static RuntimeTokenPhase2ActiveCanarySafetyResult CreateSafety()
    {
        return new RuntimeTokenPhase2ActiveCanarySafetyResult
        {
            HardFailCount = 0,
            RollbackTriggered = false,
            ManualReviewRequired = false,
            HardFailConditionsTriggered = []
        };
    }
}
