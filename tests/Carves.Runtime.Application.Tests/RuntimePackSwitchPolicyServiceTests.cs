using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackSwitchPolicyServiceTests
{
    [Fact]
    public void PinCurrentSelection_PersistsPinnedPolicyFromCurrentSelection()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackSelectionArtifact(RuntimePackSelectionTestData.CreateSelection("packsel-001", "1.2.3"));
        var service = new RuntimePackSwitchPolicyService(artifactRepository);

        var result = service.PinCurrentSelection("pin current");

        Assert.True(result.Accepted);
        var artifact = artifactRepository.TryLoadCurrentRuntimePackSwitchPolicyArtifact();
        Assert.NotNull(artifact);
        Assert.True(artifact!.PinActive);
        Assert.Equal("packsel-001", artifact.PinnedSelectionId);
        Assert.Equal("1.2.3", artifact.PackVersion);
        Assert.Equal("pin current", artifact.Reason);
        var audit = artifactRepository.LoadRuntimePackPolicyAuditEntries();
        Assert.Single(audit);
        Assert.Equal("pin_current_selection", audit[0].EventKind);
        Assert.Equal(artifact.PolicyId, audit[0].ResultingSwitchPolicyId);
    }

    [Fact]
    public void ClearPin_PersistsUnpinnedPolicy()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackSelectionArtifact(RuntimePackSelectionTestData.CreateSelection("packsel-001", "1.2.3"));
        var service = new RuntimePackSwitchPolicyService(artifactRepository);
        Assert.True(service.PinCurrentSelection(null).Accepted);

        var result = service.ClearPin("clear current pin");

        Assert.True(result.Accepted);
        var artifact = artifactRepository.TryLoadCurrentRuntimePackSwitchPolicyArtifact();
        Assert.NotNull(artifact);
        Assert.False(artifact!.PinActive);
        Assert.Equal("clear current pin", artifact.Reason);
        var audit = artifactRepository.LoadRuntimePackPolicyAuditEntries();
        Assert.Equal(2, audit.Count);
        Assert.Equal("clear_pack_pin", audit[0].EventKind);
        Assert.Equal(artifact.PolicyId, audit[0].ResultingSwitchPolicyId);
    }

    [Fact]
    public void PinCurrentSelection_RejectsWhenNoCurrentSelectionExists()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var service = new RuntimePackSwitchPolicyService(artifactRepository);

        var result = service.PinCurrentSelection(null);

        Assert.False(result.Accepted);
        Assert.Contains("runtime_pack_pin_requires_current_selection", result.FailureCodes);
    }
}

internal static class RuntimePackSelectionTestData
{
    public static RuntimePackSelectionArtifact CreateSelection(string selectionId, string packVersion, string channel = "stable")
    {
        return new RuntimePackSelectionArtifact
        {
            SelectionId = selectionId,
            PackId = "carves.runtime.core",
            PackVersion = packVersion,
            Channel = channel,
            RuntimeStandardVersion = "0.4.0",
            PackArtifactPath = $".ai/artifacts/packs/core-{packVersion}.json",
            RuntimePackAttributionPath = $".ai/artifacts/packs/core-{packVersion}.attribution.json",
            ArtifactRef = $".ai/artifacts/packs/core-{packVersion}.json",
            ExecutionProfiles = new RuntimePackAdmissionProfileSelection
            {
                PolicyPreset = "core-default",
                GatePreset = "strict",
                ValidatorProfile = "default-validator",
                EnvironmentProfile = "workspace",
                RoutingProfile = "connected-lanes",
            },
            AdmissionSource = new RuntimePackAdmissionSource
            {
                AssignmentMode = "overlay_assignment",
                AssignmentRef = $"selection-{selectionId}",
            },
            AdmissionCapturedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            SelectionMode = "manual_local_assignment",
            SelectionReason = "Synthetic selection fixture.",
            Summary = $"Selected carves.runtime.core@{packVersion} ({channel}).",
            ChecksPassed =
            [
                "selection remains local-runtime scoped",
                "selection is derived from admitted current evidence"
            ],
        };
    }
}
