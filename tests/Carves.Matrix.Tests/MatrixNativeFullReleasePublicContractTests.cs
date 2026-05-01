using System.Text.Json;
using System.Text.Json.Nodes;
using static Carves.Matrix.Tests.MatrixCliTestRunner;
using static Carves.Matrix.Tests.MatrixVerifyJsonAssertions;

namespace Carves.Matrix.Tests;

public sealed class MatrixNativeFullReleasePublicContractTests
{
    [Fact]
    public void ProofCli_NativeFullReleaseLaneRunsNativeProducersAndVerifies()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-full-release-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "proof",
                "--lane",
                "native-full-release",
                "--runtime-root",
                repoRoot,
                "--artifact-root",
                artifactRoot,
                "--configuration",
                "Debug",
                "--json");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError));

            using var summaryDocument = JsonDocument.Parse(result.StandardOutput);
            var summary = summaryDocument.RootElement;
            var capabilities = summary.GetProperty("proof_capabilities");
            Assert.Equal("full_release", summary.GetProperty("proof_mode").GetString());
            Assert.Equal("native_full_release", capabilities.GetProperty("proof_lane").GetString());
            Assert.Equal("dotnet_full_release_runner_chain", capabilities.GetProperty("execution_backend").GetString());
            Assert.True(capabilities.GetProperty("coverage").GetProperty("project_mode").GetBoolean());
            Assert.True(capabilities.GetProperty("coverage").GetProperty("packaged_install").GetBoolean());
            Assert.True(capabilities.GetProperty("coverage").GetProperty("full_release").GetBoolean());
            Assert.False(capabilities.GetProperty("requirements").GetProperty("powershell").GetBoolean());
            Assert.True(capabilities.GetProperty("requirements").GetProperty("source_checkout").GetBoolean());
            using var projectSummaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifactRoot, "project", "matrix-summary.json")));
            using var packagedSummaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifactRoot, "packaged", "matrix-packaged-summary.json")));
            Assert.Equal("native_full_release_project", projectSummaryDocument.RootElement.GetProperty("producer").GetString());
            Assert.Equal("native_full_release_packaged", packagedSummaryDocument.RootElement.GetProperty("producer").GetString());
            Assert.Empty(MatrixProofSummarySchemaTestSupport.ValidateProofSummaryFile(Path.Combine(artifactRoot, "matrix-proof-summary.json")));

            var verify = RunMatrixCli("verify", artifactRoot, "--json");
            Assert.Equal(0, verify.ExitCode);
            using var verifyDocument = JsonDocument.Parse(verify.StandardOutput);
            Assert.Equal("verified", verifyDocument.RootElement.GetProperty("status").GetString());
            Assert.True(verifyDocument.RootElement.GetProperty("summary").GetProperty("consistent").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ProofCli_UsageDocumentsOptInNativeFullReleaseLane()
    {
        var result = RunMatrixCli("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("proof --lane native-full-release", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("explicit native full-release lane", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("without making it the default", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("reserved, not implemented", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ProofCli_CompatibilityShorthandLaneSelectionRemainsUnchanged()
    {
        var jsonShorthand = RunMatrixCli(
            "proof",
            "--json",
            "--runtime-root",
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot());
        var bareShorthand = RunMatrixCli(
            "proof",
            "--work-root",
            Path.Combine(Path.GetTempPath(), "carves-matrix-bare-proof-" + Guid.NewGuid().ToString("N")));

        Assert.Equal(2, jsonShorthand.ExitCode);
        Assert.Contains("--runtime-root is not supported by proof --lane native-minimal", jsonShorthand.StandardError, StringComparison.Ordinal);
        Assert.Equal(2, bareShorthand.ExitCode);
        Assert.Contains("--work-root is only supported by proof --lane native-minimal.", bareShorthand.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyCommand_RejectsPowerShellFullReleaseRelabeledAsNativeFullRelease()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        bundle.MutateProofSummary(root =>
        {
            var capabilities = root["proof_capabilities"]!.AsObject();
            capabilities["proof_lane"] = "native_full_release";
            capabilities["execution_backend"] = "dotnet_full_release_runner_chain";
            capabilities["requirements"]!.AsObject()["powershell"] = false;
        });

        var root = RunFailedVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "matrix_proof_summary", "summary_project_producer_mismatch", "schema_mismatch");
        AssertContainsIssue(root, "matrix_proof_summary", "summary_packaged_producer_mismatch", "schema_mismatch");
    }

    [Fact]
    public void NativeFullReleaseContractDocument_FixesFuturePublicSummaryVocabulary()
    {
        var doc = ReadMatrixDoc("native-full-release-public-contract.md");

        Assert.Contains("`native-full-release`", doc, StringComparison.Ordinal);
        Assert.Contains("\"proof_mode\": \"full_release\"", doc, StringComparison.Ordinal);
        Assert.Contains("\"proof_lane\": \"native_full_release\"", doc, StringComparison.Ordinal);
        Assert.Contains("\"execution_backend\": \"dotnet_full_release_runner_chain\"", doc, StringComparison.Ordinal);
        Assert.Contains("\"powershell\": false", doc, StringComparison.Ordinal);
        Assert.Contains("cannot be relabeled as native by editing only the public summary", doc, StringComparison.Ordinal);
        Assert.Contains("`carves-matrix proof --lane full-release` remains the current PowerShell full-release lane", doc, StringComparison.Ordinal);
    }

    private static string ReadMatrixDoc(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "docs",
            "matrix",
            fileName));
    }
}
