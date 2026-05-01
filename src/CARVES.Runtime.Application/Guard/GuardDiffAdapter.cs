using Carves.Runtime.Application.Processes;

namespace Carves.Runtime.Application.Guard;

public sealed class GuardDiffAdapter
{
    private readonly IProcessRunner processRunner;

    public GuardDiffAdapter(IProcessRunner processRunner)
    {
        this.processRunner = processRunner;
    }

    public GuardDiffContext BuildContext(GuardDiffInput input, GuardPolicySnapshot policy)
    {
        var collected = input.ChangedFiles.Count > 0
            ? new ChangedFileCollection(input.ChangedFiles, Array.Empty<GuardDiffDiagnostic>())
            : CollectChangedFiles(input.RepositoryRoot, input.BaseRef, input.HeadRef);
        var changedFiles = collected.Files
            .Where(file => !IsGeneratedGuardReadModel(file.Path))
            .Select(file => BuildChangedFile(file, policy.PathPolicy))
            .DistinctBy(file => file.Path, StringComparer.Ordinal)
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();

        return new GuardDiffContext(
            input.RepositoryRoot,
            input.BaseRef,
            input.HeadRef,
            policy,
            changedFiles,
            BuildStats(changedFiles),
            ScopeSource: "guard_policy",
            RuntimeTaskId: null,
            collected.Diagnostics);
    }

    private ChangedFileCollection CollectChangedFiles(string repositoryRoot, string baseRef, string? headRef)
    {
        var status = RunGit(repositoryRoot, "status", "--porcelain=v1", "--untracked-files=all");
        if (!status.Succeeded)
        {
            return new ChangedFileCollection(
                Array.Empty<GuardChangedFileInput>(),
                [
                    new GuardDiffDiagnostic(
                        "git.status_failed",
                        GuardSeverity.Block,
                        "Git status could not be read.",
                        status.Evidence),
                ]);
        }

        if (string.IsNullOrWhiteSpace(status.StandardOutput))
        {
            return new ChangedFileCollection(Array.Empty<GuardChangedFileInput>(), Array.Empty<GuardDiffDiagnostic>());
        }

        var numstat = ReadNumstat(repositoryRoot, baseRef, headRef);
        var files = new List<GuardChangedFileInput>();
        foreach (var line in SplitLines(status.StandardOutput))
        {
            var parsed = ParsePorcelainLine(line);
            if (parsed is null)
            {
                continue;
            }

            var path = parsed.Path;
            var stats = numstat.TryGetValue(path, out var trackedStats)
                ? trackedStats
                : parsed.WasUntracked
                    ? CountUntrackedFile(repositoryRoot, path)
                    : new FileStats(0, 0, false);
            files.Add(new GuardChangedFileInput(
                path,
                parsed.OldPath,
                parsed.Status,
                stats.Additions,
                stats.Deletions,
                stats.IsBinary,
                parsed.WasUntracked));
        }

        return new ChangedFileCollection(
            files
            .DistinctBy(file => file.Path, StringComparer.Ordinal)
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray(),
            numstat.Diagnostics);
    }

    private NumstatReadResult ReadNumstat(string repositoryRoot, string baseRef, string? headRef)
    {
        var range = string.IsNullOrWhiteSpace(headRef) ? baseRef : $"{baseRef}..{headRef}";
        var output = RunGit(repositoryRoot, "diff", "--numstat", "--relative", range, "--");
        var result = new Dictionary<string, FileStats>(StringComparer.Ordinal);
        if (!output.Succeeded)
        {
            return new NumstatReadResult(
                result,
                [
                    new GuardDiffDiagnostic(
                        "git.diff_failed",
                        GuardSeverity.Block,
                        "Git diff statistics could not be read.",
                        output.Evidence),
                ]);
        }

        foreach (var line in SplitLines(output.StandardOutput))
        {
            var parts = line.Split('\t');
            if (parts.Length < 3)
            {
                continue;
            }

            var path = NormalizeGitPath(parts[^1]);
            var isBinary = parts[0] == "-" || parts[1] == "-";
            var additions = int.TryParse(parts[0], out var parsedAdditions) ? parsedAdditions : 0;
            var deletions = int.TryParse(parts[1], out var parsedDeletions) ? parsedDeletions : 0;
            result[path] = new FileStats(additions, deletions, isBinary);
        }

        return new NumstatReadResult(result, Array.Empty<GuardDiffDiagnostic>());
    }

