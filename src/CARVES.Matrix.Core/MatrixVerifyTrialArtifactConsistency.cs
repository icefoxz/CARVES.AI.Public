using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static readonly string[] TrialIdentityFields =
    [
        "suite_id",
        "pack_id",
        "pack_version",
        "task_id",
        "task_version",
        "prompt_id",
        "prompt_version",
        "challenge_id",
    ];

    private static readonly IReadOnlyDictionary<string, string[]> TrialIdentityFieldsByArtifactKind =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["trial_task_contract"] = TrialIdentityFields,
            ["trial_agent_report"] = ["task_id", "task_version", "challenge_id"],
            ["trial_diff_scope_summary"] = ["task_id", "task_version", "challenge_id"],
            ["trial_test_evidence"] = ["task_id", "task_version", "challenge_id"],
            ["trial_result"] = TrialIdentityFields,
        };

    private static void ValidateTrialArtifactConsistency(
        List<MatrixVerifyIssue> issues,
        IReadOnlyDictionary<string, JsonElement> rootsByKind)
    {
        if (!rootsByKind.TryGetValue("trial_task_contract", out var taskContract))
        {
            return;
        }

        foreach (var (artifactKind, fields) in TrialIdentityFieldsByArtifactKind)
        {
            if (!rootsByKind.TryGetValue(artifactKind, out var root))
            {
                continue;
            }

            foreach (var field in fields)
            {
                ValidateTrialArtifactIdentityField(issues, artifactKind, root, taskContract, field);
            }
        }
    }

    private static void ValidateTrialArtifactIdentityField(
        List<MatrixVerifyIssue> issues,
        string artifactKind,
        JsonElement root,
        JsonElement taskContract,
        string field)
    {
        var expected = GetString(taskContract, field);
        var actual = GetString(root, field);
        if (expected is null || actual is null || string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        issues.Add(new MatrixVerifyIssue(
            "trial_artifact",
            artifactKind,
            ResolveTrialArtifactPath(artifactKind),
            $"trial_artifact_consistency_mismatch:{artifactKind}.{field}",
            $"trial_task_contract.{field}:{expected}",
            $"{artifactKind}.{field}:{actual}"));
    }

    private static string ResolveTrialArtifactPath(string artifactKind)
    {
        return MatrixArtifactManifestWriter.TrialArtifacts
            .FirstOrDefault(requirement => string.Equals(requirement.ArtifactKind, artifactKind, StringComparison.Ordinal))
            ?.Path ?? artifactKind;
    }
}
