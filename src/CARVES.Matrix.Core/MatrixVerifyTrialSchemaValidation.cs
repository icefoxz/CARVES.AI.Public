using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const int MaxTrialSchemaIssuesPerArtifact = 8;

    private static readonly IReadOnlyDictionary<string, string> TrialSchemaResourcesByArtifactKind =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["trial_task_contract"] = "Carves.Matrix.Core.Schemas.matrix-agent-task.v0.schema.json",
            ["trial_agent_report"] = "Carves.Matrix.Core.Schemas.agent-report.v0.schema.json",
            ["trial_diff_scope_summary"] = "Carves.Matrix.Core.Schemas.diff-scope-summary.v0.schema.json",
            ["trial_test_evidence"] = "Carves.Matrix.Core.Schemas.test-evidence.v0.schema.json",
            ["trial_result"] = "Carves.Matrix.Core.Schemas.carves-agent-trial-result.v0.schema.json",
        };

    private static readonly ConcurrentDictionary<string, Lazy<JsonDocument>> TrialSchemaDocuments = new(StringComparer.Ordinal);

    private static void ValidateTrialArtifactSchema(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        JsonElement root)
    {
        if (!TrialSchemaResourcesByArtifactKind.TryGetValue(requirement.ArtifactKind, out var resourceName))
        {
            return;
        }

        var schema = TrialSchemaDocuments.GetOrAdd(
            resourceName,
            static name => new Lazy<JsonDocument>(() => LoadEmbeddedTrialSchema(name))).Value;
        var schemaIssues = new MatrixJsonSchemaSubsetValidator(schema.RootElement)
            .Validate(root)
            .ToArray();
        foreach (var issue in schemaIssues.Take(MaxTrialSchemaIssuesPerArtifact))
        {
            issues.Add(new MatrixVerifyIssue(
                "trial_artifact",
                requirement.ArtifactKind,
                requirement.Path,
                $"trial_artifact_schema_invalid:{issue.InstancePath}",
                requirement.SchemaVersion,
                TruncateSchemaIssue(issue.ToString())));
        }

        if (schemaIssues.Length > MaxTrialSchemaIssuesPerArtifact)
        {
            issues.Add(new MatrixVerifyIssue(
                "trial_artifact",
                requirement.ArtifactKind,
                requirement.Path,
                "trial_artifact_schema_invalid:truncated",
                $"max_issues:{MaxTrialSchemaIssuesPerArtifact}",
                $"actual_issues:{schemaIssues.Length}"));
        }
    }

    private static JsonDocument LoadEmbeddedTrialSchema(string resourceName)
    {
        var assembly = typeof(MatrixCliRunner).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded Matrix trial schema not found: {resourceName}");
        return JsonDocument.Parse(stream);
    }

    private static string TruncateSchemaIssue(string issue)
    {
        const int maxLength = 240;
        return issue.Length <= maxLength
            ? issue
            : issue[..maxLength] + "...";
    }
}
