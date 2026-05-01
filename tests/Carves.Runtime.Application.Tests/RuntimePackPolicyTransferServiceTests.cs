using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackPolicyTransferServiceTests
{
    [Fact]
    public void ExportAndImport_RoundTripsCurrentLocalPolicyTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var configRepository = new FileControlPlaneConfigRepository(paths);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var transferService = new RuntimePackPolicyTransferService(workspace.RootPath, artifactRepository, configRepository);
        var admissionPolicyService = new RuntimePackAdmissionPolicyService(configRepository, artifactRepository);
        var switchPolicyService = new RuntimePackSwitchPolicyService(artifactRepository);

        admissionPolicyService.SaveCurrentPolicy(RuntimePackAdmissionPolicyArtifact.CreateDefault("0.4.0") with
        {
            PolicyId = "admission-policy-exported",
            AllowedChannels = ["stable", "candidate", "preview"],
            Summary = "Exportable local admission policy.",
        });

        switchPolicyService.SaveCurrentPolicy(RuntimePackSwitchPolicyArtifact.CreateDefault() with
        {
            PolicyId = "switch-policy-exported",
            PolicyMode = "local_pin_current_selection",
            PinActive = true,
            PackId = "carves.runtime.core",
            PackVersion = "1.2.3",
            Channel = "stable",
            Summary = "Pinned current runtime pack.",
        });

        var outputPath = Path.Combine(workspace.RootPath, "artifacts", "runtime-pack-policy.json");
        var exportResult = transferService.Export(outputPath);
        Assert.True(exportResult.Succeeded);
        Assert.True(File.Exists(outputPath));

        var packageNode = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        packageNode["package_id"] = "packpolpkg-imported";
        File.WriteAllText(outputPath, packageNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var importResult = transferService.Import(outputPath);
        Assert.True(importResult.Succeeded);

        var currentAdmission = admissionPolicyService.BuildCurrentPolicy();
        var currentSwitch = switchPolicyService.BuildCurrentPolicy();
        Assert.Equal("admission-policy-exported", currentAdmission.PolicyId);
        Assert.Contains("preview", currentAdmission.AllowedChannels);
        Assert.Equal("switch-policy-exported", currentSwitch.PolicyId);
        Assert.True(currentSwitch.PinActive);

        var audit = artifactRepository.LoadRuntimePackPolicyAuditEntries();
        Assert.Equal(2, audit.Count);
        Assert.Equal("policy_imported", audit[0].EventKind);
        Assert.Equal("policy_exported", audit[1].EventKind);
    }

    [Fact]
    public void Import_RejectsWhenPackageViolatesRuntimeLocalChecks()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var configRepository = new FileControlPlaneConfigRepository(paths);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var transferService = new RuntimePackPolicyTransferService(workspace.RootPath, artifactRepository, configRepository);

        var invalidPackage = new RuntimePackPolicyPackage
        {
            PackageId = "packpolpkg-invalid",
            RuntimeStandardVersion = "0.4.0",
            AdmissionPolicy = new RuntimePackAdmissionPolicyArtifact
            {
                PolicyId = string.Empty,
                RuntimeStandardVersion = "0.4.0",
                AllowedChannels = [],
                AllowedPackTypes = [],
                RequireSignature = true,
                RequireProvenance = true,
                Summary = "invalid",
            },
            SwitchPolicy = new RuntimePackSwitchPolicyArtifact
            {
                PolicyId = string.Empty,
                PolicyMode = "local_pin_current_selection",
                PinActive = true,
                PackId = string.Empty,
                PackVersion = string.Empty,
                Channel = string.Empty,
                Reason = "invalid",
                Summary = "invalid",
                ChecksPassed = [],
            },
            Summary = "invalid",
            Notes = [],
        };

        var invalidPackagePath = workspace.WriteFile(
            "artifacts/invalid-runtime-pack-policy.json",
            JsonSerializer.Serialize(
                invalidPackage,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                }));

        var result = transferService.Import(invalidPackagePath);

        Assert.False(result.Succeeded);
        Assert.Contains("runtime_pack_policy_package_allowed_channels_missing", result.FailureCodes);
        Assert.Contains("runtime_pack_policy_package_allowed_pack_types_missing", result.FailureCodes);
        Assert.Contains("runtime_pack_policy_package_admission_policy_id_missing", result.FailureCodes);
        Assert.Contains("runtime_pack_policy_package_switch_policy_id_missing", result.FailureCodes);
        Assert.Contains("runtime_pack_policy_package_switch_policy_invalid", result.FailureCodes);
    }
}
