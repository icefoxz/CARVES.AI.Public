using System.Diagnostics;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeTargetCommitHygieneService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-13-target-commit-hygiene.md";
    public const string GuideDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeTargetCommitHygieneService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeTargetCommitHygieneSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 13 target commit hygiene document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Productized pilot status guide document", errors);

        var runtimeInitialized = File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json"));
        var statusRead = ReadGitStatus();
        if (!string.IsNullOrWhiteSpace(statusRead.Error))
        {
            errors.Add(statusRead.Error);
        }

        var dirtyPaths = statusRead.Entries
            .Select(ProjectPath)
            .OrderBy(path => path.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var officialTruth = dirtyPaths.Where(static path => path.PathClass == "official_target_truth").ToArray();
        var targetOutput = dirtyPaths.Where(static path => path.PathClass == "target_output_candidate").ToArray();
        var localResidue = dirtyPaths.Where(static path => path.PathClass == "local_or_tooling_residue").ToArray();
        var unclassified = dirtyPaths.Where(static path => path.PathClass == "unclassified").ToArray();
        var commitCandidates = officialTruth
            .Concat(targetOutput)
            .Select(static path => path.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var gaps = BuildGaps(runtimeInitialized, statusRead.GitRepositoryDetected, unclassified).ToArray();
        var canProceedToCommit = errors.Count == 0
                                 && runtimeInitialized
                                 && statusRead.GitRepositoryDetected
                                 && commitCandidates.Length > 0
                                 && unclassified.Length == 0;

        return new RuntimeTargetCommitHygieneSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            OverallPosture = ResolvePosture(runtimeInitialized, statusRead.GitRepositoryDetected, dirtyPaths.Length, commitCandidates.Length, localResidue.Length, unclassified.Length, errors.Count),
            RuntimeInitialized = runtimeInitialized,
            GitRepositoryDetected = statusRead.GitRepositoryDetected,
            CanProceedToCommit = canProceedToCommit,
            DirtyPathCount = dirtyPaths.Length,
            CommitCandidatePathCount = commitCandidates.Length,
            OfficialTruthPathCount = officialTruth.Length,
            TargetOutputCandidatePathCount = targetOutput.Length,
            LocalResiduePathCount = localResidue.Length,
            UnclassifiedPathCount = unclassified.Length,
            DirtyPaths = dirtyPaths,
            CommitCandidatePaths = commitCandidates,
            ExcludedPaths = localResidue.Select(static path => path.Path).ToArray(),
            OperatorReviewRequiredPaths = unclassified.Select(static path => path.Path).ToArray(),
            Gaps = gaps,
            Summary = BuildSummary(runtimeInitialized, statusRead.GitRepositoryDetected, dirtyPaths.Length, commitCandidates.Length, localResidue.Length, unclassified.Length),
            RecommendedNextAction = BuildRecommendedNextAction(runtimeInitialized, statusRead.GitRepositoryDetected, commitCandidates.Length, localResidue.Length, unclassified.Length),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not stage, commit, clean, delete, or rewrite git paths.",
                "This surface classifies target git status for operator review; it does not prove every target output was approved by Runtime review.",
                "This surface does not make local live state product truth.",
                "This surface does not replace human commit-message judgment or downstream push/release policy.",
            ],
        };
    }

    public static RuntimeTargetCommitHygienePathSurface ProjectPath(string statusCode, string path)
    {
        return ProjectPath(new GitStatusEntry(statusCode, path));
    }

    private static RuntimeTargetCommitHygienePathSurface ProjectPath(GitStatusEntry entry)
    {
        var path = NormalizeGitPath(entry.Path);
        var pathClass = ClassifyPath(path);
        return new RuntimeTargetCommitHygienePathSurface
        {
            StatusCode = entry.StatusCode,
            Path = path,
            PathClass = pathClass,
            CommitPosture = ResolveCommitPosture(pathClass),
            RecommendedAction = ResolveRecommendedAction(pathClass),
        };
    }

    private GitStatusReadResult ReadGitStatus()
    {
        try
        {
            if (!HasGitMetadataAtRepoRoot())
            {
                return new GitStatusReadResult(false, [], null);
            }

            var repositoryCheck = RunGit("rev-parse", "--is-inside-work-tree");
            if (repositoryCheck.ExitCode != 0
                || !string.Equals(repositoryCheck.StandardOutput.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                var detail = string.IsNullOrWhiteSpace(repositoryCheck.StandardError)
                    ? repositoryCheck.StandardOutput.Trim()
                    : repositoryCheck.StandardError.Trim();
                return new GitStatusReadResult(false, [], $"Target commit hygiene requires a git work tree rooted at the target repository: {detail}");
            }

            var status = RunGit("status", "--porcelain=v1", "--untracked-files=all");
            if (status.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(status.StandardError)
                    ? status.StandardOutput.Trim()
                    : status.StandardError.Trim();
                return new GitStatusReadResult(false, [], $"Unable to read target git status: {detail}");
            }

            var entries = status.StandardOutput
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseStatusLine)
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.Path))
                .ToArray();

            return new GitStatusReadResult(true, entries, null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return new GitStatusReadResult(false, [], $"Unable to invoke git for target commit hygiene: {exception.Message}");
        }
    }

    private bool HasGitMetadataAtRepoRoot()
    {
        var gitPath = Path.Combine(repoRoot, ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    private GitCommandResult RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitCommandResult(process.ExitCode, standardOutput, standardError);
    }

    private static GitStatusEntry ParseStatusLine(string line)
    {
        if (line.Length < 3)
        {
            return new GitStatusEntry(line.Trim(), string.Empty);
        }

        var statusCode = line[..2];
        var path = line[3..].Trim();
        var renameSeparator = path.LastIndexOf(" -> ", StringComparison.Ordinal);
        if (renameSeparator >= 0)
        {
            path = path[(renameSeparator + 4)..].Trim();
        }

        return new GitStatusEntry(statusCode.Trim(), path);
    }

    private static string ClassifyPath(string path)
    {
        if (IsLocalOrToolingResidue(path))
        {
            return "local_or_tooling_residue";
        }

        if (IsOfficialTargetTruth(path))
        {
            return "official_target_truth";
        }

        if (IsTargetOutputCandidate(path))
        {
            return "target_output_candidate";
        }

        return "unclassified";
    }

    private static bool IsOfficialTargetTruth(string path)
    {
        return PathEquals(path, "AGENTS.md")
               || PathEquals(path, ".ai/PROJECT_BOUNDARY.md")
               || PathEquals(path, ".ai/STATE.md")
               || PathEquals(path, ".ai/TASK_QUEUE.md")
               || PathEquals(path, ".ai/CURRENT_TASK.md")
               || PathEquals(path, ".ai/runtime.json")
               || PathEquals(path, ".ai/runtime/attach-handshake.json")
               || PathEquals(path, ".ai/AGENT_BOOTSTRAP.md")
               || HasPrefix(path, ".ai/config/")
               || HasPrefix(path, ".ai/codegraph/")
               || HasPrefix(path, ".ai/opportunities/")
               || HasPrefix(path, ".ai/refactoring/")
               || HasPrefix(path, ".ai/tasks/")
               || HasPrefix(path, ".ai/memory/")
               || HasPrefix(path, ".ai/evidence/")
               || HasPrefix(path, ".ai/artifacts/")
               || HasPrefix(path, ".ai/artifacts/reviews/")
               || HasPrefix(path, ".ai/artifacts/merge-candidates/")
               || HasPrefix(path, ".ai/runtime/planning/")
               || HasPrefix(path, ".ai/runtime/formal-planning/")
               || HasPrefix(path, ".ai/runtime/target-ignore-decisions/")
               || HasPrefix(path, ".ai/runtime/pilot-problems/")
               || HasPrefix(path, ".ai/runtime/pilot-evidence/")
               || HasPrefix(path, ".ai/runtime/agent-problem-follow-up-decisions/");
    }

    private static bool IsTargetOutputCandidate(string path)
    {
        return PathEquals(path, "README.md")
               || PathEquals(path, "PROJECT.md")
               || PathEquals(path, ".gitignore")
               || PathEquals(path, ".editorconfig")
               || HasPrefix(path, "docs/")
               || HasPrefix(path, "src/")
               || HasPrefix(path, "tests/")
               || HasPrefix(path, "test/")
               || HasPrefix(path, "app/")
               || HasPrefix(path, "lib/");
    }

    private static bool IsLocalOrToolingResidue(string path)
    {
        return PathEquals(path, ".DS_Store")
               || PathEquals(path, ".ai/runtime/attach.lock.json")
               || HasPrefix(path, ".ai/runtime/live-state/")
               || HasPrefix(path, ".ai/runtime/tmp/")
               || HasPrefix(path, ".ai/tmp/")
               || HasPrefix(path, ".ai/worktrees/")
               || HasPrefix(path, ".carves-platform/")
               || HasPrefix(path, ".carves-agent/")
               || HasPrefix(path, ".carves-worktrees/")
               || HasPrefix(path, ".vs/")
               || HasPrefix(path, ".idea/")
               || HasPrefix(path, ".vscode/")
               || HasPrefix(path, "bin/")
               || HasPrefix(path, "obj/")
               || HasPrefix(path, "TestResults/")
               || HasPrefix(path, "coverage/");
    }

    private static string ResolveCommitPosture(string pathClass)
    {
        return pathClass switch
        {
            "official_target_truth" => "commit_candidate",
            "target_output_candidate" => "commit_candidate_after_review_match",
            "local_or_tooling_residue" => "exclude_from_commit_by_default",
            _ => "operator_review_first",
        };
    }

    private static string ResolveRecommendedAction(string pathClass)
    {
        return pathClass switch
        {
            "official_target_truth" => "Stage only if it is current CARVES target truth produced by Runtime commands.",
            "target_output_candidate" => "Stage only if it matches an approved Runtime writeback or explicit operator-approved target output.",
            "local_or_tooling_residue" => "Do not commit by default; clean, ignore, or leave local according to operator policy.",
            _ => "Do not stage until the operator classifies this path.",
        };
    }

    private static IEnumerable<string> BuildGaps(bool runtimeInitialized, bool gitRepositoryDetected, IReadOnlyList<RuntimeTargetCommitHygienePathSurface> unclassified)
    {
        if (!runtimeInitialized)
        {
            yield return "runtime_not_initialized";
        }

        if (!gitRepositoryDetected)
        {
            yield return "git_repository_not_detected";
        }

        if (unclassified.Count > 0)
        {
            yield return "unclassified_dirty_paths_require_operator_review";
            foreach (var path in unclassified)
            {
                yield return $"unclassified:{path.Path}";
            }
        }
    }

    private static string ResolvePosture(
        bool runtimeInitialized,
        bool gitRepositoryDetected,
        int dirtyPathCount,
        int commitCandidateCount,
        int localResidueCount,
        int unclassifiedCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return gitRepositoryDetected
                ? "target_commit_hygiene_blocked_by_surface_gaps"
                : "target_commit_hygiene_blocked_by_git_repo_gap";
        }

        if (!runtimeInitialized)
        {
            return "target_commit_hygiene_blocked_by_runtime_init";
        }

        if (dirtyPathCount == 0)
        {
            return "target_commit_hygiene_clean";
        }

        if (unclassifiedCount > 0)
        {
            return "target_commit_hygiene_operator_review_required";
        }

        if (commitCandidateCount > 0 && localResidueCount > 0)
        {
            return "target_commit_hygiene_ready_with_exclusions";
        }

        if (commitCandidateCount > 0)
        {
            return "target_commit_hygiene_ready";
        }

        return "target_commit_hygiene_local_residue_only";
    }

    private static string BuildSummary(
        bool runtimeInitialized,
        bool gitRepositoryDetected,
        int dirtyPathCount,
        int commitCandidateCount,
        int localResidueCount,
        int unclassifiedCount)
    {
        if (!gitRepositoryDetected)
        {
            return "Target commit hygiene cannot read git status because the current directory is not a git work tree.";
        }

        if (!runtimeInitialized)
        {
            return "Target commit hygiene waits for `.ai/runtime.json`; run attach/init before commit closure.";
        }

        if (dirtyPathCount == 0)
        {
            return "The target git work tree is clean; no product proof commit is currently required.";
        }

        if (unclassifiedCount > 0)
        {
            return "Dirty paths include unclassified files; operator review is required before staging.";
        }

        if (commitCandidateCount > 0 && localResidueCount > 0)
        {
            return "Commit candidates are present, but local residue must be excluded from the target product commit.";
        }

        if (commitCandidateCount > 0)
        {
            return "Dirty paths are classified as official target truth or target output candidates.";
        }

        return "Only local/tooling residue is dirty; no target product commit candidate is present.";
    }

    private static string BuildRecommendedNextAction(
        bool runtimeInitialized,
        bool gitRepositoryDetected,
        int commitCandidateCount,
        int localResidueCount,
        int unclassifiedCount)
    {
        if (!gitRepositoryDetected)
        {
            return "Run this command from an attached git target repo.";
        }

        if (!runtimeInitialized)
        {
            return "carves init [target-path] --json";
        }

        if (unclassifiedCount > 0)
        {
            return "Review unclassified dirty paths before any git add or commit.";
        }

        if (commitCandidateCount > 0)
        {
            return localResidueCount > 0
                ? "Stage only commit_candidate paths, exclude local_or_tooling_residue paths, then git commit with an operator-reviewed message."
                : "Stage commit_candidate paths, then git commit with an operator-reviewed message.";
        }

        if (localResidueCount > 0)
        {
            return "Do not commit local/tooling residue by default; clean or ignore it only after operator review.";
        }

        return "no_commit_required";
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private static string NormalizeGitPath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static bool PathEquals(string path, string candidate)
    {
        return string.Equals(path, candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPrefix(string path, string prefix)
    {
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record GitStatusReadResult(bool GitRepositoryDetected, IReadOnlyList<GitStatusEntry> Entries, string? Error);

    private sealed record GitStatusEntry(string StatusCode, string Path);

    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
}
