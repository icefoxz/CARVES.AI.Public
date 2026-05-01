using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const string PortablePackageStateSchemaVersion = "carves-portable-agent-trial-state.v0";
    private const string PortablePackageStateReady = "ready_for_agent";
    private const string PortablePackageStateScored = "scored";
    private const string PortablePackageStateFailed = "failed";
    private const string PortablePackageStateContaminated = "contaminated";
    private const string PortablePackageAlreadyScoredMessage = "Portable package already scored.";
    private const string PortablePackageAlreadyFailedMessage = "Portable package already failed.";
    private const string PortablePackageAlreadyContaminatedMessage = "Portable package already contaminated.";
    private const string PortablePackageStateMissingMessage = "Portable package state missing.";
    private const string PortablePackageStateInvalidMessage = "Portable package state invalid.";
    private const string PortablePackageStaleResultsMessage = "Portable package stale results.";
    private const string PortablePackageJudgeEvidenceMessage = "Portable package judge evidence present.";
    private const string PortablePackageUnexpectedRootEntryMessage = "Portable package unexpected root entry.";

    private static readonly HashSet<string> PortablePackageAllowedRootEntries = new(StringComparer.Ordinal)
    {
        "README-FIRST.md",
        "COPY_THIS_TO_AGENT_BLIND.txt",
        "COPY_THIS_TO_AGENT_GUIDED.txt",
        "SCORE.cmd",
        "score.sh",
        "RESULT.cmd",
        "result.sh",
        "RESET.cmd",
        "reset.sh",
        "agent-workspace",
        ".carves-pack",
        "results",
        "tools",
    };

    private static readonly HashSet<string> PortablePackageAllowedInitialArtifactEntries = new(StringComparer.Ordinal)
    {
        ".gitkeep",
        "README.md",
        "agent-report.template.json",
        "agent-report.json",
    };

    private sealed record PortablePackageState(
        string SchemaVersion,
        string State,
        string UpdatedAt,
        string? LastStatus,
        IReadOnlyList<string> ReasonCodes,
        IReadOnlyList<string> EvidenceRefs,
        string LocalResultsRoot,
        string SubmitBundleRoot);

    private static void WriteInitialPortablePackageState(string scorerAuthorityRoot, DateTimeOffset createdAt)
    {
        WritePortablePackageState(
            Path.Combine(scorerAuthorityRoot, "state.json"),
            new PortablePackageState(
                PortablePackageStateSchemaVersion,
                PortablePackageStateReady,
                createdAt.ToString("O"),
                LastStatus: null,
                ReasonCodes: [],
                EvidenceRefs: [],
                "results/local",
                "results/submit-bundle"));
    }

    private static void GuardPortablePackageBeforeCollect(PortablePackageCollectPaths? paths)
    {
        if (paths is null)
        {
            return;
        }

        var state = ReadPortablePackageState(paths);
        if (state is null)
        {
            MarkPortablePackageState(
                paths,
                PortablePackageStateContaminated,
                "refused",
                ["portable_package_state_missing"],
                [".carves-pack/state.json"]);
            throw new InvalidOperationException(PortablePackageStateMissingMessage);
        }

        if (!string.Equals(state.SchemaVersion, PortablePackageStateSchemaVersion, StringComparison.Ordinal))
        {
            MarkPortablePackageState(
                paths,
                PortablePackageStateContaminated,
                "refused",
                ["portable_package_state_invalid"],
                [".carves-pack/state.json"]);
            throw new InvalidOperationException(PortablePackageStateInvalidMessage);
        }

        switch (state.State)
        {
            case PortablePackageStateReady:
                break;
            case PortablePackageStateScored:
                throw new InvalidOperationException(PortablePackageAlreadyScoredMessage);
            case PortablePackageStateFailed:
                throw new InvalidOperationException(PortablePackageAlreadyFailedMessage);
            case PortablePackageStateContaminated:
                throw new InvalidOperationException(PortablePackageAlreadyContaminatedMessage);
            default:
                MarkPortablePackageState(
                    paths,
                    PortablePackageStateContaminated,
                    "refused",
                    ["portable_package_state_invalid"],
                    [".carves-pack/state.json"]);
                throw new InvalidOperationException(PortablePackageStateInvalidMessage);
        }

        if (HasAnyFileSystemEntry(paths.LocalResultsRoot) || HasAnyFileSystemEntry(paths.SubmitBundleRoot))
        {
            MarkPortablePackageState(
                paths,
                PortablePackageStateContaminated,
                "refused",
                ["portable_package_stale_results"],
                ["results/local", "results/submit-bundle"]);
            throw new InvalidOperationException(PortablePackageStaleResultsMessage);
        }

        var judgeEvidence = FindPreExistingPortableJudgeEvidence(paths.WorkspaceRoot).ToArray();
        if (judgeEvidence.Length > 0)
        {
            MarkPortablePackageState(
                paths,
                PortablePackageStateContaminated,
                "refused",
                ["portable_package_judge_evidence_present"],
                judgeEvidence);
            throw new InvalidOperationException(PortablePackageJudgeEvidenceMessage);
        }

        var unexpectedRootEntries = FindUnexpectedPortableRootEntries(paths.PackageRoot).ToArray();
        if (unexpectedRootEntries.Length > 0)
        {
            MarkPortablePackageState(
                paths,
                PortablePackageStateContaminated,
                "refused",
                ["portable_package_unexpected_root_entry"],
                unexpectedRootEntries);
            throw new InvalidOperationException(PortablePackageUnexpectedRootEntryMessage);
        }
    }

    private static void MarkPortablePackageCollectFinished(
        PortablePackageCollectPaths? paths,
        string commandStatus,
        TrialCollectionReadback collection,
        TrialVerificationReadback? verification)
    {
        if (paths is null)
        {
            return;
        }

        var state = string.Equals(commandStatus, "verified", StringComparison.Ordinal)
            ? PortablePackageStateScored
            : PortablePackageStateFailed;
        var reasonCodes = collection.FailureReasons
            .Concat(verification?.ReasonCodes ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        MarkPortablePackageState(
            paths,
            state,
            commandStatus,
            reasonCodes,
            [ToPackageRelativePath(paths.PackageRoot, paths.LocalResultsRoot), ToPackageRelativePath(paths.PackageRoot, paths.SubmitBundleRoot)]);
    }

    private static void MarkPortablePackageCollectException(PortablePackageCollectPaths? paths, Exception exception)
    {
        if (paths is null)
        {
            return;
        }

        MarkPortablePackageState(
            paths,
            PortablePackageStateFailed,
            "failed",
            [exception.GetType().Name],
            [".carves-pack/state.json", "agent-workspace"]);
    }

    private static PortablePackageState? ReadPortablePackageState(PortablePackageCollectPaths paths)
    {
        var statePath = PortablePackageStatePath(paths);
        if (!File.Exists(statePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PortablePackageState>(File.ReadAllText(statePath), JsonOptions);
        }
        catch (JsonException)
        {
            return new PortablePackageState(
                PortablePackageStateSchemaVersion,
                State: "invalid",
                UpdatedAt: DateTimeOffset.UtcNow.ToString("O"),
                LastStatus: "invalid",
                ReasonCodes: ["portable_package_state_invalid"],
                EvidenceRefs: [".carves-pack/state.json"],
                "results/local",
                "results/submit-bundle");
        }
    }

    private static void MarkPortablePackageState(
        PortablePackageCollectPaths paths,
        string state,
        string? lastStatus,
        IReadOnlyList<string> reasonCodes,
        IReadOnlyList<string> evidenceRefs)
    {
        WritePortablePackageState(
            PortablePackageStatePath(paths),
            new PortablePackageState(
                PortablePackageStateSchemaVersion,
                state,
                DateTimeOffset.UtcNow.ToString("O"),
                lastStatus,
                reasonCodes,
                evidenceRefs,
                ToPackageRelativePath(paths.PackageRoot, paths.LocalResultsRoot),
                ToPackageRelativePath(paths.PackageRoot, paths.SubmitBundleRoot)));
    }

    private static void WritePortablePackageState(string statePath, PortablePackageState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(statePath))!);
        File.WriteAllText(statePath, JsonSerializer.Serialize(state, JsonOptions) + Environment.NewLine);
    }

    private static string PortablePackageStatePath(PortablePackageCollectPaths paths)
    {
        return Path.Combine(paths.PackageRoot, ".carves-pack", "state.json");
    }

    private static bool HasAnyFileSystemEntry(string directory)
    {
        return Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any();
    }

    private static IEnumerable<string> FindUnexpectedPortableRootEntries(string packageRoot)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(packageRoot))
        {
            var name = Path.GetFileName(entry);
            if (!PortablePackageAllowedRootEntries.Contains(name))
            {
                yield return name;
            }
        }
    }

    private static IEnumerable<string> FindPreExistingPortableJudgeEvidence(string workspaceRoot)
    {
        var artifactRoot = Path.Combine(workspaceRoot, "artifacts");
        if (!Directory.Exists(artifactRoot))
        {
            yield break;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(artifactRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = ToPackageRelativePath(artifactRoot, entry);
            if (PortablePackageAllowedInitialArtifactEntries.Contains(relativePath))
            {
                continue;
            }

            yield return "agent-workspace/artifacts/" + relativePath;
        }
    }

    private static string ToPackageRelativePath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }
}
