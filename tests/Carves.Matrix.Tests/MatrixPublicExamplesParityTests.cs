using Carves.Matrix.Core;
using System.Text.Json;
using System.Text.RegularExpressions;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixPublicExamplesParityTests
{
    private static readonly Regex LocalPathPattern = new(
        @"(?ix)([A-Z]:\\|\\\\wsl\.localhost\\|/(home|users|mnt|tmp|var/folders|workspace|workspaces|runner)(/|$)|\$HOME|%USERPROFILE%)",
        RegexOptions.Compiled);

    private static readonly Regex TokenValuePattern = new(
        @"(?i)(-----BEGIN [A-Z ]*PRIVATE KEY-----|sk-[A-Za-z0-9_-]{16,}|ghp_[A-Za-z0-9_]{16,}|xox[baprs]-[A-Za-z0-9-]{16,})",
        RegexOptions.Compiled);

    private static readonly string[] ForbiddenPayloadPropertyNames =
    [
        "password",
        "api_key",
        "access_token",
        "refresh_token",
        "client_secret",
        "private_key",
        "secret_value",
        "credential_value",
        "customer_payload",
        "private_payload",
        "raw_diff",
        "prompt",
        "model_response"
    ];

    [Fact]
    public void SchemaExamples_AreClearlyNamedAndValidateWithStandardSchemas()
    {
        var examplesRoot = ExamplesRoot();
        var schemaExamples = Directory.GetFiles(examplesRoot, "*.schema-example.json", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path) ?? throw new InvalidOperationException($"Example path has no file name: {path}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "matrix-artifact-manifest.v0.schema-example.json",
                "matrix-proof-summary.v0.schema-example.json"
            ],
            schemaExamples);

        MatrixStandardJsonSchemaTestSupport.AssertArtifactManifestValid(
            Path.Combine(examplesRoot, "matrix-artifact-manifest.v0.schema-example.json"));
        MatrixStandardJsonSchemaTestSupport.AssertProofSummaryValid(
            Path.Combine(examplesRoot, "matrix-proof-summary.v0.schema-example.json"));
    }

    [Fact]
    public void RunnableBundleExamples_WhenPresent_AreClearlyNamedAndVerify()
    {
        var examplesRoot = ExamplesRoot();
        var runnableBundles = Directory.GetDirectories(examplesRoot, "*.runnable-bundle", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var ambiguousBundles = Directory.GetDirectories(examplesRoot, "*", SearchOption.TopDirectoryOnly)
            .Where(directory => File.Exists(Path.Combine(directory, MatrixArtifactManifestWriter.DefaultManifestFileName)))
            .Where(directory => !directory.EndsWith(".runnable-bundle", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(ambiguousBundles);

        foreach (var bundle in runnableBundles)
        {
            var result = RunMatrixCli("verify", bundle, "--json");
            Assert.Equal(0, result.ExitCode);
        }
    }

    [Fact]
    public void ExampleFiles_DoNotContainProducerLocalPathsOrPrivatePayloads()
    {
        foreach (var path in Directory.GetFiles(ExamplesRoot(), "*.json", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotMatch(LocalPathPattern, text);
            Assert.DoesNotMatch(TokenValuePattern, text);
            AssertNoForbiddenPayloadProperties(path);
        }
    }

    [Fact]
    public void ReadmeAndQuickstarts_DistinguishSchemaExamplesFromRunnableBundles()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var readme = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "README.md"));
        var quickstartEn = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "quickstart.en.md"));
        var quickstartZh = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "quickstart.zh-CN.md"));

        Assert.Contains("schema examples", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not runnable verification bundles", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".runnable-bundle", readme, StringComparison.Ordinal);
        Assert.Contains("schema examples", quickstartEn, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not runnable verification bundles", quickstartEn, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schema examples", quickstartZh, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not runnable verification bundles", quickstartZh, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExamplesRoot()
    {
        return Path.Combine(MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(), "docs", "matrix", "examples");
    }

    private static void AssertNoForbiddenPayloadProperties(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var forbidden = new HashSet<string>(ForbiddenPayloadPropertyNames, StringComparer.OrdinalIgnoreCase);
        var offendingProperties = new List<string>();
        CollectForbiddenPayloadProperties(document.RootElement, "$", forbidden, offendingProperties);

        Assert.Empty(offendingProperties);
    }

    private static void CollectForbiddenPayloadProperties(
        JsonElement element,
        string jsonPath,
        HashSet<string> forbidden,
        List<string> offendingProperties)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = $"{jsonPath}.{property.Name}";
                    if (forbidden.Contains(property.Name))
                    {
                        offendingProperties.Add(propertyPath);
                    }

                    CollectForbiddenPayloadProperties(property.Value, propertyPath, forbidden, offendingProperties);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    CollectForbiddenPayloadProperties(item, $"{jsonPath}[{index}]", forbidden, offendingProperties);
                    index++;
                }

                break;
        }
    }

}
