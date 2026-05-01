using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ProviderRegistryService
{
    private readonly IProviderRegistryRepository repository;
    private readonly RepoRegistryService repoRegistryService;
    private readonly PlatformGovernanceService governanceService;
    private readonly WorkerAdapterRegistry workerAdapterRegistry;

    public ProviderRegistryService(
        IProviderRegistryRepository repository,
        RepoRegistryService repoRegistryService,
        PlatformGovernanceService governanceService,
        WorkerAdapterRegistry workerAdapterRegistry)
    {
        this.repository = repository;
        this.repoRegistryService = repoRegistryService;
        this.governanceService = governanceService;
        this.workerAdapterRegistry = workerAdapterRegistry;
    }

    public ProviderRegistry GetRegistry()
    {
        var registry = repository.Load();
        if (registry.Items.Count == 0)
        {
            registry = new ProviderRegistry
            {
                Items = CreateDefaultProviderDescriptors(),
            };
        }

        registry = Normalize(registry, workerAdapterRegistry, governanceService);
        repository.Save(registry);
        return registry;
    }

    public IReadOnlyList<ProviderDescriptor> List()
    {
        return GetRegistry().Items.OrderBy(item => item.ProviderId, StringComparer.Ordinal).ToArray();
    }

    public ProviderDescriptor Inspect(string providerId)
    {
        return List().First(item => string.Equals(item.ProviderId, providerId, StringComparison.Ordinal));
    }

    public IReadOnlyList<WorkerBackendDescriptor> ListWorkerBackends()
    {
        return List()
            .SelectMany(provider => provider.WorkerBackends)
            .OrderBy(item => item.BackendId, StringComparer.Ordinal)
            .ToArray();
    }

    public WorkerBackendDescriptor InspectWorkerBackend(string backendId)
    {
        return ListWorkerBackends().First(item => string.Equals(item.BackendId, backendId, StringComparison.Ordinal));
    }

    public string? ResolveProfileModel(string? providerId, string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        var providers = string.IsNullOrWhiteSpace(providerId)
            ? List()
            : List().Where(item => string.Equals(item.ProviderId, providerId, StringComparison.Ordinal));

        return providers
            .SelectMany(provider => provider.Profiles)
            .FirstOrDefault(profile => string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal))
            ?.Model;
    }

    public RepoDescriptor Bind(string repoId, string profileId)
    {
        var registry = GetRegistry();
        var match = registry.Items.FirstOrDefault(item => item.Profiles.Any(profile => string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal)));
        if (match is null)
        {
            throw new InvalidOperationException($"Provider profile '{profileId}' was not found.");
        }

        var descriptor = repoRegistryService.Inspect(repoId);
        descriptor.ProviderProfile = profileId;
        repoRegistryService.Update(descriptor);
        governanceService.RecordEvent(GovernanceEventType.ProviderRotated, repoId, $"Bound provider profile '{profileId}' via provider '{match.ProviderId}'.");
        return descriptor;
    }

    private static ProviderRegistry Normalize(ProviderRegistry registry, WorkerAdapterRegistry workerAdapterRegistry, PlatformGovernanceService governanceService)
    {
        var items = registry.Items.ToList();
        var defaults = CreateDefaultProviderDescriptors();
        foreach (var descriptor in defaults)
        {
            if (items.Any(item => string.Equals(item.ProviderId, descriptor.ProviderId, StringComparison.Ordinal)))
            {
                continue;
            }

            items.Add(descriptor);
        }

        var allProfiles = governanceService.GetSnapshot().WorkerPolicies
            .SelectMany(policy => policy.Profiles)
            .Select(profile => profile.ProfileId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var normalized = items
            .OrderBy(item => item.ProviderId, StringComparer.Ordinal)
            .Select(item => new ProviderDescriptor
            {
                SchemaVersion = item.SchemaVersion,
                ProviderId = item.ProviderId,
                DisplayName = item.DisplayName,
                SecretEnvironmentVariable = item.SecretEnvironmentVariable,
                Capabilities = item.Capabilities,
                Profiles = MergeProfiles(item, defaults.First(defaultItem => string.Equals(defaultItem.ProviderId, item.ProviderId, StringComparison.Ordinal)).Profiles),
                WorkerBackends = BuildWorkerBackends(item, workerAdapterRegistry, allProfiles),
                TimeoutSeconds = item.TimeoutSeconds,
                RetryLimit = item.RetryLimit,
                PermittedRepoScopes = item.PermittedRepoScopes,
            })
            .ToArray();

        return new ProviderRegistry
        {
            Items = normalized,
        };
    }

    private static IReadOnlyList<ProviderProfileBinding> MergeProfiles(ProviderDescriptor existing, IReadOnlyList<ProviderProfileBinding> defaults)
    {
        var defaultById = defaults.ToDictionary(profile => profile.ProfileId, StringComparer.Ordinal);
        var merged = existing.Profiles
            .Where(profile => !defaultById.ContainsKey(profile.ProfileId))
            .Concat(defaults)
            .OrderBy(profile => profile.ProfileId, StringComparer.Ordinal)
            .ToArray();
        return merged;
    }

    private static ProviderDescriptor[] CreateDefaultProviderDescriptors()
    {
        return
        [
            new ProviderDescriptor
            {
                ProviderId = "null",
                DisplayName = "Null provider",
                SecretEnvironmentVariable = string.Empty,
                Capabilities = new AIProviderCapabilities(false, false, false, true, false),
                Profiles =
                [
                    new ProviderProfileBinding { ProfileId = "default", Role = "worker", Model = "none", Description = "No-op fallback provider." },
                ],
                TimeoutSeconds = 5,
                RetryLimit = 0,
                PermittedRepoScopes = ["*"],
            },
            new ProviderDescriptor
            {
                ProviderId = "openai",
                DisplayName = "OpenAI Responses",
                SecretEnvironmentVariable = "OPENAI_API_KEY",
                Capabilities = new AIProviderCapabilities(true, true, true, true, true),
                Profiles =
                [
                    new ProviderProfileBinding { ProfileId = "planner-high-context", Role = "planner", Model = ResolveOpenAiProfileModel("CARVES_OPENAI_PLANNER_MODEL"), Description = "High-context planning profile." },
                    new ProviderProfileBinding { ProfileId = "worker-codegen-fast", Role = "worker", Model = ResolveOpenAiProfileModel("CARVES_OPENAI_WORKER_MODEL"), Description = "Fast code generation profile." },
                    new ProviderProfileBinding { ProfileId = "review-structured", Role = "review", Model = ResolveOpenAiProfileModel("CARVES_OPENAI_REVIEW_MODEL"), Description = "Structured review profile." },
                ],
                TimeoutSeconds = 30,
                RetryLimit = 1,
                PermittedRepoScopes = ["*"],
            },
            new ProviderDescriptor
            {
                ProviderId = "claude",
                DisplayName = "Anthropic Claude Messages",
                SecretEnvironmentVariable = "ANTHROPIC_API_KEY",
                Capabilities = new AIProviderCapabilities(true, true, true, true, false),
                Profiles =
                [
                    new ProviderProfileBinding { ProfileId = "claude-planner-high-context", Role = "planner", Model = "claude-sonnet-4-5", Description = "High-context Claude planning profile." },
                    new ProviderProfileBinding { ProfileId = "claude-worker-bounded", Role = "worker", Model = "claude-sonnet-4-5", Description = "Bounded Claude worker profile for explicit runtime qualification lanes." },
                ],
                TimeoutSeconds = 30,
                RetryLimit = 1,
                PermittedRepoScopes = ["*"],
            },
            new ProviderDescriptor
            {
                ProviderId = "codex",
                DisplayName = "OpenAI Codex",
                SecretEnvironmentVariable = "OPENAI_API_KEY",
                Capabilities = new AIProviderCapabilities(false, true, true, true, true),
                Profiles =
                [
                    new ProviderProfileBinding
                    {
                        ProfileId = "codex-worker-trusted",
                        Role = "worker",
                        Model = "gpt-5-codex",
                        Description = "Trusted Codex SDK worker profile.",
                    },
                    new ProviderProfileBinding
                    {
                        ProfileId = "codex-worker-local-cli",
                        Role = "worker",
                        Model = ResolveCodexCliProfileModel(),
                        Description = "Trusted local Codex CLI worker profile.",
                    },
                ],
                TimeoutSeconds = 120,
                RetryLimit = 2,
                PermittedRepoScopes = ["*"],
            },
            new ProviderDescriptor
            {
                ProviderId = "gemini",
                DisplayName = "Google Gemini API",
                SecretEnvironmentVariable = "GEMINI_API_KEY",
                Capabilities = new AIProviderCapabilities(true, false, true, true, false),
                Profiles =
                [
                    new ProviderProfileBinding { ProfileId = "gemini-planner-high-context", Role = "planner", Model = "gemini-2.5-pro", Description = "High-context Gemini planning profile." },
                    new ProviderProfileBinding { ProfileId = "gemini-worker-balanced", Role = "worker", Model = "gemini-2.5-pro", Description = "Balanced Gemini worker profile." },
                ],
                TimeoutSeconds = 45,
                RetryLimit = 1,
                PermittedRepoScopes = ["*"],
            },
            new ProviderDescriptor
            {
                ProviderId = "local",
                DisplayName = "Local Agent Host",
                SecretEnvironmentVariable = string.Empty,
                Capabilities = new AIProviderCapabilities(false, true, false, true, false),
                Profiles =
                [
                    new ProviderProfileBinding { ProfileId = "local-agent-worker", Role = "worker", Model = "local-agent", Description = "Local agent worker profile." },
                ],
                TimeoutSeconds = 20,
                RetryLimit = 0,
                PermittedRepoScopes = ["*"],
            },
        ];
    }

    private static IReadOnlyList<WorkerBackendDescriptor> BuildWorkerBackends(
        ProviderDescriptor provider,
        WorkerAdapterRegistry workerAdapterRegistry,
        IReadOnlyList<string> allProfiles)
    {
        var defaultBackends = GetDefaultBackendDefinitions(provider.ProviderId);
        return defaultBackends
            .Select(definition =>
            {
                var adapter = workerAdapterRegistry.TryGetByBackendId(definition.BackendId);
                var capabilities = adapter?.GetCapabilities() ?? definition.Capabilities;
                var health = adapter?.CheckHealth() ?? new WorkerBackendHealthSummary
                {
                    State = WorkerBackendHealthState.Disabled,
                    Summary = $"No runtime adapter is registered for backend '{definition.BackendId}'.",
                };

                var compatibleTrustProfiles = definition.CompatibleTrustProfiles.Count == 0
                    ? allProfiles
                    : definition.CompatibleTrustProfiles;

                return new WorkerBackendDescriptor
                {
                    BackendId = definition.BackendId,
                    ProviderId = provider.ProviderId,
                    AdapterId = adapter?.AdapterId ?? definition.AdapterId,
                    DisplayName = definition.DisplayName,
                    RoutingIdentity = definition.RoutingIdentity,
                    ProtocolFamily = definition.ProtocolFamily,
                    RequestFamily = definition.RequestFamily,
                    RoutingProfiles = definition.RoutingProfiles,
                    CompatibleTrustProfiles = compatibleTrustProfiles,
                    Capabilities = capabilities,
                    Health = health,
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<WorkerBackendDescriptor> GetDefaultBackendDefinitions(string providerId)
    {
        return providerId switch
        {
            "openai" =>
            [
                new WorkerBackendDescriptor
                {
                    BackendId = "openai_api",
                    ProviderId = "openai",
                    AdapterId = "OpenAiWorkerAdapter",
                    DisplayName = "OpenAI API Worker",
                    RoutingIdentity = "openai.worker",
                    ProtocolFamily = "openai_compatible",
                    RequestFamily = "responses_api",
                    RoutingProfiles = ["worker-codegen-fast"],
                    CompatibleTrustProfiles = ["sandbox_readonly", "workspace_safe_write", "workspace_build_test"],
                    Capabilities = new WorkerProviderCapabilities
                    {
                        SupportsExecution = true,
                        SupportsEventStream = true,
                        SupportsHealthProbe = true,
                        SupportsCancellation = false,
                        SupportsTrustedProfiles = false,
                        SupportsNetworkAccess = true,
                        SupportsDotNetBuild = true,
                        SupportsJsonMode = true,
                        SupportsSystemPrompt = true,
                    },
                },
            ],
            "claude" =>
            [
                new WorkerBackendDescriptor
                {
                    BackendId = "claude_api",
                    ProviderId = "claude",
                    AdapterId = "ClaudeWorkerAdapter",
                    DisplayName = "Claude API Worker",
                    RoutingIdentity = "claude.worker",
                    ProtocolFamily = "anthropic_native",
                    RequestFamily = "messages_api",
                    RoutingProfiles = ["claude-worker-bounded"],
                    CompatibleTrustProfiles = ["sandbox_readonly", "workspace_safe_write", "workspace_build_test"],
                    Capabilities = new WorkerProviderCapabilities
                    {
                        SupportsExecution = true,
                        SupportsHealthProbe = true,
                        SupportsNetworkAccess = true,
                        SupportsDotNetBuild = true,
                        SupportsJsonMode = false,
                        SupportsSystemPrompt = true,
                    },
                },
            ],
            "codex" =>
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
                    RoutingProfiles = ["codex-worker-local-cli"],
                    CompatibleTrustProfiles = ["workspace_safe_write", "workspace_build_test", "extended_dev_ops", "operator_approved_elevated"],
                    Capabilities = new WorkerProviderCapabilities
                    {
                        SupportsExecution = true,
                        SupportsEventStream = true,
                        SupportsHealthProbe = true,
                        SupportsCancellation = false,
                        SupportsTrustedProfiles = true,
                        SupportsNetworkAccess = true,
                        SupportsDotNetBuild = true,
                        SupportsLongRunningTasks = true,
                    },
                },
                new WorkerBackendDescriptor
                {
                    BackendId = "codex_sdk",
                    ProviderId = "codex",
                    AdapterId = "CodexWorkerAdapter",
                    DisplayName = "Codex SDK Worker",
                    RoutingIdentity = "codex.worker",
                    ProtocolFamily = "sdk_bridge",
                    RequestFamily = "codex_sdk",
                    RoutingProfiles = ["codex-worker-trusted"],
                    CompatibleTrustProfiles = ["workspace_safe_write", "workspace_build_test", "extended_dev_ops", "operator_approved_elevated"],
                    Capabilities = new WorkerProviderCapabilities
                    {
                        SupportsExecution = true,
                        SupportsEventStream = true,
                        SupportsHealthProbe = true,
                        SupportsCancellation = false,
                        SupportsTrustedProfiles = true,
                        SupportsNetworkAccess = true,
                        SupportsDotNetBuild = true,
                        SupportsLongRunningTasks = true,
                    },
                },
            ],
            "gemini" =>
            [
                new WorkerBackendDescriptor
                {
                    BackendId = "gemini_api",
                    ProviderId = "gemini",
                    AdapterId = "GeminiWorkerAdapter",
                    DisplayName = "Gemini API Worker",
                    RoutingIdentity = "gemini.worker",
                    ProtocolFamily = "gemini_native",
                    RequestFamily = "generate_content",
                    RoutingProfiles = ["gemini-worker-balanced"],
                    CompatibleTrustProfiles = ["sandbox_readonly", "workspace_safe_write", "workspace_build_test"],
                    Capabilities = new WorkerProviderCapabilities
                    {
                        SupportsExecution = true,
                        SupportsHealthProbe = true,
                        SupportsNetworkAccess = true,
                        SupportsJsonMode = true,
                        SupportsSystemPrompt = true,
                    },
                },
            ],
            "local" =>
            [
                new WorkerBackendDescriptor
                {
                    BackendId = "local_agent",
                    ProviderId = "local",
                    AdapterId = "LocalLlmWorkerAdapter",
                    DisplayName = "Local Agent Worker",
                    RoutingIdentity = "local.worker",
                    ProtocolFamily = "local_bridge",
                    RequestFamily = "local_agent",
                    RoutingProfiles = ["local-agent-worker"],
                    CompatibleTrustProfiles = ["sandbox_readonly", "workspace_safe_write", "workspace_build_test"],
                    Capabilities = new WorkerProviderCapabilities
                    {
                        SupportsHealthProbe = true,
                        SupportsDotNetBuild = true,
                    },
                },
            ],
            "null" =>
            [
                new WorkerBackendDescriptor
                {
                    BackendId = "null_worker",
                    ProviderId = "null",
                    AdapterId = "NullWorkerAdapter",
                    DisplayName = "Null Worker",
                    RoutingIdentity = "null.worker",
                    ProtocolFamily = "null",
                    RequestFamily = "none",
                    RoutingProfiles = ["default"],
                    CompatibleTrustProfiles = ["sandbox_readonly", "workspace_safe_write", "workspace_build_test"],
                    Capabilities = new WorkerProviderCapabilities
                    {
                        SupportsHealthProbe = true,
                    },
                },
            ],
            _ => Array.Empty<WorkerBackendDescriptor>(),
        };
    }

    private static string ResolveOpenAiProfileModel(string specificVariable)
    {
        return ResolveEnvironmentValue(specificVariable, "CARVES_OPENAI_MODEL") ?? "gpt-5-mini";
    }

    private static string ResolveCodexCliProfileModel()
    {
        return ResolveEnvironmentValue("CARVES_CODEX_CLI_MODEL") ?? "codex-cli";
    }

    private static string? ResolveEnvironmentValue(params string[] variableNames)
    {
        foreach (var variableName in variableNames)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                continue;
            }

            var processValue = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(processValue))
            {
                return processValue;
            }

            var userValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(userValue))
            {
                return userValue;
            }

            var machineValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(machineValue))
            {
                return machineValue;
            }
        }

        return null;
    }
}
