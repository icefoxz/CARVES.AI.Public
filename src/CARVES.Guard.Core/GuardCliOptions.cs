using Carves.Runtime.Application.Guard;

namespace Carves.Guard.Core;

public static partial class GuardCliRunner
{
    private static IReadOnlyList<string> FilterGuardRunTaskArguments(IReadOnlyList<string> arguments)
    {
        var filtered = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(argument, "--policy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--base", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--head", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            filtered.Add(argument);
        }

        return filtered;
    }

    private static GuardCliArguments ParseStandaloneArguments(IReadOnlyList<string> arguments)
    {
        string? repoRoot = null;
        var remaining = new List<string>();
        var transport = GuardRuntimeTransportPreference.Auto;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--repo-root", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return new GuardCliArguments(null, [], transport, "--repo-root requires a value.");
                }

                repoRoot = Path.GetFullPath(arguments[index + 1]);
                index++;
                continue;
            }

            if (string.Equals(argument, "--cold", StringComparison.OrdinalIgnoreCase))
            {
                transport = transport == GuardRuntimeTransportPreference.Host
                    ? GuardRuntimeTransportPreference.Auto
                    : GuardRuntimeTransportPreference.Cold;
                if (transport == GuardRuntimeTransportPreference.Auto)
                {
                    return new GuardCliArguments(repoRoot, [], transport, "Choose either --cold or --host, not both.");
                }

                continue;
            }

            if (string.Equals(argument, "--host", StringComparison.OrdinalIgnoreCase))
            {
                transport = transport == GuardRuntimeTransportPreference.Cold
                    ? GuardRuntimeTransportPreference.Auto
                    : GuardRuntimeTransportPreference.Host;
                if (transport == GuardRuntimeTransportPreference.Auto)
                {
                    return new GuardCliArguments(repoRoot, [], transport, "Choose either --cold or --host, not both.");
                }

                continue;
            }

            remaining.Add(argument);
        }

        return new GuardCliArguments(repoRoot, remaining, transport, null);
    }

    private static string? ResolveRepoRoot(string? explicitRepoRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRepoRoot))
        {
            var path = Path.GetFullPath(explicitRepoRoot);
            return Directory.Exists(path) ? path : null;
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (IsGitRepository(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool IsGitRepository(string path)
    {
        var gitPath = Path.Combine(Path.GetFullPath(path), ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    private static string? ResolveOption(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    private static int ResolveLimit(IReadOnlyList<string> arguments, int defaultValue)
    {
        var raw = ResolveOption(arguments, "--limit");
        if (raw is null)
        {
            return defaultValue;
        }

        return int.TryParse(raw, out var parsed) && parsed > 0 ? Math.Min(parsed, 100) : defaultValue;
    }

    private static string FormatOutcome(GuardDecisionOutcome outcome)
    {
        return outcome.ToString().ToLowerInvariant();
    }

    private static string FormatSeverity(GuardSeverity severity)
    {
        return severity.ToString().ToLowerInvariant();
    }

    private static string FormatStatus(GuardFileChangeStatus status)
    {
        return status.ToString().ToLowerInvariant();
    }

    private static string FormatRelativePath(string repoRoot, string absolutePath)
    {
        return Path.GetRelativePath(repoRoot, absolutePath).Replace('\\', '/');
    }

    private static bool IsPathInside(string rootPath, string candidatePath)
    {
        var root = Path.GetFullPath(rootPath);
        var candidate = Path.GetFullPath(candidatePath);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return candidate.StartsWith(root, comparison);
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "(none)" : string.Join(", ", values);
    }

    private static string FormatNullable(int? value)
    {
        return value?.ToString() ?? "unbounded";
    }
}
