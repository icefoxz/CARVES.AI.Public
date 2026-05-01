using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeValidationLabProofHandoffServiceTests
{
    [Fact]
    public void Build_ProjectsApprovalAndRecoveryProofLanes()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# boundary");

        var service = new RuntimeValidationLabProofHandoffService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault());

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-validationlab-proof-handoff", surface.SurfaceId);
        Assert.True(surface.ControlledModeDefault);
        Assert.Equal(2, surface.Lanes.Count);

        var approval = Assert.Single(surface.Lanes, lane => lane.LaneId == "approval_proof");
        Assert.Contains("approval_recovery", surface.ValidationLabFollowOnLanes);
        Assert.Contains(approval.RuntimeTruthFamilies, family =>
            family.FamilyId == "worker_execution_artifact_history"
            && family.PackagingMode == RuntimeExportPackagingMode.PointerOnly
            && family.Roots.Contains(".ai/artifacts/worker-permissions", StringComparer.Ordinal)
            && family.Roots.Contains(".ai/artifacts/merge-candidates", StringComparer.Ordinal));

        var recovery = Assert.Single(surface.Lanes, lane => lane.LaneId == "recovery_proof");
        Assert.Contains(recovery.RuntimeTruthFamilies, family =>
            family.FamilyId == "platform_runtime_ledger_history"
            && family.PackagingMode == RuntimeExportPackagingMode.PointerOnly);
        Assert.Contains("runtime_live_state", recovery.Discipline.ManifestOnlyFamilyIds);
    }

    [Fact]
    public void Build_IsInvalidWhenBoundaryDocumentIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeValidationLabProofHandoffService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault());

        var surface = service.Build();

        Assert.False(surface.IsValid);
        Assert.Contains(surface.Errors, error => error.Contains("runtime-validationlab-proof-handoff-boundary.md", StringComparison.Ordinal));
    }
}
