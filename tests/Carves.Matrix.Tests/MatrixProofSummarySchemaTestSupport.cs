using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

internal static class MatrixProofSummarySchemaTestSupport
{
    public static string[] ValidateProofSummaryFile(string summaryPath)
    {
        return ValidateProofSummaryJson(File.ReadAllText(summaryPath));
    }

    public static string[] ValidateProofSummaryJson(string summaryJson)
    {
        var schemaPath = Path.Combine(
            LocateSourceRepoRoot(),
            "docs",
            "matrix",
            "schemas",
            "matrix-proof-summary.v0.schema.json");
        using var schemaDocument = JsonDocument.Parse(File.ReadAllText(schemaPath));
        using var summaryDocument = JsonDocument.Parse(summaryJson);

        var validator = new MatrixProofSummarySchemaSubsetValidator(schemaDocument.RootElement);
        return validator.Validate(summaryDocument.RootElement);
    }

    public static string[] ValidateMutatedNativeSummary(Action<JsonObject> mutate)
    {
        using var bundle = MatrixBundleFixture.Create();
        return ValidateMutatedSummary(Path.Combine(bundle.ArtifactRoot, "matrix-proof-summary.json"), mutate);
    }

    public static string[] ValidateMutatedFullReleaseSummary(Action<JsonObject> mutate)
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        return ValidateMutatedSummary(Path.Combine(bundle.ArtifactRoot, "matrix-proof-summary.json"), mutate);
    }

    public static string LocateSourceRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CARVES.Runtime.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root from test output directory.");
    }

    private static string[] ValidateMutatedSummary(string summaryPath, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(File.ReadAllText(summaryPath))!.AsObject();
        mutate(root);

        return ValidateProofSummaryJson(root.ToJsonString());
    }
}
