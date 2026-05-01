using System.Text.Json;
using System.Text.RegularExpressions;

namespace Carves.Matrix.Tests;

internal sealed class MatrixProofSummarySchemaSubsetValidator(JsonElement rootSchema)
{
    public string[] Validate(JsonElement instance)
    {
        var errors = new List<string>();
        ValidateSchema(rootSchema, instance, "$", errors);
        return errors.ToArray();
    }

    private void ValidateSchema(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            ValidateSchema(ResolveReference(reference.GetString() ?? string.Empty), instance, path, errors);
        }

        if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            foreach (var childSchema in allOf.EnumerateArray())
            {
                ValidateSchema(childSchema, instance, path, errors);
            }
        }

        ValidateType(schema, instance, path, errors);
        ValidateConst(schema, instance, path, errors);
        ValidateEnum(schema, instance, path, errors);
        ValidateRequired(schema, instance, path, errors);
        ValidateAdditionalProperties(schema, instance, path, errors);
        ValidatePropertyNames(schema, instance, path, errors);
        ValidateProperties(schema, instance, path, errors);
        ValidateItems(schema, instance, path, errors);
        ValidateStringPattern(schema, instance, path, errors);
        ValidateIntegerMinimum(schema, instance, path, errors);
        ValidateAnyOf(schema, instance, path, errors);
        ValidateNot(schema, instance, path, errors);
        ValidateConditionals(schema, instance, path, errors);
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

    private static void ValidateType(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (!schema.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var expectedType = type.GetString();
        var matches = expectedType switch
        {
            "object" => instance.ValueKind == JsonValueKind.Object,
            "array" => instance.ValueKind == JsonValueKind.Array,
            "string" => instance.ValueKind == JsonValueKind.String,
            "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
            _ => throw new InvalidOperationException($"Unsupported schema type: {expectedType}")
        };

        if (!matches)
        {
            errors.Add($"{path}: expected {expectedType}, found {instance.ValueKind}");
        }
    }

    private static void ValidateConst(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.TryGetProperty("const", out var expected) && !JsonElementEquals(expected, instance))
        {
            errors.Add($"{path}: const mismatch");
        }
    }

    private static void ValidateEnum(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (!schema.TryGetProperty("enum", out var enumValues) || enumValues.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        if (!enumValues.EnumerateArray().Any(value => JsonElementEquals(value, instance)))
        {
            errors.Add($"{path}: enum mismatch");
        }
    }

    private static void ValidateRequired(JsonElement schema, JsonElement instance, string path, List<string> errors)
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
                errors.Add($"{path}: missing required property {propertyName}");
            }
        }
    }

    private static void ValidateAdditionalProperties(JsonElement schema, JsonElement instance, string path, List<string> errors)
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
                errors.Add($"{path}: unknown property {property.Name}");
            }
        }
    }

    private static void ValidatePropertyNames(JsonElement schema, JsonElement instance, string path, List<string> errors)
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
                errors.Add($"{path}: property name {property.Name} is not allowed");
            }
        }
    }

    private void ValidateProperties(JsonElement schema, JsonElement instance, string path, List<string> errors)
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
                ValidateSchema(propertySchema.Value, propertyValue, $"{path}.{propertySchema.Name}", errors);
            }
        }
    }

    private void ValidateItems(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (instance.ValueKind != JsonValueKind.Array || !schema.TryGetProperty("items", out var itemSchema))
        {
            return;
        }

        var index = 0;
        foreach (var item in instance.EnumerateArray())
        {
            ValidateSchema(itemSchema, item, $"{path}[{index}]", errors);
            index++;
        }
    }

    private static void ValidateStringPattern(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (instance.ValueKind == JsonValueKind.String
            && schema.TryGetProperty("pattern", out var pattern)
            && pattern.ValueKind == JsonValueKind.String
            && !Regex.IsMatch(instance.GetString() ?? string.Empty, pattern.GetString() ?? string.Empty))
        {
            errors.Add($"{path}: pattern mismatch");
        }
    }

    private static void ValidateIntegerMinimum(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (instance.ValueKind == JsonValueKind.Number
            && schema.TryGetProperty("minimum", out var minimum)
            && minimum.ValueKind == JsonValueKind.Number
            && instance.TryGetInt64(out var actual)
            && minimum.TryGetInt64(out var expectedMinimum)
            && actual < expectedMinimum)
        {
            errors.Add($"{path}: value is below minimum {expectedMinimum}");
        }
    }

    private void ValidateConditionals(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (!schema.TryGetProperty("if", out var ifSchema) || !schema.TryGetProperty("then", out var thenSchema))
        {
            return;
        }

        var ifErrors = new List<string>();
        ValidateSchema(ifSchema, instance, path, ifErrors);
        if (ifErrors.Count == 0)
        {
            ValidateSchema(thenSchema, instance, path, errors);
        }
    }

    private void ValidateNot(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (!schema.TryGetProperty("not", out var notSchema))
        {
            return;
        }

        var notErrors = new List<string>();
        ValidateSchema(notSchema, instance, path, notErrors);
        if (notErrors.Count == 0)
        {
            errors.Add($"{path}: matched forbidden schema");
        }
    }

    private void ValidateAnyOf(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (!schema.TryGetProperty("anyOf", out var anyOf) || anyOf.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var childSchema in anyOf.EnumerateArray())
        {
            var childErrors = new List<string>();
            ValidateSchema(childSchema, instance, path, childErrors);
            if (childErrors.Count == 0)
            {
                return;
            }
        }

        errors.Add($"{path}: did not match any allowed schema");
    }

    private static bool JsonElementEquals(JsonElement left, JsonElement right)
    {
        return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
    }
}
