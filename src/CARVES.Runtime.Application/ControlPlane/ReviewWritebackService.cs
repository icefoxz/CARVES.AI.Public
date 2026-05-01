using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class ReviewWritebackService
{
    private readonly string repoRoot;
    private readonly Carves.Runtime.Application.Git.IGitClient gitClient;
    private readonly ManagedWorkspacePathPolicyService? managedWorkspacePathPolicyService;
    private readonly MemoryPatternWritebackRouteAuthorizationService? memoryPatternWritebackRouteAuthorizationService;

    public ReviewWritebackService(
        string repoRoot,
        Carves.Runtime.Application.Git.IGitClient gitClient,
        ManagedWorkspacePathPolicyService? managedWorkspacePathPolicyService = null,
        MemoryPatternWritebackRouteAuthorizationService? memoryPatternWritebackRouteAuthorizationService = null)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.gitClient = gitClient;
        this.managedWorkspacePathPolicyService = managedWorkspacePathPolicyService;
        this.memoryPatternWritebackRouteAuthorizationService = memoryPatternWritebackRouteAuthorizationService;
    }

    public ReviewWritebackAttempt Apply(
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact,
        string decisionLabel = "approved")
    {
        var planResult = PlanWriteback(reviewArtifact, workerArtifact);
        if (!planResult.CanProceed)
        {
            return ReviewWritebackAttempt.Block(
                planResult.FailureMessage ?? $"Review writeback failed for {reviewArtifact.TaskId}.");
        }

        var plan = planResult.Plan;
        if (plan is null || !plan.ApplyRequested)
        {
            return ReviewWritebackAttempt.Allow(new ReviewWritebackRecord
            {
                Applied = false,
                Summary = "No delegated repo writeback was required.",
            });
        }

        foreach (var writeTarget in plan.Targets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(writeTarget.TargetPath)!);
            File.Copy(writeTarget.SourcePath, writeTarget.TargetPath, overwrite: true);
        }

        var resultCommit = CaptureResultCommit(
            reviewArtifact.TaskId,
            plan.WorktreePath,
            plan.DistinctFiles,
            decisionLabel,
            plan.DelegatedGitWorktree);
        if (plan.DelegatedGitWorktree && string.IsNullOrWhiteSpace(resultCommit))
        {
            return ReviewWritebackAttempt.Block(
                $"Cannot approve review for {reviewArtifact.TaskId}: delegated git worktree writeback could not produce a result commit.");
        }

        var summary = string.IsNullOrWhiteSpace(resultCommit)
            ? $"Materialized {plan.DistinctFiles.Count} {decisionLabel} file(s) from delegated worktree into repo root."
            : $"Materialized {plan.DistinctFiles.Count} {decisionLabel} file(s) from delegated worktree into repo root and captured result commit {resultCommit}.";

        CleanupDelegatedWorktree(plan.WorktreePath);

        return ReviewWritebackAttempt.Allow(new ReviewWritebackRecord
        {
            Applied = true,
            AppliedAt = DateTimeOffset.UtcNow,
            SourcePath = Path.GetFullPath(plan.WorktreePath),
            ResultCommit = resultCommit,
            Files = plan.DistinctFiles,
            Summary = summary,
        });
    }

    public ReviewWritebackPreview Preview(
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact)
    {
        var planResult = PlanWriteback(reviewArtifact, workerArtifact);
        if (!planResult.CanProceed)
        {
            return ReviewWritebackPreview.Block(planResult.FailureMessage ?? $"Review writeback failed for {reviewArtifact.TaskId}.");
        }

        var plan = planResult.Plan;
        if (plan is null || !plan.ApplyRequested)
        {
            return ReviewWritebackPreview.Allow(
                willApply: false,
                willCaptureResultCommit: false,
                files: Array.Empty<string>());
        }

        return ReviewWritebackPreview.Allow(
            willApply: true,
            willCaptureResultCommit: plan.WillCaptureResultCommit,
            files: plan.DistinctFiles);
    }

    private WritebackPlanResult PlanWriteback(
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact)
    {
        if (workerArtifact is null || workerArtifact.Evidence.FilesWritten.Count == 0)
        {
            return WritebackPlanResult.Allow(new ReviewWritebackPlan(
                ApplyRequested: false,
                WorktreePath: string.Empty,
                DistinctFiles: Array.Empty<string>(),
                Targets: Array.Empty<ReviewWritebackTarget>(),
                DelegatedGitWorktree: false,
                WillCaptureResultCommit: false));
        }

        if (!reviewArtifact.ValidationPassed)
        {
            return WritebackPlanResult.Block(
                $"Cannot approve review for {reviewArtifact.TaskId}: delegated result has not passed validation.");
        }

        if (reviewArtifact.SafetyOutcome != SafetyOutcome.Allow)
        {
            return WritebackPlanResult.Block(
                $"Cannot approve review for {reviewArtifact.TaskId}: safety outcome is {reviewArtifact.SafetyOutcome}.");
        }

        var worktreePath = workerArtifact.Evidence.WorktreePath;
        if (string.IsNullOrWhiteSpace(worktreePath) || !Directory.Exists(worktreePath))
        {
            return WritebackPlanResult.Block(
                $"Cannot approve review for {reviewArtifact.TaskId}: delegated worktree is unavailable for writeback.");
        }

        var normalizedFiles = new List<string>();
        foreach (var file in workerArtifact.Evidence.FilesWritten)
        {
            var normalized = NormalizeRepoRelativePath(file);
            if (normalized is null)
            {
                return WritebackPlanResult.Block(
                    $"Cannot approve review for {reviewArtifact.TaskId}: delegated result requested protected or invalid path '{file}'.");
            }

            normalizedFiles.Add(normalized);
        }

        var distinctFiles = normalizedFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var patternWritebackAuthorization = memoryPatternWritebackRouteAuthorizationService?.Evaluate(distinctFiles, workerArtifact)
            ?? MemoryPatternWritebackRouteAuthorizationAssessment.NotApplicable;
        if (patternWritebackAuthorization.Applies
            && patternWritebackAuthorization.UnauthorizedPaths.Count > 0)
        {
            return WritebackPlanResult.Block(
                $"Cannot approve review for {reviewArtifact.TaskId}: {patternWritebackAuthorization.Summary}");
        }

        foreach (var relativePath in distinctFiles)
        {
            if (managedWorkspacePathPolicyService is null
                && IsLegacyProtectedPath(relativePath)
                && !patternWritebackAuthorization.IsAuthorized(relativePath))
            {
                return WritebackPlanResult.Block(
                    $"Cannot approve review for {reviewArtifact.TaskId}: delegated result requested protected or invalid path '{relativePath}'.");
            }
        }

        var policyPaths = distinctFiles
            .Where(path => !patternWritebackAuthorization.IsAuthorized(path))
            .ToArray();
        var pathPolicy = managedWorkspacePathPolicyService?.Evaluate(reviewArtifact.TaskId, policyPaths);
        if (pathPolicy is not null
            && (pathPolicy.ScopeEscapeCount > 0
                || pathPolicy.HostOnlyCount > 0
                || pathPolicy.DenyCount > 0))
        {
            return WritebackPlanResult.Block(
                $"Cannot approve review for {reviewArtifact.TaskId}: {pathPolicy.Summary} {pathPolicy.RecommendedNextAction}.");
        }

        var targets = new List<ReviewWritebackTarget>(distinctFiles.Length);
        foreach (var relativePath in distinctFiles)
        {
            if (!TryResolvePathUnderRoot(worktreePath, relativePath, out var sourcePath))
            {
                return WritebackPlanResult.Block(
                    $"Cannot approve review for {reviewArtifact.TaskId}: delegated source path '{relativePath}' escapes the worktree.");
            }

            if (!File.Exists(sourcePath))
            {
                return WritebackPlanResult.Block(
                    $"Cannot approve review for {reviewArtifact.TaskId}: delegated source file '{relativePath}' is missing from the worktree.");
            }

            if (!TryResolvePathUnderRoot(repoRoot, relativePath, out var targetPath))
            {
                return WritebackPlanResult.Block(
                    $"Cannot approve review for {reviewArtifact.TaskId}: repo writeback path '{relativePath}' escapes the repo root.");
            }

            targets.Add(new ReviewWritebackTarget(relativePath, sourcePath, targetPath));
        }

        var delegatedGitWorktree = IsDelegatedGitWorktree(worktreePath);
        var gitRepositoryDetected = gitClient.IsRepository(worktreePath);
        if (delegatedGitWorktree && !gitRepositoryDetected)
        {
            return WritebackPlanResult.Block(
                $"Cannot approve review for {reviewArtifact.TaskId}: delegated git worktree writeback could not produce a result commit.");
        }

        return WritebackPlanResult.Allow(new ReviewWritebackPlan(
            ApplyRequested: true,
            WorktreePath: worktreePath,
            DistinctFiles: distinctFiles,
            Targets: targets,
            DelegatedGitWorktree: delegatedGitWorktree,
            WillCaptureResultCommit: delegatedGitWorktree && gitRepositoryDetected));
    }

    private string? CaptureResultCommit(
        string taskId,
        string worktreePath,
        IReadOnlyList<string> files,
        string decisionLabel,
        bool delegatedGitWorktree)
    {
        if (!delegatedGitWorktree || !gitClient.IsRepository(worktreePath))
        {
            return null;
        }

        return gitClient.TryCreateScopedSnapshotCommit(
            worktreePath,
            files,
            $"CARVES {decisionLabel} result for {taskId}");
    }

    private void CleanupDelegatedWorktree(string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath) || !Directory.Exists(worktreePath))
        {
            return;
        }

        try
        {
            var manifest = TryReadWorktreeManifest(worktreePath);
            var manifestRepoRoot = manifest?.RepoRoot;
            if (string.Equals(manifest?.Mode, "git", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(manifestRepoRoot))
            {
                gitClient.TryRemoveWorktree(manifestRepoRoot, worktreePath);
                return;
            }

            Directory.Delete(worktreePath, recursive: true);
        }
        catch
        {
            // Approval already materialized the bounded patch; cleanup failure should not block writeback truth.
        }
    }

    private static WorktreeManifest? TryReadWorktreeManifest(string worktreePath)
    {
        try
        {
            var manifestPath = Path.Combine(worktreePath, ".carves-worktree.json");
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
            var repoRoot = document.RootElement.TryGetProperty("RepoRoot", out var repoRootProperty)
                ? repoRootProperty.GetString()
                : null;
            var mode = document.RootElement.TryGetProperty("Mode", out var modeProperty)
                ? modeProperty.GetString()
                : null;
            return new WorktreeManifest(repoRoot, mode);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadRepoRootFromManifest(string worktreePath)
    {
        return TryReadWorktreeManifest(worktreePath)?.RepoRoot;
    }

    private static bool IsDelegatedGitWorktree(string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
        {
            return false;
        }

        if (File.Exists(Path.Combine(worktreePath, ".git"))
            || Directory.Exists(Path.Combine(worktreePath, ".git")))
        {
            return true;
        }

        var manifest = TryReadWorktreeManifest(worktreePath);
        return string.Equals(manifest?.Mode, "git", StringComparison.OrdinalIgnoreCase);
    }

    private string? NormalizeRepoRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var candidate = path.Replace('\\', '/').Trim();
        if (candidate.StartsWith("/", StringComparison.Ordinal) || Path.IsPathRooted(candidate))
        {
            return null;
        }

        if (!TryResolvePathUnderRoot(repoRoot, candidate, out var resolvedPath))
        {
            return null;
        }

        var relativePath = Path.GetRelativePath(repoRoot, resolvedPath).Replace('\\', '/');
        var firstSegment = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (firstSegment is null)
        {
            return null;
        }

        return relativePath;
    }

    private static bool IsLegacyProtectedPath(string relativePath)
    {
        var firstSegment = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstSegment is null
               || firstSegment.Equals(".ai", StringComparison.OrdinalIgnoreCase)
               || firstSegment.Equals(".git", StringComparison.OrdinalIgnoreCase)
               || firstSegment.Equals(".carves-platform", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolvePathUnderRoot(string rootPath, string relativePath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        var fullRoot = Path.GetFullPath(rootPath);
        var fullRootWithSeparator = EnsureTrailingSeparator(fullRoot);
        var combinedPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!combinedPath.StartsWith(fullRootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(combinedPath, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        resolvedPath = combinedPath;
        return true;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
               || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}

public sealed record ReviewWritebackAttempt(bool CanProceed, ReviewWritebackRecord Record, string? FailureMessage)
{
    public static ReviewWritebackAttempt Allow(ReviewWritebackRecord record)
    {
        return new ReviewWritebackAttempt(true, record, null);
    }

    public static ReviewWritebackAttempt Block(string failureMessage)
    {
        return new ReviewWritebackAttempt(
            false,
            new ReviewWritebackRecord
            {
                Applied = false,
                Summary = failureMessage,
            },
            failureMessage);
    }
}

public sealed record ReviewWritebackPreview(
    bool CanProceed,
    bool WillApply,
    bool WillCaptureResultCommit,
    IReadOnlyList<string> Files,
    string? FailureMessage)
{
    public static ReviewWritebackPreview Allow(
        bool willApply,
        bool willCaptureResultCommit,
        IReadOnlyList<string> files)
    {
        return new ReviewWritebackPreview(true, willApply, willCaptureResultCommit, files, null);
    }

    public static ReviewWritebackPreview Block(string failureMessage)
    {
        return new ReviewWritebackPreview(false, false, false, Array.Empty<string>(), failureMessage);
    }

    public ReviewWritebackEvidenceProjection ToEvidenceProjection()
    {
        return new ReviewWritebackEvidenceProjection(WillApply, WillCaptureResultCommit, Files);
    }
}

public sealed record ReviewWritebackEvidenceProjection(
    bool WillApply,
    bool WillCaptureResultCommit,
    IReadOnlyList<string> Files);

internal sealed record ReviewWritebackPlan(
    bool ApplyRequested,
    string WorktreePath,
    IReadOnlyList<string> DistinctFiles,
    IReadOnlyList<ReviewWritebackTarget> Targets,
    bool DelegatedGitWorktree,
    bool WillCaptureResultCommit);

internal sealed record ReviewWritebackTarget(string RelativePath, string SourcePath, string TargetPath);

internal sealed record WorktreeManifest(string? RepoRoot, string? Mode);

internal sealed record WritebackPlanResult(bool CanProceed, ReviewWritebackPlan? Plan, string? FailureMessage)
{
    public static WritebackPlanResult Allow(ReviewWritebackPlan plan)
    {
        return new WritebackPlanResult(true, plan, null);
    }

    public static WritebackPlanResult Block(string failureMessage)
    {
        return new WritebackPlanResult(false, null, failureMessage);
    }
}
