using Carves.Matrix.Core;
using System.Text.Json;

namespace Carves.Matrix.Tests;

public sealed class MatrixFullReleaseArtifactContractTests
{
    [Fact]
    public void FullReleaseManifestRequirements_FreezeProjectPackagedAndWrapperPaths()
    {
        var required = MatrixArtifactManifestWriter.DefaultRequiredArtifacts
            .Select(FormatRequirement)
            .ToArray();
        var optional = MatrixArtifactManifestWriter.DefaultOptionalArtifacts
            .Select(FormatRequirement)
            .ToArray();

        Assert.Equal(
            [
                "guard_decision|project/decisions.jsonl|guard-decision-jsonl|carves-guard",
                "handoff_packet|project/handoff.json|carves-continuity-handoff.v1|carves-handoff",
                "audit_evidence|project/shield-evidence.json|shield-evidence.v0|carves-audit",
                "shield_evaluation|project/shield-evaluate.json|shield-evaluate.v0|carves-shield",
                "shield_badge_json|project/shield-badge.json|shield-badge.v0|carves-shield",
                "shield_badge_svg|project/shield-badge.svg|shield-badge-svg.v0|carves-shield",
                "matrix_summary|project/matrix-summary.json|matrix-summary.v0|carves-matrix",
            ],
            required);
        Assert.Equal(
            [
                "project_matrix_output|project-matrix-output.json|matrix-script-output.v0|carves-matrix",
                "packaged_matrix_output|packaged-matrix-output.json|matrix-script-output.v0|carves-matrix",
                "packaged_matrix_summary|packaged/matrix-packaged-summary.json|matrix-packaged-summary.v0|carves-matrix",
            ],
            optional);
    }

    [Fact]
    public void FullReleasePowerShellProjectProducer_FreezeSummaryShapeAndRedactionMarkers()
    {
        var script = ReadRepoText("scripts/matrix/matrix-e2e-smoke.ps1");

        Assert.Contains("smoke = \"matrix_e2e\"", script, StringComparison.Ordinal);
        Assert.Contains("target_repository = \"<redacted-target-repository>\"", script, StringComparison.Ordinal);
        Assert.Contains("artifact_root = \".\"", script, StringComparison.Ordinal);
        Assert.Contains("guard_init = \"guard-init.json\"", script, StringComparison.Ordinal);
        Assert.Contains("guard_check = \"guard-check.json\"", script, StringComparison.Ordinal);
        Assert.Contains("guard_decisions = \"decisions.jsonl\"", script, StringComparison.Ordinal);
        Assert.Contains("handoff_packet = \"handoff.json\"", script, StringComparison.Ordinal);
        Assert.Contains("handoff_inspect = \"handoff-inspect.json\"", script, StringComparison.Ordinal);
        Assert.Contains("audit_summary = \"audit-summary.json\"", script, StringComparison.Ordinal);
        Assert.Contains("audit_timeline = \"audit-timeline.json\"", script, StringComparison.Ordinal);
        Assert.Contains("audit_explain = \"audit-explain.json\"", script, StringComparison.Ordinal);
        Assert.Contains("shield_evidence = \"shield-evidence.json\"", script, StringComparison.Ordinal);
        Assert.Contains("shield_evaluate = \"shield-evaluate.json\"", script, StringComparison.Ordinal);
        Assert.Contains("shield_badge_json = \"shield-badge.json\"", script, StringComparison.Ordinal);
        Assert.Contains("shield_badge_svg = \"shield-badge.svg\"", script, StringComparison.Ordinal);
        Assert.Contains("proof_role = \"composition_orchestrator\"", script, StringComparison.Ordinal);
        Assert.Contains("scoring_owner = \"shield\"", script, StringComparison.Ordinal);
        Assert.Contains("alters_shield_score = $false", script, StringComparison.Ordinal);
        Assert.Contains("public_rating_claim = \"local_self_check_only\"", script, StringComparison.Ordinal);
        Assert.Contains("public_rating_claims_allowed = \"limited_to_local_self_check\"", script, StringComparison.Ordinal);
        Assert.Contains("certification = $false", script, StringComparison.Ordinal);
        Assert.Contains("hosted_verification = $false", script, StringComparison.Ordinal);
        Assert.Contains("os_sandbox_claim = $false", script, StringComparison.Ordinal);
        Assert.Contains("$summaryJson | Set-Content -Path (Join-Path $ArtifactRoot \"matrix-summary.json\")", script, StringComparison.Ordinal);
    }

