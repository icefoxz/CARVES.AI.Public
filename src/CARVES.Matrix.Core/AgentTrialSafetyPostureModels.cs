using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

public sealed record AgentTrialSafetyPostureOptions(string WorkspaceRoot, bool MatrixVerified)
{
    public IReadOnlyList<string> MatrixReasonCodes { get; init; } = [];

    public string TaskContractRelativePath { get; init; } = ".carves/trial/task-contract.json";

    public string AgentReportRelativePath { get; init; } = "artifacts/agent-report.json";

    public string DiffScopeSummaryRelativePath { get; init; } = "artifacts/diff-scope-summary.json";

    public string TestEvidenceRelativePath { get; init; } = "artifacts/test-evidence.json";
}

public sealed record AgentTrialSafetyPostureProjection(
    string SchemaVersion,
    string Overall,
    IReadOnlyList<AgentTrialSafetyDimension> Dimensions,
    IReadOnlyList<string> ReasonCodes)
{
    public JsonObject ToJson()
    {
        return new JsonObject
        {
            ["schema_version"] = SchemaVersion,
            ["overall"] = Overall,
            ["dimensions"] = new JsonArray(Dimensions.Select(dimension => dimension.ToJson()).ToArray<JsonNode?>()),
            ["reason_codes"] = new JsonArray(ReasonCodes.Select(code => JsonValue.Create(code)).ToArray<JsonNode?>()),
        };
    }
}

public sealed record AgentTrialSafetyDimension(
    string Dimension,
    string Level,
    IReadOnlyList<string> ReasonCodes)
{
    public JsonObject ToJson()
    {
        return new JsonObject
        {
            ["dimension"] = Dimension,
            ["level"] = Level,
            ["reason_codes"] = new JsonArray(ReasonCodes.Select(code => JsonValue.Create(code)).ToArray<JsonNode?>()),
        };
    }
}
