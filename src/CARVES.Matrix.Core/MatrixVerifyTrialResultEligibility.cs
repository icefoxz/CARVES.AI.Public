using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static void ValidateTrialResultEligibility(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        JsonElement root)
    {
        ValidateTrialResultString(
            issues,
            requirement,
            "authority_mode",
            "local_only",
            GetString(root, "authority_mode"));
        ValidateTrialResultString(
            issues,
            requirement,
            "verification_status",
            "not_matrix_verified_by_collector",
            GetString(root, "verification_status"));
        ValidateTrialResultFalse(
            issues,
            requirement,
            "official_leaderboard_eligible",
            GetBool(root, "official_leaderboard_eligible"));
        ValidateTrialResultString(
            issues,
            requirement,
            "scoring_profile_id",
            AgentTrialVersionContract.ScoringProfileId,
            GetString(root, "scoring_profile_id"));
        ValidateTrialResultString(
            issues,
            requirement,
            "scoring_profile_version",
            AgentTrialVersionContract.ScoringProfileVersion,
            GetString(root, "scoring_profile_version"));
        ValidateTrialResultString(
            issues,
            requirement,
            "instruction_pack.prompt_id",
            GetString(root, "prompt_id") ?? string.Empty,
            GetString(root, "instruction_pack", "prompt_id"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "instruction_pack.prompt_version",
            GetString(root, "prompt_version") ?? string.Empty,
            GetString(root, "instruction_pack", "prompt_version"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultVersionComparability(issues, requirement, root);

        ValidateTrialResultString(
            issues,
            requirement,
            "leaderboard_eligibility.status",
            "ineligible_local_only",
            GetString(root, "leaderboard_eligibility", "status"));
        ValidateTrialResultString(
            issues,
            requirement,
            "leaderboard_eligibility.authority_mode",
            "local_only",
            GetString(root, "leaderboard_eligibility", "authority_mode"));
        ValidateTrialResultString(
            issues,
            requirement,
            "leaderboard_eligibility.verification_status",
            "not_matrix_verified_by_collector",
            GetString(root, "leaderboard_eligibility", "verification_status"));
        ValidateTrialResultFalse(
            issues,
            requirement,
            "leaderboard_eligibility.official_leaderboard_eligible",
            GetBool(root, "leaderboard_eligibility", "official_leaderboard_eligible"));

        var reasonCodes = GetStringArray(root, "leaderboard_eligibility", "reason_codes");
        if (!reasonCodes.Contains("local_dry_run_challenge", StringComparer.Ordinal))
        {
            issues.Add(new MatrixVerifyIssue(
                "trial_artifact",
                requirement.ArtifactKind,
                requirement.Path,
                "trial_result_eligibility_reason_missing:local_dry_run_challenge",
                "leaderboard_eligibility.reason_codes:local_dry_run_challenge",
                FormatStringArray(reasonCodes)));
        }
    }

    private static void ValidateTrialResultVersionComparability(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        JsonElement root)
    {
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.suite_id",
            GetString(root, "suite_id") ?? string.Empty,
            GetString(root, "version_comparability", "suite_id"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.pack_id",
            GetString(root, "pack_id") ?? string.Empty,
            GetString(root, "version_comparability", "pack_id"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.pack_version",
            GetString(root, "pack_version") ?? string.Empty,
            GetString(root, "version_comparability", "pack_version"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.task_id",
            GetString(root, "task_id") ?? string.Empty,
            GetString(root, "version_comparability", "task_id"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.task_version",
            GetString(root, "task_version") ?? string.Empty,
            GetString(root, "version_comparability", "task_version"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.instruction_pack_id",
            GetString(root, "instruction_pack", "instruction_pack_id") ?? string.Empty,
            GetString(root, "version_comparability", "instruction_pack_id"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.instruction_pack_version",
            GetString(root, "instruction_pack", "instruction_pack_version") ?? string.Empty,
            GetString(root, "version_comparability", "instruction_pack_version"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.prompt_id",
            GetString(root, "prompt_id") ?? string.Empty,
            GetString(root, "version_comparability", "prompt_id"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.prompt_version",
            GetString(root, "prompt_version") ?? string.Empty,
            GetString(root, "version_comparability", "prompt_version"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.scoring_profile_id",
            AgentTrialVersionContract.ScoringProfileId,
            GetString(root, "version_comparability", "scoring_profile_id"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.scoring_profile_version",
            AgentTrialVersionContract.ScoringProfileVersion,
            GetString(root, "version_comparability", "scoring_profile_version"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.collector_version",
            AgentTrialVersionContract.CollectorVersion,
            GetString(root, "version_comparability", "collector_version"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.matrix_verifier_version",
            AgentTrialVersionContract.MatrixVerifierVersionUnavailable,
            GetString(root, "version_comparability", "matrix_verifier_version"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.comparison_scope",
            AgentTrialVersionContract.ComparisonScope,
            GetString(root, "version_comparability", "comparison_scope"),
            "trial_result_version_field_mismatch");
        ValidateTrialResultString(
            issues,
            requirement,
            "version_comparability.cross_version_comparison",
            AgentTrialVersionContract.CrossVersionComparison,
            GetString(root, "version_comparability", "cross_version_comparison"),
            "trial_result_version_field_mismatch");
    }

    private static void ValidateTrialResultString(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        string fieldPath,
        string expected,
        string? actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        issues.Add(new MatrixVerifyIssue(
            "trial_artifact",
            requirement.ArtifactKind,
            requirement.Path,
            $"trial_result_eligibility_field_mismatch:{fieldPath}",
            $"{fieldPath}:{expected}",
            actual is null ? null : $"{fieldPath}:{actual}"));
    }

    private static void ValidateTrialResultString(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        string fieldPath,
        string expected,
        string? actual,
        string codePrefix)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        issues.Add(new MatrixVerifyIssue(
            "trial_artifact",
            requirement.ArtifactKind,
            requirement.Path,
            $"{codePrefix}:{fieldPath}",
            $"{fieldPath}:{expected}",
            actual is null ? null : $"{fieldPath}:{actual}"));
    }

    private static void ValidateTrialResultFalse(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        string fieldPath,
        bool? actual)
    {
        if (actual == false)
        {
            return;
        }

        issues.Add(new MatrixVerifyIssue(
            "trial_artifact",
            requirement.ArtifactKind,
            requirement.Path,
            $"trial_result_eligibility_field_mismatch:{fieldPath}",
            $"{fieldPath}:false",
            FormatBool(actual)));
    }
}
