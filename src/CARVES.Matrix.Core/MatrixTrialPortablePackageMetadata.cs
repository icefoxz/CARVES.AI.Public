using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const string PortablePackageManifestSchemaVersion = "carves-portable-agent-trial-pack.v0";
    private const string PortablePackageBaselineSchemaVersion = "carves-portable-agent-trial-baseline.v0";
    private const string PortablePackageExpectedArtifactSchemaVersion = "carves-portable-agent-trial-expected-artifact.v0";
    private const string PortablePackageScoringContractSchemaVersion = "carves-portable-agent-trial-scoring-contract.v0";

    private static void WritePortablePackManifest(
        string path,
        DateTimeOffset createdAt,
        JsonObject pack,
        JsonObject challenge,
        string baselineCommitSha,
        IReadOnlyList<string> nonClaims)
    {
        AgentTrialLocalJson.WriteObject(path, new JsonObject
        {
            ["schema_version"] = PortablePackageManifestSchemaVersion,
            ["package_id"] = "portable-" + AgentTrialLocalJson.GetRequiredString(pack, "pack_id"),
            ["package_version"] = "0.1.0-local",
            ["created_at"] = createdAt.ToString("O"),
            ["layout_contract"] = "CARD-938",
            ["agent_workspace"] = "agent-workspace",
            ["scorer_authority"] = ".carves-pack",
            ["results_root"] = "results",
            ["submit_bundle_root"] = "results/submit-bundle",
            ["pack_id"] = AgentTrialLocalJson.GetRequiredString(pack, "pack_id"),
            ["pack_version"] = AgentTrialLocalJson.GetRequiredString(pack, "pack_version"),
            ["suite_id"] = AgentTrialLocalJson.GetRequiredString(pack, "suite_id"),
            ["task_id"] = AgentTrialLocalJson.GetRequiredString(challenge, "task_id"),
            ["task_version"] = AgentTrialLocalJson.GetRequiredString(challenge, "task_version"),
            ["prompt_id"] = AgentTrialLocalJson.GetRequiredString(challenge, "prompt_id"),
            ["prompt_version"] = AgentTrialLocalJson.GetRequiredString(challenge, "prompt_version"),
            ["challenge_id"] = AgentTrialLocalJson.GetRequiredString(challenge, "challenge_id"),
            ["challenge_source"] = AgentTrialLocalJson.GetRequiredString(challenge, "challenge_source"),
            ["scoring_profile_id"] = AgentTrialVersionContract.ScoringProfileId,
            ["scoring_profile_version"] = AgentTrialVersionContract.ScoringProfileVersion,
            ["required_carves_min_version"] = AgentTrialVersionContract.RequiredCarvesMinimumVersion,
            ["baseline_commit_sha"] = baselineCommitSha,
            ["local_only"] = true,
            ["server_submission"] = false,
            ["leaderboard_eligible"] = false,
            ["non_claims"] = ToJsonArray(nonClaims)
        });
    }

    private static void WritePortableBaselineManifest(
        string path,
        DateTimeOffset createdAt,
        string agentWorkspaceRoot,
        string baselineCommitSha)
    {
        var baselineTreeSha = RunTrialGitCapture(agentWorkspaceRoot, "rev-parse", baselineCommitSha + "^{tree}");
        var initialFiles = ReadPortableBaselineFiles(agentWorkspaceRoot, baselineCommitSha);
        AgentTrialLocalJson.WriteObject(path, new JsonObject
        {
            ["schema_version"] = PortablePackageBaselineSchemaVersion,
            ["created_at"] = createdAt.ToString("O"),
            ["workspace_relative_path"] = "agent-workspace",
            ["git_initialized"] = true,
            ["manual_git_setup_required"] = false,
            ["baseline_commit_sha"] = baselineCommitSha,
            ["baseline_tree_sha"] = baselineTreeSha,
            ["tracked_file_count"] = initialFiles.Count,
            ["initial_files"] = new JsonArray(initialFiles
                .Select(ToJson)
                .ToArray<JsonNode?>())
        });
    }

    private static void WriteExpectedArtifactMetadata(
        string path,
        string artifactKind,
        string workspaceRelativePath,
        string authorityRelativePath,
        string authorityPath)
    {
        AgentTrialLocalJson.WriteObject(path, new JsonObject
        {
            ["schema_version"] = PortablePackageExpectedArtifactSchemaVersion,
            ["artifact_kind"] = artifactKind,
            ["workspace_relative_path"] = workspaceRelativePath,
            ["authority_relative_path"] = authorityRelativePath,
            ["sha256"] = AgentTrialLocalJson.HashFile(authorityPath),
            ["source"] = "official_starter_pack",
            ["pinned"] = true
        });
    }

    private static void WritePortableScoringContract(string path)
    {
        AgentTrialLocalJson.WriteObject(path, new JsonObject
        {
            ["schema_version"] = PortablePackageScoringContractSchemaVersion,
            ["scoring_profile_id"] = AgentTrialVersionContract.ScoringProfileId,
            ["scoring_profile_version"] = AgentTrialVersionContract.ScoringProfileVersion,
            ["collector_version"] = AgentTrialVersionContract.CollectorVersion,
            ["comparison_scope"] = AgentTrialVersionContract.ComparisonScope,
            ["cross_version_comparison"] = AgentTrialVersionContract.CrossVersionComparison,
            ["dimensions"] = ToJsonArray([
                "reviewability",
                "traceability",
                "explainability",
                "report_honesty",
                "constraint",
                "reproducibility"]),
            ["local_only"] = true,
            ["leaderboard_eligible"] = false
        });
    }

    private static IReadOnlyList<PortableBaselineFile> ReadPortableBaselineFiles(string workspaceRoot, string baselineCommitSha)
    {
        var stdout = RunTrialGitCapture(workspaceRoot, "ls-tree", "-r", "--long", baselineCommitSha);
        var files = new List<PortableBaselineFile>();
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

            var relativePath = line[(tabIndex + 1)..].Replace('\\', '/');
            var fullPath = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            files.Add(new PortableBaselineFile(
                relativePath,
                header[2],
                long.Parse(header[3], System.Globalization.CultureInfo.InvariantCulture),
                AgentTrialLocalJson.HashFile(fullPath),
                IsProtectedPortableBaselinePath(relativePath)));
        }

        return files.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray();
    }

    private static bool IsProtectedPortableBaselinePath(string relativePath)
    {
        return relativePath.StartsWith(".carves/", StringComparison.Ordinal)
            || relativePath.StartsWith("prompts/", StringComparison.Ordinal)
            || relativePath.StartsWith("tasks/", StringComparison.Ordinal)
            || string.Equals(relativePath, "AGENTS.md", StringComparison.Ordinal)
            || string.Equals(relativePath, "CLAUDE.md", StringComparison.Ordinal)
            || string.Equals(relativePath, "README.md", StringComparison.Ordinal)
            || relativePath.EndsWith(".csproj", StringComparison.Ordinal);
    }

    private static JsonObject ToJson(PortableBaselineFile file)
    {
        return new JsonObject
        {
            ["path"] = file.Path,
            ["git_blob_sha"] = file.GitBlobSha,
            ["size"] = file.Size.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sha256"] = file.Sha256,
            ["protected"] = file.Protected
        };
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static IReadOnlyList<string> BuildPortablePackageNonClaims()
    {
        return
        [
            "local_only",
            "not_certification",
            "not_hosted_verification",
            "not_leaderboard_eligible",
            "not_producer_identity",
            "not_os_sandbox",
            "not_semantic_correctness",
            "not_local_anti_cheat",
            "not_tamper_proof"
        ];
    }

    private sealed record PortableBaselineFile(
        string Path,
        string GitBlobSha,
        long Size,
        string Sha256,
        bool Protected);
}
