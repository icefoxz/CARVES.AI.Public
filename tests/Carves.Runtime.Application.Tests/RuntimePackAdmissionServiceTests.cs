using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackAdmissionServiceTests
{
    [Fact]
    public void Admit_PersistsCurrentAdmissionArtifactForMatchingPair()
    {
        using var workspace = new TemporaryWorkspace();
        var (packArtifactPath, attributionPath) = WriteValidPackPair(workspace, "0.4.0", "0.4.x");
        var service = CreateService(workspace.RootPath, out var artifactRepository);

        var result = service.Admit(packArtifactPath, attributionPath);

        Assert.True(result.Admitted);
        var artifact = artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact();
        Assert.NotNull(artifact);
        Assert.Equal("carves.runtime.core", artifact!.PackId);
        Assert.Equal("1.2.3", artifact.PackVersion);
        Assert.Equal("stable", artifact.Channel);
        Assert.Equal("0.4.0", artifact.RuntimeStandardVersion);
        Assert.Contains("policy preset matches", artifact.ChecksPassed);
        Assert.Contains("runtime compatibility accepts local CARVES standard", artifact.ChecksPassed);
    }

    [Fact]
    public void Admit_RejectsCompatibilityMismatchBeforePersistingLocalTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var (packArtifactPath, attributionPath) = WriteValidPackPair(workspace, "0.5.0", "0.5.x");
        var service = CreateService(workspace.RootPath, out var artifactRepository);

        var result = service.Admit(packArtifactPath, attributionPath);

        Assert.False(result.Admitted);
        Assert.Contains("runtime_standard_incompatible", result.FailureCodes);
        Assert.Null(artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact());
    }

    [Fact]
    public void Admit_RejectsDisallowedPreviewChannelBeforePersistingLocalTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var (packArtifactPath, attributionPath) = WriteValidPackPair(workspace, "0.4.0", "0.4.x", channel: "preview");
        var service = CreateService(workspace.RootPath, out var artifactRepository);

        var result = service.Admit(packArtifactPath, attributionPath);

        Assert.False(result.Admitted);
        Assert.Contains("runtime_pack_admission_channel_disallowed", result.FailureCodes);
        Assert.Null(artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact());
    }

    [Fact]
    public void Admit_RejectsDisallowedPackTypeBeforePersistingLocalTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var (packArtifactPath, attributionPath) = WriteValidPackPair(workspace, "0.4.0", "0.4.x", packType: "enterprise_profile_pack");
        var service = CreateService(workspace.RootPath, out var artifactRepository);

        var result = service.Admit(packArtifactPath, attributionPath);

        Assert.False(result.Admitted);
        Assert.Contains("runtime_pack_admission_pack_type_disallowed", result.FailureCodes);
        Assert.Null(artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact());
    }

    [Fact]
    public void Admit_RejectsMissingSignatureAndProvenanceBeforePersistingLocalTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var (packArtifactPath, attributionPath) = WriteValidPackPair(workspace, "0.4.0", "0.4.x", includeSignature: false, includeProvenance: false);
        var service = CreateService(workspace.RootPath, out var artifactRepository);

        var result = service.Admit(packArtifactPath, attributionPath);

        Assert.False(result.Admitted);
        Assert.Contains("runtime_pack_admission_signature_required", result.FailureCodes);
        Assert.Contains("runtime_pack_admission_provenance_required", result.FailureCodes);
        Assert.Null(artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact());
    }

    private static RuntimePackAdmissionService CreateService(string rootPath, out JsonRuntimeArtifactRepository artifactRepository)
    {
        return RuntimePackAdmissionServiceTestsHelper.CreateAdmissionService(rootPath, out artifactRepository);
    }

    private static (string PackArtifactPath, string AttributionPath) WriteValidPackPair(
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
        return RuntimePackAdmissionServiceTestsHelper.WriteValidPackPair(
            workspace,
            minVersion,
            maxVersion,
            packVersion,
            channel,
            fileStem,
            packType,
            includeSignature,
            includeProvenance);
    }
}
