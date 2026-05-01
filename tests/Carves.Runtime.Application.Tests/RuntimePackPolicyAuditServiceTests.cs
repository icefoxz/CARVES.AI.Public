using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackPolicyAuditServiceTests
{
    [Fact]
    public void Build_ProjectsRecentPolicyAuditEntries()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackPolicyAuditEntry(new RuntimePackPolicyAuditEntry
        {
            EventKind = "policy_exported",
            SourceKind = "local_export",
            PackageId = "packpolpkg-exported",
            ResultingAdmissionPolicyId = "admission-policy-1",
            ResultingSwitchPolicyId = "switch-policy-1",
            Summary = "Exported local policy package.",
        });
        artifactRepository.SaveRuntimePackPolicyAuditEntry(new RuntimePackPolicyAuditEntry
        {
            EventKind = "policy_imported",
            SourceKind = "local_import",
            PackageId = "packpolpkg-imported",
            ResultingAdmissionPolicyId = "admission-policy-2",
            ResultingSwitchPolicyId = "switch-policy-2",
            Summary = "Imported local policy package.",
        });

        var surface = new RuntimePackPolicyAuditService(artifactRepository).Build();

        Assert.Equal("runtime-pack-policy-audit", surface.SurfaceId);
        Assert.Equal(2, surface.Entries.Count);
        Assert.Equal("policy_imported", surface.Entries[0].EventKind);
        Assert.Equal("policy_exported", surface.Entries[1].EventKind);
        Assert.Contains("append-only", string.Join(' ', surface.Notes), StringComparison.OrdinalIgnoreCase);
    }
}
