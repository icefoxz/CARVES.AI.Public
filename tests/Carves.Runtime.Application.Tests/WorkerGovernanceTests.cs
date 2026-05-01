using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class WorkerGovernanceTests
{
    [Fact]
    public void ProviderRegistryService_ProjectsWorkerBackendsWithTrustAndHealth()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoWorkerRegistry");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-worker-registry", "codex-worker-trusted", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var registry = new ProviderRegistryService(
            new JsonProviderRegistryRepository(workspace.Paths),
            repoRegistry,
            governance,
            TestWorkerAdapterRegistryFactory.Create("codex"));

        var backends = registry.ListWorkerBackends();
        var codex = registry.InspectWorkerBackend("codex_sdk");
        var codexCli = registry.InspectWorkerBackend("codex_cli");
        var gemini = registry.InspectWorkerBackend("gemini_api");

        Assert.Contains(backends, item => item.BackendId == "openai_api");
        Assert.Contains(backends, item => item.BackendId == "local_agent");
        Assert.Contains(backends, item => item.BackendId == "codex_cli");
        Assert.Contains("workspace_build_test", codex.CompatibleTrustProfiles);
        Assert.Contains("extended_dev_ops", codex.CompatibleTrustProfiles);
        Assert.Contains("codex-worker-local-cli", codexCli.RoutingProfiles);
        Assert.NotEqual(WorkerBackendHealthState.Disabled, codex.Health.State);
        Assert.Equal(WorkerBackendHealthState.Unavailable, gemini.Health.State);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.PlatformProvidersRoot, "codex.json")));
    }

    [Fact]
    public void WorkerSelectionPolicyService_UsesFallbackWhenRequestedBackendIsUnavailable()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoWorkerSelection");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-worker-selection", "gemini-worker-balanced", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var selection = new WorkerSelectionPolicyService(
            workspace.RootPath,
            repoRegistry,
            providers,
            routing,
            governance,
            adapters,
            boundary,
            new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters));

        var decision = selection.Evaluate(repoId: "repo-worker-selection");

        Assert.True(decision.Allowed);
        Assert.True(decision.UsedFallback);
        Assert.Equal("workspace_build_test", decision.RequestedTrustProfileId);
        Assert.True(new[] { "codex_cli", "codex_sdk" }.Contains(decision.SelectedBackendId, StringComparer.Ordinal));
        Assert.Equal("codex", decision.SelectedProviderId);
        Assert.Contains(decision.Candidates, candidate => candidate.BackendId == "gemini_api" && !candidate.Selected);
    }

    [Fact]
    public void WorkerSelectionPolicyService_UsesNullWorkerForLocalUnregisteredRepoWhenProviderIsNull()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("null");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var selection = new WorkerSelectionPolicyService(
            workspace.RootPath,
            repoRegistry,
            providers,
            routing,
            governance,
            adapters,
            boundary,
            new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters));
        var task = new Carves.Runtime.Domain.Tasks.TaskNode
        {
            TaskId = "T-LOCAL-NULL",
            Title = "Local null worker task",
            TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
            Validation = new Carves.Runtime.Domain.Tasks.ValidationPlan
            {
                Commands =
                [
                    ["dotnet", "--version"],
                ],
            },
        };

        var decision = selection.Evaluate(task);

        Assert.True(decision.Allowed);
        Assert.False(decision.UsedFallback);
        Assert.Equal("null_worker", decision.SelectedBackendId);
        Assert.Equal("null", decision.SelectedProviderId);
        Assert.Equal("workspace_build_test", decision.RequestedTrustProfileId);
    }

    [Fact]
    public void WorkerSelectionPolicyService_AppliesExternalCodexPolicyForLocalDelegatedTaskRun()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalCliModel = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_MODEL");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_MODEL", "codex-cli");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
            var runtimePolicyBundleService = new RuntimePolicyBundleService(workspace.Paths, governance, operationalPolicyService, providers);
            File.WriteAllText(workspace.Paths.PlatformWorkerSelectionPolicyFile, """
{
  "version": "1.0",
  "preferred_backend_id": "codex_cli",
  "default_trust_profile_id": "workspace_build_test",
  "allow_routing_fallback": true,
  "fallback_backend_ids": ["null_worker"],
  "allowed_backend_ids": ["codex_cli", "null_worker"]
}
""");
            var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance, operationalPolicyService, runtimePolicyBundleService);
            var selection = new WorkerSelectionPolicyService(
                workspace.RootPath,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
                operationalPolicyService,
                runtimePolicyBundleService);
            var task = new Carves.Runtime.Domain.Tasks.TaskNode
            {
                TaskId = "T-LOCAL-CODEX-CLI",
                Title = "Local Codex CLI delegated task",
                TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
                Scope = ["docs/runtime/"],
                Validation = new Carves.Runtime.Domain.Tasks.ValidationPlan
                {
                    Commands =
                    [
                        ["dotnet", "--version"],
                    ],
                },
            };

            var decision = selection.Evaluate(task, repoId: null);

            Assert.True(decision.Allowed);
            Assert.Equal("codex_cli", decision.SelectedBackendId);
            Assert.Equal("codex", decision.SelectedProviderId);
            Assert.Equal("CodexCliWorkerAdapter", decision.SelectedAdapterId);
            Assert.Equal("codex-cli", decision.SelectedModelId);
            Assert.Contains(decision.Candidates, candidate =>
                candidate.BackendId == "null_worker"
                && !candidate.Selected
                && candidate.Eligibility == RouteEligibilityStatus.Eligible);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_MODEL", originalCliModel);
        }
    }

    [Fact]
    public void WorkerSelectionPolicyService_PrefersActiveProviderForLocalRepoWhenOperationalPolicyPointsAtDifferentProvider()
    {
        using var workspace = new TemporaryWorkspace();
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("openai");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
            var operationalPolicyService = new WorkerOperationalPolicyService(
                workspace.RootPath,
                repoRegistry,
                WorkerOperationalPolicy.CreateDefault() with
                {
                    PreferredBackendId = "codex_cli",
                });
            var selection = new WorkerSelectionPolicyService(
                workspace.RootPath,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
                operationalPolicyService);

            var decision = selection.Evaluate(repoId: null);

            Assert.True(decision.Allowed);
            Assert.Equal("openai_api", decision.SelectedBackendId);
            Assert.Equal("openai", decision.SelectedProviderId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void WorkerSelectionPolicyService_PrefersCodexCliForPatchCapableExecutionWhenRemoteApiIsAlsoAvailable()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("openai");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
            var selection = new WorkerSelectionPolicyService(
                workspace.RootPath,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters));
            var task = new Carves.Runtime.Domain.Tasks.TaskNode
            {
                TaskId = "T-MATERIALIZED-CODEX-CLI",
                Title = "Materialized code task",
                TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
                Scope = ["tests/Ordering.FunctionalTests/OrderingApiTests.cs"],
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["routing_intent"] = "patch_draft",
                    ["module_id"] = "Execution/ResultEnvelope",
                },
                Validation = new Carves.Runtime.Domain.Tasks.ValidationPlan
                {
                    Commands =
                    [
                        ["dotnet", "test"],
                    ],
                },
            };

            var decision = selection.Evaluate(
                task,
                repoId: null,
                options: new WorkerSelectionOptions
                {
                    RequestedBackendOverride = "claude_api",
                });

            Assert.True(decision.Allowed);
            Assert.Equal("codex_cli", decision.SelectedBackendId);
            Assert.Equal("codex", decision.SelectedProviderId);
            Assert.Equal("codex-worker-local-cli", decision.SelectedRoutingProfileId);
            Assert.Contains(decision.Candidates, candidate =>
                candidate.BackendId == "openai_api"
                && candidate.Eligibility == RouteEligibilityStatus.Unsupported
                && candidate.Reason.Contains("materialized patch/result submission", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void WorkerSelectionPolicyService_RejectsClaudeForUnqualifiedPatchDraftLane()
    {
        using var workspace = new TemporaryWorkspace();
        var originalApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-claude-key");
        try
        {
            var managedRepo = CreateManagedRepo(workspace, "RepoClaudePatch");
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            repoRegistry.Register(managedRepo, "repo-claude-patch", "claude-worker-bounded", "balanced");
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("claude");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var boundary = new WorkerExecutionBoundaryService(managedRepo, repoRegistry, governance);
            var selection = new WorkerSelectionPolicyService(
                managedRepo,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters));
            var task = new Carves.Runtime.Domain.Tasks.TaskNode
            {
                TaskId = "T-CLAUDE-PATCH-DRAFT",
                Title = "Claude patch draft task",
                TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
                Scope = ["src/CARVES.Runtime.Host/Program.cs"],
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["routing_intent"] = "patch_draft",
                    ["module_id"] = "src/CARVES.Runtime.Host/Program.cs",
                },
                Validation = new Carves.Runtime.Domain.Tasks.ValidationPlan
                {
                    Commands =
                    [
                        ["dotnet", "--version"],
                    ],
                },
            };

            var decision = selection.Evaluate(
                task,
                repoId: "repo-claude-patch",
                options: new WorkerSelectionOptions
                {
                    RequestedBackendOverride = "claude_api",
                });

            Assert.NotEqual("claude_api", decision.SelectedBackendId);
            Assert.Contains(decision.Candidates, candidate =>
                candidate.BackendId == "claude_api"
                && candidate.Eligibility == RouteEligibilityStatus.Unsupported
                && !candidate.CapabilityCompatible);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void WorkerSelectionPolicyService_AllowsGeminiStructuredOutputButRejectsPatchDraft()
    {
        using var workspace = new TemporaryWorkspace();
        var originalApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-gemini-key");
        try
        {
            var managedRepo = CreateManagedRepo(workspace, "RepoGeminiQualified");
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            repoRegistry.Register(managedRepo, "repo-gemini-qualified", "gemini-worker-balanced", "balanced");
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("gemini");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var boundary = new WorkerExecutionBoundaryService(managedRepo, repoRegistry, governance);
            var selection = new WorkerSelectionPolicyService(
                managedRepo,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters));
            var structuredTask = new Carves.Runtime.Domain.Tasks.TaskNode
            {
                TaskId = "T-GEMINI-STRUCTURED",
                TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["routing_intent"] = "structured_output",
                    ["module_id"] = "Execution/ResultEnvelope",
                },
            };
            var patchTask = new Carves.Runtime.Domain.Tasks.TaskNode
            {
                TaskId = "T-GEMINI-PATCH",
                TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
                Scope = ["src/Sample.cs"],
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["routing_intent"] = "patch_draft",
                    ["module_id"] = "Execution/ResultEnvelope",
                },
                Validation = new Carves.Runtime.Domain.Tasks.ValidationPlan
                {
                    Commands =
                    [
                        ["dotnet", "test"],
                    ],
                },
            };

            var structuredDecision = selection.Evaluate(
                structuredTask,
                repoId: "repo-gemini-qualified",
                options: new WorkerSelectionOptions
                {
                    RequestedBackendOverride = "gemini_api",
                });
            var patchDecision = selection.Evaluate(
                patchTask,
                repoId: "repo-gemini-qualified",
                options: new WorkerSelectionOptions
                {
                    RequestedBackendOverride = "gemini_api",
                });

            Assert.True(structuredDecision.Allowed);
            Assert.Equal("gemini_api", structuredDecision.SelectedBackendId);
            Assert.False(patchDecision.Candidates.Single(candidate => candidate.BackendId == "gemini_api").CapabilityCompatible);
            Assert.DoesNotContain(patchDecision.SelectedBackendId, new[] { "gemini_api" });
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void WorkerSelectionPolicyService_SelectsClaudeForQualifiedReviewLaneWhenExplicitlyRequested()
    {
        using var workspace = new TemporaryWorkspace();
        var originalApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-claude-key");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("claude");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
            var selection = new WorkerSelectionPolicyService(
                workspace.RootPath,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters));
            var task = new Carves.Runtime.Domain.Tasks.TaskNode
            {
                TaskId = "T-CLAUDE-REVIEW-SUMMARY",
                Title = "Claude review summary task",
                TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
                Scope = [".ai/STATE.md"],
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["routing_intent"] = "review_summary",
                    ["module_id"] = ".ai/STATE.md",
                },
                Validation = new Carves.Runtime.Domain.Tasks.ValidationPlan
                {
                    Commands =
                    [
                        ["dotnet", "--version"],
                    ],
                },
            };

            var decision = selection.Evaluate(
                task,
                repoId: null,
                options: new WorkerSelectionOptions
                {
                    RequestedBackendOverride = "claude_api",
                });

            Assert.True(decision.Allowed);
            Assert.Equal("claude_api", decision.SelectedBackendId);
            Assert.Equal("claude", decision.SelectedProviderId);
            Assert.Equal("claude-worker-bounded", decision.SelectedRoutingProfileId);
            Assert.Contains(decision.Candidates, candidate =>
                candidate.BackendId == "claude_api"
                && candidate.Selected
                && candidate.CapabilityCompatible
                && candidate.ProfileCompatible);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void WorkerSelectionPolicyService_UsesDefaultUntrustedProfileForLocalRepoWhenRuntimeSelectionPolicyWouldPromoteTrustedDefault()
    {
        using var workspace = new TemporaryWorkspace();
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("openai");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var operationalPolicyService = new WorkerOperationalPolicyService(
                workspace.RootPath,
                repoRegistry,
                WorkerOperationalPolicy.CreateDefault() with
                {
                    PreferredBackendId = "codex_cli",
                    PreferredTrustProfileId = "extended_dev_ops",
                });
            var runtimePolicyBundleService = new RuntimePolicyBundleService(workspace.Paths, governance, operationalPolicyService, providers);
            var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance, operationalPolicyService, runtimePolicyBundleService);
            var selection = new WorkerSelectionPolicyService(
                workspace.RootPath,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
                operationalPolicyService,
                runtimePolicyBundleService);

            var decision = selection.Evaluate(repoId: null);

            Assert.True(decision.Allowed);
            Assert.Equal("workspace_build_test", decision.RequestedTrustProfileId);
            Assert.Equal("openai_api", decision.SelectedBackendId);
            Assert.Equal("openai", decision.SelectedProviderId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void WorkerSelectionPolicyService_OverridesRemoteApiPatchRouteWithCodexCliForMaterializedExecution()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoRoutingMaterializedCodexCli");
        var cliPath = CreateFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            repoRegistry.Register(managedRepo, "repo-routing-materialized-codex-cli", "worker-codegen-fast", "balanced");
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("openai");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
            var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
            var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
            routingRepository.SaveActive(new RuntimeRoutingProfile
            {
                ProfileId = "profile-routing-materialized-codex-cli",
                Rules =
                [
                    new RuntimeRoutingRule
                    {
                        RuleId = "rule-patch-draft-remote-api",
                        RoutingIntent = "patch_draft",
                        ModuleId = "Execution/ResultEnvelope",
                        Summary = "Legacy remote-api route for patch drafts.",
                        PreferredRoute = new RuntimeRoutingRoute
                        {
                            ProviderId = "openai",
                            BackendId = "openai_api",
                            RoutingProfileId = "worker-codegen-fast",
                            RequestFamily = "responses_api",
                            Model = "gpt-5.4",
                        },
                    },
                ],
            });
            var selection = new WorkerSelectionPolicyService(
                workspace.RootPath,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
                operationalPolicyService,
                runtimeRoutingProfileService: new RuntimeRoutingProfileService(routingRepository));
            var task = new Carves.Runtime.Domain.Tasks.TaskNode
            {
                TaskId = "T-REMOTE-PATCH-OVERRIDE",
                Title = "Remote patch route override",
                TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
                Scope = ["src/runtime/result-envelope"],
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["routing_intent"] = "patch_draft",
                    ["module_id"] = "Execution/ResultEnvelope",
                },
                Validation = new Carves.Runtime.Domain.Tasks.ValidationPlan
                {
                    Commands =
                    [
                        ["dotnet", "test"],
                    ],
                },
            };

            var decision = selection.Evaluate(task, repoId: "repo-routing-materialized-codex-cli");

            Assert.True(decision.Allowed);
            Assert.Equal("codex_cli", decision.SelectedBackendId);
            Assert.Equal("codex-worker-local-cli", decision.SelectedRoutingProfileId);
            Assert.Equal("profile-routing-materialized-codex-cli", decision.ActiveRoutingProfileId);
            Assert.Equal("rule-patch-draft-remote-api", decision.AppliedRoutingRuleId);
            Assert.Equal("active_profile_overridden", decision.RouteSource);
            Assert.Equal(RouteEligibilityStatus.Unsupported, decision.PreferredRouteEligibility);
            Assert.Contains("materialized patch/result submission", decision.PreferredIneligibilityReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void WorkerSelectionPolicyService_SelectsCodexCliWhenRepoUsesLocalCliProfile()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoWorkerCliSelection");
        var cliPath = CreateFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalCliModel = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_MODEL");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_MODEL", "codex-cli");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            repoRegistry.Register(managedRepo, "repo-worker-cli-selection", "codex-worker-local-cli", "balanced");
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
            var selection = new WorkerSelectionPolicyService(
                workspace.RootPath,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters));

            var decision = selection.Evaluate(repoId: "repo-worker-cli-selection");

            Assert.True(decision.Allowed);
            Assert.False(decision.UsedFallback);
            Assert.Equal("codex_cli", decision.SelectedBackendId);
            Assert.Equal("codex", decision.SelectedProviderId);
            Assert.Equal("codex-worker-local-cli", decision.SelectedRoutingProfileId);
            Assert.Equal("codex-cli", decision.SelectedModelId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_MODEL", originalCliModel);
        }
    }

    [Fact]
    public void WorkerSelectionPolicyService_ReconcilesMissingQuotaEntriesForCodexCliProfiles()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoWorkerCliQuotaRepair");
        var cliPath = CreateFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            repoRegistry.Register(managedRepo, "repo-worker-cli-quota", "codex-worker-local-cli", "balanced");
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();

            var quotaRepository = new JsonProviderQuotaRepository(workspace.Paths);
            quotaRepository.Save(new ProviderQuotaSnapshot
            {
                Entries =
                [
                    new ProviderQuotaEntry
                    {
                        ProfileId = "default",
                        UsedThisHour = 0,
                        LimitPerHour = 100,
                        WindowStartedAt = DateTimeOffset.UtcNow,
                    },
                ],
            });

            var routing = new ProviderRoutingService(providers, repoRegistry, governance, quotaRepository);
            var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
            var selection = new WorkerSelectionPolicyService(
                workspace.RootPath,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters));

            var decision = selection.Evaluate(repoId: "repo-worker-cli-quota");
            var reconciledSnapshot = quotaRepository.Load();

            Assert.True(decision.Allowed);
            Assert.Equal("codex_cli", decision.SelectedBackendId);
            Assert.Contains(reconciledSnapshot.Entries, entry => string.Equals(entry.ProfileId, "codex-worker-local-cli", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void RuntimePolicyBundleService_PersistsDefaultPolicies_AndExternalizedSelectionControlsBackendAndTrustProfile()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var runtimePolicyBundleService = new RuntimePolicyBundleService(workspace.Paths, governance, operationalPolicyService, providers);

        File.WriteAllText(workspace.Paths.PlatformWorkerSelectionPolicyFile, """
{
  "version": "1.0",
  "preferred_backend_id": "codex_cli",
  "default_trust_profile_id": "extended_dev_ops",
  "allow_routing_fallback": true,
  "fallback_backend_ids": ["codex_sdk", "null_worker"],
  "allowed_backend_ids": ["codex_cli", "codex_sdk", "null_worker"]
}
""");

        var validation = runtimePolicyBundleService.Validate();
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance, operationalPolicyService, runtimePolicyBundleService);
        var selection = new WorkerSelectionPolicyService(
            workspace.RootPath,
            repoRegistry,
            providers,
            new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths)),
            governance,
            adapters,
            boundary,
            new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
            operationalPolicyService,
            runtimePolicyBundleService);

        var decision = selection.Evaluate(repoId: null);
        var bundle = runtimePolicyBundleService.Load();

        Assert.True(File.Exists(workspace.Paths.PlatformDelegationPolicyFile));
        Assert.True(File.Exists(workspace.Paths.PlatformApprovalPolicyFile));
        Assert.True(File.Exists(workspace.Paths.PlatformRoleGovernancePolicyFile));
        Assert.True(File.Exists(workspace.Paths.PlatformWorkerSelectionPolicyFile));
        Assert.True(File.Exists(workspace.Paths.PlatformTrustProfilesFile));
        Assert.True(File.Exists(workspace.Paths.PlatformHostInvokePolicyFile));
        Assert.True(File.Exists(workspace.Paths.PlatformGovernanceContinuationGatePolicyFile));
        Assert.True(validation.IsValid);
        Assert.Equal(RoleGovernanceRuntimePolicy.DisabledMode, bundle.RoleGovernance.RoleMode);
        Assert.False(bundle.RoleGovernance.ControlledModeDefault);
        Assert.False(bundle.RoleGovernance.PlannerWorkerSplitEnabled);
        Assert.False(bundle.RoleGovernance.WorkerDelegationEnabled);
        Assert.False(bundle.RoleGovernance.SchedulerAutoDispatchEnabled);
        var roleGovernanceJson = File.ReadAllText(workspace.Paths.PlatformRoleGovernancePolicyFile);
        Assert.Contains("\"role_mode\": \"disabled\"", roleGovernanceJson, StringComparison.Ordinal);
        Assert.DoesNotContain("role_mode_disabled", roleGovernanceJson, StringComparison.Ordinal);
        Assert.True(bundle.HostInvoke.ControlPlaneMutation.UseAcceptedOperationPolling);
        Assert.Equal(15, bundle.HostInvoke.ControlPlaneMutation.BaseWaitSeconds);
        Assert.Equal(45, bundle.HostInvoke.ControlPlaneMutation.MaxWaitSeconds);
        Assert.True(bundle.GovernanceContinuationGate.HoldContinuationWithoutQualifyingDelta);
        Assert.Contains("file_too_large", bundle.GovernanceContinuationGate.ClosureBlockingBacklogKinds);
        Assert.Contains(decision.SelectedBackendId, new[] { "codex_cli", "codex_sdk" });
        Assert.Equal("workspace_build_test", decision.RequestedTrustProfileId);
    }

    [Fact]
    public void RuntimePolicyBundleService_LoadsLegacyRoleGovernancePolicyAsDisabledKillSwitch()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var providers = new ProviderRegistryService(
            new JsonProviderRegistryRepository(workspace.Paths),
            repoRegistry,
            governance,
            TestWorkerAdapterRegistryFactory.Create("codex"));
        providers.List();
        var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var runtimePolicyBundleService = new RuntimePolicyBundleService(workspace.Paths, governance, operationalPolicyService, providers);

        File.WriteAllText(workspace.Paths.PlatformRoleGovernancePolicyFile, """
{
  "version": "1.0",
  "controlled_mode_default": true,
  "producer_cannot_self_approve": true,
  "reviewer_cannot_approve_same_task": true,
  "default_role_binding": {
    "producer": "planner",
    "executor": "worker",
    "reviewer": "planner",
    "approver": "operator",
    "scope_steward": "operator",
    "policy_owner": "operator"
  },
  "validation_lab_follow_on_lanes": [
    "approval_recovery",
    "controlled_mode_governance"
  ]
}
""");

        var validation = runtimePolicyBundleService.Validate();
        var policy = runtimePolicyBundleService.LoadRoleGovernancePolicy();

        Assert.True(validation.IsValid);
        Assert.Contains(validation.Warnings, warning => warning.Contains("not enabled", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(RoleGovernanceRuntimePolicy.DisabledMode, policy.RoleMode);
        Assert.False(policy.ControlledModeDefault);
        Assert.False(policy.PlannerWorkerSplitEnabled);
        Assert.False(policy.WorkerDelegationEnabled);
        Assert.False(policy.SchedulerAutoDispatchEnabled);
    }

    [Fact]
    public void RuntimeRoleModeExecutionGate_RejectsDelegatedExecutionWhenRoleModeIsDisabled()
    {
        var decision = RuntimeRoleModeExecutionGate.EvaluateDelegatedExecution(
            RoleGovernanceRuntimePolicy.CreateDefault() with
            {
                PlannerWorkerSplitEnabled = true,
                WorkerDelegationEnabled = true,
            });

        Assert.False(decision.Allowed);
        Assert.Equal("role_mode_disabled", decision.Outcome);
        Assert.Contains(decision.Guidance, item => item.Contains("before worker lease", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RuntimeRoleModeExecutionGate_RequiresPlannerWorkerSplitAndWorkerDelegation()
    {
        var splitDisabled = RuntimeRoleModeExecutionGate.EvaluateDelegatedExecution(
            RoleGovernanceRuntimePolicy.CreateDefault() with
            {
                RoleMode = RoleGovernanceRuntimePolicy.EnabledMode,
                PlannerWorkerSplitEnabled = false,
                WorkerDelegationEnabled = true,
            });
        var delegationDisabled = RuntimeRoleModeExecutionGate.EvaluateDelegatedExecution(
            RoleGovernanceRuntimePolicy.CreateDefault() with
            {
                RoleMode = RoleGovernanceRuntimePolicy.EnabledMode,
                PlannerWorkerSplitEnabled = true,
                WorkerDelegationEnabled = false,
            });
        var enabled = RuntimeRoleModeExecutionGate.EvaluateDelegatedExecution(
            RoleGovernanceRuntimePolicy.CreateDefault() with
            {
                RoleMode = RoleGovernanceRuntimePolicy.EnabledMode,
                PlannerWorkerSplitEnabled = true,
                WorkerDelegationEnabled = true,
            });

        Assert.False(splitDisabled.Allowed);
        Assert.Equal("planner_worker_split_disabled", splitDisabled.Outcome);
        Assert.False(delegationDisabled.Allowed);
        Assert.Equal("worker_delegation_disabled", delegationDisabled.Outcome);
        Assert.True(enabled.Allowed);
        Assert.Equal("delegated_execution_enabled", enabled.Outcome);
    }

    [Fact]
    public void RuntimeRoleModeExecutionGate_RequiresSchedulerAutoDispatchForHostScheduling()
    {
        var disabled = RuntimeRoleModeExecutionGate.EvaluateSchedulerAutoDispatch(RoleGovernanceRuntimePolicy.CreateDefault());
        var schedulerDisabled = RuntimeRoleModeExecutionGate.EvaluateSchedulerAutoDispatch(
            RoleGovernanceRuntimePolicy.CreateDefault() with
            {
                RoleMode = RoleGovernanceRuntimePolicy.EnabledMode,
                PlannerWorkerSplitEnabled = true,
                WorkerDelegationEnabled = true,
                SchedulerAutoDispatchEnabled = false,
            });
        var enabled = RuntimeRoleModeExecutionGate.EvaluateSchedulerAutoDispatch(
            RoleGovernanceRuntimePolicy.CreateDefault() with
            {
                RoleMode = RoleGovernanceRuntimePolicy.EnabledMode,
                PlannerWorkerSplitEnabled = true,
                WorkerDelegationEnabled = true,
                SchedulerAutoDispatchEnabled = true,
            });

        Assert.False(disabled.Allowed);
        Assert.Equal("role_mode_disabled", disabled.Outcome);
        Assert.False(schedulerDisabled.Allowed);
        Assert.Equal("scheduler_auto_dispatch_disabled", schedulerDisabled.Outcome);
        Assert.True(enabled.Allowed);
        Assert.Equal("scheduler_auto_dispatch_enabled", enabled.Outcome);
    }

    [Fact]
    public void RuntimeRoleModeExecutionGate_RequiresRoleAutomationForReviewAutoContinue()
    {
        var disabled = RuntimeRoleModeExecutionGate.EvaluateReviewAutoContinue(RoleGovernanceRuntimePolicy.CreateDefault());
        var schedulerDisabled = RuntimeRoleModeExecutionGate.EvaluateReviewAutoContinue(
            RoleGovernanceRuntimePolicy.CreateDefault() with
            {
                RoleMode = RoleGovernanceRuntimePolicy.EnabledMode,
                PlannerWorkerSplitEnabled = true,
                WorkerDelegationEnabled = true,
                SchedulerAutoDispatchEnabled = false,
            });
        var enabled = RuntimeRoleModeExecutionGate.EvaluateReviewAutoContinue(
            RoleGovernanceRuntimePolicy.CreateDefault() with
            {
                RoleMode = RoleGovernanceRuntimePolicy.EnabledMode,
                PlannerWorkerSplitEnabled = true,
                WorkerDelegationEnabled = true,
                SchedulerAutoDispatchEnabled = true,
            });

        Assert.False(disabled.Allowed);
        Assert.Equal("role_mode_disabled", disabled.Outcome);
        Assert.Contains("complete the current task", disabled.NextAction, StringComparison.OrdinalIgnoreCase);
        Assert.False(schedulerDisabled.Allowed);
        Assert.Equal("scheduler_auto_dispatch_disabled", schedulerDisabled.Outcome);
        Assert.True(enabled.Allowed);
        Assert.Equal("review_auto_continue_enabled", enabled.Outcome);
    }

    [Fact]
    public void WorkerSelectionPolicyService_RuntimePolicyForcesNullWorkerWhenExplicitRemoteBackendIsRequested()
    {
        using var workspace = new TemporaryWorkspace();
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("gemini");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var runtimePolicyBundleService = new RuntimePolicyBundleService(workspace.Paths, governance, operationalPolicyService, providers);
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance, operationalPolicyService, runtimePolicyBundleService);
        var selection = new WorkerSelectionPolicyService(
            workspace.RootPath,
            repoRegistry,
            providers,
            new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths)),
            governance,
            adapters,
            boundary,
            new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
            operationalPolicyService,
            runtimePolicyBundleService);

        var decision = selection.Evaluate(
            repoId: null,
            options: new WorkerSelectionOptions
            {
                RequestedBackendOverride = "gemini_api",
            });

        Assert.True(decision.Allowed);
        Assert.Equal("null_worker", decision.SelectedBackendId);
        Assert.Equal("null", decision.SelectedProviderId);
        Assert.Contains(decision.Candidates, candidate =>
            candidate.BackendId == "gemini_api"
            && !candidate.Selected
            && candidate.Reason.Contains("allowed_backends", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkerSelectionPolicyService_RuntimePolicyForcesNullWorkerWhenRoutingProfilePrefersRemoteBackend()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoNullWorkerOnlyRouting");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-null-worker-routing", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("gemini");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        routingRepository.SaveActive(new RuntimeRoutingProfile
        {
            ProfileId = "profile-remote-preferred",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-structured-output",
                    RoutingIntent = "structured_output",
                    Summary = "Prefer Gemini API for structured output.",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "gemini",
                        BackendId = "gemini_api",
                        RoutingProfileId = "gemini-worker-balanced",
                        RequestFamily = "generate_content",
                        Model = "gemini-2.5-pro",
                    },
                },
            ],
        });
        var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var runtimePolicyBundleService = new RuntimePolicyBundleService(workspace.Paths, governance, operationalPolicyService, providers);
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance, operationalPolicyService, runtimePolicyBundleService);
        var selection = new WorkerSelectionPolicyService(
            workspace.RootPath,
            repoRegistry,
            providers,
            new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths)),
            governance,
            adapters,
            boundary,
            new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
            operationalPolicyService,
            runtimePolicyBundleService,
            new RuntimeRoutingProfileService(routingRepository));

        var decision = selection.Evaluate(new Carves.Runtime.Domain.Tasks.TaskNode
        {
            TaskId = "T-NULL-WORKER-ONLY-ROUTING",
            Title = "Structured output task",
            TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "structured_output",
            },
        }, repoId: "repo-null-worker-routing");

        Assert.True(decision.Allowed);
        Assert.Equal("null_worker", decision.SelectedBackendId);
        Assert.Equal("profile-remote-preferred", decision.ActiveRoutingProfileId);
        Assert.Contains(decision.Candidates, candidate =>
            candidate.BackendId == "gemini_api"
            && !candidate.Selected
            && candidate.Reason.Contains("allowed_backends", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkerSelectionPolicyService_UsesActiveRoutingProfilePreferredRouteWhenRuleMatches()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoRoutingPreferred");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-routing-preferred", "codex-worker-trusted", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        routingRepository.SaveActive(new RuntimeRoutingProfile
        {
            ProfileId = "profile-routing-preferred",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-patch-draft",
                    RoutingIntent = "patch_draft",
                    ModuleId = "src/runtime/routing",
                    Summary = "Prefer codex SDK for routing patches.",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "codex",
                        BackendId = "codex_sdk",
                        RoutingProfileId = "codex-worker-trusted",
                        RequestFamily = "codex_sdk",
                        Model = "gpt-5-codex-routing",
                    },
                },
            ],
        });
        var selection = new WorkerSelectionPolicyService(
            workspace.RootPath,
            repoRegistry,
            providers,
            routing,
            governance,
            adapters,
            boundary,
            new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
            operationalPolicyService,
            runtimeRoutingProfileService: new RuntimeRoutingProfileService(routingRepository));

        var decision = selection.Evaluate(new Carves.Runtime.Domain.Tasks.TaskNode
        {
            TaskId = "T-ROUTING-PREFERRED",
            Title = "Route preferred task",
            TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
            Scope = ["src/runtime/routing"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "patch_draft",
                ["module_id"] = "src/runtime/routing",
            },
        }, repoId: "repo-routing-preferred");

        Assert.True(decision.Allowed);
        Assert.False(decision.UsedFallback);
        Assert.Equal("codex_sdk", decision.SelectedBackendId);
        Assert.Equal("codex-worker-trusted", decision.SelectedRoutingProfileId);
        Assert.Equal("profile-routing-preferred", decision.ActiveRoutingProfileId);
        Assert.Equal("rule-patch-draft", decision.AppliedRoutingRuleId);
        Assert.Equal("patch_draft", decision.RoutingIntent);
        Assert.Equal("src/runtime/routing", decision.RoutingModuleId);
        Assert.Equal("gpt-5-codex-routing", decision.SelectedModelId);
        Assert.Equal("active_profile_preferred", decision.RouteSource);
        Assert.Contains("preferred route", decision.RouteReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(decision.Candidates, candidate =>
            candidate.BackendId == "codex_sdk"
            && candidate.Selected
            && candidate.RoutingRuleId == "rule-patch-draft"
            && candidate.RouteDisposition == "preferred");
    }

    [Fact]
    public void WorkerSelectionPolicyService_UsesActiveRoutingProfileFallbackRouteWhenPreferredRouteIsUnavailable()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoRoutingFallback");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-routing-fallback", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        routingRepository.SaveActive(new RuntimeRoutingProfile
        {
            ProfileId = "profile-routing-fallback",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-review-summary",
                    RoutingIntent = "review_summary",
                    ModuleId = "src/runtime/review",
                    Summary = "Prefer gemini, fall back to codex when unavailable.",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "gemini",
                        BackendId = "gemini_api",
                        RoutingProfileId = "gemini-worker-balanced",
                        RequestFamily = "generate_content",
                        Model = "gemini-2.5-pro",
                    },
                    FallbackRoutes =
                    [
                        new RuntimeRoutingRoute
                        {
                            ProviderId = "codex",
                            BackendId = "codex_sdk",
                            RoutingProfileId = "codex-worker-trusted",
                            RequestFamily = "codex_sdk",
                            Model = "gpt-5-codex-fallback",
                        },
                    ],
                },
            ],
        });
        var selection = new WorkerSelectionPolicyService(
            workspace.RootPath,
            repoRegistry,
            providers,
            routing,
            governance,
            adapters,
            boundary,
            new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
            operationalPolicyService,
            runtimeRoutingProfileService: new RuntimeRoutingProfileService(routingRepository));

        var decision = selection.Evaluate(new Carves.Runtime.Domain.Tasks.TaskNode
        {
            TaskId = "T-ROUTING-FALLBACK",
            Title = "Route fallback task",
            TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
            Scope = ["src/runtime/review"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "review_summary",
                ["module_id"] = "src/runtime/review",
            },
        }, repoId: "repo-routing-fallback");

        Assert.True(decision.Allowed);
        Assert.True(decision.UsedFallback);
        Assert.Equal("codex_sdk", decision.SelectedBackendId);
        Assert.Equal("codex-worker-trusted", decision.SelectedRoutingProfileId);
        Assert.Equal("profile-routing-fallback", decision.ActiveRoutingProfileId);
        Assert.Equal("rule-review-summary", decision.AppliedRoutingRuleId);
        Assert.Equal("gpt-5-codex-fallback", decision.SelectedModelId);
        Assert.Equal("active_profile_fallback", decision.RouteSource);
        Assert.Contains("fallback route", decision.RouteReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(decision.Candidates, candidate =>
            candidate.BackendId == "codex_sdk"
            && candidate.Selected
            && candidate.RoutingRuleId == "rule-review-summary"
            && candidate.RouteDisposition == "fallback");
    }

    [Fact]
    public void WorkerSelectionPolicyService_ExplainsForcedFallbackWithEligibilitySignals()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoRoutingExplain");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-routing-explain", "codex-worker-trusted", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        routingRepository.SaveActive(new RuntimeRoutingProfile
        {
            ProfileId = "profile-routing-explain",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-failure-summary",
                    RoutingIntent = "failure_summary",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "codex",
                        BackendId = "codex_sdk",
                        RoutingProfileId = "codex-worker-trusted",
                        RequestFamily = "codex_sdk",
                        Model = "gpt-5-codex-primary",
                    },
                    FallbackRoutes =
                    [
                        new RuntimeRoutingRoute
                        {
                            ProviderId = "null",
                            BackendId = "null_worker",
                            RequestFamily = "none",
                            Model = "null-fallback",
                        },
                    ],
                },
            ],
        });
        var selection = new WorkerSelectionPolicyService(
            workspace.RootPath,
            repoRegistry,
            providers,
            routing,
            governance,
            adapters,
            boundary,
            new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
            operationalPolicyService,
            runtimeRoutingProfileService: new RuntimeRoutingProfileService(routingRepository));

        var decision = selection.Evaluate(new Carves.Runtime.Domain.Tasks.TaskNode
        {
            TaskId = "T-ROUTING-EXPLAIN",
            Title = "Force fallback explain task",
            TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "failure_summary",
            },
        }, repoId: "repo-routing-explain", options: new WorkerSelectionOptions { ForceFallbackOnly = true });

        Assert.True(decision.Allowed);
        Assert.True(decision.UsedFallback);
        Assert.Equal(RouteEligibilityStatus.TemporarilyIneligible, decision.PreferredRouteEligibility);
        Assert.Contains("forced fallback", decision.PreferredIneligibilityReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fallback_route_selected", decision.SelectedBecause);
        Assert.Contains(decision.Candidates, candidate =>
            candidate.RouteDisposition == "preferred"
            && candidate.Eligibility == RouteEligibilityStatus.TemporarilyIneligible
            && candidate.Signals.TokenBudgetFit);
    }

    [Fact]
    public void WorkerSelectionPolicyService_RejectsActiveRouteWhenBackendRequestFamilyIsIncompatible()
    {
        using var workspace = new TemporaryWorkspace();
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        try
        {
            var managedRepo = CreateManagedRepo(workspace, "RepoRoutingRequestFamilyMismatch");
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            repoRegistry.Register(managedRepo, "repo-routing-request-family-mismatch", "worker-codegen-fast", "balanced");
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("openai");
            var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
            providers.List();
            var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
            var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
            var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
            var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
            routingRepository.SaveActive(new RuntimeRoutingProfile
            {
                ProfileId = "profile-routing-request-family-mismatch",
                Rules =
                [
                    new RuntimeRoutingRule
                    {
                        RuleId = "rule-reasoning-summary",
                        RoutingIntent = "reasoning_summary",
                        ModuleId = ".ai/runtime/sustainability",
                        PreferredRoute = new RuntimeRoutingRoute
                        {
                            ProviderId = "openai",
                            BackendId = "openai_api",
                            RoutingProfileId = "worker-codegen-fast",
                            RequestFamily = "chat_completions",
                            Model = "llama-3.3-70b-versatile",
                        },
                    },
                ],
            });
            var selection = new WorkerSelectionPolicyService(
                workspace.RootPath,
                repoRegistry,
                providers,
                routing,
                governance,
                adapters,
                boundary,
                new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters),
                operationalPolicyService,
                runtimeRoutingProfileService: new RuntimeRoutingProfileService(routingRepository));

            var decision = selection.Evaluate(new Carves.Runtime.Domain.Tasks.TaskNode
            {
                TaskId = "T-ROUTING-REQUEST-FAMILY-MISMATCH",
                Title = "Route request-family mismatch task",
                TaskType = Carves.Runtime.Domain.Tasks.TaskType.Execution,
                Scope = [".ai/runtime/sustainability"],
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["routing_intent"] = "reasoning_summary",
                    ["module_id"] = ".ai/runtime/sustainability",
                },
                Validation = new Carves.Runtime.Domain.Tasks.ValidationPlan
                {
                    Commands =
                    [
                        ["dotnet", "--version"],
                    ],
                },
            }, repoId: "repo-routing-request-family-mismatch");

            Assert.False(decision.Allowed);
            Assert.Equal("active_profile_unresolved", decision.RouteSource);
            Assert.Equal("rule-reasoning-summary", decision.AppliedRoutingRuleId);
            Assert.Contains(decision.Candidates, candidate =>
                candidate.BackendId == "openai_api"
                && candidate.Eligibility == RouteEligibilityStatus.Unsupported
                && candidate.Reason.Contains("request-family", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    private static string CreateManagedRepo(TemporaryWorkspace workspace, string repoName)
    {
        var root = Path.Combine(workspace.RootPath, repoName);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, ".ai"));
        return root;
    }

    private static string CreateFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  echo {"type":"thread.started","thread_id":"cli-selection"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"selection ok"}}
  echo {"type":"turn.completed","usage":{"input_tokens":1,"output_tokens":1}}
  exit /b 0
)
echo unsupported
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  echo '{"type":"thread.started","thread_id":"cli-selection"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"agent_message","text":"selection ok"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":1,"output_tokens":1}}'
  exit 0
fi
echo unsupported
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }
}
