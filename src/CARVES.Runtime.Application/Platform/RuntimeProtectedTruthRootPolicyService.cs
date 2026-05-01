using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeProtectedTruthRootPolicyService
{
    private const string PolicyDocumentPath = "docs/runtime/runtime-protected-truth-root-policy.md";
    private const string ProjectBoundaryPath = ".ai/PROJECT_BOUNDARY.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeProtectedTruthRootPolicyService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeProtectedTruthRootPolicySurface Build()
    {
        var errors = new List<string>();
        ValidatePath(documentRoot.DocumentRoot, PolicyDocumentPath, "Protected truth-root policy document", errors);
        ValidatePath(ProjectBoundaryPath, "Project boundary document", errors);

        return new RuntimeProtectedTruthRootPolicySurface
        {
            PolicyDocumentPath = PolicyDocumentPath,
            ProjectBoundaryPath = ProjectBoundaryPath,
            OverallPosture = errors.Count == 0
                ? "protected_truth_root_policy_ready"
                : "blocked_by_protected_truth_root_policy_gaps",
            ProtectedRoots = BuildProtectedRoots(),
            DeniedRoots = BuildDeniedRoots(),
            RecommendedNextAction = errors.Count == 0
                ? "Keep protected truth root mutations host-routed; worker or adapter returned material must remove these paths or request governed replan/writeback."
                : "Restore the protected-root policy anchors before relying on path-policy enforcement projection.",
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This policy does not classify every repository path as protected truth.",
                "This policy does not block Runtime-owned commands from updating governed truth through their normal write channels.",
                "This policy does not depend on Codex, Claude, Cursor, Copilot, or IDE-specific permission files.",
            ],
        };
    }

    public static IReadOnlyList<RuntimeProtectedTruthRootSurface> BuildProtectedRoots()
    {
        return
        [
            BuildRoot(
                ".ai/tasks/",
                "task_truth",
                "plan/task/review Runtime commands and markdown sync",
                "block_before_writeback",
                "Remove direct task-truth writes from returned material; use governed planning, review, or sync commands.",
                ["nodes/*.json", "cards/*.md", "graph.json"]),
            BuildRoot(
                ".ai/memory/",
                "memory_truth",
                "Runtime-governed memory update or explicit operator-approved memory edit",
                "block_before_writeback",
                "Route memory mutations through governed memory update flow or operator review, not worker patch return.",
                ["architecture/*.md", "modules/*.md"]),
            BuildRoot(
                ".ai/artifacts/reviews/",
                "review_truth",
                "planner review artifact creation and review lifecycle commands",
                "block_before_writeback",
                "Let Runtime create review artifacts; workers should return evidence, not review decisions.",
                ["review artifacts", "approval decision evidence"]),
            BuildRoot(
                ".carves-platform/",
                "platform_truth",
                "Runtime platform registry/governance commands",
                "block_before_writeback",
                "Use platform/governance commands for platform state; do not return platform truth edits as workspace output.",
                ["runtime registry", "platform governance state"]),
        ];
    }

    public static IReadOnlyList<RuntimeProtectedTruthRootSurface> BuildDeniedRoots()
    {
        return
        [
            BuildRoot(
                ".git/",
                "vcs_internal",
                "none",
                "deny_without_review_or_writeback",
                "Remove VCS-internal writes from the result and recreate the work through normal git operations.",
                ["objects", "refs", "index"]),
            BuildRoot(
                ".vs/",
                "machine_local_state",
                "none",
                "deny_without_review_or_writeback",
                "Remove machine-local IDE state from returned material.",
                ["IDE cache"]),
            BuildRoot(
                ".idea/",
                "machine_local_state",
                "none",
                "deny_without_review_or_writeback",
                "Remove machine-local IDE state from returned material.",
                ["IDE cache"]),
            BuildRoot(
                "secret-like paths",
                "secret_material",
                "none",
                "deny_without_review_or_writeback",
                "Remove secret material and rotate/review credentials outside worker result flow when needed.",
                [".env", "*.pfx", "*.snk", "secrets.json"]),
        ];
    }

    public static RuntimeProtectedPathViolationSurface ClassifyViolation(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        var root = BuildProtectedRoots().FirstOrDefault(item => PathMatchesRoot(item.Root, normalized))
            ?? BuildDeniedRoots().FirstOrDefault(item => PathMatchesRoot(item.Root, normalized));

        return new RuntimeProtectedPathViolationSurface
        {
            Path = normalized,
            ProtectedClassification = root?.Classification ?? "unclassified_protected_path",
            RemediationAction = root?.RemediationAction ?? "Remove the protected path from returned material or request a governed replan/writeback.",
        };
    }

    private static RuntimeProtectedTruthRootSurface BuildRoot(
        string root,
        string classification,
        string allowedMutationChannel,
        string unauthorizedMutationOutcome,
        string remediationAction,
        IReadOnlyList<string> examples)
    {
        return new RuntimeProtectedTruthRootSurface
        {
            Root = root,
            Classification = classification,
            AllowedMutationChannel = allowedMutationChannel,
            UnauthorizedMutationOutcome = unauthorizedMutationOutcome,
            RemediationAction = remediationAction,
            Examples = examples,
        };
    }

    private static bool PathMatchesRoot(string root, string path)
    {
        if (string.Equals(root, "secret-like paths", StringComparison.Ordinal))
        {
            return IsSecretLikePath(path);
        }

        var normalizedRoot = root.Replace('\\', '/').Trim().TrimEnd('/');
        return string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith($"{normalizedRoot}/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSecretLikePath(string normalizedPath)
    {
        var fileName = Path.GetFileName(normalizedPath);
        return fileName.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".snk", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/secrets/", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("secrets.json", StringComparison.OrdinalIgnoreCase);
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        ValidatePath(repoRoot, repoRelativePath, label, errors);
    }

    private static void ValidatePath(string root, string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(root, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
