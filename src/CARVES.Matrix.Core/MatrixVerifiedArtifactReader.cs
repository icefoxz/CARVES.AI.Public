using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private sealed record MatrixVerifiedArtifactManifestEntry(string Sha256, long Size);

    private sealed record MatrixVerifiedArtifactReadTarget(
        string Scope,
        string ArtifactKind,
        string Path,
        string ManifestCodePrefix,
        string SourceMissingCode,
        string SourceInvalidJsonCodePrefix,
        string SourceReadFailedCodePrefix);

    private static bool TryReadVerifiedSummarySourceDocument(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        MatrixArtifactManifestRequirement requirement,
        MatrixArtifactManifestVerificationResult manifestVerification,
        string codePrefix,
        out JsonDocument document,
        out bool coverageConsistent)
    {
        var target = new MatrixVerifiedArtifactReadTarget(
            "summary",
            "matrix_proof_summary",
            summaryRelativePath,
            "summary_source",
            $"{codePrefix}_source_missing",
            $"{codePrefix}_source_invalid_json",
            $"{codePrefix}_source_read_failed");
        return TryReadVerifiedArtifactDocument(
            artifactRoot,
            issues,
            target,
            requirement,
            manifestVerification,
            out document,
            out coverageConsistent);
    }

    private static bool TryReadVerifiedShieldEvaluationDocument(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        MatrixArtifactManifestVerificationResult manifestVerification,
        out JsonDocument document,
        out bool coverageConsistent)
    {
        var target = new MatrixVerifiedArtifactReadTarget(
            "shield_evaluation",
            requirement.ArtifactKind,
            requirement.Path,
            "shield_evaluation_source",
            "shield_evaluation_source_missing",
            "shield_evaluation_source_invalid_json",
            "shield_evaluation_source_read_failed");
        return TryReadVerifiedArtifactDocument(
            artifactRoot,
            issues,
            target,
            requirement,
            manifestVerification,
            out document,
            out coverageConsistent);
    }

    private static bool TryReadVerifiedArtifactDocument(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        MatrixVerifiedArtifactReadTarget target,
        MatrixArtifactManifestRequirement requirement,
        MatrixArtifactManifestVerificationResult manifestVerification,
        out JsonDocument document,
        out bool coverageConsistent)
    {
        document = null!;
        if (!TryGetVerifiedArtifactManifestEntry(
                artifactRoot,
                issues,
                target,
                requirement,
                manifestVerification,
                out var entry,
                out coverageConsistent))
        {
            return false;
        }

        var sourcePath = Path.Combine(artifactRoot, requirement.Path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(sourcePath))
        {
            AddVerifiedArtifactIssue(issues, target, target.SourceMissingCode, requirement.Path, null);
            return false;
        }

        try
        {
            if (MatrixArtifactManifestWriter.IsReparsePointOrSymbolicLink(sourcePath))
            {
                AddVerifiedArtifactIssue(
                    issues,
                    target,
                    $"{target.ManifestCodePrefix}_reparse_point_rejected:{requirement.ArtifactKind}",
                    requirement.Path,
                    "source:link_or_reparse_rejected");
                return false;
            }

            var sourceBytes = File.ReadAllBytes(sourcePath);
            var actualSize = sourceBytes.LongLength;
            if (entry.Size != actualSize)
            {
                AddVerifiedArtifactIssue(
                    issues,
                    target,
                    $"{target.ManifestCodePrefix}_manifest_size_mismatch:{requirement.ArtifactKind}",
                    entry.Size.ToString(),
                    actualSize.ToString());
                return false;
            }

            var actualSha256 = Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
            if (!string.Equals(entry.Sha256, actualSha256, StringComparison.Ordinal))
            {
                AddVerifiedArtifactIssue(
                    issues,
                    target,
                    $"{target.ManifestCodePrefix}_manifest_hash_mismatch:{requirement.ArtifactKind}",
                    entry.Sha256,
                    actualSha256);
                return false;
            }

            document = JsonDocument.Parse(sourceBytes);
            return true;
        }
        catch (JsonException ex)
        {
            AddVerifiedArtifactIssue(issues, target, $"{target.SourceInvalidJsonCodePrefix}:{ex.GetType().Name}", null, null);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddVerifiedArtifactIssue(issues, target, $"{target.SourceReadFailedCodePrefix}:{ex.GetType().Name}", null, null);
            return false;
        }
    }

    private static void AddVerifiedArtifactIssue(
        List<MatrixVerifyIssue> issues,
        MatrixVerifiedArtifactReadTarget target,
        string code,
        string? expected,
        string? actual)
    {
        issues.Add(new MatrixVerifyIssue(
            target.Scope,
            target.ArtifactKind,
            target.Path,
            code,
            expected,
            actual));
    }
}
