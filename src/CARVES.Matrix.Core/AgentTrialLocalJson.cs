using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

internal static class AgentTrialLocalJson
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public const string MissingArtifactHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    public static JsonObject ReadObject(string path)
    {
        return JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidDataException($"Expected JSON object: {path}");
    }

    public static void WriteObject(string path, JsonObject value)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, value.ToJsonString(JsonOptions) + Environment.NewLine);
    }

    public static string GetRequiredString(JsonObject root, string propertyName)
    {
        var value = root[propertyName]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Required string property is missing: {propertyName}");
        }

        return value;
    }

    public static IReadOnlyList<string> GetStringArray(JsonObject root, string propertyName)
    {
        var array = root[propertyName]?.AsArray()
            ?? throw new InvalidDataException($"Required array property is missing: {propertyName}");

        return array
            .Select(item => item?.GetValue<string>() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    public static bool GetBooleanOrDefault(JsonObject root, string propertyName, bool defaultValue)
    {
        return root.TryGetPropertyValue(propertyName, out var value) && value is not null
            ? value.GetValue<bool>()
            : defaultValue;
    }

    public static string HashFileOrMissing(string path)
    {
        return File.Exists(path) ? HashFile(path) : MissingArtifactHash;
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string HashString(string value)
    {
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
