using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static MatrixVerifyShieldEvaluation VerifyShieldEvaluation(
        string artifactRoot,
        MatrixArtifactManifestVerificationResult manifestVerification,
        List<MatrixVerifyIssue> issues)
    {
        var requirement = MatrixArtifactManifestWriter.DefaultRequiredArtifacts.Single(
            artifact => string.Equals(artifact.ArtifactKind, "shield_evaluation", StringComparison.Ordinal));
        var path = Path.Combine(artifactRoot, requirement.Path.Replace('/', Path.DirectorySeparatorChar));
        var present = File.Exists(path);
        if (!TryReadVerifiedShieldEvaluationDocument(
                artifactRoot,
                issues,
                requirement,
                manifestVerification,
                out var document,
                out var coverageConsistent))
        {
            return new MatrixVerifyShieldEvaluation(requirement.Path, Present: present, ScoreVerified: false);
        }

        try
        {
            using (document)
            {
                var root = document.RootElement;
                var scoreVerified = coverageConsistent;
                if (!string.Equals(GetString(root, "schema_version"), requirement.SchemaVersion, StringComparison.Ordinal))
                {
                    scoreVerified = false;
                    issues.Add(new MatrixVerifyIssue(
                        "shield_evaluation",
                        requirement.ArtifactKind,
                        requirement.Path,
                        "shield_evaluation_schema_mismatch",
                        $"schema_version:{requirement.SchemaVersion}",
                        GetString(root, "schema_version") is { } actual ? $"schema_version:{actual}" : null));
                }

                scoreVerified &= VerifyShieldEvaluationField(
                    issues,
                    requirement,
                    "status",
                    expected: "status:ok",
                    string.Equals(GetString(root, "status"), "ok", StringComparison.Ordinal),
                    GetString(root, "status") is { } status ? $"status:{status}" : null);
                scoreVerified &= VerifyShieldEvaluationField(
                    issues,
                    requirement,
                    "certification",
                    expected: "certification:false",
                    GetBool(root, "certification") == false,
                    GetBool(root, "certification") is { } certification ? $"certification:{certification.ToString().ToLowerInvariant()}" : null);
                scoreVerified &= VerifyShieldEvaluationField(
                    issues,
                    requirement,
                    "standard.label",
                    expected: "standard.label:present",
                    !string.IsNullOrWhiteSpace(GetString(root, "standard", "label")),
                    null);
                scoreVerified &= VerifyShieldEvaluationField(
                    issues,
                    requirement,
                    "lite.score",
                    expected: "lite.score:0..100",
                    GetInt(root, "lite", "score") is >= 0 and <= 100,
                    GetInt(root, "lite", "score") is { } score ? $"lite.score:{score}" : null);
                scoreVerified &= VerifyShieldEvaluationField(
                    issues,
                    requirement,
                    "lite.band",
                    expected: "lite.band:present",
                    !string.IsNullOrWhiteSpace(GetString(root, "lite", "band")),
                    null);
                scoreVerified &= VerifyShieldEvidenceHash(artifactRoot, issues, requirement, root);

                return new MatrixVerifyShieldEvaluation(requirement.Path, Present: true, ScoreVerified: scoreVerified);
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new MatrixVerifyIssue(
                "shield_evaluation",
                requirement.ArtifactKind,
                requirement.Path,
                $"shield_evaluation_invalid_json:{ex.GetType().Name}",
                null,
                null));
            return new MatrixVerifyShieldEvaluation(requirement.Path, Present: true, ScoreVerified: false);
        }
        catch (IOException ex)
        {
            issues.Add(new MatrixVerifyIssue(
                "shield_evaluation",
                requirement.ArtifactKind,
                requirement.Path,
                $"shield_evaluation_read_failed:{ex.GetType().Name}",
                null,
                null));
            return new MatrixVerifyShieldEvaluation(requirement.Path, Present: true, ScoreVerified: false);
        }
        catch (UnauthorizedAccessException ex)
        {
            issues.Add(new MatrixVerifyIssue(
                "shield_evaluation",
                requirement.ArtifactKind,
                requirement.Path,
                $"shield_evaluation_read_failed:{ex.GetType().Name}",
                null,
                null));
            return new MatrixVerifyShieldEvaluation(requirement.Path, Present: true, ScoreVerified: false);
        }
    }

    private static bool VerifyShieldEvaluationField(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        string field,
        string expected,
        bool verified,
        string? actual)
    {
        if (verified)
        {
            return true;
        }

        issues.Add(new MatrixVerifyIssue(
            "shield_evaluation",
            requirement.ArtifactKind,
            requirement.Path,
            "shield_score_unverified",
            expected,
            actual ?? $"{field}:missing"));
        return false;
    }

    private static bool VerifyShieldEvidenceHash(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement shieldEvaluationRequirement,
        JsonElement shieldEvaluationRoot)
    {
        var evidenceRequirement = MatrixArtifactManifestWriter.DefaultRequiredArtifacts.Single(
            artifact => string.Equals(artifact.ArtifactKind, "audit_evidence", StringComparison.Ordinal));
        var evidencePath = Path.Combine(artifactRoot, evidenceRequirement.Path.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(evidencePath))
        {
            issues.Add(new MatrixVerifyIssue(
                "shield_evaluation",
                shieldEvaluationRequirement.ArtifactKind,
                shieldEvaluationRequirement.Path,
                "shield_evidence_hash_missing",
                $"evidence_artifact:{evidenceRequirement.Path}",
                null));
            return false;
        }

        try
        {
            if (MatrixArtifactManifestWriter.IsReparsePointOrSymbolicLink(evidencePath))
            {
                issues.Add(new MatrixVerifyIssue(
                    "shield_evaluation",
                    shieldEvaluationRequirement.ArtifactKind,
                    shieldEvaluationRequirement.Path,
                    "shield_evidence_hash_unverified",
                    $"evidence_artifact:{evidenceRequirement.Path}",
                    "evidence_artifact:link_or_reparse_rejected"));
                return false;
            }

            var actualEvidenceSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(evidencePath);
            var consumedEvidenceSha256 = GetString(shieldEvaluationRoot, "consumed_evidence_sha256");
            if (string.IsNullOrWhiteSpace(consumedEvidenceSha256))
            {
                issues.Add(new MatrixVerifyIssue(
                    "shield_evaluation",
                    shieldEvaluationRequirement.ArtifactKind,
                    shieldEvaluationRequirement.Path,
                    "shield_evidence_hash_missing",
                    $"consumed_evidence_sha256:{actualEvidenceSha256}",
                    null));
                return false;
            }

            if (!string.Equals(actualEvidenceSha256, consumedEvidenceSha256, StringComparison.Ordinal))
            {
                issues.Add(new MatrixVerifyIssue(
                    "shield_evaluation",
                    shieldEvaluationRequirement.ArtifactKind,
                    shieldEvaluationRequirement.Path,
                    "shield_evidence_hash_mismatch",
                    $"consumed_evidence_sha256:{actualEvidenceSha256}",
                    $"consumed_evidence_sha256:{consumedEvidenceSha256}"));
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new MatrixVerifyIssue(
                "shield_evaluation",
                shieldEvaluationRequirement.ArtifactKind,
                shieldEvaluationRequirement.Path,
                $"shield_evidence_hash_read_failed:{ex.GetType().Name}",
                $"evidence_artifact:{evidenceRequirement.Path}",
                null));
            return false;
        }
    }
}
