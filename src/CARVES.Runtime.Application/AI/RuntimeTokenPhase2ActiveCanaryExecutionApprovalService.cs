using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase2ActiveCanaryExecutionApprovalService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly RuntimeTokenWorkerWrapperCanaryService canaryService;

    public RuntimeTokenPhase2ActiveCanaryExecutionApprovalService(
        ControlPlanePaths paths,
        RuntimeTokenWorkerWrapperCanaryService? canaryService = null)
    {
        this.paths = paths;
        this.canaryService = canaryService ?? new RuntimeTokenWorkerWrapperCanaryService();
    }

    public RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult Persist(
        RuntimeTokenPhase2ActiveCanaryApprovalReviewResult approvalReviewResult)
    {
        return Persist(paths, approvalReviewResult, canaryService.DescribeMechanismContract(), approvalReviewResult.ResultDate);
    }

    internal static RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenPhase2ActiveCanaryApprovalReviewResult approvalReviewResult,
        RuntimeTokenWorkerWrapperCanaryMechanismContract mechanismContract,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        if (approvalReviewResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Active canary execution approval requires the implementation approval review to share the same result date.");
        }

        var blockingReasons = new List<string>();
        if (!approvalReviewResult.PrerequisiteReviewPassed)
        {
            blockingReasons.Add("active_canary_implementation_review_not_passed");
        }

        if (!approvalReviewResult.CanaryImplementationAuthorized)
        {
            blockingReasons.Add("canary_implementation_not_authorized");
        }

        if (!approvalReviewResult.RollbackPlanFrozen)
        {
            blockingReasons.Add("rollback_plan_not_frozen");
        }

        if (!approvalReviewResult.NonInferiorityCohortFrozen)
        {
            blockingReasons.Add("non_inferiority_cohort_not_frozen");
        }

        if (!mechanismContract.DefaultOffSupported)
        {
            blockingReasons.Add("default_off_canary_mechanism_missing");
        }

        if (!mechanismContract.GlobalKillSwitchSupported)
        {
            blockingReasons.Add("global_kill_switch_mechanism_missing");
        }

        if (!mechanismContract.RequestKindAllowlistSupported)
        {
            blockingReasons.Add("request_kind_allowlist_mechanism_missing");
        }

        if (!mechanismContract.SurfaceAllowlistSupported)
        {
            blockingReasons.Add("surface_allowlist_mechanism_missing");
        }

        if (!mechanismContract.CandidateVersionPinSupported)
        {
            blockingReasons.Add("candidate_version_pin_mechanism_missing");
        }

        if (!string.Equals(mechanismContract.TargetSurface, approvalReviewResult.TargetSurface, StringComparison.Ordinal))
        {
            blockingReasons.Add("mechanism_target_surface_mismatch");
        }

        if (!string.Equals(mechanismContract.CandidateVersion, approvalReviewResult.CandidateVersion, StringComparison.Ordinal))
        {
            blockingReasons.Add("mechanism_candidate_version_mismatch");
        }

        if (!string.Equals(mechanismContract.FallbackVersion, approvalReviewResult.FallbackVersion, StringComparison.Ordinal))
        {
            blockingReasons.Add("mechanism_fallback_version_mismatch");
        }

        var approved = blockingReasons.Count == 0;
        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = approvalReviewResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            ApprovalReviewMarkdownArtifactPath = approvalReviewResult.MarkdownArtifactPath,
            ApprovalReviewJsonArtifactPath = approvalReviewResult.JsonArtifactPath,
            TargetSurface = approvalReviewResult.TargetSurface,
            CandidateStrategy = approvalReviewResult.CandidateStrategy,
            ApprovalScope = approvalReviewResult.ApprovalScope,
            CanaryRequestKindAllowlist = approvalReviewResult.CanaryRequestKindAllowlist,
            CandidateVersion = approvalReviewResult.CandidateVersion,
            FallbackVersion = approvalReviewResult.FallbackVersion,
            DefaultEnabled = false,
            RollbackPlanFrozen = approvalReviewResult.RollbackPlanFrozen,
            NonInferiorityCohortFrozen = approvalReviewResult.NonInferiorityCohortFrozen,
            ReviewVerdict = approved ? "approved_for_active_canary_execution" : "blocked_for_active_canary_execution",
            ActiveCanaryApproved = approved,
            CanaryExecutionAuthorized = approved,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            ExpectedWholeRequestReductionP95 = approvalReviewResult.ExpectedWholeRequestReductionP95,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            NextRequiredActions = BuildNextActions(approved),
            Notes = BuildNotes(approvalReviewResult, mechanismContract, approved)
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result, mechanismContract));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string FormatMarkdown(
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult result,
        RuntimeTokenWorkerWrapperCanaryMechanismContract mechanismContract)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Active Canary Execution Approval");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Review verdict: `{result.ReviewVerdict}`");
        builder.AppendLine($"- Active canary approved: `{(result.ActiveCanaryApproved ? "yes" : "no")}`");
        builder.AppendLine($"- Canary execution authorized: `{(result.CanaryExecutionAuthorized ? "yes" : "no")}`");
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
        builder.AppendLine($"- Expected whole-request reduction p95: `{result.ExpectedWholeRequestReductionP95:0.000}`");
        builder.AppendLine();
        builder.AppendLine("## Referenced Artifacts");
        builder.AppendLine();
        builder.AppendLine($"- Implementation approval review markdown: `{result.ApprovalReviewMarkdownArtifactPath}`");
        builder.AppendLine($"- Implementation approval review json: `{result.ApprovalReviewJsonArtifactPath}`");
        builder.AppendLine();
        builder.AppendLine("## Mechanism Contract");
        builder.AppendLine();
        builder.AppendLine($"- Target surface: `{mechanismContract.TargetSurface}`");
        builder.AppendLine($"- Request kind: `{mechanismContract.RequestKind}`");
        builder.AppendLine($"- Default-off supported: `{(mechanismContract.DefaultOffSupported ? "yes" : "no")}`");
        builder.AppendLine($"- Global kill switch supported: `{(mechanismContract.GlobalKillSwitchSupported ? "yes" : "no")}`");
        builder.AppendLine($"- Request-kind allowlist supported: `{(mechanismContract.RequestKindAllowlistSupported ? "yes" : "no")}`");
        builder.AppendLine($"- Surface allowlist supported: `{(mechanismContract.SurfaceAllowlistSupported ? "yes" : "no")}`");
        builder.AppendLine($"- Candidate version pin supported: `{(mechanismContract.CandidateVersionPinSupported ? "yes" : "no")}`");
        builder.AppendLine($"- Environment variables: `{string.Join(", ", mechanismContract.EnvironmentVariables)}`");

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

    private static IReadOnlyList<string> BuildNextActions(bool approved)
    {
        if (!approved)
        {
            return ["clear execution approval blockers and rerun active-canary-execution-approval"];
        }

        return
        [
            "keep the canary mechanism default-off until an operator explicitly enables the worker-only allowlist",
            "scope execution to the pinned candidate version and original fallback version only",
            "monitor non-inferiority metrics and trigger rollback immediately on any hard fail"
        ];
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenPhase2ActiveCanaryApprovalReviewResult approvalReviewResult,
        RuntimeTokenWorkerWrapperCanaryMechanismContract mechanismContract,
        bool approved)
    {
        var notes = new List<string>
        {
            "This approval is execution-scoped and remains limited to the explicit worker allowlist only.",
            "Default-off posture remains mandatory; approval does not imply automatic enablement.",
            $"Mechanism contract pins candidate `{mechanismContract.CandidateVersion}` with fallback `{mechanismContract.FallbackVersion}`."
        };

        if (approved)
        {
            notes.Add($"Execution is approved only for `{approvalReviewResult.TargetSurface}` and expected whole-request reduction remains `{approvalReviewResult.ExpectedWholeRequestReductionP95:0.000}` on the frozen cohort.");
        }
        else
        {
            notes.Add("Execution stayed blocked because implementation approval or mechanism contract requirements did not fully pass.");
        }

        return notes;
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-active-canary-execution-approval-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"active-canary-execution-approval-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }
}
