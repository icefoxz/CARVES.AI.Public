using System.Text.Json;

namespace Carves.Matrix.Tests;

public sealed class MatrixProofSummarySchemaRuntimeOutputTests
{
    [Fact]
    public void ProofSummarySchema_AcceptsNativeMinimalRuntimeOutput()
    {
        using var bundle = MatrixBundleFixture.Create();

        var errors = MatrixProofSummarySchemaTestSupport.ValidateProofSummaryFile(
            Path.Combine(bundle.ArtifactRoot, "matrix-proof-summary.json"));

        Assert.Empty(errors);
    }

    [Fact]
    public void ProofSummarySchema_AcceptsFullReleaseRuntimeOutput()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();

        var errors = MatrixProofSummarySchemaTestSupport.ValidateProofSummaryFile(
            Path.Combine(bundle.ArtifactRoot, "matrix-proof-summary.json"));

        Assert.Empty(errors);
    }

    [Fact]
    public void ProofSummarySchema_DefinesPublicShapeRatherThanFullVerifierSemantics()
    {
        var schemaPath = Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "docs",
            "matrix",
            "schemas",
            "matrix-proof-summary.v0.schema.json");
        using var schemaDocument = JsonDocument.Parse(File.ReadAllText(schemaPath));

        Assert.Contains(
            "Closed public readback contract",
            schemaDocument.RootElement.GetProperty("description").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Undefined, GetPropertyOrDefault(
            schemaDocument.RootElement.GetProperty("properties").GetProperty("smoke"),
            "const").ValueKind);
    }

    private static JsonElement GetPropertyOrDefault(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property)
            ? property
            : default;
    }
}
