using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackPolicyPreviewServiceTests
{
    [Fact]
    public void Preview_DiffsIncomingPackageWithoutMutatingCurrentTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var configRepository = new FileControlPlaneConfigRepository(workspace.Paths);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var transferService = new RuntimePackPolicyTransferService(workspace.RootPath, artifactRepository, configRepository);
        var previewService = new RuntimePackPolicyPreviewService(workspace.RootPath, artifactRepository, configRepository);
        var admissionPolicyService = new RuntimePackAdmissionPolicyService(configRepository, artifactRepository);
        var switchPolicyService = new RuntimePackSwitchPolicyService(artifactRepository);

        admissionPolicyService.SaveCurrentPolicy(RuntimePackAdmissionPolicyArtifact.CreateDefault("0.4.0") with
        {
            PolicyId = "admission-policy-current",
            AllowedChannels = ["stable", "candidate"],
            Summary = "Current admission policy.",
        });
        switchPolicyService.SaveCurrentPolicy(RuntimePackSwitchPolicyArtifact.CreateDefault() with
        {
            PolicyId = "switch-policy-current",
            Summary = "Current switch policy.",
        });

        var packagePath = Path.Combine(workspace.RootPath, "artifacts", "runtime-pack-policy.json");
        Assert.True(transferService.Export(packagePath).Succeeded);

        var packageNode = JsonNode.Parse(File.ReadAllText(packagePath))!.AsObject();
        var admissionPolicy = packageNode["admission_policy"]!.AsObject();
        admissionPolicy["policy_id"] = "admission-policy-preview";
        admissionPolicy["allowed_channels"] = new JsonArray("stable", "candidate", "preview");
        var switchPolicy = packageNode["switch_policy"]!.AsObject();
        switchPolicy["policy_id"] = "switch-policy-preview";
        switchPolicy["pin_active"] = true;
        switchPolicy["pack_id"] = "carves.runtime.core";
        switchPolicy["pack_version"] = "1.2.5";
        switchPolicy["channel"] = "candidate";
        File.WriteAllText(packagePath, packageNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var result = previewService.Preview(packagePath);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Artifact);
        Assert.Contains(result.Artifact!.Differences, item => item.DiffCode == "admission_policy_id_changed");
        Assert.Contains(result.Artifact.Differences, item => item.DiffCode == "admission_allowed_channels_changed");
        Assert.Contains(result.Artifact.Differences, item => item.DiffCode == "switch_pin_state_changed");
        Assert.Contains(result.Artifact.Differences, item => item.DiffCode == "switch_target_changed");

        var currentAdmission = admissionPolicyService.BuildCurrentPolicy();
        var currentSwitch = switchPolicyService.BuildCurrentPolicy();
        Assert.Equal("admission-policy-current", currentAdmission.PolicyId);
        Assert.DoesNotContain("preview", currentAdmission.AllowedChannels);
        Assert.Equal("switch-policy-current", currentSwitch.PolicyId);
        Assert.False(currentSwitch.PinActive);

        var storedPreview = artifactRepository.TryLoadCurrentRuntimePackPolicyPreviewArtifact();
        Assert.NotNull(storedPreview);
        Assert.Equal("admission-policy-preview", storedPreview!.IncomingAdmissionPolicy.PolicyId);
    }
}
