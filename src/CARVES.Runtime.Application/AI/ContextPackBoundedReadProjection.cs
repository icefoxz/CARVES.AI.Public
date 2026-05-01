using System.Text;

namespace Carves.Runtime.Application.AI;

internal sealed record ContextReadWindowProjection(
    string Path,
    int TotalLines,
    int StartLine,
    int EndLine,
    string Reason,
    bool Truncated,
    string Snippet);

internal sealed record BoundedReadProjection(
    IReadOnlyList<string> RelevantFiles,
    IReadOnlyList<ContextReadWindowProjection> WindowedReads,
    int CandidateFileCount,
    int FullReadCount,
    int OmittedFileCount,
    string Strategy);

internal static class BoundedReadProjectionBuilder
{
    private const int MaxRelevantFiles = 8;
    private const int LargeFileLineThreshold = 160;
    private const int WindowLineCount = 80;
    private const int SnippetLineCount = 8;
    private const int SnippetCharBudget = 720;

    public static BoundedReadProjection Build(string repoRoot, IEnumerable<string> candidateFiles)
    {
        var normalizedCandidates = candidateFiles
            .Select(NormalizePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var relevantFiles = normalizedCandidates.Take(MaxRelevantFiles).ToArray();
        var windows = new List<ContextReadWindowProjection>();
        var fullReadCount = 0;

        foreach (var relativePath in relevantFiles)
        {
            var absolutePath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                fullReadCount++;
                continue;
            }

            var lines = File.ReadAllLines(absolutePath);
            if (lines.Length > LargeFileLineThreshold)
            {
                var endLine = Math.Min(lines.Length, WindowLineCount);
                windows.Add(new ContextReadWindowProjection(
                    relativePath,
                    lines.Length,
                    1,
                    endLine,
                    "large_file_threshold_exceeded",
                    true,
                    BuildSnippet(lines, 1, endLine)));
                continue;
            }

            fullReadCount++;
        }

        var omittedFileCount = Math.Max(0, normalizedCandidates.Length - relevantFiles.Length);
        var strategy = windows.Count > 0 || omittedFileCount > 0
            ? "bounded_scope_windowing"
            : "full_scope_projection";

        return new BoundedReadProjection(
            relevantFiles,
            windows,
            normalizedCandidates.Length,
            fullReadCount,
            omittedFileCount,
            strategy);
    }

    private static string BuildSnippet(IReadOnlyList<string> lines, int startLine, int endLine)
    {
        var builder = new StringBuilder();
        var emitted = 0;
        for (var index = startLine - 1; index < endLine && index < lines.Count && emitted < SnippetLineCount; index++)
        {
            var line = lines[index].TrimEnd();
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(index + 1)
                .Append(": ")
                .Append(line);
            emitted++;

            if (builder.Length >= SnippetCharBudget)
            {
                break;
            }
        }

        return builder.Length <= SnippetCharBudget
            ? builder.ToString()
            : $"{builder.ToString()[..Math.Max(0, SnippetCharBudget - 3)].TrimEnd()}...";
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
