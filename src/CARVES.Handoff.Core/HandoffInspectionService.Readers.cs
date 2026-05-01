using System.Text.Json;

namespace Carves.Handoff.Core;

public sealed partial class HandoffInspectionService
{
    private static IReadOnlyList<HandoffInspectionReference> ReadReferences(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var refs) || refs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<HandoffInspectionReference>();
        foreach (var reference in refs.EnumerateArray())
        {
            if (reference.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            result.Add(new HandoffInspectionReference(
                ReadString(reference, "kind"),
                ReadString(reference, "ref"),
                ReadString(reference, "reason"),
                ReadString(reference, "summary"),
                ReadInt(reference, "priority")));
        }

        return result;
    }

    private static IReadOnlyList<HandoffInspectionTextItem> ReadTextItems(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<HandoffInspectionTextItem>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                result.Add(new HandoffInspectionTextItem(item.GetString() ?? string.Empty, null, null));
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            result.Add(new HandoffInspectionTextItem(
                ReadString(item, "item") ?? ReadString(item, "text") ?? ReadString(item, "reason") ?? string.Empty,
                ReadString(item, "reason"),
                ReadString(item, "unblock_condition")));
        }

        return result;
    }

    private static HandoffInspectionNextAction? ReadRecommendedNextAction(JsonElement root)
    {
        if (!root.TryGetProperty("recommended_next_action", out var action))
        {
            return null;
        }

        if (action.ValueKind == JsonValueKind.String)
        {
            return new HandoffInspectionNextAction(action.GetString() ?? string.Empty, null);
        }

        if (action.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new HandoffInspectionNextAction(
            ReadString(action, "action") ?? string.Empty,
            ReadString(action, "rationale"));
    }

    private static string ResolvePath(string repoRoot, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(repoRoot, path));
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static string? NormalizeOrientationValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : value.Trim();
    }
}
