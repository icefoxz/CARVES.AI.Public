using Carves.Runtime.Application.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeRemoteWorkerOnboardingChecklistServiceTests
{
    [Fact]
    public void BuildSurface_ProjectsChecklistForBoundedRemoteProviders()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var providerRegistry = new ProviderRegistryService(
            new JsonProviderRegistryRepository(workspace.Paths),
            repoRegistry,
            governance,
            TestWorkerAdapterRegistryFactory.Create("gemini"));
        var service = new RuntimeRemoteWorkerOnboardingChecklistService(providerRegistry);

        var surface = service.BuildSurface();

        Assert.Equal("runtime-remote-worker-onboarding-checklist", surface.SurfaceId);
        Assert.Equal("external_app_cli_only", surface.CurrentActivationMode);
        Assert.Equal(
            ["codex_app_schedule_evidence_callback", "codex_cli_host_routed_execution", "external_agent_app_cli_adapter_governed_onboarding"],
            surface.AllowedExternalWorkerPaths);
        Assert.False(surface.SdkApiWorkerBackendsAllowed);
        Assert.Equal("closed_until_separate_governed_activation", surface.SdkApiWorkerBoundary);
        Assert.Contains("claude_api", surface.DisallowedWorkerBackendIds);
        Assert.Contains("gemini_api", surface.DisallowedWorkerBackendIds);
        Assert.Contains(surface.Notes, item => item.Contains("Claude is not blocked by provider name", StringComparison.Ordinal));
        Assert.Contains(surface.Providers, item => item.ProviderId == "claude");
        Assert.Contains(surface.Providers, item => item.ProviderId == "gemini");
        Assert.All(surface.Providers, item =>
        {
            Assert.Equal("closed_current_policy_future_external_adapter_onboarding_only", item.ActivationState);
            Assert.False(item.CurrentWorkerSelectionEligible);
        });
        Assert.Contains(surface.Providers.SelectMany(item => item.Checklist), item => item.ItemId == "protocol_adapter");
        Assert.Contains(surface.Providers.SelectMany(item => item.Checklist), item => item.ItemId == "checkpoint_proof");
    }
}
