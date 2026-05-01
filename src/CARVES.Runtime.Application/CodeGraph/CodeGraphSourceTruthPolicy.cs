using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Application.CodeGraph;

public static class CodeGraphSourceTruthPolicy
{
    private static readonly string[] IndexedExtensions = [".cs", ".py"];
    private static readonly string[] ForbiddenPathSegments = [".git", ".nuget", "bin", "obj", "TestResults", "coverage"];
    private static readonly string[] ForbiddenPathFragments = [".ai/worktrees/"];
    private static readonly string[] ForbiddenFileNames = ["project.assets.json"];
    private static readonly string[] ForbiddenFileSuffixes =
    [
        ".deps.json",
        ".runtimeconfig.json",
        ".nuget.dgspec.json",
        ".AssemblyInfo.cs",
        ".GlobalUsings.g.cs",
        ".AssemblyAttributes.cs",
    ];

    public static bool ShouldTrackFile(string repoRoot, string fullPath, SystemConfig systemConfig)
    {
        var relativePath = Path.GetRelativePath(repoRoot, fullPath);
        return ShouldTrackRelativePath(relativePath, systemConfig);
    }

    public static bool ShouldTrackRelativePath(string relativePath, SystemConfig systemConfig)
    {
        var normalized = Normalize(relativePath);
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.StartsWith("../", StringComparison.Ordinal)
            || normalized.StartsWith(".ai/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!HasIndexedExtension(normalized)
            || ContainsForbiddenPathSegment(normalized)
            || ContainsConfiguredExclusion(normalized, systemConfig)
            || MatchesForbiddenFile(normalized))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsConfiguredExclusion(string normalizedRelativePath, SystemConfig systemConfig)
    {
        return systemConfig.ExcludedDirectories.Any(excluded =>
        {
            if (string.IsNullOrWhiteSpace(excluded))
            {
                return false;
            }

            var normalizedExcluded = Normalize(excluded).Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedExcluded))
            {
                return false;
            }

            return normalizedRelativePath.Contains($"/{normalizedExcluded}/", StringComparison.OrdinalIgnoreCase)
                || normalizedRelativePath.StartsWith($"{normalizedExcluded}/", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool ContainsForbiddenPathSegment(string normalizedRelativePath)
    {
        var segments = normalizedRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => ForbiddenPathSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        return ForbiddenPathFragments.Any(fragment => normalizedRelativePath.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesForbiddenFile(string normalizedRelativePath)
    {
        var fileName = Path.GetFileName(normalizedRelativePath);
        if (ForbiddenFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return ForbiddenFileSuffixes.Any(suffix => normalizedRelativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasIndexedExtension(string normalizedRelativePath)
    {
        var extension = Path.GetExtension(normalizedRelativePath);
        return IndexedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
