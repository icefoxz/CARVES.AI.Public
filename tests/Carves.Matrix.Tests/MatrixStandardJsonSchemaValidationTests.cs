using Carves.Matrix.Core;

namespace Carves.Matrix.Tests;

public sealed class MatrixStandardJsonSchemaValidationTests
{
    [Fact]
    public void StandardJsonSchemaValidator_LoadsPublicMatrixSchemas()
    {
        Assert.NotNull(MatrixStandardJsonSchemaTestSupport.ProofSummarySchema);
        Assert.NotNull(MatrixStandardJsonSchemaTestSupport.ArtifactManifestSchema);
    }

    [Fact]
    public void StandardJsonSchemaValidator_AcceptsNativeMinimalFixtureOutputs()
    {
        using var bundle = MatrixBundleFixture.Create();

        MatrixStandardJsonSchemaTestSupport.AssertProofSummaryValid(
            Path.Combine(bundle.ArtifactRoot, "matrix-proof-summary.json"));
        MatrixStandardJsonSchemaTestSupport.AssertArtifactManifestValid(
            Path.Combine(bundle.ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName));
    }

    [Fact]
    public void StandardJsonSchemaValidator_AcceptsFullReleaseFixtureOutputs()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();

        MatrixStandardJsonSchemaTestSupport.AssertProofSummaryValid(
            Path.Combine(bundle.ArtifactRoot, "matrix-proof-summary.json"));
        MatrixStandardJsonSchemaTestSupport.AssertArtifactManifestValid(
            Path.Combine(bundle.ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName));
    }
}