    private GitCommandOutput RunGit(string repositoryRoot, params string[] arguments)
    {
        try
        {
            var result = processRunner.Run(["git", .. arguments], repositoryRoot);
            return result.ExitCode == 0
                ? new GitCommandOutput(true, result.StandardOutput, string.Empty)
                : new GitCommandOutput(false, string.Empty, BuildGitFailureEvidence(arguments, result.ExitCode, result.StandardError));
        }
        catch (Exception exception)
        {
            return new GitCommandOutput(false, string.Empty, BuildGitFailureEvidence(arguments, exitCode: null, exception.Message));
        }
    }

    private static ParsedStatusLine? ParsePorcelainLine(string line)
    {
        if (line.Length < 4)
        {
            return null;
        }

        if (line.StartsWith("?? ", StringComparison.Ordinal))
        {
            return new ParsedStatusLine(
                NormalizeGitPath(line[3..]),
                OldPath: null,
                GuardFileChangeStatus.Added,
                WasUntracked: true);
        }

        var statusText = line[..2];
        var pathText = line.Length > 3 ? line[3..].Trim() : string.Empty;
        if (line.Length > 2 && line[1] == ' ' && line[0] != ' ')
        {
            statusText = $"{line[0]} ";
            pathText = line[2..].Trim();
        }
        var statusCode = statusText.Contains('R') ? 'R'
            : statusText.Contains('C') ? 'C'
            : statusText.Contains('D') ? 'D'
            : statusText.Contains('A') ? 'A'
            : statusText.Contains('T') ? 'T'
            : statusText.Contains('M') ? 'M'
            : '?';
        string? oldPath = null;
        var path = pathText;
        var renameSeparatorIndex = pathText.IndexOf(" -> ", StringComparison.Ordinal);
        if (renameSeparatorIndex >= 0)
        {
            oldPath = NormalizeGitPath(pathText[..renameSeparatorIndex]);
            path = pathText[(renameSeparatorIndex + " -> ".Length)..];
        }

        return new ParsedStatusLine(
            NormalizeGitPath(path),
            oldPath,
            statusCode switch
            {
                'A' => GuardFileChangeStatus.Added,
                'M' => GuardFileChangeStatus.Modified,
                'D' => GuardFileChangeStatus.Deleted,
                'R' => GuardFileChangeStatus.Renamed,
                'C' => GuardFileChangeStatus.Copied,
                'T' => GuardFileChangeStatus.TypeChanged,
                _ => GuardFileChangeStatus.Unknown,
            },
            WasUntracked: false);
    }

    private static GuardChangedFile BuildChangedFile(GuardChangedFileInput input, GuardPathPolicy pathPolicy)
    {
        var path = NormalizeGitPath(input.Path);
        var oldPath = string.IsNullOrWhiteSpace(input.OldPath) ? null : NormalizeGitPath(input.OldPath);
        var pathIsUnsafe = IsUnsafeRepoPath(input.Path) || (!string.IsNullOrWhiteSpace(input.OldPath) && IsUnsafeRepoPath(input.OldPath));
        var matchedProtectedPrefix = pathIsUnsafe
            ? "unsafe_repo_path"
            : FindMatchingPathPattern(path, oldPath, pathPolicy.ProtectedPathPrefixes, pathPolicy.CaseSensitive);
        var matchedAllowedPrefix = pathIsUnsafe
            ? null
            : FindMatchingPathPattern(path, oldPath, pathPolicy.AllowedPathPrefixes, pathPolicy.CaseSensitive);
        return new GuardChangedFile(
            path,
            oldPath,
            input.Status,
            Math.Max(0, input.Additions),
            Math.Max(0, input.Deletions),
            input.IsBinary,
            input.WasUntracked,
            MatchesAllowedPath: !string.IsNullOrWhiteSpace(matchedAllowedPrefix),
            MatchesProtectedPath: !string.IsNullOrWhiteSpace(matchedProtectedPrefix),
            matchedAllowedPrefix,
            matchedProtectedPrefix);
    }

