using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase2RequestKindSliceProofService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly string[] ReviewedRequestKinds =
    [
        "worker",
        "planner",
        "reviewer",
        "repair",
        "retry",
        "failure_recovery",
        "decomposer",
        "operator_readback",
    ];

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase2RequestKindSliceProofService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase2RequestKindSliceProofResult Persist(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenPhase2ManualReviewResolutionResult manualReviewResolutionResult)
    {
        return Persist(paths, candidateResult, manifestResult, manualReviewResolutionResult, candidateResult.ResultDate);
    }

    internal static RuntimeTokenPhase2RequestKindSliceProofResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenPhase2ManualReviewResolutionResult manualReviewResolutionResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(candidateResult, manifestResult, manualReviewResolutionResult, resultDate);

        var surface = manifestResult.SurfaceManifests.Single(item => string.Equals(item.InventoryId, candidateResult.CandidateSurfaceId, StringComparison.Ordinal));
        var reviewLookup = manualReviewResolutionResult.ReviewItems
            .ToDictionary(item => item.InvariantId, StringComparer.Ordinal);
        var matrixEntries = new List<RuntimeTokenPhase2RequestKindSliceProofMatrixEntry>();

        foreach (var invariant in surface.Invariants.OrderBy(item => item.InvariantId, StringComparer.Ordinal))
        {
            reviewLookup.TryGetValue(invariant.InvariantId, out var manualReviewItem);

            foreach (var requestKind in ReviewedRequestKinds)
            {
                matrixEntries.Add(BuildMatrixEntry(surface, invariant, requestKind, manualReviewItem));
            }
        }

        var removedPolicyCriticalCount = matrixEntries.Count(item => item.PolicyCritical && item.RemovedFromRequestKind);
        var missingWorkerManualReviewCount = surface.Invariants.Count(invariant => !reviewLookup.ContainsKey(invariant.InvariantId));
        var blockingReasons = new List<string>();
        if (manualReviewResolutionResult.UnresolvedReviewCount > 0)
        {
            blockingReasons.Add("manual_review_unresolved");
        }

        if (manualReviewResolutionResult.FailCount > 0)
        {
            blockingReasons.Add("manual_review_failed");
        }

        if (removedPolicyCriticalCount > 0)
        {
            blockingReasons.Add("policy_critical_fragment_removed_by_request_kind_slice");
        }

        if (missingWorkerManualReviewCount > 0)
        {
            blockingReasons.Add("worker_scope_manual_review_incomplete");
        }

        var proofAvailable = blockingReasons.Count == 0;
        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2RequestKindSliceProofResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = candidateResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            CandidateMarkdownArtifactPath = candidateResult.MarkdownArtifactPath,
            CandidateJsonArtifactPath = candidateResult.JsonArtifactPath,
            ManifestMarkdownArtifactPath = manifestResult.MarkdownArtifactPath,
            ManifestJsonArtifactPath = manifestResult.JsonArtifactPath,
            ManualReviewResolutionMarkdownArtifactPath = manualReviewResolutionResult.MarkdownArtifactPath,
            ManualReviewResolutionJsonArtifactPath = manualReviewResolutionResult.JsonArtifactPath,
            TargetSurface = candidateResult.CandidateSurfaceId,
            CandidateStrategy = candidateResult.CandidateStrategy,
            CrossKindProofVerdict = proofAvailable
                ? "proof_available_for_worker_only_canary_scope"
                : "proof_blocked",
            CrossKindProofAvailable = proofAvailable,
            CanaryRequestKindAllowlist = ["worker"],
            RequestKindsReviewed = ReviewedRequestKinds,
            PolicyCriticalFragmentCount = surface.Invariants.Count,
            PolicyCriticalFragmentRemovedCount = removedPolicyCriticalCount,
            MatrixEntries = matrixEntries,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            Notes = BuildNotes(candidateResult, surface, proofAvailable),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenPhase2RequestKindSliceProofResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Wrapper Request-kind Slice Proof");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Cross-kind proof verdict: `{result.CrossKindProofVerdict}`");
        builder.AppendLine($"- Cross-kind proof available: `{(result.CrossKindProofAvailable ? "yes" : "no")}`");
        builder.AppendLine($"- Canary request-kind allowlist: `{string.Join(", ", result.CanaryRequestKindAllowlist)}`");
        builder.AppendLine($"- Policy-critical fragment count: `{result.PolicyCriticalFragmentCount}`");
        builder.AppendLine($"- Policy-critical fragment removed count: `{result.PolicyCriticalFragmentRemovedCount}`");
        builder.AppendLine();

        builder.AppendLine("## Matrix");
        builder.AppendLine();
        builder.AppendLine("| Fragment | Request Kind | Status | Removed | Candidate Scope | Manual Review | Blocking |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (var entry in result.MatrixEntries)
        {
            builder.AppendLine($"| `{entry.FragmentId}` | `{entry.RequestKind}` | `{entry.MatrixStatus}` | `{(entry.RemovedFromRequestKind ? "yes" : "no")}` | `{(entry.CandidateScope ? "yes" : "no")}` | `{entry.ManualReviewStatus}` | `{(entry.Blocking ? "yes" : "no")}` |");
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

    private static RuntimeTokenPhase2RequestKindSliceProofMatrixEntry BuildMatrixEntry(
        RuntimeTokenWrapperPolicyInvariantSurfaceManifest surface,
        RuntimeTokenWrapperPolicyInvariantItem invariant,
        string requestKind,
        RuntimeTokenPhase2ManualReviewResolutionItem? manualReviewItem)
    {
        if (string.Equals(requestKind, surface.RequestKind, StringComparison.Ordinal))
        {
            return new RuntimeTokenPhase2RequestKindSliceProofMatrixEntry
            {
                FragmentId = invariant.InvariantId,
                FragmentTitle = invariant.Title,
                RequestKind = requestKind,
                MatrixStatus = "preserved_in_candidate_scope",
                PolicyCritical = surface.PolicyCritical,
                RemovedFromRequestKind = false,
                CandidateScope = true,
                ManualReviewStatus = manualReviewItem?.ReviewResult ?? "not_reviewed",
                Evidence = "Candidate line remains scoped to worker only and preserves this policy-critical fragment inside the offline candidate.",
                Blocking = false,
            };
        }

        var status = string.Equals(requestKind, "operator_readback", StringComparison.Ordinal)
            ? "not_applicable"
            : "out_of_scope_not_removed";
        var evidence = string.Equals(requestKind, "operator_readback", StringComparison.Ordinal)
            ? "Operator readback is outside the wrapper canary scope on this line and no fragment removal is introduced for it."
            : "This candidate is not attached to the request kind; the original wrapper line remains in place and no fragment is removed.";

        return new RuntimeTokenPhase2RequestKindSliceProofMatrixEntry
        {
            FragmentId = invariant.InvariantId,
            FragmentTitle = invariant.Title,
            RequestKind = requestKind,
            MatrixStatus = status,
            PolicyCritical = surface.PolicyCritical,
            RemovedFromRequestKind = false,
            CandidateScope = false,
            ManualReviewStatus = "not_required",
            Evidence = evidence,
            Blocking = false,
        };
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperPolicyInvariantSurfaceManifest surface,
        bool proofAvailable)
    {
        var notes = new List<string>
        {
            $"This proof is limited to the current canary scope allowlist `worker` for `{candidateResult.CandidateSurfaceId}`.",
            "Non-worker request kinds remain on the original wrapper line; this proof does not authorize applying the candidate outside worker scope.",
            $"All `{surface.Invariants.Count}` policy-critical fragments stay preserved in worker scope and are not removed from out-of-scope request kinds."
        };

        if (proofAvailable)
        {
            notes.Add("Cross-kind proof is available for review because the worker-only canary scope leaves non-worker request kinds untouched.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenPhase2ManualReviewResolutionResult manualReviewResolutionResult,
        DateOnly resultDate)
    {
        if (candidateResult.ResultDate != resultDate
            || manifestResult.ResultDate != resultDate
            || manualReviewResolutionResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Request-kind slice proof requires candidate, manifest, and manual review resolution dates to match the requested result date.");
        }

        if (!string.Equals(candidateResult.CohortId, manifestResult.CohortId, StringComparison.Ordinal)
            || !string.Equals(candidateResult.CohortId, manualReviewResolutionResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Request-kind slice proof requires candidate, manifest, and manual review resolution to point at the same frozen cohort.");
        }

        if (!string.Equals(candidateResult.CandidateSurfaceId, manualReviewResolutionResult.TargetSurface, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Request-kind slice proof requires manual review resolution to point at the same wrapper surface.");
        }

        if (!string.Equals(candidateResult.CandidateSurfaceId, "worker:system:$.instructions", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Request-kind slice proof currently supports only the worker system wrapper candidate line.");
        }

        if (!string.Equals(candidateResult.CandidateStrategy, "dedupe_then_request_kind_slice", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Request-kind slice proof requires the dedupe_then_request_kind_slice candidate strategy.");
        }

        if (manualReviewResolutionResult.UnresolvedReviewCount > 0)
        {
            throw new InvalidOperationException("Request-kind slice proof requires manual review resolution to clear unresolved review items first.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-wrapper-request-kind-slice-proof-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"wrapper-request-kind-slice-proof-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }
}
