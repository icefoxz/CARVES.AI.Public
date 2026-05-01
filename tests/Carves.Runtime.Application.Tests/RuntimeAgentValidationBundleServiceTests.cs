using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeAgentValidationBundleServiceTests
{
    [Fact]
    public void Build_ProjectsBoundedStage6ValidationBundle()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-agent-governed-validation-bundle-test-hardening-contract.md", "# contract");
        workspace.WriteFile("docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md", "# guide");
        workspace.WriteFile("docs/runtime/runtime-agent-v1-delivery-workmap.md", "# workmap");
        workspace.WriteFile("docs/runtime/runtime-agent-v1-architecture.md", "# architecture");
        workspace.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# packet");
        workspace.WriteFile("docs/guides/HOST_AND_PROVIDER_QUICKSTART.md", "# quickstart");
        workspace.WriteFile("docs/session-gateway/session-gateway-v1.md", "# gateway");
        workspace.WriteFile("docs/session-gateway/gateway-boundary.md", "# boundary");
        workspace.WriteFile("docs/runtime/runtime-agent-governed-inspect-plan-surfaces-contract.md", "# inspect");
        workspace.WriteFile("docs/runtime/runtime-agent-governed-run-diff-review-surfaces-contract.md", "# run");
        workspace.WriteFile("docs/runtime/runtime-agent-governed-safety-validation-gate-hardening-contract.md", "# hardening");
        workspace.WriteFile("docs/runtime/runtime-consistency-check.md", "# consistency");
        workspace.WriteFile("docs/runtime/delegated-worker-lifecycle-reconciliation.md", "# reconcile");
        workspace.WriteFile("docs/runtime/runtime-agent-governed-failure-classification-recovery-closure-contract.md", "# failure");
        workspace.WriteFile("docs/runtime/runtime-host-lifecycle-proof-gate.md", "# host lifecycle proof");
        workspace.WriteFile("tests/Carves.Runtime.Application.Tests/TestData/runtime-contract-presence.manifest.json", "{}");
        workspace.WriteFile("tests/Carves.Runtime.Application.Tests/RuntimeContractTests.cs", "// contract");
        workspace.WriteFile("tests/Carves.Runtime.Application.Tests/RuntimeAgentBootstrapSurfaceServiceTests.cs", "// bootstrap");
        workspace.WriteFile("tests/Carves.Runtime.Application.Tests/RuntimeAgentValidationBundleServiceTests.cs", "// validation bundle");
        workspace.WriteFile("tests/Carves.Runtime.Application.Tests/RuntimeAgentFailureRecoveryClosureServiceTests.cs", "// failure");
        workspace.WriteFile("tests/Carves.Runtime.Application.Tests/RuntimeSessionGatewayServiceTests.cs", "// gateway");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/RuntimeSurfaceCommandRegistryHostContractTests.cs", "// registry");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/RuntimeAgentValidationBundleHostContractTests.cs", "// host");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/FriendlyCliEntryTests.cs", "// cli");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs", "// host client");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.RoutingAndAcceptedOperations.cs", "// host client routing");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.ControlPlaneMutationRouting.cs", "// host client mutation");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/ColdHostCommandLauncherTests.cs", "// cold");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/RuntimeFirstRunOperatorPacketHostContractTests.cs", "// packet");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/SessionGatewayHostContractTests.cs", "// gateway host");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/SessionGatewayShellHostContractTests.cs", "// gateway shell");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/RuntimeAgentFailureRecoveryClosureHostContractTests.cs", "// failure host");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.HostLifecycle.cs", "// host lifecycle");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/GuardCheckCliTests.cs", "// guard cli");
        workspace.WriteFile("tests/Carves.Runtime.IntegrationTests/RuntimeGovernedAgentHandoffHostContractTests.cs", "// pilot honesty");
        workspace.WriteFile("scripts/beta/host-lifecycle-proof-lane.ps1", "# host proof lane");
        workspace.WriteFile("docs/guides/RUNTIME_OPERATOR_HOST_AND_LIFECYCLE_REFERENCE.md", "# host lifecycle guide");

        var service = new RuntimeAgentValidationBundleService(workspace.RootPath);

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-agent-validation-bundle", surface.SurfaceId);
        Assert.Equal("bounded_v1_validation_bundle_ready", surface.OverallPosture);
        Assert.Equal("runtime_owned_v1_validation_bundle", surface.ValidationOwnership);
        Assert.Equal("docs/runtime/runtime-agent-governed-validation-bundle-test-hardening-contract.md", surface.BoundaryDocumentPath);
        Assert.Equal("docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md", surface.GuidePath);
        Assert.Equal(6, surface.Lanes.Count);
        Assert.Contains(surface.Lanes, item => item.LaneId == "contract_and_read_model_lane");
        Assert.Contains(surface.Lanes, item => item.LaneId == "attach_and_first_run_lane");
        Assert.Contains(surface.Lanes, item => item.LaneId == "gateway_and_governed_entry_lane");
        Assert.Contains(surface.Lanes, item => item.LaneId == "governed_dev_lane");
        Assert.Contains(surface.Lanes, item => item.LaneId == "failure_and_recovery_lane");
        Assert.Contains(surface.Lanes, item => item.LaneId == "host_lifecycle_and_entry_honesty_lane");
        Assert.Contains(surface.Lanes, item => item.LaneId == "gateway_and_governed_entry_lane"
            && item.Notes.Any(note => note.Contains("mutation-forwarding timeout drift", StringComparison.Ordinal)));
        Assert.Contains(surface.Lanes, item => item.LaneId == "governed_dev_lane"
            && item.ValidationCommands.Any(command => command.Contains("HostStatusJson_WithHost_ProjectsSelectedLoopCapabilityReadiness", StringComparison.Ordinal)));
        Assert.Contains(surface.Lanes, item => item.LaneId == "governed_dev_lane"
            && item.ValidationCommands.Any(command => command.Contains("TaskInspectWithRuns_WithHost_ProjectsCandidateReadBeforeDryRun", StringComparison.Ordinal)));
        Assert.Contains(surface.Lanes, item => item.LaneId == "governed_dev_lane"
            && item.ValidationCommands.Any(command => command.Contains("TaskRunDryRun_WithHost_RoutesThroughResidentHostAndPreservesRoleModeGate", StringComparison.Ordinal)));
        Assert.Contains(surface.Lanes, item => item.LaneId == "governed_dev_lane"
            && item.ValidationCommands.Any(command => command.Contains("TaskIngestResult_WithoutHost_RequiresExplicitHostEnsureAndDoesNotMutateTruth", StringComparison.Ordinal)));
        Assert.Contains(surface.Lanes, item => item.LaneId == "governed_dev_lane"
            && item.ValidationCommands.Any(command => command.Contains("SyncState_WithHost_RoutesThroughResidentHost", StringComparison.Ordinal)));
        Assert.Contains(surface.Lanes, item => item.LaneId == "failure_and_recovery_lane"
            && item.Notes.Any(note => note.Contains("RuntimeConsistencyCommandTests", StringComparison.Ordinal)));
        Assert.Contains(surface.Lanes, item => item.LaneId == "host_lifecycle_and_entry_honesty_lane"
            && item.ValidationCommands.Any(command => command.Contains("host-lifecycle-proof-lane.ps1", StringComparison.Ordinal)));
        Assert.Contains(surface.Lanes, item => item.LaneId == "host_lifecycle_and_entry_honesty_lane"
            && item.Notes.Any(note => note.Contains("serial", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(surface.NonClaims, item => item.Contains("whole-suite perfection", StringComparison.Ordinal));
    }
}
