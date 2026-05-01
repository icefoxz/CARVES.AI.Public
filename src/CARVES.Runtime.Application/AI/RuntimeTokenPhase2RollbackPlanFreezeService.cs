using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase2RollbackPlanFreezeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly string[] AutomaticRollbackTriggers =
    [
        "hard_fail_count_gt_0",
        "constraint_violation_rate_regression",
        "task_success_rate_regression",
        "review_admission_rate_regression",
        "retry_count_per_task_regression",
        "repair_count_per_task_regression",
        "provider_or_internal_cap_hit_regression",
    ];

    private static readonly string[] ManualRollbackActions =
    [
        "disable_candidate_globally",
        "restrict_canary_allowlist_to_none",
        "fallback_to_original_worker_system_instructions",
        "pin_runtime_telemetry_to_original_candidate_version",
    ];

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase2RollbackPlanFreezeService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase2RollbackPlanFreezeResult Persist(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenPhase2RequestKindSliceProofResult requestKindSliceProofResult)
    {
        return Persist(paths, candidateResult, requestKindSliceProofResult, candidateResult.ResultDate);
    }

    internal static RuntimeTokenPhase2RollbackPlanFreezeResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenPhase2RequestKindSliceProofResult requestKindSliceProofResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(candidateResult, requestKindSliceProofResult, resultDate);

        var blockingReasons = new List<string>();
        if (!requestKindSliceProofResult.CrossKindProofAvailable)
        {
            blockingReasons.Add("request_kind_slice_cross_kind_proof_not_available");
        }

        if (requestKindSliceProofResult.PolicyCriticalFragmentRemovedCount > 0)
        {
            blockingReasons.Add("policy_critical_fragment_removed_by_request_kind_slice");
        }

        var reviewed = blockingReasons.Count == 0;
        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2RollbackPlanFreezeResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = candidateResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            CandidateMarkdownArtifactPath = candidateResult.MarkdownArtifactPath,
            CandidateJsonArtifactPath = candidateResult.JsonArtifactPath,
            RequestKindSliceProofMarkdownArtifactPath = requestKindSliceProofResult.MarkdownArtifactPath,
            RequestKindSliceProofJsonArtifactPath = requestKindSliceProofResult.JsonArtifactPath,
            TargetSurface = candidateResult.CandidateSurfaceId,
            CandidateStrategy = candidateResult.CandidateStrategy,
            CandidateVersion = $"wrapper_candidate_{resultDate:yyyyMMdd}_{Sanitize(candidateResult.CandidateSurfaceId)}",
            FallbackVersion = "original_worker_system_instructions",
            RollbackPlanReviewed = reviewed,
            RollbackTestPlanDefined = reviewed,
            DefaultEnabled = false,
            GlobalKillSwitch = true,
            PerRequestKindFallback = true,
            PerSurfaceFallback = true,
            CanaryRequestKindAllowlist = requestKindSliceProofResult.CanaryRequestKindAllowlist,
            AutomaticRollbackTriggers = AutomaticRollbackTriggers,
            ManualRollbackActions = ManualRollbackActions,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            Notes = BuildNotes(candidateResult, requestKindSliceProofResult, reviewed),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenPhase2RollbackPlanFreezeResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Wrapper Canary Rollback Plan");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Candidate version: `{result.CandidateVersion}`");
        builder.AppendLine($"- Fallback version: `{result.FallbackVersion}`");
        builder.AppendLine($"- Rollback plan reviewed: `{(result.RollbackPlanReviewed ? "yes" : "no")}`");
        builder.AppendLine($"- Rollback test plan defined: `{(result.RollbackTestPlanDefined ? "yes" : "no")}`");
        builder.AppendLine($"- Default enabled: `{(result.DefaultEnabled ? "yes" : "no")}`");
        builder.AppendLine($"- Global kill switch: `{(result.GlobalKillSwitch ? "yes" : "no")}`");
        builder.AppendLine($"- Per-request-kind fallback: `{(result.PerRequestKindFallback ? "yes" : "no")}`");
        builder.AppendLine($"- Per-surface fallback: `{(result.PerSurfaceFallback ? "yes" : "no")}`");
        builder.AppendLine($"- Canary request-kind allowlist: `{string.Join(", ", result.CanaryRequestKindAllowlist)}`");
        builder.AppendLine();

        builder.AppendLine("## Automatic Rollback Triggers");
        builder.AppendLine();
        foreach (var trigger in result.AutomaticRollbackTriggers)
        {
            builder.AppendLine($"- `{trigger}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Manual Rollback Actions");
        builder.AppendLine();
        foreach (var action in result.ManualRollbackActions)
        {
            builder.AppendLine($"- `{action}`");
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
        builder.AppendLine("## Notes");
        builder.AppendLine();
        foreach (var note in result.Notes)
        {
            builder.AppendLine($"- {note}");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenPhase2RequestKindSliceProofResult requestKindSliceProofResult,
        bool reviewed)
    {
        var notes = new List<string>
        {
            $"Rollback posture is frozen for `{candidateResult.CandidateSurfaceId}` on the worker-only canary scope `{string.Join(", ", requestKindSliceProofResult.CanaryRequestKindAllowlist)}`.",
            "This artifact freezes rollback intent only. It does not approve runtime shadow, active canary, or live wrapper replacement.",
            "Fallback target remains the original worker system instruction line with default-off posture."
        };

        if (reviewed)
        {
            notes.Add("Rollback review passed on the current offline candidate line because cross-kind slicing proof shows zero removed policy-critical fragments.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenPhase2RequestKindSliceProofResult requestKindSliceProofResult,
        DateOnly resultDate)
    {
        if (candidateResult.ResultDate != resultDate || requestKindSliceProofResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Rollback plan freeze requires candidate and request-kind slice proof dates to match the requested result date.");
        }

        if (!string.Equals(candidateResult.CohortId, requestKindSliceProofResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Rollback plan freeze requires candidate and request-kind slice proof to point at the same frozen cohort.");
        }

        if (!string.Equals(candidateResult.CandidateSurfaceId, requestKindSliceProofResult.TargetSurface, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Rollback plan freeze requires request-kind slice proof to point at the same wrapper surface.");
        }

        if (!string.Equals(candidateResult.CandidateSurfaceId, "worker:system:$.instructions", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Rollback plan freeze currently supports only the worker system wrapper candidate line.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-wrapper-canary-rollback-plan-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"wrapper-canary-rollback-plan-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString();
    }
}
