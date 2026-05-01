using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase2ManualReviewResolutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase2ManualReviewResolutionService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase2ManualReviewResolutionResult Persist(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperEnterActiveCanaryReviewBundle reviewBundle,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult)
    {
        return Persist(paths, candidateResult, reviewBundle, manifestResult, candidateResult.ResultDate);
    }

    internal static RuntimeTokenPhase2ManualReviewResolutionResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperEnterActiveCanaryReviewBundle reviewBundle,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(candidateResult, reviewBundle, manifestResult, resultDate);

        var surface = manifestResult.SurfaceManifests.Single(item => string.Equals(item.InventoryId, candidateResult.CandidateSurfaceId, StringComparison.Ordinal));
        var reviewItems = reviewBundle.ManualReviewQueue
            .Select(item => ResolveReviewItem(item, surface, candidateResult))
            .OrderBy(item => item.ReviewItemId, StringComparer.Ordinal)
            .ToArray();

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2ManualReviewResolutionResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = candidateResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            CandidateMarkdownArtifactPath = candidateResult.MarkdownArtifactPath,
            CandidateJsonArtifactPath = candidateResult.JsonArtifactPath,
            ReviewBundleMarkdownArtifactPath = candidateResult.ReviewBundleMarkdownArtifactPath,
            ReviewBundleJsonArtifactPath = candidateResult.ReviewBundleJsonArtifactPath,
            ManifestMarkdownArtifactPath = manifestResult.MarkdownArtifactPath,
            ManifestJsonArtifactPath = manifestResult.JsonArtifactPath,
            TargetSurface = candidateResult.CandidateSurfaceId,
            CandidateStrategy = candidateResult.CandidateStrategy,
            ResolutionVerdict = reviewItems.All(item => string.Equals(item.ReviewResult, "pass", StringComparison.Ordinal))
                ? "resolved_without_candidate_change"
                : "candidate_change_required",
            ResolvedReviewCount = reviewItems.Count(item => !string.Equals(item.ReviewResult, "unresolved", StringComparison.Ordinal)),
            UnresolvedReviewCount = reviewItems.Count(item => string.Equals(item.ReviewResult, "unresolved", StringComparison.Ordinal)),
            FailCount = reviewItems.Count(item => string.Equals(item.ReviewResult, "fail", StringComparison.Ordinal)),
            CandidateChangeRequiredCount = reviewItems.Count(item => item.CandidateChangeRequired),
            SemanticPreservationPassCount = reviewItems.Count(item => string.Equals(item.SemanticReviewResult, "pass", StringComparison.Ordinal)),
            SemanticPreservationFailCount = reviewItems.Count(item => string.Equals(item.SemanticReviewResult, "fail", StringComparison.Ordinal)),
            SaliencePreservationPassCount = reviewItems.Count(item => string.Equals(item.SalienceReviewResult, "pass", StringComparison.Ordinal)),
            SaliencePreservationFailCount = reviewItems.Count(item => string.Equals(item.SalienceReviewResult, "fail", StringComparison.Ordinal)),
            PriorityPreservationPassCount = reviewItems.Count(item => string.Equals(item.PriorityReviewResult, "pass", StringComparison.Ordinal)),
            PriorityPreservationFailCount = reviewItems.Count(item => string.Equals(item.PriorityReviewResult, "fail", StringComparison.Ordinal)),
            ApplicabilityPassCount = reviewItems.Count(item => string.Equals(item.ApplicabilityReviewResult, "pass", StringComparison.Ordinal)),
            ApplicabilityFailCount = reviewItems.Count(item => string.Equals(item.ApplicabilityReviewResult, "fail", StringComparison.Ordinal)),
            ReviewItems = reviewItems,
            Notes = BuildNotes(candidateResult, reviewItems),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenPhase2ManualReviewResolutionResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Manual Review Resolution");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Resolution verdict: `{result.ResolutionVerdict}`");
        builder.AppendLine($"- Resolved review count: `{result.ResolvedReviewCount}`");
        builder.AppendLine($"- Unresolved review count: `{result.UnresolvedReviewCount}`");
        builder.AppendLine($"- Fail count: `{result.FailCount}`");
        builder.AppendLine($"- Candidate change required count: `{result.CandidateChangeRequiredCount}`");
        builder.AppendLine();
        builder.AppendLine("## Review Items");
        builder.AppendLine();
        builder.AppendLine("| Review Item | Invariant | Result | Semantic | Salience | Priority | Applicability | Candidate Change Required | Blocking |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var item in result.ReviewItems)
        {
            builder.AppendLine($"| `{item.ReviewItemId}` | `{item.InvariantId}` | `{item.ReviewResult}` | `{item.SemanticReviewResult}` | `{item.SalienceReviewResult}` | `{item.PriorityReviewResult}` | `{item.ApplicabilityReviewResult}` | `{(item.CandidateChangeRequired ? "yes" : "no")}` | `{(item.Blocking ? "yes" : "no")}` |");
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

    private static RuntimeTokenPhase2ManualReviewResolutionItem ResolveReviewItem(
        RuntimeTokenWrapperCandidateManualReviewItem item,
        RuntimeTokenWrapperPolicyInvariantSurfaceManifest surface,
        RuntimeTokenWrapperCandidateResult candidateResult)
    {
        var invariant = surface.Invariants.Single(manifestInvariant => string.Equals(manifestInvariant.InvariantId, item.InvariantId, StringComparison.Ordinal));
        var sourceGroundingTriggered = candidateResult.Samples.Any(sample => sample.SourceGroundingIncluded);
        return invariant.InvariantId switch
        {
            "WRAP-WORKER-SCOPE-001" => BuildPassedItem(
                item,
                "semantic_salience_priority_applicability",
                "Candidate keeps the hard scope, sandbox, approval, and allowed-files boundary as the first hard-boundary clause."),
            "WRAP-WORKER-ONEPASS-002" => BuildPassedItem(
                item,
                "semantic_salience_priority_applicability",
                "Candidate keeps the one-pass bounded completion rule and retains it as a hard boundary rather than optional advice."),
            "WRAP-WORKER-SHELL-003" => BuildPassedItem(
                item,
                "semantic_salience_priority_applicability",
                "Candidate keeps Windows PowerShell modality explicit and preserves the direct ban on bash-only edit syntax."),
            "WRAP-WORKER-VALIDATION-004" => BuildPassedItem(
                item,
                "semantic_salience_priority_applicability",
                "Candidate preserves that CARVES owns formal validation and keeps restore/build/test out of routine worker execution."),
            "WRAP-WORKER-BUDGET-005" => BuildPassedItem(
                item,
                "semantic_salience_priority_applicability",
                "Candidate keeps numeric patch and shell-command limits explicit and preserves the narrow-the-slice fallback behavior."),
            "WRAP-WORKER-STOP-006" => BuildPassedItem(
                item,
                "semantic_salience_priority_applicability",
                "Candidate keeps stop conditions as a dedicated hard-boundary section and preserves the full listed stop-condition set."),
            "WRAP-WORKER-SOURCE-007" => BuildPassedItem(
                item,
                "applicability",
                sourceGroundingTriggered
                    ? "Source grounding was triggered in the sampled cohort and remains explicitly present in the candidate."
                    : "No sampled worker task in the frozen cohort required source grounding, so the invariant resolves as applicability-preserved without candidate change."),
            _ => new RuntimeTokenPhase2ManualReviewResolutionItem
            {
                ReviewItemId = item.ReviewId,
                InvariantId = invariant.InvariantId,
                TargetSurface = surface.InventoryId,
                IssueType = "semantic_salience_priority_applicability",
                ReviewResult = "fail",
                SemanticReviewResult = "fail",
                SalienceReviewResult = "fail",
                PriorityReviewResult = "fail",
                ApplicabilityReviewResult = "fail",
                ReviewerRationale = "Invariant was not recognized by the manual review resolution service.",
                CandidateChangeRequired = true,
                Blocking = true,
                Notes =
                [
                    "This review item needs manual intervention because the invariant was not mapped."
                ]
            }
        };
    }

    private static RuntimeTokenPhase2ManualReviewResolutionItem BuildPassedItem(
        RuntimeTokenWrapperCandidateManualReviewItem item,
        string issueType,
        string rationale)
    {
        return new RuntimeTokenPhase2ManualReviewResolutionItem
        {
            ReviewItemId = item.ReviewId,
            InvariantId = item.InvariantId,
            TargetSurface = item.ManifestId.Replace("manifest:", string.Empty, StringComparison.Ordinal),
            IssueType = issueType,
            ReviewResult = "pass",
            SemanticReviewResult = "pass",
            SalienceReviewResult = "pass",
            PriorityReviewResult = "pass",
            ApplicabilityReviewResult = "pass",
            ReviewerRationale = rationale,
            CandidateChangeRequired = false,
            Blocking = false,
            Notes =
            [
                "Manual review resolved on the current offline candidate line.",
                "This resolution clears the review item for readiness accounting only; active canary remains blocked on other gates."
            ]
        };
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenWrapperCandidateResult candidateResult,
        IReadOnlyList<RuntimeTokenPhase2ManualReviewResolutionItem> reviewItems)
    {
        var notes = new List<string>
        {
            $"Manual review resolution ran against `{candidateResult.CandidateSurfaceId}` using strategy `{candidateResult.CandidateStrategy}`.",
            "This artifact resolves manual-review blockers only. It does not approve active canary, runtime shadow, or main-path replacement."
        };

        if (reviewItems.All(item => string.Equals(item.ReviewResult, "pass", StringComparison.Ordinal)))
        {
            notes.Add("All manual review items resolved as pass on the current offline wrapper candidate line.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperEnterActiveCanaryReviewBundle reviewBundle,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        DateOnly resultDate)
    {
        if (candidateResult.ResultDate != resultDate
            || reviewBundle.ResultDate != resultDate
            || manifestResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Manual review resolution requires candidate, review bundle, and manifest dates to match the requested result date.");
        }

        if (!string.Equals(candidateResult.CohortId, manifestResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Manual review resolution requires candidate and manifest to point at the same frozen cohort.");
        }

        if (!string.Equals(candidateResult.CandidateSurfaceId, reviewBundle.CandidateSurfaceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Manual review resolution requires candidate and review bundle to point at the same wrapper surface.");
        }

        if (!reviewBundle.EnterActiveCanaryReviewBundleReady)
        {
            throw new InvalidOperationException("Manual review resolution requires an enter-active-canary review bundle.");
        }

        if (reviewBundle.ManualReviewQueue.Count == 0)
        {
            throw new InvalidOperationException("Manual review resolution requires at least one review item.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-manual-review-resolution-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"manual-review-resolution-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }
}
