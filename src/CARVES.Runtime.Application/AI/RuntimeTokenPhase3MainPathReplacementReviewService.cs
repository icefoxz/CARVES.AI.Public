using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase3MainPathReplacementReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly RuntimeTokenWorkerWrapperCanaryService canaryService;

    public RuntimeTokenPhase3MainPathReplacementReviewService(
        ControlPlanePaths paths,
        RuntimeTokenWorkerWrapperCanaryService? canaryService = null)
    {
        this.paths = paths;
        this.canaryService = canaryService ?? new RuntimeTokenWorkerWrapperCanaryService();
    }

    public RuntimeTokenPhase3MainPathReplacementReviewResult Persist(
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult executionApprovalResult,
        RuntimeTokenPhase2ActiveCanaryResult canaryResult,
        RuntimeTokenPhase2ActiveCanaryResultReviewResult canaryResultReviewResult,
        RuntimeTokenPhase2PostCanaryGateResult postCanaryGateResult)
    {
        return Persist(
            paths,
            executionApprovalResult,
            canaryResult,
            canaryResultReviewResult,
            postCanaryGateResult,
            canaryService.DescribeMechanismContract(),
            postCanaryGateResult.ResultDate);
    }

    internal static RuntimeTokenPhase3MainPathReplacementReviewResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult executionApprovalResult,
        RuntimeTokenPhase2ActiveCanaryResult canaryResult,
        RuntimeTokenPhase2ActiveCanaryResultReviewResult canaryResultReviewResult,
        RuntimeTokenPhase2PostCanaryGateResult postCanaryGateResult,
        RuntimeTokenWorkerWrapperCanaryMechanismContract mechanismContract,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        EnsureSameResultDate(resultDate, executionApprovalResult.ResultDate, "execution approval");
        EnsureSameResultDate(resultDate, canaryResult.ResultDate, "active canary result");
        EnsureSameResultDate(resultDate, canaryResultReviewResult.ResultDate, "active canary result review");
        EnsureSameResultDate(resultDate, postCanaryGateResult.ResultDate, "post-canary gate");

        var blockingReasons = new List<string>();
        if (!postCanaryGateResult.MainPathReplacementReviewAllowed)
        {
            blockingReasons.Add("post_canary_gate_not_open");
        }

        if (!executionApprovalResult.ActiveCanaryApproved || !executionApprovalResult.CanaryExecutionAuthorized)
        {
            blockingReasons.Add("active_canary_execution_not_approved");
        }

        if (!string.Equals(canaryResult.Decision, "pass", StringComparison.Ordinal))
        {
            blockingReasons.Add("active_canary_result_not_pass");
        }

        if (!string.Equals(canaryResultReviewResult.ReviewVerdict, "pass", StringComparison.Ordinal))
        {
            blockingReasons.Add("active_canary_result_review_not_pass");
        }

        if (!canaryResultReviewResult.CostSavingProven)
        {
            blockingReasons.Add("cost_saving_not_proven");
        }

        if (!canaryResultReviewResult.NonInferiorityPassed)
        {
            blockingReasons.Add("non_inferiority_not_passed");
        }

        if (canaryResultReviewResult.Safety.HardFailCount > 0)
        {
            blockingReasons.Add("hard_fail_count_gt_0");
        }

        if (canaryResultReviewResult.Safety.RollbackTriggered)
        {
            blockingReasons.Add("rollback_triggered");
        }

        if (!string.Equals(postCanaryGateResult.ExecutionTruthScope.ExecutionMode, "no_provider_agent_mediated", StringComparison.Ordinal))
        {
            blockingReasons.Add("execution_mode_out_of_review_scope");
        }

        if (!string.Equals(postCanaryGateResult.ExecutionTruthScope.WorkerBackend, "null_worker", StringComparison.Ordinal))
        {
            blockingReasons.Add("worker_backend_out_of_review_scope");
        }

        if (!string.Equals(postCanaryGateResult.ExecutionTruthScope.ProviderModelBehaviorClaim, "not_claimed", StringComparison.Ordinal))
        {
            blockingReasons.Add("provider_model_behavior_claim_not_allowed");
        }

        if (!string.Equals(postCanaryGateResult.ExecutionTruthScope.ProviderBilledCostClaim, "not_applicable", StringComparison.Ordinal))
        {
            blockingReasons.Add("provider_billed_cost_claim_not_allowed");
        }

        if (executionApprovalResult.CanaryRequestKindAllowlist.Count != 1
            || !string.Equals(executionApprovalResult.CanaryRequestKindAllowlist[0], RuntimeTokenWorkerWrapperCanaryService.RequestKind, StringComparison.Ordinal))
        {
            blockingReasons.Add("request_kind_scope_not_worker_only");
        }

        if (!string.Equals(mechanismContract.TargetSurface, postCanaryGateResult.TargetSurface, StringComparison.Ordinal))
        {
            blockingReasons.Add("mechanism_target_surface_mismatch");
        }

        if (!string.Equals(mechanismContract.RequestKind, RuntimeTokenWorkerWrapperCanaryService.RequestKind, StringComparison.Ordinal))
        {
            blockingReasons.Add("mechanism_request_kind_mismatch");
        }

        if (!mechanismContract.GlobalKillSwitchSupported)
        {
            blockingReasons.Add("global_kill_switch_not_retained");
        }

        if (!mechanismContract.RequestKindAllowlistSupported)
        {
            blockingReasons.Add("request_kind_fallback_not_retained");
        }

        if (!mechanismContract.SurfaceAllowlistSupported)
        {
            blockingReasons.Add("surface_fallback_not_retained");
        }

        if (!mechanismContract.CandidateVersionPinSupported)
        {
            blockingReasons.Add("candidate_version_pin_not_retained");
        }

        if (!string.Equals(mechanismContract.CandidateVersion, executionApprovalResult.CandidateVersion, StringComparison.Ordinal))
        {
            blockingReasons.Add("mechanism_candidate_version_mismatch");
        }

        if (!string.Equals(mechanismContract.FallbackVersion, executionApprovalResult.FallbackVersion, StringComparison.Ordinal))
        {
            blockingReasons.Add("mechanism_fallback_version_mismatch");
        }

        string reviewVerdict;
        if (blockingReasons.Count == 0)
        {
            reviewVerdict = "approve_limited_main_path_replacement";
        }
        else if (string.Equals(canaryResultReviewResult.ReviewVerdict, "fail", StringComparison.Ordinal)
                 || string.Equals(postCanaryGateResult.GateVerdict, "blocked_after_canary_failure", StringComparison.Ordinal))
        {
            reviewVerdict = "reject";
        }
        else
        {
            reviewVerdict = "require_more_evidence";
        }

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase3MainPathReplacementReviewResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = postCanaryGateResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            EvidenceInputs =
            [
                canaryResult.MarkdownArtifactPath,
                canaryResultReviewResult.MarkdownArtifactPath,
                postCanaryGateResult.MarkdownArtifactPath
            ],
            ExecutionApprovalMarkdownArtifactPath = executionApprovalResult.MarkdownArtifactPath,
            ExecutionApprovalJsonArtifactPath = executionApprovalResult.JsonArtifactPath,
            CanaryResultMarkdownArtifactPath = canaryResult.MarkdownArtifactPath,
            CanaryResultJsonArtifactPath = canaryResult.JsonArtifactPath,
            CanaryResultReviewMarkdownArtifactPath = canaryResultReviewResult.MarkdownArtifactPath,
            CanaryResultReviewJsonArtifactPath = canaryResultReviewResult.JsonArtifactPath,
            PostCanaryGateMarkdownArtifactPath = postCanaryGateResult.MarkdownArtifactPath,
            PostCanaryGateJsonArtifactPath = postCanaryGateResult.JsonArtifactPath,
            TargetSurface = postCanaryGateResult.TargetSurface,
            RequestKind = RuntimeTokenWorkerWrapperCanaryService.RequestKind,
            CandidateStrategy = postCanaryGateResult.CandidateStrategy,
            CandidateVersion = postCanaryGateResult.CandidateVersion,
            FallbackVersion = postCanaryGateResult.FallbackVersion,
            ApprovalScope = executionApprovalResult.ApprovalScope,
            ExecutionTruthScope = postCanaryGateResult.ExecutionTruthScope,
            AttemptedTaskCohort = postCanaryGateResult.AttemptedTaskCohort,
            ReplacementScope = new RuntimeTokenPhase3MainPathReplacementScope
            {
                RequestKind = RuntimeTokenWorkerWrapperCanaryService.RequestKind,
                Surface = postCanaryGateResult.TargetSurface,
                ExecutionMode = postCanaryGateResult.ExecutionTruthScope.ExecutionMode,
                WorkerBackend = postCanaryGateResult.ExecutionTruthScope.WorkerBackend,
                ProviderSdkMode = postCanaryGateResult.ExecutionTruthScope.ProviderSdkExecutionRequired ? "required" : "not_applicable"
            },
            Controls = new RuntimeTokenPhase3MainPathReplacementControls
            {
                GlobalKillSwitchRetained = mechanismContract.GlobalKillSwitchSupported,
                PerRequestKindFallbackRetained = mechanismContract.RequestKindAllowlistSupported,
                PerSurfaceFallbackRetained = mechanismContract.SurfaceAllowlistSupported,
                CandidateVersionPinned = mechanismContract.CandidateVersionPinSupported,
                PostRolloutAuditRequired = true,
                DefaultEnabledToday = executionApprovalResult.DefaultEnabled,
                FallbackVersion = mechanismContract.FallbackVersion
            },
            ReviewVerdict = reviewVerdict,
            MainPathReplacementAllowed = string.Equals(reviewVerdict, "approve_limited_main_path_replacement", StringComparison.Ordinal),
            MainRendererReplacementAllowed = false,
            RuntimeShadowExecutionAllowed = false,
            FullRolloutAllowed = false,
            CostSavingProven = canaryResultReviewResult.CostSavingProven,
            NonInferiorityPassed = canaryResultReviewResult.NonInferiorityPassed,
            TokenMetrics = canaryResultReviewResult.TokenMetrics,
            NonInferiority = canaryResultReviewResult.NonInferiority,
            Safety = canaryResultReviewResult.Safety,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            NextRequiredActions = BuildNextRequiredActions(reviewVerdict),
            Notes = BuildNotes(postCanaryGateResult, canaryResultReviewResult, reviewVerdict)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string FormatMarkdown(RuntimeTokenPhase3MainPathReplacementReviewResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 3 Main Path Replacement Review");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Review verdict: `{result.ReviewVerdict}`");
        builder.AppendLine($"- Main-path replacement allowed: `{(result.MainPathReplacementAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Main renderer replacement allowed: `{(result.MainRendererReplacementAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(result.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Full rollout allowed: `{(result.FullRolloutAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Request kind: `{result.RequestKind}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Candidate version: `{result.CandidateVersion}`");
        builder.AppendLine($"- Fallback version: `{result.FallbackVersion}`");
        builder.AppendLine($"- Approval scope: `{result.ApprovalScope}`");
        builder.AppendLine($"- Cost saving proven: `{(result.CostSavingProven ? "yes" : "no")}`");
        builder.AppendLine($"- Non-inferiority passed: `{(result.NonInferiorityPassed ? "yes" : "no")}`");
        builder.AppendLine();
        builder.AppendLine("## Replacement Scope");
        builder.AppendLine();
        builder.AppendLine($"- Request kind: `{result.ReplacementScope.RequestKind}`");
        builder.AppendLine($"- Surface: `{result.ReplacementScope.Surface}`");
        builder.AppendLine($"- Execution mode: `{result.ReplacementScope.ExecutionMode}`");
        builder.AppendLine($"- Worker backend: `{result.ReplacementScope.WorkerBackend}`");
        builder.AppendLine($"- Provider SDK mode: `{result.ReplacementScope.ProviderSdkMode}`");
        builder.AppendLine();
        builder.AppendLine("## Execution Truth Scope");
        builder.AppendLine();
        builder.AppendLine($"- Execution mode: `{result.ExecutionTruthScope.ExecutionMode}`");
        builder.AppendLine($"- Worker backend: `{result.ExecutionTruthScope.WorkerBackend}`");
        builder.AppendLine($"- Provider SDK execution required: `{(result.ExecutionTruthScope.ProviderSdkExecutionRequired ? "yes" : "no")}`");
        builder.AppendLine($"- Provider model behavior claim: `{result.ExecutionTruthScope.ProviderModelBehaviorClaim}`");
        builder.AppendLine($"- Behavioral non-inferiority scope: `{result.ExecutionTruthScope.BehavioralNonInferiorityScope}`");
        builder.AppendLine($"- Provider billed cost claim: `{result.ExecutionTruthScope.ProviderBilledCostClaim}`");
        builder.AppendLine();
        builder.AppendLine("## Attempted Task Cohort");
        builder.AppendLine();
        builder.AppendLine($"- Selection mode: `{result.AttemptedTaskCohort.SelectionMode}`");
        builder.AppendLine($"- Attempted task count: `{result.AttemptedTaskCohort.AttemptedTaskCount}`");
        builder.AppendLine($"- Successful attempted task count: `{result.AttemptedTaskCohort.SuccessfulAttemptedTaskCount}`");
        builder.AppendLine($"- Failed attempted task count: `{result.AttemptedTaskCohort.FailedAttemptedTaskCount}`");
        builder.AppendLine($"- Incomplete attempted task count: `{result.AttemptedTaskCohort.IncompleteAttemptedTaskCount}`");
        builder.AppendLine($"- Covers frozen replay task set: `{(result.AttemptedTaskCohort.CoversFrozenReplayTaskSet ? "yes" : "no")}`");
        builder.AppendLine();
        builder.AppendLine("## Controls");
        builder.AppendLine();
        builder.AppendLine($"- Global kill switch retained: `{(result.Controls.GlobalKillSwitchRetained ? "yes" : "no")}`");
        builder.AppendLine($"- Per-request-kind fallback retained: `{(result.Controls.PerRequestKindFallbackRetained ? "yes" : "no")}`");
        builder.AppendLine($"- Per-surface fallback retained: `{(result.Controls.PerSurfaceFallbackRetained ? "yes" : "no")}`");
        builder.AppendLine($"- Candidate version pinned: `{(result.Controls.CandidateVersionPinned ? "yes" : "no")}`");
        builder.AppendLine($"- Fallback version: `{result.Controls.FallbackVersion}`");
        builder.AppendLine($"- Default enabled today: `{(result.Controls.DefaultEnabledToday ? "yes" : "no")}`");
        builder.AppendLine($"- Post-rollout audit required: `{(result.Controls.PostRolloutAuditRequired ? "yes" : "no")}`");
        builder.AppendLine();
        builder.AppendLine("## Key Metrics");
        builder.AppendLine();
        builder.AppendLine($"- Observed whole-request reduction p95: `{result.TokenMetrics.ObservedWholeRequestReductionP95:0.000}`");
        builder.AppendLine($"- Expected whole-request reduction p95: `{result.TokenMetrics.ExpectedWholeRequestReductionP95:0.000}`");
        builder.AppendLine($"- Delta total tokens per successful task: `{result.TokenMetrics.DeltaTotalTokensPerSuccessfulTask:0.0}`");
        builder.AppendLine($"- Relative change total tokens per successful task: `{result.TokenMetrics.RelativeChangeTotalTokensPerSuccessfulTask:0.000}`");
        builder.AppendLine($"- Hard fail count: `{result.Safety.HardFailCount}`");
        builder.AppendLine($"- Rollback triggered: `{(result.Safety.RollbackTriggered ? "yes" : "no")}`");
        builder.AppendLine();
        builder.AppendLine("## Evidence Inputs");
        builder.AppendLine();
        foreach (var input in result.EvidenceInputs)
        {
            builder.AppendLine($"- `{input}`");
        }

        if (result.BlockingReasons.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Blocking Reasons");
            builder.AppendLine();
            foreach (var reason in result.BlockingReasons)
            {
                builder.AppendLine($"- `{reason}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Next Required Actions");
        builder.AppendLine();
        foreach (var action in result.NextRequiredActions)
        {
            builder.AppendLine($"- {action}");
        }

        builder.AppendLine();
        builder.AppendLine("## Notes");
        builder.AppendLine();
        foreach (var note in result.Notes)
        {
            builder.AppendLine($"- {note}");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildNextRequiredActions(string reviewVerdict)
    {
        return reviewVerdict switch
        {
            "approve_limited_main_path_replacement" =>
            [
                "open a separate limited main-path replacement implementation line; do not treat this review as main renderer replacement approval",
                "retain global kill switch, per-request-kind fallback, per-surface fallback, and candidate version pin through rollout",
                "keep scope limited to worker:system:$.instructions under no_provider_agent_mediated runtime mode only",
                "require a post-rollout audit before any expansion or full rollout"
            ],
            "reject" =>
            [
                "keep the original worker instructions on the main path and close this replacement line",
                "treat the current candidate as failed for replacement purposes until a new candidate line is reviewed"
            ],
            _ =>
            [
                "collect the missing replacement evidence and rerun main-path-replacement-review",
                "do not enable any main-path replacement from this review line"
            ]
        };
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenPhase2PostCanaryGateResult postCanaryGateResult,
        RuntimeTokenPhase2ActiveCanaryResultReviewResult canaryResultReviewResult,
        string reviewVerdict)
    {
        var notes = new List<string>
        {
            "This review is limited to current no_provider_agent_mediated runtime mode on the formal null_worker backend.",
            "This review does not authorize runtime shadow, main renderer replacement, provider-backed behavior claims, or full rollout.",
            $"Attempted-task cohort remains `{postCanaryGateResult.AttemptedTaskCohort.SelectionMode}` with attempted=`{postCanaryGateResult.AttemptedTaskCohort.AttemptedTaskCount}`, successful=`{postCanaryGateResult.AttemptedTaskCohort.SuccessfulAttemptedTaskCount}`, failed=`{postCanaryGateResult.AttemptedTaskCohort.FailedAttemptedTaskCount}`, incomplete=`{postCanaryGateResult.AttemptedTaskCohort.IncompleteAttemptedTaskCount}`.",
            $"Observed whole-request p95 reduction is `{canaryResultReviewResult.TokenMetrics.ObservedWholeRequestReductionP95:0.000}` and successful-task token delta is `{canaryResultReviewResult.TokenMetrics.DeltaTotalTokensPerSuccessfulTask:0.0}` under current runtime mode."
        };

        notes.Add(reviewVerdict switch
        {
            "approve_limited_main_path_replacement" => "The current evidence is strong enough to request a controlled main-path default for worker:system:$.instructions, but only within the current runtime mode and with all fallback controls retained.",
            "reject" => "The current evidence is not acceptable for any main-path promotion and the candidate must stay off the main path.",
            _ => "The evidence is still not strong enough for main-path promotion, so the candidate must remain outside the main path."
        });

        return notes;
    }

    private static void EnsureSameResultDate(DateOnly expected, DateOnly actual, string artifactName)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException($"Phase 3 main-path replacement review requires {artifactName} to share result date '{expected:yyyy-MM-dd}'.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-3-main-path-replacement-review-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-3",
            $"main-path-replacement-review-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string absolutePath)
    {
        return Path.GetRelativePath(repoRoot, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
