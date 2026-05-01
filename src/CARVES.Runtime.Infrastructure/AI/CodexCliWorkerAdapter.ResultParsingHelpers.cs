using System.Text.Json;
using System.Text.RegularExpressions;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter
{
    private static IReadOnlyList<string> ExtractShellChangedFiles(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText)
            || (!commandText.Contains("Set-Content", StringComparison.OrdinalIgnoreCase)
                && !LooksLikePythonWriteCommand(commandText)))
        {
            return Array.Empty<string>();
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (TryExtractPowerShellChangedFile(commandText, out var powerShellPath))
        {
            paths.Add(powerShellPath);
        }

        foreach (var candidate in ExtractPythonWritePaths(commandText))
        {
            paths.Add(candidate);
        }

        return paths.Count == 0
            ? Array.Empty<string>()
            : paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool TryExtractPowerShellChangedFile(string commandText, out string path)
    {
        path = string.Empty;
        if (!commandText.Contains("Set-Content", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var markerIndex = commandText.IndexOf("-Path", StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        var candidate = commandText[(markerIndex + "-Path".Length)..].Trim().Trim('"', '\'');
        if (candidate.StartsWith("$", StringComparison.Ordinal))
        {
            if (!TryExtractAssignedShellPath(commandText, out candidate))
            {
                return false;
            }
        }

        var terminatorIndex = candidate.IndexOfAny(['\r', '\n', ';', '|', ' ']);
        if (terminatorIndex >= 0)
        {
            candidate = candidate[..terminatorIndex];
        }

        candidate = candidate.Trim('"', '\'').Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(candidate)
            && !TryExtractAssignedShellPath(commandText, out candidate))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate)
            && !TryExtractRepoRelativePath(commandText, out candidate))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static bool TryExtractAssignedShellPath(string commandText, out string path)
    {
        path = string.Empty;
        var match = Regex.Match(
            commandText,
            """\$path\s*=\s*['"]*(?<path>(?:[A-Za-z0-9_.-]+[\\/])+[A-Za-z0-9_.-]+\.[A-Za-z0-9]+)['"]*""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return false;
        }

        path = match.Groups["path"].Value.Replace('\\', '/');
        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool TryExtractRepoRelativePath(string commandText, out string path)
    {
        path = string.Empty;
        var match = Regex.Match(
            commandText.Replace('\\', '/'),
            """(?<path>(?:[A-Za-z0-9_.-]+/)+[A-Za-z0-9_.-]+\.[A-Za-z0-9]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return false;
        }

        path = match.Groups["path"].Value;
        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool LooksLikePythonWriteCommand(string commandText)
    {
        if (!Regex.IsMatch(
                commandText,
                """(^|[\s'"])(python|python3|py)([\s'"]|$)""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            return false;
        }

        return commandText.Contains("write_text(", StringComparison.Ordinal)
               || commandText.Contains("write_bytes(", StringComparison.Ordinal)
               || commandText.Contains(".open(", StringComparison.Ordinal)
               || commandText.Contains("open(", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ExtractPythonWritePaths(string commandText)
    {
        if (!LooksLikePythonWriteCommand(commandText))
        {
            return Array.Empty<string>();
        }

        var normalized = commandText.Replace('\\', '/');
        var matches = Regex.Matches(
            normalized,
            """['"](?<path>(?:[A-Za-z0-9_.-]+/)+[A-Za-z0-9_.-]+\.[A-Za-z0-9]+)['"]""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matches.Count == 0)
        {
            return Array.Empty<string>();
        }

        return matches
            .Select(match => match.Groups["path"].Value.Trim())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsNonFatalCommandProbeFailure(string? commandText, int? exitCode, string? aggregatedOutput, string? errorText)
    {
        if ((exitCode ?? 1) == 0 || string.IsNullOrWhiteSpace(commandText))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(aggregatedOutput)
            && string.IsNullOrWhiteSpace(errorText)
            && Regex.IsMatch(
                commandText,
                """(^|[\s'"])(rg|rg\.exe)([\s'"]|$)""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            return true;
        }

        var failureText = FirstNonEmpty(aggregatedOutput, errorText);
        return IsNonFatalGitProbeFailure(commandText, failureText);
    }

    private static bool IsNonFatalGitProbeFailure(string commandText, string failureText)
    {
        if (string.IsNullOrWhiteSpace(failureText))
        {
            return false;
        }

        var matchesGitProbe = Regex.IsMatch(
                                  commandText,
                                  """(^|[\s'"])(git|git\.exe)([\s'"]|$)""",
                                  RegexOptions.IgnoreCase | RegexOptions.Singleline)
                              && Regex.IsMatch(
                                  commandText,
                                  """(^|[\s'"])(status|diff)([\s'"]|$)""",
                                  RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!matchesGitProbe)
        {
            return false;
        }

        return failureText.Contains("not a git repository", StringComparison.OrdinalIgnoreCase)
               || failureText.Contains("use --no-index to compare two paths outside a working tree", StringComparison.OrdinalIgnoreCase)
               || failureText.Contains("git diff --no-index", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractChangedFilePaths(JsonElement element)
    {
        foreach (var propertyName in new[] { "changed_files", "changedFiles", "files_changed", "filesChanged" })
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return property.EnumerateArray()
                .Select(ExtractChangedFilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!.Replace('\\', '/').Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static string? ExtractChangedFilePath(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object => TryGetString(element, "path") ?? TryGetString(element, "file") ?? TryGetString(element, "file_path"),
            _ => null,
        };
    }

    private static void AddChangedFileEvents(
        WorkerExecutionRequest request,
        string runId,
        IReadOnlyList<string> paths,
        string itemType,
        string rawPayload,
        HashSet<string> changedFiles,
        List<WorkerEvent> events)
    {
        foreach (var path in paths)
        {
            if (!changedFiles.Add(path))
            {
                continue;
            }

            events.Add(BuildEvent(
                request.TaskId,
                runId,
                WorkerEventType.FileEditObserved,
                $"Codex CLI reported changed file '{path}'.",
                itemType,
                null,
                null,
                rawPayload,
                path));
        }
    }

    private static WorkerEvent BuildEvent(
        string taskId,
        string runId,
        WorkerEventType eventType,
        string summary,
        string itemType,
        string? commandText,
        int? exitCode,
        string rawPayload,
        string? filePath = null)
    {
        return new WorkerEvent
        {
            RunId = runId,
            TaskId = taskId,
            EventType = eventType,
            Summary = summary,
            ItemType = itemType,
            CommandText = commandText,
            FilePath = filePath,
            ExitCode = exitCode,
            RawPayload = rawPayload,
            OccurredAt = DateTimeOffset.UtcNow,
        };
    }

    private static WorkerExecutionStatus InferStatus(int exitCode, WorkerFailureKind failureKind, IReadOnlyCollection<WorkerPermissionRequest> permissionRequests)
    {
        if (permissionRequests.Count > 0 || failureKind == WorkerFailureKind.ApprovalRequired)
        {
            return WorkerExecutionStatus.ApprovalWait;
        }

        return exitCode == 0 ? WorkerExecutionStatus.Succeeded : WorkerExecutionStatus.Failed;
    }

    private static WorkerExecutionStatus InferFailedStatus(string? aggregatedOutput, string? errorText)
    {
        var text = $"{aggregatedOutput} {errorText}";
        return text.Contains("blocked by policy", StringComparison.OrdinalIgnoreCase)
            ? WorkerExecutionStatus.Blocked
            : WorkerExecutionStatus.Failed;
    }

    private static WorkerFailureKind ClassifyFailure(string? stdout, string? stderr, int exitCode)
    {
        var normalizedStdout = stdout ?? string.Empty;
        var normalizedStderr = stderr ?? string.Empty;
        var text = $"{normalizedStdout} {normalizedStderr}".ToLowerInvariant();
        if (text.Contains("blocked by policy", StringComparison.Ordinal)
            || text.Contains("read-only", StringComparison.Ordinal)
            || text.Contains("rejected", StringComparison.Ordinal))
        {
            return WorkerFailureKind.PolicyDenied;
        }

        if (ContainsExplicitApprovalSignal(normalizedStdout, normalizedStderr))
        {
            return WorkerFailureKind.ApprovalRequired;
        }

        if (text.Contains("not recognized as the name of a cmdlet", StringComparison.Ordinal)
            || text.Contains("is not recognized as an internal or external command", StringComparison.Ordinal)
            || text.Contains("command not found", StringComparison.Ordinal)
            || text.Contains("dotnet_cli_home", StringComparison.Ordinal)
            || text.Contains("nuget-migrations", StringComparison.Ordinal)
            || text.Contains("first-time use", StringComparison.Ordinal)
            || text.Contains("api key", StringComparison.Ordinal)
            || text.Contains("login", StringComparison.Ordinal)
            || text.Contains("authentication", StringComparison.Ordinal)
            || text.Contains("permission denied", StringComparison.Ordinal)
            || text.Contains("access denied", StringComparison.Ordinal)
            || text.Contains("operation not permitted", StringComparison.Ordinal)
            || text.Contains("unauthorized", StringComparison.Ordinal))
        {
            return WorkerFailureKind.EnvironmentBlocked;
        }

        if (text.Contains("timed out", StringComparison.Ordinal)
            || text.Contains("timeout", StringComparison.Ordinal))
        {
            return WorkerFailureKind.Timeout;
        }

        if (text.Contains("connection", StringComparison.Ordinal)
            || text.Contains("network", StringComparison.Ordinal)
            || text.Contains("temporar", StringComparison.Ordinal)
            || text.Contains("429", StringComparison.Ordinal)
            || text.Contains("503", StringComparison.Ordinal))
        {
            return WorkerFailureKind.TransientInfra;
        }

        if (exitCode != 0 && text.Contains("{", StringComparison.Ordinal) is false && text.Contains("}", StringComparison.Ordinal) is false)
        {
            if (text.Contains("build failed", StringComparison.Ordinal)
                && text.Contains("0 warning(s)", StringComparison.Ordinal)
                && text.Contains("0 error(s)", StringComparison.Ordinal))
            {
                return WorkerFailureKind.EnvironmentBlocked;
            }

            return WorkerFailureKind.InvalidOutput;
        }

        return exitCode == 0 ? WorkerFailureKind.None : WorkerFailureKind.TaskLogicFailed;
    }

    private static bool ContainsExplicitApprovalSignal(string stdout, string stderr)
    {
        var text = $"{stdout} {stderr}".ToLowerInvariant();
        return text.Contains("approval required", StringComparison.Ordinal)
            || text.Contains("requires approval", StringComparison.Ordinal)
            || text.Contains("awaiting approval", StringComparison.Ordinal)
            || text.Contains("waiting for approval", StringComparison.Ordinal)
            || text.Contains("request approval", StringComparison.Ordinal)
            || text.Contains("permission request", StringComparison.Ordinal)
            || text.Contains("permission required", StringComparison.Ordinal)
            || text.Contains("[y/n]", StringComparison.Ordinal)
            || text.Contains("[y/n", StringComparison.Ordinal)
            || text.Contains("allow this", StringComparison.Ordinal);
    }

    private static bool IsRetryable(WorkerFailureKind failureKind)
    {
        return failureKind is WorkerFailureKind.TransientInfra or WorkerFailureKind.Timeout;
    }

    private static WorkerFailureLayer ResolveFailureLayer(
        WorkerFailureKind failureKind,
        string stdout,
        string stderr,
        IReadOnlyCollection<CommandExecutionRecord> commandTrace,
        int changedFileCount)
    {
        var text = $"{stdout} {stderr}";
        return failureKind switch
        {
            WorkerFailureKind.EnvironmentBlocked => WorkerFailureLayer.Environment,
            WorkerFailureKind.TransientInfra or WorkerFailureKind.Timeout => WorkerFailureLayer.Transport,
            WorkerFailureKind.PolicyDenied or WorkerFailureKind.ApprovalRequired => WorkerFailureLayer.Environment,
            WorkerFailureKind.InvalidOutput => changedFileCount == 0 && commandTrace.Count <= 1
                ? WorkerFailureLayer.Protocol
                : WorkerFailureLayer.WorkerSemantic,
            WorkerFailureKind.TaskLogicFailed => text.Contains("build failed", StringComparison.OrdinalIgnoreCase)
                                                 && text.Contains("0 error(s)", StringComparison.OrdinalIgnoreCase)
                ? WorkerFailureLayer.Protocol
                : WorkerFailureLayer.WorkerSemantic,
            _ => WorkerFailureLayer.None,
        };
    }

    private static string? ExtractAgentMessageText(JsonElement item)
    {
        if (TryGetString(item, "text") is { Length: > 0 } directText)
        {
            return directText;
        }

        if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in content.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (TryGetString(entry, "text") is { Length: > 0 } entryText)
                {
                    return entryText;
                }
            }
        }

        return null;
    }

    private static string[] SplitCommand(string? commandText)
    {
        return string.IsNullOrWhiteSpace(commandText)
            ? Array.Empty<string>()
            : commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null,
        };
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(property.ToString(), out number) ? number : null;
    }
}
