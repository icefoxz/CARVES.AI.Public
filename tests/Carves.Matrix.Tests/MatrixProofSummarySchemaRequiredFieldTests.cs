namespace Carves.Matrix.Tests;

public sealed class MatrixProofSummarySchemaRequiredFieldTests
{
    [Fact]
    public void ProofSummarySchema_RequiresTopLevelSchemaVersion()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root.Remove("schema_version"));

        AssertContainsSchemaError(errors, "$: missing required property schema_version");
    }

    [Fact]
    public void ProofSummarySchema_RequiresArtifactManifestHash()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["artifact_manifest"]!.AsObject().Remove("sha256"));

        AssertContainsSchemaError(errors, "$.artifact_manifest: missing required property sha256");
    }

    [Fact]
    public void ProofSummarySchema_RequiresProofCapabilities()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root.Remove("proof_capabilities"));

        AssertContainsSchemaError(errors, "$: missing required property proof_capabilities");
    }

    [Fact]
    public void ProofSummarySchema_RequiresProofCapabilityCoverage()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["proof_capabilities"]!.AsObject().Remove("coverage"));

        AssertContainsSchemaError(errors, "$.proof_capabilities: missing required property coverage");
    }

    [Fact]
    public void ProofSummarySchema_RequiresPrivacySummaryOnlyFlag()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["privacy"]!.AsObject().Remove("summary_only"));

        AssertContainsSchemaError(errors, "$.privacy: missing required property summary_only");
    }

    [Fact]
    public void ProofSummarySchema_RequiresPublicClaimCertificationFlag()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["public_claims"]!.AsObject().Remove("certification"));

        AssertContainsSchemaError(errors, "$.public_claims: missing required property certification");
    }

    [Fact]
    public void ProofSummarySchema_RequiresTrustChainComputedBy()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["trust_chain_hardening"]!.AsObject().Remove("computed_by"));

        AssertContainsSchemaError(errors, "$.trust_chain_hardening: missing required property computed_by");
    }

    [Fact]
    public void ProofSummarySchema_RequiresTrustChainGateId()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["trust_chain_hardening"]!["gates"]!.AsArray()[0]!.AsObject().Remove("gate_id"));

        AssertContainsSchemaError(errors, "$.trust_chain_hardening.gates[0]: missing required property gate_id");
    }

    [Fact]
    public void ProofSummarySchema_RequiresNativeMatrixSummaryArtifact()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["native"]!.AsObject().Remove("matrix_summary_artifact"));

        AssertContainsSchemaError(errors, "$.native: missing required property matrix_summary_artifact");
    }

    [Fact]
    public void ProofSummarySchema_RequiresFullReleaseProjectGuardRunId()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["project"]!.AsObject().Remove("guard_run_id"));

        AssertContainsSchemaError(errors, "$.project: missing required property guard_run_id");
    }

    [Fact]
    public void ProofSummarySchema_RequiresFullReleasePackagedGuardVersion()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["packaged"]!.AsObject().Remove("guard_version"));

        AssertContainsSchemaError(errors, "$.packaged: missing required property guard_version");
    }

    [Fact]
    public void ProofSummarySchema_RequiresFullReleaseTrustChainReleaseCheckpoint()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["project"]!["trust_chain_hardening"]!.AsObject().Remove("release_checkpoint"));

        AssertContainsSchemaError(
            errors,
            "$.project.trust_chain_hardening: missing required property release_checkpoint");
    }

    private static void AssertContainsSchemaError(string[] errors, string expectedFragment)
    {
        Assert.Contains(errors, error => error.Contains(expectedFragment, StringComparison.Ordinal));
    }
}
