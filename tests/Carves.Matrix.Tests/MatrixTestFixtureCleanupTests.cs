namespace Carves.Matrix.Tests;

public sealed class MatrixTestFixtureCleanupTests
{
    [Fact]
    public void MatrixBundleFixture_IsSplitIntoFocusedHelpers()
    {
        var main = ReadMatrixTestSource("MatrixBundleFixture.cs");
        var artifacts = ReadMatrixTestSource("MatrixBundleFixture.Artifacts.cs");
        var manifest = ReadMatrixTestSource("MatrixBundleFixture.Manifest.cs");
        var proofSummary = ReadMatrixTestSource("MatrixBundleFixture.ProofSummary.cs");

        Assert.Contains("Create(", main, StringComparison.Ordinal);
        Assert.Contains("WriteArtifact", artifacts, StringComparison.Ordinal);
        Assert.Contains("FindManifestArtifact", manifest, StringComparison.Ordinal);
        Assert.Contains("WriteProofSummary", proofSummary, StringComparison.Ordinal);

        Assert.DoesNotContain("FindManifestArtifact", main, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteProofSummary", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidTrustChainHardening", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("RemoveManifestArtifact", proofSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixVerifyHelpers_KeepCliCaptureAndJsonAssertionsSeparate()
    {
        var runner = ReadMatrixTestSource("MatrixCliTestRunner.cs");
        var assertions = ReadMatrixTestSource("MatrixVerifyJsonAssertions.cs");

        Assert.Contains("Console.SetOut", runner, StringComparison.Ordinal);
        Assert.Contains("MatrixCliRunner.Run", runner, StringComparison.Ordinal);
        Assert.Contains("AssertContainsIssue", assertions, StringComparison.Ordinal);
        Assert.Contains("RunFailedVerifyJson", assertions, StringComparison.Ordinal);

        Assert.DoesNotContain("AssertContainsIssue", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.SetOut", assertions, StringComparison.Ordinal);
    }

    private static string ReadMatrixTestSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "tests",
            "Carves.Matrix.Tests",
            fileName));
    }
}
