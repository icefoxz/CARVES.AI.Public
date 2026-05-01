using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

internal static class AgentTrialPortableBaselineValidator
{
    public static void Validate(
        string workspaceRoot,
        string? baselinePath,
        TimeSpan timeout,
        List<string> missing,
        List<string> reasons)
    {
        if (baselinePath is null)
        {
            return;
        }

        PortableBaselineManifest baseline;
        try
        {
            baseline = ReadBaselineManifest(baselinePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            reasons.Add("portable_baseline_metadata_invalid");
            return;
        }

        if (!Directory.Exists(Path.Combine(workspaceRoot, ".git")))
        {
            missing.Add("portable_git_baseline");
            reasons.Add("portable_baseline_git_missing");
            return;
        }

        if (!GitSucceeded(workspaceRoot, timeout, "cat-file", "-e", baseline.BaselineCommitSha + "^{commit}"))
        {
            reasons.Add("portable_baseline_commit_missing");
            return;
        }

        var tree = RunGit(workspaceRoot, timeout, "rev-parse", baseline.BaselineCommitSha + "^{tree}");
        if (tree.ExitCode != 0 || !string.Equals(tree.Stdout.Trim(), baseline.BaselineTreeSha, StringComparison.Ordinal))
        {
            reasons.Add("portable_baseline_tree_mismatch");
            return;
        }

        var listed = RunGit(workspaceRoot, timeout, "ls-tree", "-r", "--long", baseline.BaselineCommitSha);
        if (listed.ExitCode != 0 || !BaselineFilesMatch(ParseLsTree(listed.Stdout), baseline.InitialFiles))
        {
            reasons.Add("portable_baseline_initial_files_mismatch");
        }

        foreach (var file in baseline.InitialFiles.Where(static file => file.Protected))
        {
            var fullPath = Path.Combine(workspaceRoot, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath) || !string.Equals(AgentTrialLocalJson.HashFile(fullPath), file.Sha256, StringComparison.Ordinal))
            {
                reasons.Add("portable_protected_metadata_changed");
                break;
            }
        }
    }

    private static PortableBaselineManifest ReadBaselineManifest(string path)
    {
        var root = AgentTrialLocalJson.ReadObject(path);
        var files = root["initial_files"]?.AsArray()
            ?? throw new InvalidDataException("Required array property is missing: initial_files");
        return new PortableBaselineManifest(
            AgentTrialLocalJson.GetRequiredString(root, "baseline_commit_sha"),
            AgentTrialLocalJson.GetRequiredString(root, "baseline_tree_sha"),
            files.OfType<JsonObject>().Select(ReadBaselineFile).ToArray());
    }

    private static PortableBaselineFile ReadBaselineFile(JsonObject root)
    {
        return new PortableBaselineFile(
            AgentTrialLocalJson.GetRequiredString(root, "path"),
            AgentTrialLocalJson.GetRequiredString(root, "git_blob_sha"),
            ReadRequiredInt64(root, "size"),
            AgentTrialLocalJson.GetRequiredString(root, "sha256"),
            AgentTrialLocalJson.GetBooleanOrDefault(root, "protected", false));
    }

    private static long ReadRequiredInt64(JsonObject root, string propertyName)
    {
        var value = root[propertyName]
            ?? throw new InvalidDataException($"Required integer property is missing: {propertyName}");

        return value.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? long.Parse(value.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture)
            : value.GetValue<long>();
    }

    private static bool GitSucceeded(string workspaceRoot, TimeSpan timeout, params string[] arguments)
    {
        return RunGit(workspaceRoot, timeout, arguments).ExitCode == 0;
    }

    private static AgentTrialProcessResult RunGit(string workspaceRoot, TimeSpan timeout, params string[] arguments)
    {
        return AgentTrialLocalProcessRunner.Run("git", arguments, workspaceRoot, timeout);
    }

    private static IReadOnlyDictionary<string, (string BlobSha, long Size)> ParseLsTree(string stdout)
    {
        var files = new Dictionary<string, (string BlobSha, long Size)>(StringComparer.Ordinal);
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tabIndex = line.IndexOf('\t');
            if (tabIndex <= 0)
            {
                continue;
            }

            var header = line[..tabIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (header.Length < 4)
            {
                continue;
            }

            files[line[(tabIndex + 1)..].Replace('\\', '/')] =
                (header[2], long.Parse(header[3], System.Globalization.CultureInfo.InvariantCulture));
        }

        return files;
    }

    private static bool BaselineFilesMatch(
        IReadOnlyDictionary<string, (string BlobSha, long Size)> actual,
        IReadOnlyList<PortableBaselineFile> expected)
    {
        return actual.Count == expected.Count
            && expected.All(file =>
                actual.TryGetValue(file.Path, out var actualFile)
                && string.Equals(actualFile.BlobSha, file.GitBlobSha, StringComparison.Ordinal)
                && actualFile.Size == file.Size);
    }

    private sealed record PortableBaselineManifest(
        string BaselineCommitSha,
        string BaselineTreeSha,
        IReadOnlyList<PortableBaselineFile> InitialFiles);

    private sealed record PortableBaselineFile(
        string Path,
        string GitBlobSha,
        long Size,
        string Sha256,
        bool Protected);
}