    [Fact]
    public void FullReleasePowerShellPackagedProducer_FreezeSummaryShapeAndRedactionMarkers()
    {
        var script = ReadRepoText("scripts/matrix/matrix-packaged-install-smoke.ps1");

        Assert.Contains("smoke = \"matrix_packaged_install\"", script, StringComparison.Ordinal);
        Assert.Contains("package_root = \"<redacted-local-package-root>\"", script, StringComparison.Ordinal);
        Assert.Contains("tool_root = \"<redacted-local-tool-root>\"", script, StringComparison.Ordinal);
        Assert.Contains("artifact_root = \".\"", script, StringComparison.Ordinal);
        Assert.Contains("remote_registry_published = $false", script, StringComparison.Ordinal);
        Assert.Contains("nuget_org_push_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("carves_guard = \"carves-guard\"", script, StringComparison.Ordinal);
        Assert.Contains("carves_handoff = \"carves-handoff\"", script, StringComparison.Ordinal);
        Assert.Contains("carves_audit = \"carves-audit\"", script, StringComparison.Ordinal);
        Assert.Contains("carves_shield = \"carves-shield\"", script, StringComparison.Ordinal);
        Assert.Contains("carves_matrix = \"carves-matrix\"", script, StringComparison.Ordinal);
        Assert.Contains("guard = \"CARVES.Guard.Cli.$GuardVersion.nupkg\"", script, StringComparison.Ordinal);
        Assert.Contains("handoff = \"CARVES.Handoff.Cli.$HandoffVersion.nupkg\"", script, StringComparison.Ordinal);
        Assert.Contains("audit = \"CARVES.Audit.Cli.$AuditVersion.nupkg\"", script, StringComparison.Ordinal);
        Assert.Contains("shield = \"CARVES.Shield.Cli.$ShieldVersion.nupkg\"", script, StringComparison.Ordinal);
        Assert.Contains("matrix = \"CARVES.Matrix.Cli.$MatrixVersion.nupkg\"", script, StringComparison.Ordinal);
        Assert.Contains("matrix = $matrixJson", script, StringComparison.Ordinal);
        Assert.Contains("pack_command_count = $packResults.Count", script, StringComparison.Ordinal);
        Assert.Contains("install_command_count = $installResults.Count", script, StringComparison.Ordinal);
        Assert.Contains("$summaryJson | Set-Content -Path (Join-Path $ArtifactRoot \"matrix-packaged-summary.json\")", script, StringComparison.Ordinal);
    }

    [Fact]
    public void FullReleaseProofSummaryFixture_FreezeVerifierConsumedPublicReadback()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundle.ArtifactRoot, "matrix-proof-summary.json")));
        var root = document.RootElement;
        var capabilities = root.GetProperty("proof_capabilities");

        Assert.Equal("matrix-proof-summary.v0", root.GetProperty("schema_version").GetString());
        Assert.Equal("matrix_proof_lane", root.GetProperty("smoke").GetString());
        Assert.Equal("full_release", root.GetProperty("proof_mode").GetString());
        Assert.Equal(MatrixArtifactManifestWriter.PortableArtifactRoot, root.GetProperty("artifact_root").GetString());
        Assert.Equal("full_release", capabilities.GetProperty("proof_lane").GetString());
        Assert.Equal("powershell_release_units", capabilities.GetProperty("execution_backend").GetString());
        Assert.True(capabilities.GetProperty("coverage").GetProperty("project_mode").GetBoolean());
        Assert.True(capabilities.GetProperty("coverage").GetProperty("packaged_install").GetBoolean());
        Assert.True(capabilities.GetProperty("coverage").GetProperty("full_release").GetBoolean());
        Assert.True(capabilities.GetProperty("requirements").GetProperty("powershell").GetBoolean());
        Assert.True(capabilities.GetProperty("requirements").GetProperty("source_checkout").GetBoolean());
        Assert.True(capabilities.GetProperty("requirements").GetProperty("dotnet_sdk").GetBoolean());
        Assert.True(capabilities.GetProperty("requirements").GetProperty("git").GetBoolean());

        AssertFieldSetEquals(MatrixProofSummaryPublicContract.Model.Project.FieldNames, FieldNames(root.GetProperty("project")));
        AssertFieldSetEquals(MatrixProofSummaryPublicContract.Model.Packaged.FieldNames, FieldNames(root.GetProperty("packaged")));
        AssertFieldSetEquals(
            MatrixProofSummaryPublicContract.Model.ProjectTrustChainHardening.FieldNames,
            FieldNames(root.GetProperty("project").GetProperty("trust_chain_hardening")));
        AssertFieldSetEquals(
            MatrixProofSummaryPublicContract.Model.PackagedTrustChainHardening.FieldNames,
            FieldNames(root.GetProperty("packaged").GetProperty("trust_chain_hardening")));
        Assert.Equal("project", root.GetProperty("project").GetProperty("artifact_root").GetString());
        Assert.Equal("packaged", root.GetProperty("packaged").GetProperty("artifact_root").GetString());
    }

    [Fact]
    public void FullReleaseArtifactContractDocument_NamesPublicVerifierAndCompatibilityFields()
    {
        var doc = ReadRepoText("docs/matrix/full-release-artifact-contract.md");

        Assert.Contains("Status: current PowerShell compatibility contract.", doc, StringComparison.Ordinal);
        Assert.Contains("`project-matrix-output.json`", doc, StringComparison.Ordinal);
        Assert.Contains("`packaged-matrix-output.json`", doc, StringComparison.Ordinal);
        Assert.Contains("Verifier reads the manifest-bound byte snapshot", doc, StringComparison.Ordinal);
        Assert.Contains("Compatibility wrapper output", doc, StringComparison.Ordinal);
        Assert.Contains("`package_root` is `<redacted-local-package-root>`.", doc, StringComparison.Ordinal);
        Assert.Contains("`tool_root` is `<redacted-local-tool-root>`.", doc, StringComparison.Ordinal);
        Assert.Contains("Existing `proof --lane full-release` remains PowerShell compatibility", doc, StringComparison.Ordinal);
    }

    private static string FormatRequirement(MatrixArtifactManifestRequirement requirement)
    {
        return string.Join(
            "|",
            requirement.ArtifactKind,
            requirement.Path,
            requirement.SchemaVersion,
            requirement.Producer);
    }

    private static string[] FieldNames(JsonElement element)
    {
        return element.EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
    }

    private static void AssertFieldSetEquals(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        Assert.Equal(
            expected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            actual.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static string ReadRepoText(string relativePath)
    {
        return File.ReadAllText(Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
