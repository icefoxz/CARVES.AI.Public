using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static object BuildNativeMatrixSummary(string artifactRoot, string shieldEvaluationJson)
    {
        using var shieldDocument = JsonDocument.Parse(shieldEvaluationJson);
        var shieldRoot = shieldDocument.RootElement;
        return new
        {
            schema_version = "matrix-summary.v0",
            proof_role = "composition_orchestrator",
            proof_mode = "native_minimal",
            scoring_owner = "shield",
            alters_shield_score = false,
            artifact_root = ToPublicArtifactRootMarker(),
            shield = new
            {
                status = GetString(shieldRoot, "status"),
                standard_label = GetString(shieldRoot, "standard", "label"),
                lite_score = GetInt(shieldRoot, "lite", "score"),
                consumed_evidence_sha256 = GetString(shieldRoot, "consumed_evidence_sha256"),
            },
            artifacts = BuildNativeProofArtifactIndex(),
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

    private static object BuildNativeProofSummary(
        string artifactRoot,
        string manifestRelativePath,
        string manifestSha256,
        MatrixArtifactManifestVerificationResult manifestVerification,
        JsonElement matrixSummaryRoot,
        MatrixVerifyTrustChainHardening trustChainHardening)
    {
        return new
        {
            schema_version = "matrix-proof-summary.v0",
            smoke = "matrix_native_minimal_proof_lane",
            shell = "carves-matrix",
            proof_mode = "native_minimal",
            proof_capabilities = BuildNativeMinimalProofCapabilities(),
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
            native = new
            {
                passed = true,
                proof_role = GetString(matrixSummaryRoot, "proof_role"),
                scoring_owner = GetString(matrixSummaryRoot, "scoring_owner"),
                alters_shield_score = GetBool(matrixSummaryRoot, "alters_shield_score"),
                shield_status = GetString(matrixSummaryRoot, "shield", "status"),
                shield_standard_label = GetString(matrixSummaryRoot, "shield", "standard_label"),
                lite_score = GetInt(matrixSummaryRoot, "shield", "lite_score"),
                consumed_shield_evidence_sha256 = GetString(matrixSummaryRoot, "shield", "consumed_evidence_sha256"),
                guard_decision_artifact = "project/decisions.jsonl",
                handoff_packet_artifact = "project/handoff.json",
                consumed_shield_evidence_artifact = "project/shield-evidence.json",
                shield_evaluation_artifact = "project/shield-evaluate.json",
                shield_badge_json_artifact = "project/shield-badge.json",
                shield_badge_svg_artifact = "project/shield-badge.svg",
                matrix_summary_artifact = "project/matrix-summary.json",
                artifact_root = ToPublicArtifactRootMarker(artifactRoot, GetString(matrixSummaryRoot, "artifact_root")),
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
