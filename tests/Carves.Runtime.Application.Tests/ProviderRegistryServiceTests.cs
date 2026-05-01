using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class ProviderRegistryServiceTests
{
    [Fact]
    public void List_UsesConfiguredOpenAiWorkerModelOverride()
    {
        using var workspace = new TemporaryWorkspace();
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var originalWorkerModel = Environment.GetEnvironmentVariable("CARVES_OPENAI_WORKER_MODEL");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-openai-key");
        Environment.SetEnvironmentVariable("CARVES_OPENAI_WORKER_MODEL", "gpt-5.4");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("openai");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);

            var registry = providers.List();
            var openAi = Assert.Single(registry, item => item.ProviderId == "openai");
            var workerProfile = Assert.Single(openAi.Profiles, item => item.ProfileId == "worker-codegen-fast");

            Assert.Equal("gpt-5.4", workerProfile.Model);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
            Environment.SetEnvironmentVariable("CARVES_OPENAI_WORKER_MODEL", originalWorkerModel);
        }
    }

    [Fact]
    public void List_RefreshesPersistedCanonicalOpenAiProfileWhenEnvironmentOverrideChanges()
    {
        using var workspace = new TemporaryWorkspace();
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var originalWorkerModel = Environment.GetEnvironmentVariable("CARVES_OPENAI_WORKER_MODEL");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-openai-key");
        Environment.SetEnvironmentVariable("CARVES_OPENAI_WORKER_MODEL", "gpt-5.4");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("openai");
            var repository = new JsonProviderRegistryRepository(workspace.Paths);
            repository.Save(new ProviderRegistry
            {
                Items =
                [
                    new ProviderDescriptor
                    {
                        ProviderId = "openai",
                        DisplayName = "OpenAI Responses",
                        SecretEnvironmentVariable = "OPENAI_API_KEY",
                        Capabilities = new AIProviderCapabilities(true, true, true, true, true),
                        Profiles =
                        [
                            new ProviderProfileBinding { ProfileId = "worker-codegen-fast", Role = "worker", Model = "gpt-5-mini", Description = "Legacy worker profile." },
                            new ProviderProfileBinding { ProfileId = "custom-experimental", Role = "worker", Model = "gpt-4.1", Description = "Custom profile." },
                        ],
                        PermittedRepoScopes = ["*"],
                    },
                ],
            });
            var providers = new ProviderRegistryService(repository, repoRegistry, governance, adapters);

            var registry = providers.List();
            var openAi = Assert.Single(registry, item => item.ProviderId == "openai");
            var workerProfile = Assert.Single(openAi.Profiles, item => item.ProfileId == "worker-codegen-fast");
            var customProfile = Assert.Single(openAi.Profiles, item => item.ProfileId == "custom-experimental");

            Assert.Equal("gpt-5.4", workerProfile.Model);
            Assert.Equal("Custom profile.", customProfile.Description);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
            Environment.SetEnvironmentVariable("CARVES_OPENAI_WORKER_MODEL", originalWorkerModel);
        }
    }

    [Fact]
    public void ProviderRegistryRepository_StripsEmbeddedBackendHealthFromDefinitionFiles()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonProviderRegistryRepository(workspace.Paths);
        var codex = new ProviderDescriptor
        {
            ProviderId = "codex",
            DisplayName = "OpenAI Codex",
            SecretEnvironmentVariable = "OPENAI_API_KEY",
            WorkerBackends =
            [
                new WorkerBackendDescriptor
                {
                    BackendId = "codex_cli",
                    ProviderId = "codex",
                    AdapterId = "CodexCliWorkerAdapter",
                    DisplayName = "Codex CLI Worker",
                    RoutingIdentity = "codex.worker.cli",
                    ProtocolFamily = "local_cli",
                    RequestFamily = "codex_exec",
                    Health = new WorkerBackendHealthSummary
                    {
                        State = WorkerBackendHealthState.Healthy,
                        Summary = "Healthy",
                    },
                },
            ],
        };

        repository.Save(new ProviderRegistry { Items = [codex] });

        var providerJson = File.ReadAllText(Path.Combine(workspace.Paths.PlatformProvidersRoot, "codex.json"));
        var registryJson = File.ReadAllText(workspace.Paths.PlatformProviderRegistryFile);

        Assert.DoesNotContain("\"health\"", providerJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"health\"", registryJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void List_DoesNotRewriteCanonicalProviderDefinitionsWhenNoSemanticChangeExists()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create();
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);

        _ = providers.List();
        var providerPayload = File.ReadAllText(Path.Combine(workspace.Paths.PlatformProvidersRoot, "codex.json"));
        var registryPayload = File.ReadAllText(workspace.Paths.PlatformProviderRegistryFile);

        _ = providers.List();
        var secondProviderPayload = File.ReadAllText(Path.Combine(workspace.Paths.PlatformProvidersRoot, "codex.json"));
        var secondRegistryPayload = File.ReadAllText(workspace.Paths.PlatformProviderRegistryFile);

        Assert.Equal(providerPayload, secondProviderPayload);
        Assert.Equal(registryPayload, secondRegistryPayload);
    }

    [Fact]
    public void List_ExposesClaudeWorkerProfileAndExecutionCapability()
    {
        using var workspace = new TemporaryWorkspace();
        var originalApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-anthropic-key");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("claude");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);

            var registry = providers.List();
            var claude = Assert.Single(registry, item => item.ProviderId == "claude");
            var workerProfile = Assert.Single(claude.Profiles, item => item.ProfileId == "claude-worker-bounded");
            var backend = Assert.Single(claude.WorkerBackends, item => item.BackendId == "claude_api");

            Assert.True(claude.Capabilities.SupportsCodeGeneration);
            Assert.Equal("worker", workerProfile.Role);
            Assert.Equal("claude-worker-bounded", Assert.Single(backend.RoutingProfiles));
            Assert.True(backend.Capabilities.SupportsExecution);
            Assert.True(backend.Capabilities.SupportsDotNetBuild);
            Assert.True(backend.Capabilities.SupportsNetworkAccess);
            Assert.Equal(WorkerBackendHealthState.Healthy, backend.Health.State);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", originalApiKey);
        }
    }
}
