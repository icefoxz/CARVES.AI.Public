using System.Text.Json;

namespace Carves.Matrix.Core;

internal sealed partial class MatrixJsonSchemaSubsetValidator(JsonElement rootSchema)
{
    public IReadOnlyList<MatrixJsonSchemaValidationIssue> Validate(JsonElement instance)
    {
        var issues = new List<MatrixJsonSchemaValidationIssue>();
        ValidateSchema(rootSchema, instance, "$", issues);
        return issues;
    }

    private void ValidateSchema(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            ValidateSchema(ResolveReference(reference.GetString() ?? string.Empty), instance, path, issues);
        }

        if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var childSchema in allOf.EnumerateArray())
            {
                ValidateSchema(childSchema, instance, path, issues);
            }
        }

        ValidateType(schema, instance, path, issues);
        ValidateConst(schema, instance, path, issues);
        ValidateEnum(schema, instance, path, issues);
        ValidateRequired(schema, instance, path, issues);
        ValidateAdditionalProperties(schema, instance, path, issues);
        ValidatePropertyNames(schema, instance, path, issues);
        ValidateProperties(schema, instance, path, issues);
        ValidateItems(schema, instance, path, issues);
        ValidateContains(schema, instance, path, issues);
        ValidateStringPattern(schema, instance, path, issues);
        ValidateStringMinLength(schema, instance, path, issues);
        ValidateArrayMinItems(schema, instance, path, issues);
        ValidateIntegerMinimum(schema, instance, path, issues);
        ValidateAnyOf(schema, instance, path, issues);
        ValidateNot(schema, instance, path, issues);
        ValidateConditionals(schema, instance, path, issues);
    }

    private JsonElement ResolveReference(string reference)
    {
        const string prefix = "#/$defs/";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported schema reference: {reference}");
        }

        return rootSchema.GetProperty("$defs").GetProperty(reference[prefix.Length..]);
    }

    private static void ValidateType(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        var expectedTypes = ReadExpectedTypes(schema);
        if (expectedTypes.Length == 0 || expectedTypes.Any(type => MatchesType(instance, type)))
        {
            return;
        }

        issues.Add(new MatrixJsonSchemaValidationIssue(
            path,
            $"expected {string.Join("|", expectedTypes)}, found {instance.ValueKind}"));
    }

    private static string[] ReadExpectedTypes(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return [];
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return [type.GetString() ?? string.Empty];
        }

        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray()
            : [];
    }

    private static bool MatchesType(JsonElement instance, string expectedType)
    {
        return expectedType switch
        {
            "object" => instance.ValueKind == JsonValueKind.Object,
            "array" => instance.ValueKind == JsonValueKind.Array,
            "string" => instance.ValueKind == JsonValueKind.String,
            "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
            "number" => instance.ValueKind == JsonValueKind.Number,
            "null" => instance.ValueKind == JsonValueKind.Null,
            _ => throw new InvalidOperationException($"Unsupported schema type: {expectedType}")
        };
    }

    private static void ValidateConst(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (schema.TryGetProperty("const", out var expected) && !JsonElementEquals(expected, instance))
        {
            issues.Add(new MatrixJsonSchemaValidationIssue(path, "const mismatch"));
        }
    }

    private static void ValidateEnum(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (!schema.TryGetProperty("enum", out var enumValues) || enumValues.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        if (!enumValues.EnumerateArray().Any(value => JsonElementEquals(value, instance)))
        {
            issues.Add(new MatrixJsonSchemaValidationIssue(path, "enum mismatch"));
        }
    }

    private static void ValidateRequired(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("required", out var required)
            || required.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var requiredProperty in required.EnumerateArray())
        {
            var propertyName = requiredProperty.GetString() ?? string.Empty;
            if (!instance.TryGetProperty(propertyName, out _))
            {
                issues.Add(new MatrixJsonSchemaValidationIssue(path, $"missing required property {propertyName}"));
            }
        }
    }

    private static void ValidateAdditionalProperties(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("additionalProperties", out var additionalProperties)
            || additionalProperties.ValueKind != JsonValueKind.False)
        {
            return;
        }

        var allowedProperties = schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object
            ? properties.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
            : [];
        foreach (var property in instance.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                issues.Add(new MatrixJsonSchemaValidationIssue(path, $"unknown property {property.Name}"));
            }
        }
    }

    private static void ValidatePropertyNames(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("propertyNames", out var propertyNames)
            || !propertyNames.TryGetProperty("enum", out var enumValues)
            || enumValues.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var allowedProperties = enumValues.EnumerateArray()
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var property in instance.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                issues.Add(new MatrixJsonSchemaValidationIssue(path, $"property name {property.Name} is not allowed"));
            }
        }
    }

    private void ValidateProperties(
        JsonElement schema,
        JsonElement instance,
        string path,
        List<MatrixJsonSchemaValidationIssue> issues)
    {
        if (instance.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var propertySchema in properties.EnumerateObject())
        {
            if (instance.TryGetProperty(propertySchema.Name, out var propertyValue))
            {
                ValidateSchema(propertySchema.Value, propertyValue, $"{path}.{propertySchema.Name}", issues);
            }
        }
    }

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
    {
        return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
    }
}
