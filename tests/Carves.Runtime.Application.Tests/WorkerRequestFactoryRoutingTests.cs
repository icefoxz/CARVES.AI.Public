using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class WorkerRequestFactoryRoutingTests
{
    [Fact]
    public void WorkerRequestFactory_ProjectsRoutingSelectionIntoSessionAndExecutionRequest()
    {
        using var workspace = new TemporaryWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai"));

        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(workspace.RootPath, "repo-routing-request", "default", "balanced");
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
            ProfileId = "profile-routing-request",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "src/runtime/failures",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "codex",
                        BackendId = "codex_sdk",
                        RoutingProfileId = "codex-worker-trusted",
                        RequestFamily = "codex_sdk",
                        BaseUrl = "https://api.openai.com/v1",
                        ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
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

        var task = new TaskNode
        {
            TaskId = "T-ROUTING-REQUEST",
            Title = "Route request projection task",
            Description = "Ensure routing metadata flows into session truth and worker request.",
            TaskType = TaskType.Execution,
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = ["src/runtime/failures"],
            Acceptance = ["request captures selected routing metadata"],
            Validation = new ValidationPlan
            {
                Commands =
                [
                    ["dotnet", "--version"],
                ],
            },
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "failure_summary",
                ["module_id"] = "src/runtime/failures",
            },
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = "AC-T-ROUTING-REQUEST",
                Title = "Routing request contract",
                Status = AcceptanceContractLifecycleStatus.Compiled,
                Intent = new AcceptanceContractIntent
                {
                    Goal = "Project acceptance contract binding into worker request metadata.",
                    BusinessValue = "Host surfaces should inspect the same contract the worker received.",
                },
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "result_commit" },
                    new AcceptanceContractEvidenceRequirement { Type = "validation_evidence" },
                ],
                HumanReview = new AcceptanceContractHumanReviewPolicy
                {
                    Required = true,
                    ProvisionalAllowed = true,
                    Decisions =
                    [
                        AcceptanceContractHumanDecision.Accept,
                        AcceptanceContractHumanDecision.ProvisionalAccept,
                        AcceptanceContractHumanDecision.Reject,
                        AcceptanceContractHumanDecision.Reopen,
                    ],
                },
            },
        };

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var memoryService = new MemoryService(new StubMemoryRepository(), new ExecutionContextBuilder());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var failureSummaryProjectionService = new FailureSummaryProjectionService(
            workspace.Paths,
            new FailureContextService(new JsonFailureReportRepository(workspace.Paths)),
            executionRunService);
        var contextPackService = new ContextPackService(
            workspace.Paths,
            taskGraphService,
            new StubCodeGraphQueryService(),
            memoryService,
            failureSummaryProjectionService,
            executionRunService);
        var worktreeRuntimeService = new WorktreeRuntimeService(workspace.RootPath, new StubGitClient(), new InMemoryWorktreeRuntimeRepository());
        var incidentTimelineService = new RuntimeIncidentTimelineService(
            new InMemoryRuntimeIncidentTimelineRepository(),
            new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository()));
        var executionPacketCompilerService = new ExecutionPacketCompilerService(
            workspace.Paths,
            taskGraphService,
            new StubCodeGraphQueryService(),
            memoryService,
            new PlannerIntentRoutingService());
        var requestFactory = new WorkerRequestFactory(
            workspace.RootPath,
            TestSystemConfigFactory.Create(),
            new StubGitClient(),
            adapters,
            new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low"),
            new StubWorktreeManager(workspace.RootPath),
            worktreeRuntimeService,
            incidentTimelineService,
            memoryService,
            contextPackService,
            boundary,
            selection,
            executionPacketCompilerService);

        var request = requestFactory.Create(task, dryRun: true);
        var executionRequest = Assert.IsType<WorkerExecutionRequest>(request.ExecutionRequest);

        Assert.Equal("repo-routing-request", request.Session.RepoId);
        Assert.Equal("codex_sdk", request.Session.WorkerBackend);
        Assert.Equal("codex", request.Session.WorkerProviderId);
        Assert.Equal("codex-worker-trusted", request.Session.WorkerRoutingProfileId);
        Assert.Equal("profile-routing-request", request.Session.ActiveRoutingProfileId);
        Assert.Equal("rule-failure-summary", request.Session.WorkerRoutingRuleId);
        Assert.Equal("failure_summary", request.Session.WorkerRoutingIntent);
        Assert.Equal("src/runtime/failures", request.Session.WorkerRoutingModuleId);
        Assert.Equal("gpt-5-codex-routing", request.Session.WorkerModelId);
        Assert.Equal("active_profile_preferred", request.Session.WorkerRouteSource);

        Assert.Equal("codex_sdk", executionRequest.BackendHint);
        Assert.Equal("gpt-5-codex-routing", executionRequest.ModelOverride);
        Assert.Equal(120, executionRequest.TimeoutSeconds);
        Assert.Equal(120, executionRequest.RequestBudget.TimeoutSeconds);
        Assert.Equal("runtime_governed_dynamic_request_budget_v1", executionRequest.RequestBudget.PolicyId);
        Assert.Equal(120, request.Session.WorkerRequestBudget.TimeoutSeconds);
        Assert.Equal("failure_summary", executionRequest.RoutingIntent);
        Assert.Equal("src/runtime/failures", executionRequest.RoutingModuleId);
        Assert.Equal("codex-worker-trusted", executionRequest.RoutingProfileId);
        Assert.Equal("rule-failure-summary", executionRequest.RoutingRuleId);
        Assert.Equal("profile-routing-request", executionRequest.ActiveRoutingProfileId);
        Assert.Equal("gpt-5-codex-routing", executionRequest.Metadata["worker_model"]);
        Assert.Equal("active_profile_preferred", executionRequest.Metadata["route_source"]);
        Assert.Equal("failure_summary", executionRequest.Metadata["routing_intent"]);
        Assert.Equal("src/runtime/failures", executionRequest.Metadata["routing_module"]);
        Assert.Equal("rule-failure-summary", executionRequest.Metadata["routing_rule_id"]);
        Assert.Equal("profile-routing-request", executionRequest.Metadata["active_routing_profile_id"]);
        Assert.Equal("codex-worker-trusted", executionRequest.Metadata["selected_routing_profile_id"]);
        Assert.Equal("codex_sdk", executionRequest.Metadata["route_request_family"]);
        Assert.Equal("https://api.openai.com/v1", executionRequest.Metadata["route_base_url"]);
        Assert.Equal("OPENAI_API_KEY", executionRequest.Metadata["route_api_key_env"]);
        Assert.Equal("120", executionRequest.Metadata["worker_request_timeout_seconds"]);
        Assert.Contains("provider_baseline=120s", executionRequest.Metadata["worker_request_budget_reasons"], StringComparison.Ordinal);
        Assert.NotNull(request.Packet);
        Assert.NotNull(executionRequest.Packet);
        Assert.Equal("Execution", executionRequest.Metadata["planner_intent"]);
        Assert.StartsWith("EP-T-ROUTING-REQUEST-", executionRequest.Metadata["execution_packet_id"], StringComparison.Ordinal);
        Assert.Contains(".ai/runtime/execution-packets/T-ROUTING-REQUEST.json", executionRequest.Metadata["execution_packet_path"], StringComparison.Ordinal);
        Assert.Equal("AC-T-ROUTING-REQUEST", executionRequest.Metadata["acceptance_contract_id"]);
        Assert.Equal("Compiled", executionRequest.Metadata["acceptance_contract_status"]);
        Assert.Equal("result_commit|validation_evidence", executionRequest.Metadata["acceptance_contract_evidence_required"]);
        Assert.Equal("true", executionRequest.Metadata["acceptance_contract_human_review_required"]);
        Assert.Equal("true", executionRequest.Metadata["acceptance_contract_provisional_allowed"]);
        Assert.Contains("ProvisionalAccept", executionRequest.Metadata["acceptance_contract_human_decisions"], StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerRequestFactory_RoutingIntentOverride_SelectsActiveProfileRuleWithoutTaskMetadata()
    {
        using var workspace = new TemporaryWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai"));

        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(workspace.RootPath, "repo-routing-override", "default", "balanced");
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
            ProfileId = "profile-routing-override",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-reasoning-summary",
                    RoutingIntent = "reasoning_summary",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "codex",
                        BackendId = "codex_sdk",
                        RoutingProfileId = "codex-worker-trusted",
                        RequestFamily = "codex_sdk",
                        BaseUrl = "https://api.openai.com/v1",
                        ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
                        Model = "gpt-5-codex-reasoning",
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

        var task = new TaskNode
        {
            TaskId = "T-ROUTING-OVERRIDE",
            Title = "Route request override task",
            Description = "Ensure routing overrides flow into worker selection.",
            TaskType = TaskType.Execution,
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = [".ai/runtime/sustainability"],
            Acceptance = ["request captures override-selected routing metadata"],
            Validation = new ValidationPlan
            {
                Commands =
                [
                    ["dotnet", "--version"],
                ],
            },
            AcceptanceContract = CreateAcceptanceContract("T-ROUTING-OVERRIDE"),
        };

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var memoryService = new MemoryService(new StubMemoryRepository(), new ExecutionContextBuilder());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var failureSummaryProjectionService = new FailureSummaryProjectionService(
            workspace.Paths,
            new FailureContextService(new JsonFailureReportRepository(workspace.Paths)),
            executionRunService);
        var contextPackService = new ContextPackService(
            workspace.Paths,
            taskGraphService,
            new StubCodeGraphQueryService(),
            memoryService,
            failureSummaryProjectionService,
            executionRunService);
        var worktreeRuntimeService = new WorktreeRuntimeService(workspace.RootPath, new StubGitClient(), new InMemoryWorktreeRuntimeRepository());
        var incidentTimelineService = new RuntimeIncidentTimelineService(
            new InMemoryRuntimeIncidentTimelineRepository(),
            new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository()));
        var executionPacketCompilerService = new ExecutionPacketCompilerService(
            workspace.Paths,
            taskGraphService,
            new StubCodeGraphQueryService(),
            memoryService,
            new PlannerIntentRoutingService());
        var requestFactory = new WorkerRequestFactory(
            workspace.RootPath,
            TestSystemConfigFactory.Create(),
            new StubGitClient(),
            adapters,
            new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low"),
            new StubWorktreeManager(workspace.RootPath),
            worktreeRuntimeService,
            incidentTimelineService,
            memoryService,
            contextPackService,
            boundary,
            selection,
            executionPacketCompilerService);

        var request = requestFactory.Create(
            task,
            dryRun: true,
            new WorkerSelectionOptions
            {
                RoutingIntentOverride = "reasoning_summary",
            });
        var executionRequest = Assert.IsType<WorkerExecutionRequest>(request.ExecutionRequest);

        Assert.Equal("codex_sdk", request.Session.WorkerBackend);
        Assert.Equal("reasoning_summary", request.Session.WorkerRoutingIntent);
        Assert.Equal(".ai/runtime/sustainability", request.Session.WorkerRoutingModuleId);
        Assert.Equal("active_profile_preferred", request.Session.WorkerRouteSource);
        Assert.Equal("gpt-5-codex-reasoning", executionRequest.ModelOverride);
        Assert.Equal("reasoning_summary", executionRequest.RoutingIntent);
        Assert.Equal(".ai/runtime/sustainability", executionRequest.RoutingModuleId);
        Assert.Equal("profile-routing-override", executionRequest.ActiveRoutingProfileId);
        Assert.Equal("rule-reasoning-summary", executionRequest.RoutingRuleId);
        Assert.Equal("codex_sdk", executionRequest.Metadata["route_request_family"]);
        Assert.Equal("https://api.openai.com/v1", executionRequest.Metadata["route_base_url"]);
        Assert.Equal("OPENAI_API_KEY", executionRequest.Metadata["route_api_key_env"]);
        Assert.NotNull(executionRequest.Packet);
        Assert.Equal("Execution", executionRequest.Metadata["planner_intent"]);
    }

    [Fact]
    public void WorkerRequestFactory_UsesSelectedProviderProfileModelWhenNoActiveRouteMatches()
    {
        using var workspace = new TemporaryWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai"));

        var cliPath = CreateFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalCliModel = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_MODEL");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_MODEL", "codex-cli");
        try
        {
            var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
            repoRegistry.Register(workspace.RootPath, "repo-routing-profile-model", "codex-worker-local-cli", "balanced");
            var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
            var adapters = TestWorkerAdapterRegistryFactory.Create("gemini");
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

            var task = new TaskNode
            {
                TaskId = "T-ROUTING-PROFILE-MODEL",
                Title = "Use selected provider profile model",
                Description = "Ensure selected worker profile model overrides the ambient provider default.",
                TaskType = TaskType.Execution,
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                Scope = ["src/runtime/workbench"],
                Acceptance = ["execution request uses selected provider profile model"],
                Validation = new ValidationPlan
                {
                    Commands =
                    [
                        ["dotnet", "--version"],
                    ],
                },
                AcceptanceContract = CreateAcceptanceContract("T-ROUTING-PROFILE-MODEL"),
            };

            var taskGraphService = new TaskGraphService(
                new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task])),
                new Carves.Runtime.Application.TaskGraph.TaskScheduler());
            var memoryService = new MemoryService(new StubMemoryRepository(), new ExecutionContextBuilder());
            var executionRunService = new ExecutionRunService(workspace.Paths);
            var failureSummaryProjectionService = new FailureSummaryProjectionService(
                workspace.Paths,
                new FailureContextService(new JsonFailureReportRepository(workspace.Paths)),
                executionRunService);
            var contextPackService = new ContextPackService(
                workspace.Paths,
                taskGraphService,
                new StubCodeGraphQueryService(),
                memoryService,
                failureSummaryProjectionService,
                executionRunService);
            var worktreeRuntimeService = new WorktreeRuntimeService(workspace.RootPath, new StubGitClient(), new InMemoryWorktreeRuntimeRepository());
            var incidentTimelineService = new RuntimeIncidentTimelineService(
                new InMemoryRuntimeIncidentTimelineRepository(),
                new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository()));
            var executionPacketCompilerService = new ExecutionPacketCompilerService(
                workspace.Paths,
                taskGraphService,
                new StubCodeGraphQueryService(),
                memoryService,
                new PlannerIntentRoutingService());
            var requestFactory = new WorkerRequestFactory(
                workspace.RootPath,
                TestSystemConfigFactory.Create(),
                new StubGitClient(),
                adapters,
                new WorkerAiRequestFactory(500, 30, "gemini-2.5-pro", "low"),
                new StubWorktreeManager(workspace.RootPath),
                worktreeRuntimeService,
                incidentTimelineService,
                memoryService,
                contextPackService,
                boundary,
                selection,
                executionPacketCompilerService);

            var request = requestFactory.Create(task, dryRun: true);
            var executionRequest = Assert.IsType<WorkerExecutionRequest>(request.ExecutionRequest);

        Assert.Equal("codex_cli", request.Session.WorkerBackend);
        Assert.Equal("codex", request.Session.WorkerProviderId);
        Assert.Equal("codex-worker-local-cli", request.Session.WorkerRoutingProfileId);
        Assert.Equal("codex-cli", request.Session.WorkerModelId);
        Assert.Equal(120, request.Session.WorkerRequestBudget.TimeoutSeconds);
        Assert.Equal("codex-cli", executionRequest.ModelOverride);
        Assert.Equal(120, executionRequest.TimeoutSeconds);
        Assert.Equal("runtime_governed_dynamic_request_budget_v1", executionRequest.RequestBudget.PolicyId);
        Assert.Equal("codex-cli", executionRequest.Metadata["worker_model"]);
        Assert.Equal("120", executionRequest.Metadata["worker_request_timeout_seconds"]);
    }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_MODEL", originalCliModel);
        }
    }

    private sealed class StubMemoryRepository : IMemoryRepository
    {
        public IReadOnlyList<MemoryDocument> LoadCategory(string category)
        {
            return Array.Empty<MemoryDocument>();
        }

        public IReadOnlyList<MemoryDocument> LoadRelevantModules(IReadOnlyList<string> moduleNames)
        {
            return moduleNames
                .Select(name => new MemoryDocument($".ai/memory/modules/{name}.md", "modules", name, $"Memory for {name}"))
                .ToArray();
        }
    }

    private static AcceptanceContract CreateAcceptanceContract(string taskId)
    {
        return new AcceptanceContract
        {
            ContractId = $"AC-{taskId}",
            Title = $"Acceptance contract for {taskId}",
            Status = AcceptanceContractLifecycleStatus.Compiled,
            Traceability = new AcceptanceContractTraceability
            {
                SourceTaskId = taskId,
            },
        };
    }

    private sealed class StubWorktreeManager : IWorktreeManager
    {
        private readonly string rootPath;

        public StubWorktreeManager(string rootPath)
        {
            this.rootPath = rootPath;
        }

        public string ResolveWorktreeRoot(SystemConfig systemConfig, string repoRoot)
        {
            return Path.Combine(rootPath, ".carves-worktrees");
        }

        public string PrepareWorktree(SystemConfig systemConfig, string repoRoot, string taskId, string? startPoint)
        {
            var path = Path.Combine(ResolveWorktreeRoot(systemConfig, repoRoot), taskId);
            Directory.CreateDirectory(path);
            return path;
        }

        public void CleanupWorktree(string worktreePath)
        {
        }
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
echo unsupported
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }
}
