using Carves.Matrix.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

internal sealed partial class MatrixBundleFixture
{
    public void MutateProofSummary(Action<JsonObject> mutate)
    {
        var root = ReadProofSummaryObject();
        mutate(root);
        WriteProofSummaryObject(root);
    }

    public void WriteProofSummary()
    {
        var manifestPath = Path.Combine(ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);
        using var matrixSummaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(ArtifactRoot, "project", "matrix-summary.json")));
        var matrixSummaryRoot = matrixSummaryDocument.RootElement;
        var summary = new
        {
            schema_version = "matrix-proof-summary.v0",
            smoke = "matrix_native_minimal_proof_lane",
            shell = "carves-matrix",
            proof_mode = "native_minimal",
            proof_capabilities = NativeProofCapabilities(),
            artifact_root = MatrixArtifactManifestWriter.PortableArtifactRoot,
            artifact_manifest = new
            {
                path = MatrixArtifactManifestWriter.DefaultManifestFileName,
                schema_version = MatrixArtifactManifestWriter.ManifestSchemaVersion,
                sha256 = MatrixArtifactManifestWriter.ComputeFileSha256(manifestPath),
                verification_posture = manifestVerification.VerificationPosture,
                issue_count = manifestVerification.Issues.Count,
            },
            trust_chain_hardening = ValidTrustChainHardening(CurrentTrialTrustChainGateReason()),
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
                artifact_root = GetString(matrixSummaryRoot, "artifact_root"),
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

        File.WriteAllText(
            Path.Combine(ArtifactRoot, "matrix-proof-summary.json"),
            JsonSerializer.Serialize(summary, JsonOptions));
    }

    private static string MatrixSummaryJson(string artifactRoot)
    {
        var evidenceSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(Path.Combine(artifactRoot, "project", "shield-evidence.json"));
        var summary = new
        {
            schema_version = "matrix-summary.v0",
            proof_role = "composition_orchestrator",
            proof_mode = "native_minimal",
            scoring_owner = "shield",
            alters_shield_score = false,
            artifact_root = MatrixArtifactManifestWriter.PortableArtifactRoot,
            shield = new
            {
                status = "ok",
                standard_label = "CARVES G1.H1.A1 /1d PASS",
                lite_score = 50,
                consumed_evidence_sha256 = evidenceSha256,
            },
        };
        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private string CurrentTrialTrustChainGateReason()
    {
        var manifestPath = Path.Combine(ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        var looseFileCount = MatrixArtifactManifestWriter.TrialArtifacts.Count(requirement =>
            File.Exists(Path.Combine(ArtifactRoot, requirement.Path.Replace('/', Path.DirectorySeparatorChar))));

        var trialClaimed = false;
        if (File.Exists(manifestPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (document.RootElement.TryGetProperty("artifacts", out var artifacts)
                && artifacts.ValueKind == JsonValueKind.Array)
            {
                var trialKinds = MatrixArtifactManifestWriter.TrialArtifacts
                    .Select(requirement => requirement.ArtifactKind)
                    .ToHashSet(StringComparer.Ordinal);
                trialClaimed = artifacts.EnumerateArray().Any(entry =>
                    entry.TryGetProperty("artifact_kind", out var kind)
                    && kind.ValueKind == JsonValueKind.String
                    && trialKinds.Contains(kind.GetString() ?? string.Empty));
            }
        }

        if (trialClaimed)
        {
            return "Agent Trial artifacts are manifest-covered and verified.";
        }

        return looseFileCount > 0
            ? $"Loose Agent Trial files were detected outside manifest coverage ({looseFileCount}); ordinary verify treats them as unclaimed compatibility readback."
            : "Agent Trial artifacts are not present; non-trial Matrix compatibility mode applies.";
    }

    private static object ValidTrustChainHardening(string trialArtifactsReason)
    {
        return new
        {
            gates_satisfied = true,
            computed_by = "matrix_verifier",
            gates = new object[]
            {
                ValidGate("manifest_integrity", "Manifest hashes, sizes, privacy flags, and artifact file presence verified."),
                ValidGate("required_artifacts", "All required artifact entries are present with expected path, schema, and producer metadata."),
                ValidGate("trial_artifacts", trialArtifactsReason),
                ValidGate("shield_score", "Shield evaluation status, certification posture, Standard label, and Lite score fields are verified."),
                ValidGate("summary_consistency", "Matrix proof summary references the current manifest hash, posture, and issue count."),
            },
        };
    }

    private static object NativeProofCapabilities()
    {
        return new
        {
            proof_lane = "native_minimal",
            execution_backend = "dotnet_runner_chain",
            coverage = new
            {
                project_mode = true,
                packaged_install = false,
                full_release = false,
            },
            requirements = new
            {
                powershell = false,
                source_checkout = false,
                dotnet_sdk = true,
                git = true,
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

    private static string? GetString(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path) && current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path)
               && current.ValueKind == JsonValueKind.Number
               && current.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool? GetBool(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path) && current.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? current.GetBoolean()
            : null;
    }

    private static bool TryGet(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private JsonObject ReadProofSummaryObject()
    {
        var summaryPath = Path.Combine(ArtifactRoot, "matrix-proof-summary.json");
        return JsonNode.Parse(File.ReadAllText(summaryPath))!.AsObject();
    }

    private void WriteProofSummaryObject(JsonObject root)
    {
        var summaryPath = Path.Combine(ArtifactRoot, "matrix-proof-summary.json");
        File.WriteAllText(summaryPath, root.ToJsonString(JsonOptions));
    }
}
