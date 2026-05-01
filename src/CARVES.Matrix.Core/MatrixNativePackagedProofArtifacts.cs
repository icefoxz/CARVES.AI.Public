using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static void ResetNativeFullReleasePackagedArtifacts(string artifactRoot, string packagedRoot)
    {
        if (Directory.Exists(packagedRoot))
        {
            Directory.Delete(packagedRoot, recursive: true);
        }

        Directory.CreateDirectory(packagedRoot);
        var outputPath = ResolveNativeRelativePath(artifactRoot, "packaged-matrix-output.json");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
    }

    private static string WriteNativeFullReleasePackagedArtifacts(
        string artifactRoot,
        JsonElement matrixRoot,
        MatrixNativePackagingHarnessResult harness)
    {
        var summary = BuildNativeFullReleasePackagedSummary(matrixRoot, harness);
        var summaryJson = JsonSerializer.Serialize(summary, JsonOptions);
        File.WriteAllText(ResolveNativeRelativePath(artifactRoot, "packaged/matrix-packaged-summary.json"), summaryJson);
        return summaryJson;
    }

    private static object BuildNativeFullReleasePackagedSummary(
        JsonElement matrixRoot,
        MatrixNativePackagingHarnessResult harness)
    {
        return new
        {
            smoke = "matrix_packaged_install",
            producer = "native_full_release_packaged",
            guard_version = RequiredPackageVersion(harness, "guard"),
            handoff_version = RequiredPackageVersion(harness, "handoff"),
            audit_version = RequiredPackageVersion(harness, "audit"),
            shield_version = RequiredPackageVersion(harness, "shield"),
            matrix_version = RequiredPackageVersion(harness, "matrix"),
            package_root = "<redacted-local-package-root>",
            tool_root = "<redacted-local-tool-root>",
            artifact_root = ToPublicArtifactRootMarker(),
            remote_registry_published = false,
            nuget_org_push_required = false,
            installed_commands = new
            {
                carves_guard = "carves-guard",
                carves_handoff = "carves-handoff",
                carves_audit = "carves-audit",
                carves_shield = "carves-shield",
                carves_matrix = "carves-matrix",
            },
            packages = new
            {
                guard = RequiredPackageFileName(harness, "guard"),
                handoff = RequiredPackageFileName(harness, "handoff"),
                audit = RequiredPackageFileName(harness, "audit"),
                shield = RequiredPackageFileName(harness, "shield"),
                matrix = RequiredPackageFileName(harness, "matrix"),
            },
            matrix = matrixRoot,
            privacy = CloneJsonElement(matrixRoot, "privacy"),
            public_claims = CloneJsonElement(matrixRoot, "public_claims"),
            pack_command_count = harness.Packages.Count,
            install_command_count = harness.ToolInstalls.Count,
        };
    }

    private static string RequiredPackageVersion(MatrixNativePackagingHarnessResult harness, string toolName)
    {
        return harness.Packages.Single(package => string.Equals(package.ToolName, toolName, StringComparison.Ordinal)).Version;
    }

    private static string RequiredPackageFileName(MatrixNativePackagingHarnessResult harness, string toolName)
    {
        return Path.GetFileName(harness.Packages.Single(package => string.Equals(package.ToolName, toolName, StringComparison.Ordinal)).PackagePath);
    }

    private static JsonElement? CloneJsonElement(JsonElement root, params string[] path)
    {
        return TryGet(root, out var value, path) ? value.Clone() : null;
    }
}
