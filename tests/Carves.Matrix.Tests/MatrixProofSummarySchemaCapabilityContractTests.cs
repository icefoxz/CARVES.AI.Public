using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

public sealed class MatrixProofSummarySchemaCapabilityContractTests
{
    [Fact]
    public void ProofSummarySchema_RejectsNativeCapabilityLaneMismatch()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["proof_capabilities"]!.AsObject()["proof_lane"] = "full_release");

        AssertContainsSchemaError(errors, "$.proof_capabilities.proof_lane: const mismatch");
    }

    [Fact]
    public void ProofSummarySchema_RejectsNativeFullReleaseCoverageClaim()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["proof_capabilities"]!["coverage"]!.AsObject()["full_release"] = true);

        AssertContainsSchemaError(errors, "$.proof_capabilities.coverage.full_release: const mismatch");
    }

    [Fact]
    public void ProofSummarySchema_RejectsNativePowerShellRequirementClaim()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedNativeSummary(root =>
            root["proof_capabilities"]!["requirements"]!.AsObject()["powershell"] = true);

        AssertContainsSchemaError(errors, "$.proof_capabilities.requirements.powershell: const mismatch");
    }

    [Fact]
    public void ProofSummarySchema_RejectsFullReleaseBackendMismatch()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["proof_capabilities"]!.AsObject()["execution_backend"] = "dotnet_runner_chain");

        AssertContainsSchemaError(errors, "$.proof_capabilities.execution_backend");
    }

    [Fact]
    public void ProofSummarySchema_RejectsFullReleasePackagedCoverageMismatch()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
            root["proof_capabilities"]!["coverage"]!.AsObject()["packaged_install"] = false);

        AssertContainsSchemaError(errors, "$.proof_capabilities.coverage.packaged_install: const mismatch");
    }

    [Fact]
    public void ProofSummarySchema_AcceptsNativeFullReleaseCapabilityContract()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
        {
            var capabilities = root["proof_capabilities"]!.AsObject();
            capabilities["proof_lane"] = "native_full_release";
            capabilities["execution_backend"] = "dotnet_full_release_runner_chain";
            capabilities["requirements"]!.AsObject()["powershell"] = false;
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void ProofSummarySchema_RejectsNativeFullReleasePowerShellRequirement()
    {
        var errors = MatrixProofSummarySchemaTestSupport.ValidateMutatedFullReleaseSummary(root =>
        {
            var capabilities = root["proof_capabilities"]!.AsObject();
            capabilities["proof_lane"] = "native_full_release";
            capabilities["execution_backend"] = "dotnet_full_release_runner_chain";
            capabilities["requirements"]!.AsObject()["powershell"] = true;
        });

        AssertContainsSchemaError(errors, "$.proof_capabilities.requirements.powershell: const mismatch");
    }

    private static void AssertContainsSchemaError(string[] errors, string expectedFragment)
    {
        Assert.Contains(errors, error => error.Contains(expectedFragment, StringComparison.Ordinal));
    }
}
