using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase2ActiveCanaryResultReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase2ActiveCanaryResultReviewService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase2ActiveCanaryResultReviewResult Persist(
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult executionApprovalResult,
        RuntimeTokenPhase2ActiveCanaryResult canaryResult)
    {
        return Persist(paths, executionApprovalResult, canaryResult, canaryResult.ResultDate);
    }

    internal static RuntimeTokenPhase2ActiveCanaryResultReviewResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult executionApprovalResult,
        RuntimeTokenPhase2ActiveCanaryResult canaryResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(executionApprovalResult, canaryResult, resultDate);

        var blockingReasons = new List<string>();
        if (!executionApprovalResult.ActiveCanaryApproved || !executionApprovalResult.CanaryExecutionAuthorized)
        {
            blockingReasons.Add("active_canary_execution_not_authorized");
        }

        if (!canaryResult.AttemptedTaskCohort.CoversFrozenReplayTaskSet || canaryResult.AttemptedTaskCohort.AttemptedTaskCount == 0)
        {
            blockingReasons.Add("attempted_task_cohort_not_proven");
        }

        blockingReasons.AddRange(canaryResult.BlockingReasons);

        foreach (var evaluation in canaryResult.NonInferiority.ThresholdEvaluations)
        {
            if (evaluation.Evaluated && !evaluation.Passed)
            {
                blockingReasons.Add($"threshold_failed:{evaluation.MetricId}");
            }
        }

        if (canaryResult.Safety.HardFailCount > 0)
        {
            blockingReasons.Add("hard_fail_count_gt_0");
        }

        if (canaryResult.Safety.RollbackTriggered)
        {
            blockingReasons.Add("rollback_triggered");
        }

        var costSavingObserved = canaryResult.TokenMetrics.DeltaTotalTokensPerSuccessfulTask < 0d;
        if (!costSavingObserved)
        {
            blockingReasons.Add("total_tokens_per_successful_task_not_reduced");
        }

        var reviewVerdict = ResolveReviewVerdict(executionApprovalResult, canaryResult, costSavingObserved);
        var costSavingProven = reviewVerdict == "pass";
        var mainPathReplacementReviewEligible = reviewVerdict == "pass";
        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2ActiveCanaryResultReviewResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = canaryResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            ExecutionApprovalMarkdownArtifactPath = executionApprovalResult.MarkdownArtifactPath,
            ExecutionApprovalJsonArtifactPath = executionApprovalResult.JsonArtifactPath,
            CanaryResultMarkdownArtifactPath = canaryResult.MarkdownArtifactPath,
            CanaryResultJsonArtifactPath = canaryResult.JsonArtifactPath,
            TargetSurface = canaryResult.TargetSurface,
            CandidateStrategy = canaryResult.CandidateStrategy,
            CandidateVersion = canaryResult.CandidateVersion,
            FallbackVersion = canaryResult.FallbackVersion,
            ApprovalScope = executionApprovalResult.ApprovalScope,
            CanaryScope = canaryResult.CanaryScope,
            ExecutionTruthScope = canaryResult.ExecutionTruthScope,
            ObservationMode = canaryResult.ObservationMode,
            AttemptedTaskCohort = canaryResult.AttemptedTaskCohort,
            ReviewVerdict = reviewVerdict,
            CanaryResultDecision = canaryResult.Decision,
            CanaryExecutionAuthorized = executionApprovalResult.CanaryExecutionAuthorized,
            CostSavingObserved = costSavingObserved,
            CostSavingProven = costSavingProven,
            NonInferiorityPassed = canaryResult.NonInferiority.Passed,
            MainPathReplacementReviewEligible = mainPathReplacementReviewEligible,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            FullRolloutAllowed = false,
            TokenMetrics = canaryResult.TokenMetrics,
            NonInferiority = canaryResult.NonInferiority,
            Safety = canaryResult.Safety,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            NextRequiredActions = BuildNextRequiredActions(reviewVerdict),
            Notes = BuildNotes(canaryResult, reviewVerdict, costSavingObserved),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string FormatMarkdown(RuntimeTokenPhase2ActiveCanaryResultReviewResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Active Canary Result Review");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Review verdict: `{result.ReviewVerdict}`");
        builder.AppendLine($"- Canary result decision: `{result.CanaryResultDecision}`");
        builder.AppendLine($"- Canary execution authorized: `{(result.CanaryExecutionAuthorized ? "yes" : "no")}`");
        builder.AppendLine($"- Cost saving observed: `{(result.CostSavingObserved ? "yes" : "no")}`");
        builder.AppendLine($"- Cost saving proven: `{(result.CostSavingProven ? "yes" : "no")}`");
        builder.AppendLine($"- Non-inferiority passed: `{(result.NonInferiorityPassed ? "yes" : "no")}`");
        builder.AppendLine($"- Main-path replacement review eligible: `{(result.MainPathReplacementReviewEligible ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(result.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Main renderer replacement allowed: `{(result.MainRendererReplacementAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Full rollout allowed: `{(result.FullRolloutAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Candidate version: `{result.CandidateVersion}`");
        builder.AppendLine($"- Fallback version: `{result.FallbackVersion}`");
        builder.AppendLine($"- Approval scope: `{result.ApprovalScope}`");
        builder.AppendLine($"- Observation mode: `{result.ObservationMode}`");
        builder.AppendLine();
        builder.AppendLine("## Canary Scope");
        builder.AppendLine();
        builder.AppendLine($"- Request kinds: `{string.Join(", ", result.CanaryScope.RequestKinds)}`");
        builder.AppendLine($"- Surface allowlist: `{string.Join(", ", result.CanaryScope.SurfaceAllowlist)}`");
        builder.AppendLine($"- Default enabled: `{(result.CanaryScope.DefaultEnabled ? "yes" : "no")}`");
        builder.AppendLine($"- Allowlist mode: `{result.CanaryScope.AllowlistMode}`");
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
        builder.AppendLine("## Token Metrics");
        builder.AppendLine();
        builder.AppendLine($"- Observed whole-request reduction p95: `{result.TokenMetrics.ObservedWholeRequestReductionP95:0.000}`");
        builder.AppendLine($"- Baseline total tokens per successful task: `{result.TokenMetrics.BaselineTotalTokensPerSuccessfulTask:0.0}`");
        builder.AppendLine($"- Candidate total tokens per successful task: `{result.TokenMetrics.CandidateTotalTokensPerSuccessfulTask:0.0}`");
        builder.AppendLine($"- Delta total tokens per successful task: `{result.TokenMetrics.DeltaTotalTokensPerSuccessfulTask:0.0}`");
        builder.AppendLine($"- Relative change total tokens per successful task: `{result.TokenMetrics.RelativeChangeTotalTokensPerSuccessfulTask:0.000}`");
        builder.AppendLine($"- Baseline context-window input tokens p95: `{result.TokenMetrics.BaselineContextWindowInputTokensP95:0.0}`");
        builder.AppendLine($"- Candidate context-window input tokens p95: `{result.TokenMetrics.CandidateContextWindowInputTokensP95:0.0}`");
        builder.AppendLine($"- Delta context-window input tokens p95: `{result.TokenMetrics.DeltaContextWindowInputTokensP95:0.0}`");
        builder.AppendLine();
        builder.AppendLine("## Safety And Non-Inferiority");
        builder.AppendLine();
        builder.AppendLine($"- Hard fail count: `{result.Safety.HardFailCount}`");
        builder.AppendLine($"- Rollback triggered: `{(result.Safety.RollbackTriggered ? "yes" : "no")}`");
        builder.AppendLine($"- Manual review required: `{(result.Safety.ManualReviewRequired ? "yes" : "no")}`");
        builder.AppendLine($"- Sample size sufficient: `{(result.NonInferiority.SampleSizeSufficient ? "yes" : "no")}`");
        builder.AppendLine($"- Unavailable metrics: `{string.Join(", ", result.NonInferiority.UnavailableMetrics)}`");
        builder.AppendLine();
        builder.AppendLine("## Referenced Artifacts");
        builder.AppendLine();
        builder.AppendLine($"- Execution approval markdown: `{result.ExecutionApprovalMarkdownArtifactPath}`");
        builder.AppendLine($"- Execution approval json: `{result.ExecutionApprovalJsonArtifactPath}`");
        builder.AppendLine($"- Canary result markdown: `{result.CanaryResultMarkdownArtifactPath}`");
        builder.AppendLine($"- Canary result json: `{result.CanaryResultJsonArtifactPath}`");

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

    private static string ResolveReviewVerdict(
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult executionApprovalResult,
        RuntimeTokenPhase2ActiveCanaryResult canaryResult,
        bool costSavingObserved)
    {
        if (!executionApprovalResult.ActiveCanaryApproved || !executionApprovalResult.CanaryExecutionAuthorized)
        {
            return "inconclusive";
        }

        var evaluatedThresholdFailure = canaryResult.NonInferiority.ThresholdEvaluations.Any(item => item.Evaluated && !item.Passed);
        if (canaryResult.Safety.HardFailCount > 0
            || canaryResult.Safety.RollbackTriggered
            || evaluatedThresholdFailure
            || string.Equals(canaryResult.Decision, "fail", StringComparison.Ordinal))
        {
            return "fail";
        }

        if (string.Equals(canaryResult.Decision, "pass", StringComparison.Ordinal)
            && canaryResult.NonInferiority.Passed
            && costSavingObserved
            && !canaryResult.Safety.ManualReviewRequired
            && canaryResult.AttemptedTaskCohort.CoversFrozenReplayTaskSet
            && canaryResult.AttemptedTaskCohort.AttemptedTaskCount > 0)
        {
            return "pass";
        }

        return "inconclusive";
    }

    private static IReadOnlyList<string> BuildNextRequiredActions(string reviewVerdict)
    {
        return reviewVerdict switch
        {
            "pass" =>
            [
                "open a separate main-path replacement review; do not treat this review as replacement approval",
                "keep the canary scope worker-only and keep default-off outside the explicit allowlist until replacement review passes",
                "do not expand request kinds or surfaces without a new governed review line"
            ],
            "fail" =>
            [
                "treat the worker wrapper candidate as canary-failed and keep fallback pinned to original_worker_system_instructions",
                "investigate the failing metric or hard-fail condition before any new canary attempt",
                "do not expand canary scope, runtime shadow, or main-path changes from this result line"
            ],
            _ =>
            [
                "do not treat this result as proof that cost optimization has completed",
                "collect a larger canary line or execution-grade behavior evidence before any replacement review",
                "rerun active-canary-result and active-canary-result-review after behavior metrics or sample size gaps are resolved"
            ]
        };
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenPhase2ActiveCanaryResult canaryResult,
        string reviewVerdict,
        bool costSavingObserved)
    {
        var notes = new List<string>
        {
            "This review decides whether the controlled canary result is strong enough to enter a separate main-path replacement review. It does not approve replacement or full rollout.",
            "Runtime shadow execution remains blocked and this review does not authorize any additional request kind or surface.",
            "This review is scoped to no_provider_agent_mediated execution on the formal null_worker backend. It does not claim provider-backed model-behavior non-inferiority.",
            $"Observed whole-request p95 reduction was `{canaryResult.TokenMetrics.ObservedWholeRequestReductionP95:0.000}` against expected `{canaryResult.TokenMetrics.ExpectedWholeRequestReductionP95:0.000}`."
        };

        if (costSavingObserved)
        {
            notes.Add($"Successful-task cost moved from `{canaryResult.TokenMetrics.BaselineTotalTokensPerSuccessfulTask:0.0}` to `{canaryResult.TokenMetrics.CandidateTotalTokensPerSuccessfulTask:0.0}`.");
        }
        else
        {
            notes.Add("Successful-task cost did not improve, so the canary cannot be treated as a cost-saving pass.");
        }

        notes.Add($"Attempted-task cohort for this review is `{canaryResult.AttemptedTaskCohort.SelectionMode}` with attempted=`{canaryResult.AttemptedTaskCohort.AttemptedTaskCount}`, successful=`{canaryResult.AttemptedTaskCohort.SuccessfulAttemptedTaskCount}`, failed=`{canaryResult.AttemptedTaskCohort.FailedAttemptedTaskCount}`, incomplete=`{canaryResult.AttemptedTaskCohort.IncompleteAttemptedTaskCount}`.");

        notes.Add("Provider-billed cost is not applicable in this review line, and provider SDK/API samples are not required for the approved current-runtime-mode scope.");

        notes.Add(reviewVerdict switch
        {
            "pass" => "This line is strong enough to request a separate main-path replacement review, but replacement remains unapproved here.",
            "fail" => "This line failed canary review and must not be used to justify wider runtime rollout.",
            _ => "This line remains inconclusive because behavior-grade non-inferiority evidence is incomplete or still requires manual review."
        });

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult executionApprovalResult,
        RuntimeTokenPhase2ActiveCanaryResult canaryResult,
        DateOnly resultDate)
    {
        if (executionApprovalResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Active canary result review requires the execution approval artifact to share the same result date.");
        }

        if (canaryResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Active canary result review requires the canary result artifact to share the same result date.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-active-canary-result-review-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"active-canary-result-review-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string absolutePath)
    {
        return Path.GetRelativePath(repoRoot, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
