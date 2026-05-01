using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase2PostCanaryGateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase2PostCanaryGateService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase2PostCanaryGateResult Persist(
        RuntimeTokenPhase2ActiveCanaryResultReviewResult canaryResultReviewResult)
    {
        return Persist(paths, canaryResultReviewResult, canaryResultReviewResult.ResultDate);
    }

    internal static RuntimeTokenPhase2PostCanaryGateResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenPhase2ActiveCanaryResultReviewResult canaryResultReviewResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        if (canaryResultReviewResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Post-canary gate requires the canary result review artifact to share the same result date.");
        }

        var blockingReasons = new List<string>(canaryResultReviewResult.BlockingReasons);
        if (!canaryResultReviewResult.CostSavingProven)
        {
            blockingReasons.Add("cost_saving_not_proven");
        }

        if (!canaryResultReviewResult.NonInferiorityPassed)
        {
            blockingReasons.Add("non_inferiority_not_passed");
        }

        if (!canaryResultReviewResult.MainPathReplacementReviewEligible)
        {
            blockingReasons.Add("main_path_replacement_review_not_eligible");
        }

        string gateVerdict;
        if (string.Equals(canaryResultReviewResult.ReviewVerdict, "pass", StringComparison.Ordinal)
            && canaryResultReviewResult.CostSavingProven
            && canaryResultReviewResult.NonInferiorityPassed
            && canaryResultReviewResult.MainPathReplacementReviewEligible
            && canaryResultReviewResult.Safety.HardFailCount == 0
            && !canaryResultReviewResult.Safety.RollbackTriggered)
        {
            gateVerdict = "eligible_for_main_path_replacement_review";
        }
        else if (string.Equals(canaryResultReviewResult.ReviewVerdict, "fail", StringComparison.Ordinal))
        {
            gateVerdict = "blocked_after_canary_failure";
        }
        else
        {
            gateVerdict = "blocked_pending_post_canary_evidence";
        }

        var mainPathReplacementReviewAllowed = string.Equals(gateVerdict, "eligible_for_main_path_replacement_review", StringComparison.Ordinal);
        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2PostCanaryGateResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = canaryResultReviewResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            CanaryResultReviewMarkdownArtifactPath = canaryResultReviewResult.MarkdownArtifactPath,
            CanaryResultReviewJsonArtifactPath = canaryResultReviewResult.JsonArtifactPath,
            TargetSurface = canaryResultReviewResult.TargetSurface,
            CandidateStrategy = canaryResultReviewResult.CandidateStrategy,
            CandidateVersion = canaryResultReviewResult.CandidateVersion,
            FallbackVersion = canaryResultReviewResult.FallbackVersion,
            ApprovalScope = canaryResultReviewResult.ApprovalScope,
            CanaryScope = canaryResultReviewResult.CanaryScope,
            ExecutionTruthScope = canaryResultReviewResult.ExecutionTruthScope,
            AttemptedTaskCohort = canaryResultReviewResult.AttemptedTaskCohort,
            GateVerdict = gateVerdict,
            CanaryResultReviewVerdict = canaryResultReviewResult.ReviewVerdict,
            MainPathReplacementReviewAllowed = mainPathReplacementReviewAllowed,
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
            NextRequiredActions = BuildNextRequiredActions(gateVerdict),
            Notes = BuildNotes(canaryResultReviewResult, gateVerdict),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string FormatMarkdown(RuntimeTokenPhase2PostCanaryGateResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Post-Canary Gate");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Gate verdict: `{result.GateVerdict}`");
        builder.AppendLine($"- Canary result review verdict: `{result.CanaryResultReviewVerdict}`");
        builder.AppendLine($"- Main-path replacement review allowed: `{(result.MainPathReplacementReviewAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Main renderer replacement allowed: `{(result.MainRendererReplacementAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(result.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Full rollout allowed: `{(result.FullRolloutAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Cost saving proven: `{(result.CostSavingProven ? "yes" : "no")}`");
        builder.AppendLine($"- Non-inferiority passed: `{(result.NonInferiorityPassed ? "yes" : "no")}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Candidate version: `{result.CandidateVersion}`");
        builder.AppendLine($"- Fallback version: `{result.FallbackVersion}`");
        builder.AppendLine($"- Approval scope: `{result.ApprovalScope}`");
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
        builder.AppendLine("## Key Metrics");
        builder.AppendLine();
        builder.AppendLine($"- Observed whole-request reduction p95: `{result.TokenMetrics.ObservedWholeRequestReductionP95:0.000}`");
        builder.AppendLine($"- Expected whole-request reduction p95: `{result.TokenMetrics.ExpectedWholeRequestReductionP95:0.000}`");
        builder.AppendLine($"- Delta total tokens per successful task: `{result.TokenMetrics.DeltaTotalTokensPerSuccessfulTask:0.0}`");
        builder.AppendLine($"- Relative change total tokens per successful task: `{result.TokenMetrics.RelativeChangeTotalTokensPerSuccessfulTask:0.000}`");
        builder.AppendLine($"- Hard fail count: `{result.Safety.HardFailCount}`");
        builder.AppendLine($"- Rollback triggered: `{(result.Safety.RollbackTriggered ? "yes" : "no")}`");
        builder.AppendLine($"- Manual review required: `{(result.Safety.ManualReviewRequired ? "yes" : "no")}`");
        builder.AppendLine();
        builder.AppendLine("## Referenced Artifacts");
        builder.AppendLine();
        builder.AppendLine($"- Canary result review markdown: `{result.CanaryResultReviewMarkdownArtifactPath}`");
        builder.AppendLine($"- Canary result review json: `{result.CanaryResultReviewJsonArtifactPath}`");

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

    private static IReadOnlyList<string> BuildNextRequiredActions(string gateVerdict)
    {
        return gateVerdict switch
        {
            "eligible_for_main_path_replacement_review" =>
            [
                "open a separate main-path replacement review; do not treat this gate as replacement approval",
                "keep the candidate worker-only and default-off outside the explicit allowlist until replacement review passes",
                "do not expand request kinds or surfaces from this gate"
            ],
            "blocked_after_canary_failure" =>
            [
                "keep fallback pinned to original_worker_system_instructions and treat this candidate as canary-failed",
                "investigate the failing metric or hard-fail condition before any new canary attempt",
                "do not open main-path replacement review from this line"
            ],
            _ =>
            [
                "collect execution-grade behavior evidence or a larger sample before any replacement review",
                "rerun active-canary-result, active-canary-result-review, and post-canary-gate after evidence gaps are resolved",
                "do not open main-path replacement review from this line"
            ]
        };
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenPhase2ActiveCanaryResultReviewResult canaryResultReviewResult,
        string gateVerdict)
    {
        var notes = new List<string>
        {
            "This gate decides only whether the canary line is strong enough to enter a separate main-path replacement review. It does not approve replacement, runtime shadow, or full rollout.",
            "This gate is limited to no_provider_agent_mediated execution on the formal null_worker backend. It does not claim provider-backed model-behavior non-inferiority.",
            $"Observed whole-request p95 reduction remains `{canaryResultReviewResult.TokenMetrics.ObservedWholeRequestReductionP95:0.000}` with successful-task cost delta `{canaryResultReviewResult.TokenMetrics.DeltaTotalTokensPerSuccessfulTask:0.0}`."
        };
        notes.Add($"Attempted-task cohort for this gate is `{canaryResultReviewResult.AttemptedTaskCohort.SelectionMode}` with attempted=`{canaryResultReviewResult.AttemptedTaskCohort.AttemptedTaskCount}`, successful=`{canaryResultReviewResult.AttemptedTaskCohort.SuccessfulAttemptedTaskCount}`, failed=`{canaryResultReviewResult.AttemptedTaskCohort.FailedAttemptedTaskCount}`, incomplete=`{canaryResultReviewResult.AttemptedTaskCohort.IncompleteAttemptedTaskCount}`.");

        notes.Add("Provider-billed cost is not applicable in this gate line, and provider SDK/API samples are not required for current-runtime-mode-only post-canary evidence.");

        notes.Add(gateVerdict switch
        {
            "eligible_for_main_path_replacement_review" => "The canary line is strong enough to request a separate replacement review, but replacement remains unapproved here.",
            "blocked_after_canary_failure" => "The canary line failed and must not be used to justify broader runtime rollout.",
            _ => "The canary line remains blocked because behavior-grade proof or sample confidence is still incomplete."
        });

        return notes;
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-post-canary-gate-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"post-canary-gate-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string absolutePath)
    {
        return Path.GetRelativePath(repoRoot, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
