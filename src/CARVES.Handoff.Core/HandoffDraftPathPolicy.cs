namespace Carves.Handoff.Core;

public sealed class HandoffDraftPathPolicy
{
    private readonly IReadOnlyList<string> protectedPrefixes;
    private readonly IReadOnlySet<string> protectedSegments;

    public static HandoffDraftPathPolicy Default { get; } = new(
        [".ai/tasks/", ".ai/memory/", ".git/"],
        new HashSet<string>(StringComparer.Ordinal) { "bin", "obj" });

    public HandoffDraftPathPolicy(IReadOnlyList<string> protectedPrefixes, IReadOnlySet<string> protectedSegments)
    {
        this.protectedPrefixes = protectedPrefixes
            .Select(NormalizePrefix)
            .ToArray();
        this.protectedSegments = new HashSet<string>(
            protectedSegments.Select(segment => segment.Trim().ToLowerInvariant()),
            StringComparer.Ordinal);
    }

    public HandoffDraftPathResolution Resolve(string repoRoot, string packetPath)
    {
        var resolvedPath = Path.GetFullPath(Path.IsPathRooted(packetPath)
            ? packetPath
            : Path.Combine(repoRoot, packetPath));
        var relativePath = GetRepoRelativePath(repoRoot, resolvedPath);
        return new HandoffDraftPathResolution(resolvedPath, relativePath);
    }

    public bool IsProtectedPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
        if (protectedPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return true;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(protectedSegments.Contains);
    }

    private static string GetRepoRelativePath(string repoRoot, string resolvedPath)
    {
        var root = Path.GetFullPath(repoRoot);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolvedPath, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Packet path must stay inside the current repository.");
        }

        return Path.GetRelativePath(root, resolvedPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string NormalizePrefix(string prefix)
    {
        return prefix.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
    }
}

public sealed record HandoffDraftPathResolution(string ResolvedPath, string RelativePath);
