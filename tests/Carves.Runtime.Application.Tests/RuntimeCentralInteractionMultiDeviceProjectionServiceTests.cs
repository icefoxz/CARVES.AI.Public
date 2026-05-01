using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeCentralInteractionMultiDeviceProjectionServiceTests
{
    [Fact]
    public void Build_ProjectsBoundedCentralInteractionTruth()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-central-interaction-point-and-official-truth-ingress.md", "# doctrine");
        workspace.WriteFile("docs/runtime/runtime-central-interaction-multi-device-projection-workmap.md", "# workmap");
        workspace.WriteFile("docs/runtime/runtime-projection-class-matrix.md", "# projection classes");
        workspace.WriteFile("docs/runtime/runtime-role-authority-matrix.md", "# role matrix");
        workspace.WriteFile("docs/runtime/runtime-mobile-thin-client-action-envelope.md", "# envelope");
        workspace.WriteFile("docs/runtime/runtime-external-agent-ingress-contract.md", "# ingress");
        workspace.WriteFile("docs/runtime/runtime-session-continuity-and-notification-return-lane.md", "# continuity");
        workspace.WriteFile("docs/runtime/runtime-governance-program-reaudit.md", "# reaudit");
        workspace.WriteFile("docs/session-gateway/capability-forge-retirement-routing.md", "# retirement");
        workspace.WriteFile("docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md", "# session gateway");

        var service = new RuntimeCentralInteractionMultiDeviceProjectionService(workspace.RootPath);
        var surface = service.Build();

        Assert.Equal("runtime-central-interaction-multi-device-projection", surface.SurfaceId);
        Assert.Equal("central_interaction_multi_device_projection_ready", surface.OverallPosture);
        Assert.Equal("611_line_central_interaction_multi_device_projection", surface.CurrentLine);
        Assert.Equal("none", surface.DeferredNextLine);
        Assert.Equal("program_closure_complete", surface.ProgramClosureVerdict);
        Assert.Equal(4, surface.ProjectionClassCount);
        Assert.True(surface.RoleAuthorityCount >= 6);
        Assert.True(surface.ClientActionEnvelopeCount >= 5);
        Assert.True(surface.ExternalAgentIngressCount >= 4);
        Assert.True(surface.ContinuityLaneCount >= 4);
        Assert.Equal(4, surface.ProjectionClasses.Count);
        Assert.True(surface.IsValid);
        Assert.Empty(surface.Errors);
        Assert.Contains(surface.NonClaims, item => item.Contains("does not prove", StringComparison.OrdinalIgnoreCase));
    }
}
