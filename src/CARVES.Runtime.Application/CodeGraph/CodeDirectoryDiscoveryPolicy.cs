using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Application.CodeGraph;

public static class CodeDirectoryDiscoveryPolicy
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    private static readonly string[] SourceAnchorExtensions =
    [
        ".cs",
        ".py",
        ".razor",
        ".csproj",
        ".fsproj",
        ".vbproj",
    ];

    private static readonly string[] AlwaysExcludedTopLevelDirectories =
    [
        ".ai",
        ".carves-platform",
        ".git",
        ".vs",
        "node_modules",
    ];

    public static IReadOnlyList<string> Discover(string repoRoot, SystemConfig systemConfig)
    {
        if (!Directory.Exists(repoRoot))
        {
            return Array.Empty<string>();
        }

        var discovered = new List<string>();
        if (DirectoryContainsSourceAnchors(repoRoot, repoRoot, systemConfig, recurse: false))
        {
            discovered.Add(".");
        }

        foreach (var directory in EnumerateTopLevelDirectories(repoRoot, systemConfig))
        {
            if (DirectoryContainsSourceAnchors(repoRoot, directory, systemConfig, recurse: true))
            {
                discovered.Add(Path.GetRelativePath(repoRoot, directory).Replace('\\', '/'));
            }
        }

        return CompressRoots(discovered);
    }

    public static IReadOnlyList<string> ResolveEffectiveDirectories(string repoRoot, SystemConfig systemConfig)
    {
        var configured = CompressRoots(
            systemConfig.CodeDirectories
                .Where(path => DirectoryHintExists(repoRoot, path))
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))!);
        var discovered = Discover(repoRoot, systemConfig);
        if (configured.Count == 0)
        {
            return discovered;
        }

        if (discovered.Count == 0)
        {
            return configured;
        }

        return CompressRoots(configured.Concat(discovered));
    }

    private static IEnumerable<string> EnumerateTopLevelDirectories(string repoRoot, SystemConfig systemConfig)
    {
        var discovered = Directory.EnumerateDirectories(repoRoot, "*", SearchOption.TopDirectoryOnly)
            .Where(path => !IsExcludedTopLevelDirectory(Path.GetFileName(path), systemConfig))
            .OrderBy(path => path, PathComparer)
            .ToArray();

        var conventionalRoots = systemConfig.CodeDirectories
            .Select(NormalizePath)
            .Where(path => !string.IsNullOrWhiteSpace(path) && path is not ".")
            .Select(path => Path.GetFullPath(Path.Combine(repoRoot, path!)))
            .Where(Directory.Exists)
            .OrderBy(path => path, PathComparer);

        return conventionalRoots.Concat(discovered).Distinct(PathComparer);
    }

    private static bool DirectoryContainsSourceAnchors(string repoRoot, string directoryPath, SystemConfig systemConfig, bool recurse)
    {
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = recurse,
            IgnoreInaccessible = EnumerationOptions.IgnoreInaccessible,
            AttributesToSkip = EnumerationOptions.AttributesToSkip,
        };
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", enumerationOptions))
        {
            if (CodeGraphSourceTruthPolicy.ShouldTrackFile(repoRoot, file, systemConfig) || IsSourceAnchorFile(file))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExcludedTopLevelDirectory(string? name, SystemConfig systemConfig)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        return AlwaysExcludedTopLevelDirectories.Contains(name, PathComparer)
            || systemConfig.ExcludedDirectories.Any(excluded =>
                !string.IsNullOrWhiteSpace(excluded)
                && string.Equals(NormalizePath(excluded)?.Trim('/'), name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSourceAnchorFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return SourceAnchorExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static bool DirectoryHintExists(string repoRoot, string path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized == ".")
        {
            return Directory.Exists(repoRoot);
        }

        return Directory.Exists(Path.Combine(repoRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static IReadOnlyList<string> CompressRoots(IEnumerable<string> roots)
    {
        var selected = new List<string>();
        foreach (var root in roots
                     .Select(NormalizePath)
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(PathComparer)
                     .OrderBy(path => path == "." ? string.Empty : path, PathComparer)
                     .ThenBy(path => path == "." ? 0 : path!.Count(ch => ch == '/')))
        {
            if (root is null)
            {
                continue;
            }

            if (selected.Any(existing =>
                    existing == "."
                    || string.Equals(existing, root, StringComparison.OrdinalIgnoreCase)
                    || root.StartsWith(existing + "/", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            selected.Add(root);
        }

        return selected;
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Trim().Replace('\\', '/').Trim('/');
        return string.IsNullOrWhiteSpace(normalized) ? "." : normalized;
    }
}
