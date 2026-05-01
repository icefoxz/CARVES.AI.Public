using System.Text.Json;

namespace Carves.Matrix.Core;

internal sealed partial class MatrixJsonSchemaSubsetValidator
{
    private void ValidateAnyOf(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (!schema.TryGetProperty("anyOf", out var anyOf) || anyOf.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var childSchema in anyOf.EnumerateArray())
        {
            var childIssues = new List<MatrixJsonSchemaValidationIssue>();
            ValidateSchema(childSchema, instance, path, childIssues);
            if (childIssues.Count == 0)
            {
                return;
            }
        }

        issues.Add(new MatrixJsonSchemaValidationIssue(path, "did not match any allowed schema"));
    }

    private void ValidateNot(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (!schema.TryGetProperty("not", out var notSchema))
        {
            return;
        }

        var notIssues = new List<MatrixJsonSchemaValidationIssue>();
        ValidateSchema(notSchema, instance, path, notIssues);
        if (notIssues.Count == 0)
        {
            issues.Add(new MatrixJsonSchemaValidationIssue(path, "matched forbidden schema"));
        }
    }

    private void ValidateConditionals(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (!schema.TryGetProperty("if", out var ifSchema) || !schema.TryGetProperty("then", out var thenSchema))
        {
            return;
        }

        var ifIssues = new List<MatrixJsonSchemaValidationIssue>();
        ValidateSchema(ifSchema, instance, path, ifIssues);
        if (ifIssues.Count == 0)
        {
            ValidateSchema(thenSchema, instance, path, issues);
        }
    }
}
