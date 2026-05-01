using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

internal sealed record AgentTrialDiffSnapshot(
    IReadOnlyList<AgentTrialChangedFile> ChangedFiles,
    bool Available,
    string? Error,
    AgentTrialWorkspaceSnapshot PreCommandSnapshot,
    AgentTrialWorkspaceSnapshot PostCommandSnapshot);

internal sealed record AgentTrialWorkspaceSnapshot(
    string Phase,
    IReadOnlyList<AgentTrialChangedFile> ChangedFiles,
    bool Available,
    string? Error);

internal sealed record AgentTrialChangedFile(
    string Path,
    string ChangeKind,
    string Bucket,
    bool Allowed,
    bool Forbidden,
    bool ExpectedByTask,
    bool Generated,
    bool Deleted,
    bool ReplacementClaimed,
    bool Staged,
    bool Unstaged,
    bool Untracked,
    bool SeenPreCommand,
    bool SeenPostCommand);

internal static class AgentTrialLocalDiffReader
{
    public static AgentTrialDiffSnapshot Read(
        string workspaceRoot,
        AgentTrialTaskContract contract,
        AgentTrialAgentReport agentReport,
        string baseRef,
        TimeSpan timeout)
    {
        var snapshot = ReadWorkspaceSnapshot(
            workspaceRoot,
            contract,
            agentReport,
            phase: "post_command",
            timeout);

        return FromSnapshots(snapshot, snapshot);
    }

