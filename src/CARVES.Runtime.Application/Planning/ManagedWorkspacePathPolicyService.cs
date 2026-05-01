using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed class ManagedWorkspacePathPolicyService
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly IReadOnlyList<string> HostOnlyRoots =
    [
        ".ai/tasks",
        ".ai/memory",
        ".ai/artifacts/reviews",
        ".carves-platform",
    ];

    private static readonly IReadOnlyList<string> ReviewRequiredRoots =
    [
        ".ai/artifacts",
    ];

    private static readonly IReadOnlyList<string> DenyRoots =
    [
        ".git",
        ".vs",
        ".idea",
    ];

    private readonly string repoRoot;
    private readonly IManagedWorkspaceLeaseRepository repository;

    public ManagedWorkspacePathPolicyService(string repoRoot, IManagedWorkspaceLeaseRepository repository)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.repository = repository;
    }

    public ManagedWorkspacePathPolicyAssessment Evaluate(TaskNode task, IEnumerable<string> changedPaths)
    {
        return Evaluate(task.TaskId, changedPaths);
    }

    public ManagedWorkspacePathPolicyAssessment Evaluate(string taskId, IEnumerable<string> changedPaths)
    {
        var lease = repository.Load().Leases
            .Where(existing => existing.Status == ManagedWorkspaceLeaseStatus.Active)
            .Where(existing => string.Equals(existing.TaskId, taskId, StringComparison.Ordinal))
            .OrderByDescending(existing => existing.CreatedAt)
            .FirstOrDefault();

        var touchedPaths = changedPaths
            .Select(path => Classify(path, lease))
            .Where(item => !string.IsNullOrWhiteSpace(item.Path))
            .DistinctBy(item => item.Path, PathComparer)
            .OrderBy(item => item.Path, PathComparer)
            .ToArray();

        var workspaceOpenCount = touchedPaths.Count(item => string.Equals(item.PolicyClass, "workspace_open", StringComparison.Ordinal));
        var reviewRequiredCount = touchedPaths.Count(item => string.Equals(item.PolicyClass, "review_required", StringComparison.Ordinal));
        var scopeEscapeCount = touchedPaths.Count(item => string.Equals(item.PolicyClass, "scope_escape", StringComparison.Ordinal));
        var hostOnlyCount = touchedPaths.Count(item => string.Equals(item.PolicyClass, "host_only", StringComparison.Ordinal));
        var denyCount = touchedPaths.Count(item => string.Equals(item.PolicyClass, "deny", StringComparison.Ordinal));

        var status = ResolveStatus(scopeEscapeCount, hostOnlyCount, denyCount, reviewRequiredCount, touchedPaths.Length, lease is not null);
        var summary = ResolveSummary(status, touchedPaths, lease);
        var nextAction = ResolveRecommendedNextAction(status, lease, touchedPaths);

        return new ManagedWorkspacePathPolicyAssessment
        {
            EnforcementActive = lease is not null || hostOnlyCount > 0 || denyCount > 0,
            LeaseAware = lease is not null,
            LeaseId = lease?.LeaseId,
            Status = status,
            Summary = summary,
            RecommendedNextAction = nextAction,
            AllowedWritablePaths = lease?.AllowedWritablePaths ?? Array.Empty<string>(),
            TouchedPaths = touchedPaths,
            WorkspaceOpenCount = workspaceOpenCount,
            ReviewRequiredCount = reviewRequiredCount,
            ScopeEscapeCount = scopeEscapeCount,
            HostOnlyCount = hostOnlyCount,
            DenyCount = denyCount,
        };
    }

    public static IReadOnlyList<ManagedWorkspacePathPolicyRule> BuildDefaultRules()
    {
        return
        [
            new ManagedWorkspacePathPolicyRule
            {
                PolicyClass = "workspace_open",
                Summary = "Task-bound code and support files inside the issued workspace may be edited freely within the declared writable scope.",
                EnforcementEffect = "allow_inside_active_lease_scope",
                Examples = ["src/", "tests/", "task-bound docs"],
            },
            new ManagedWorkspacePathPolicyRule
            {
                PolicyClass = "review_required",
                Summary = "Generated assets, high-risk config, and scope-expanding edits may be prepared in the workspace but still require review before official ingress.",
                EnforcementEffect = "allow_preparation_but_require_host_review_before_ingress",
                Examples = [".ai/artifacts/", "generated outputs", "scope-expanding config deltas"],
            },
            new ManagedWorkspacePathPolicyRule
            {
                PolicyClass = "scope_escape",
                Summary = "Repo-valid edits outside the active lease writable scope are not accepted as ordinary workspace output and require replan before writeback.",
                EnforcementEffect = "fail_closed_and_require_replan",
                Examples = ["docs/ when only src/ is leased", "new subsystem outside task scope"],
            },
            new ManagedWorkspacePathPolicyRule
            {
                PolicyClass = "host_only",
                Summary = "Governed truth, review truth, and approval truth remain host-routed even when a leased workspace exists.",
                EnforcementEffect = "fail_closed_and_route_to_host_writeback",
                Examples = [".ai/tasks/", ".ai/memory/", ".ai/artifacts/reviews/", ".carves-platform/"],
            },
            new ManagedWorkspacePathPolicyRule
            {
                PolicyClass = "deny",
                Summary = "Secrets, VCS internals, and system-level files are outside the managed workspace mutation contract.",
                EnforcementEffect = "deny_without_review_or_writeback",
                Examples = [".git/", "secret material", "machine-level config"],
            },
        ];
    }

    private ManagedWorkspaceTouchedPath Classify(string rawPath, ManagedWorkspaceLease? lease)
    {
        if (!TryNormalizePath(rawPath, out var normalized))
        {
            return new ManagedWorkspaceTouchedPath
            {
                Path = string.IsNullOrWhiteSpace(rawPath) ? "(invalid)" : rawPath.Replace('\\', '/').Trim(),
                PolicyClass = "deny",
                AssetClass = "denied_root",
                Summary = "The path escaped the repo root or used an invalid absolute location.",
            };
        }

        if (IsUnderRoots(normalized, DenyRoots) || IsSecretLikePath(normalized))
        {
            return new ManagedWorkspaceTouchedPath
            {
                Path = normalized,
                PolicyClass = "deny",
                AssetClass = "denied_root",
                Summary = "Denied roots and secret-like files are outside the managed workspace mutation contract.",
            };
        }

        if (IsUnderRoots(normalized, HostOnlyRoots))
        {
            return new ManagedWorkspaceTouchedPath
            {
                Path = normalized,
                PolicyClass = "host_only",
                AssetClass = "truth_root",
                Summary = "Governed truth roots stay host-routed even when a managed workspace lease exists.",
            };
        }

        if (IsUnderRoots(normalized, ReviewRequiredRoots))
        {
            return new ManagedWorkspaceTouchedPath
            {
                Path = normalized,
                PolicyClass = "review_required",
                AssetClass = "generated",
                Summary = "Runtime artifact and generated paths are review-required before official ingress.",
            };
        }

        if (lease is not null
            && lease.AllowedWritablePaths.Count > 0
            && !lease.AllowedWritablePaths.Any(scope => PathMatchesScope(scope, normalized)))
        {
            return new ManagedWorkspaceTouchedPath
            {
                Path = normalized,
                PolicyClass = "scope_escape",
                AssetClass = ClassifyAssetClass(normalized),
                Summary = "The changed path is outside the writable scope declared by the active managed workspace lease.",
            };
        }

        return new ManagedWorkspaceTouchedPath
        {
            Path = normalized,
            PolicyClass = IsGeneratedPath(normalized) ? "review_required" : "workspace_open",
            AssetClass = ClassifyAssetClass(normalized),
            Summary = IsGeneratedPath(normalized)
                ? "Generated or lockfile-like output was prepared inside the workspace and still requires review at ingress."
                : "The path stayed within the active writable scope.",
        };
    }

    private bool TryNormalizePath(string rawPath, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return false;
        }

        try
        {
            var candidate = rawPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim();
            var fullPath = Path.IsPathRooted(candidate)
                ? Path.GetFullPath(candidate)
                : Path.GetFullPath(Path.Combine(repoRoot, candidate));
            var repoRootWithSeparator = EnsureTrailingSeparator(repoRoot);
            if (!fullPath.StartsWith(repoRootWithSeparator, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullPath, repoRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            normalized = Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');
            return !string.IsNullOrWhiteSpace(normalized) && !string.Equals(normalized, ".", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveStatus(
        int scopeEscapeCount,
        int hostOnlyCount,
        int denyCount,
        int reviewRequiredCount,
        int totalTouchedPaths,
        bool leaseAware)
    {
        if (denyCount > 0)
        {
            return "deny";
        }

        if (hostOnlyCount > 0)
        {
            return "host_only";
        }

        if (scopeEscapeCount > 0)
        {
            return "scope_escape";
        }

        if (reviewRequiredCount > 0)
        {
            return "review_required";
        }

        if (totalTouchedPaths > 0)
        {
            return "workspace_open";
        }

        return leaseAware ? "clear" : "not_required";
    }

    private static string ResolveSummary(string status, IReadOnlyList<ManagedWorkspaceTouchedPath> touchedPaths, ManagedWorkspaceLease? lease)
    {
        string Describe(params string[] policyClasses)
        {
            return string.Join(", ", touchedPaths
                .Where(item => policyClasses.Contains(item.PolicyClass, StringComparer.Ordinal))
                .Select(item => item.Path));
        }

        return status switch
        {
            "deny" => $"Managed workspace path policy denied the observed path set: {Describe("deny")}.",
            "host_only" => $"Managed workspace path policy reserved host-only truth roots: {Describe("host_only")}.",
            "scope_escape" => $"Managed workspace lease '{lease?.LeaseId ?? "(none)"}' observed scope escape outside declared writable paths: {Describe("scope_escape")}.",
            "review_required" => $"Managed workspace path policy marked review-required paths: {Describe("review_required")}.",
            "workspace_open" => $"Managed workspace lease '{lease?.LeaseId ?? "(none)"}' stayed within the declared writable scope.",
            "clear" => $"Managed workspace lease '{lease?.LeaseId ?? "(none)"}' recorded no changed paths.",
            _ => "Managed workspace path policy enforcement did not apply to the current path set.",
        };
    }

    private static string ResolveRecommendedNextAction(string status, ManagedWorkspaceLease? lease, IReadOnlyList<ManagedWorkspaceTouchedPath> touchedPaths)
    {
        return status switch
        {
            "deny" => "remove denied-root or secret-like writes from the result before any further review or writeback",
            "host_only" => "route governed truth mutations through host-routed review/writeback instead of editing them in the workspace",
            "scope_escape" => lease is null
                ? "re-evaluate the task scope before widening file changes"
                : $"replan before widening beyond the active lease '{lease.LeaseId}' writable scope",
            "review_required" => $"keep review-required outputs under review: {string.Join(", ", touchedPaths.Where(item => item.PolicyClass == "review_required").Select(item => item.Path))}",
            "workspace_open" => "continue inside the leased workspace and return changes through review/writeback",
            "clear" => "continue the current task-bound workspace flow",
            _ => "observe current state",
        };
    }

    private static string ClassifyAssetClass(string normalizedPath)
    {
        if (normalizedPath.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return "documentation";
        }

        if (normalizedPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return "structured_text";
        }

        if (IsGeneratedPath(normalizedPath))
        {
            return "generated";
        }

        return "code";
    }

    private static bool IsGeneratedPath(string normalizedPath)
    {
        var fileName = Path.GetFileName(normalizedPath);
        return normalizedPath.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("package-lock.json", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("pnpm-lock.yaml", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("yarn.lock", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsUnderRoots(string normalizedPath, IReadOnlyList<string> roots)
    {
        return roots.Any(root => PathMatchesScope(root, normalizedPath));
    }

    private static bool PathMatchesScope(string scope, string observedPath)
    {
        var normalizedScope = scope.Replace('\\', '/').Trim();
        var normalizedPath = observedPath.Replace('\\', '/').Trim();
        if (string.Equals(normalizedScope, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedScope.EndsWith("/**", StringComparison.Ordinal))
        {
            var prefix = normalizedScope[..^3].TrimEnd('/');
            return normalizedPath.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedPath, prefix, StringComparison.OrdinalIgnoreCase);
        }

        var root = normalizedScope.TrimEnd('/');
        return normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
               || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
