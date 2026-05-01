using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static object BuildProofSummary(
        string artifactRoot,
        string manifestRelativePath,
        string manifestSha256,
        MatrixArtifactManifestVerificationResult manifestVerification,
        JsonElement projectRoot,
        JsonElement packagedRoot,
        MatrixVerifyTrustChainHardening trustChainHardening,
        object proofCapabilities)
    {
        return new
        {
            schema_version = "matrix-proof-summary.v0",
            smoke = "matrix_proof_lane",
            shell = "carves-matrix",
            proof_mode = "full_release",
            proof_capabilities = proofCapabilities,
            artifact_root = ToPublicArtifactRootMarker(),
            artifact_manifest = new
            {
                path = manifestRelativePath,
                schema_version = MatrixArtifactManifestWriter.ManifestSchemaVersion,
                sha256 = manifestSha256,
                verification_posture = manifestVerification.VerificationPosture,
                issue_count = manifestVerification.Issues.Count,
            },
            trust_chain_hardening = trustChainHardening,
            project = new
            {
                passed = true,
                guard_run_id = GetString(projectRoot, "guard_run_id"),
                shield_status = GetString(projectRoot, "shield", "status"),
                shield_standard_label = GetString(projectRoot, "shield", "standard_label"),
                lite_score = GetInt(projectRoot, "shield", "lite_score"),
                consumed_shield_evidence_sha256 = GetString(projectRoot, "shield", "consumed_evidence_sha256"),
                proof_role = GetString(projectRoot, "matrix", "proof_role"),
                scoring_owner = GetString(projectRoot, "matrix", "scoring_owner"),
                alters_shield_score = GetBool(projectRoot, "matrix", "alters_shield_score"),
                consumed_shield_evidence_artifact = GetString(projectRoot, "matrix", "consumed_shield_evidence_artifact"),
                shield_evaluation_artifact = GetString(projectRoot, "matrix", "shield_evaluation_artifact"),
                shield_badge_json_artifact = GetString(projectRoot, "matrix", "shield_badge_json_artifact"),
                shield_badge_svg_artifact = GetString(projectRoot, "matrix", "shield_badge_svg_artifact"),
                trust_chain_hardening = BuildTrustChainHardeningEvidence(projectRoot, "matrix"),
                artifact_root = ToPublicArtifactRootMarker(artifactRoot, GetString(projectRoot, "artifact_root")),
            },
            packaged = new
            {
                passed = true,
                guard_version = GetString(packagedRoot, "guard_version"),
                handoff_version = GetString(packagedRoot, "handoff_version"),
                audit_version = GetString(packagedRoot, "audit_version"),
                shield_version = GetString(packagedRoot, "shield_version"),
                matrix_version = GetString(packagedRoot, "matrix_version"),
                guard_run_id = GetString(packagedRoot, "matrix", "guard_run_id"),
                shield_status = GetString(packagedRoot, "matrix", "shield", "status"),
                shield_standard_label = GetString(packagedRoot, "matrix", "shield", "standard_label"),
                lite_score = GetInt(packagedRoot, "matrix", "shield", "lite_score"),
                consumed_shield_evidence_sha256 = GetString(packagedRoot, "matrix", "shield", "consumed_evidence_sha256"),
                proof_role = GetString(packagedRoot, "matrix", "matrix", "proof_role"),
                scoring_owner = GetString(packagedRoot, "matrix", "matrix", "scoring_owner"),
                alters_shield_score = GetBool(packagedRoot, "matrix", "matrix", "alters_shield_score"),
                consumed_shield_evidence_artifact = GetString(packagedRoot, "matrix", "matrix", "consumed_shield_evidence_artifact"),
                shield_evaluation_artifact = GetString(packagedRoot, "matrix", "matrix", "shield_evaluation_artifact"),
                shield_badge_json_artifact = GetString(packagedRoot, "matrix", "matrix", "shield_badge_json_artifact"),
                shield_badge_svg_artifact = GetString(packagedRoot, "matrix", "matrix", "shield_badge_svg_artifact"),
                trust_chain_hardening = BuildTrustChainHardeningEvidence(packagedRoot, "matrix", "matrix"),
                artifact_root = ToPublicArtifactRootMarker(artifactRoot, GetString(packagedRoot, "artifact_root")),
            },
            privacy = new
            {
                summary_only = true,
                source_upload_required = false,
                raw_diff_upload_required = false,
                prompt_upload_required = false,
                model_response_upload_required = false,
                secrets_required = false,
                hosted_api_required = false,
            },
            public_claims = new
            {
                certification = false,
                hosted_verification = false,
                public_leaderboard = false,
                os_sandbox_claim = false,
            },
        };
    }
}
