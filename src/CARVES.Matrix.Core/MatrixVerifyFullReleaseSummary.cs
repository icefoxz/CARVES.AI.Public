using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool VerifyFullReleaseProofSummary(
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
        var consistent = VerifySummaryField(
            issues,
            summaryRelativePath,
            "smoke",
            "matrix_proof_lane",
            GetString(root, "smoke"));
        consistent &= VerifyFullReleaseProjectSummary(artifactRoot, issues, summaryRelativePath, manifestVerification, root);
        consistent &= VerifyFullReleasePackagedSummary(artifactRoot, issues, summaryRelativePath, manifestVerification, root);
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

    private static bool VerifyFullReleaseProjectSummary(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        MatrixArtifactManifestVerificationResult manifestVerification,
        JsonElement root)
    {
        var requirement = MatrixArtifactManifestWriter.DefaultRequiredArtifacts.Single(
            artifact => string.Equals(artifact.ArtifactKind, "matrix_summary", StringComparison.Ordinal));
        if (!TryReadVerifiedSummarySourceDocument(
                artifactRoot,
                issues,
                summaryRelativePath,
                requirement,
                manifestVerification,
                "summary_full_release_project",
                out var document,
                out var consistent))
        {
            return false;
        }

        using (document)
        {
            var source = document.RootElement;
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.passed", "true", FormatBool(GetBool(root, "project", "passed")));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.guard_run_id", GetString(source, "guard_run_id"), GetString(root, "project", "guard_run_id"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.shield_status", GetString(source, "shield", "status"), GetString(root, "project", "shield_status"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.shield_standard_label", GetString(source, "shield", "standard_label"), GetString(root, "project", "shield_standard_label"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.lite_score", GetInt(source, "shield", "lite_score")?.ToString(), GetInt(root, "project", "lite_score")?.ToString());
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.consumed_shield_evidence_sha256", GetString(source, "shield", "consumed_evidence_sha256"), GetString(root, "project", "consumed_shield_evidence_sha256"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.proof_role", GetString(source, "matrix", "proof_role"), GetString(root, "project", "proof_role"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.scoring_owner", GetString(source, "matrix", "scoring_owner"), GetString(root, "project", "scoring_owner"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.alters_shield_score", FormatBool(GetBool(source, "matrix", "alters_shield_score")), FormatBool(GetBool(root, "project", "alters_shield_score")));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.consumed_shield_evidence_artifact", GetString(source, "matrix", "consumed_shield_evidence_artifact"), GetString(root, "project", "consumed_shield_evidence_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.shield_evaluation_artifact", GetString(source, "matrix", "shield_evaluation_artifact"), GetString(root, "project", "shield_evaluation_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.shield_badge_json_artifact", GetString(source, "matrix", "shield_badge_json_artifact"), GetString(root, "project", "shield_badge_json_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.shield_badge_svg_artifact", GetString(source, "matrix", "shield_badge_svg_artifact"), GetString(root, "project", "shield_badge_svg_artifact"));
            consistent &= VerifyFullReleaseTrustChainEvidence(issues, summaryRelativePath, "project", root, source, "matrix");
            consistent &= VerifySummaryField(issues, summaryRelativePath, "project.artifact_root", ToPublicArtifactRootMarker(artifactRoot, GetString(source, "artifact_root")), GetString(root, "project", "artifact_root"));
            consistent &= VerifyNativeFullReleaseProducerMarker(
                issues,
                summaryRelativePath,
                root,
                source,
                "project.producer",
                "native_full_release_project");
            return consistent;
        }
    }

    private static bool VerifyFullReleasePackagedSummary(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        MatrixArtifactManifestVerificationResult manifestVerification,
        JsonElement root)
    {
        var requirement = MatrixArtifactManifestWriter.DefaultOptionalArtifacts.Single(
            artifact => string.Equals(artifact.ArtifactKind, "packaged_matrix_summary", StringComparison.Ordinal));
        if (!TryReadVerifiedSummarySourceDocument(
                artifactRoot,
                issues,
                summaryRelativePath,
                requirement,
                manifestVerification,
                "summary_full_release_packaged",
                out var document,
                out var consistent))
        {
            return false;
        }

        using (document)
        {
            var source = document.RootElement;
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.passed", "true", FormatBool(GetBool(root, "packaged", "passed")));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.guard_version", GetString(source, "guard_version"), GetString(root, "packaged", "guard_version"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.handoff_version", GetString(source, "handoff_version"), GetString(root, "packaged", "handoff_version"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.audit_version", GetString(source, "audit_version"), GetString(root, "packaged", "audit_version"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.shield_version", GetString(source, "shield_version"), GetString(root, "packaged", "shield_version"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.matrix_version", GetString(source, "matrix_version"), GetString(root, "packaged", "matrix_version"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.guard_run_id", GetString(source, "matrix", "guard_run_id"), GetString(root, "packaged", "guard_run_id"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.shield_status", GetString(source, "matrix", "shield", "status"), GetString(root, "packaged", "shield_status"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.shield_standard_label", GetString(source, "matrix", "shield", "standard_label"), GetString(root, "packaged", "shield_standard_label"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.lite_score", GetInt(source, "matrix", "shield", "lite_score")?.ToString(), GetInt(root, "packaged", "lite_score")?.ToString());
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.consumed_shield_evidence_sha256", GetString(source, "matrix", "shield", "consumed_evidence_sha256"), GetString(root, "packaged", "consumed_shield_evidence_sha256"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.proof_role", GetString(source, "matrix", "matrix", "proof_role"), GetString(root, "packaged", "proof_role"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.scoring_owner", GetString(source, "matrix", "matrix", "scoring_owner"), GetString(root, "packaged", "scoring_owner"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.alters_shield_score", FormatBool(GetBool(source, "matrix", "matrix", "alters_shield_score")), FormatBool(GetBool(root, "packaged", "alters_shield_score")));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.consumed_shield_evidence_artifact", GetString(source, "matrix", "matrix", "consumed_shield_evidence_artifact"), GetString(root, "packaged", "consumed_shield_evidence_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.shield_evaluation_artifact", GetString(source, "matrix", "matrix", "shield_evaluation_artifact"), GetString(root, "packaged", "shield_evaluation_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.shield_badge_json_artifact", GetString(source, "matrix", "matrix", "shield_badge_json_artifact"), GetString(root, "packaged", "shield_badge_json_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.shield_badge_svg_artifact", GetString(source, "matrix", "matrix", "shield_badge_svg_artifact"), GetString(root, "packaged", "shield_badge_svg_artifact"));
            consistent &= VerifyFullReleaseTrustChainEvidence(issues, summaryRelativePath, "packaged", root, source, "matrix", "matrix");
            consistent &= VerifySummaryField(issues, summaryRelativePath, "packaged.artifact_root", ToPublicArtifactRootMarker(artifactRoot, GetString(source, "artifact_root")), GetString(root, "packaged", "artifact_root"));
            consistent &= VerifyNativeFullReleaseProducerMarker(
                issues,
                summaryRelativePath,
                root,
                source,
                "packaged.producer",
                "native_full_release_packaged");
            return consistent;
        }
    }

    private static bool VerifyNativeFullReleaseProducerMarker(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root,
        JsonElement source,
        string fieldPath,
        string expectedProducer)
    {
        if (!string.Equals(GetString(root, "proof_capabilities", "proof_lane"), MatrixProofSummaryPublicContract.NativeFullReleaseProofLane, StringComparison.Ordinal))
        {
            return true;
        }

        return VerifySummaryField(
            issues,
            summaryRelativePath,
            fieldPath,
            expectedProducer,
            GetString(source, "producer"));
    }

    private static bool VerifyFullReleaseTrustChainEvidence(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        string summarySection,
        JsonElement root,
        JsonElement source,
        params string[] sourceMatrixPath)
    {
        var consistent = true;
        foreach (var field in MatrixProofSummaryPublicContract.Model.ProjectTrustChainHardening.FieldNames)
        {
            consistent &= VerifySummaryField(
                issues,
                summaryRelativePath,
                $"{summarySection}.trust_chain_hardening.{field}",
                GetString(source, ConcatPath(sourceMatrixPath, "trust_chain_hardening", field)),
                GetString(root, summarySection, "trust_chain_hardening", field));
        }

        return consistent;
    }
}
