using System.Text.Json.Nodes;
using static Carves.Matrix.Tests.MatrixVerifyJsonAssertions;

namespace Carves.Matrix.Tests;

public sealed class MatrixProofSummaryIntegrityTests
{
    [Fact]
    public void VerifyCommand_RejectsMutatedSummaryPublicClaim()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["public_claims"]!.AsObject()["certification"] = true);

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "privacy_forbidden_public_claim_true:certification", "privacy_violation");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedSummaryPrivacyPosture()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["privacy"]!.AsObject()["raw_diff_upload_required"] = true);

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "privacy_forbidden_summary_flag_true:raw_diff_upload_required", "privacy_violation");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedSummaryProofMode()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["proof_mode"] = "forged_mode");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_proof_mode_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsNativeSummaryForgedCapabilityLane()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["proof_capabilities"]!.AsObject()["proof_lane"] = "full_release");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_proof_capabilities_proof_lane_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsNativeSummaryForgedFullReleaseCoverage()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["proof_capabilities"]!["coverage"]!.AsObject()["full_release"] = true);

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_proof_capabilities_coverage_full_release_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsNativeSummaryForgedPowerShellRequirement()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["proof_capabilities"]!["requirements"]!.AsObject()["powershell"] = true);

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_proof_capabilities_requirements_powershell_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedNativeSummaryReadback()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["native"]!.AsObject()["lite_score"] = 99);

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_native_lite_score_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsUnknownSummaryTopLevelField()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["local_workspace"] = "/home/user/private/matrix");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_unknown_field:local_workspace", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsUnknownNativeNestedField()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["native"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_unknown_field:native.debug_path", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsUnknownSummaryPrivacyField()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["privacy"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_unknown_field:privacy.debug_path", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsUnknownTrustChainGateField()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root =>
            root["trust_chain_hardening"]!["gates"]!.AsArray()[0]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_unknown_field:trust_chain_hardening.gates[].debug_path", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedNativeTrustChainGatesSatisfied()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root => root["trust_chain_hardening"]!.AsObject()["gates_satisfied"] = false);

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_trust_chain_hardening_gates_satisfied_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedNativeTrustChainGateReason()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root =>
            root["trust_chain_hardening"]!["gates"]!.AsArray()[0]!.AsObject()["reason"] = "forged reason");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_trust_chain_hardening_gates_manifest_integrity_reason_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedNativeTrustChainIssueCodes()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.MutateProofSummary(root =>
            root["trust_chain_hardening"]!["gates"]!.AsArray()[0]!.AsObject()["issue_codes"] = new JsonArray("forged_issue"));

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_trust_chain_hardening_gates_manifest_integrity_issue_codes_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsNativeSummaryHashDriftDuringSemanticRead()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.ReplaceMatrixSummaryTextAndRefreshProofSummary("\"scoring_owner\": \"shield\"", "\"scoring_owner\": \"forged\"");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_source_manifest_hash_mismatch:matrix_summary", "hash_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsNativeSummaryDuplicateManifestEntry()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.DuplicateManifestArtifact("matrix_summary");
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_source_manifest_entry_duplicate:matrix_summary", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsNativeSummaryReparsePoint()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var bundle = MatrixBundleFixture.Create();
        var outsideRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-summary-symlink-target-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(outsideRoot);
            var sourcePath = Path.Combine(bundle.ArtifactRoot, "project", "matrix-summary.json");
            var outsidePath = Path.Combine(outsideRoot, "matrix-summary.json");
            File.WriteAllText(outsidePath, File.ReadAllText(sourcePath));
            File.Delete(sourcePath);
            File.CreateSymbolicLink(sourcePath, outsidePath);
            bundle.WriteProofSummary();

            var root = RunVerifyJson(bundle.ArtifactRoot);

            AssertContainsIssue(root, "matrix_proof_summary", "summary_source_reparse_point_rejected:matrix_summary", "schema_mismatch");
        }
        finally
        {
            if (Directory.Exists(outsideRoot))
            {
                Directory.Delete(outsideRoot, recursive: true);
            }
        }
    }

}
