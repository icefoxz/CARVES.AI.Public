using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeSessionGatewayDogfoodValidationServiceTests
{
    [Fact]
    public void Build_ProjectsNarrowPrivateAlphaReadinessOnRuntimeOwnedLane()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/session-gateway/dogfood-validation.md", "# stage4");
        workspace.WriteFile("docs/session-gateway/release-surface.md", "# release");
        workspace.WriteFile("docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md", "# plan");
        workspace.WriteFile("docs/session-gateway/operator-proof-contract.md", "# operator proof");

        var service = new RuntimeSessionGatewayDogfoodValidationService(
            workspace.RootPath,
            () => new RuntimeGovernanceProgramReauditSurface
            {
                OverallVerdict = "program_closure_complete",
                ContinuationGateOutcome = "closure_review_completed",
            });

        var surface = service.Build();

        Assert.Equal("runtime-session-gateway-dogfood-validation", surface.SurfaceId);
        Assert.Equal("narrow_private_alpha_ready", surface.OverallPosture);
        Assert.Equal("program_closure_complete", surface.ProgramClosureVerdict);
        Assert.Equal("closure_review_completed", surface.ContinuationGateOutcome);
        Assert.Equal("/session-gateway/v1/shell", surface.ThinShellRoute);
        Assert.Equal("/api/session-gateway/v1/operations/{operation_id}", surface.AcceptedOperationRouteTemplate);
        Assert.Equal("docs/session-gateway/operator-proof-contract.md", surface.OperatorProofContractPath);
        Assert.Contains("discuss", surface.SupportedIntents);
        Assert.Contains("governed_run", surface.SupportedIntents);
        Assert.Contains("accepted_operation_lookup_under_gateway_namespace", surface.ValidatedScenarios);
        Assert.Contains("operator_proof_contract_projection", surface.ValidatedScenarios);
        Assert.Equal("runtime_owned_forwarding_landed", surface.MutationForwardingPosture);
        Assert.Equal("narrow_private_alpha_ready", surface.PrivateAlphaPosture);
        Assert.Equal(SessionGatewayProofSources.RepoLocalProof, surface.OperatorProofContract.CurrentProofSource);
        Assert.Equal(SessionGatewayOperatorWaitStates.WaitingOperatorSetup, surface.OperatorProofContract.CurrentOperatorState);
        Assert.True(surface.OperatorProofContract.RealWorldProofMissing);
        Assert.Empty(surface.DeferredFollowOns);
        Assert.True(surface.IsValid);
    }
}
