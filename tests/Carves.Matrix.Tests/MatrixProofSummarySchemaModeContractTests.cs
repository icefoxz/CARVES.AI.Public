using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

public sealed class MatrixProofSummarySchemaModeContractTests
{
    [Fact]
    public void ProofSummarySchema_RejectsUnknownProofMode()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["proof_mode"] = "forged_mode");

        AssertContainsSchemaError(errors, "$.proof_mode: enum mismatch");
    }

    [Fact]
    public void ProofSummarySchema_RequiresNativeContainerForNativeMinimalMode()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root.Remove("native"));

        AssertContainsSchemaError(errors, "$: missing required property native");
    }

    [Fact]
    public void ProofSummarySchema_RejectsProjectContainerForNativeMinimalMode()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["project"] = new JsonObject());

        AssertContainsSchemaError(errors, "$: matched forbidden schema");
    }

    [Fact]
    public void ProofSummarySchema_RejectsPackagedContainerForNativeMinimalMode()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["packaged"] = new JsonObject());

        AssertContainsSchemaError(errors, "$: matched forbidden schema");
    }

    [Fact]
    public void ProofSummarySchema_RequiresProjectContainerForFullReleaseMode()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root.Remove("project"));

        AssertContainsSchemaError(errors, "$: missing required property project");
    }

    [Fact]
    public void ProofSummarySchema_RequiresPackagedContainerForFullReleaseMode()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root.Remove("packaged"));

        AssertContainsSchemaError(errors, "$: missing required property packaged");
    }

    [Fact]
    public void ProofSummarySchema_RejectsNativeContainerForFullReleaseMode()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["native"] = new JsonObject());

        AssertContainsSchemaError(errors, "$: matched forbidden schema");
    }

    private static void AssertContainsSchemaError(string[] errors, string expectedFragment)
    {
        Assert.Contains(errors, error => error.Contains(expectedFragment, StringComparison.Ordinal));
    }
}
