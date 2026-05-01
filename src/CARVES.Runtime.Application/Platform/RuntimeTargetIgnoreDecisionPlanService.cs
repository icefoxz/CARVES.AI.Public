using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeTargetIgnoreDecisionPlanService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md";
    public const string ResiduePolicyDocumentPath = RuntimeTargetResiduePolicyService.PhaseDocumentPath;
    public const string GuideDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeTargetIgnoreDecisionPlanService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeTargetIgnoreDecisionPlanSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 28 target ignore decision plan document", errors);
        ValidateRuntimeDocument(ResiduePolicyDocumentPath, "Product closure Phase 27 external target residue policy document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Productized pilot status guide document", errors);

        var residuePolicy = new RuntimeTargetResiduePolicyService(repoRoot).Build();
        errors.AddRange(residuePolicy.Errors.Select(static error => $"runtime-target-residue-policy: {error}"));

        var existingGitIgnoreEntries = ReadGitIgnoreEntries();
        var candidates = residuePolicy.IgnoreSuggestions
            .Select(suggestion => BuildCandidate(suggestion, existingGitIgnoreEntries))
            .OrderBy(static candidate => candidate.Entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingIgnoreEntries = candidates
            .Where(static candidate => !candidate.AlreadyPresentInGitIgnore)
            .Select(static candidate => candidate.Entry)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var planReady = errors.Count == 0 && residuePolicy.ResiduePolicyReady;
        var ignoreDecisionRequired = planReady && missingIgnoreEntries.Length > 0;

        return new RuntimeTargetIgnoreDecisionPlanSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            ResiduePolicyDocumentPath = ResiduePolicyDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(residuePolicy, planReady, candidates.Length, missingIgnoreEntries.Length, errors.Count),
            IgnoreDecisionPlanId = BuildPlanId(residuePolicy, candidates),
            ResiduePolicyPosture = residuePolicy.OverallPosture,
            CommitClosureComplete = residuePolicy.CommitClosureComplete,
            ResiduePolicyReady = residuePolicy.ResiduePolicyReady,
            ProductProofCanRemainComplete = residuePolicy.ProductProofCanRemainComplete,
            IgnoreDecisionPlanReady = planReady,
            IgnoreDecisionRequired = ignoreDecisionRequired,
            CanKeepResidueLocal = planReady && residuePolicy.CanKeepResidueLocal,
            CanApplyIgnoreAfterReview = planReady && missingIgnoreEntries.Length > 0,
            GitIgnoreExists = File.Exists(Path.Combine(repoRoot, ".gitignore")),
            ResiduePathCount = residuePolicy.ResiduePathCount,
            SuggestedIgnoreEntryCount = residuePolicy.SuggestedIgnoreEntryCount,
            MissingIgnoreEntryCount = missingIgnoreEntries.Length,
            DecisionCandidateCount = candidates.Length,
            ResiduePaths = residuePolicy.ResiduePaths,
            SuggestedIgnoreEntries = residuePolicy.SuggestedIgnoreEntries,
            MissingIgnoreEntries = missingIgnoreEntries,
            GitIgnorePatchPreview = BuildGitIgnorePatchPreview(missingIgnoreEntries).ToArray(),
            DecisionCandidates = candidates,
            OperatorDecisionChecklist =
            [
                "Confirm each matching residue path is local/tooling state and not target product truth.",
                "Choose keep_local when residue may remain uncommitted on this machine.",
                "Choose add_to_gitignore_after_review only when the ignore entry is acceptable for the target repo.",
                "Choose manual_cleanup_after_review when the residue should be removed locally rather than ignored.",
                "If .gitignore is edited, rerun carves pilot commit-plan --json, commit only reviewed target paths, then rerun carves pilot closure --json, carves pilot residue --json, carves pilot ignore-plan --json, and carves pilot proof --json.",
            ],
            BoundaryRules =
            [
                "The default safe decision is keep_local; local/tooling residue is not product truth.",
                "Suggested ignore entries are operator-reviewed candidates, not automatic target policy.",
                "Editing .gitignore is a target repo product decision and must go through commit-plan and commit closure afterward.",
                "This surface is valid when residue policy is ready; it does not require an ignore edit to keep product proof complete.",
            ],
            Gaps = BuildGaps(residuePolicy, planReady).ToArray(),
            Summary = BuildSummary(residuePolicy, planReady, candidates.Length, missingIgnoreEntries.Length, errors.Count),
            RecommendedNextAction = BuildRecommendedNextAction(residuePolicy, planReady, candidates.Length, missingIgnoreEntries.Length, errors.Count),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not write, append, sort, or rewrite .gitignore.",
                "This surface does not stage, commit, clean, delete, push, or release target paths.",
                "This surface does not turn suggested ignore entries into approved target policy without operator review.",
                "This surface does not replace target commit closure, residue policy, product pilot proof, or downstream release policy.",
            ],
        };
    }

    private static RuntimeTargetIgnoreDecisionCandidateSurface BuildCandidate(
        RuntimeTargetResidueIgnoreSuggestionSurface suggestion,
        IReadOnlySet<string> existingGitIgnoreEntries)
    {
        return new RuntimeTargetIgnoreDecisionCandidateSurface
        {
            Entry = suggestion.Entry,
            AlreadyPresentInGitIgnore = existingGitIgnoreEntries.Contains(suggestion.Entry),
            OperatorApprovalRequired = true,
            RecommendedDecision = "operator_review_required",
            DecisionOptions =
            [
                "keep_local",
                "add_to_gitignore_after_review",
                "manual_cleanup_after_review",
            ],
            MatchingPathCount = suggestion.MatchingPathCount,
            MatchingPaths = suggestion.MatchingPaths,
            Reason = suggestion.Reason,
        };
    }

    private IReadOnlySet<string> ReadGitIgnoreEntries()
    {
        var gitIgnorePath = Path.Combine(repoRoot, ".gitignore");
        if (!File.Exists(gitIgnorePath))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var entries = File.ReadAllLines(gitIgnorePath)
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return entries;
    }

    private static IEnumerable<string> BuildGitIgnorePatchPreview(IReadOnlyList<string> missingIgnoreEntries)
    {
        if (missingIgnoreEntries.Count == 0)
        {
            yield break;
        }

        yield return "# CARVES local/tooling residue (operator reviewed)";
        foreach (var entry in missingIgnoreEntries)
        {
            yield return entry;
        }
    }

    private static IEnumerable<string> BuildGaps(RuntimeTargetResiduePolicySurface residuePolicy, bool planReady)
    {
        if (planReady)
        {
            yield break;
        }

        foreach (var gap in residuePolicy.Gaps)
        {
            yield return $"target_residue_policy:{gap}";
        }

        if (!residuePolicy.ResiduePolicyReady)
        {
            yield return "target_ignore_decision_plan_waiting_for_residue_policy";
        }
    }

    private static string ResolvePosture(
        RuntimeTargetResiduePolicySurface residuePolicy,
        bool planReady,
        int candidateCount,
        int missingIgnoreEntryCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "target_ignore_decision_plan_blocked_by_surface_gaps";
        }

        if (!planReady)
        {
            return "target_ignore_decision_plan_waiting_for_residue_policy";
        }

        if (residuePolicy.ResiduePathCount == 0)
        {
            return "target_ignore_decision_plan_no_residue";
        }

        if (candidateCount == 0)
        {
            return "target_ignore_decision_plan_local_only_no_ignore_candidates";
        }

        return missingIgnoreEntryCount > 0
            ? "target_ignore_decision_plan_ready_for_operator_review"
            : "target_ignore_decision_plan_candidates_already_present";
    }

    private static string BuildSummary(
        RuntimeTargetResiduePolicySurface residuePolicy,
        bool planReady,
        int candidateCount,
        int missingIgnoreEntryCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "Target ignore decision plan cannot be trusted until required documents and residue-policy gaps are resolved.";
        }

        if (!planReady)
        {
            return residuePolicy.Summary;
        }

        if (residuePolicy.ResiduePathCount == 0)
        {
            return "No local/tooling residue remains; no ignore decision is required.";
        }

        if (candidateCount == 0)
        {
            return "Local/tooling residue remains, but there are no deterministic ignore candidates; keep it local or clean it manually after operator review.";
        }

        return missingIgnoreEntryCount > 0
            ? "Ignore decision candidates are ready for operator review; product proof can remain complete without applying them."
            : "Ignore decision candidates are already present in .gitignore; product proof can remain complete.";
    }

    private static string BuildRecommendedNextAction(
        RuntimeTargetResiduePolicySurface residuePolicy,
        bool planReady,
        int candidateCount,
        int missingIgnoreEntryCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "Restore target ignore decision plan docs and residue-policy inputs, then rerun carves pilot ignore-plan --json.";
        }

        if (!planReady)
        {
            return "Run carves pilot residue --json and resolve its gaps before ignore decision planning.";
        }

        if (residuePolicy.ResiduePathCount == 0)
        {
            return "target_ignore_decision_plan_readback_clean";
        }

        if (candidateCount == 0)
        {
            return "Keep residue local or clean it manually after operator review; do not commit residue by default.";
        }

        return missingIgnoreEntryCount > 0
            ? "Choose keep_local, manual_cleanup_after_review, or edit .gitignore with the patch preview after operator review; if edited, rerun commit-plan, closure, residue, ignore-plan, and proof."
            : "Keep residue local or rerun commit-plan if .gitignore changed; product proof can remain complete.";
    }

    private static string BuildPlanId(
        RuntimeTargetResiduePolicySurface residuePolicy,
        IReadOnlyList<RuntimeTargetIgnoreDecisionCandidateSurface> candidates)
    {
        var builder = new StringBuilder();
        builder.AppendLine(residuePolicy.CommitPlanId);
        builder.AppendLine(residuePolicy.OverallPosture);
        foreach (var candidate in candidates.OrderBy(static candidate => candidate.Entry, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(candidate.Entry)
                .Append('|')
                .Append(candidate.AlreadyPresentInGitIgnore)
                .Append('|')
                .Append(candidate.MatchingPathCount)
                .Append('|')
                .AppendLine(candidate.Reason);
            foreach (var path in candidate.MatchingPaths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("path:")
                    .AppendLine(path);
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return $"target-ignore-decision-plan-{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
