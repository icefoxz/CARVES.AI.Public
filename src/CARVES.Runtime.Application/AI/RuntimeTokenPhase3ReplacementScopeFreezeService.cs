using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase3ReplacementScopeFreezeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase3ReplacementScopeFreezeService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase3ReplacementScopeFreezeResult Persist(
        RuntimeTokenPhase3MainPathReplacementReviewResult reviewResult)
    {
        return Persist(paths, reviewResult, reviewResult.ResultDate);
    }

    internal static RuntimeTokenPhase3ReplacementScopeFreezeResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenPhase3MainPathReplacementReviewResult reviewResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        if (reviewResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Replacement scope freeze requires the main-path replacement review artifact to share the same result date.");
        }

        var blockingReasons = new List<string>();
        if (!string.Equals(reviewResult.ReviewVerdict, "approve_limited_main_path_replacement", StringComparison.Ordinal)
            || !reviewResult.MainPathReplacementAllowed)
        {
            blockingReasons.Add("main_path_replacement_review_not_approved");
        }

        if (!string.Equals(reviewResult.RequestKind, RuntimeTokenWorkerWrapperCanaryService.RequestKind, StringComparison.Ordinal))
        {
            blockingReasons.Add("request_kind_out_of_scope");
        }

        if (!string.Equals(reviewResult.TargetSurface, RuntimeTokenWorkerWrapperCanaryService.TargetSurface, StringComparison.Ordinal))
        {
            blockingReasons.Add("target_surface_out_of_scope");
        }

        if (!string.Equals(reviewResult.ExecutionTruthScope.ExecutionMode, "no_provider_agent_mediated", StringComparison.Ordinal))
        {
            blockingReasons.Add("execution_mode_out_of_scope");
        }

        if (!string.Equals(reviewResult.ExecutionTruthScope.WorkerBackend, "null_worker", StringComparison.Ordinal))
        {
            blockingReasons.Add("worker_backend_out_of_scope");
        }

        if (!string.Equals(reviewResult.ExecutionTruthScope.ProviderModelBehaviorClaim, "not_claimed", StringComparison.Ordinal))
        {
            blockingReasons.Add("provider_model_behavior_claim_not_allowed");
        }

        if (!string.Equals(reviewResult.ExecutionTruthScope.ProviderBilledCostClaim, "not_applicable", StringComparison.Ordinal))
        {
            blockingReasons.Add("provider_billed_cost_claim_not_allowed");
        }

        if (!reviewResult.Controls.GlobalKillSwitchRetained)
        {
            blockingReasons.Add("global_kill_switch_not_retained");
        }

        if (!reviewResult.Controls.PerRequestKindFallbackRetained)
        {
            blockingReasons.Add("per_request_kind_fallback_not_retained");
        }

        if (!reviewResult.Controls.PerSurfaceFallbackRetained)
        {
            blockingReasons.Add("per_surface_fallback_not_retained");
        }

        if (!reviewResult.Controls.CandidateVersionPinned)
        {
            blockingReasons.Add("candidate_version_not_pinned");
        }

        if (!string.Equals(reviewResult.Controls.FallbackVersion, RuntimeTokenWorkerWrapperCanaryService.FallbackVersion, StringComparison.Ordinal))
        {
            blockingReasons.Add("fallback_version_not_original");
        }

        var freezeVerdict = blockingReasons.Count == 0 ? "limited_scope_frozen" : "scope_freeze_blocked";
        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase3ReplacementScopeFreezeResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = reviewResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            MainPathReplacementReviewMarkdownArtifactPath = reviewResult.MarkdownArtifactPath,
            MainPathReplacementReviewJsonArtifactPath = reviewResult.JsonArtifactPath,
            TargetSurface = reviewResult.TargetSurface,
            RequestKind = reviewResult.RequestKind,
            CandidateVersion = reviewResult.CandidateVersion,
            FallbackVersion = reviewResult.FallbackVersion,
            ApprovalScope = reviewResult.ApprovalScope,
            ExecutionTruthScope = reviewResult.ExecutionTruthScope,
            AttemptedTaskCohort = reviewResult.AttemptedTaskCohort,
            ReplacementScope = reviewResult.ReplacementScope,
            Controls = reviewResult.Controls,
            FreezeVerdict = freezeVerdict,
            ImplementationScopeFrozen = string.Equals(freezeVerdict, "limited_scope_frozen", StringComparison.Ordinal),
            LimitedMainPathImplementationAllowed = string.Equals(freezeVerdict, "limited_scope_frozen", StringComparison.Ordinal),
            ScopeExpansionAllowed = false,
            MainRendererReplacementAllowed = false,
            RuntimeShadowExecutionAllowed = false,
            FullRolloutAllowed = false,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            NextRequiredActions = BuildNextActions(freezeVerdict),
            Notes = BuildNotes(reviewResult, freezeVerdict)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string FormatMarkdown(RuntimeTokenPhase3ReplacementScopeFreezeResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 3 Replacement Scope Freeze");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Freeze verdict: `{result.FreezeVerdict}`");
        builder.AppendLine($"- Implementation scope frozen: `{(result.ImplementationScopeFrozen ? "yes" : "no")}`");
        builder.AppendLine($"- Limited main-path implementation allowed: `{(result.LimitedMainPathImplementationAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Scope expansion allowed: `{(result.ScopeExpansionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Main renderer replacement allowed: `{(result.MainRendererReplacementAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(result.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Full rollout allowed: `{(result.FullRolloutAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Request kind: `{result.RequestKind}`");
        builder.AppendLine($"- Candidate version: `{result.CandidateVersion}`");
        builder.AppendLine($"- Fallback version: `{result.FallbackVersion}`");
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
        builder.AppendLine($"- Fallback version: `{result.Controls.FallbackVersion}`");
        builder.AppendLine($"- Post-rollout audit required: `{(result.Controls.PostRolloutAuditRequired ? "yes" : "no")}`");
        builder.AppendLine();
        builder.AppendLine("## Execution Truth Scope");
        builder.AppendLine();
        builder.AppendLine($"- Execution mode: `{result.ExecutionTruthScope.ExecutionMode}`");
        builder.AppendLine($"- Worker backend: `{result.ExecutionTruthScope.WorkerBackend}`");
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
        builder.AppendLine();
        builder.AppendLine("## Referenced Review");
        builder.AppendLine();
        builder.AppendLine($"- Main-path replacement review markdown: `{result.MainPathReplacementReviewMarkdownArtifactPath}`");
        builder.AppendLine($"- Main-path replacement review json: `{result.MainPathReplacementReviewJsonArtifactPath}`");

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

    private static IReadOnlyList<string> BuildNextActions(string freezeVerdict)
    {
        if (string.Equals(freezeVerdict, "limited_scope_frozen", StringComparison.Ordinal))
        {
            return
            [
                "implement only the frozen worker main-path default and do not widen request kinds, surfaces, or runtime modes",
                "retain global kill switch, per-request-kind fallback, per-surface fallback, and candidate version pin throughout rollout",
                "run a post-rollout audit before any request-kind expansion, surface expansion, or full rollout review"
            ];
        }

        return
        [
            "clear replacement-scope blockers and rerun replacement-scope-freeze",
            "do not start any main-path implementation while scope freeze is blocked"
        ];
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenPhase3MainPathReplacementReviewResult reviewResult,
        string freezeVerdict)
    {
        var notes = new List<string>
        {
            "This artifact freezes only the implementation boundary for a limited main-path line. It does not approve main renderer replacement, runtime shadow, or full rollout.",
            "The frozen scope remains limited to current no_provider_agent_mediated runtime mode on the formal null_worker backend.",
            $"Replacement scope is `{reviewResult.RequestKind}` on `{reviewResult.TargetSurface}` with fallback `{reviewResult.FallbackVersion}`."
        };

        notes.Add(string.Equals(freezeVerdict, "limited_scope_frozen", StringComparison.Ordinal)
            ? "Scope is now fixed for a narrow implementation line and must not expand to other request kinds, surfaces, or provider-backed modes."
            : "Scope freeze is blocked, so no main-path implementation line should start from this state.");

        return notes;
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-3-replacement-scope-freeze-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-3",
            $"replacement-scope-freeze-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string absolutePath)
    {
        return Path.GetRelativePath(repoRoot, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
