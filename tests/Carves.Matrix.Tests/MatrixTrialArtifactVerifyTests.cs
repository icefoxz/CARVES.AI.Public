using System.Text.Json;
using static Carves.Matrix.Tests.MatrixVerifyJsonAssertions;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialArtifactVerifyTests
{
    [Fact]
    public void VerifyCommand_NonTrialBundleRemainsValidAndReportsTrialModeNotPresent()
    {
        using var bundle = MatrixBundleFixture.Create();

        var root = RunVerifyJson(bundle.ArtifactRoot, expectedExitCode: 0);

        Assert.Equal("verified", root.GetProperty("status").GetString());
        Assert.Equal("ordinary", root.GetProperty("verification_mode").GetString());
        var trialArtifacts = root.GetProperty("trial_artifacts");
        Assert.Equal("not_present", trialArtifacts.GetProperty("mode").GetString());
        Assert.False(trialArtifacts.GetProperty("required").GetBoolean());
        Assert.Equal(0, trialArtifacts.GetProperty("loose_file_count").GetInt32());
        Assert.True(trialArtifacts.GetProperty("verified").GetBoolean());
        var gate = GetTrustChainGate(root, "trial_artifacts");
        Assert.True(gate.GetProperty("satisfied").GetBoolean());
        Assert.Contains("non-trial Matrix compatibility mode", gate.GetProperty("reason").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyCommand_ExplicitTrialModeRequiresManifestCoveredArtifacts()
    {
        using var bundle = MatrixBundleFixture.Create();

        var root = RunVerifyJson(bundle.ArtifactRoot, expectedExitCode: 1, "--trial");

        Assert.Equal("trial", root.GetProperty("verification_mode").GetString());
        AssertContainsReasonCode(root, "missing_artifact");
        AssertContainsIssue(root, "trial_task_contract", "trial_artifact_entry_missing", "missing_artifact");
        var trialArtifacts = root.GetProperty("trial_artifacts");
        Assert.Equal("required_missing", trialArtifacts.GetProperty("mode").GetString());
        Assert.True(trialArtifacts.GetProperty("required").GetBoolean());
        Assert.Equal(5, trialArtifacts.GetProperty("expected_count").GetInt32());
        Assert.Equal(0, trialArtifacts.GetProperty("present_count").GetInt32());
        Assert.Equal(5, trialArtifacts.GetProperty("missing_count").GetInt32());
        Assert.False(trialArtifacts.GetProperty("verified").GetBoolean());
        var gate = GetTrustChainGate(root, "trial_artifacts");
        Assert.False(gate.GetProperty("satisfied").GetBoolean());
        AssertContainsGateReasonCode(gate, "missing_artifact");
    }

    [Fact]
    public void VerifyCommand_CompleteTrialArtifactsPassWhenManifestCovered()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();

        var root = RunVerifyJson(bundle.ArtifactRoot, expectedExitCode: 0);

        var trialArtifacts = root.GetProperty("trial_artifacts");
        Assert.Equal("claimed", trialArtifacts.GetProperty("mode").GetString());
        Assert.False(trialArtifacts.GetProperty("required").GetBoolean());
        Assert.Equal(5, trialArtifacts.GetProperty("expected_count").GetInt32());
        Assert.Equal(5, trialArtifacts.GetProperty("present_count").GetInt32());
        Assert.Equal(0, trialArtifacts.GetProperty("missing_count").GetInt32());
        Assert.Equal(0, trialArtifacts.GetProperty("mismatch_count").GetInt32());
        Assert.Equal(5, trialArtifacts.GetProperty("loose_file_count").GetInt32());
        Assert.True(trialArtifacts.GetProperty("verified").GetBoolean());
        Assert.Empty(root.GetProperty("reason_codes").EnumerateArray());
    }

    [Fact]
    public void VerifyCommand_ExplicitTrialModePassesWhenManifestCoveredArtifactsVerify()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();

        var root = RunVerifyJson(bundle.ArtifactRoot, expectedExitCode: 0, "--trial");

        Assert.Equal("trial", root.GetProperty("verification_mode").GetString());
        var trialArtifacts = root.GetProperty("trial_artifacts");
        Assert.Equal("claimed", trialArtifacts.GetProperty("mode").GetString());
        Assert.True(trialArtifacts.GetProperty("required").GetBoolean());
        Assert.Equal(5, trialArtifacts.GetProperty("present_count").GetInt32());
        Assert.Equal(0, trialArtifacts.GetProperty("missing_count").GetInt32());
        Assert.Equal(0, trialArtifacts.GetProperty("mismatch_count").GetInt32());
        Assert.True(trialArtifacts.GetProperty("verified").GetBoolean());
        var gate = GetTrustChainGate(root, "trial_artifacts");
        Assert.True(gate.GetProperty("satisfied").GetBoolean());
        Assert.Equal("Agent Trial artifacts are manifest-covered and verified.", gate.GetProperty("reason").GetString());
    }

    [Fact]
    public void VerifyCommand_OrdinaryVerifyReportsLooseTrialFilesWithoutFailing()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddLooseTrialArtifactsWithoutManifestCoverage();

        var root = RunVerifyJson(bundle.ArtifactRoot, expectedExitCode: 0);

        Assert.Equal("verified", root.GetProperty("status").GetString());
        var trialArtifacts = root.GetProperty("trial_artifacts");
        Assert.Equal("loose_files_not_manifested", trialArtifacts.GetProperty("mode").GetString());
        Assert.False(trialArtifacts.GetProperty("required").GetBoolean());
        Assert.Equal(5, trialArtifacts.GetProperty("loose_file_count").GetInt32());
        Assert.False(trialArtifacts.GetProperty("verified").GetBoolean());
        var gate = GetTrustChainGate(root, "trial_artifacts");
        Assert.True(gate.GetProperty("satisfied").GetBoolean());
        Assert.Contains("ordinary verify treats them as unclaimed compatibility readback", gate.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Empty(root.GetProperty("reason_codes").EnumerateArray());
    }

    [Fact]
    public void VerifyCommand_ExplicitTrialModeRejectsLooseTrialFilesOutsideManifest()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddLooseTrialArtifactsWithoutManifestCoverage();

        var root = RunVerifyJson(bundle.ArtifactRoot, expectedExitCode: 1, "--require-trial");

        Assert.Equal("trial", root.GetProperty("verification_mode").GetString());
        AssertContainsReasonCode(root, "missing_artifact");
        var trialArtifacts = root.GetProperty("trial_artifacts");
        Assert.Equal("loose_files_not_manifested", trialArtifacts.GetProperty("mode").GetString());
        Assert.True(trialArtifacts.GetProperty("required").GetBoolean());
        Assert.Equal(5, trialArtifacts.GetProperty("loose_file_count").GetInt32());
        Assert.Equal(5, trialArtifacts.GetProperty("missing_count").GetInt32());
        Assert.False(trialArtifacts.GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialConsistencyRejectsChallengeIdMismatch()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/diff-scope-summary.json",
            "\"challenge_id\": \"local-mvp-task-001\"",
            "\"challenge_id\": \"local-mvp-task-mismatch\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_artifact_consistency_mismatch");
        AssertContainsIssue(
            root,
            "trial_diff_scope_summary",
            "trial_artifact_consistency_mismatch:trial_diff_scope_summary.challenge_id",
            "trial_artifact_consistency_mismatch");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialConsistencyRejectsTaskVersionMismatch()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/test-evidence.json",
            "\"task_version\": \"0.1.0-local\"",
            "\"task_version\": \"0.2.0-local\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_artifact_consistency_mismatch");
        AssertContainsIssue(
            root,
            "trial_test_evidence",
            "trial_artifact_consistency_mismatch:trial_test_evidence.task_version",
            "trial_artifact_consistency_mismatch");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialConsistencyRejectsPromptVersionMismatch()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/task-contract.json",
            "\"prompt_version\": \"0.1.0-local\"",
            "\"prompt_version\": \"0.2.0-local\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_artifact_consistency_mismatch");
        AssertContainsIssue(
            root,
            "trial_result",
            "trial_artifact_consistency_mismatch:trial_result.prompt_version",
            "trial_artifact_consistency_mismatch");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialSchemaRejectsExtraFields()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/agent-report.json",
            "\"privacy\": {",
            "\"debug_path\": \"/tmp/private\", \"privacy\": {");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_artifact_schema_invalid");
        AssertContainsIssue(root, "trial_agent_report", "trial_artifact_schema_invalid:$", "trial_artifact_schema_invalid");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialSchemaRejectsWrongChallengeSourceConst()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/task-contract.json",
            "\"challenge_source\": \"pack_local_dry_run\"",
            "\"challenge_source\": \"server_issued\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_artifact_schema_invalid");
        AssertContainsIssue(root, "trial_task_contract", "trial_artifact_schema_invalid:$.challenge_source", "trial_artifact_schema_invalid");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialSchemaRejectsWrongEnum()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/agent-report.json",
            "\"completion_status\": \"completed\"",
            "\"completion_status\": \"done\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_artifact_schema_invalid");
        AssertContainsIssue(root, "trial_agent_report", "trial_artifact_schema_invalid:$.completion_status", "trial_artifact_schema_invalid");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialSchemaRejectsWrongType()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/agent-report.json",
            "\"claimed_tests_passed\": true",
            "\"claimed_tests_passed\": \"yes\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_artifact_schema_invalid");
        AssertContainsIssue(root, "trial_agent_report", "trial_artifact_schema_invalid:$.claimed_tests_passed", "trial_artifact_schema_invalid");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialSchemaRejectsBadRelativePath()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/agent-report.json",
            "\"src/bounded-fixture.js\"",
            "\"../secrets.txt\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_artifact_schema_invalid");
        AssertContainsIssue(root, "trial_agent_report", "trial_artifact_schema_invalid:$.claimed_files_changed[0]", "trial_artifact_schema_invalid");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialModeRequiresAllFiveManifestEntries()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.RemoveManifestArtifact("trial_agent_report");
        bundle.WriteProofSummary();

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("trial_artifact_failed", root.GetProperty("verification_posture").GetString());
        AssertContainsReasonCode(root, "missing_artifact");
        AssertContainsIssue(root, "trial_agent_report", "trial_artifact_entry_missing", "missing_artifact");
        var trialArtifacts = root.GetProperty("trial_artifacts");
        Assert.Equal(1, trialArtifacts.GetProperty("missing_count").GetInt32());
        Assert.False(trialArtifacts.GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialArtifactHashMismatchFails()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        File.AppendAllText(Path.Combine(bundle.ArtifactRoot, "trial", "test-evidence.json"), "\n{\"mutated\":true}");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "hash_mismatch");
        AssertContainsIssue(root, "trial_test_evidence", "artifact_hash_mismatch", "hash_mismatch");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
        Assert.False(root.GetProperty("trust_chain_hardening").GetProperty("gates_satisfied").GetBoolean());
        var gate = GetTrustChainGate(root, "trial_artifacts");
        Assert.False(gate.GetProperty("satisfied").GetBoolean());
        AssertContainsGateIssueCode(gate, "artifact_hash_mismatch");
        AssertContainsGateReasonCode(gate, "hash_mismatch");
    }

    [Fact]
    public void VerifyCommand_TrialArtifactContentSchemaMismatchFails()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/agent-report.json",
            "\"schema_version\": \"agent-report.v0\"",
            "\"schema_version\": \"agent-report.v999\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "schema_mismatch");
        AssertContainsIssue(root, "trial_agent_report", "trial_artifact_schema_version_mismatch", "schema_mismatch");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialArtifactPrivacyViolationFails()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/carves-agent-trial-result.json",
            "\"source_included\": false",
            "\"source_included\": true");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "privacy_violation");
        AssertContainsIssue(root, "trial_result", "privacy_trial_forbidden_flag_true:source_included", "privacy_violation");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialResultCannotClaimLeaderboardEligibility()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/carves-agent-trial-result.json",
            "\"official_leaderboard_eligible\": false",
            "\"official_leaderboard_eligible\": true");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_artifact_schema_invalid");
        AssertContainsReasonCode(root, "trial_eligibility_violation");
        AssertContainsIssue(
            root,
            "trial_result",
            "trial_result_eligibility_field_mismatch:official_leaderboard_eligible",
            "trial_eligibility_violation");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialResultCannotPromoteAuthorityMode()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/carves-agent-trial-result.json",
            "\"authority_mode\": \"local_only\"",
            "\"authority_mode\": \"leaderboard_eligible\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_eligibility_violation");
        AssertContainsIssue(
            root,
            "trial_result",
            "trial_result_eligibility_field_mismatch:authority_mode",
            "trial_eligibility_violation");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_TrialResultCannotForgeVersionComparability()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.AddValidTrialArtifactsAndRewriteManifest();
        bundle.ReplaceTrialArtifactTextAndRewriteManifest(
            "trial/carves-agent-trial-result.json",
            "\"matrix_verifier_version\": \"unavailable_local_only\"",
            "\"matrix_verifier_version\": \"matrix-verify.v0\"");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsReasonCode(root, "trial_version_mismatch");
        AssertContainsIssue(
            root,
            "trial_result",
            "trial_result_version_field_mismatch:version_comparability.matrix_verifier_version",
            "trial_version_mismatch");
        Assert.False(root.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
    }

    private static JsonElement GetTrustChainGate(JsonElement root, string gateId)
    {
        foreach (var gate in root.GetProperty("trust_chain_hardening").GetProperty("gates").EnumerateArray())
        {
            if (gate.GetProperty("gate_id").GetString() == gateId)
            {
                return gate.Clone();
            }
        }

        throw new InvalidOperationException($"Missing trust-chain gate: {gateId}");
    }

    private static void AssertContainsGateIssueCode(JsonElement gate, string issueCode)
    {
        Assert.Contains(
            gate.GetProperty("issue_codes").EnumerateArray(),
            code => code.GetString() == issueCode);
    }

    private static void AssertContainsGateReasonCode(JsonElement gate, string reasonCode)
    {
        Assert.Contains(
            gate.GetProperty("reason_codes").EnumerateArray(),
            code => code.GetString() == reasonCode);
    }
}
