using System.Diagnostics;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter
{
    private static IReadOnlyList<string> ObserveActualChangedFiles(string worktreeRoot)
    {
        if (string.IsNullOrWhiteSpace(worktreeRoot) || !Directory.Exists(worktreeRoot))
        {
            return Array.Empty<string>();
        }

        var gitMarkerPath = Path.Combine(worktreeRoot, ".git");
        if (!Directory.Exists(gitMarkerPath) && !File.Exists(gitMarkerPath))
        {
            return Array.Empty<string>();
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = worktreeRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("status");
            startInfo.ArgumentList.Add("--porcelain=v1");
            startInfo.ArgumentList.Add("--untracked-files=all");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Array.Empty<string>();
            }

            var stdout = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(10_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                process.WaitForExit();
                return Array.Empty<string>();
            }

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return Array.Empty<string>();
            }

            return stdout
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(ParsePorcelainPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Where(path => !IsRuntimeWorktreeMarker(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<WorkerEvent> BuildObservedChangedFileEvents(
        string taskId,
        string runId,
        IReadOnlyList<string> observedChangedFiles,
        IReadOnlyList<string> reportedChangedFiles)
    {
        if (observedChangedFiles.Count == 0)
        {
            return Array.Empty<WorkerEvent>();
        }

        var reported = new HashSet<string>(reportedChangedFiles, StringComparer.OrdinalIgnoreCase);
        return observedChangedFiles
            .Where(path => !reported.Contains(path))
            .Select(path => new WorkerEvent
            {
                RunId = runId,
                TaskId = taskId,
                EventType = WorkerEventType.FileEditObserved,
                Summary = $"Codex CLI worktree status observed actual changed file '{path}'.",
                ItemType = "git_status_observed_file_change",
                FilePath = path,
                RawPayload = path,
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["source"] = "git_status_porcelain",
                    ["reported_by_worker"] = "false",
                },
                OccurredAt = DateTimeOffset.UtcNow,
            })
            .ToArray();
    }

    private static string ParsePorcelainPath(string line)
    {
        if (line.Length <= 3)
        {
            return string.Empty;
        }

        var path = line[3..].Trim();
        var renameSeparator = " -> ";
        var renameIndex = path.IndexOf(renameSeparator, StringComparison.Ordinal);
        if (renameIndex >= 0)
        {
            path = path[(renameIndex + renameSeparator.Length)..];
        }

        return path.Trim('"').Replace('\\', '/');
    }

    private static bool IsRuntimeWorktreeMarker(string path)
    {
        return string.Equals(path.Trim().Replace('\\', '/'), ".carves-worktree.json", StringComparison.OrdinalIgnoreCase);
    }
}
