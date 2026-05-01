using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase3ReplacementScopeFreezeServiceTests
{
    [Fact]
    public void Persist_FreezesLimitedScopeWhenPhase3ReviewPassed()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);

        var result = RuntimeTokenPhase3ReplacementScopeFreezeService.Persist(
            workspace.Paths,
            CreateReview(resultDate),
            resultDate);

        Assert.Equal("limited_scope_frozen", result.FreezeVerdict);
        Assert.True(result.ImplementationScopeFrozen);
        Assert.True(result.LimitedMainPathImplementationAllowed);
        Assert.False(result.ScopeExpansionAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.FullRolloutAllowed);
        Assert.Empty(result.BlockingReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_BlocksFreezeWhenScopeExpandsBeyondWorker()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var review = CreateReview(resultDate) with
        {
            RequestKind = "planner",
            ReplacementScope = CreateReview(resultDate).ReplacementScope with
            {
                RequestKind = "planner"
            }
        };

        var result = RuntimeTokenPhase3ReplacementScopeFreezeService.Persist(
            workspace.Paths,
            review,
            resultDate);

        Assert.Equal("scope_freeze_blocked", result.FreezeVerdict);
        Assert.False(result.ImplementationScopeFrozen);
        Assert.Contains("request_kind_out_of_scope", result.BlockingReasons);
    }

    [Fact]
    public void Persist_BlocksFreezeWhenReviewDidNotApproveLimitedReplacement()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var review = CreateReview(resultDate) with
        {
            ReviewVerdict = "require_more_evidence",
            MainPathReplacementAllowed = false,
            BlockingReasons = ["post_canary_gate_not_open"]
        };

        var result = RuntimeTokenPhase3ReplacementScopeFreezeService.Persist(
            workspace.Paths,
            review,
            resultDate);

        Assert.Equal("scope_freeze_blocked", result.FreezeVerdict);
        Assert.Contains("main_path_replacement_review_not_approved", result.BlockingReasons);
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
            ApprovalScope = "limited_explicit_allowlist",
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
}
