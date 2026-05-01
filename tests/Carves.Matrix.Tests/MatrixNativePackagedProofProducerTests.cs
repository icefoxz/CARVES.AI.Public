using Carves.Matrix.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixNativePackagedProofProducerTests
{
    [Fact]
    public void NativePackagedProducer_WritesRedactedPackagedSummaryAndVerifierConsumableBundle()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var workRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-packaged-producer-" + Guid.NewGuid().ToString("N"));
        const string version = "0.1.0-card887.1";

        try
        {
            var result = MatrixCliRunner.ProduceNativeFullReleasePackagedArtifacts(
                bundle.ArtifactRoot,
                runtimeRoot: repoRoot,
                workRoot: workRoot,
                configuration: "Debug",
                version: version);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("passed", result.Status);
            Assert.Empty(result.ReasonCodes);
            Assert.Equal("packaged-matrix-output.json", result.PackagedMatrixOutputPath);

            var summaryPath = BundlePath(bundle.ArtifactRoot, "packaged/matrix-packaged-summary.json");
            var wrapperPath = BundlePath(bundle.ArtifactRoot, "packaged-matrix-output.json");
            var matrixOutputPath = BundlePath(bundle.ArtifactRoot, "packaged/matrix-e2e-output.json");
            Assert.True(File.Exists(summaryPath));
            Assert.True(File.Exists(wrapperPath));
            Assert.True(File.Exists(matrixOutputPath));
            Assert.False(File.Exists(matrixOutputPath + ".stderr.txt"));
            Assert.Equal(File.ReadAllText(summaryPath), File.ReadAllText(wrapperPath));

            using var summaryDocument = JsonDocument.Parse(File.ReadAllText(summaryPath));
            var summary = summaryDocument.RootElement;
            Assert.Equal("matrix_packaged_install", summary.GetProperty("smoke").GetString());
            Assert.Equal("<redacted-local-package-root>", summary.GetProperty("package_root").GetString());
            Assert.Equal("<redacted-local-tool-root>", summary.GetProperty("tool_root").GetString());
            Assert.Equal(".", summary.GetProperty("artifact_root").GetString());
            Assert.False(summary.GetProperty("remote_registry_published").GetBoolean());
            Assert.False(summary.GetProperty("nuget_org_push_required").GetBoolean());
            Assert.Equal(5, summary.GetProperty("pack_command_count").GetInt32());
            Assert.Equal(5, summary.GetProperty("install_command_count").GetInt32());
            Assert.Equal("carves-matrix", summary.GetProperty("installed_commands").GetProperty("carves_matrix").GetString());
            Assert.Equal($"CARVES.Matrix.Cli.{version}.nupkg", summary.GetProperty("packages").GetProperty("matrix").GetString());
            Assert.Equal("installed", summary.GetProperty("matrix").GetProperty("tool_mode").GetString());
            Assert.Equal("<redacted-target-repository>", summary.GetProperty("matrix").GetProperty("target_repository").GetString());
            Assert.Equal(".", summary.GetProperty("matrix").GetProperty("artifact_root").GetString());

            var summaryText = File.ReadAllText(summaryPath).Replace('\\', '/');
            Assert.DoesNotContain(workRoot.Replace('\\', '/'), summaryText, StringComparison.Ordinal);
            Assert.DoesNotContain("tools/", summaryText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("packages/", summaryText, StringComparison.OrdinalIgnoreCase);

            MatrixArtifactManifestWriter.WriteDefaultProofManifest(
                bundle.ArtifactRoot,
                DateTimeOffset.Parse("2026-04-16T00:00:00+00:00"));
            RefreshProofSummaryForProducedPackaged(bundle.ArtifactRoot);

            var verify = RunMatrixCli("verify", bundle.ArtifactRoot, "--json");

            Assert.Equal(0, verify.ExitCode);
            using var verifyDocument = JsonDocument.Parse(verify.StandardOutput);
            Assert.Equal("verified", verifyDocument.RootElement.GetProperty("status").GetString());
            Assert.True(verifyDocument.RootElement.GetProperty("summary").GetProperty("consistent").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(workRoot))
            {
                Directory.Delete(workRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void NativePackagedProducer_InvalidConfigurationWritesFailureWithoutPackagedSummary()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-packaged-failure-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = MatrixCliRunner.ProduceNativeFullReleasePackagedArtifacts(
                artifactRoot,
                runtimeRoot: MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
                configuration: "RelWithDebInfo");

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Status);
            Assert.Contains("native_packaged_configuration_invalid", result.ReasonCodes);
            Assert.False(File.Exists(BundlePath(artifactRoot, "packaged/matrix-packaged-summary.json")));

            using var document = JsonDocument.Parse(File.ReadAllText(BundlePath(artifactRoot, "packaged-matrix-output.json")));
            var root = document.RootElement;
            Assert.Equal("matrix-native-packaged-proof.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("failed", root.GetProperty("status").GetString());
            Assert.Equal("configuration", root.GetProperty("failed_step").GetString());
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    private static void RefreshProofSummaryForProducedPackaged(string artifactRoot)
    {
        var summaryPath = BundlePath(artifactRoot, "matrix-proof-summary.json");
        var root = JsonNode.Parse(File.ReadAllText(summaryPath))!.AsObject();
        using var packagedDocument = JsonDocument.Parse(File.ReadAllText(BundlePath(artifactRoot, "packaged/matrix-packaged-summary.json")));
        var packaged = packagedDocument.RootElement;
        var manifestPath = BundlePath(artifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);

        root["artifact_manifest"]!["sha256"] = MatrixArtifactManifestWriter.ComputeFileSha256(manifestPath);
        root["artifact_manifest"]!["verification_posture"] = manifestVerification.VerificationPosture;
        root["artifact_manifest"]!["issue_count"] = manifestVerification.Issues.Count;
        root["packaged"] = new JsonObject
        {
            ["passed"] = true,
            ["guard_version"] = String(packaged, "guard_version"),
            ["handoff_version"] = String(packaged, "handoff_version"),
            ["audit_version"] = String(packaged, "audit_version"),
            ["shield_version"] = String(packaged, "shield_version"),
            ["matrix_version"] = String(packaged, "matrix_version"),
            ["guard_run_id"] = String(packaged, "matrix", "guard_run_id"),
            ["shield_status"] = String(packaged, "matrix", "shield", "status"),
            ["shield_standard_label"] = String(packaged, "matrix", "shield", "standard_label"),
            ["lite_score"] = Int(packaged, "matrix", "shield", "lite_score"),
            ["consumed_shield_evidence_sha256"] = String(packaged, "matrix", "shield", "consumed_evidence_sha256"),
            ["proof_role"] = String(packaged, "matrix", "matrix", "proof_role"),
            ["scoring_owner"] = String(packaged, "matrix", "matrix", "scoring_owner"),
            ["alters_shield_score"] = Bool(packaged, "matrix", "matrix", "alters_shield_score"),
            ["consumed_shield_evidence_artifact"] = String(packaged, "matrix", "matrix", "consumed_shield_evidence_artifact"),
            ["shield_evaluation_artifact"] = String(packaged, "matrix", "matrix", "shield_evaluation_artifact"),
            ["shield_badge_json_artifact"] = String(packaged, "matrix", "matrix", "shield_badge_json_artifact"),
            ["shield_badge_svg_artifact"] = String(packaged, "matrix", "matrix", "shield_badge_svg_artifact"),
            ["trust_chain_hardening"] = JsonNode.Parse(packaged.GetProperty("matrix").GetProperty("matrix").GetProperty("trust_chain_hardening").GetRawText()),
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
