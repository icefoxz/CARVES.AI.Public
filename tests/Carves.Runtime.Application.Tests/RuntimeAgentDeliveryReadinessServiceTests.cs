using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeAgentDeliveryReadinessServiceTests
{
    [Fact]
    public void Build_ProjectsBoundedStage6DeliveryReadiness()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-agent-governed-packaging-closure-delivery-readiness-contract.md", "# contract");
        workspace.WriteFile("docs/guides/RUNTIME_AGENT_V1_DELIVERY_READINESS.md", "# guide");
        workspace.WriteFile("docs/runtime/runtime-packaging-proof-federation-maturity.md", "# packaging");
        workspace.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# packet");
        workspace.WriteFile("docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md", "# bundle");
        workspace.WriteFile("scripts/carves-host.ps1", "Write-Host 'carves'");

        var service = new RuntimeAgentDeliveryReadinessService(workspace.RootPath);

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-agent-delivery-readiness", surface.SurfaceId);
        Assert.Equal("bounded_delivery_readiness_ready", surface.OverallPosture);
        Assert.Equal("runtime_owned_source_tree_trial_entry", surface.DeliveryOwnership);
        Assert.Equal("resident_host_attach_first_run_validation_bundle", surface.EntryLaneId);
        Assert.Equal("scripts/carves-host.ps1", surface.TrialWrapperPath);
        Assert.Contains(surface.RuntimeTruthFiles, item => item == ".ai/runtime.json");
        Assert.Contains(surface.RelatedSurfaceRefs, item => item == "runtime-agent-validation-bundle");
        Assert.Contains(surface.BlockedClaims, item => item.Contains("second_product_shell", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, item => item.Contains("CARVES.Operator", StringComparison.Ordinal));
    }
}
