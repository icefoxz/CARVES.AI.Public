using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackAdmissionPolicyServiceTests
{
    [Fact]
    public void BuildSurface_ProjectsDefaultLocalAdmissionPolicy()
    {
        using var workspace = new TemporaryWorkspace();
        var configRepository = new FileControlPlaneConfigRepository(workspace.Paths);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var service = new RuntimePackAdmissionPolicyService(configRepository, artifactRepository);

        var surface = service.BuildSurface();

        Assert.Equal("runtime-pack-admission-policy", surface.SurfaceId);
        Assert.Equal("0.4.0", surface.RuntimeStandardVersion);
        Assert.Contains("stable", surface.CurrentPolicy.AllowedChannels);
        Assert.Contains("candidate", surface.CurrentPolicy.AllowedChannels);
        Assert.Contains("runtime_pack", surface.CurrentPolicy.AllowedPackTypes);
        Assert.Contains("vertical_runtime_pack", surface.CurrentPolicy.AllowedPackTypes);
        Assert.True(surface.CurrentPolicy.RequireSignature);
        Assert.True(surface.CurrentPolicy.RequireProvenance);
    }

    [Fact]
    public void Evaluate_RejectsPreviewChannelAndMissingSignatureOrProvenance()
    {
        using var workspace = new TemporaryWorkspace();
        var configRepository = new FileControlPlaneConfigRepository(workspace.Paths);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var service = new RuntimePackAdmissionPolicyService(configRepository, artifactRepository);

        var result = service.Evaluate("runtime_pack", "preview", hasSignature: false, hasProvenance: false);

        Assert.Contains("runtime_pack_admission_channel_disallowed", result.FailureCodes);
        Assert.Contains("runtime_pack_admission_signature_required", result.FailureCodes);
        Assert.Contains("runtime_pack_admission_provenance_required", result.FailureCodes);
    }

    [Fact]
    public void SaveCurrentPolicy_PersistsArtifactBackedAdmissionPolicy()
    {
        using var workspace = new TemporaryWorkspace();
        var configRepository = new FileControlPlaneConfigRepository(workspace.Paths);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var service = new RuntimePackAdmissionPolicyService(configRepository, artifactRepository);
        var policy = RuntimePackAdmissionPolicyArtifact.CreateDefault("0.4.0") with
        {
            PolicyId = "admission-policy-imported",
            AllowedChannels = ["stable", "candidate", "preview"],
            Summary = "Imported local admission policy.",
        };

        service.SaveCurrentPolicy(policy);

        var current = service.BuildCurrentPolicy();
        Assert.Equal("admission-policy-imported", current.PolicyId);
        Assert.Contains("preview", current.AllowedChannels);
        Assert.Equal("Imported local admission policy.", current.Summary);
    }
}
