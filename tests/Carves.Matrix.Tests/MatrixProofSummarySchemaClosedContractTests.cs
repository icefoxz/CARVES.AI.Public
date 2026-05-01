using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

public sealed class MatrixProofSummarySchemaClosedContractTests
{
    [Fact]
    public void ProofSummarySchema_RejectsUnknownTopLevelField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["local_workspace"] = "/home/user/private/matrix");

        AssertContainsSchemaError(errors, "$: unknown property local_workspace");
    }

    [Fact]
    public void ProofSummarySchema_RejectsUnknownPrivacyField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["privacy"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        AssertContainsSchemaError(errors, "$.privacy: unknown property debug_path");
    }

    [Fact]
    public void ProofSummarySchema_RejectsUnknownPublicClaimField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["public_claims"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        AssertContainsSchemaError(errors, "$.public_claims: unknown property debug_path");
    }

    [Fact]
    public void ProofSummarySchema_RejectsUnknownCapabilityField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["proof_capabilities"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        AssertContainsSchemaError(errors, "$.proof_capabilities: unknown property debug_path");
    }

    [Fact]
    public void ProofSummarySchema_RejectsUnknownCapabilityCoverageField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["proof_capabilities"]!["coverage"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        AssertContainsSchemaError(errors, "$.proof_capabilities.coverage: unknown property debug_path");
    }

    [Fact]
    public void ProofSummarySchema_RejectsUnknownNativeField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["native"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        AssertContainsSchemaError(errors, "$.native: property name debug_path is not allowed");
    }

    [Fact]
    public void ProofSummarySchema_RejectsUnknownTrustChainGateField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["trust_chain_hardening"]!["gates"]!.AsArray()[0]!.AsObject()["debug_path"] =
                "/home/user/private/matrix");

        AssertContainsSchemaError(errors, "$.trust_chain_hardening.gates[0]: property name debug_path is not allowed");
    }

    [Fact]
    public void ProofSummarySchema_RejectsUnknownFullReleaseProjectField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["project"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        AssertContainsSchemaError(errors, "$.project: property name debug_path is not allowed");
    }

    [Fact]
    public void ProofSummarySchema_RejectsUnknownFullReleasePackagedField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["packaged"]!.AsObject()["debug_path"] = "/home/user/private/matrix");

        AssertContainsSchemaError(errors, "$.packaged: property name debug_path is not allowed");
    }

    [Fact]
    public void ProofSummarySchema_RejectsUnknownFullReleaseTrustChainEvidenceField()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["project"]!["trust_chain_hardening"]!.AsObject()["debug_path"] =
                "/home/user/private/matrix");

        AssertContainsSchemaError(
            errors,
            "$.project.trust_chain_hardening: property name debug_path is not allowed");
    }

    private static void AssertContainsSchemaError(string[] errors, string expectedFragment)
    {
        Assert.Contains(errors, error => error.Contains(expectedFragment, StringComparison.Ordinal));
    }
}
