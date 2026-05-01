using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeControlledGovernanceProofServiceTests
{
    [Fact]
    public void Build_ProjectsControlledGovernanceProofLanes()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# handoff");
        workspace.WriteFile("docs/runtime/runtime-controlled-governance-proof-integration.md", "# integration");

        var service = new RuntimeControlledGovernanceProofService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault());

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-controlled-governance-proof", surface.SurfaceId);
        Assert.True(surface.ControlledModeDefault);
        Assert.True(surface.ProducerCannotSelfApprove);
        Assert.True(surface.ReviewerCannotApproveSameTask);
        Assert.Contains(surface.ControlledModeInvariants, item => item.Contains("no second proof ledger", StringComparison.Ordinal));
        Assert.Equal(4, surface.Lanes.Count);

        var approval = Assert.Single(surface.Lanes, lane => lane.LaneId == "approval");
        Assert.Contains("approval_proof", approval.SourceHandoffLaneIds);
        Assert.Contains(approval.GoverningCommands, command => command.StartsWith("approve-review", StringComparison.Ordinal));

        var refusal = Assert.Single(surface.Lanes, lane => lane.LaneId == "refusal");
        Assert.Contains("recovery_proof", refusal.SourceHandoffLaneIds);
        Assert.Contains(refusal.RuntimeEvidencePaths, item => item == ".carves-platform/runtime-state/events/permission_audit.json");

        var interruption = Assert.Single(surface.Lanes, lane => lane.LaneId == "interruption");
        Assert.Contains(interruption.GoverningCommands, command => command.StartsWith("inspect execution-run-exceptions", StringComparison.Ordinal));

        var recovery = Assert.Single(surface.Lanes, lane => lane.LaneId == "recovery");
        Assert.Contains(recovery.RuntimeTruthFamilies, family => family.FamilyId == "platform_runtime_ledger_history");
    }

    [Fact]
    public void Build_IsInvalidWhenBoundaryDocumentIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# handoff");

        var service = new RuntimeControlledGovernanceProofService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault());

        var surface = service.Build();

        Assert.False(surface.IsValid);
        Assert.Contains(surface.Errors, error => error.Contains("runtime-controlled-governance-proof-integration.md", StringComparison.Ordinal));
    }
}
