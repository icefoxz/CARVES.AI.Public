using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static readonly IReadOnlyDictionary<string, string[]> TrialRequiredFieldsByArtifactKind =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["trial_task_contract"] =
            [
                "suite_id",
                "pack_id",
                "pack_version",
                "task_id",
                "task_version",
                "prompt_id",
                "prompt_version",
                "challenge_id",
                "challenge_source",
                "objective",
                "instruction_class",
                "allowed_paths",
                "forbidden_paths",
                "required_commands",
                "permission_profile",
                "tool_profile",
                "expected_evidence",
                "failure_posture",
                "stop_and_ask_conditions",
                "result_mode",
                "official_leaderboard_eligible",
                "privacy",
            ],
            ["trial_agent_report"] =
            [
                "task_id",
                "task_version",
                "challenge_id",
                "agent_profile_snapshot",
                "completion_status",
                "claimed_files_changed",
                "claimed_tests_run",
                "claimed_tests_passed",
                "risks",
                "deviations",
                "blocked_or_uncertain_decisions",
                "follow_up_work",
                "evidence_refs",
                "privacy",
            ],
            ["trial_diff_scope_summary"] =
            [
                "task_id",
                "task_version",
                "challenge_id",
                "base_ref",
                "worktree_ref",
                "pre_command_snapshot",
                "post_command_snapshot",
                "changed_files",
                "changed_file_count",
                "buckets",
                "allowed_scope_match",
                "forbidden_path_violations",
                "unrequested_change_count",
                "source_files_changed_without_tests",
                "deleted_files",
                "privacy",
            ],
            ["trial_test_evidence"] =
            [
                "task_id",
                "task_version",
                "challenge_id",
                "required_commands",
                "executed_commands",
                "missing_required_commands",
                "summary",
                "failure_summary",
                "log_hashes",
                "result_artifact_hashes",
                "agent_claimed_tests_passed",
                "privacy",
            ],
            ["trial_result"] =
            [
                "result_mode",
                "suite_id",
                "pack_id",
                "pack_version",
                "task_id",
                "task_version",
                "prompt_id",
                "prompt_version",
                "instruction_pack",
                "challenge_id",
                "challenge_source",
                "scoring_profile_id",
                "scoring_profile_version",
                "local_score",
                "local_collection_status",
                "authority_mode",
                "verification_status",
                "official_leaderboard_eligible",
                "version_comparability",
                "ineligibility_reasons",
                "leaderboard_eligibility",
                "artifact_hashes",
                "missing_required_artifacts",
                "privacy",
                "created_at",
            ],
        };

    private static readonly IReadOnlyDictionary<string, string[]> TrialFalsePrivacyFieldsByArtifactKind =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["trial_task_contract"] =
            [
                "source_upload_required",
                "raw_diff_included",
                "prompt_response_included",
                "model_response_included",
                "secrets_included",
                "credentials_included",
                "customer_payload_included",
            ],
            ["trial_agent_report"] =
            [
                "prompt_response_included",
                "model_response_included",
                "raw_diff_included",
                "source_included",
                "secrets_included",
                "credentials_included",
                "customer_payload_included",
            ],
            ["trial_diff_scope_summary"] = ["raw_diff_included", "source_included"],
            ["trial_test_evidence"] = ["full_logs_included", "source_included", "raw_diff_included"],
            ["trial_result"] =
            [
                "source_included",
                "raw_diff_included",
                "prompt_response_included",
                "model_response_included",
                "full_logs_included",
                "secrets_included",
                "credentials_included",
                "customer_payload_included",
                "certification_claim",
            ],
        };

    private static void ValidateTrialArtifactContent(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        JsonElement root,
        IReadOnlyDictionary<string, MatrixTrialManifestEntry[]> entriesByKind)
    {
        var expectedSchema = requirement.SchemaVersion;
        var actualSchema = GetString(root, "schema_version");
        if (!string.Equals(expectedSchema, actualSchema, StringComparison.Ordinal))
        {
            issues.Add(new MatrixVerifyIssue(
                "trial_artifact",
                requirement.ArtifactKind,
                requirement.Path,
                "trial_artifact_schema_version_mismatch",
                expectedSchema,
                actualSchema));
        }

        ValidateTrialRequiredFields(issues, requirement, root);
        ValidateTrialArtifactSchema(issues, requirement, root);
        ValidateTrialPrivacy(issues, requirement, root);
        if (string.Equals(requirement.ArtifactKind, "trial_result", StringComparison.Ordinal))
        {
            ValidateTrialResultEligibility(issues, requirement, root);
            ValidateTrialResultLocalScore(issues, requirement, root);
            ValidateTrialResultArtifactHashes(issues, requirement, root, entriesByKind);
        }
    }

    private static void ValidateTrialRequiredFields(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        JsonElement root)
    {
        if (!TrialRequiredFieldsByArtifactKind.TryGetValue(requirement.ArtifactKind, out var requiredFields))
        {
            return;
        }

        foreach (var field in requiredFields)
        {
            if (!root.TryGetProperty(field, out _))
            {
                issues.Add(new MatrixVerifyIssue(
                    "trial_artifact",
                    requirement.ArtifactKind,
                    requirement.Path,
                    $"trial_artifact_required_field_missing:{field}",
                    field,
                    null));
            }
        }
    }

    private static void ValidateTrialPrivacy(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        JsonElement root)
    {
        if (!root.TryGetProperty("privacy", out var privacy) || privacy.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new MatrixVerifyIssue(
                "trial_artifact",
                requirement.ArtifactKind,
                requirement.Path,
                "privacy_trial_flags_missing",
                "privacy",
                null));
            return;
        }

        if (GetBool(root, "privacy", "summary_only") != true)
        {
            issues.Add(new MatrixVerifyIssue(
                "trial_artifact",
                requirement.ArtifactKind,
                requirement.Path,
                "privacy_trial_summary_only_not_true",
                "privacy.summary_only:true",
                FormatBool(GetBool(root, "privacy", "summary_only"))));
        }

        if (!TrialFalsePrivacyFieldsByArtifactKind.TryGetValue(requirement.ArtifactKind, out var falseFields))
        {
            return;
        }

        foreach (var field in falseFields)
        {
            var value = GetBool(root, "privacy", field);
            if (!value.HasValue)
            {
                issues.Add(new MatrixVerifyIssue(
                    "trial_artifact",
                    requirement.ArtifactKind,
                    requirement.Path,
                    $"privacy_trial_flag_missing:{field}",
                    $"privacy.{field}:false",
                    null));
                continue;
            }

            if (value.Value)
            {
                issues.Add(new MatrixVerifyIssue(
                    "trial_artifact",
                    requirement.ArtifactKind,
                    requirement.Path,
                    $"privacy_trial_forbidden_flag_true:{field}",
                    $"privacy.{field}:false",
                    $"privacy.{field}:true"));
            }
        }
    }

}
