using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackSelectionServiceTests
{
    [Fact]
    public void Assign_PersistsCurrentSelectionForMatchingAdmission()
    {
        using var workspace = new TemporaryWorkspace();
        var (packArtifactPath, attributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x");
        var admissionService = RuntimePackAdmissionServiceTestsHelper.CreateAdmissionService(workspace.RootPath, out var artifactRepository);
        var selectionService = new RuntimePackSelectionService(artifactRepository);

        var admission = admissionService.Admit(packArtifactPath, attributionPath);
        var result = selectionService.Assign("carves.runtime.core", "1.2.3", "stable", "select current local pack");

        Assert.True(admission.Admitted);
        Assert.True(result.Selected);
        var current = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        Assert.NotNull(current);
        Assert.Equal("carves.runtime.core", current!.PackId);
        Assert.StartsWith("packsel-", current.SelectionId, StringComparison.Ordinal);
        Assert.Equal("manual_local_assignment", current.SelectionMode);
        Assert.Equal("select current local pack", current.SelectionReason);
        Assert.Contains("selection remains local-runtime scoped", current.ChecksPassed);
        Assert.Single(artifactRepository.LoadRuntimePackSelectionHistory());
        var audit = artifactRepository.LoadRuntimePackSelectionAuditEntries();
        Assert.Single(audit);
        Assert.Equal("selection_assigned", audit[0].EventKind);
        Assert.Equal(current.SelectionId, audit[0].SelectionId);
    }

    [Fact]
    public void Rollback_RestoresHistoricalSelectionWhenAdmissionMatches()
    {
        using var workspace = new TemporaryWorkspace();
        var admissionService = RuntimePackAdmissionServiceTestsHelper.CreateAdmissionService(workspace.RootPath, out var artifactRepository);
        var selectionService = new RuntimePackSelectionService(artifactRepository);

        var (packOneArtifactPath, packOneAttributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x", "1.2.3", "stable", "pack-one");
        var (packTwoArtifactPath, packTwoAttributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x", "1.2.4", "stable", "pack-two");

        Assert.True(admissionService.Admit(packOneArtifactPath, packOneAttributionPath).Admitted);
        Assert.True(selectionService.Assign("carves.runtime.core", "1.2.3", "stable", "select pack one").Selected);
        var firstSelectionId = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact()!.SelectionId;

        Assert.True(admissionService.Admit(packTwoArtifactPath, packTwoAttributionPath).Admitted);
        Assert.True(selectionService.Assign("carves.runtime.core", "1.2.4", "stable", "select pack two").Selected);

        Assert.True(admissionService.Admit(packOneArtifactPath, packOneAttributionPath).Admitted);
        var rollback = selectionService.Rollback(firstSelectionId, "rollback to pack one");

        Assert.True(rollback.Selected);
        var current = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        Assert.NotNull(current);
        Assert.Equal("1.2.3", current!.PackVersion);
        Assert.Equal("manual_local_rollback", current.SelectionMode);
        Assert.Equal(firstSelectionId, current.RollbackTargetSelectionId);
        Assert.Equal(3, artifactRepository.LoadRuntimePackSelectionHistory().Count);
        var audit = artifactRepository.LoadRuntimePackSelectionAuditEntries();
        Assert.Equal(3, audit.Count);
        Assert.Equal("selection_rolled_back", audit[0].EventKind);
        Assert.Equal(firstSelectionId, audit[0].RollbackTargetSelectionId);
    }

    [Fact]
    public void Rollback_RejectsWhenTargetSelectionIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var admissionService = RuntimePackAdmissionServiceTestsHelper.CreateAdmissionService(workspace.RootPath, out var artifactRepository);
        var selectionService = new RuntimePackSelectionService(artifactRepository);
        var (packArtifactPath, attributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x");

        Assert.True(admissionService.Admit(packArtifactPath, attributionPath).Admitted);
        var result = selectionService.Rollback("packsel-missing", null);

        Assert.False(result.Selected);
        Assert.Contains("runtime_pack_selection_target_missing", result.FailureCodes);
    }

    [Fact]
    public void Rollback_RejectsWhenCurrentAdmissionDoesNotMatchHistoricalSelection()
    {
        using var workspace = new TemporaryWorkspace();
        var admissionService = RuntimePackAdmissionServiceTestsHelper.CreateAdmissionService(workspace.RootPath, out var artifactRepository);
        var selectionService = new RuntimePackSelectionService(artifactRepository);
        var (packOneArtifactPath, packOneAttributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x", "1.2.3", "stable", "pack-one");
        var (packTwoArtifactPath, packTwoAttributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x", "1.2.4", "stable", "pack-two");

        Assert.True(admissionService.Admit(packOneArtifactPath, packOneAttributionPath).Admitted);
        Assert.True(selectionService.Assign("carves.runtime.core", "1.2.3", "stable", "select pack one").Selected);
        var firstSelectionId = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact()!.SelectionId;

        Assert.True(admissionService.Admit(packTwoArtifactPath, packTwoAttributionPath).Admitted);
        Assert.True(selectionService.Assign("carves.runtime.core", "1.2.4", "stable", "select pack two").Selected);

        var rollback = selectionService.Rollback(firstSelectionId, null);

        Assert.False(rollback.Selected);
        Assert.Contains("runtime_pack_selection_target_not_currently_admitted", rollback.FailureCodes);
    }

    [Fact]
    public void Assign_RejectsWhenNoAdmissionExists()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var selectionService = new RuntimePackSelectionService(artifactRepository);

        var result = selectionService.Assign("carves.runtime.core", "1.2.3", "stable", null);

        Assert.False(result.Selected);
        Assert.Contains("runtime_pack_selection_requires_admission", result.FailureCodes);
        Assert.Null(artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact());
    }

    [Fact]
    public void Assign_RejectsWhenRequestedIdentityDoesNotMatchCurrentAdmission()
    {
        using var workspace = new TemporaryWorkspace();
        var (packArtifactPath, attributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x");
        var admissionService = RuntimePackAdmissionServiceTestsHelper.CreateAdmissionService(workspace.RootPath, out var artifactRepository);
        var selectionService = new RuntimePackSelectionService(artifactRepository);

        var admission = admissionService.Admit(packArtifactPath, attributionPath);
        var result = selectionService.Assign("carves.runtime.core", "9.9.9", "stable", null);

        Assert.True(admission.Admitted);
        Assert.False(result.Selected);
        Assert.Contains("pack_version_mismatch", result.FailureCodes);
        Assert.Null(artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact());
    }

    [Fact]
    public void Assign_RejectsWhenActivePinBlocksDivergentAdmission()
    {
        using var workspace = new TemporaryWorkspace();
        var admissionService = RuntimePackAdmissionServiceTestsHelper.CreateAdmissionService(workspace.RootPath, out var artifactRepository);
        var selectionService = new RuntimePackSelectionService(artifactRepository);
        var switchPolicyService = new RuntimePackSwitchPolicyService(artifactRepository);
        var (packOneArtifactPath, packOneAttributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x", "1.2.3", "stable", "pack-one");
        var (packTwoArtifactPath, packTwoAttributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x", "1.2.4", "stable", "pack-two");

        Assert.True(admissionService.Admit(packOneArtifactPath, packOneAttributionPath).Admitted);
        Assert.True(selectionService.Assign("carves.runtime.core", "1.2.3", "stable", "select pack one").Selected);
        Assert.True(switchPolicyService.PinCurrentSelection("pin pack one").Accepted);

        Assert.True(admissionService.Admit(packTwoArtifactPath, packTwoAttributionPath).Admitted);
        var result = selectionService.Assign("carves.runtime.core", "1.2.4", "stable", "attempt pack two");

        Assert.False(result.Selected);
        Assert.Contains("runtime_pack_selection_blocked_by_local_pin", result.FailureCodes);
    }

    [Fact]
    public void Rollback_RejectsWhenActivePinBlocksTargetIdentity()
    {
        using var workspace = new TemporaryWorkspace();
        var admissionService = RuntimePackAdmissionServiceTestsHelper.CreateAdmissionService(workspace.RootPath, out var artifactRepository);
        var selectionService = new RuntimePackSelectionService(artifactRepository);
        var switchPolicyService = new RuntimePackSwitchPolicyService(artifactRepository);
        var (packOneArtifactPath, packOneAttributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x", "1.2.3", "stable", "pack-one");
        var (packTwoArtifactPath, packTwoAttributionPath) = RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(workspace, "0.4.0", "0.4.x", "1.2.4", "stable", "pack-two");

        Assert.True(admissionService.Admit(packOneArtifactPath, packOneAttributionPath).Admitted);
        Assert.True(selectionService.Assign("carves.runtime.core", "1.2.3", "stable", "select pack one").Selected);
        var firstSelectionId = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact()!.SelectionId;

        Assert.True(admissionService.Admit(packTwoArtifactPath, packTwoAttributionPath).Admitted);
        Assert.True(selectionService.Assign("carves.runtime.core", "1.2.4", "stable", "select pack two").Selected);
        Assert.True(switchPolicyService.PinCurrentSelection("pin pack two").Accepted);

        Assert.True(admissionService.Admit(packOneArtifactPath, packOneAttributionPath).Admitted);
        var rollback = selectionService.Rollback(firstSelectionId, "rollback to pack one");

        Assert.False(rollback.Selected);
        Assert.Contains("runtime_pack_rollback_blocked_by_local_pin", rollback.FailureCodes);
    }
}

internal static class RuntimePackAdmissionServiceTestsHelper
{
    public static RuntimePackAdmissionService CreateAdmissionService(string rootPath, out JsonRuntimeArtifactRepository artifactRepository)
    {
        var paths = ControlPlanePaths.FromRepoRoot(rootPath);
        var configRepository = new FileControlPlaneConfigRepository(paths);
        artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var validationService = new SpecificationValidationService(rootPath, paths, configRepository);
        return new RuntimePackAdmissionService(rootPath, configRepository, artifactRepository, validationService);
    }

    public static (string PackArtifactPath, string AttributionPath) WriteValidPackPair(
        TemporaryWorkspace workspace,
        string minVersion,
        string maxVersion,
        string packVersion = "1.2.3",
        string channel = "stable",
        string fileStem = "pack",
        string packType = "runtime_pack",
        bool includeSignature = true,
        bool includeProvenance = true)
    {
        var signatureBlock = includeSignature
            ? """
              "signature": {
                "scheme": "sha256-rsa",
                "keyId": "core-signing-key",
                "digest": "sha256:abc123"
              },
              """
            : string.Empty;
        var provenanceBlock = includeProvenance
            ? """
              "provenance": {
                "publishedAtUtc": "2026-03-31T00:00:00+00:00",
                "publishedBy": "operator@carves",
                "sourcePackLine": "core-stable",
                "sourceGenerationId": "gen-001"
              },
              """
            : string.Empty;
        var packArtifactPath = workspace.WriteFile(
            $"artifacts/{fileStem}-artifact.json",
            $$"""
            {
              "schemaVersion": "1.0",
              "packId": "carves.runtime.core",
              "packVersion": "{{packVersion}}",
              "packType": "{{packType}}",
              "channel": "{{channel}}",
              "runtimeCompatibility": {
                "minVersion": "{{minVersion}}",
                "maxVersion": "{{maxVersion}}"
              },
              "kernelCompatibility": {
                "minVersion": "0.1.0",
                "maxVersion": null
              },
              "executionProfiles": {
                "policyPreset": "core-default",
                "gatePreset": "strict",
                "validatorProfile": "default-validator",
                "environmentProfile": "workspace",
                "routingProfile": "connected-lanes",
                "providerAllowlist": ["codex", "openai"]
              },
              "operatorChecklistRefs": ["docs/checklists/core-release.md"],
              {{signatureBlock}}
              {{provenanceBlock}}
              "releaseNoteRef": "docs/releases/core-{{packVersion}}.md",
              "parentPackVersion": null,
              "approvalRef": "APP-001",
              "supersedes": ["1.2.2"]
            }
            """);

        var attributionPath = workspace.WriteFile(
            $"artifacts/{fileStem}-runtime-pack-attribution.json",
            $$"""
            {
              "schemaVersion": "1.0",
              "packId": "carves.runtime.core",
              "packVersion": "{{packVersion}}",
              "channel": "{{channel}}",
              "artifactRef": ".ai/artifacts/packs/core-{{packVersion}}.json",
              "executionProfiles": {
                "policyPreset": "core-default",
                "gatePreset": "strict",
                "validatorProfile": "default-validator",
                "environmentProfile": "workspace",
                "routingProfile": "connected-lanes"
              },
              "source": {
                "assignmentMode": "overlay_assignment",
                "assignmentRef": "overlay-assignment-001"
              },
              "attributedAtUtc": "2026-03-31T00:00:00+00:00"
            }
            """);

        return (packArtifactPath, attributionPath);
    }
}
