using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeAgentOperatorFeedbackClosureServiceTests
{
    [Fact]
    public void Build_ProjectsBoundedOperatorFeedbackBundles()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-agent-governed-operator-feedback-closure-contract.md", "# contract");
        workspace.WriteFile("docs/guides/RUNTIME_AGENT_V1_OPERATOR_FEEDBACK_GUIDE.md", "# guide");
        workspace.WriteFile("docs/guides/RUNTIME_AGENT_V1_DELIVERY_READINESS.md", "# delivery");
        workspace.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# packet");
        workspace.WriteFile("docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md", "# bundle");
        workspace.WriteFile("docs/runtime/runtime-agent-governed-failure-classification-recovery-closure-contract.md", "# failure");

        var service = new RuntimeAgentOperatorFeedbackClosureService(workspace.RootPath);

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-agent-operator-feedback-closure", surface.SurfaceId);
        Assert.Equal("bounded_operator_feedback_ready", surface.OverallPosture);
        Assert.Equal("runtime_owned_operator_guidance_projection", surface.FeedbackOwnership);
        Assert.Equal(5, surface.FeedbackBundles.Count);
        Assert.Contains(surface.FeedbackBundles, item => item.BundleId == "host_start_and_attach");
        Assert.Contains(surface.FeedbackBundles, item => item.BundleId == "delivery_and_validation_readback");
        Assert.Contains(surface.FeedbackBundles, item => item.BundleId == "repair_and_recovery");
        Assert.Contains(surface.FeedbackBundles, item => item.BundleId == "dispatchable_run"
            && item.Commands.Any(command => command == "carves run"));
        Assert.Equal("host_start_and_attach", RuntimeAgentOperatorFeedbackClosureService.SelectBundleId(false, false, RepoRuntimeHealthState.Healthy, "host_not_running"));
        Assert.Equal("repair_and_recovery", RuntimeAgentOperatorFeedbackClosureService.SelectBundleId(true, true, RepoRuntimeHealthState.Dirty, "idle"));
        Assert.Equal("dispatchable_run", RuntimeAgentOperatorFeedbackClosureService.SelectBundleId(true, true, RepoRuntimeHealthState.Healthy, "dispatchable"));
    }
}
