using System.Text.Json;
using System.Text.RegularExpressions;

namespace Carves.Matrix.Core;

internal sealed partial class MatrixJsonSchemaSubsetValidator
{
    private void ValidateItems(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind != JsonValueKind.Array || !schema.TryGetProperty("items", out var itemSchema))
        {
            return;
        }

        var index = 0;
        foreach (var item in instance.EnumerateArray())
        {
            ValidateSchema(itemSchema, item, $"{path}[{index}]", issues);
            index++;
        }
    }

    private void ValidateContains(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind != JsonValueKind.Array || !schema.TryGetProperty("contains", out var containsSchema))
        {
            return;
        }

        foreach (var item in instance.EnumerateArray())
        {
            var itemIssues = new List<MatrixJsonSchemaValidationIssue>();
            ValidateSchema(containsSchema, item, path, itemIssues);
            if (itemIssues.Count == 0)
            {
                return;
            }
        }

        issues.Add(new MatrixJsonSchemaValidationIssue(path, "contains mismatch"));
    }

    private static void ValidateStringPattern(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind == JsonValueKind.String
            && schema.TryGetProperty("pattern", out var pattern)
            && pattern.ValueKind == JsonValueKind.String
            && !Regex.IsMatch(instance.GetString() ?? string.Empty, pattern.GetString() ?? string.Empty))
        {
            issues.Add(new MatrixJsonSchemaValidationIssue(path, "pattern mismatch"));
        }
    }

    private static void ValidateStringMinLength(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind == JsonValueKind.String
            && schema.TryGetProperty("minLength", out var minLength)
            && minLength.ValueKind == JsonValueKind.Number
            && minLength.TryGetInt32(out var minimum)
            && (instance.GetString() ?? string.Empty).Length < minimum)
        {
            issues.Add(new MatrixJsonSchemaValidationIssue(path, $"string shorter than {minimum}"));
        }
    }

    private static void ValidateArrayMinItems(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind == JsonValueKind.Array
            && schema.TryGetProperty("minItems", out var minItems)
            && minItems.ValueKind == JsonValueKind.Number
            && minItems.TryGetInt32(out var minimum)
            && instance.GetArrayLength() < minimum)
        {
            issues.Add(new MatrixJsonSchemaValidationIssue(path, $"array has fewer than {minimum} item(s)"));
        }
    }

    private static void ValidateIntegerMinimum(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind == JsonValueKind.Number
            && schema.TryGetProperty("minimum", out var minimum)
            && minimum.ValueKind == JsonValueKind.Number
            && instance.TryGetInt64(out var actual)
            && minimum.TryGetInt64(out var expectedMinimum)
            && actual < expectedMinimum)
        {
            issues.Add(new MatrixJsonSchemaValidationIssue(path, $"value is below minimum {expectedMinimum}"));
        }
    }
}
