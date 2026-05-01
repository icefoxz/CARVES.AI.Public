using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeSessionGatewayPrivateAlphaHandoffServiceTests
{
    [Fact]
    public void Build_ProjectsRuntimeOwnedPrivateAlphaHandoffReadiness()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md", "# plan");
        workspace.WriteFile("docs/session-gateway/release-surface.md", "# release");
        workspace.WriteFile("docs/session-gateway/dogfood-validation.md", "# dogfood");
        workspace.WriteFile("docs/session-gateway/operator-proof-contract.md", "# operator proof");
        workspace.WriteFile("docs/session-gateway/ALPHA_SETUP.md", "# setup");
        workspace.WriteFile("docs/session-gateway/ALPHA_QUICKSTART.md", "# quickstart");
        workspace.WriteFile("docs/session-gateway/KNOWN_LIMITATIONS.md", "# limitations");
        workspace.WriteFile("docs/session-gateway/BUG_REPORT_BUNDLE.md", "# bundle");

        var service = new RuntimeSessionGatewayPrivateAlphaHandoffService(
            workspace.RootPath,
            () => new RuntimeSessionGatewayDogfoodValidationSurface
            {
                OverallPosture = "narrow_private_alpha_ready",
                ProgramClosureVerdict = "program_closure_complete",
                ContinuationGateOutcome = "closure_review_completed",
                ThinShellRoute = "/session-gateway/v1/shell",
                SessionCollectionRoute = "/api/session-gateway/v1/sessions",
                MessageRouteTemplate = "/api/session-gateway/v1/sessions/{session_id}/messages",
                EventsRouteTemplate = "/api/session-gateway/v1/sessions/{session_id}/events",
                AcceptedOperationRouteTemplate = "/api/session-gateway/v1/operations/{operation_id}",
                SupportedIntents = ["discuss", "plan", "governed_run"],
                IsValid = true,
            },
            () => new OperationalSummary
            {
                ProviderHealthIssueCount = 1,
                OptionalProviderHealthIssueCount = 1,
                DisabledProviderCount = 0,
                RecommendedNextAction = "continue on fallback backend and repair the preferred lane if failures continue",
                Providers =
                [
                    new OperationalProviderHealthSummary
                    {
                        BackendId = "codex_cli",
                        ProviderId = "codex",
                        State = "healthy",
                        RecommendedNextAction = "observe",
                    },
                ],
            },
            () => new RepoRuntimeHealthCheckResult
            {
                State = RepoRuntimeHealthState.Healthy,
                Summary = "Runtime health is healthy.",
                SuggestedAction = "observe",
            });

        var surface = service.Build();

        Assert.Equal("runtime-session-gateway-private-alpha-handoff", surface.SurfaceId);
        Assert.Equal("private_alpha_deliverable_ready", surface.OverallPosture);
        Assert.Equal("narrow_private_alpha_ready", surface.DogfoodValidationPosture);
        Assert.Equal("program_closure_complete", surface.ProgramClosureVerdict);
        Assert.Equal("closure_review_completed", surface.ContinuationGateOutcome);
        Assert.Equal("runtime_owned_private_alpha", surface.HandoffOwnership);
        Assert.Equal("docs/session-gateway/operator-proof-contract.md", surface.OperatorProofContractPath);
        Assert.Equal("/session-gateway/v1/shell", surface.ThinShellRoute);
        Assert.Equal("actionability_issues=1; optional=1; disabled=0", surface.ProviderVisibilitySummary);
        Assert.Contains(RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-private-alpha-handoff"), surface.StartupCommands);
        Assert.Contains(RuntimeHostCommandLauncher.Cold("repair"), surface.MaintenanceCommands);
        Assert.Contains("governed_run", surface.SupportedIntents);
        Assert.Equal(SessionGatewayProofSources.RepoLocalProof, surface.OperatorProofContract.CurrentProofSource);
        Assert.Equal(SessionGatewayOperatorWaitStates.WaitingOperatorSetup, surface.OperatorProofContract.CurrentOperatorState);
        Assert.Equal(4, surface.OperatorProofContract.StageExitContracts.Count);
        Assert.True(surface.IsValid);
        Assert.Empty(surface.Errors);
    }
}
