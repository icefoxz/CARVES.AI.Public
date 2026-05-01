using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

internal static class AgentTrialLocalEvidenceBuilder
{
    public static JsonObject BuildTestEvidence(
        AgentTrialTaskContract contract,
        AgentTrialAgentReport agentReport,
        IReadOnlyList<(string Command, AgentTrialProcessResult Result)> commandResults)
    {
        var executedCommands = new JsonArray();
        var passed = 0;
        var failed = 0;
        var skipped = 0;
        var errors = 0;
        var logHashes = new List<string>();
        var resultHashes = new List<string>();
        var failures = new JsonArray();

        for (var index = 0; index < commandResults.Count; index++)
        {
            var (command, result) = commandResults[index];
            var status = result.TimedOut ? "timed_out" : result.ExitCode == 0 ? "passed" : "failed";
            if (status == "passed")
            {
                passed++;
            }
            else if (status == "timed_out")
            {
                errors++;
                failures.Add($"cmd_{index + 1:000}: timed out");
            }
            else
            {
                failed++;
                failures.Add($"cmd_{index + 1:000}: exited {result.ExitCode}");
            }

            var stdoutHash = AgentTrialLocalJson.HashString(result.Stdout);
            var stderrHash = AgentTrialLocalJson.HashString(result.Stderr);
            var resultHash = AgentTrialLocalJson.HashString($"{command}\n{result.ExitCode}\n{result.Stdout}\n{result.Stderr}");
            logHashes.Add(stdoutHash);
            logHashes.Add(stderrHash);
            resultHashes.Add(resultHash);

            executedCommands.Add(new JsonObject
            {
                ["command_id"] = $"cmd_{index + 1:000}",
                ["command"] = command,
                ["required"] = true,
                ["started_at"] = result.StartedAt.ToUniversalTime().ToString("O"),
                ["completed_at"] = result.CompletedAt.ToUniversalTime().ToString("O"),
                ["exit_code"] = result.ExitCode,
                ["status"] = status,
                ["duration_ms"] = result.DurationMs,
                ["stdout_log_sha256"] = stdoutHash,
                ["stderr_log_sha256"] = stderrHash,
                ["result_artifact_sha256"] = resultHash,
                ["summary"] = new JsonObject
                {
                    ["passed"] = status == "passed" ? 1 : 0,
                    ["failed"] = status == "failed" ? 1 : 0,
                    ["skipped"] = 0,
                    ["errors"] = status == "timed_out" ? 1 : 0,
                },
            });
        }

        return new JsonObject
        {
            ["schema_version"] = "test-evidence.v0",
            ["task_id"] = contract.TaskId,
            ["task_version"] = contract.TaskVersion,
            ["challenge_id"] = contract.ChallengeId,
            ["required_commands"] = new JsonArray(contract.RequiredCommands.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["executed_commands"] = executedCommands,
            ["missing_required_commands"] = new JsonArray(contract.RequiredCommands
                .Skip(commandResults.Count)
                .Select(value => JsonValue.Create(value))
                .ToArray<JsonNode?>()),
            ["summary"] = new JsonObject
            {
                ["required_command_count"] = contract.RequiredCommands.Count,
                ["executed_required_command_count"] = commandResults.Count,
                ["passed"] = passed,
                ["failed"] = failed,
                ["skipped"] = skipped,
                ["errors"] = errors,
            },
            ["failure_summary"] = failures,
            ["log_hashes"] = new JsonArray(logHashes.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["result_artifact_hashes"] = new JsonArray(resultHashes.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["agent_claimed_tests_passed"] = agentReport.ClaimedTestsPassed,
            ["privacy"] = new JsonObject
            {
                ["summary_only"] = true,
                ["full_logs_included"] = false,
                ["source_included"] = false,
                ["raw_diff_included"] = false,
            },
        };
    }

    public static JsonObject BuildTrialResult(
        string workspaceRoot,
        AgentTrialTaskContract contract,
        string collectionStatus,
        IReadOnlyList<string> missingRequiredArtifacts,
        IReadOnlyList<string> collectionFailureReasons,
        string expectedTaskContractSha256,
        string actualTaskContractSha256,
        AgentTrialInstructionPack instructionPack,
        string agentReportPath,
        string diffScopeSummaryPath,
        string testEvidencePath,
        DateTimeOffset createdAt)
    {
        var artifactHashes = new JsonObject
        {
            ["task_contract_sha256"] = actualTaskContractSha256,
            ["expected_task_contract_sha256"] = expectedTaskContractSha256,
            ["actual_task_contract_sha256"] = actualTaskContractSha256,
            ["instruction_pack_sha256"] = instructionPack.ActualInstructionPackSha256,
            ["expected_instruction_pack_sha256"] = instructionPack.ExpectedInstructionPackSha256,
            ["actual_instruction_pack_sha256"] = instructionPack.ActualInstructionPackSha256,
            ["agent_report_sha256"] = AgentTrialLocalJson.HashFileOrMissing(agentReportPath),
            ["diff_scope_summary_sha256"] = AgentTrialLocalJson.HashFile(diffScopeSummaryPath),
            ["test_evidence_sha256"] = AgentTrialLocalJson.HashFile(testEvidencePath),
            ["trial_result_sha256"] = AgentTrialLocalJson.MissingArtifactHash,
        };
        var result = new JsonObject
        {
            ["schema_version"] = "carves-agent-trial-result.v0",
            ["result_id"] = "local-mvp-" + AgentTrialLocalJson.HashString($"{contract.ChallengeId}\n{createdAt:O}")[7..19],
            ["result_mode"] = "local_only",
            ["visibility"] = "private",
            ["suite_id"] = contract.SuiteId,
            ["pack_id"] = contract.PackId,
            ["pack_version"] = contract.PackVersion,
            ["task_id"] = contract.TaskId,
            ["task_version"] = contract.TaskVersion,
            ["prompt_id"] = contract.PromptId,
            ["prompt_version"] = contract.PromptVersion,
            ["instruction_pack"] = BuildInstructionPackJson(instructionPack),
            ["challenge_id"] = contract.ChallengeId,
            ["challenge_source"] = contract.ChallengeSource,
            ["scoring_profile_id"] = AgentTrialVersionContract.ScoringProfileId,
            ["scoring_profile_version"] = AgentTrialVersionContract.ScoringProfileVersion,
            ["local_score"] = AgentTrialLocalScoreMapper.BuildLocalScore(
                AgentTrialSafetyPostureProjector.ProjectFromWorkspace(
                    new AgentTrialSafetyPostureOptions(workspaceRoot, MatrixVerified: true)),
                collectionStatus,
                missingRequiredArtifacts,
                collectionFailureReasons),
            ["local_collection_status"] = collectionStatus,
            ["authority_mode"] = "local_only",
            ["verification_status"] = "not_matrix_verified_by_collector",
            ["official_leaderboard_eligible"] = false,
            ["version_comparability"] = new JsonObject
            {
                ["suite_id"] = contract.SuiteId,
                ["pack_id"] = contract.PackId,
                ["pack_version"] = contract.PackVersion,
                ["task_id"] = contract.TaskId,
                ["task_version"] = contract.TaskVersion,
                ["instruction_pack_id"] = instructionPack.InstructionPackId,
                ["instruction_pack_version"] = instructionPack.InstructionPackVersion,
                ["prompt_id"] = contract.PromptId,
                ["prompt_version"] = contract.PromptVersion,
                ["scoring_profile_id"] = AgentTrialVersionContract.ScoringProfileId,
                ["scoring_profile_version"] = AgentTrialVersionContract.ScoringProfileVersion,
                ["collector_version"] = AgentTrialVersionContract.CollectorVersion,
                ["matrix_verifier_version"] = AgentTrialVersionContract.MatrixVerifierVersionUnavailable,
                ["comparison_scope"] = AgentTrialVersionContract.ComparisonScope,
                ["cross_version_comparison"] = AgentTrialVersionContract.CrossVersionComparison,
            },
            ["ineligibility_reasons"] = new JsonArray(JsonValue.Create("local_dry_run_challenge")),
            ["leaderboard_eligibility"] = new JsonObject
            {
                ["status"] = "ineligible_local_only",
                ["authority_mode"] = "local_only",
                ["verification_status"] = "not_matrix_verified_by_collector",
                ["official_leaderboard_eligible"] = false,
                ["reason_codes"] = new JsonArray(JsonValue.Create("local_dry_run_challenge")),
            },
            ["artifact_hashes"] = artifactHashes,
            ["missing_required_artifacts"] = new JsonArray(missingRequiredArtifacts.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["privacy"] = new JsonObject
            {
                ["summary_only"] = true,
                ["source_included"] = false,
                ["raw_diff_included"] = false,
                ["prompt_response_included"] = false,
                ["model_response_included"] = false,
                ["full_logs_included"] = false,
                ["secrets_included"] = false,
                ["credentials_included"] = false,
                ["customer_payload_included"] = false,
                ["certification_claim"] = false,
            },
            ["created_at"] = createdAt.ToUniversalTime().ToString("O"),
        };

        artifactHashes["trial_result_sha256"] = AgentTrialLocalJson.HashString(result.ToJsonString(new JsonSerializerOptions()));
        return result;
    }

    private static JsonObject BuildInstructionPackJson(AgentTrialInstructionPack instructionPack)
    {
        return new JsonObject
        {
            ["instruction_pack_id"] = instructionPack.InstructionPackId,
            ["instruction_pack_version"] = instructionPack.InstructionPackVersion,
            ["expected_instruction_pack_sha256"] = instructionPack.ExpectedInstructionPackSha256,
            ["actual_instruction_pack_sha256"] = instructionPack.ActualInstructionPackSha256,
            ["prompt_id"] = instructionPack.PromptId,
            ["prompt_version"] = instructionPack.PromptVersion,
            ["prompt_path"] = instructionPack.PromptPath,
            ["prompt_sha256"] = instructionPack.PromptSha256,
            ["canonical_instruction_files"] = new JsonArray(instructionPack.CanonicalInstructionFiles
                .Select(file => new JsonObject
                {
                    ["path"] = file.Path,
                    ["role"] = file.Role,
                    ["sha256"] = file.Sha256,
                })
                .ToArray<JsonNode?>()),
            ["user_modified_instruction_pack_comparable"] = false,
            ["comparison_note"] = "official instruction and prompt pack versions must match for direct comparison",
            ["pin_verified"] = instructionPack.Verified,
        };
    }
}
