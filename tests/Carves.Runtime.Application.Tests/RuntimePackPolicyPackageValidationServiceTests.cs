using System.Text.Json;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackPolicyPackageValidationServiceTests
{
    [Fact]
    public void Validate_SucceedsForExportedLocalPolicyPackage()
    {
        using var workspace = new TemporaryWorkspace();
        var configRepository = new FileControlPlaneConfigRepository(workspace.Paths);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var transferService = new RuntimePackPolicyTransferService(workspace.RootPath, artifactRepository, configRepository);
        var validationService = new RuntimePackPolicyPackageValidationService(configRepository);

        var packagePath = Path.Combine(workspace.RootPath, "artifacts", "runtime-pack-policy.json");
        var export = transferService.Export(packagePath);
        Assert.True(export.Succeeded);

        var result = validationService.Validate(packagePath);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Package);
        Assert.Equal(Path.GetFullPath(packagePath), result.Path);
        Assert.Contains("runtime standard matches current local runtime", result.ChecksPassed);
    }

    [Fact]
    public void Validate_RejectsInvalidSwitchAndAdmissionPolicyFields()
    {
        using var workspace = new TemporaryWorkspace();
        var configRepository = new FileControlPlaneConfigRepository(workspace.Paths);
        var validationService = new RuntimePackPolicyPackageValidationService(configRepository);

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

        var result = validationService.Validate(invalidPackagePath);

        Assert.False(result.Succeeded);
        Assert.Contains("runtime_pack_policy_package_allowed_channels_missing", result.FailureCodes);
        Assert.Contains("runtime_pack_policy_package_allowed_pack_types_missing", result.FailureCodes);
        Assert.Contains("runtime_pack_policy_package_admission_policy_id_missing", result.FailureCodes);
        Assert.Contains("runtime_pack_policy_package_switch_policy_id_missing", result.FailureCodes);
        Assert.Contains("runtime_pack_policy_package_switch_policy_invalid", result.FailureCodes);
    }
}
