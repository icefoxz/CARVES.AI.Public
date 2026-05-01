using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenBaselineTrustLineService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeTokenBaselineTrustLineService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenBaselineTrustLineResult Persist(
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineReadinessGateResult readinessGateResult,
        RuntimeTokenBaselineRecomputeResult recomputeResult)
    {
        return Persist(paths, evidenceResult, readinessGateResult, recomputeResult, recomputeResult.ResultDate);
    }

    internal static RuntimeTokenBaselineTrustLineResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineReadinessGateResult readinessGateResult,
        RuntimeTokenBaselineRecomputeResult recomputeResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(evidenceResult, readinessGateResult, recomputeResult, resultDate);

        var phase10TargetDecisionMayReferenceThisLine = readinessGateResult.UnlocksPhase10TargetDecision;
        var capBasedTargetDecisionAllowed = phase10TargetDecisionMayReferenceThisLine
                                            && readinessGateResult.Readiness.CapBasedTargetDecisionAllowed;
        var totalCostClaimAllowed = phase10TargetDecisionMayReferenceThisLine
                                    && readinessGateResult.Readiness.TotalCostClaimAllowed;
        var blockingReasons = readinessGateResult.BlockingReasons
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var trustLineClassification = phase10TargetDecisionMayReferenceThisLine
            ? "recomputed_trusted_for_phase_1_target_decision"
            : "recomputed_but_insufficient_data_for_phase_1_target_decision";
        var trustMarkdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var trustJsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenBaselineTrustLineResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = evidenceResult.Aggregation.Cohort.CohortId,
            TrustLineClassification = trustLineClassification,
            SupersedesPreLedgerLine = true,
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            ReadinessMarkdownArtifactPath = recomputeResult.ReadinessMarkdownArtifactPath,
            ReadinessJsonArtifactPath = recomputeResult.ReadinessJsonArtifactPath,
            RecomputeMarkdownArtifactPath = recomputeResult.MarkdownArtifactPath,
            RecomputeJsonArtifactPath = recomputeResult.JsonArtifactPath,
            RecommendationDecision = recomputeResult.RecommendationDecision,
            RecommendationNextTrack = recomputeResult.RecommendationNextTrack,
            Phase10TargetDecisionMayReferenceThisLine = phase10TargetDecisionMayReferenceThisLine,
            CapBasedTargetDecisionAllowed = capBasedTargetDecisionAllowed,
            TotalCostClaimAllowed = totalCostClaimAllowed,
            Phase12TargetedCompactCandidateAllowed = false,
            RuntimeShadowExecutionAllowed = false,
            ActiveCanaryAllowed = false,
            BlockingReasons = blockingReasons,
            Notes = BuildNotes(phase10TargetDecisionMayReferenceThisLine, capBasedTargetDecisionAllowed, totalCostClaimAllowed),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(trustMarkdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(trustJsonPath)!);
        File.WriteAllText(trustMarkdownPath, FormatMarkdown(result));
        File.WriteAllText(trustJsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenBaselineTrustLineResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 0A Trust Line Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Trust line classification: `{result.TrustLineClassification}`");
        builder.AppendLine($"- Supersedes pre-ledger line: `{(result.SupersedesPreLedgerLine ? "yes" : "no")}`");
        builder.AppendLine($"- Phase 1.0 target decision may reference this line: `{(result.Phase10TargetDecisionMayReferenceThisLine ? "yes" : "no")}`");
        builder.AppendLine($"- Cap-based target decision allowed: `{(result.CapBasedTargetDecisionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Total cost claim allowed: `{(result.TotalCostClaimAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Phase 1.2 targeted compact candidate allowed: `{(result.Phase12TargetedCompactCandidateAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(result.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Active canary allowed: `{(result.ActiveCanaryAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Recommendation decision: `{result.RecommendationDecision}`");
        builder.AppendLine($"- Recommendation next track: `{result.RecommendationNextTrack}`");
        builder.AppendLine($"- Evidence markdown artifact: `{result.EvidenceMarkdownArtifactPath}`");
        builder.AppendLine($"- Evidence json artifact: `{result.EvidenceJsonArtifactPath}`");
        builder.AppendLine($"- Readiness markdown artifact: `{result.ReadinessMarkdownArtifactPath}`");
        builder.AppendLine($"- Readiness json artifact: `{result.ReadinessJsonArtifactPath}`");
        builder.AppendLine($"- Recompute markdown artifact: `{result.RecomputeMarkdownArtifactPath}`");
        builder.AppendLine($"- Recompute json artifact: `{result.RecomputeJsonArtifactPath}`");
        builder.AppendLine();
        builder.AppendLine("## Blocking Reasons");
        builder.AppendLine();
        if (result.BlockingReasons.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var reason in result.BlockingReasons)
            {
                builder.AppendLine($"- `{reason}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Notes");
        builder.AppendLine();
        if (result.Notes.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var note in result.Notes)
            {
                builder.AppendLine($"- {note}");
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildNotes(
        bool phase10TargetDecisionMayReferenceThisLine,
        bool capBasedTargetDecisionAllowed,
        bool totalCostClaimAllowed)
    {
        var notes = new List<string>
        {
            "This trust line supersedes the pre-ledger Phase 0A result line for Phase 1 reference decisions.",
            "Phase 1.2 targeted compact candidate work remains blocked until Phase 1.0 proves a material target.",
            "Runtime shadow execution, active canary, and main renderer replacement remain blocked.",
        };

        if (!phase10TargetDecisionMayReferenceThisLine)
        {
            notes.Add("Phase 1.0 may not reference this line until the remaining blocking reasons are resolved.");
        }

        if (!capBasedTargetDecisionAllowed)
        {
            notes.Add("Cap-based dominance remains blocked on this line.");
        }

        if (!totalCostClaimAllowed)
        {
            notes.Add("Authoritative total-cost claims remain blocked on this line.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineReadinessGateResult readinessGateResult,
        RuntimeTokenBaselineRecomputeResult recomputeResult,
        DateOnly resultDate)
    {
        if (evidenceResult.ResultDate != resultDate
            || readinessGateResult.ResultDate != resultDate
            || recomputeResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Trust line classification requires evidence, readiness, and recompute results to share the same result date.");
        }

        if (!string.Equals(evidenceResult.Aggregation.Cohort.CohortId, recomputeResult.Cohort.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Trust line classification requires recompute result to reference the same frozen cohort as the evidence result.");
        }

        if (!string.Equals(readinessGateResult.EvidenceMarkdownArtifactPath, evidenceResult.MarkdownArtifactPath, StringComparison.Ordinal)
            || !string.Equals(readinessGateResult.EvidenceJsonArtifactPath, evidenceResult.JsonArtifactPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Trust line classification requires readiness gate references to match the evidence artifacts.");
        }

        if (string.IsNullOrWhiteSpace(recomputeResult.MarkdownArtifactPath)
            || string.IsNullOrWhiteSpace(recomputeResult.JsonArtifactPath))
        {
            throw new InvalidOperationException("Trust line classification requires recompute artifact paths.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-0a-trust-line-result-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-0a",
            $"trust-line-result-{resultDate:yyyy-MM-dd}.json");
    }
}
