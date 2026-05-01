using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenBaselineRecomputeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly RuntimeTokenBaselineEvidenceResultFormatterService formatterService;
    private readonly RuntimeTokenBaselineReadinessGateService readinessGateService;
    private readonly RuntimeTokenBaselineTrustLineService trustLineService;

    public RuntimeTokenBaselineRecomputeService(
        ControlPlanePaths paths,
        RuntimeTokenBaselineEvidenceResultFormatterService formatterService,
        RuntimeTokenBaselineReadinessGateService readinessGateService,
        RuntimeTokenBaselineTrustLineService trustLineService)
    {
        this.paths = paths;
        this.formatterService = formatterService;
        this.readinessGateService = readinessGateService;
        this.trustLineService = trustLineService;
    }

    public RuntimeTokenBaselineRecomputeResult Persist(RuntimeTokenBaselineCohortFreeze cohort, DateOnly resultDate)
    {
        RuntimeTokenBaselineAggregatorService.Validate(cohort);
        var evidenceResult = formatterService.Persist(cohort, resultDate);
        var readinessGateResult = readinessGateService.Persist(evidenceResult);
        var draftResult = BuildDraftResult(paths, cohort, evidenceResult, readinessGateResult, resultDate);
        PersistArtifacts(paths, cohort, draftResult);
        var trustLineResult = trustLineService.Persist(evidenceResult, readinessGateResult, draftResult);
        var finalResult = Persist(paths, cohort, evidenceResult, readinessGateResult, trustLineResult, resultDate, draftResult.RecomputedAtUtc);
        return finalResult;
    }

    internal static RuntimeTokenBaselineRecomputeResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenBaselineCohortFreeze cohort,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineReadinessGateResult readinessGateResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        DateOnly resultDate,
        DateTimeOffset? recomputedAtUtc = null)
    {
        ValidateInputs(cohort, evidenceResult, readinessGateResult, trustLineResult);

        var additionalCollectionReasons = BuildAdditionalCollectionReasons(readinessGateResult);
        var result = new RuntimeTokenBaselineRecomputeResult
        {
            ResultDate = resultDate,
            RecomputedAtUtc = recomputedAtUtc ?? DateTimeOffset.UtcNow,
            RecomputeMode = "recomputed_from_raw_records",
            CohortJsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, GetCohortJsonArtifactPath(paths, cohort, resultDate)),
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, GetMarkdownArtifactPath(paths, resultDate)),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, GetJsonArtifactPath(paths, resultDate)),
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            ReadinessMarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineReadinessGateService.GetMarkdownArtifactPathFor(paths, resultDate)),
            ReadinessJsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineReadinessGateService.GetJsonArtifactPathFor(paths, resultDate)),
            TrustMarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineTrustLineService.GetMarkdownArtifactPathFor(paths, resultDate)),
            TrustJsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineTrustLineService.GetJsonArtifactPathFor(paths, resultDate)),
            Cohort = cohort,
            ReadinessVerdict = readinessGateResult.Verdict,
            Phase10TargetDecisionAllowed = trustLineResult.Phase10TargetDecisionMayReferenceThisLine,
            RecommendationDecision = evidenceResult.Recommendation.Decision,
            RecommendationNextTrack = evidenceResult.Recommendation.NextTrack,
            TrustLineClassification = trustLineResult.TrustLineClassification,
            SupersedesPreLedgerLine = trustLineResult.SupersedesPreLedgerLine,
            CapBasedTargetDecisionAllowed = trustLineResult.CapBasedTargetDecisionAllowed,
            TotalCostClaimAllowed = trustLineResult.TotalCostClaimAllowed,
            AdditionalCollectionRecommended = additionalCollectionReasons.Length > 0,
            AdditionalCollectionReasons = additionalCollectionReasons,
            Notes = BuildNotes(trustLineResult, evidenceResult, additionalCollectionReasons),
        };

        PersistArtifacts(paths, cohort, result);
        return result;
    }

    private static RuntimeTokenBaselineRecomputeResult BuildDraftResult(
        ControlPlanePaths paths,
        RuntimeTokenBaselineCohortFreeze cohort,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineReadinessGateResult readinessGateResult,
        DateOnly resultDate,
        DateTimeOffset? recomputedAtUtc = null)
    {
        ValidateInputs(cohort, evidenceResult, readinessGateResult);
        var additionalCollectionReasons = BuildAdditionalCollectionReasons(readinessGateResult);
        return new RuntimeTokenBaselineRecomputeResult
        {
            ResultDate = resultDate,
            RecomputedAtUtc = recomputedAtUtc ?? DateTimeOffset.UtcNow,
            RecomputeMode = "recomputed_from_raw_records",
            CohortJsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, GetCohortJsonArtifactPath(paths, cohort, resultDate)),
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, GetMarkdownArtifactPath(paths, resultDate)),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, GetJsonArtifactPath(paths, resultDate)),
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            ReadinessMarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineReadinessGateService.GetMarkdownArtifactPathFor(paths, resultDate)),
            ReadinessJsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineReadinessGateService.GetJsonArtifactPathFor(paths, resultDate)),
            Cohort = cohort,
            ReadinessVerdict = readinessGateResult.Verdict,
            Phase10TargetDecisionAllowed = readinessGateResult.UnlocksPhase10TargetDecision,
            RecommendationDecision = evidenceResult.Recommendation.Decision,
            RecommendationNextTrack = evidenceResult.Recommendation.NextTrack,
            AdditionalCollectionRecommended = additionalCollectionReasons.Length > 0,
            AdditionalCollectionReasons = additionalCollectionReasons,
            Notes = BuildDraftNotes(readinessGateResult, evidenceResult, additionalCollectionReasons),
        };
    }

    internal static string FormatMarkdown(RuntimeTokenBaselineRecomputeResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 0A Ledger Recompute Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Recomputed at: `{result.RecomputedAtUtc:O}`");
        builder.AppendLine($"- Recompute mode: `{result.RecomputeMode}`");
        builder.AppendLine($"- Cohort: `{result.Cohort.CohortId}`");
        builder.AppendLine($"- Readiness verdict: `{result.ReadinessVerdict}`");
        builder.AppendLine($"- Trust line classification: `{result.TrustLineClassification}`");
        builder.AppendLine($"- Supersedes pre-ledger line: `{(result.SupersedesPreLedgerLine ? "yes" : "no")}`");
        builder.AppendLine($"- Phase 1.0 target decision allowed: `{(result.Phase10TargetDecisionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Cap-based target decision allowed: `{(result.CapBasedTargetDecisionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Total cost claim allowed: `{(result.TotalCostClaimAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Recommendation decision: `{result.RecommendationDecision}`");
        builder.AppendLine($"- Recommendation next track: `{result.RecommendationNextTrack}`");
        builder.AppendLine($"- Additional collection recommended: `{(result.AdditionalCollectionRecommended ? "yes" : "no")}`");
        builder.AppendLine($"- Cohort json artifact: `{result.CohortJsonArtifactPath}`");
        builder.AppendLine($"- Evidence markdown artifact: `{result.EvidenceMarkdownArtifactPath}`");
        builder.AppendLine($"- Evidence json artifact: `{result.EvidenceJsonArtifactPath}`");
        builder.AppendLine($"- Readiness markdown artifact: `{result.ReadinessMarkdownArtifactPath}`");
        builder.AppendLine($"- Readiness json artifact: `{result.ReadinessJsonArtifactPath}`");
        builder.AppendLine($"- Trust markdown artifact: `{result.TrustMarkdownArtifactPath}`");
        builder.AppendLine($"- Trust json artifact: `{result.TrustJsonArtifactPath}`");
        builder.AppendLine();
        builder.AppendLine("## Additional Collection Reasons");
        builder.AppendLine();
        if (result.AdditionalCollectionReasons.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var reason in result.AdditionalCollectionReasons)
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

    private static string[] BuildAdditionalCollectionReasons(RuntimeTokenBaselineReadinessGateResult readinessGateResult)
    {
        return readinessGateResult.Readiness.TaskCostBlockingReasons
            .Concat(readinessGateResult.Readiness.RouteReinjectionBlockingReasons)
            .Concat(readinessGateResult.Readiness.CapTruthBlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildDraftNotes(
        RuntimeTokenBaselineReadinessGateResult readinessGateResult,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        IReadOnlyList<string> additionalCollectionReasons)
    {
        var notes = new List<string>();
        if (!readinessGateResult.UnlocksPhase10TargetDecision)
        {
            notes.Add("This recompute line does not yet unlock Phase 1.0 target decision.");
        }

        if (additionalCollectionReasons.Count > 0)
        {
            notes.Add($"Additional collection remains recommended for: {string.Join(", ", additionalCollectionReasons)}.");
        }

        if (string.Equals(evidenceResult.Recommendation.Decision, "insufficient_data", StringComparison.Ordinal))
        {
            notes.Add("Recommendation is still insufficient_data after recompute.");
        }

        return notes;
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        IReadOnlyList<string> additionalCollectionReasons)
    {
        var notes = new List<string>();
        if (!trustLineResult.Phase10TargetDecisionMayReferenceThisLine)
        {
            notes.Add("This recompute line does not yet unlock Phase 1.0 target decision.");
        }

        if (additionalCollectionReasons.Count > 0)
        {
            notes.Add($"Additional collection remains recommended for: {string.Join(", ", additionalCollectionReasons)}.");
        }

        if (string.Equals(evidenceResult.Recommendation.Decision, "insufficient_data", StringComparison.Ordinal))
        {
            notes.Add("Recommendation is still insufficient_data after recompute.");
        }

        if (!trustLineResult.CapBasedTargetDecisionAllowed)
        {
            notes.Add("Cap-based dominance remains blocked on the recomputed trust line.");
        }

        if (!trustLineResult.TotalCostClaimAllowed)
        {
            notes.Add("Authoritative total-cost claims remain blocked on the recomputed trust line.");
        }

        return notes;
    }

    private static void PersistArtifacts(
        ControlPlanePaths paths,
        RuntimeTokenBaselineCohortFreeze cohort,
        RuntimeTokenBaselineRecomputeResult result)
    {
        var cohortJsonPath = Path.Combine(paths.RepoRoot, result.CohortJsonArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        var markdownPath = Path.Combine(paths.RepoRoot, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        var jsonPath = Path.Combine(paths.RepoRoot, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(cohortJsonPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(cohortJsonPath, JsonSerializer.Serialize(cohort, JsonOptions));
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
    }

    private static void ValidateInputs(
        RuntimeTokenBaselineCohortFreeze cohort,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineReadinessGateResult readinessGateResult)
    {
        RuntimeTokenBaselineAggregatorService.Validate(cohort);
        if (!string.Equals(cohort.CohortId, evidenceResult.Aggregation.Cohort.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Baseline recompute requires evidence result to match the frozen cohort id.");
        }

        if (string.IsNullOrWhiteSpace(evidenceResult.MarkdownArtifactPath)
            || string.IsNullOrWhiteSpace(evidenceResult.JsonArtifactPath))
        {
            throw new InvalidOperationException("Baseline recompute requires evidence result artifact paths.");
        }

        if (string.IsNullOrWhiteSpace(readinessGateResult.EvidenceMarkdownArtifactPath)
            || string.IsNullOrWhiteSpace(readinessGateResult.EvidenceJsonArtifactPath))
        {
            throw new InvalidOperationException("Baseline recompute requires readiness gate evidence artifact references.");
        }
    }

    private static void ValidateInputs(
        RuntimeTokenBaselineCohortFreeze cohort,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineReadinessGateResult readinessGateResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult)
    {
        ValidateInputs(cohort, evidenceResult, readinessGateResult);

        if (!string.Equals(cohort.CohortId, trustLineResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Baseline recompute requires trust line result to match the frozen cohort id.");
        }
    }

    private static string GetCohortJsonArtifactPath(ControlPlanePaths paths, RuntimeTokenBaselineCohortFreeze cohort, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-0a",
            $"cohort-freeze-{SanitizeForFileName(cohort.CohortId)}-{resultDate:yyyy-MM-dd}.json");
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-0a-ledger-recompute-result-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-0a",
            $"ledger-recompute-result-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string absolutePath)
    {
        var relative = Path.GetRelativePath(repoRoot, absolutePath);
        return relative.Replace('\\', '/');
    }

    private static string SanitizeForFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(Array.IndexOf(invalid, character) >= 0 ? '-' : character);
        }

        return builder.ToString();
    }
}
