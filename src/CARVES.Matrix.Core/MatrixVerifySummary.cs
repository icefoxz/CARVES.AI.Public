using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static MatrixVerifyProofSummary VerifyProofSummary(
        string artifactRoot,
        string expectedManifestRelativePath,
        string? expectedManifestSha256,
        MatrixArtifactManifestVerificationResult manifestVerification,
        MatrixVerifyRequiredArtifacts requiredArtifacts,
        MatrixVerifyTrialArtifacts trialArtifacts,
        MatrixVerifyShieldEvaluation shieldEvaluation,
        List<MatrixVerifyIssue> issues)
    {
        var summaryRelativePath = "matrix-proof-summary.json";
        var summaryPath = Path.Combine(artifactRoot, summaryRelativePath);
        if (!File.Exists(summaryPath))
        {
            issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, "summary_missing", summaryRelativePath, null));
            return new MatrixVerifyProofSummary(summaryRelativePath, Present: false, Consistent: false);
        }

        try
        {
            var nonSummaryIssues = issues.ToArray();
            using var document = JsonDocument.Parse(File.ReadAllText(summaryPath));
            var root = document.RootElement;
            var consistent = true;
            consistent &= VerifyProofSummaryPublicContract(issues, summaryRelativePath, root);
            consistent &= VerifySummaryField(
                issues,
                summaryRelativePath,
                "schema_version",
                "matrix-proof-summary.v0",
                GetString(root, "schema_version"));
            consistent &= VerifySummaryField(
                issues,
                summaryRelativePath,
                "shell",
                "carves-matrix",
                GetString(root, "shell"));
            consistent &= VerifySummaryField(
                issues,
                summaryRelativePath,
                "artifact_manifest.path",
                expectedManifestRelativePath,
                GetString(root, "artifact_manifest", "path"));
            consistent &= VerifySummaryField(
                issues,
                summaryRelativePath,
                "artifact_manifest.schema_version",
                MatrixArtifactManifestWriter.ManifestSchemaVersion,
                GetString(root, "artifact_manifest", "schema_version"));
            consistent &= VerifySummaryField(
                issues,
                summaryRelativePath,
                "artifact_manifest.sha256",
                expectedManifestSha256,
                GetString(root, "artifact_manifest", "sha256"));
            consistent &= VerifySummaryField(
                issues,
                summaryRelativePath,
                "artifact_manifest.verification_posture",
                manifestVerification.VerificationPosture,
                GetString(root, "artifact_manifest", "verification_posture"));
            consistent &= VerifySummaryField(
                issues,
                summaryRelativePath,
                "artifact_manifest.issue_count",
                manifestVerification.Issues.Count.ToString(),
                GetInt(root, "artifact_manifest", "issue_count")?.ToString());
            consistent &= VerifySummaryField(
                issues,
                summaryRelativePath,
                "artifact_root",
                MatrixArtifactManifestWriter.PortableArtifactRoot,
                GetString(root, "artifact_root"));
            consistent &= VerifySummaryProofCapabilities(issues, summaryRelativePath, root);
            consistent &= VerifySummaryProofMode(
                artifactRoot,
                issues,
                summaryRelativePath,
                root,
                manifestVerification,
                requiredArtifacts,
                trialArtifacts,
                shieldEvaluation,
                nonSummaryIssues);
            consistent &= VerifySummaryPrivacy(issues, summaryRelativePath, root);
            consistent &= VerifySummaryPublicClaims(issues, summaryRelativePath, root);
            return new MatrixVerifyProofSummary(summaryRelativePath, Present: true, consistent);
        }
        catch (JsonException ex)
        {
            issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, $"summary_invalid_json:{ex.GetType().Name}", null, null));
            return new MatrixVerifyProofSummary(summaryRelativePath, Present: true, Consistent: false);
        }
        catch (IOException ex)
        {
            issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, $"summary_read_failed:{ex.GetType().Name}", null, null));
            return new MatrixVerifyProofSummary(summaryRelativePath, Present: true, Consistent: false);
        }
        catch (UnauthorizedAccessException ex)
        {
            issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, $"summary_read_failed:{ex.GetType().Name}", null, null));
            return new MatrixVerifyProofSummary(summaryRelativePath, Present: true, Consistent: false);
        }
    }

    private static bool VerifySummaryProofMode(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root,
        MatrixArtifactManifestVerificationResult manifestVerification,
        MatrixVerifyRequiredArtifacts requiredArtifacts,
        MatrixVerifyTrialArtifacts trialArtifacts,
        MatrixVerifyShieldEvaluation shieldEvaluation,
        IReadOnlyList<MatrixVerifyIssue> nonSummaryIssues)
    {
        var proofMode = GetString(root, "proof_mode");
        if (string.Equals(proofMode, MatrixProofSummaryPublicContract.NativeMinimalProofMode, StringComparison.Ordinal))
        {
            var consistent = VerifySummaryField(
                issues,
                summaryRelativePath,
                "smoke",
                "matrix_native_minimal_proof_lane",
                GetString(root, "smoke"));
            consistent &= VerifyNativeProofSummary(artifactRoot, issues, summaryRelativePath, root, manifestVerification);
            consistent &= VerifySummaryTrustChainHardening(
                issues,
                summaryRelativePath,
                root,
                manifestVerification,
                requiredArtifacts,
                trialArtifacts,
                shieldEvaluation,
                nonSummaryIssues);
            return consistent;
        }

        if (string.Equals(proofMode, MatrixProofSummaryPublicContract.FullReleaseProofMode, StringComparison.Ordinal))
        {
            return VerifyFullReleaseProofSummary(
                artifactRoot,
                issues,
                summaryRelativePath,
                root,
                manifestVerification,
                requiredArtifacts,
                trialArtifacts,
                shieldEvaluation,
                nonSummaryIssues);
        }

        issues.Add(new MatrixVerifyIssue(
            "summary",
            "matrix_proof_summary",
            summaryRelativePath,
            "summary_proof_mode_mismatch",
            $"{MatrixProofSummaryPublicContract.NativeMinimalProofMode}|{MatrixProofSummaryPublicContract.FullReleaseProofMode}",
            proofMode));
        return false;
    }

    private static bool VerifySummaryPrivacy(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root)
    {
        var consistent = true;
        if (GetBool(root, "privacy", "summary_only") != true)
        {
            issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, "privacy_summary_only_not_true", "privacy.summary_only:true", FormatBool(GetBool(root, "privacy", "summary_only"))));
            consistent = false;
        }

        foreach (var field in MatrixProofSummaryPublicContract.Model.PrivacyFalseFieldNames)
        {
            var value = GetBool(root, "privacy", field);
            if (!value.HasValue)
            {
                issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, $"privacy_summary_flag_missing:{field}", $"privacy.{field}:false", null));
                consistent = false;
                continue;
            }

            if (value.Value)
            {
                issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, $"privacy_forbidden_summary_flag_true:{field}", $"privacy.{field}:false", $"privacy.{field}:true"));
                consistent = false;
            }
        }

        return consistent;
    }

    private static bool VerifySummaryPublicClaims(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root)
    {
        var consistent = true;
        foreach (var field in MatrixProofSummaryPublicContract.Model.PublicClaimFalseFieldNames)
        {
            var value = GetBool(root, "public_claims", field);
            if (!value.HasValue)
            {
                issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, $"privacy_public_claim_missing:{field}", $"public_claims.{field}:false", null));
                consistent = false;
                continue;
            }

            if (value.Value)
            {
                issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, $"privacy_forbidden_public_claim_true:{field}", $"public_claims.{field}:false", $"public_claims.{field}:true"));
                consistent = false;
            }
        }

        return consistent;
    }

    private static bool VerifySummaryField(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        string field,
        string? expected,
        string? actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return true;
        }

        issues.Add(new MatrixVerifyIssue(
            "summary",
            "matrix_proof_summary",
            summaryRelativePath,
            $"summary_{field.Replace('.', '_')}_mismatch",
            expected,
            actual));
        return false;
    }

    private static string? FormatBool(bool? value)
    {
        return value.HasValue
            ? value.Value.ToString().ToLowerInvariant()
            : null;
    }
}
