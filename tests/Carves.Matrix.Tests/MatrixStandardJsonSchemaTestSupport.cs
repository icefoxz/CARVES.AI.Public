using Json.Schema;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Carves.Matrix.Tests;

internal static class MatrixStandardJsonSchemaTestSupport
{
    private static readonly Lazy<JsonSchema> ProofSummarySchemaLazy = new(
        () => LoadSchema("matrix-proof-summary.v0.schema.json"));

    private static readonly Lazy<JsonSchema> ArtifactManifestSchemaLazy = new(
        () => LoadSchema("matrix-artifact-manifest.v0.schema.json"));

    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> PublicSchemas = new();

    public static JsonSchema ProofSummarySchema => ProofSummarySchemaLazy.Value;

    public static JsonSchema ArtifactManifestSchema => ArtifactManifestSchemaLazy.Value;

    public static JsonSchema LoadPublicSchema(string fileName)
    {
        return PublicSchemas.GetOrAdd(
            fileName,
            static key => new Lazy<JsonSchema>(() => LoadSchema(key))).Value;
    }

    public static void AssertProofSummaryValid(string instancePath)
    {
        AssertValid(ProofSummarySchema, instancePath);
    }

    public static void AssertArtifactManifestValid(string instancePath)
    {
        AssertValid(ArtifactManifestSchema, instancePath);
    }

    public static void AssertValid(JsonSchema schema, string instancePath)
    {
        using var instanceDocument = JsonDocument.Parse(File.ReadAllText(instancePath));
        var results = schema.Evaluate(instanceDocument.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        });

        if (results.IsValid)
        {
            return;
        }

        results.ToList();
        Assert.True(
            results.IsValid,
            $"{instancePath} failed standard JSON Schema validation:{Environment.NewLine}{JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })}");
    }

    private static JsonSchema LoadSchema(string fileName)
    {
        var schemaPath = Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "docs",
            "matrix",
            "schemas",
            fileName);

        return JsonSerializer.Deserialize<JsonSchema>(File.ReadAllText(schemaPath))
            ?? throw new InvalidOperationException($"Unable to load JSON Schema: {schemaPath}");
    }
}
