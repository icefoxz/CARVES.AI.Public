using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase2ActiveCanaryApprovalReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly RuntimeTokenWorkerWrapperCanaryService canaryService;

    public RuntimeTokenPhase2ActiveCanaryApprovalReviewService(
        ControlPlanePaths paths,
        RuntimeTokenWorkerWrapperCanaryService? canaryService = null)
    {
        this.paths = paths;
        this.canaryService = canaryService ?? new RuntimeTokenWorkerWrapperCanaryService();
    }

    public RuntimeTokenPhase2ActiveCanaryApprovalReviewResult Persist(
        RuntimeTokenPhase2ActiveCanaryReadinessReviewResult readinessReviewResult,
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperEnterActiveCanaryReviewBundle reviewBundle,
        RuntimeTokenPhase2RollbackPlanFreezeResult rollbackPlanFreezeResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult nonInferiorityCohortFreezeResult)
    {
        return Persist(
            paths,
            readinessReviewResult,
            candidateResult,
            reviewBundle,
            rollbackPlanFreezeResult,
            nonInferiorityCohortFreezeResult,
            canaryService.DescribeMechanismContract(),
            readinessReviewResult.ResultDate);
    }

    internal static RuntimeTokenPhase2ActiveCanaryApprovalReviewResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenPhase2ActiveCanaryReadinessReviewResult readinessReviewResult,
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperEnterActiveCanaryReviewBundle reviewBundle,
        RuntimeTokenPhase2RollbackPlanFreezeResult rollbackPlanFreezeResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult nonInferiorityCohortFreezeResult,
        RuntimeTokenWorkerWrapperCanaryMechanismContract mechanismContract,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(readinessReviewResult, candidateResult, reviewBundle, rollbackPlanFreezeResult, nonInferiorityCohortFreezeResult, resultDate);

        var blockingReasons = new List<string>();
        if (!readinessReviewResult.EnterActiveCanaryReviewAccepted)
        {
            blockingReasons.Add("enter_active_canary_review_not_accepted");
        }

        if (readinessReviewResult.BlockingReasons.Count > 0)
        {
            blockingReasons.AddRange(readinessReviewResult.BlockingReasons);
        }

        if (!rollbackPlanFreezeResult.RollbackPlanReviewed)
        {
            blockingReasons.Add("rollback_plan_not_reviewed");
        }

        if (!rollbackPlanFreezeResult.RollbackTestPlanDefined)
        {
            blockingReasons.Add("rollback_test_plan_not_defined");
        }

        if (rollbackPlanFreezeResult.DefaultEnabled)
        {
            blockingReasons.Add("canary_default_must_remain_off");
        }

        if (!rollbackPlanFreezeResult.GlobalKillSwitch)
        {
            blockingReasons.Add("global_kill_switch_not_defined");
        }

        if (!rollbackPlanFreezeResult.PerRequestKindFallback)
        {
            blockingReasons.Add("per_request_kind_fallback_not_defined");
        }

        if (!rollbackPlanFreezeResult.PerSurfaceFallback)
        {
            blockingReasons.Add("per_surface_fallback_not_defined");
        }

        if (!nonInferiorityCohortFreezeResult.NonInferiorityCohortFrozen)
        {
            blockingReasons.Add("non_inferiority_cohort_not_frozen");
        }

        var prerequisiteReviewPassed = blockingReasons.Count == 0;
        var executionNotApprovedReasons = new List<string>();
        if (prerequisiteReviewPassed)
        {
            executionNotApprovedReasons.Add("separate_active_canary_execution_approval_required");
        }

        var reviewVerdict = prerequisiteReviewPassed
            ? "approved_for_canary_implementation_only"
            : "blocked_for_active_canary";

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2ActiveCanaryApprovalReviewResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = readinessReviewResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            ReadinessReviewMarkdownArtifactPath = readinessReviewResult.MarkdownArtifactPath,
            ReadinessReviewJsonArtifactPath = readinessReviewResult.JsonArtifactPath,
            CandidateMarkdownArtifactPath = candidateResult.MarkdownArtifactPath,
            CandidateJsonArtifactPath = candidateResult.JsonArtifactPath,
            ReviewBundleMarkdownArtifactPath = candidateResult.ReviewBundleMarkdownArtifactPath,
            ReviewBundleJsonArtifactPath = candidateResult.ReviewBundleJsonArtifactPath,
            ManualReviewResolutionMarkdownArtifactPath = readinessReviewResult.ManualReviewResolutionMarkdownArtifactPath,
            ManualReviewResolutionJsonArtifactPath = readinessReviewResult.ManualReviewResolutionJsonArtifactPath,
            RequestKindSliceProofMarkdownArtifactPath = readinessReviewResult.RequestKindSliceProofMarkdownArtifactPath,
            RequestKindSliceProofJsonArtifactPath = readinessReviewResult.RequestKindSliceProofJsonArtifactPath,
            RollbackPlanMarkdownArtifactPath = rollbackPlanFreezeResult.MarkdownArtifactPath,
            RollbackPlanJsonArtifactPath = rollbackPlanFreezeResult.JsonArtifactPath,
            NonInferiorityCohortMarkdownArtifactPath = nonInferiorityCohortFreezeResult.MarkdownArtifactPath,
            NonInferiorityCohortJsonArtifactPath = nonInferiorityCohortFreezeResult.JsonArtifactPath,
            TargetSurface = candidateResult.CandidateSurfaceId,
            CandidateStrategy = candidateResult.CandidateStrategy,
            TargetSurfaceReductionRatioP95 = readinessReviewResult.TargetSurfaceReductionRatioP95,
            TargetSurfaceShareP95 = readinessReviewResult.TargetSurfaceShareP95,
            ExpectedWholeRequestReductionP95 = readinessReviewResult.ExpectedWholeRequestReductionP95,
            DefaultEnabled = rollbackPlanFreezeResult.DefaultEnabled,
            ApprovalScope = "limited_explicit_allowlist",
            CanaryRequestKindAllowlist = rollbackPlanFreezeResult.CanaryRequestKindAllowlist,
            CandidateVersion = rollbackPlanFreezeResult.CandidateVersion,
            FallbackVersion = rollbackPlanFreezeResult.FallbackVersion,
            RollbackPlanFrozen = rollbackPlanFreezeResult.RollbackPlanReviewed,
            NonInferiorityCohortFrozen = nonInferiorityCohortFreezeResult.NonInferiorityCohortFrozen,
            ReviewVerdict = reviewVerdict,
            PrerequisiteReviewPassed = prerequisiteReviewPassed,
            CanaryImplementationAuthorized = prerequisiteReviewPassed,
            CanaryExecutionAuthorized = false,
            ActiveCanaryApproved = false,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            ExecutionNotApprovedReasons = executionNotApprovedReasons,
            NextRequiredActions = BuildNextRequiredActions(prerequisiteReviewPassed),
            Notes = BuildNotes(readinessReviewResult, rollbackPlanFreezeResult, nonInferiorityCohortFreezeResult, mechanismContract, prerequisiteReviewPassed),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenPhase2ActiveCanaryApprovalReviewResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Active Canary Approval");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Approval requested: `{result.ApprovalRequested}`");
        builder.AppendLine($"- Review verdict: `{result.ReviewVerdict}`");
        builder.AppendLine($"- Prerequisite review passed: `{(result.PrerequisiteReviewPassed ? "yes" : "no")}`");
        builder.AppendLine($"- Canary implementation authorized: `{(result.CanaryImplementationAuthorized ? "yes" : "no")}`");
        builder.AppendLine($"- Canary execution authorized: `{(result.CanaryExecutionAuthorized ? "yes" : "no")}`");
        builder.AppendLine($"- Active canary approved: `{(result.ActiveCanaryApproved ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(result.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Main renderer replacement allowed: `{(result.MainRendererReplacementAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Approval scope: `{result.ApprovalScope}`");
        builder.AppendLine($"- Default enabled: `{(result.DefaultEnabled ? "yes" : "no")}`");
        builder.AppendLine($"- Canary request-kind allowlist: `{string.Join(", ", result.CanaryRequestKindAllowlist)}`");
        builder.AppendLine($"- Candidate version: `{result.CandidateVersion}`");
        builder.AppendLine($"- Fallback version: `{result.FallbackVersion}`");
        builder.AppendLine($"- Rollback plan frozen: `{(result.RollbackPlanFrozen ? "yes" : "no")}`");
        builder.AppendLine($"- Non-inferiority cohort frozen: `{(result.NonInferiorityCohortFrozen ? "yes" : "no")}`");
        builder.AppendLine();

        builder.AppendLine("## Reduction");
        builder.AppendLine();
        builder.AppendLine($"- Target surface reduction ratio p95: `{result.TargetSurfaceReductionRatioP95:0.000}`");
        builder.AppendLine($"- Target surface share p95: `{result.TargetSurfaceShareP95:0.000}`");
        builder.AppendLine($"- Expected whole-request reduction p95: `{result.ExpectedWholeRequestReductionP95:0.000}`");
        builder.AppendLine();

        builder.AppendLine("## Referenced Artifacts");
        builder.AppendLine();
        builder.AppendLine($"- Readiness review markdown: `{result.ReadinessReviewMarkdownArtifactPath}`");
        builder.AppendLine($"- Readiness review json: `{result.ReadinessReviewJsonArtifactPath}`");
        builder.AppendLine($"- Candidate markdown: `{result.CandidateMarkdownArtifactPath}`");
        builder.AppendLine($"- Candidate json: `{result.CandidateJsonArtifactPath}`");
        builder.AppendLine($"- Review bundle markdown: `{result.ReviewBundleMarkdownArtifactPath}`");
        builder.AppendLine($"- Review bundle json: `{result.ReviewBundleJsonArtifactPath}`");
        builder.AppendLine($"- Manual review resolution markdown: `{result.ManualReviewResolutionMarkdownArtifactPath}`");
        builder.AppendLine($"- Manual review resolution json: `{result.ManualReviewResolutionJsonArtifactPath}`");
        builder.AppendLine($"- Request-kind slice proof markdown: `{result.RequestKindSliceProofMarkdownArtifactPath}`");
        builder.AppendLine($"- Request-kind slice proof json: `{result.RequestKindSliceProofJsonArtifactPath}`");
        builder.AppendLine($"- Rollback plan markdown: `{result.RollbackPlanMarkdownArtifactPath}`");
        builder.AppendLine($"- Rollback plan json: `{result.RollbackPlanJsonArtifactPath}`");
        builder.AppendLine($"- Non-inferiority cohort markdown: `{result.NonInferiorityCohortMarkdownArtifactPath}`");
        builder.AppendLine($"- Non-inferiority cohort json: `{result.NonInferiorityCohortJsonArtifactPath}`");

        if (result.BlockingReasons.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Blocking Reasons");
            builder.AppendLine();
            foreach (var item in result.BlockingReasons)
            {
                builder.AppendLine($"- `{item}`");
            }
        }

        if (result.ExecutionNotApprovedReasons.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Execution Not Approved Reasons");
            builder.AppendLine();
            foreach (var item in result.ExecutionNotApprovedReasons)
            {
                builder.AppendLine($"- `{item}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Next Required Actions");
        builder.AppendLine();
        foreach (var item in result.NextRequiredActions)
        {
            builder.AppendLine($"- {item}");
        }

        builder.AppendLine();
        builder.AppendLine("## Notes");
        builder.AppendLine();
        foreach (var item in result.Notes)
        {
            builder.AppendLine($"- {item}");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildNextRequiredActions(bool prerequisiteReviewPassed)
    {
        if (!prerequisiteReviewPassed)
        {
            return ["clear approval blockers and rerun active-canary-approval-review"];
        }

        return
        [
            "implement a default-off active canary mechanism scoped to the explicit allowlist only",
            "preserve global kill switch, per-request-kind fallback, and per-surface fallback in runtime wiring",
            "request a separate active canary execution approval after implementation is reviewed"
        ];
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenPhase2ActiveCanaryReadinessReviewResult readinessReviewResult,
        RuntimeTokenPhase2RollbackPlanFreezeResult rollbackPlanFreezeResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult nonInferiorityCohortFreezeResult,
        RuntimeTokenWorkerWrapperCanaryMechanismContract mechanismContract,
        bool prerequisiteReviewPassed)
    {
        var notes = new List<string>
        {
            "This review is separate from readiness review. It decides whether active canary implementation or execution may proceed.",
            "Active canary execution remains blocked until a separate execution approval is granted.",
            $"Rollback posture remains default-off with fallback `{rollbackPlanFreezeResult.FallbackVersion}` and allowlist `{string.Join(", ", rollbackPlanFreezeResult.CanaryRequestKindAllowlist)}`.",
            $"Frozen non-inferiority cohort remains `{nonInferiorityCohortFreezeResult.TaskIds.Count}` worker task(s) with expected whole-request reduction `{readinessReviewResult.ExpectedWholeRequestReductionP95:0.000}`.",
            $"Mechanism contract currently advertises default-off={mechanismContract.DefaultOffSupported.ToString().ToLowerInvariant()}, kill-switch={mechanismContract.GlobalKillSwitchSupported.ToString().ToLowerInvariant()}, request-kind allowlist={mechanismContract.RequestKindAllowlistSupported.ToString().ToLowerInvariant()}, and surface allowlist={mechanismContract.SurfaceAllowlistSupported.ToString().ToLowerInvariant()}."
        };

        if (prerequisiteReviewPassed)
        {
            notes.Add("Current approval is implementation-only because execution review remains a separate governed step even when the canary mechanism is present.");
        }
        else
        {
            notes.Add("Approval stayed blocked because prerequisite review did not fully pass.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenPhase2ActiveCanaryReadinessReviewResult readinessReviewResult,
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperEnterActiveCanaryReviewBundle reviewBundle,
        RuntimeTokenPhase2RollbackPlanFreezeResult rollbackPlanFreezeResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult nonInferiorityCohortFreezeResult,
        DateOnly resultDate)
    {
        if (readinessReviewResult.ResultDate != resultDate
            || candidateResult.ResultDate != resultDate
            || reviewBundle.ResultDate != resultDate
            || rollbackPlanFreezeResult.ResultDate != resultDate
            || nonInferiorityCohortFreezeResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Active canary approval review requires all inputs to share the same result date.");
        }

        if (!string.Equals(readinessReviewResult.CohortId, candidateResult.CohortId, StringComparison.Ordinal)
            || !string.Equals(readinessReviewResult.CohortId, rollbackPlanFreezeResult.CohortId, StringComparison.Ordinal)
            || !string.Equals(readinessReviewResult.CohortId, nonInferiorityCohortFreezeResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary approval review requires all inputs to point at the same frozen cohort.");
        }

        if (!string.Equals(readinessReviewResult.TargetSurface, candidateResult.CandidateSurfaceId, StringComparison.Ordinal)
            || !string.Equals(readinessReviewResult.TargetSurface, reviewBundle.CandidateSurfaceId, StringComparison.Ordinal)
            || !string.Equals(readinessReviewResult.TargetSurface, rollbackPlanFreezeResult.TargetSurface, StringComparison.Ordinal)
            || !string.Equals(readinessReviewResult.TargetSurface, nonInferiorityCohortFreezeResult.TargetSurface, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary approval review requires all inputs to point at the same wrapper surface.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-active-canary-approval-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"active-canary-approval-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }
}
