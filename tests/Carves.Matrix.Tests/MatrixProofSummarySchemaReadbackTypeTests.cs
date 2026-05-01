using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

public sealed class MatrixProofSummarySchemaReadbackTypeTests
{
    [Fact]
    public void ProofSummarySchema_RejectsNativeScoreString()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["native"]!.AsObject()["lite_score"] = "50");

        AssertContainsSchemaError(errors, "$.native.lite_score: expected integer, found String");
    }

    [Fact]
    public void ProofSummarySchema_RejectsNativeEvidenceHashPatternMismatch()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["native"]!.AsObject()["consumed_shield_evidence_sha256"] = "not-a-sha");

        AssertContainsSchemaError(errors, "$.native.consumed_shield_evidence_sha256: pattern mismatch");
    }

    [Fact]
    public void ProofSummarySchema_RejectsTrustChainIssueCodesString()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["trust_chain_hardening"]!["gates"]!.AsArray()[0]!.AsObject()["issue_codes"] = "none");

        AssertContainsSchemaError(errors, "$.trust_chain_hardening.gates[0].issue_codes: expected array, found String");
    }

    [Fact]
    public void ProofSummarySchema_RejectsTrustChainIssueCodeNumber()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["trust_chain_hardening"]!["gates"]!.AsArray()[0]!.AsObject()["issue_codes"] = new JsonArray(404));

        AssertContainsSchemaError(errors, "$.trust_chain_hardening.gates[0].issue_codes[0]: expected string, found Number");
    }

    [Fact]
    public void ProofSummarySchema_RejectsFullReleaseBooleanString()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["project"]!.AsObject()["alters_shield_score"] = "false");

        AssertContainsSchemaError(errors, "$.project.alters_shield_score: expected boolean, found String");
    }

    [Fact]
    public void ProofSummarySchema_RejectsPackagedVersionNumber()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["packaged"]!.AsObject()["guard_version"] = 10);

        AssertContainsSchemaError(errors, "$.packaged.guard_version: expected string, found Number");
    }

    [Fact]
    public void ProofSummarySchema_RejectsFullReleaseTrustChainEvidenceBoolean()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["project"]!["trust_chain_hardening"]!.AsObject()["public_rating_claims_allowed"] = false);

        AssertContainsSchemaError(
            errors,
            "$.project.trust_chain_hardening.public_rating_claims_allowed: expected string, found False");
    }

    private static void AssertContainsSchemaError(string[] errors, string expectedFragment)
    {
        Assert.Contains(errors, error => error.Contains(expectedFragment, StringComparison.Ordinal));
    }
}
