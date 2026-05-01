using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeTargetResiduePolicyService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md";
    public const string GuideDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeTargetResiduePolicyService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeTargetResiduePolicySurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 27 external target residue policy document", errors);
        ValidateRuntimeDocument(RuntimeTargetCommitClosureService.PhaseDocumentPath, "Product closure Phase 15 target commit closure document", errors);
        ValidateRuntimeDocument(RuntimeTargetCommitPlanService.PhaseDocumentPath, "Product closure Phase 14 target commit plan document", errors);
        ValidateRuntimeDocument(RuntimeTargetCommitHygieneService.PhaseDocumentPath, "Product closure Phase 13 target commit hygiene document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Productized pilot status guide document", errors);

        var commitClosure = new RuntimeTargetCommitClosureService(repoRoot).Build();
        errors.AddRange(commitClosure.Errors.Select(static error => $"runtime-target-commit-closure: {error}"));

        var suggestions = BuildIgnoreSuggestions(commitClosure.ExcludedPaths).ToArray();
        var suggestedIgnoreEntries = suggestions
            .Select(static suggestion => suggestion.Entry)
            .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var residuePolicyReady = errors.Count == 0
                                 && commitClosure.RuntimeInitialized
                                 && commitClosure.GitRepositoryDetected
                                 && commitClosure.CommitClosureComplete
                                 && commitClosure.StagePathCount == 0
                                 && commitClosure.OperatorReviewRequiredPathCount == 0;
        var productProofCanRemainComplete = residuePolicyReady;

        return new RuntimeTargetResiduePolicySurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            CommitClosureDocumentPath = RuntimeTargetCommitClosureService.PhaseDocumentPath,
            CommitPlanDocumentPath = RuntimeTargetCommitPlanService.PhaseDocumentPath,
            CommitHygieneDocumentPath = RuntimeTargetCommitHygieneService.PhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(commitClosure, residuePolicyReady, suggestions.Length, errors.Count),
            CommitClosurePosture = commitClosure.OverallPosture,
            CommitPlanPosture = commitClosure.CommitPlanPosture,
            CommitPlanId = commitClosure.CommitPlanId,
            RuntimeInitialized = commitClosure.RuntimeInitialized,
            GitRepositoryDetected = commitClosure.GitRepositoryDetected,
            TargetGitWorktreeClean = commitClosure.TargetGitWorktreeClean,
            CommitClosureComplete = commitClosure.CommitClosureComplete,
            ResiduePolicyReady = residuePolicyReady,
            ProductProofCanRemainComplete = productProofCanRemainComplete,
            CanKeepResidueLocal = residuePolicyReady && commitClosure.ExcludedPathCount > 0,
            CanAddIgnoreAfterReview = residuePolicyReady && suggestions.Length > 0,
            StagePathCount = commitClosure.StagePathCount,
            ResiduePathCount = commitClosure.ExcludedPathCount,
            OperatorReviewRequiredPathCount = commitClosure.OperatorReviewRequiredPathCount,
            SuggestedIgnoreEntryCount = suggestedIgnoreEntries.Length,
            StagePaths = commitClosure.StagePaths,
            ResiduePaths = commitClosure.ExcludedPaths,
            OperatorReviewRequiredPaths = commitClosure.OperatorReviewRequiredPaths,
            SuggestedIgnoreEntries = suggestedIgnoreEntries,
            IgnoreSuggestions = suggestions,
            BoundaryRules =
            [
                "Do not commit local/tooling residue by default.",
                "Do not treat local Runtime locks, live state, tmp folders, worktrees, IDE state, build outputs, or .carves-platform/ as target product truth.",
                "A target product proof can remain complete when stage_paths and operator_review_required_paths are empty and only excluded local/tooling residue remains.",
                "Adding suggested .gitignore entries is an operator-reviewed target decision; this surface does not write .gitignore.",
            ],
            Gaps = BuildGaps(commitClosure, residuePolicyReady).ToArray(),
            Summary = BuildSummary(commitClosure, residuePolicyReady, suggestions.Length, errors.Count),
            RecommendedNextAction = BuildRecommendedNextAction(commitClosure, residuePolicyReady, suggestions.Length, errors.Count),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not stage, commit, clean, delete, ignore, rewrite, or push git paths.",
                "This surface does not mutate .gitignore; suggested ignore entries require operator review before use.",
                "This surface does not make excluded local/tooling residue product truth.",
                "This surface does not replace target commit closure, product pilot proof, or downstream release policy.",
            ],
        };
    }

    private static IEnumerable<RuntimeTargetResidueIgnoreSuggestionSurface> BuildIgnoreSuggestions(IReadOnlyList<string> residuePaths)
    {
        return residuePaths
            .Select(path => new { Path = path, Suggestion = ResolveSuggestedIgnoreEntry(path) })
            .Where(static item => item.Suggestion is not null)
            .GroupBy(static item => item.Suggestion!.Value.Entry, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group =>
            {
                var first = group.First().Suggestion!.Value;
                var matchingPaths = group
                    .Select(static item => item.Path)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new RuntimeTargetResidueIgnoreSuggestionSurface
                {
                    Entry = first.Entry,
                    MatchingPathCount = matchingPaths.Length,
                    MatchingPaths = matchingPaths,
                    Reason = first.Reason,
                };
            });
    }

    private static (string Entry, string Reason)? ResolveSuggestedIgnoreEntry(string path)
    {
        if (PathEquals(path, ".ai/runtime/attach.lock.json"))
        {
            return (".ai/runtime/attach.lock.json", "local attach coordination lock, not target product truth");
        }

        if (PathEquals(path, ".DS_Store"))
        {
            return (".DS_Store", "OS metadata residue");
        }

        if (HasPrefix(path, ".ai/runtime/live-state/"))
        {
            return (".ai/runtime/live-state/", "local runtime live state, not durable target truth by default");
        }

        if (HasPrefix(path, ".ai/runtime/tmp/"))
        {
            return (".ai/runtime/tmp/", "local runtime temporary files");
        }

        if (HasPrefix(path, ".ai/tmp/"))
        {
            return (".ai/tmp/", "local CARVES temporary files");
        }

        if (HasPrefix(path, ".ai/worktrees/"))
        {
            return (".ai/worktrees/", "local managed or auxiliary worktree residue");
        }

        if (HasPrefix(path, ".carves-platform/"))
        {
            return (".carves-platform/", "local CARVES platform state outside target product truth");
        }

        if (HasPrefix(path, ".carves-worktrees/"))
        {
            return (".carves-worktrees/", "local CARVES worktree residue");
        }

        if (HasPrefix(path, ".vs/"))
        {
            return (".vs/", "IDE local state");
        }

        if (HasPrefix(path, ".idea/"))
        {
            return (".idea/", "IDE local state");
        }

        if (HasPrefix(path, ".vscode/"))
        {
            return (".vscode/", "IDE local state");
        }

        if (HasPrefix(path, "bin/"))
        {
            return ("bin/", "build output");
        }

        if (HasPrefix(path, "obj/"))
        {
            return ("obj/", "build output");
        }

        if (HasPrefix(path, "TestResults/"))
        {
            return ("TestResults/", "test output");
        }

        if (HasPrefix(path, "coverage/"))
        {
            return ("coverage/", "coverage output");
        }

        return null;
    }

    private static IEnumerable<string> BuildGaps(RuntimeTargetCommitClosureSurface commitClosure, bool residuePolicyReady)
    {
        if (residuePolicyReady)
        {
            yield break;
        }

        foreach (var gap in commitClosure.Gaps)
        {
            yield return $"target_commit_closure:{gap}";
        }

        if (!commitClosure.RuntimeInitialized)
        {
            yield return "runtime_not_initialized";
        }

        if (!commitClosure.GitRepositoryDetected)
        {
            yield return "target_git_repository_not_detected";
        }

        if (commitClosure.StagePathCount > 0)
        {
            yield return "target_commit_stage_paths_pending";
        }

        if (commitClosure.OperatorReviewRequiredPathCount > 0)
        {
            yield return "operator_review_required_paths_remain";
        }

        yield return "target_residue_policy_waiting_for_commit_closure";
    }

    private static string ResolvePosture(RuntimeTargetCommitClosureSurface commitClosure, bool residuePolicyReady, int suggestionCount, int errorCount)
    {
        if (!commitClosure.GitRepositoryDetected)
        {
            return "target_residue_policy_blocked_by_git_repo_gap";
        }

        if (!commitClosure.RuntimeInitialized)
        {
            return "target_residue_policy_blocked_by_runtime_init";
        }

        if (errorCount > 0)
        {
            return "target_residue_policy_blocked_by_surface_gaps";
        }

        if (commitClosure.OperatorReviewRequiredPathCount > 0)
        {
            return "target_residue_policy_blocked_by_operator_review_required";
        }

        if (commitClosure.StagePathCount > 0 || commitClosure.CanStage)
        {
            return "target_residue_policy_waiting_for_operator_commit";
        }

        if (!residuePolicyReady)
        {
            return "target_residue_policy_blocked_by_commit_closure";
        }

        if (commitClosure.ExcludedPathCount == 0)
        {
            return "target_residue_policy_clean";
        }

        return suggestionCount > 0
            ? "target_residue_policy_ready_with_ignore_suggestions"
            : "target_residue_policy_ready_local_only";
    }

    private static string BuildSummary(RuntimeTargetCommitClosureSurface commitClosure, bool residuePolicyReady, int suggestionCount, int errorCount)
    {
        if (!commitClosure.GitRepositoryDetected)
        {
            return "Target residue policy cannot read git status because the current directory is not a git work tree.";
        }

        if (!commitClosure.RuntimeInitialized)
        {
            return "Target residue policy waits for `.ai/runtime.json`; run attach/init before residue readback.";
        }

        if (errorCount > 0)
        {
            return "Target residue policy cannot be trusted until required documents and commit-closure gaps are resolved.";
        }

        if (commitClosure.OperatorReviewRequiredPathCount > 0)
        {
            return "Target residue policy is blocked because operator-review paths remain.";
        }

        if (commitClosure.StagePathCount > 0 || commitClosure.CanStage)
        {
            return "Target residue policy waits until the operator-reviewed target product proof commit has been made.";
        }

        if (!residuePolicyReady)
        {
            return "Target residue policy waits for target commit closure to complete.";
        }

        if (commitClosure.ExcludedPathCount == 0)
        {
            return "Target git work tree has no excluded local/tooling residue after commit closure.";
        }

        return suggestionCount > 0
            ? "Only excluded local/tooling residue remains; product proof can remain complete, and suggested .gitignore entries are available for operator review."
            : "Only excluded local/tooling residue remains; product proof can remain complete, and the operator may keep the residue local or clean it manually.";
    }

    private static string BuildRecommendedNextAction(RuntimeTargetCommitClosureSurface commitClosure, bool residuePolicyReady, int suggestionCount, int errorCount)
    {
        if (!commitClosure.GitRepositoryDetected)
        {
            return "Run this command from an attached git target repo.";
        }

        if (!commitClosure.RuntimeInitialized)
        {
            return "carves init [target-path] --json";
        }

        if (errorCount > 0)
        {
            return "Restore residue-policy and commit-closure surface inputs before using residue guidance.";
        }

        if (commitClosure.OperatorReviewRequiredPathCount > 0)
        {
            return "Review operator_review_required_paths before any git add, ignore, cleanup, or product proof claim.";
        }

        if (commitClosure.StagePathCount > 0 || commitClosure.CanStage)
        {
            return "Run carves pilot commit-plan --json, stage only stage_paths, commit with an operator-reviewed message, then rerun carves pilot closure --json and carves pilot residue --json.";
        }

        if (!residuePolicyReady)
        {
            return "Run carves pilot closure --json and resolve its gaps before residue policy readback.";
        }

        if (commitClosure.ExcludedPathCount == 0)
        {
            return "target_residue_policy_readback_clean";
        }

        return suggestionCount > 0
            ? "Keep residue local or add suggested .gitignore entries after operator review; do not commit residue by default."
            : "Keep residue local or clean it manually after operator review; do not commit residue by default.";
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private static bool PathEquals(string path, string candidate)
    {
        return string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPrefix(string path, string prefix)
    {
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
