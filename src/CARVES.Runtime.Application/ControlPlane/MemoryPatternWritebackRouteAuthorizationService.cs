using System.Text.Json;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class MemoryPatternWritebackRouteAuthorizationService
{
    private const string ExpectedSchemaVersion = "benchmark-memory-pattern-writeback-route.v1";
    private const string ExpectedRouteStatus = "completed";
    private const string ExpectedRouteDecision = "host_writeback_line_required";
    private const string ExpectedCurrentPosture = "durable_markdown_writeback_input_ready";
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly string repoRoot;
    private readonly string routeSearchRoot;

    public MemoryPatternWritebackRouteAuthorizationService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        routeSearchRoot = Path.Combine(this.repoRoot, "artifacts", "bench", "memory-maturity");
    }

    public MemoryPatternWritebackRouteAuthorizationAssessment Evaluate(
        IEnumerable<string> changedPaths,
        WorkerExecutionArtifact? workerArtifact)
    {
        var patternPaths = changedPaths
            .Select(NormalizePath)
            .Where(static path => path.StartsWith(".ai/memory/patterns/", StringComparison.OrdinalIgnoreCase))
            .Distinct(PathComparer)
            .OrderBy(static path => path, PathComparer)
            .ToArray();
        if (patternPaths.Length == 0)
        {
            return MemoryPatternWritebackRouteAuthorizationAssessment.NotApplicable;
        }

        var worktreePath = workerArtifact?.Evidence.WorktreePath;
        if (string.IsNullOrWhiteSpace(worktreePath) || !Directory.Exists(worktreePath))
        {
            return new MemoryPatternWritebackRouteAuthorizationAssessment(
                Applies: true,
                AllTouchedPathsAuthorized: false,
                AuthorizedPaths: Array.Empty<string>(),
                UnauthorizedPaths: patternPaths,
                Summary: "Pattern markdown writeback route cannot be authorized because the delegated worktree is unavailable.");
        }

        var routeByTargetPath = LoadEligibleRoutes();
        var authorizedPaths = new List<string>();
        var unauthorizedPaths = new List<string>();

        foreach (var patternPath in patternPaths)
        {
            if (!routeByTargetPath.TryGetValue(patternPath, out var route))
            {
                unauthorizedPaths.Add(patternPath);
                continue;
            }

            if (!TryResolvePathUnderRoot(this.repoRoot, route.DraftMarkdownArtifactPath, out var draftPath)
                || !File.Exists(draftPath))
            {
                unauthorizedPaths.Add(patternPath);
                continue;
            }

            if (!TryResolvePathUnderRoot(worktreePath, patternPath, out var worktreeFilePath)
                || !File.Exists(worktreeFilePath))
            {
                unauthorizedPaths.Add(patternPath);
                continue;
            }

            if (!ContentsMatch(draftPath, worktreeFilePath))
            {
                unauthorizedPaths.Add(patternPath);
                continue;
            }

            authorizedPaths.Add(patternPath);
        }

        var summary = unauthorizedPaths.Count == 0
            ? $"Authorized governed pattern markdown writeback for {authorizedPaths.Count} path(s): {string.Join(", ", authorizedPaths)}."
            : $"Pattern markdown writeback authorization missing for: {string.Join(", ", unauthorizedPaths)}.";

        return new MemoryPatternWritebackRouteAuthorizationAssessment(
            Applies: true,
            AllTouchedPathsAuthorized: unauthorizedPaths.Count == 0,
            AuthorizedPaths: authorizedPaths,
            UnauthorizedPaths: unauthorizedPaths,
            Summary: summary);
    }

    private IReadOnlyDictionary<string, EligibleRouteRecord> LoadEligibleRoutes()
    {
        if (!Directory.Exists(routeSearchRoot))
        {
            return new Dictionary<string, EligibleRouteRecord>(PathComparer);
        }

        var routeFiles = Directory.GetFiles(
                routeSearchRoot,
                "memory_pattern_writeback_route.json",
                SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        var routes = new Dictionary<string, EligibleRouteRecord>(PathComparer);
        foreach (var routeFile in routeFiles)
        {
            if (!TryReadEligibleRoute(routeFile, out var route))
            {
                continue;
            }

            if (!routes.ContainsKey(route.TargetMemoryPath))
            {
                routes.Add(route.TargetMemoryPath, route);
            }
        }

        return routes;
    }

    private static bool TryReadEligibleRoute(string routePath, out EligibleRouteRecord route)
    {
        route = EligibleRouteRecord.None;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(routePath));
            var root = document.RootElement;
            if (!TryGetRequiredString(root, "schema_version", out var schemaVersion)
                || !string.Equals(schemaVersion, ExpectedSchemaVersion, StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryGetRequiredString(root, "route_status", out var routeStatus)
                || !string.Equals(routeStatus, ExpectedRouteStatus, StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryGetRequiredString(root, "route_decision", out var routeDecision)
                || !string.Equals(routeDecision, ExpectedRouteDecision, StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryGetRequiredString(root, "current_posture", out var currentPosture)
                || !string.Equals(currentPosture, ExpectedCurrentPosture, StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryGetRequiredString(root, "target_memory_path", out var targetMemoryPath)
                || !targetMemoryPath.StartsWith(".ai/memory/patterns/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!root.TryGetProperty("source", out var source)
                || !TryGetRequiredString(source, "draft_markdown_artifact_path", out var draftMarkdownArtifactPath))
            {
                return false;
            }

            route = new EligibleRouteRecord(targetMemoryPath, draftMarkdownArtifactPath);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryGetRequiredString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool ContentsMatch(string leftPath, string rightPath)
    {
        return string.Equals(
            File.ReadAllText(leftPath),
            File.ReadAllText(rightPath),
            StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static bool TryResolvePathUnderRoot(string root, string repoRelativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(repoRelativePath))
        {
            return false;
        }

        try
        {
            var candidate = repoRelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var resolved = Path.GetFullPath(Path.Combine(root, candidate));
            var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
            if (!resolved.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolved, normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            fullPath = resolved;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private readonly record struct EligibleRouteRecord(
        string TargetMemoryPath,
        string DraftMarkdownArtifactPath)
    {
        public static EligibleRouteRecord None => new(string.Empty, string.Empty);
    }
}

public sealed record MemoryPatternWritebackRouteAuthorizationAssessment(
    bool Applies,
    bool AllTouchedPathsAuthorized,
    IReadOnlyList<string> AuthorizedPaths,
    IReadOnlyList<string> UnauthorizedPaths,
    string Summary)
{
    public static MemoryPatternWritebackRouteAuthorizationAssessment NotApplicable { get; } =
        new(
            Applies: false,
            AllTouchedPathsAuthorized: false,
            AuthorizedPaths: Array.Empty<string>(),
            UnauthorizedPaths: Array.Empty<string>(),
            Summary: "Pattern markdown writeback authorization is not applicable.");

    public bool IsAuthorized(string path)
    {
        return AuthorizedPaths.Contains(path.Replace('\\', '/').Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
