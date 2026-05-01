using System.Globalization;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase2ActiveCanaryReadinessReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase2ActiveCanaryReadinessReviewService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase2ActiveCanaryReadinessReviewResult Persist(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperEnterActiveCanaryReviewBundle reviewBundle,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenPhase2ManualReviewResolutionResult? manualReviewResolutionResult = null,
        RuntimeTokenPhase2RequestKindSliceProofResult? requestKindSliceProofResult = null,
        RuntimeTokenPhase2RollbackPlanFreezeResult? rollbackPlanFreezeResult = null,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult? nonInferiorityCohortFreezeResult = null)
    {
        return Persist(paths, candidateResult, reviewBundle, manifestResult, manualReviewResolutionResult, requestKindSliceProofResult, rollbackPlanFreezeResult, nonInferiorityCohortFreezeResult, candidateResult.ResultDate);
    }

    internal static RuntimeTokenPhase2ActiveCanaryReadinessReviewResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperEnterActiveCanaryReviewBundle reviewBundle,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenPhase2ManualReviewResolutionResult? manualReviewResolutionResult,
        RuntimeTokenPhase2RequestKindSliceProofResult? requestKindSliceProofResult,
        RuntimeTokenPhase2RollbackPlanFreezeResult? rollbackPlanFreezeResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult? nonInferiorityCohortFreezeResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(candidateResult, reviewBundle, manifestResult, manualReviewResolutionResult, requestKindSliceProofResult, rollbackPlanFreezeResult, nonInferiorityCohortFreezeResult, resultDate);

        var surface = manifestResult.SurfaceManifests.Single(item => string.Equals(item.InventoryId, candidateResult.CandidateSurfaceId, StringComparison.Ordinal));
        var policyInvariantCount = surface.Invariants.Count;
        var policyInvariantCoverageCount = candidateResult.InvariantCoveragePass ? policyInvariantCount : 0;
        var policyInvariantCoverageRatio = policyInvariantCount == 0 ? 0d : (double)policyInvariantCoverageCount / policyInvariantCount;
        var semanticPreservationFailCount = manualReviewResolutionResult?.SemanticPreservationFailCount
                                            ?? (candidateResult.SemanticPreservationPass ? 0 : policyInvariantCount);
        var saliencePreservationFailCount = manualReviewResolutionResult?.SaliencePreservationFailCount
                                            ?? (candidateResult.SaliencePreservationPass ? 0 : policyInvariantCount);
        var priorityPreservationFailCount = manualReviewResolutionResult?.PriorityPreservationFailCount
                                            ?? (candidateResult.PriorityPreservationPass ? 0 : policyInvariantCount);
        var unresolvedManualReviewCount = manualReviewResolutionResult?.UnresolvedReviewCount
                                          ?? candidateResult.ManualReviewQueue.Count(item =>
                                              !string.Equals(item.ReviewStatus, "resolved", StringComparison.Ordinal)
                                              && !string.Equals(item.ReviewStatus, "approved", StringComparison.Ordinal));
        var requestKindSliceCrossKindProofAvailable = requestKindSliceProofResult?.CrossKindProofAvailable
                                                      ?? manifestResult.RequestKindsCovered.Count > 1;
        var requestKindSliceRemovedPolicyCriticalCount = requestKindSliceProofResult?.PolicyCriticalFragmentRemovedCount ?? 0;
        var blockingReasons = new List<string>();

        if (!candidateResult.EnterActiveCanaryReviewBundleReady)
        {
            blockingReasons.Add("review_bundle_not_ready");
        }

        if (!candidateResult.MaterialReductionPass)
        {
            blockingReasons.Add("material_reduction_not_met");
        }

        if (!candidateResult.SchemaValidityPass)
        {
            blockingReasons.Add("schema_validity_failed");
        }

        if (!candidateResult.InvariantCoveragePass || policyInvariantCoverageCount != policyInvariantCount)
        {
            blockingReasons.Add("policy_invariant_coverage_incomplete");
        }

        if (semanticPreservationFailCount > 0)
        {
            blockingReasons.Add("semantic_preservation_failed");
        }

        if (saliencePreservationFailCount > 0)
        {
            blockingReasons.Add("salience_preservation_failed");
        }

        if (priorityPreservationFailCount > 0)
        {
            blockingReasons.Add("priority_preservation_failed");
        }

        if (unresolvedManualReviewCount > 0)
        {
            blockingReasons.Add("manual_review_unresolved");
        }

        if (!requestKindSliceCrossKindProofAvailable)
        {
            blockingReasons.Add("request_kind_slice_cross_kind_proof_not_available");
        }

        if (requestKindSliceRemovedPolicyCriticalCount > 0)
        {
            blockingReasons.Add("request_kind_slice_removed_policy_critical_fragment");
        }

        if (!(rollbackPlanFreezeResult?.RollbackPlanReviewed ?? false))
        {
            blockingReasons.Add("rollback_plan_not_reviewed");
        }
        if (!(nonInferiorityCohortFreezeResult?.NonInferiorityCohortFrozen ?? false))
        {
            blockingReasons.Add("non_inferiority_cohort_not_frozen");
        }

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2ActiveCanaryReadinessReviewResult
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
            ManualReviewResolutionMarkdownArtifactPath = manualReviewResolutionResult?.MarkdownArtifactPath ?? string.Empty,
            ManualReviewResolutionJsonArtifactPath = manualReviewResolutionResult?.JsonArtifactPath ?? string.Empty,
            RequestKindSliceProofMarkdownArtifactPath = requestKindSliceProofResult?.MarkdownArtifactPath ?? string.Empty,
            RequestKindSliceProofJsonArtifactPath = requestKindSliceProofResult?.JsonArtifactPath ?? string.Empty,
            RollbackPlanMarkdownArtifactPath = rollbackPlanFreezeResult?.MarkdownArtifactPath ?? string.Empty,
            RollbackPlanJsonArtifactPath = rollbackPlanFreezeResult?.JsonArtifactPath ?? string.Empty,
            NonInferiorityCohortMarkdownArtifactPath = nonInferiorityCohortFreezeResult?.MarkdownArtifactPath ?? string.Empty,
            NonInferiorityCohortJsonArtifactPath = nonInferiorityCohortFreezeResult?.JsonArtifactPath ?? string.Empty,
            TrustLineClassification = candidateResult.TrustLineClassification,
            Phase10Decision = candidateResult.Phase10Decision,
            Phase10NextTrack = candidateResult.Phase10NextTrack,
            TargetSurface = candidateResult.CandidateSurfaceId,
            CandidateStrategy = candidateResult.CandidateStrategy,
            ReviewVerdict = candidateResult.EnterActiveCanaryReviewBundleReady ? "accepted_for_review_only" : "blocked_before_review",
            EnterActiveCanaryReviewAccepted = candidateResult.EnterActiveCanaryReviewBundleReady,
            ActiveCanaryApproved = false,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            TargetSurfaceReductionRatioP95 = candidateResult.ReductionRatioP95,
            TargetSurfaceShareP95 = surface.ShareP95,
            ExpectedWholeRequestReductionP95 = candidateResult.ReductionRatioP95 * surface.ShareP95,
            PolicyInvariantCount = policyInvariantCount,
            PolicyInvariantCoverageCount = policyInvariantCoverageCount,
            PolicyInvariantCoverageRatio = policyInvariantCoverageRatio,
            SemanticPreservationFailCount = semanticPreservationFailCount,
            SaliencePreservationFailCount = saliencePreservationFailCount,
            PriorityPreservationFailCount = priorityPreservationFailCount,
            NeedsManualReviewUnresolvedCount = unresolvedManualReviewCount,
            RequestKindSliceRemovedPolicyCriticalCount = requestKindSliceRemovedPolicyCriticalCount,
            RequestKindSliceCrossKindProofAvailable = requestKindSliceCrossKindProofAvailable,
            RuntimePathTouched = false,
            RetrievalOrEvidenceWritten = false,
            NonInferiorityCohortFrozen = nonInferiorityCohortFreezeResult?.NonInferiorityCohortFrozen ?? false,
            RollbackPlanReviewed = rollbackPlanFreezeResult?.RollbackPlanReviewed ?? false,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            RequiredBeforeActiveCanary =
            [
                "policy invariant coverage 100%",
                "semantic/salience/priority preservation all pass",
                "manual review queue resolved",
                "expected whole-request reduction reviewed",
                "request-kind slicing proof reviewed",
                "rollback plan reviewed",
                "non-inferiority cohort frozen"
            ],
            Notes = BuildNotes(candidateResult, surface, unresolvedManualReviewCount, requestKindSliceCrossKindProofAvailable, requestKindSliceProofResult, rollbackPlanFreezeResult, nonInferiorityCohortFreezeResult),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenPhase2ActiveCanaryReadinessReviewResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Active Canary Readiness Review");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Review verdict: `{result.ReviewVerdict}`");
        builder.AppendLine($"- Enter-active-canary review accepted: `{(result.EnterActiveCanaryReviewAccepted ? "yes" : "no")}`");
        builder.AppendLine($"- Active canary approved: `{(result.ActiveCanaryApproved ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(result.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Main renderer replacement allowed: `{(result.MainRendererReplacementAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Phase 1.0 decision: `{result.Phase10Decision}`");
        builder.AppendLine($"- Phase 1.0 next track: `{result.Phase10NextTrack}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        if (!string.IsNullOrWhiteSpace(result.ManualReviewResolutionMarkdownArtifactPath))
        {
            builder.AppendLine($"- Manual review resolution markdown artifact: `{result.ManualReviewResolutionMarkdownArtifactPath}`");
        }

        if (!string.IsNullOrWhiteSpace(result.ManualReviewResolutionJsonArtifactPath))
        {
            builder.AppendLine($"- Manual review resolution json artifact: `{result.ManualReviewResolutionJsonArtifactPath}`");
        }

        if (!string.IsNullOrWhiteSpace(result.RequestKindSliceProofMarkdownArtifactPath))
        {
            builder.AppendLine($"- Request-kind slice proof markdown artifact: `{result.RequestKindSliceProofMarkdownArtifactPath}`");
        }

        if (!string.IsNullOrWhiteSpace(result.RequestKindSliceProofJsonArtifactPath))
        {
            builder.AppendLine($"- Request-kind slice proof json artifact: `{result.RequestKindSliceProofJsonArtifactPath}`");
        }

        if (!string.IsNullOrWhiteSpace(result.RollbackPlanMarkdownArtifactPath))
        {
            builder.AppendLine($"- Rollback plan markdown artifact: `{result.RollbackPlanMarkdownArtifactPath}`");
        }

        if (!string.IsNullOrWhiteSpace(result.RollbackPlanJsonArtifactPath))
        {
            builder.AppendLine($"- Rollback plan json artifact: `{result.RollbackPlanJsonArtifactPath}`");
        }

        if (!string.IsNullOrWhiteSpace(result.NonInferiorityCohortMarkdownArtifactPath))
        {
            builder.AppendLine($"- Non-inferiority cohort markdown artifact: `{result.NonInferiorityCohortMarkdownArtifactPath}`");
        }

        if (!string.IsNullOrWhiteSpace(result.NonInferiorityCohortJsonArtifactPath))
        {
            builder.AppendLine($"- Non-inferiority cohort json artifact: `{result.NonInferiorityCohortJsonArtifactPath}`");
        }
        builder.AppendLine();

        builder.AppendLine("## Reduction");
        builder.AppendLine();
        builder.AppendLine($"- Target surface reduction ratio p95: `{FormatRatio(result.TargetSurfaceReductionRatioP95)}`");
        builder.AppendLine($"- Target surface share of request p95: `{FormatRatio(result.TargetSurfaceShareP95)}`");
        builder.AppendLine($"- Expected whole-request reduction p95: `{FormatRatio(result.ExpectedWholeRequestReductionP95)}`");
        builder.AppendLine();

        builder.AppendLine("## Policy Checks");
        builder.AppendLine();
        builder.AppendLine($"- Policy invariant coverage: `{result.PolicyInvariantCoverageCount}/{result.PolicyInvariantCount}` (`{FormatRatio(result.PolicyInvariantCoverageRatio)}`)");
        builder.AppendLine($"- Semantic preservation fail count: `{result.SemanticPreservationFailCount}`");
        builder.AppendLine($"- Salience preservation fail count: `{result.SaliencePreservationFailCount}`");
        builder.AppendLine($"- Priority preservation fail count: `{result.PriorityPreservationFailCount}`");
        builder.AppendLine($"- Unresolved manual review count: `{result.NeedsManualReviewUnresolvedCount}`");
        builder.AppendLine($"- Request-kind slice removed policy-critical count: `{result.RequestKindSliceRemovedPolicyCriticalCount}`");
        builder.AppendLine($"- Cross-kind slice proof available: `{(result.RequestKindSliceCrossKindProofAvailable ? "yes" : "no")}`");
        builder.AppendLine();

        builder.AppendLine("## Runtime Safety");
        builder.AppendLine();
        builder.AppendLine($"- Runtime path touched: `{(result.RuntimePathTouched ? "yes" : "no")}`");
        builder.AppendLine($"- Retrieval or evidence written: `{(result.RetrievalOrEvidenceWritten ? "yes" : "no")}`");
        builder.AppendLine($"- Non-inferiority cohort frozen: `{(result.NonInferiorityCohortFrozen ? "yes" : "no")}`");
        builder.AppendLine($"- Rollback plan reviewed: `{(result.RollbackPlanReviewed ? "yes" : "no")}`");
        builder.AppendLine();

        builder.AppendLine("## Blocking Reasons");
        builder.AppendLine();
        foreach (var reason in result.BlockingReasons)
        {
            builder.AppendLine($"- `{reason}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Required Before Active Canary");
        builder.AppendLine();
        foreach (var item in result.RequiredBeforeActiveCanary)
        {
            builder.AppendLine($"- {item}");
        }

        if (result.Notes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Notes");
            builder.AppendLine();
            foreach (var note in result.Notes)
            {
                builder.AppendLine($"- {note}");
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperPolicyInvariantSurfaceManifest surface,
        int unresolvedManualReviewCount,
        bool requestKindSliceCrossKindProofAvailable,
        RuntimeTokenPhase2RequestKindSliceProofResult? requestKindSliceProofResult,
        RuntimeTokenPhase2RollbackPlanFreezeResult? rollbackPlanFreezeResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult? nonInferiorityCohortFreezeResult)
    {
        var notes = new List<string>
        {
            "This review accepts the bundle for operator canary review only. It does not approve runtime shadow, active canary, or main-path replacement.",
            $"Current target surface is `{candidateResult.CandidateSurfaceId}` with structural-only strategy `{candidateResult.CandidateStrategy}`.",
            $"Expected whole-request reduction is projected from target-surface share ({surface.ShareP95.ToString("0.000", CultureInfo.InvariantCulture)}) times target-surface reduction ({candidateResult.ReductionRatioP95.ToString("0.000", CultureInfo.InvariantCulture)})."
        };

        if (unresolvedManualReviewCount > 0)
        {
            notes.Add($"Manual review remains open on {unresolvedManualReviewCount} invariant item(s); active canary stays blocked.");
        }

        if (!requestKindSliceCrossKindProofAvailable)
        {
            notes.Add("Current trusted cohort is worker-only, so cross-request-kind slicing proof is not yet available.");
        }
        else if (requestKindSliceProofResult is not null)
        {
            notes.Add($"Cross-request-kind slicing proof is available with canary allowlist `{string.Join(", ", requestKindSliceProofResult.CanaryRequestKindAllowlist)}` and zero removed policy-critical fragments.");
        }

        if (rollbackPlanFreezeResult is not null && rollbackPlanFreezeResult.RollbackPlanReviewed)
        {
            notes.Add($"Rollback plan is frozen with default-off posture, candidate version `{rollbackPlanFreezeResult.CandidateVersion}`, and fallback version `{rollbackPlanFreezeResult.FallbackVersion}`.");
        }

        if (nonInferiorityCohortFreezeResult is not null && nonInferiorityCohortFreezeResult.NonInferiorityCohortFrozen)
        {
            notes.Add($"Non-inferiority cohort is frozen on `{nonInferiorityCohortFreezeResult.TaskIds.Count}` worker task(s) with provider/model/tokenizer `{nonInferiorityCohortFreezeResult.Provider}` / `{nonInferiorityCohortFreezeResult.Model}` / `{nonInferiorityCohortFreezeResult.Tokenizer}`.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenWrapperEnterActiveCanaryReviewBundle reviewBundle,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenPhase2ManualReviewResolutionResult? manualReviewResolutionResult,
        RuntimeTokenPhase2RequestKindSliceProofResult? requestKindSliceProofResult,
        RuntimeTokenPhase2RollbackPlanFreezeResult? rollbackPlanFreezeResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult? nonInferiorityCohortFreezeResult,
        DateOnly resultDate)
    {
        if (candidateResult.ResultDate != resultDate
            || reviewBundle.ResultDate != resultDate
            || manifestResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Active canary readiness review requires candidate, review bundle, and manifest dates to match the requested result date.");
        }

        if (!string.Equals(candidateResult.CohortId, manifestResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires candidate and manifest to point at the same frozen cohort.");
        }

        if (!string.Equals(candidateResult.CandidateSurfaceId, reviewBundle.CandidateSurfaceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires candidate and review bundle to point at the same wrapper surface.");
        }

        if (!string.Equals(candidateResult.CandidateSurfaceId, "worker:system:$.instructions", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review currently supports only the worker system wrapper candidate line.");
        }

        if (!reviewBundle.EnterActiveCanaryReviewBundleReady)
        {
            throw new InvalidOperationException("Active canary readiness review requires an enter-active-canary review bundle.");
        }

        if (reviewBundle.ActiveCanaryApprovalGranted || candidateResult.ActiveCanaryApprovalGranted)
        {
            throw new InvalidOperationException("Active canary readiness review cannot start from an already approved canary line.");
        }

        if (manualReviewResolutionResult is null)
        {
            if (requestKindSliceProofResult is null && rollbackPlanFreezeResult is null && nonInferiorityCohortFreezeResult is null)
            {
                return;
            }

            throw new InvalidOperationException("Active canary readiness review requires manual review resolution before request-kind slice proof, rollback plan, or non-inferiority cohort can be applied.");
        }

        if (manualReviewResolutionResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Active canary readiness review requires manual review resolution to share the same result date.");
        }

        if (!string.Equals(manualReviewResolutionResult.CohortId, candidateResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires manual review resolution to point at the same frozen cohort.");
        }

        if (!string.Equals(manualReviewResolutionResult.TargetSurface, candidateResult.CandidateSurfaceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires manual review resolution to point at the same wrapper surface.");
        }

        if (requestKindSliceProofResult is null)
        {
            if (rollbackPlanFreezeResult is null && nonInferiorityCohortFreezeResult is null)
            {
                return;
            }

            throw new InvalidOperationException("Active canary readiness review requires request-kind slice proof before rollback plan or non-inferiority cohort can be applied.");
        }

        if (requestKindSliceProofResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Active canary readiness review requires request-kind slice proof to share the same result date.");
        }

        if (!string.Equals(requestKindSliceProofResult.CohortId, candidateResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires request-kind slice proof to point at the same frozen cohort.");
        }

        if (!string.Equals(requestKindSliceProofResult.TargetSurface, candidateResult.CandidateSurfaceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires request-kind slice proof to point at the same wrapper surface.");
        }

        if (rollbackPlanFreezeResult is null)
        {
            if (nonInferiorityCohortFreezeResult is null)
            {
                return;
            }

            throw new InvalidOperationException("Active canary readiness review requires rollback plan before non-inferiority cohort can be applied.");
        }

        if (rollbackPlanFreezeResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Active canary readiness review requires rollback plan to share the same result date.");
        }

        if (!string.Equals(rollbackPlanFreezeResult.CohortId, candidateResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires rollback plan to point at the same frozen cohort.");
        }

        if (!string.Equals(rollbackPlanFreezeResult.TargetSurface, candidateResult.CandidateSurfaceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires rollback plan to point at the same wrapper surface.");
        }

        if (nonInferiorityCohortFreezeResult is null)
        {
            return;
        }

        if (nonInferiorityCohortFreezeResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Active canary readiness review requires non-inferiority cohort to share the same result date.");
        }

        if (!string.Equals(nonInferiorityCohortFreezeResult.CohortId, candidateResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires non-inferiority cohort to point at the same frozen cohort.");
        }

        if (!string.Equals(nonInferiorityCohortFreezeResult.TargetSurface, candidateResult.CandidateSurfaceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Active canary readiness review requires non-inferiority cohort to point at the same wrapper surface.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-active-canary-readiness-review-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"active-canary-readiness-review-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }

    private static string FormatRatio(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
}
