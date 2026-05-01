using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeTargetCommitClosureService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-15-target-commit-closure.md";
    public const string GuideDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeTargetCommitClosureService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeTargetCommitClosureSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 15 target commit closure document", errors);
        ValidateRuntimeDocument(RuntimeTargetCommitPlanService.PhaseDocumentPath, "Product closure Phase 14 target commit plan document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Productized pilot status guide document", errors);

        var commitPlan = new RuntimeTargetCommitPlanService(repoRoot).Build();
        errors.AddRange(commitPlan.Errors.Select(static error => $"runtime-target-commit-plan: {error}"));

        var targetGitWorktreeClean = string.Equals(commitPlan.OverallPosture, "target_commit_plan_clean", StringComparison.Ordinal)
                                     && commitPlan.StagePathCount == 0
                                     && commitPlan.ExcludedPathCount == 0
                                     && commitPlan.OperatorReviewRequiredPathCount == 0;
        var closureComplete = commitPlan.IsValid
                              && commitPlan.RuntimeInitialized
                              && commitPlan.GitRepositoryDetected
                              && commitPlan.StagePathCount == 0
                              && commitPlan.OperatorReviewRequiredPathCount == 0;

        return new RuntimeTargetCommitClosureSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            CommitPlanDocumentPath = RuntimeTargetCommitPlanService.PhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            OverallPosture = ResolvePosture(commitPlan, closureComplete, errors.Count),
            CommitPlanPosture = commitPlan.OverallPosture,
            CommitPlanId = commitPlan.CommitPlanId,
            RuntimeInitialized = commitPlan.RuntimeInitialized,
            GitRepositoryDetected = commitPlan.GitRepositoryDetected,
            TargetGitWorktreeClean = targetGitWorktreeClean,
            CommitClosureComplete = closureComplete,
            CanStage = commitPlan.CanStage,
            StagePathCount = commitPlan.StagePathCount,
            ExcludedPathCount = commitPlan.ExcludedPathCount,
            OperatorReviewRequiredPathCount = commitPlan.OperatorReviewRequiredPathCount,
            StagePaths = commitPlan.StagePaths,
            ExcludedPaths = commitPlan.ExcludedPaths,
            OperatorReviewRequiredPaths = commitPlan.OperatorReviewRequiredPaths,
            Gaps = BuildGaps(commitPlan, closureComplete).ToArray(),
            Summary = BuildSummary(commitPlan, targetGitWorktreeClean, closureComplete, errors.Count),
            RecommendedNextAction = BuildRecommendedNextAction(commitPlan, closureComplete, errors.Count),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not stage, commit, clean, delete, rewrite, or push git paths.",
                "This surface only proves the target commit-closure readback posture from git status and commit-plan state.",
                "This surface does not prove the commit was pushed, tagged, released, or accepted by a remote platform.",
                "This surface does not replace operator review of the final commit contents or downstream release policy.",
            ],
        };
    }

    private static IEnumerable<string> BuildGaps(RuntimeTargetCommitPlanSurface commitPlan, bool closureComplete)
    {
        foreach (var gap in commitPlan.Gaps)
        {
            yield return gap;
        }

        if (!commitPlan.IsValid)
        {
            yield return "target_commit_plan_invalid";
        }

        if (!closureComplete && commitPlan.CanStage)
        {
            yield return "target_commit_plan_stage_paths_pending";
        }

        if (!closureComplete && commitPlan.ExcludedPathCount > 0)
        {
            yield return "excluded_paths_remain";
        }

        if (!closureComplete && commitPlan.OperatorReviewRequiredPathCount > 0)
        {
            yield return "operator_review_required_paths_remain";
        }
    }

    private static string ResolvePosture(RuntimeTargetCommitPlanSurface commitPlan, bool closureComplete, int errorCount)
    {
        if (!commitPlan.GitRepositoryDetected)
        {
            return "target_commit_closure_blocked_by_git_repo_gap";
        }

        if (!commitPlan.RuntimeInitialized)
        {
            return "target_commit_closure_blocked_by_runtime_init";
        }

        if (errorCount > 0)
        {
            return "target_commit_closure_blocked_by_surface_gaps";
        }

        if (closureComplete)
        {
            return commitPlan.ExcludedPathCount > 0
                ? "target_commit_closure_local_residue_only_complete"
                : "target_commit_closure_clean";
        }

        if (commitPlan.OperatorReviewRequiredPathCount > 0)
        {
            return "target_commit_closure_blocked_by_operator_review_required";
        }

        if (commitPlan.CanStage)
        {
            return "target_commit_closure_waiting_for_operator_commit";
        }

        if (commitPlan.ExcludedPathCount > 0 && commitPlan.StagePathCount == 0)
        {
            return "target_commit_closure_local_residue_only";
        }

        return "target_commit_closure_blocked_by_commit_plan";
    }

    private static string BuildSummary(RuntimeTargetCommitPlanSurface commitPlan, bool targetGitWorktreeClean, bool closureComplete, int errorCount)
    {
        if (!commitPlan.GitRepositoryDetected)
        {
            return "Target commit closure cannot read git status because the current directory is not a git work tree.";
        }

        if (!commitPlan.RuntimeInitialized)
        {
            return "Target commit closure waits for `.ai/runtime.json`; run attach/init before commit closure.";
        }

        if (errorCount > 0)
        {
            return "Target commit closure cannot be trusted until required documents and commit-plan gaps are resolved.";
        }

        if (closureComplete)
        {
            return targetGitWorktreeClean
                ? "Target git work tree is clean after governed writeback; product pilot commit closure is complete."
                : "Product pilot commit closure is complete; only excluded local/tooling residue remains outside the product commit.";
        }

        if (commitPlan.OperatorReviewRequiredPathCount > 0)
        {
            return "Target commit closure is blocked because operator-review paths remain.";
        }

        if (commitPlan.CanStage)
        {
            return "Target commit closure is pending; stage the commit-plan paths and commit with an operator-reviewed message.";
        }

        if (commitPlan.ExcludedPathCount > 0 && commitPlan.StagePathCount == 0)
        {
            return "Only excluded local/tooling residue remains; operator cleanup or ignore policy is required before closure can be called clean.";
        }

        return "Target commit closure is blocked by the current commit-plan posture.";
    }

    private static string BuildRecommendedNextAction(RuntimeTargetCommitPlanSurface commitPlan, bool closureComplete, int errorCount)
    {
        if (!commitPlan.GitRepositoryDetected)
        {
            return "Run this command from an attached git target repo.";
        }

        if (!commitPlan.RuntimeInitialized)
        {
            return "carves init [target-path] --json";
        }

        if (errorCount > 0)
        {
            return "Restore commit-closure and commit-plan surface inputs before final pilot closure.";
        }

        if (closureComplete)
        {
            return "product_pilot_closure_readback_complete";
        }

        if (commitPlan.CanStage)
        {
            return "Run carves pilot commit-plan --json, stage the listed paths, commit with an operator-reviewed message, then rerun carves pilot closure --json.";
        }

        if (commitPlan.OperatorReviewRequiredPathCount > 0)
        {
            return "Review operator_review_required_paths before any git add or commit.";
        }

        if (commitPlan.ExcludedPathCount > 0)
        {
            return "Resolve excluded local/tooling residue according to operator policy, then rerun carves pilot closure --json.";
        }

        return commitPlan.RecommendedNextAction;
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
