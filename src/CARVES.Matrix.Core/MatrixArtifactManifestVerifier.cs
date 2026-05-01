using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixArtifactManifestWriter
{
    public static MatrixArtifactManifestVerificationResult VerifyManifest(string manifestPath)
    {
        return VerifyManifest(manifestPath, expectedArtifactRoot: null);
    }

    public static MatrixArtifactManifestVerificationResult VerifyManifest(string manifestPath, string? expectedArtifactRoot)
    {
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return new MatrixArtifactManifestVerificationResult(
                "invalid_manifest",
                [new MatrixArtifactVerificationIssue("manifest", manifestPath, "manifest_missing", null, null, null, null)]);
        }

        try
        {
            var verificationArtifactRoot = ResolveVerificationArtifactRoot(manifestPath, expectedArtifactRoot);
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            if (!string.Equals(ReadString(root, "schema_version"), ManifestSchemaVersion, StringComparison.Ordinal))
            {
                return new MatrixArtifactManifestVerificationResult(
                    "invalid_manifest",
                    [new MatrixArtifactVerificationIssue("manifest", manifestPath, "unsupported_schema", ManifestSchemaVersion, ReadString(root, "schema_version"), null, null)]);
            }

            var artifactRoot = ReadString(root, "artifact_root");
            if (string.IsNullOrWhiteSpace(artifactRoot))
            {
                return new MatrixArtifactManifestVerificationResult(
                    "invalid_manifest",
                    [new MatrixArtifactVerificationIssue("manifest", manifestPath, "artifact_root_missing", null, null, null, null)]);
            }

            if (!root.TryGetProperty("artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array)
            {
                return new MatrixArtifactManifestVerificationResult(
                    "invalid_manifest",
                    [new MatrixArtifactVerificationIssue("manifest", manifestPath, "artifacts_missing", null, null, null, null)]);
            }

            var issues = new List<MatrixArtifactVerificationIssue>();
            ValidatePrivacyFlags(issues, "manifest", manifestPath, root, "privacy");
            ValidateManifestArtifactRoot(issues, manifestPath, artifactRoot);
            foreach (var artifact in artifacts.EnumerateArray())
            {
                VerifyManifestArtifact(verificationArtifactRoot, issues, artifact);
            }

            return new MatrixArtifactManifestVerificationResult(
                ResolveVerificationPosture(issues),
                issues);
        }
        catch (JsonException ex)
        {
            return new MatrixArtifactManifestVerificationResult(
                "invalid_manifest",
                [new MatrixArtifactVerificationIssue("manifest", manifestPath, $"invalid_json:{ex.GetType().Name}", null, null, null, null)]);
        }
        catch (IOException ex)
        {
            return new MatrixArtifactManifestVerificationResult(
                "invalid_manifest",
                [new MatrixArtifactVerificationIssue("manifest", manifestPath, $"manifest_read_failed:{ex.GetType().Name}", null, null, null, null)]);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new MatrixArtifactManifestVerificationResult(
                "invalid_manifest",
                [new MatrixArtifactVerificationIssue("manifest", manifestPath, $"manifest_read_failed:{ex.GetType().Name}", null, null, null, null)]);
        }
        catch (ArgumentException ex)
        {
            return new MatrixArtifactManifestVerificationResult(
                "invalid_manifest",
                [new MatrixArtifactVerificationIssue("manifest", manifestPath, $"artifact_root_invalid:{ex.GetType().Name}", null, null, null, null)]);
        }
    }

    private static void VerifyManifestArtifact(
        string artifactRoot,
        List<MatrixArtifactVerificationIssue> issues,
        JsonElement artifact)
    {
        var kind = ReadString(artifact, "artifact_kind") ?? "unknown";
        var relativePath = ReadString(artifact, "path") ?? string.Empty;
        var expectedSha256 = ReadString(artifact, "sha256");
        var expectedSize = ReadInt64(artifact, "size");
        ValidatePrivacyFlags(issues, kind, relativePath, artifact, "privacy_flags");
        if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(expectedSha256) || !expectedSize.HasValue)
        {
            issues.Add(new MatrixArtifactVerificationIssue(kind, relativePath, "artifact_manifest_entry_incomplete", expectedSha256, null, expectedSize, null));
            return;
        }

        string fullPath;
        try
        {
            fullPath = ResolveRelativePath(artifactRoot, relativePath);
        }
        catch (ArgumentException)
        {
            issues.Add(new MatrixArtifactVerificationIssue(kind, relativePath, "artifact_path_escapes_root", expectedSha256, null, expectedSize, null));
            return;
        }

        if (!File.Exists(fullPath))
        {
            issues.Add(new MatrixArtifactVerificationIssue(kind, relativePath, "artifact_missing", expectedSha256, null, expectedSize, null));
            return;
        }

        try
        {
            if (IsReparsePointOrSymbolicLink(fullPath))
            {
                issues.Add(new MatrixArtifactVerificationIssue(kind, relativePath, "artifact_reparse_point_rejected", expectedSha256, null, expectedSize, null));
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new MatrixArtifactVerificationIssue(kind, relativePath, $"artifact_metadata_read_failed:{ex.GetType().Name}", expectedSha256, null, expectedSize, null));
            return;
        }

        var actualSha256 = ComputeFileSha256(fullPath);
        var actualSize = new FileInfo(fullPath).Length;
        if (!string.Equals(expectedSha256, actualSha256, StringComparison.Ordinal))
        {
            issues.Add(new MatrixArtifactVerificationIssue(kind, relativePath, "artifact_hash_mismatch", expectedSha256, actualSha256, expectedSize, actualSize));
            return;
        }

        if (expectedSize != actualSize)
        {
            issues.Add(new MatrixArtifactVerificationIssue(kind, relativePath, "artifact_size_mismatch", expectedSha256, actualSha256, expectedSize, actualSize));
        }
    }

    private static string ResolveVerificationArtifactRoot(string manifestPath, string? expectedArtifactRoot)
    {
        if (!string.IsNullOrWhiteSpace(expectedArtifactRoot))
        {
            return Path.GetFullPath(expectedArtifactRoot);
        }

        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            throw new ArgumentException("Manifest path must have a directory.", nameof(manifestPath));
        }

        return manifestDirectory;
    }

    private static void ValidateManifestArtifactRoot(
        List<MatrixArtifactVerificationIssue> issues,
        string manifestPath,
        string manifestArtifactRoot)
    {
        if (string.Equals(manifestArtifactRoot, PortableArtifactRoot, StringComparison.Ordinal)
            || string.Equals(manifestArtifactRoot, "bundle_root", StringComparison.Ordinal))
        {
            return;
        }

        issues.Add(new MatrixArtifactVerificationIssue(
            "manifest",
            manifestPath,
            "artifact_root_not_portable",
            PortableArtifactRoot,
            manifestArtifactRoot,
            null,
            null));
    }

    private static string ResolveVerificationPosture(IReadOnlyList<MatrixArtifactVerificationIssue> issues)
    {
        if (issues.Count == 0)
        {
            return "verified";
        }

        return issues.Any(issue => issue.Code.StartsWith("privacy_", StringComparison.Ordinal))
            ? "privacy_gate_failed"
            : "changed_or_substituted";
    }
}
