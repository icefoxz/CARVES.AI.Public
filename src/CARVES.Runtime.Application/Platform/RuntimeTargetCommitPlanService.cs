using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeTargetCommitPlanService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-14-target-commit-plan.md";
    public const string GuideDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeTargetCommitPlanService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeTargetCommitPlanSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 14 target commit plan document", errors);
        ValidateRuntimeDocument(RuntimeTargetCommitHygieneService.PhaseDocumentPath, "Product closure Phase 13 target commit hygiene document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Productized pilot status guide document", errors);

        var hygiene = new RuntimeTargetCommitHygieneService(repoRoot).Build();
        errors.AddRange(hygiene.Errors.Select(static error => $"runtime-target-commit-hygiene: {error}"));

        var stagePaths = hygiene.CommitCandidatePaths.ToArray();
        var excludedPaths = hygiene.ExcludedPaths.ToArray();
        var reviewPaths = hygiene.OperatorReviewRequiredPaths.ToArray();
        var gaps = BuildGaps(hygiene).ToArray();
        var canStage = errors.Count == 0 && hygiene.CanProceedToCommit;
        var suggestedCommitMessage = BuildSuggestedCommitMessage(hygiene);

        return new RuntimeTargetCommitPlanSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            HygieneDocumentPath = RuntimeTargetCommitHygieneService.PhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            OverallPosture = ResolvePosture(hygiene, errors.Count),
            CommitPlanId = BuildCommitPlanId(hygiene),
            RuntimeInitialized = hygiene.RuntimeInitialized,
            GitRepositoryDetected = hygiene.GitRepositoryDetected,
            CanStage = canStage,
            CanCommitAfterStaging = canStage,
            StagePathCount = stagePaths.Length,
            ExcludedPathCount = excludedPaths.Length,
            OperatorReviewRequiredPathCount = reviewPaths.Length,
            StagePaths = stagePaths,
            ExcludedPaths = excludedPaths,
            OperatorReviewRequiredPaths = reviewPaths,
            SuggestedCommitMessage = suggestedCommitMessage,
            GitAddCommandPreview = BuildGitAddCommandPreview(stagePaths),
            GitCommitCommandPreview = BuildGitCommitCommandPreview(suggestedCommitMessage, canStage),
            HygieneSummary = hygiene.Summary,
            Gaps = gaps,
            Summary = BuildSummary(hygiene, stagePaths.Length, excludedPaths.Length, reviewPaths.Length, errors.Count),
            RecommendedNextAction = BuildRecommendedNextAction(hygiene, stagePaths.Length, excludedPaths.Length, reviewPaths.Length, errors.Count),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not stage, commit, clean, delete, rewrite, or push git paths.",
                "This surface only builds a deterministic operator-reviewed staging plan from target commit hygiene classifications.",
                "This surface does not prove every target output candidate was approved by Runtime review.",
                "This surface does not replace human commit-message judgment or downstream release policy.",
            ],
        };
    }

    private static IEnumerable<string> BuildGaps(RuntimeTargetCommitHygieneSurface hygiene)
    {
        foreach (var gap in hygiene.Gaps)
        {
            yield return gap;
        }

        if (!hygiene.IsValid)
        {
            yield return "target_commit_hygiene_invalid";
        }

        if (hygiene.DirtyPathCount > 0 && hygiene.CommitCandidatePathCount == 0 && hygiene.UnclassifiedPathCount == 0)
        {
            yield return "no_stageable_commit_candidate_paths";
        }
    }

    private static string ResolvePosture(RuntimeTargetCommitHygieneSurface hygiene, int errorCount)
    {
        if (!hygiene.GitRepositoryDetected)
        {
            return "target_commit_plan_blocked_by_git_repo_gap";
        }

        if (!hygiene.RuntimeInitialized)
        {
            return "target_commit_plan_blocked_by_runtime_init";
        }

        if (errorCount > 0)
        {
            return "target_commit_plan_blocked_by_surface_gaps";
        }

        if (hygiene.DirtyPathCount == 0)
        {
            return "target_commit_plan_clean";
        }

        if (hygiene.UnclassifiedPathCount > 0)
        {
            return "target_commit_plan_blocked_by_operator_review_required";
        }

        if (hygiene.CommitCandidatePathCount == 0)
        {
            return "target_commit_plan_no_stage_candidates";
        }

        return hygiene.LocalResiduePathCount > 0
            ? "target_commit_plan_ready_with_exclusions"
            : "target_commit_plan_ready";
    }

    private static string BuildSummary(
        RuntimeTargetCommitHygieneSurface hygiene,
        int stagePathCount,
        int excludedPathCount,
        int reviewPathCount,
        int errorCount)
    {
        if (!hygiene.GitRepositoryDetected)
        {
            return "Target commit plan cannot read git status because the current directory is not a git work tree.";
        }

        if (!hygiene.RuntimeInitialized)
        {
            return "Target commit plan waits for `.ai/runtime.json`; run attach/init before commit closure.";
        }

        if (errorCount > 0)
        {
            return "Target commit plan cannot be trusted until required documents and hygiene surface gaps are resolved.";
        }

        if (hygiene.DirtyPathCount == 0)
        {
            return "The target git work tree is clean; no staging plan is required.";
        }

        if (reviewPathCount > 0)
        {
            return "The target commit plan is blocked because unclassified dirty paths require operator review.";
        }

        if (stagePathCount > 0 && excludedPathCount > 0)
        {
            return "The target commit plan is ready; stage only listed paths and exclude local/tooling residue.";
        }

        if (stagePathCount > 0)
        {
            return "The target commit plan is ready; stage the listed CARVES truth and approved output paths.";
        }

        return "Only local/tooling residue is dirty; no staging plan is required by default.";
    }

    private static string BuildRecommendedNextAction(
        RuntimeTargetCommitHygieneSurface hygiene,
        int stagePathCount,
        int excludedPathCount,
        int reviewPathCount,
        int errorCount)
    {
        if (!hygiene.GitRepositoryDetected)
        {
            return "Run this command from an attached git target repo.";
        }

        if (!hygiene.RuntimeInitialized)
        {
            return "carves init [target-path] --json";
        }

        if (errorCount > 0)
        {
            return "Restore commit-plan and commit-hygiene surface inputs before staging.";
        }

        if (reviewPathCount > 0)
        {
            return "Review operator_review_required_paths before any git add or commit.";
        }

        if (stagePathCount > 0)
        {
            return excludedPathCount > 0
                ? "Run the git add preview for stage_paths only, exclude excluded_paths, then commit with an operator-reviewed message."
                : "Run the git add preview for stage_paths, then commit with an operator-reviewed message.";
        }

        if (excludedPathCount > 0)
        {
            return "Do not commit local/tooling residue by default; clean or ignore it only after operator review.";
        }

        return "no_commit_required";
    }

    private static string BuildSuggestedCommitMessage(RuntimeTargetCommitHygieneSurface hygiene)
    {
        if (hygiene.OfficialTruthPathCount > 0 && hygiene.TargetOutputCandidatePathCount > 0)
        {
            return "Record CARVES governed target proof";
        }

        if (hygiene.OfficialTruthPathCount > 0)
        {
            return "Update CARVES target truth";
        }

        if (hygiene.TargetOutputCandidatePathCount > 0)
        {
            return "Add CARVES governed target output";
        }

        return "No CARVES target commit required";
    }

    private static string BuildGitAddCommandPreview(IReadOnlyList<string> stagePaths)
    {
        return stagePaths.Count == 0
            ? "no_stage_required"
            : $"git add -- {string.Join(' ', stagePaths.Select(QuotePowerShellPath))}";
    }

    private static string BuildGitCommitCommandPreview(string suggestedCommitMessage, bool canStage)
    {
        return canStage
            ? $"git commit -m {QuotePowerShellPath(suggestedCommitMessage)}"
            : "blocked_until_commit_plan_ready";
    }

    private static string BuildCommitPlanId(RuntimeTargetCommitHygieneSurface hygiene)
    {
        var builder = new StringBuilder();
        builder.AppendLine(hygiene.RuntimeInitialized.ToString());
        builder.AppendLine(hygiene.GitRepositoryDetected.ToString());
        foreach (var path in hygiene.DirtyPaths.OrderBy(static path => path.Path, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(path.StatusCode)
                .Append('|')
                .Append(path.Path)
                .Append('|')
                .Append(path.PathClass)
                .Append('|')
                .AppendLine(path.CommitPosture);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return $"target-commit-plan-{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
    }

    private static string QuotePowerShellPath(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
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
