using Json.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

public sealed class AgentTrialLocalMvpSchemaValidationTests
{
    public static IEnumerable<object[]> ValidLocalMvpArtifacts()
    {
        yield return
        [
            "matrix-agent-task.v0.schema.json",
            Path.Combine(
                "tests",
                "fixtures",
                "agent-trial-v1",
                "task-001-pack",
                ".carves",
                "trial",
                "task-contract.json"),
        ];
        yield return
        [
            "agent-report.v0.schema.json",
            Path.Combine(
                "tests",
                "fixtures",
                "agent-trial-v1",
                "local-mvp-schema-examples",
                "agent-report.json"),
        ];
        yield return
        [
            "diff-scope-summary.v0.schema.json",
            Path.Combine(
                "tests",
                "fixtures",
                "agent-trial-v1",
                "local-mvp-schema-examples",
                "diff-scope-summary.json"),
        ];
        yield return
        [
            "test-evidence.v0.schema.json",
            Path.Combine(
                "tests",
                "fixtures",
                "agent-trial-v1",
                "local-mvp-schema-examples",
                "test-evidence.json"),
        ];
        yield return
        [
            "carves-agent-trial-result.v0.schema.json",
            Path.Combine(
                "tests",
                "fixtures",
                "agent-trial-v1",
                "local-mvp-schema-examples",
                "carves-agent-trial-result.json"),
        ];
    }

    [Theory]
    [MemberData(nameof(ValidLocalMvpArtifacts))]
    public void LocalMvpSchemas_AcceptGoodFixtureArtifacts(string schemaFileName, string instanceRelativePath)
    {
        var schema = MatrixStandardJsonSchemaTestSupport.LoadPublicSchema(schemaFileName);
        var instancePath = Path.Combine(MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(), instanceRelativePath);

        MatrixStandardJsonSchemaTestSupport.AssertValid(schema, instancePath);
    }

    [Theory]
    [MemberData(nameof(ValidLocalMvpArtifacts))]
    public void LocalMvpSchemas_RejectMissingRequiredSchemaVersion(string schemaFileName, string instanceRelativePath)
    {
        var schema = MatrixStandardJsonSchemaTestSupport.LoadPublicSchema(schemaFileName);
        var instancePath = Path.Combine(MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(), instanceRelativePath);
        var instance = JsonNode.Parse(File.ReadAllText(instancePath))?.AsObject()
            ?? throw new InvalidOperationException($"Unable to parse fixture instance: {instancePath}");

        Assert.True(instance.Remove("schema_version"));

        var results = Evaluate(schema, instance);

        Assert.False(results.IsValid);
    }

    [Fact]
    public void LocalMvpAgentTaskSchema_RejectsServerIssuedChallengeForLocalMvp()
    {
        var schema = MatrixStandardJsonSchemaTestSupport.LoadPublicSchema("matrix-agent-task.v0.schema.json");
        var instancePath = Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "tests",
            "fixtures",
            "agent-trial-v1",
            "task-001-pack",
            ".carves",
            "trial",
            "task-contract.json");
        var instance = JsonNode.Parse(File.ReadAllText(instancePath))?.AsObject()
            ?? throw new InvalidOperationException($"Unable to parse fixture instance: {instancePath}");

        instance["challenge_source"] = "server_issued";

        var results = Evaluate(schema, instance);

        Assert.False(results.IsValid);
    }

    private static EvaluationResults Evaluate(JsonSchema schema, JsonNode instance)
    {
        using var document = JsonDocument.Parse(instance.ToJsonString());

        return schema.Evaluate(document.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        });
    }
}
