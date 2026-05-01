using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase3PostRolloutAuditGateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase3PostRolloutAuditGateService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase3PostRolloutAuditGateResult Persist(
        RuntimeTokenPhase3MainPathReplacementReviewResult reviewResult,
        RuntimeTokenPhase3ReplacementScopeFreezeResult scopeFreezeResult,
        RuntimeTokenPhase3PostRolloutEvidenceResult evidenceResult)
    {
        return Persist(paths, reviewResult, scopeFreezeResult, evidenceResult, reviewResult.ResultDate);
    }

    internal static RuntimeTokenPhase3PostRolloutAuditGateResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenPhase3MainPathReplacementReviewResult reviewResult,
        RuntimeTokenPhase3ReplacementScopeFreezeResult scopeFreezeResult,
        RuntimeTokenPhase3PostRolloutEvidenceResult evidenceResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        if (reviewResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Post-rollout audit gate requires the main-path replacement review artifact to share the same result date.");
        }

        if (scopeFreezeResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Post-rollout audit gate requires the replacement scope freeze artifact to share the same result date.");
        }

        if (evidenceResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Post-rollout audit gate requires the post-rollout evidence artifact to share the same result date.");
        }

        var blockingReasons = new List<string>();
        if (!reviewResult.MainPathReplacementAllowed || !string.Equals(reviewResult.ReviewVerdict, "approve_limited_main_path_replacement", StringComparison.Ordinal))
        {
            blockingReasons.Add("main_path_replacement_review_not_approved");
        }

        if (!scopeFreezeResult.ImplementationScopeFrozen || !scopeFreezeResult.LimitedMainPathImplementationAllowed)
        {
            blockingReasons.Add("replacement_scope_not_frozen");
        }

        if (!evidenceResult.LimitedMainPathImplementationObserved)
        {
            blockingReasons.Add("limited_main_path_default_not_enabled");
        }

        if (!evidenceResult.PostRolloutBehaviorEvidenceObserved)
        {
            blockingReasons.Add("post_rollout_behavior_evidence_not_observed");
        }

        if (!evidenceResult.PostRolloutTokenEvidenceObserved)
        {
            blockingReasons.Add("post_rollout_token_evidence_not_observed");
        }

        var gateVerdict = blockingReasons.Count == 0
            ? "post_rollout_audit_passed"
            : "blocked_pending_post_rollout_evidence";
        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase3PostRolloutAuditGateResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = reviewResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            MainPathReplacementReviewMarkdownArtifactPath = reviewResult.MarkdownArtifactPath,
            MainPathReplacementReviewJsonArtifactPath = reviewResult.JsonArtifactPath,
            ReplacementScopeFreezeMarkdownArtifactPath = scopeFreezeResult.MarkdownArtifactPath,
            ReplacementScopeFreezeJsonArtifactPath = scopeFreezeResult.JsonArtifactPath,
            PostRolloutEvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            PostRolloutEvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            TargetSurface = reviewResult.TargetSurface,
            RequestKind = reviewResult.RequestKind,
            CandidateVersion = reviewResult.CandidateVersion,
            FallbackVersion = reviewResult.FallbackVersion,
            ExecutionTruthScope = evidenceResult.ExecutionTruthScope,
            AttemptedTaskCohort = evidenceResult.AttemptedTaskCohort,
            ReplacementScope = reviewResult.ReplacementScope,
            Controls = scopeFreezeResult.Controls,
            GateVerdict = gateVerdict,
            PostRolloutAuditPassed = blockingReasons.Count == 0,
            LimitedMainPathImplementationObserved = evidenceResult.LimitedMainPathImplementationObserved,
            MainPathReplacementRetained = blockingReasons.Count == 0,
            RequestKindExpansionAllowed = false,
            SurfaceExpansionAllowed = false,
            MainRendererReplacementAllowed = false,
            RuntimeShadowExecutionAllowed = false,
            FullRolloutAllowed = false,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            NextRequiredActions = BuildNextActions(blockingReasons.Count == 0),
            Notes = BuildNotes(reviewResult, scopeFreezeResult, evidenceResult, blockingReasons.Count == 0)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string FormatMarkdown(RuntimeTokenPhase3PostRolloutAuditGateResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 3 Post-Rollout Audit Gate");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Gate verdict: `{result.GateVerdict}`");
        builder.AppendLine($"- Post-rollout audit passed: `{(result.PostRolloutAuditPassed ? "yes" : "no")}`");
        builder.AppendLine($"- Limited main-path implementation observed: `{(result.LimitedMainPathImplementationObserved ? "yes" : "no")}`");
        builder.AppendLine($"- Main-path replacement retained: `{(result.MainPathReplacementRetained ? "yes" : "no")}`");
        builder.AppendLine($"- Request-kind expansion allowed: `{(result.RequestKindExpansionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Surface expansion allowed: `{(result.SurfaceExpansionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Main renderer replacement allowed: `{(result.MainRendererReplacementAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(result.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Full rollout allowed: `{(result.FullRolloutAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Request kind: `{result.RequestKind}`");
        builder.AppendLine($"- Candidate version: `{result.CandidateVersion}`");
        builder.AppendLine($"- Fallback version: `{result.FallbackVersion}`");
        builder.AppendLine();
        builder.AppendLine("## Execution Truth Scope");
        builder.AppendLine();
        builder.AppendLine($"- Execution mode: `{result.ExecutionTruthScope.ExecutionMode}`");
        builder.AppendLine($"- Worker backend: `{result.ExecutionTruthScope.WorkerBackend}`");
        builder.AppendLine($"- Provider model behavior claim: `{result.ExecutionTruthScope.ProviderModelBehaviorClaim}`");
        builder.AppendLine($"- Behavioral non-inferiority scope: `{result.ExecutionTruthScope.BehavioralNonInferiorityScope}`");
        builder.AppendLine($"- Provider billed cost claim: `{result.ExecutionTruthScope.ProviderBilledCostClaim}`");
        builder.AppendLine();
        builder.AppendLine("## Frozen Scope");
        builder.AppendLine();
        builder.AppendLine($"- Request kind: `{result.ReplacementScope.RequestKind}`");
        builder.AppendLine($"- Surface: `{result.ReplacementScope.Surface}`");
        builder.AppendLine($"- Execution mode: `{result.ReplacementScope.ExecutionMode}`");
        builder.AppendLine($"- Worker backend: `{result.ReplacementScope.WorkerBackend}`");
        builder.AppendLine($"- Provider SDK mode: `{result.ReplacementScope.ProviderSdkMode}`");
        builder.AppendLine();
        builder.AppendLine("## Controls");
        builder.AppendLine();
        builder.AppendLine($"- Global kill switch retained: `{(result.Controls.GlobalKillSwitchRetained ? "yes" : "no")}`");
        builder.AppendLine($"- Per-request-kind fallback retained: `{(result.Controls.PerRequestKindFallbackRetained ? "yes" : "no")}`");
        builder.AppendLine($"- Per-surface fallback retained: `{(result.Controls.PerSurfaceFallbackRetained ? "yes" : "no")}`");
        builder.AppendLine($"- Candidate version pinned: `{(result.Controls.CandidateVersionPinned ? "yes" : "no")}`");
        builder.AppendLine($"- Default enabled today: `{(result.Controls.DefaultEnabledToday ? "yes" : "no")}`");
        builder.AppendLine($"- Post-rollout audit required: `{(result.Controls.PostRolloutAuditRequired ? "yes" : "no")}`");
        builder.AppendLine();
        builder.AppendLine("## Referenced Artifacts");
        builder.AppendLine();
        builder.AppendLine($"- Main-path replacement review markdown: `{result.MainPathReplacementReviewMarkdownArtifactPath}`");
        builder.AppendLine($"- Main-path replacement review json: `{result.MainPathReplacementReviewJsonArtifactPath}`");
        builder.AppendLine($"- Replacement scope freeze markdown: `{result.ReplacementScopeFreezeMarkdownArtifactPath}`");
        builder.AppendLine($"- Replacement scope freeze json: `{result.ReplacementScopeFreezeJsonArtifactPath}`");
        builder.AppendLine($"- Post-rollout evidence markdown: `{result.PostRolloutEvidenceMarkdownArtifactPath}`");
        builder.AppendLine($"- Post-rollout evidence json: `{result.PostRolloutEvidenceJsonArtifactPath}`");
        builder.AppendLine();
        builder.AppendLine("## Attempted Task Cohort");
        builder.AppendLine();
        builder.AppendLine($"- Selection mode: `{result.AttemptedTaskCohort.SelectionMode}`");
        builder.AppendLine($"- Attempted task count: `{result.AttemptedTaskCohort.AttemptedTaskCount}`");
        builder.AppendLine($"- Successful attempted task count: `{result.AttemptedTaskCohort.SuccessfulAttemptedTaskCount}`");
        builder.AppendLine($"- Failed attempted task count: `{result.AttemptedTaskCohort.FailedAttemptedTaskCount}`");
        builder.AppendLine($"- Incomplete attempted task count: `{result.AttemptedTaskCohort.IncompleteAttemptedTaskCount}`");

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

    private static IReadOnlyList<string> BuildNextActions(bool passed)
    {
        if (passed)
        {
            return
            [
                "retain kill switch, fallback, and candidate pin during the post-rollout audit window",
                "do not expand request kinds, surfaces, runtime modes, or rollout scope from this gate",
                "treat this result as audit pass for limited main-path retention only, not full rollout approval"
            ];
        }

        return
        [
            "implement the frozen limited main-path default before rerunning this audit gate",
            "collect post-rollout token and behavior evidence on the retained worker-only scope",
            "rerun post-rollout-audit-gate after the limited main-path line is live and audited",
            "do not expand request kinds, surfaces, runtime modes, or rollout scope from this gate"
        ];
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenPhase3MainPathReplacementReviewResult reviewResult,
        RuntimeTokenPhase3ReplacementScopeFreezeResult scopeFreezeResult,
        RuntimeTokenPhase3PostRolloutEvidenceResult evidenceResult,
        bool passed)
    {
        var notes = new List<string>
        {
            "This gate audits post-rollout evidence only. It does not substitute for rollout implementation.",
            "Current scope remains limited to worker:system:$.instructions under no_provider_agent_mediated runtime mode on the formal null_worker backend.",
            $"Replacement review verdict is `{reviewResult.ReviewVerdict}` and scope freeze verdict is `{scopeFreezeResult.FreezeVerdict}`.",
            $"Post-rollout evidence status is `{evidenceResult.EvidenceStatus}` with current-runtime-only execution truth."
        };

        notes.Add(passed
            ? "Because the limited main-path default is observed on the full frozen task set and post-rollout token/behavior evidence exists, this gate passes for limited main-path retention."
            : "Because the limited main-path default is not yet observed or post-rollout evidence remains incomplete, this gate remains blocked.");

        return notes;
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-3-post-rollout-audit-gate-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-3",
            $"post-rollout-audit-gate-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string absolutePath)
    {
        return Path.GetRelativePath(repoRoot, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
