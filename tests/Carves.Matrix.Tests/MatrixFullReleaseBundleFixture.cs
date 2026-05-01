using Carves.Matrix.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

internal sealed partial class MatrixFullReleaseBundleFixture : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private MatrixFullReleaseBundleFixture(string artifactRoot)
    {
        ArtifactRoot = artifactRoot;
    }

    public string ArtifactRoot { get; }

    public static MatrixFullReleaseBundleFixture Create()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-full-release-" + Guid.NewGuid().ToString("N"));
        var fixture = new MatrixFullReleaseBundleFixture(artifactRoot);
        fixture.WriteArtifact("project/decisions.jsonl", """{"decision":"allow"}""");
        fixture.WriteArtifact("project/handoff.json", """{"schema_version":"carves-continuity-handoff.v1"}""");
        fixture.WriteArtifact("project/shield-evidence.json", """{"schema_version":"shield-evidence.v0"}""");
        var evidenceSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(Path.Combine(artifactRoot, "project", "shield-evidence.json"));
        fixture.WriteArtifact("project/shield-evaluate.json", ValidShieldEvaluationJson(evidenceSha256));
        fixture.WriteArtifact("project/shield-badge.json", """{"schema_version":"shield-badge.v0"}""");
        fixture.WriteArtifact("project/shield-badge.svg", "<svg></svg>");
        fixture.WriteArtifact("project/matrix-summary.json", ProjectMatrixSummaryJson(artifactRoot, evidenceSha256));
        fixture.WriteArtifact("packaged/matrix-packaged-summary.json", PackagedSummaryJson(artifactRoot, evidenceSha256));
        MatrixArtifactManifestWriter.WriteDefaultProofManifest(
            artifactRoot,
            DateTimeOffset.Parse("2026-04-15T00:00:00+00:00"));
        fixture.WriteProofSummary(evidenceSha256);
        return fixture;
    }

    public void MutateProofSummary(Action<JsonObject> mutate)
    {
        var summaryPath = Path.Combine(ArtifactRoot, "matrix-proof-summary.json");
        var root = JsonNode.Parse(File.ReadAllText(summaryPath))!.AsObject();
        mutate(root);
        File.WriteAllText(summaryPath, root.ToJsonString(JsonOptions));
    }

    public void Dispose()
    {
        if (Directory.Exists(ArtifactRoot))
        {
            Directory.Delete(ArtifactRoot, recursive: true);
        }
    }

    private void WriteProofSummary(string evidenceSha256)
    {
        var manifestPath = Path.Combine(ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);
        var summary = new
        {
            schema_version = "matrix-proof-summary.v0",
            smoke = "matrix_proof_lane",
            shell = "carves-matrix",
            proof_mode = "full_release",
            proof_capabilities = FullReleaseProofCapabilities(),
            artifact_root = MatrixArtifactManifestWriter.PortableArtifactRoot,
            artifact_manifest = new
            {
                path = MatrixArtifactManifestWriter.DefaultManifestFileName,
                schema_version = MatrixArtifactManifestWriter.ManifestSchemaVersion,
                sha256 = MatrixArtifactManifestWriter.ComputeFileSha256(manifestPath),
                verification_posture = manifestVerification.VerificationPosture,
                issue_count = manifestVerification.Issues.Count,
            },
            trust_chain_hardening = ValidTrustChainHardening(),
            project = new
            {
                passed = true,
                guard_run_id = "GRD-FULL-1",
                shield_status = "ok",
                shield_standard_label = "CARVES G1.H1.A1 /1d PASS",
                lite_score = 50,
                consumed_shield_evidence_sha256 = evidenceSha256,
                proof_role = "composition_orchestrator",
                scoring_owner = "shield",
                alters_shield_score = false,
                consumed_shield_evidence_artifact = "shield-evidence.json",
                shield_evaluation_artifact = "shield-evaluate.json",
                shield_badge_json_artifact = "shield-badge.json",
                shield_badge_svg_artifact = "shield-badge.svg",
                trust_chain_hardening = TrustChainEvidence(),
                artifact_root = "project",
            },
            packaged = new
            {
                passed = true,
                guard_version = "0.2.0-beta.1",
                handoff_version = "0.1.0-alpha.1",
                audit_version = "0.1.0-alpha.1",
                shield_version = "0.1.0-alpha.1",
                matrix_version = "0.2.0-alpha.1",
                guard_run_id = "GRD-FULL-1",
                shield_status = "ok",
                shield_standard_label = "CARVES G1.H1.A1 /1d PASS",
                lite_score = 50,
                consumed_shield_evidence_sha256 = evidenceSha256,
                proof_role = "composition_orchestrator",
                scoring_owner = "shield",
                alters_shield_score = false,
                consumed_shield_evidence_artifact = "shield-evidence.json",
                shield_evaluation_artifact = "shield-evaluate.json",
                shield_badge_json_artifact = "shield-badge.json",
                shield_badge_svg_artifact = "shield-badge.svg",
                trust_chain_hardening = TrustChainEvidence(),
                artifact_root = "packaged",
            },
            privacy = SummaryPrivacy(),
            public_claims = PublicClaims(),
        };

        File.WriteAllText(Path.Combine(ArtifactRoot, "matrix-proof-summary.json"), JsonSerializer.Serialize(summary, JsonOptions));
    }

    private static string ProjectMatrixSummaryJson(string artifactRoot, string evidenceSha256)
    {
        var summary = new
        {
            schema_version = "matrix-summary.v0",
            smoke = "matrix_e2e",
            tool_mode = "project",
            artifact_root = Path.Combine(artifactRoot, "project"),
            guard_run_id = "GRD-FULL-1",
            matrix = MatrixSummaryCore(),
            shield = ShieldSummary(evidenceSha256),
            privacy = SummaryPrivacy(),
            public_claims = PublicClaims(),
        };
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static string PackagedSummaryJson(string artifactRoot, string evidenceSha256)
    {
        var summary = new
        {
            schema_version = "matrix-packaged-summary.v0",
            smoke = "matrix_packaged_install",
            guard_version = "0.2.0-beta.1",
            handoff_version = "0.1.0-alpha.1",
            audit_version = "0.1.0-alpha.1",
            shield_version = "0.1.0-alpha.1",
            matrix_version = "0.2.0-alpha.1",
            artifact_root = Path.Combine(artifactRoot, "packaged"),
            matrix = new
            {
                guard_run_id = "GRD-FULL-1",
                matrix = MatrixSummaryCore(),
                shield = ShieldSummary(evidenceSha256),
                privacy = SummaryPrivacy(),
                public_claims = PublicClaims(),
            },
            privacy = SummaryPrivacy(),
            public_claims = PublicClaims(),
        };
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static object MatrixSummaryCore()
    {
        return new
        {
            proof_role = "composition_orchestrator",
            scoring_owner = "shield",
            alters_shield_score = false,
            consumed_shield_evidence_artifact = "shield-evidence.json",
            shield_evaluation_artifact = "shield-evaluate.json",
            shield_badge_json_artifact = "shield-badge.json",
            shield_badge_svg_artifact = "shield-badge.svg",
            trust_chain_hardening = TrustChainEvidence(),
        };
    }

    private static object FullReleaseProofCapabilities()
    {
        return new
        {
            proof_lane = "full_release",
            execution_backend = "powershell_release_units",
            coverage = new
            {
                project_mode = true,
                packaged_install = true,
                full_release = true,
            },
            requirements = new
            {
                powershell = true,
                source_checkout = true,
                dotnet_sdk = true,
                git = true,
            },
        };
    }

    private static object ShieldSummary(string evidenceSha256)
    {
        return new
        {
            status = "ok",
            standard_label = "CARVES G1.H1.A1 /1d PASS",
            lite_score = 50,
            lite_band = "disciplined",
            consumed_evidence_sha256 = evidenceSha256,
        };
    }

    private static object TrustChainEvidence()
    {
        return new
        {
            audit_evidence_integrity = "complete_card_796",
            guard_deletion_replacement_honesty = "complete_card_797",
            shield_evidence_contract_alignment = "complete_card_798",
            guard_audit_store_multiprocess_durability = "complete_card_799",
            handoff_completed_state_semantics = "complete_card_800",
            matrix_shield_proof_bridge_claim_boundary = "complete_card_801",
            large_log_streaming_output_boundaries = "complete_card_802",
            handoff_reference_freshness_portability = "complete_card_803",
            usability_coverage_cleanup = "complete_card_804",
            release_checkpoint = "complete_card_805",
            public_rating_claim = "local_self_check_only",
            public_rating_claims_allowed = "limited_to_local_self_check",
        };
    }

    private static object ValidTrustChainHardening()
    {
        return new
        {
            gates_satisfied = true,
            computed_by = "matrix_verifier",
            gates = new object[]
            {
                ValidGate("manifest_integrity", "Manifest hashes, sizes, privacy flags, and artifact file presence verified."),
                ValidGate("required_artifacts", "All required artifact entries are present with expected path, schema, and producer metadata."),
                ValidGate("trial_artifacts", "Agent Trial artifacts are not present; non-trial Matrix compatibility mode applies."),
                ValidGate("shield_score", "Shield evaluation status, certification posture, Standard label, and Lite score fields are verified."),
                ValidGate("summary_consistency", "Matrix proof summary references the current manifest hash, posture, and issue count."),
            },
        };
    }

    private static object ValidGate(string gateId, string reason)
    {
        return new
        {
            gate_id = gateId,
            satisfied = true,
            reason,
            issue_codes = Array.Empty<string>(),
            reason_codes = Array.Empty<string>(),
        };
    }

    private static object SummaryPrivacy()
    {
        return new
        {
            summary_only = true,
            source_upload_required = false,
            raw_diff_upload_required = false,
            prompt_upload_required = false,
            model_response_upload_required = false,
            secrets_required = false,
            hosted_api_required = false,
        };
    }

    private static object PublicClaims()
    {
        return new
        {
            certification = false,
            hosted_verification = false,
            public_leaderboard = false,
            os_sandbox_claim = false,
        };
    }

    private static string ValidShieldEvaluationJson(string consumedEvidenceSha256)
    {
        return $$"""
        {
          "schema_version": "shield-evaluate.v0",
          "status": "ok",
          "certification": false,
          "consumed_evidence_sha256": "{{consumedEvidenceSha256}}",
          "standard": {
            "label": "CARVES G1.H1.A1 /1d PASS"
          },
          "lite": {
            "score": 50,
            "band": "disciplined"
          }
        }
        """;
    }

    private void WriteArtifact(string relativePath, string contents)
    {
        var path = Path.Combine(ArtifactRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
    }
}
