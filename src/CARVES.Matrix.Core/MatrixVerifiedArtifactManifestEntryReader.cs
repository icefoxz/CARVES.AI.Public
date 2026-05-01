using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool TryGetVerifiedArtifactManifestEntry(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        MatrixVerifiedArtifactReadTarget target,
        MatrixArtifactManifestRequirement requirement,
        MatrixArtifactManifestVerificationResult manifestVerification,
        out MatrixVerifiedArtifactManifestEntry entry,
        out bool coverageConsistent)
    {
        entry = null!;
        coverageConsistent = true;
        var manifestPath = Path.Combine(artifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        if (!File.Exists(manifestPath))
        {
            AddVerifiedArtifactIssue(issues, target, $"{target.ManifestCodePrefix}_manifest_missing", MatrixArtifactManifestWriter.DefaultManifestFileName, null);
            return false;
        }

        try
        {
            using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!manifestDocument.RootElement.TryGetProperty("artifacts", out var artifacts)
                || artifacts.ValueKind != JsonValueKind.Array)
            {
                AddVerifiedArtifactIssue(issues, target, $"{target.ManifestCodePrefix}_manifest_artifacts_missing", "artifacts", null);
                return false;
            }

            var matches = artifacts.EnumerateArray()
                .Where(artifact => string.Equals(GetString(artifact, "artifact_kind"), requirement.ArtifactKind, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                AddVerifiedArtifactIssue(
                    issues,
                    target,
                    $"{target.ManifestCodePrefix}_manifest_entry_missing:{requirement.ArtifactKind}",
                    requirement.Path,
                    null);
                return false;
            }

            if (matches.Length > 1)
            {
                AddVerifiedArtifactIssue(
                    issues,
                    target,
                    $"{target.ManifestCodePrefix}_manifest_entry_duplicate:{requirement.ArtifactKind}",
                    "1",
                    matches.Length.ToString());
                return false;
            }

            return TryMaterializeVerifiedArtifactEntry(
                issues,
                target,
                requirement,
                manifestVerification,
                matches[0],
                out entry,
                out coverageConsistent);
        }
        catch (JsonException ex)
        {
            AddVerifiedArtifactIssue(issues, target, $"{target.ManifestCodePrefix}_manifest_invalid_json:{ex.GetType().Name}", null, null);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddVerifiedArtifactIssue(issues, target, $"{target.ManifestCodePrefix}_manifest_read_failed:{ex.GetType().Name}", null, null);
            return false;
        }
    }

    private static bool TryMaterializeVerifiedArtifactEntry(
        List<MatrixVerifyIssue> issues,
        MatrixVerifiedArtifactReadTarget target,
        MatrixArtifactManifestRequirement requirement,
        MatrixArtifactManifestVerificationResult manifestVerification,
        JsonElement manifestEntry,
        out MatrixVerifiedArtifactManifestEntry entry,
        out bool coverageConsistent)
    {
        entry = null!;
        coverageConsistent = true;
        var entryMetadataConsistent = true;
        entryMetadataConsistent &= VerifyVerifiedArtifactManifestField(issues, target, requirement, "path", requirement.Path, GetString(manifestEntry, "path"));
        entryMetadataConsistent &= VerifyVerifiedArtifactManifestField(issues, target, requirement, "schema_version", requirement.SchemaVersion, GetString(manifestEntry, "schema_version"));
        entryMetadataConsistent &= VerifyVerifiedArtifactManifestField(issues, target, requirement, "producer", requirement.Producer, GetString(manifestEntry, "producer"));
        if (!entryMetadataConsistent)
        {
            coverageConsistent = false;
            return false;
        }

        var sha256 = GetString(manifestEntry, "sha256");
        var size = GetLong(manifestEntry, "size");
        if (string.IsNullOrWhiteSpace(sha256) || !size.HasValue)
        {
            AddVerifiedArtifactIssue(
                issues,
                target,
                $"{target.ManifestCodePrefix}_manifest_entry_incomplete:{requirement.ArtifactKind}",
                "sha256|size",
                null);
            return false;
        }

        entry = new MatrixVerifiedArtifactManifestEntry(sha256, size.Value);
        coverageConsistent = VerifyManifestVerificationHasNoSourceIssues(
            issues,
            target,
            requirement,
            manifestVerification);
        return true;
    }

    private static bool VerifyManifestVerificationHasNoSourceIssues(
        List<MatrixVerifyIssue> issues,
        MatrixVerifiedArtifactReadTarget target,
        MatrixArtifactManifestRequirement requirement,
        MatrixArtifactManifestVerificationResult manifestVerification)
    {
        var sourceIssues = manifestVerification.Issues
            .Where(issue => string.Equals(issue.ArtifactKind, requirement.ArtifactKind, StringComparison.Ordinal))
            .Select(issue => issue.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        if (sourceIssues.Length == 0)
        {
            return true;
        }

        AddVerifiedArtifactIssue(
            issues,
            target,
            $"{target.ManifestCodePrefix}_manifest_entry_unverified:{requirement.ArtifactKind}",
            "verified",
            string.Join("|", sourceIssues));
        return false;
    }

    private static bool VerifyVerifiedArtifactManifestField(
        List<MatrixVerifyIssue> issues,
        MatrixVerifiedArtifactReadTarget target,
        MatrixArtifactManifestRequirement requirement,
        string fieldName,
        string expected,
        string? actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return true;
        }

        AddVerifiedArtifactIssue(
            issues,
            target,
            $"{target.ManifestCodePrefix}_manifest_{requirement.ArtifactKind}_{fieldName}_mismatch",
            expected,
            actual);
        return false;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetInt64(out var value) ? value : null;
    }
}
