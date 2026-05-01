namespace Carves.Matrix.Core;

internal static class MatrixProcessCommandResolver
{
    private static readonly string[] DefaultWindowsExecutableExtensions = [".COM", ".EXE", ".BAT", ".CMD"];

    public static string ResolveForProcessStart(string fileName)
    {
        return ResolveForProcessStart(
            fileName,
            OperatingSystem.IsWindows(),
            Environment.GetEnvironmentVariable("PATH"),
            Environment.GetEnvironmentVariable("PATHEXT"),
            File.Exists);
    }

    internal static string ResolveForProcessStart(
        string fileName,
        bool isWindows,
        string? pathValue,
        string? pathExtValue,
        Func<string, bool> fileExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(fileExists);

        if (!isWindows || IsExplicitPath(fileName))
        {
            return fileName;
        }

        var pathEntries = SplitPath(pathValue, isWindows);
        if (pathEntries.Count == 0)
        {
            return fileName;
        }

        var candidateNames = BuildCandidateNames(fileName, pathExtValue);
        foreach (var pathEntry in pathEntries)
        {
            foreach (var candidateName in candidateNames)
            {
                var candidatePath = Path.Combine(pathEntry, candidateName);
                if (fileExists(candidatePath))
                {
                    return candidatePath;
                }
            }
        }

        return fileName;
    }

    private static bool IsExplicitPath(string fileName)
    {
        return Path.IsPathRooted(fileName)
            || fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> SplitPath(string? pathValue, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return [];
        }

        var separator = isWindows ? ';' : Path.PathSeparator;
        return pathValue.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToArray();
    }

    private static IReadOnlyList<string> BuildCandidateNames(string fileName, string? pathExtValue)
    {
        if (!string.IsNullOrEmpty(Path.GetExtension(fileName)))
        {
            return [fileName];
        }

        var candidateNames = new List<string> { fileName };
        foreach (var extension in EnumerateWindowsExtensions(pathExtValue))
        {
            candidateNames.Add(fileName + extension);
        }

        return candidateNames;
    }

    private static IEnumerable<string> EnumerateWindowsExtensions(string? pathExtValue)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in SplitPathExt(pathExtValue).Concat(DefaultWindowsExecutableExtensions))
        {
            if (seen.Add(extension))
            {
                yield return extension;
            }
        }
    }

    private static IEnumerable<string> SplitPathExt(string? pathExtValue)
    {
        if (string.IsNullOrWhiteSpace(pathExtValue))
        {
            yield break;
        }

        foreach (var rawExtension in pathExtValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var extension = rawExtension.StartsWith(".", StringComparison.Ordinal)
                ? rawExtension
                : "." + rawExtension;
            if (extension.Length > 1)
            {
                yield return extension;
            }
        }
    }
}
