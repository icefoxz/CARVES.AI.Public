using Carves.Matrix.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixNativeProjectProofProducerTests
{
    [Fact]
    public void NativeProjectProducer_WritesProjectArtifactsAndVerifierConsumableSummary()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();

        var result = MatrixCliRunner.ProduceNativeFullReleaseProjectArtifacts(
            bundle.ArtifactRoot,
            configuration: "Debug");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("passed", result.Status);
        Assert.Empty(result.ReasonCodes);
        foreach (var relativePath in ExpectedProjectArtifacts())
        {
            Assert.True(File.Exists(BundlePath(bundle.ArtifactRoot, relativePath)), $"Missing project artifact: {relativePath}");
        }

        var summaryText = File.ReadAllText(BundlePath(bundle.ArtifactRoot, "project/matrix-summary.json"));
        var wrapperText = File.ReadAllText(BundlePath(bundle.ArtifactRoot, "project-matrix-output.json"));
        Assert.Equal(summaryText, wrapperText);

        using var summaryDocument = JsonDocument.Parse(summaryText);
        var summary = summaryDocument.RootElement;
        Assert.Equal("matrix_e2e", summary.GetProperty("smoke").GetString());
        Assert.Equal("project", summary.GetProperty("tool_mode").GetString());
        Assert.Equal("<redacted-target-repository>", summary.GetProperty("target_repository").GetString());
        Assert.Equal(".", summary.GetProperty("artifact_root").GetString());
        Assert.Equal("allow", summary.GetProperty("guard").GetProperty("decision").GetString());
        Assert.Equal("ready", summary.GetProperty("handoff").GetProperty("readiness").GetString());
        Assert.Equal("shield-evidence.v0", summary.GetProperty("audit").GetProperty("evidence_schema").GetString());
        Assert.Equal("ok", summary.GetProperty("shield").GetProperty("status").GetString());
        Assert.False(summary.GetProperty("public_claims").GetProperty("certification").GetBoolean());

        var evidenceSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(BundlePath(bundle.ArtifactRoot, "project/shield-evidence.json"));
        Assert.Equal(evidenceSha256, summary.GetProperty("shield").GetProperty("consumed_evidence_sha256").GetString());
        using var shieldDocument = JsonDocument.Parse(File.ReadAllText(BundlePath(bundle.ArtifactRoot, "project/shield-evaluate.json")));
        Assert.Equal(evidenceSha256, shieldDocument.RootElement.GetProperty("consumed_evidence_sha256").GetString());

        MatrixArtifactManifestWriter.WriteDefaultProofManifest(
            bundle.ArtifactRoot,
            DateTimeOffset.Parse("2026-04-16T00:00:00+00:00"));
        RefreshProofSummaryForProducedProject(bundle.ArtifactRoot);

        var verify = RunMatrixCli("verify", bundle.ArtifactRoot, "--json");

        Assert.Equal(0, verify.ExitCode);
        using var verifyDocument = JsonDocument.Parse(verify.StandardOutput);
        Assert.Equal("verified", verifyDocument.RootElement.GetProperty("status").GetString());
        Assert.True(verifyDocument.RootElement.GetProperty("summary").GetProperty("consistent").GetBoolean());
    }

    [Fact]
    public void NativeProjectProducer_InvalidConfigurationWritesStableFailureReason()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-project-failure-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = MatrixCliRunner.ProduceNativeFullReleaseProjectArtifacts(
                artifactRoot,
                configuration: "RelWithDebInfo");

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Status);
            Assert.Contains("native_project_configuration_invalid", result.ReasonCodes);

            using var document = JsonDocument.Parse(File.ReadAllText(BundlePath(artifactRoot, "project-matrix-output.json")));
            var root = document.RootElement;
            Assert.Equal("matrix-native-project-proof.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("failed", root.GetProperty("status").GetString());
            Assert.Equal("configuration", root.GetProperty("failed_step").GetString());
            Assert.Contains(
                root.GetProperty("reason_codes").EnumerateArray(),
                code => code.GetString() == "native_project_configuration_invalid");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    private static string[] ExpectedProjectArtifacts()
    {
        return
        [
            "project/guard-init.json",
            "project/guard-check.json",
            "project/decisions.jsonl",
            "project/handoff-draft.json",
            "project/handoff.json",
            "project/handoff-inspect.json",
            "project/audit-summary.json",
            "project/audit-timeline.json",
            "project/audit-explain.json",
            "project/audit-evidence.json",
            "project/shield-evidence.json",
            "project/shield-evaluate.json",
            "project/shield-badge.json",
            "project/shield-badge.svg",
            "project/matrix-summary.json",
            "project-matrix-output.json",
        ];
    }

    private static void RefreshProofSummaryForProducedProject(string artifactRoot)
    {
        var summaryPath = BundlePath(artifactRoot, "matrix-proof-summary.json");
        var root = JsonNode.Parse(File.ReadAllText(summaryPath))!.AsObject();
        using var projectDocument = JsonDocument.Parse(File.ReadAllText(BundlePath(artifactRoot, "project/matrix-summary.json")));
        var project = projectDocument.RootElement;
        var manifestPath = BundlePath(artifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);

        root["artifact_manifest"]!["sha256"] = MatrixArtifactManifestWriter.ComputeFileSha256(manifestPath);
        root["artifact_manifest"]!["verification_posture"] = manifestVerification.VerificationPosture;
        root["artifact_manifest"]!["issue_count"] = manifestVerification.Issues.Count;
        root["project"] = new JsonObject
        {
            ["passed"] = true,
            ["guard_run_id"] = String(project, "guard_run_id"),
            ["shield_status"] = String(project, "shield", "status"),
            ["shield_standard_label"] = String(project, "shield", "standard_label"),
            ["lite_score"] = Int(project, "shield", "lite_score"),
            ["consumed_shield_evidence_sha256"] = String(project, "shield", "consumed_evidence_sha256"),
            ["proof_role"] = String(project, "matrix", "proof_role"),
            ["scoring_owner"] = String(project, "matrix", "scoring_owner"),
            ["alters_shield_score"] = Bool(project, "matrix", "alters_shield_score"),
            ["consumed_shield_evidence_artifact"] = String(project, "matrix", "consumed_shield_evidence_artifact"),
            ["shield_evaluation_artifact"] = String(project, "matrix", "shield_evaluation_artifact"),
            ["shield_badge_json_artifact"] = String(project, "matrix", "shield_badge_json_artifact"),
            ["shield_badge_svg_artifact"] = String(project, "matrix", "shield_badge_svg_artifact"),
            ["trust_chain_hardening"] = JsonNode.Parse(project.GetProperty("matrix").GetProperty("trust_chain_hardening").GetRawText()),
            ["artifact_root"] = ".",
        };

        File.WriteAllText(summaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string BundlePath(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string? String(JsonElement element, params string[] path)
    {
        return TryGet(element, out var value, path) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? Int(JsonElement element, params string[] path)
    {
        return TryGet(element, out var value, path) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private static bool? Bool(JsonElement element, params string[] path)
    {
        return TryGet(element, out var value, path) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
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
}
