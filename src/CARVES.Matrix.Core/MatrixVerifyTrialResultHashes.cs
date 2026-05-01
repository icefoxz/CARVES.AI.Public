using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static readonly IReadOnlyDictionary<string, string> TrialResultArtifactHashFields =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trial_task_contract"] = "task_contract_sha256",
            ["trial_agent_report"] = "agent_report_sha256",
            ["trial_diff_scope_summary"] = "diff_scope_summary_sha256",
            ["trial_test_evidence"] = "test_evidence_sha256",
        };

    private static void ValidateTrialResultArtifactHashes(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        JsonElement root,
        IReadOnlyDictionary<string, MatrixTrialManifestEntry[]> entriesByKind)
    {
        foreach (var (artifactKind, hashField) in TrialResultArtifactHashFields)
        {
            if (!entriesByKind.TryGetValue(artifactKind, out var entries) || entries.Length != 1)
            {
                continue;
            }

            var expected = "sha256:" + entries[0].Sha256;
            var actual = GetString(root, "artifact_hashes", hashField);
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                issues.Add(new MatrixVerifyIssue(
                    "trial_artifact",
                    requirement.ArtifactKind,
                    requirement.Path,
                    "trial_result_artifact_hash_mismatch",
                    $"{hashField}:{expected}",
                    actual is null ? null : $"{hashField}:{actual}"));
            }
        }

        var taskContractHash = GetString(root, "artifact_hashes", "task_contract_sha256");
        var actualTaskContractHash = GetString(root, "artifact_hashes", "actual_task_contract_sha256");
        if (!string.Equals(taskContractHash, actualTaskContractHash, StringComparison.Ordinal))
        {
            issues.Add(new MatrixVerifyIssue(
                "trial_artifact",
                requirement.ArtifactKind,
                requirement.Path,
                "trial_result_task_contract_actual_hash_mismatch",
                taskContractHash is null ? null : $"task_contract_sha256:{taskContractHash}",
                actualTaskContractHash is null ? null : $"actual_task_contract_sha256:{actualTaskContractHash}"));
        }

        var expectedSelfHash = ComputeTrialResultSelfHash(root);
        var actualSelfHash = GetString(root, "artifact_hashes", "trial_result_sha256");
        if (!string.Equals(expectedSelfHash, actualSelfHash, StringComparison.Ordinal))
        {
            issues.Add(new MatrixVerifyIssue(
                "trial_artifact",
                requirement.ArtifactKind,
                requirement.Path,
                "trial_result_artifact_hash_mismatch",
                $"trial_result_sha256:{expectedSelfHash}",
                actualSelfHash is null ? null : $"trial_result_sha256:{actualSelfHash}"));
        }
    }

    private static string ComputeTrialResultSelfHash(JsonElement root)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(root.GetRawText())!.AsObject();
        node["artifact_hashes"]!.AsObject()["trial_result_sha256"] = AgentTrialLocalJson.MissingArtifactHash;
        return AgentTrialLocalJson.HashString(node.ToJsonString(new JsonSerializerOptions()));
    }
}
