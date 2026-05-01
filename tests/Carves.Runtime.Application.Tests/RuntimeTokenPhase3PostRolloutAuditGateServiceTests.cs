using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase3PostRolloutAuditGateServiceTests
{
    [Fact]
    public void Persist_BlocksWhenLimitedMainPathDefaultIsNotObserved()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);

        var result = RuntimeTokenPhase3PostRolloutAuditGateService.Persist(
            workspace.Paths,
            CreateReview(resultDate),
            CreateScopeFreeze(resultDate),
            CreatePostRolloutEvidence(resultDate) with
            {
                EvidenceStatus = "incomplete_post_rollout_evidence",
                LimitedMainPathImplementationObserved = false,
                PostRolloutBehaviorEvidenceObserved = false,
                PostRolloutTokenEvidenceObserved = false,
                BlockingReasons =
                [
                    "limited_main_path_default_not_observed_on_frozen_scope",
                    "post_rollout_behavior_evidence_not_observed",
                    "post_rollout_token_evidence_not_observed"
                ]
            },
            resultDate);

        Assert.Equal("blocked_pending_post_rollout_evidence", result.GateVerdict);
        Assert.False(result.PostRolloutAuditPassed);
        Assert.False(result.LimitedMainPathImplementationObserved);
        Assert.False(result.MainPathReplacementRetained);
        Assert.False(result.RequestKindExpansionAllowed);
        Assert.False(result.SurfaceExpansionAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.FullRolloutAllowed);
        Assert.Contains("limited_main_path_default_not_enabled", result.BlockingReasons);
        Assert.Contains("post_rollout_behavior_evidence_not_observed", result.BlockingReasons);
        Assert.Contains("post_rollout_token_evidence_not_observed", result.BlockingReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_BlocksWhenScopeFreezeWasNotApproved()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var scopeFreeze = CreateScopeFreeze(resultDate) with
        {
            FreezeVerdict = "scope_freeze_blocked",
            ImplementationScopeFrozen = false,
            LimitedMainPathImplementationAllowed = false,
            BlockingReasons = ["request_kind_out_of_scope"]
        };

        var result = RuntimeTokenPhase3PostRolloutAuditGateService.Persist(
            workspace.Paths,
            CreateReview(resultDate),
            scopeFreeze,
            CreatePostRolloutEvidence(resultDate),
            resultDate);

        Assert.Equal("blocked_pending_post_rollout_evidence", result.GateVerdict);
        Assert.Contains("replacement_scope_not_frozen", result.BlockingReasons);
    }

    [Fact]
    public void Persist_PassesWhenPostRolloutEvidenceIsObserved()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);

        var result = RuntimeTokenPhase3PostRolloutAuditGateService.Persist(
            workspace.Paths,
            CreateReview(resultDate),
            CreateScopeFreeze(resultDate),
            CreatePostRolloutEvidence(resultDate),
            resultDate);

        Assert.Equal("post_rollout_audit_passed", result.GateVerdict);
        Assert.True(result.PostRolloutAuditPassed);
        Assert.True(result.LimitedMainPathImplementationObserved);
        Assert.True(result.MainPathReplacementRetained);
        Assert.Empty(result.BlockingReasons);
        Assert.False(result.RequestKindExpansionAllowed);
        Assert.False(result.SurfaceExpansionAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.FullRolloutAllowed);
    }

    private static RuntimeTokenPhase3MainPathReplacementReviewResult CreateReview(DateOnly resultDate)
    {
        return new RuntimeTokenPhase3MainPathReplacementReviewResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-3-main-path-replacement-review-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-3/main-path-replacement-review-2026-04-21.json",
            MainPathReplacementAllowed = true,
            MainRendererReplacementAllowed = false,
            RuntimeShadowExecutionAllowed = false,
            FullRolloutAllowed = false,
            ReviewVerdict = "approve_limited_main_path_replacement",
            TargetSurface = "worker:system:$.instructions",
            RequestKind = "worker",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ExecutionTruthScope = new RuntimeTokenPhase2ExecutionTruthScope
            {
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkExecutionRequired = false,
                ProviderModelBehaviorClaim = "not_claimed",
                BehavioralNonInferiorityScope = "current_runtime_mode_only",
                ProviderBilledCostClaim = "not_applicable"
            },
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = 20,
                SuccessfulAttemptedTaskCount = 20,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0
            },
            ReplacementScope = new RuntimeTokenPhase3MainPathReplacementScope
            {
                RequestKind = "worker",
                Surface = "worker:system:$.instructions",
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkMode = "not_applicable"
            },
            Controls = new RuntimeTokenPhase3MainPathReplacementControls
            {
                GlobalKillSwitchRetained = true,
                PerRequestKindFallbackRetained = true,
                PerSurfaceFallbackRetained = true,
                CandidateVersionPinned = true,
                PostRolloutAuditRequired = true,
                DefaultEnabledToday = false,
                FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion
            }
        };
    }

    private static RuntimeTokenPhase3ReplacementScopeFreezeResult CreateScopeFreeze(DateOnly resultDate)
    {
        return new RuntimeTokenPhase3ReplacementScopeFreezeResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-3-replacement-scope-freeze-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-3/replacement-scope-freeze-2026-04-21.json",
            MainPathReplacementReviewMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-3-main-path-replacement-review-2026-04-21.md",
            MainPathReplacementReviewJsonArtifactPath = ".ai/runtime/token-optimization/phase-3/main-path-replacement-review-2026-04-21.json",
            TargetSurface = "worker:system:$.instructions",
            RequestKind = "worker",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ExecutionTruthScope = new RuntimeTokenPhase2ExecutionTruthScope
            {
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkExecutionRequired = false,
                ProviderModelBehaviorClaim = "not_claimed",
                BehavioralNonInferiorityScope = "current_runtime_mode_only",
                ProviderBilledCostClaim = "not_applicable"
            },
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = 20,
                SuccessfulAttemptedTaskCount = 20,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0
            },
            ReplacementScope = new RuntimeTokenPhase3MainPathReplacementScope
            {
                RequestKind = "worker",
                Surface = "worker:system:$.instructions",
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkMode = "not_applicable"
            },
            Controls = new RuntimeTokenPhase3MainPathReplacementControls
            {
                GlobalKillSwitchRetained = true,
                PerRequestKindFallbackRetained = true,
                PerSurfaceFallbackRetained = true,
                CandidateVersionPinned = true,
                PostRolloutAuditRequired = true,
                DefaultEnabledToday = false,
                FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion
            },
            FreezeVerdict = "limited_scope_frozen",
            ImplementationScopeFrozen = true,
            LimitedMainPathImplementationAllowed = true,
            ScopeExpansionAllowed = false,
            MainRendererReplacementAllowed = false,
            RuntimeShadowExecutionAllowed = false,
            FullRolloutAllowed = false
        };
    }

    private static RuntimeTokenPhase3PostRolloutEvidenceResult CreatePostRolloutEvidence(DateOnly resultDate)
    {
        return new RuntimeTokenPhase3PostRolloutEvidenceResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-3-post-rollout-evidence-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-3/post-rollout-evidence-2026-04-21.json",
            MainPathReplacementReviewMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-3-main-path-replacement-review-2026-04-21.md",
            MainPathReplacementReviewJsonArtifactPath = ".ai/runtime/token-optimization/phase-3/main-path-replacement-review-2026-04-21.json",
            ReplacementScopeFreezeMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-3-replacement-scope-freeze-2026-04-21.md",
            ReplacementScopeFreezeJsonArtifactPath = ".ai/runtime/token-optimization/phase-3/replacement-scope-freeze-2026-04-21.json",
            WorkerRecollectMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-worker-recollect-result-2026-04-21.md",
            WorkerRecollectJsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/worker-recollect-result-2026-04-21.json",
            TargetSurface = "worker:system:$.instructions",
            RequestKind = "worker",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ObservationMode = "limited_main_path_default_replay_with_null_worker_attempted_task_truth",
            EvidenceStatus = "observed_for_frozen_scope",
            ExecutionTruthScope = new RuntimeTokenPhase2ExecutionTruthScope
            {
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkExecutionRequired = false,
                ProviderModelBehaviorClaim = "not_claimed",
                BehavioralNonInferiorityScope = "current_runtime_mode_only",
                ProviderBilledCostClaim = "not_applicable"
            },
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = 20,
                SuccessfulAttemptedTaskCount = 20,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0
            },
            RolloutScope = new RuntimeTokenPhase3PostRolloutScope
            {
                RequestKind = "worker",
                Surface = "worker:system:$.instructions",
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                DefaultEnabled = true,
                FullRollout = false,
                AllowlistMode = "frozen_scope"
            },
            BehaviorEvidence = new RuntimeTokenPhase3PostRolloutBehaviorEvidence
            {
                AttemptedTaskCount = 20,
                SuccessfulAttemptedTaskCount = 20,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0,
                TaskSuccessRateDeltaPercentagePoints = 0,
                ReviewAdmissionRateDeltaPercentagePoints = 0,
                ConstraintViolationRateDeltaPercentagePoints = 0,
                RetryCountPerTaskRelativeDelta = 0,
                RepairCountPerTaskRelativeDelta = 0,
                Observed = true,
                UnavailableMetrics = []
            },
            Safety = new RuntimeTokenPhase3PostRolloutSafetyEvidence
            {
                HardFailCount = 0,
                RollbackTriggered = false,
                KillSwitchUsed = false,
                HardFailConditionsTriggered = []
            },
            LimitedMainPathImplementationObserved = true,
            PostRolloutBehaviorEvidenceObserved = true,
            PostRolloutTokenEvidenceObserved = true,
            BlockingReasons = []
        };
    }
}
