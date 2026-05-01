using System.Text.Json;

namespace Carves.Runtime.Application.Platform;

public static class RuntimeTargetBindingReadbackResolver
{
    public static RuntimeTargetBindingReadback Resolve(
        string? targetRepoPath,
        string? expectedRuntimeRoot,
        string expectedRuntimeRootKind)
    {
        if (string.IsNullOrWhiteSpace(targetRepoPath))
        {
            return new RuntimeTargetBindingReadback(null, "target_repo_unavailable", "none", false, []);
        }

        var handshakePath = Path.Combine(targetRepoPath, ".ai", "runtime", "attach-handshake.json");
        var manifestPath = Path.Combine(targetRepoPath, ".ai", "runtime.json");
        var handshakeExists = File.Exists(handshakePath);
        var manifestExists = File.Exists(manifestPath);
        var handshakeRoot = ReadJsonString(handshakePath, "request", "runtime_root");
        var manifestRoot = ReadJsonString(manifestPath, "runtime_root");
        var hasHandshake = !string.IsNullOrWhiteSpace(handshakeRoot);
        var hasManifest = !string.IsNullOrWhiteSpace(manifestRoot);

        if (!hasHandshake && !hasManifest)
        {
            if (handshakeExists || manifestExists)
            {
                return new RuntimeTargetBindingReadback(
                    null,
                    "runtime_binding_missing_root",
                    handshakeExists && manifestExists
                        ? "attach_handshake_and_runtime_manifest"
                        : handshakeExists
                            ? "attach_handshake_runtime_root"
                            : "runtime_manifest_root",
                    true,
                    ["runtime_binding_missing_root", "operator_rebind_required"]);
            }

            return new RuntimeTargetBindingReadback(null, "no_existing_runtime_binding", "none", false, []);
        }

        var boundRoot = hasHandshake ? Path.GetFullPath(handshakeRoot!) : Path.GetFullPath(manifestRoot!);
        var source = hasHandshake && hasManifest
            ? "attach_handshake_and_runtime_manifest"
            : hasHandshake
                ? "attach_handshake_runtime_root"
                : "runtime_manifest_root";

        if (hasHandshake
            && hasManifest
            && !PathEquals(handshakeRoot!, manifestRoot!))
        {
            return new RuntimeTargetBindingReadback(
                boundRoot,
                "runtime_binding_internal_mismatch",
                source,
                true,
                ["runtime_binding_internal_mismatch", "operator_rebind_required"]);
        }

        if (!string.IsNullOrWhiteSpace(expectedRuntimeRoot)
            && !PathEquals(boundRoot, expectedRuntimeRoot))
        {
            return new RuntimeTargetBindingReadback(
                boundRoot,
                $"runtime_binding_conflicts_with_{expectedRuntimeRootKind}",
                source,
                true,
                ["runtime_binding_mismatch", "operator_rebind_required"]);
        }

        return new RuntimeTargetBindingReadback(
            boundRoot,
            string.IsNullOrWhiteSpace(expectedRuntimeRoot)
                ? "runtime_binding_present_expected_root_unavailable"
                : $"runtime_binding_matches_{expectedRuntimeRootKind}",
            source,
            false,
            []);
    }

    private static string? ReadJsonString(string path, params string[] propertyPath)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var current = document.RootElement;
            foreach (var property in propertyPath)
            {
                if (current.ValueKind != JsonValueKind.Object
                    || !current.TryGetProperty(property, out current))
                {
                    return null;
                }
            }

            return current.ValueKind == JsonValueKind.String
                ? current.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}

public sealed record RuntimeTargetBindingReadback(
    string? BoundRuntimeRoot,
    string Status,
    string Source,
    bool BlocksAgentStartup,
    IReadOnlyList<string> Gaps);