    private static string? FindMatchingPathPattern(string path, string? oldPath, IReadOnlyList<string> patterns, bool caseSensitive)
    {
        return patterns.FirstOrDefault(pattern =>
            PathMatches(path, pattern, caseSensitive)
            || (!string.IsNullOrWhiteSpace(oldPath) && PathMatches(oldPath, pattern, caseSensitive)));
    }

    internal static bool PathMatches(string path, string pattern, bool caseSensitive = true)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var normalizedPath = NormalizeGitPath(path);
        var normalizedPattern = NormalizeGitPath(pattern);
        return normalizedPattern.EndsWith("/", StringComparison.Ordinal)
            ? normalizedPath.StartsWith(normalizedPattern, comparison)
            : string.Equals(normalizedPath, normalizedPattern, comparison);
    }

    internal static bool GlobMatches(string path, string pattern, bool caseSensitive = true)
    {
        var normalizedPath = NormalizeGitPath(path);
        var normalizedPattern = NormalizeGitPath(pattern);
        if (!normalizedPattern.Contains('*', StringComparison.Ordinal))
        {
            return PathMatches(normalizedPath, normalizedPattern, caseSensitive);
        }

        if (!normalizedPattern.Contains('/', StringComparison.Ordinal))
        {
            return SegmentGlobMatches(Path.GetFileName(normalizedPath), normalizedPattern, caseSensitive);
        }

        var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var patternSegments = normalizedPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return GlobSegmentsMatch(pathSegments, 0, patternSegments, 0, caseSensitive);
    }

    private static bool GlobSegmentsMatch(
        IReadOnlyList<string> pathSegments,
        int pathIndex,
        IReadOnlyList<string> patternSegments,
        int patternIndex,
        bool caseSensitive)
    {
        if (patternIndex == patternSegments.Count)
        {
            return pathIndex == pathSegments.Count;
        }

        if (string.Equals(patternSegments[patternIndex], "**", StringComparison.Ordinal))
        {
            if (GlobSegmentsMatch(pathSegments, pathIndex, patternSegments, patternIndex + 1, caseSensitive))
            {
                return true;
            }

            return pathIndex < pathSegments.Count
                   && GlobSegmentsMatch(pathSegments, pathIndex + 1, patternSegments, patternIndex, caseSensitive);
        }

        return pathIndex < pathSegments.Count
               && SegmentGlobMatches(pathSegments[pathIndex], patternSegments[patternIndex], caseSensitive)
               && GlobSegmentsMatch(pathSegments, pathIndex + 1, patternSegments, patternIndex + 1, caseSensitive);
    }

    private static bool SegmentGlobMatches(string value, string pattern, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(value, pattern, comparison);
        }

        var parts = pattern.Split('*');
        var position = 0;
        if (parts.Length > 0 && parts[0].Length > 0)
        {
            if (!value.StartsWith(parts[0], comparison))
            {
                return false;
            }

            position = parts[0].Length;
        }

        for (var index = 1; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Length == 0)
            {
                continue;
            }

            var found = value.IndexOf(part, position, comparison);
            if (found < 0)
            {
                return false;
            }

            position = found + part.Length;
        }

        var lastPart = parts[^1];
        return lastPart.Length == 0 || value.EndsWith(lastPart, comparison);
    }

    private static GuardPatchStats BuildStats(IReadOnlyList<GuardChangedFile> files)
    {
        return new GuardPatchStats(
            files.Count,
            files.Count(file => file.Status == GuardFileChangeStatus.Added),
            files.Count(file => file.Status == GuardFileChangeStatus.Modified),
            files.Count(file => file.Status == GuardFileChangeStatus.Deleted),
            files.Count(file => file.Status == GuardFileChangeStatus.Renamed),
            files.Count(file => file.IsBinary),
            files.Sum(file => file.Additions),
            files.Sum(file => file.Deletions));
    }

    private static FileStats CountUntrackedFile(string repositoryRoot, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(fullPath))
        {
            return new FileStats(0, 0, false);
        }

        try
        {
            using var stream = File.OpenRead(fullPath);
            Span<byte> buffer = stackalloc byte[4096];
            var read = stream.Read(buffer);
            if (buffer[..read].Contains((byte)0))
            {
                return new FileStats(0, 0, true);
            }
        }
        catch
        {
            return new FileStats(0, 0, true);
        }

        try
        {
            return new FileStats(File.ReadLines(fullPath).Count(), 0, false);
        }
        catch
        {
            return new FileStats(0, 0, true);
        }
    }

    private static string NormalizeGitPath(string path)
    {
        var normalized = path.Trim().Trim('"').Replace('\\', '/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        var trailingSlash = normalized.EndsWith("/", StringComparison.Ordinal);
        var segments = new List<string>();
        foreach (var rawSegment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(rawSegment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(rawSegment, "..", StringComparison.Ordinal))
            {
                if (segments.Count > 0 && !string.Equals(segments[^1], "..", StringComparison.Ordinal))
                {
                    segments.RemoveAt(segments.Count - 1);
                    continue;
                }

                segments.Add(rawSegment);
                continue;
            }

            segments.Add(rawSegment);
        }

        normalized = string.Join('/', segments);
        return trailingSlash && normalized.Length > 0 ? $"{normalized}/" : normalized;
    }

    internal static bool IsUnsafeRepoPath(string path)
    {
        var normalized = path.Trim().Trim('"').Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.StartsWith("//", StringComparison.Ordinal)
            || normalized.Contains("://", StringComparison.Ordinal)
            || (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':'))
        {
            return true;
        }

        var depth = 0;
        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                if (depth == 0)
                {
                    return true;
                }

                depth--;
                continue;
            }

            depth++;
        }

        return false;
    }

    private static bool IsGeneratedGuardReadModel(string path)
    {
        return string.Equals(NormalizeGitPath(path), GuardDecisionAuditStore.RelativeAuditPath, StringComparison.Ordinal);
    }

    private static string[] SplitLines(string value)
    {
        return value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed record ParsedStatusLine(
        string Path,
        string? OldPath,
        GuardFileChangeStatus Status,
        bool WasUntracked);

    private sealed record FileStats(int Additions, int Deletions, bool IsBinary);
    private sealed record GitCommandOutput(bool Succeeded, string StandardOutput, string Evidence);
    private sealed record NumstatReadResult(
        Dictionary<string, FileStats> Stats,
        IReadOnlyList<GuardDiffDiagnostic> Diagnostics)
    {
        public bool TryGetValue(string path, out FileStats stats)
        {
            return Stats.TryGetValue(path, out stats!);
        }
    }
    private sealed record ChangedFileCollection(
        IReadOnlyList<GuardChangedFileInput> Files,
        IReadOnlyList<GuardDiffDiagnostic> Diagnostics);

    private static string BuildGitFailureEvidence(IReadOnlyList<string> arguments, int? exitCode, string? detail)
    {
        var command = $"git {string.Join(' ', arguments)}";
        var normalizedDetail = string.IsNullOrWhiteSpace(detail)
            ? "no stderr"
            : detail.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Trim();
        return exitCode is null
            ? $"command={command}; exception={normalizedDetail}"
            : $"command={command}; exit_code={exitCode}; stderr={normalizedDetail}";
    }
}
