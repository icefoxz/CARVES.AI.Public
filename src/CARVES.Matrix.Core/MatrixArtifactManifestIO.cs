using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixArtifactManifestWriter
{
    public static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static long? ReadInt64(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var value)
            ? value
            : null;
    }

    private static bool? ReadBool(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;
    }
}
