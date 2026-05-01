using Carves.Matrix.Core;
using System.Text.Json;

namespace Carves.Matrix.Tests;

public sealed class MatrixProofSummarySchemaDriftGuardTests
{
    [Fact]
    public void ProofSummarySchema_TopLevelFieldsTrackRuntimeContract()
    {
        var schema = LoadProofSummarySchema();

        AssertFieldSetEquals(RuntimeContract.AllTopLevelFields, SchemaProperties(schema));
    }

    [Fact]
    public void ProofSummarySchema_ArtifactManifestFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.ArtifactManifest.ContractName);

        AssertFieldSetEquals(RuntimeContract.ArtifactManifest.FieldNames, SchemaProperties(definition));
    }

    [Fact]
    public void ProofSummarySchema_ProofCapabilityFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.ProofCapabilities.ContractName);

        AssertFieldSetEquals(RuntimeContract.ProofCapabilities.FieldNames, SchemaProperties(definition));
    }

    [Fact]
    public void ProofSummarySchema_ProofCapabilityCoverageFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.ProofCapabilityCoverage.ContractName);

        AssertFieldSetEquals(RuntimeContract.ProofCapabilityCoverage.FieldNames, SchemaProperties(definition));
    }

    [Fact]
    public void ProofSummarySchema_ProofCapabilityRequirementFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.ProofCapabilityRequirements.ContractName);

        AssertFieldSetEquals(RuntimeContract.ProofCapabilityRequirements.FieldNames, SchemaProperties(definition));
    }

    [Fact]
    public void ProofSummarySchema_PrivacyFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.Privacy.ContractName);

        AssertFieldSetEquals(RuntimeContract.Privacy.FieldNames, SchemaProperties(definition));
    }

    [Fact]
    public void ProofSummarySchema_PublicClaimFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.PublicClaims.ContractName);

        AssertFieldSetEquals(RuntimeContract.PublicClaims.FieldNames, SchemaProperties(definition));
    }

    [Fact]
    public void ProofSummarySchema_TrustChainFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.TrustChainHardening.ContractName);

        AssertFieldSetEquals(RuntimeContract.TrustChainHardening.FieldNames, SchemaProperties(definition));
    }

    [Fact]
    public void ProofSummarySchema_TrustChainGateFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.TrustChainGate.ContractName);

        AssertFieldSetEquals(RuntimeContract.TrustChainGate.FieldNames, SchemaPropertyNames(definition));
    }

    [Fact]
    public void ProofSummarySchema_NativeFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.Native.ContractName);

        AssertFieldSetEquals(RuntimeContract.Native.FieldNames, SchemaPropertyNames(definition));
    }

    [Fact]
    public void ProofSummarySchema_FullReleaseProjectFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.Project.ContractName);

        AssertFieldSetEquals(RuntimeContract.Project.FieldNames, SchemaPropertyNames(definition));
    }

    [Fact]
    public void ProofSummarySchema_FullReleasePackagedFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.Packaged.ContractName);

        AssertFieldSetEquals(RuntimeContract.Packaged.FieldNames, SchemaPropertyNames(definition));
    }

    [Fact]
    public void ProofSummarySchema_FullReleaseTrustChainEvidenceFieldsTrackRuntimeContract()
    {
        var definition = SchemaDefinition(RuntimeContract.ProjectTrustChainHardening.ContractName);

        AssertFieldSetEquals(RuntimeContract.ProjectTrustChainHardening.FieldNames, SchemaPropertyNames(definition));
    }

    private static MatrixProofSummaryPublicContractModel RuntimeContract => MatrixProofSummaryPublicContract.Model;

    private static JsonElement SchemaDefinition(string name)
    {
        return LoadProofSummarySchema().GetProperty("$defs").GetProperty(name);
    }

    private static JsonElement LoadProofSummarySchema()
    {
        var schemaPath = Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "docs",
            "matrix",
            "schemas",
            "matrix-proof-summary.v0.schema.json");
        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        return document.RootElement.Clone();
    }

    private static string[] SchemaProperties(JsonElement schema)
    {
        return schema.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
    }

    private static string[] SchemaPropertyNames(JsonElement schema)
    {
        return schema.GetProperty("propertyNames")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
    }

    private static void AssertFieldSetEquals(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var expectedFields = expected.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var actualFields = actual.OrderBy(value => value, StringComparer.Ordinal).ToArray();

        Assert.Equal(expectedFields, actualFields);
    }
}