    public static AgentTrialWorkspaceSnapshot ReadWorkspaceSnapshot(
        string workspaceRoot,
        AgentTrialTaskContract contract,
        AgentTrialAgentReport agentReport,
        string phase,
        TimeSpan timeout)
    {
        var result = AgentTrialLocalProcessRunner.Run(
            "git",
            ["status", "--porcelain=v1", "-z", "--untracked-files=all"],
            workspaceRoot,
            timeout);

        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.Stderr) ? "git status failed" : result.Stderr.Trim();
            return new AgentTrialWorkspaceSnapshot(phase, [], Available: false, Error: message);
        }

        var changedFiles = ParsePorcelainStatus(result.Stdout, contract, agentReport, phase)
            .Where(file => file is not null)
            .Cast<AgentTrialChangedFile>()
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();

        return new AgentTrialWorkspaceSnapshot(phase, changedFiles, Available: true, Error: null);
    }

    public static AgentTrialDiffSnapshot FromSnapshots(
        AgentTrialWorkspaceSnapshot preCommandSnapshot,
        AgentTrialWorkspaceSnapshot postCommandSnapshot)
    {
        var available = preCommandSnapshot.Available && postCommandSnapshot.Available;
        var error = available
            ? null
            : string.Join("; ", new[] { preCommandSnapshot.Error, postCommandSnapshot.Error }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return new AgentTrialDiffSnapshot(
            MergeSnapshotFiles(preCommandSnapshot, postCommandSnapshot),
            available,
            string.IsNullOrWhiteSpace(error) ? null : error,
            preCommandSnapshot,
            postCommandSnapshot);
    }

    public static AgentTrialDiffSnapshot Unavailable(string? reason)
    {
        var pre = new AgentTrialWorkspaceSnapshot("pre_command", [], Available: false, Error: reason);
        var post = new AgentTrialWorkspaceSnapshot("post_command", [], Available: false, Error: reason);
        return new AgentTrialDiffSnapshot([], Available: false, Error: reason, pre, post);
    }

    public static JsonObject ToJson(
        AgentTrialDiffSnapshot snapshot,
        AgentTrialTaskContract contract,
        string baseRef,
        string worktreeRef)
    {
        var changedFiles = snapshot.ChangedFiles;
        var bucketCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["source"] = 0,
            ["test"] = 0,
            ["docs"] = 0,
            ["config"] = 0,
            ["generated"] = 0,
            ["metadata"] = 0,
            ["unknown"] = 0,
        };

        foreach (var file in changedFiles)
        {
            var aggregateBucket = file.Bucket == "artifact" ? "generated" : file.Bucket;
            if (!bucketCounts.ContainsKey(aggregateBucket))
            {
                aggregateBucket = "unknown";
            }

            bucketCounts[aggregateBucket]++;
        }

        var testsChanged = changedFiles.Any(file => file.Bucket == "test" && !file.Deleted);
        var sourceFilesChangedWithoutTests = testsChanged
            ? Array.Empty<string>()
            : changedFiles
                .Where(file => file.Bucket == "source" && !file.Deleted)
                .Select(file => file.Path)
                .ToArray();

        return new JsonObject
        {
            ["schema_version"] = "diff-scope-summary.v0",
            ["task_id"] = contract.TaskId,
            ["task_version"] = contract.TaskVersion,
            ["challenge_id"] = contract.ChallengeId,
            ["base_ref"] = baseRef,
            ["worktree_ref"] = worktreeRef,
            ["pre_command_snapshot"] = SnapshotToJson(snapshot.PreCommandSnapshot),
            ["post_command_snapshot"] = SnapshotToJson(snapshot.PostCommandSnapshot),
            ["changed_files"] = new JsonArray(changedFiles.Select(ToJson).ToArray<JsonNode?>()),
            ["changed_file_count"] = changedFiles.Count,
            ["buckets"] = new JsonObject
            {
                ["source"] = bucketCounts["source"],
                ["test"] = bucketCounts["test"],
                ["docs"] = bucketCounts["docs"],
                ["config"] = bucketCounts["config"],
                ["generated"] = bucketCounts["generated"],
                ["metadata"] = bucketCounts["metadata"],
                ["unknown"] = bucketCounts["unknown"],
            },
            ["allowed_scope_match"] = snapshot.Available && changedFiles.All(file => file.Allowed && !file.Forbidden),
            ["forbidden_path_violations"] = new JsonArray(changedFiles
                .Where(file => file.Forbidden)
                .Select(file => JsonValue.Create(file.Path))
                .ToArray<JsonNode?>()),
            ["unrequested_change_count"] = changedFiles.Count(file => !file.ExpectedByTask || file.Forbidden),
            ["source_files_changed_without_tests"] = new JsonArray(sourceFilesChangedWithoutTests
                .Select(path => JsonValue.Create(path))
                .ToArray<JsonNode?>()),
            ["deleted_files"] = new JsonArray(changedFiles
                .Where(file => file.Deleted)
                .Select(file => JsonValue.Create(file.Path))
                .ToArray<JsonNode?>()),
            ["privacy"] = new JsonObject
            {
                ["summary_only"] = true,
                ["raw_diff_included"] = false,
                ["source_included"] = false,
            },
        };
    }

    private static JsonObject SnapshotToJson(AgentTrialWorkspaceSnapshot snapshot)
    {
        var changedFiles = snapshot.ChangedFiles;
        return new JsonObject
        {
            ["phase"] = snapshot.Phase,
            ["available"] = snapshot.Available,
            ["error"] = snapshot.Error is null ? null : JsonValue.Create(snapshot.Error),
            ["changed_files"] = new JsonArray(changedFiles.Select(ToJson).ToArray<JsonNode?>()),
            ["changed_file_count"] = changedFiles.Count,
            ["forbidden_path_violations"] = new JsonArray(changedFiles
                .Where(file => file.Forbidden)
                .Select(file => JsonValue.Create(file.Path))
                .ToArray<JsonNode?>()),
            ["unrequested_change_count"] = changedFiles.Count(file => !file.ExpectedByTask || file.Forbidden),
            ["unknown_file_count"] = changedFiles.Count(file => file.Bucket == "unknown"),
        };
    }

    private static JsonObject ToJson(AgentTrialChangedFile file)
    {
        return new JsonObject
        {
            ["path"] = file.Path,
            ["change_kind"] = file.ChangeKind,
            ["bucket"] = file.Bucket,
            ["allowed"] = file.Allowed,
            ["forbidden"] = file.Forbidden,
            ["expected_by_task"] = file.ExpectedByTask,
            ["generated"] = file.Generated,
            ["deleted"] = file.Deleted,
            ["replacement_claimed"] = file.ReplacementClaimed,
            ["staged"] = file.Staged,
            ["unstaged"] = file.Unstaged,
            ["untracked"] = file.Untracked,
            ["seen_pre_command"] = file.SeenPreCommand,
            ["seen_post_command"] = file.SeenPostCommand,
        };
    }

    private static IReadOnlyList<AgentTrialChangedFile?> ParsePorcelainStatus(
        string stdout,
        AgentTrialTaskContract contract,
        AgentTrialAgentReport agentReport,
        string phase)
    {
        var entries = stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var files = new List<AgentTrialChangedFile?>();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry.Length < 4)
            {
                continue;
            }

            var status = entry[..2];
            var path = entry[3..];
            var renamed = status.Contains('R') || status.Contains('C');
            files.Add(ParseStatusEntry(status, path, contract, agentReport, phase));
            if (renamed && index + 1 < entries.Length)
            {
                index++;
            }
        }

        return files;
    }

    private static AgentTrialChangedFile? ParseStatusEntry(
        string status,
        string path,
        AgentTrialTaskContract contract,
        AgentTrialAgentReport agentReport,
        string phase)
    {
        var normalizedPath = NormalizeRelativePath(path);
        var staged = status[0] != ' ' && status[0] != '?';
        var unstaged = status[1] != ' ' && status[1] != '?';
        var untracked = string.Equals(status, "??", StringComparison.Ordinal);
        var deleted = status.Contains('D');
        var changeKind = status.Contains('R') ? "renamed" : status switch
        {
            _ when deleted => "deleted",
            _ when untracked || status.Contains('A') => "added",
            _ when status.Contains('M') || status.Contains('T') || status.Contains('U') => "modified",
            _ => "unknown"
        };
        var bucket = ClassifyBucket(normalizedPath);
        var forbidden = contract.ForbiddenPaths.Any(rule => MatchesRule(rule, normalizedPath));
        var generated = IsGeneratedArtifact(normalizedPath);
        var allowed = !forbidden && (generated || contract.AllowedPaths.Any(rule => MatchesRule(rule, normalizedPath)));
        var claimed = agentReport.ClaimedFilesChanged.Contains(normalizedPath);

        return new AgentTrialChangedFile(
            normalizedPath,
            changeKind,
            bucket,
            allowed,
            forbidden,
            allowed,
            generated,
            changeKind == "deleted",
            claimed,
            staged,
            unstaged,
            untracked,
            phase == "pre_command",
            phase == "post_command");
    }

    private static IReadOnlyList<AgentTrialChangedFile> MergeSnapshotFiles(
        AgentTrialWorkspaceSnapshot preCommandSnapshot,
        AgentTrialWorkspaceSnapshot postCommandSnapshot)
    {
        var files = new Dictionary<string, AgentTrialChangedFile>(StringComparer.Ordinal);
        foreach (var file in preCommandSnapshot.ChangedFiles)
        {
            files[file.Path] = file with { SeenPreCommand = true, SeenPostCommand = false };
        }

        foreach (var file in postCommandSnapshot.ChangedFiles)
        {
            files[file.Path] = files.TryGetValue(file.Path, out var existing)
                ? file with { SeenPreCommand = existing.SeenPreCommand, SeenPostCommand = true }
                : file with { SeenPreCommand = false, SeenPostCommand = true };
        }

        return files.Values
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(segment => segment == ".."))
        {
            throw new InvalidDataException($"Invalid changed path: {path}");
        }

        return normalized;
    }

    private static bool MatchesRule(string rule, string path)
    {
        var normalizedRule = rule.Replace('\\', '/').Trim();
        if (normalizedRule.EndsWith("/", StringComparison.Ordinal))
        {
            return path.StartsWith(normalizedRule, StringComparison.Ordinal);
        }

        return string.Equals(path, normalizedRule, StringComparison.Ordinal);
    }

    private static string ClassifyBucket(string path)
    {
        if (IsGeneratedArtifact(path) && !path.StartsWith("artifacts/", StringComparison.Ordinal))
        {
            return "generated";
        }

        if (path.StartsWith("src/", StringComparison.Ordinal))
        {
            return "source";
        }

        if (path.StartsWith("tests/", StringComparison.Ordinal))
        {
            return "test";
        }

        if (path.StartsWith("docs/", StringComparison.Ordinal) || path.EndsWith(".md", StringComparison.Ordinal))
        {
            return "docs";
        }

        if (path.StartsWith("artifacts/", StringComparison.Ordinal))
        {
            return "artifact";
        }

        if (path.StartsWith(".carves/", StringComparison.Ordinal) ||
            path.StartsWith(".github/", StringComparison.Ordinal) ||
            path.EndsWith(".json", StringComparison.Ordinal) ||
            path.EndsWith(".csproj", StringComparison.Ordinal))
        {
            return "config";
        }

        return "unknown";
    }

    private static bool IsGeneratedArtifact(string path)
    {
        return path.StartsWith("artifacts/", StringComparison.Ordinal) ||
            path.Contains("/bin/", StringComparison.Ordinal) ||
            path.Contains("/obj/", StringComparison.Ordinal) ||
            path.StartsWith("bin/", StringComparison.Ordinal) ||
            path.StartsWith("obj/", StringComparison.Ordinal) ||
            path.StartsWith("TestResults/", StringComparison.Ordinal);
    }
}
