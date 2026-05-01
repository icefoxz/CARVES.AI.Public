using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;
using static Carves.Matrix.Tests.MatrixVerifyJsonAssertions;

namespace Carves.Matrix.Tests;

public sealed class MatrixFullReleaseProofSummaryIntegrityTests
{
    [Fact]
    public void VerifyCommand_AcceptsFullReleaseSummaryBoundToSourceArtifacts()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();

        var result = RunMatrixCli("verify", bundle.ArtifactRoot, "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.True(document.RootElement.GetProperty("summary").GetProperty("consistent").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedFullReleaseTopLevelArtifactRoot()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.MutateProofSummary(root => root["artifact_root"] = "/home/user/private/matrix");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_artifact_root_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedFullReleaseProjectReadback()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.MutateProofSummary(root => root["project"]!.AsObject()["lite_score"] = 99);

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_project_lite_score_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedFullReleaseCapabilityBackend()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.MutateProofSummary(root => root["proof_capabilities"]!.AsObject()["execution_backend"] = "dotnet_runner_chain");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_proof_capabilities_execution_backend_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedFullReleasePackagedCoverage()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.MutateProofSummary(root => root["proof_capabilities"]!["coverage"]!.AsObject()["packaged_install"] = false);

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_proof_capabilities_coverage_packaged_install_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsUnknownFullReleaseProjectNestedField()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.MutateProofSummary(root => root["project"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_unknown_field:project.debug_path", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsFullReleasePackagedSourceWithoutManifestCoverage()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.RemoveManifestArtifactAndRefreshProofSummary("packaged_matrix_summary");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_source_manifest_entry_missing:packaged_matrix_summary", "missing_artifact");
    }

    [Fact]
    public void VerifyCommand_RejectsFullReleasePackagedSourceHashDriftDuringSemanticRead()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.ReplacePackagedSummaryTextAndRefreshProofSummary("0.2.0-alpha.1", "9.9.9-alpha.9");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_source_manifest_hash_mismatch:packaged_matrix_summary", "hash_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsFullReleasePackagedSourceSizeDriftDuringSemanticRead()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.AppendPackagedSummaryAndRefreshProofSummary("\n");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_source_manifest_size_mismatch:packaged_matrix_summary", "hash_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedFullReleasePackagedReadback()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.MutateProofSummary(root => root["packaged"]!.AsObject()["guard_version"] = "9.9.9-forged");

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_packaged_guard_version_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsMutatedFullReleaseTrustChainHardening()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.MutateProofSummary(root =>
        {
            var trustChain = root["trust_chain_hardening"]!.AsObject();
            trustChain["gates_satisfied"] = false;
            trustChain["gates"]!.AsArray()[0]!.AsObject()["satisfied"] = false;
        });

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_trust_chain_hardening_gates_satisfied_mismatch", "schema_mismatch");
        AssertContainsIssue(root, "matrix_proof_summary", "summary_trust_chain_hardening_gates_manifest_integrity_satisfied_mismatch", "schema_mismatch");
    }

}
