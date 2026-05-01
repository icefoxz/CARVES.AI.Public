using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RoutingValidationServiceTests
{
    [Fact]
    public void RunTask_PreservesBaselineRoutingAndForcedFallbackSelectionTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoValidationRouting");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-validation-routing", "codex-worker-trusted", "balanced");
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
            ProfileId = "profile-validation-routing",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "codex",
                        BackendId = "codex_sdk",
                        RoutingProfileId = "codex-worker-trusted",
                        Model = "route-model",
                    },
                    FallbackRoutes =
                    [
                        new RuntimeRoutingRoute
                        {
                            ProviderId = "null",
                            BackendId = "null_worker",
                            Model = "fallback-model",
                        },
                    ],
                },
            ],
        });
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        qualificationRepository.SaveMatrix(new ModelQualificationMatrix
        {
            MatrixId = "matrix-routing-validation",
            Lanes =
            [
                new ModelQualificationLane
                {
                    LaneId = "baseline-null",
                    ProviderId = "null",
                    BackendId = "null_worker",
                    RequestFamily = "direct",
                    Model = "baseline-model",
                    BaseUrl = "memory://baseline",
                    ApiKeyEnvironmentVariable = "NONE",
                },
                new ModelQualificationLane
                {
                    LaneId = "preferred-codex",
                    ProviderId = "codex",
                    BackendId = "codex_sdk",
                    RequestFamily = "direct",
                    Model = "route-model",
                    BaseUrl = "memory://preferred",
                    ApiKeyEnvironmentVariable = "NONE",
                    RoutingProfileId = "codex-worker-trusted",
                },
                new ModelQualificationLane
                {
                    LaneId = "fallback-null",
                    ProviderId = "null",
                    BackendId = "null_worker",
                    RequestFamily = "direct",
                    Model = "fallback-model",
                    BaseUrl = "memory://fallback",
                    ApiKeyEnvironmentVariable = "NONE",
                },
            ],
        });
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveCatalog(new RoutingValidationCatalog
        {
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-ROUTING-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize the failure in three bullets.",
                    BaselineLaneId = "baseline-null",
                    Summary = "Routing validation sample.",
                },
            ],
        });

        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubRoutingValidationLaneExecutor());
        var selectionService = new WorkerSelectionPolicyService(
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
        var service = new RoutingValidationService(
            validationRepository,
            qualificationService,
            new StubRoutingValidationLaneExecutor(),
            selectionService,
            new RuntimeRoutingProfileService(routingRepository));

        var baseline = service.RunTask("VAL-ROUTING-001", RoutingValidationMode.Baseline, "repo-validation-routing");
        var routingTrace = service.RunTask("VAL-ROUTING-001", RoutingValidationMode.Routing, "repo-validation-routing");
        var forcedFallback = service.RunTask("VAL-ROUTING-001", RoutingValidationMode.ForcedFallback, "repo-validation-routing");

        Assert.Equal("baseline-null", baseline.SelectedLane);
        Assert.Equal("validation_baseline", baseline.RouteSource);
        Assert.Contains("baseline_fixed_lane", baseline.SelectedBecause);

        Assert.Equal("preferred-codex", routingTrace.SelectedLane);
        Assert.Equal("active_profile_preferred", routingTrace.RouteSource);
        Assert.False(routingTrace.FallbackTriggered);
        Assert.Equal("codex-thread-preferred-codex", routingTrace.CodexThreadId);
        Assert.Equal(WorkerThreadContinuity.NewThread, routingTrace.CodexThreadContinuity);
        Assert.Contains("preferred_route_eligible", routingTrace.SelectedBecause);

        Assert.Equal("fallback-null", forcedFallback.SelectedLane);
        Assert.Equal("active_profile_fallback", forcedFallback.RouteSource);
        Assert.True(forcedFallback.FallbackConfigured);
        Assert.True(forcedFallback.FallbackTriggered);
        Assert.Equal(RouteEligibilityStatus.TemporarilyIneligible, forcedFallback.PreferredRouteEligibility);
        Assert.Contains("forced fallback", forcedFallback.PreferredIneligibilityReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fallback_route_selected", forcedFallback.SelectedBecause);

        var traces = service.LoadTraces();
        Assert.Equal(3, traces.Count);
    }

    [Fact]
    public void RunSuite_SavesLatestSummaryWithAggregatedRates()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoValidationSummary");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-validation-summary", "codex-worker-trusted", "balanced");
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
            ProfileId = "profile-validation-summary",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-structured-output",
                    RoutingIntent = "structured_output",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "codex",
                        BackendId = "codex_sdk",
                        RoutingProfileId = "codex-worker-trusted",
                        Model = "route-json-model",
                    },
                },
                new RuntimeRoutingRule
                {
                    RuleId = "rule-failure-summary",
                    RoutingIntent = "failure_summary",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "codex",
                        BackendId = "codex_sdk",
                        RoutingProfileId = "codex-worker-trusted",
                        Model = "route-text-model",
                    },
                },
            ],
        });
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        qualificationRepository.SaveMatrix(new ModelQualificationMatrix
        {
            MatrixId = "matrix-validation-summary",
            Lanes =
            [
                new ModelQualificationLane
                {
                    LaneId = "route-json",
                    ProviderId = "codex",
                    BackendId = "codex_sdk",
                    RequestFamily = "direct",
                    Model = "route-json-model",
                    BaseUrl = "memory://json",
                    ApiKeyEnvironmentVariable = "NONE",
                    RoutingProfileId = "codex-worker-trusted",
                },
                new ModelQualificationLane
                {
                    LaneId = "route-text",
                    ProviderId = "codex",
                    BackendId = "codex_sdk",
                    RequestFamily = "direct",
                    Model = "route-text-model",
                    BaseUrl = "memory://text",
                    ApiKeyEnvironmentVariable = "NONE",
                    RoutingProfileId = "codex-worker-trusted",
                },
            ],
        });
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveCatalog(new RoutingValidationCatalog
        {
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-SUM-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    Prompt = "Summarize the failure.",
                    BaselineLaneId = "route-text",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-SUM-002",
                    TaskType = "evidence-normalization",
                    RoutingIntent = "structured_output",
                    Prompt = "Return JSON with risk_level, root_cause, mitigation_steps.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["risk_level", "root_cause", "mitigation_steps"],
                    BaselineLaneId = "route-json",
                },
            ],
        });

        var laneExecutor = new StubRoutingValidationLaneExecutor();
        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            laneExecutor);
        var selectionService = new WorkerSelectionPolicyService(
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
        var service = new RoutingValidationService(
            validationRepository,
            qualificationService,
            laneExecutor,
            selectionService,
            new RuntimeRoutingProfileService(routingRepository));

        var catalog = service.LoadOrCreateCatalog();
        var summary = service.RunSuite(RoutingValidationMode.Routing, repoId: "repo-validation-summary");
        var latest = service.LoadLatestSummary();

        Assert.NotNull(latest);
        Assert.Equal(catalog.Tasks.Length, summary.Tasks);
        Assert.Equal(1.0d, summary.SuccessRate);
        Assert.Equal(1.0d, summary.SchemaValidityRate);
        Assert.Equal(0.0d, summary.FallbackRate);
        Assert.True(summary.AverageLatencyMs >= 50);
        Assert.True(summary.TotalEstimatedCostUsd >= 0m);
        Assert.Contains(summary.RouteBreakdown, item =>
            item.TaskFamily == "failure-summary"
            && item.ProviderId == "codex"
            && item.BackendId == "codex_sdk"
            && item.SelectedLane == "route-text");
        Assert.Contains(summary.RouteBreakdown, item =>
            item.TaskFamily == "evidence-normalization"
            && item.SuccessRate == 1.0d
            && item.PatchAcceptanceRate == 0.0d);
        Assert.Equal(summary.RunId, latest!.RunId);
    }

    [Fact]
    public void RunTask_VerySmallCodeValidationRecordsBuildTestAndSafetyOutcomes()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoValidationCode");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-validation-code", "codex-worker-trusted", "balanced");
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
            ProfileId = "profile-validation-code",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-code-patch",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "codex",
                        BackendId = "codex_sdk",
                        RoutingProfileId = "codex-worker-trusted",
                        Model = "route-code-model",
                    },
                },
            ],
        });
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        qualificationRepository.SaveMatrix(new ModelQualificationMatrix
        {
            MatrixId = "matrix-validation-code",
            Lanes =
            [
                new ModelQualificationLane
                {
                    LaneId = "n1n-responses",
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    RequestFamily = "responses_api",
                    Model = "gpt-4.1",
                    BaseUrl = "memory://baseline-code",
                    ApiKeyEnvironmentVariable = "NONE",
                },
                new ModelQualificationLane
                {
                    LaneId = "preferred-code",
                    ProviderId = "codex",
                    BackendId = "codex_sdk",
                    RequestFamily = "direct",
                    Model = "route-code-model",
                    BaseUrl = "memory://preferred-code",
                    ApiKeyEnvironmentVariable = "NONE",
                    RoutingProfileId = "codex-worker-trusted",
                },
            ],
        });
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveCatalog(new RoutingValidationCatalog
        {
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODE-001",
                    TaskType = "code.small.fix",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Return JSON with fields change_summary, files_touched, validation_commands, backward_compatibility_notes.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["change_summary", "files_touched", "validation_commands", "backward_compatibility_notes"],
                    BaselineLaneId = "n1n-responses",
                },
            ],
        });

        var laneExecutor = new StubRoutingValidationLaneExecutor();
        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            laneExecutor);
        var selectionService = new WorkerSelectionPolicyService(
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
        var service = new RoutingValidationService(
            validationRepository,
            qualificationService,
            laneExecutor,
            selectionService,
            new RuntimeRoutingProfileService(routingRepository));

        var baseline = service.RunTask("VAL-CODE-001", RoutingValidationMode.Baseline, "repo-validation-code");
        var routingTrace = service.RunTask("VAL-CODE-001", RoutingValidationMode.Routing, "repo-validation-code");

        Assert.Equal("n1n-responses", baseline.SelectedLane);
        Assert.Equal("preferred-code", routingTrace.SelectedLane);
        Assert.Equal(RoutingValidationExecutionOutcome.Passed, routingTrace.BuildOutcome);
        Assert.Equal(RoutingValidationExecutionOutcome.Passed, routingTrace.TestOutcome);
        Assert.Equal(RoutingValidationExecutionOutcome.Passed, routingTrace.SafetyOutcome);
        Assert.True(routingTrace.PatchAccepted);
        Assert.Equal("codex-thread-preferred-code", routingTrace.CodexThreadId);
        Assert.Equal(WorkerThreadContinuity.NewThread, routingTrace.CodexThreadContinuity);
    }

    [Fact]
    public void LoadOrCreateCatalog_ExpandsVerySmallCodePack()
    {
        using var workspace = new TemporaryWorkspace();
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveCatalog(new RoutingValidationCatalog
        {
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODE-001",
                    TaskType = "very-small-code-task",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "legacy",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["change_summary"],
                    BaselineLaneId = "n1n-responses",
                },
            ],
        });

        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubRoutingValidationLaneExecutor());
        var service = new RoutingValidationService(
            validationRepository,
            qualificationService,
            new StubRoutingValidationLaneExecutor(),
            CreateSelectionService(workspace.RootPath),
            new RuntimeRoutingProfileService(routingRepository));

        var catalog = service.LoadOrCreateCatalog();

        Assert.Contains(catalog.Tasks, task => task.TaskId == "VAL-CODE-001" && task.TaskType == "code.small.fix");
        Assert.Contains(catalog.Tasks, task => task.TaskId == "VAL-CODE-IMPL-001" && task.TaskType == "code.small.impl");
        Assert.Contains(catalog.Tasks, task => task.TaskId == "VAL-CODE-TEST-001" && task.TaskType == "code.small.test");
    }

    [Fact]
    public void LoadHistory_ReturnsRecentBatchSummaries()
    {
        using var workspace = new TemporaryWorkspace();
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "run-older",
            ExecutionMode = RoutingValidationMode.Baseline,
            GeneratedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            Tasks = 1,
            SuccessRate = 1,
        });
        validationRepository.SaveSummary(new RoutingValidationSummary
        {
            RunId = "run-newer",
            ExecutionMode = RoutingValidationMode.Routing,
            GeneratedAt = DateTimeOffset.UtcNow,
            Tasks = 2,
            SuccessRate = 1,
        });

        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubRoutingValidationLaneExecutor());
        var service = new RoutingValidationService(
            validationRepository,
            qualificationService,
            new StubRoutingValidationLaneExecutor(),
            CreateSelectionService(workspace.RootPath),
            new RuntimeRoutingProfileService(routingRepository));

        var history = service.LoadHistory();

        Assert.Equal(2, history.BatchCount);
        Assert.Equal("run-newer", history.LatestRunId);
        Assert.Equal("run-newer", history.Batches[0].RunId);
    }

    [Fact]
    public void LoadOrCreateCatalog_IncludesCodexComparativeTasks()
    {
        using var workspace = new TemporaryWorkspace();
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubRoutingValidationLaneExecutor());
        var service = new RoutingValidationService(
            validationRepository,
            qualificationService,
            new StubRoutingValidationLaneExecutor(),
            CreateSelectionService(workspace.RootPath),
            new RuntimeRoutingProfileService(routingRepository));

        var catalog = service.LoadOrCreateCatalog();

        // Codex SDK comparative tasks exist with correct baseline lane IDs
        Assert.Contains(catalog.Tasks, task =>
            task.TaskId == "VAL-CODEX-FS-001"
            && task.BaselineLaneId == "codex-sdk-worker"
            && task.TaskType == "failure-summary");
        Assert.Contains(catalog.Tasks, task =>
            task.TaskId == "VAL-CODEX-CLI-FS-001"
            && task.BaselineLaneId == "codex-cli-worker"
            && task.TaskType == "failure-summary");
        Assert.Contains(catalog.Tasks, task =>
            task.TaskId == "VAL-CODEX-CODE-001"
            && task.BaselineLaneId == "codex-sdk-worker"
            && task.TaskType == "code.small.fix");
        Assert.Contains(catalog.Tasks, task =>
            task.TaskId == "VAL-CODEX-CLI-CODE-001"
            && task.BaselineLaneId == "codex-cli-worker"
            && task.TaskType == "code.small.impl");

        // All Codex tasks reference a valid routing intent
        var codexTasks = catalog.Tasks.Where(task => task.TaskId.StartsWith("VAL-CODEX-", StringComparison.Ordinal)).ToArray();
        Assert.All(codexTasks, task => Assert.False(string.IsNullOrWhiteSpace(task.RoutingIntent)));
        Assert.All(codexTasks, task => Assert.False(string.IsNullOrWhiteSpace(task.Prompt)));
    }

    [Fact]
    public void LoadOrCreateCatalog_IncludesReasoningSummaryPromotionTask()
    {
        using var workspace = new TemporaryWorkspace();
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubRoutingValidationLaneExecutor());
        var service = new RoutingValidationService(
            validationRepository,
            qualificationService,
            new StubRoutingValidationLaneExecutor(),
            CreateSelectionService(workspace.RootPath),
            new RuntimeRoutingProfileService(routingRepository));

        var catalog = service.LoadOrCreateCatalog();

        Assert.Contains(catalog.Tasks, task =>
            task.TaskId == "VAL-RS-001"
            && task.RoutingIntent == "reasoning_summary"
            && task.BaselineLaneId == "n1n-responses");
    }

    [Fact]
    public void RunTask_CodexBaselineLane_TracksCodexThreadContinuity()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoCodexBaseline");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-codex-baseline", "codex-worker-trusted", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        qualificationRepository.SaveMatrix(new ModelQualificationMatrix
        {
            MatrixId = "matrix-codex-baseline",
            Lanes =
            [
                new ModelQualificationLane
                {
                    LaneId = "codex-sdk-worker",
                    ProviderId = "codex",
                    BackendId = "codex_sdk",
                    RequestFamily = "codex_sdk",
                    Model = "codex-default",
                    BaseUrl = "",
                    ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
                    RoutingProfileId = "codex-worker-trusted",
                },
                new ModelQualificationLane
                {
                    LaneId = "codex-cli-worker",
                    ProviderId = "codex",
                    BackendId = "codex_cli",
                    RequestFamily = "codex_exec",
                    Model = "codex-cli",
                    BaseUrl = "",
                    ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
                    RoutingProfileId = "codex-worker-local-cli",
                },
            ],
        });
        var validationRepository = new JsonRoutingValidationRepository(workspace.Paths);
        validationRepository.SaveCatalog(new RoutingValidationCatalog
        {
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODEX-FS-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize this substrate failure for an operator in exactly three bullets: Worker failed.",
                    BaselineLaneId = "codex-sdk-worker",
                    Summary = "Codex SDK baseline test.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODEX-CLI-FS-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize this semantic failure for an operator in exactly three bullets: Contract mismatch.",
                    BaselineLaneId = "codex-cli-worker",
                    Summary = "Codex CLI baseline test.",
                },
            ],
        });

        var laneExecutor = new StubRoutingValidationLaneExecutor();
        var qualificationService = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            laneExecutor);
        var selectionService = new WorkerSelectionPolicyService(
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
        var service = new RoutingValidationService(
            validationRepository,
            qualificationService,
            laneExecutor,
            selectionService,
            new RuntimeRoutingProfileService(routingRepository));

        // Baseline against codex_sdk lane — thread continuity must be captured
        var sdkTrace = service.RunTask("VAL-CODEX-FS-001", RoutingValidationMode.Baseline);
        Assert.Equal("codex-sdk-worker", sdkTrace.SelectedLane);
        Assert.Equal("codex_sdk", sdkTrace.SelectedBackend);
        Assert.Equal("validation_baseline", sdkTrace.RouteSource);
        Assert.NotNull(sdkTrace.CodexThreadId);
        Assert.Equal(WorkerThreadContinuity.NewThread, sdkTrace.CodexThreadContinuity);

        // Baseline against codex_cli lane — cli backend does not emit thread ID
        var cliTrace = service.RunTask("VAL-CODEX-CLI-FS-001", RoutingValidationMode.Baseline);
        Assert.Equal("codex-cli-worker", cliTrace.SelectedLane);
        Assert.Equal("codex_cli", cliTrace.SelectedBackend);
        Assert.Equal("validation_baseline", cliTrace.RouteSource);
        // CLI stub emits null ThreadId (WorkerThreadContinuity.None)
        Assert.Equal(WorkerThreadContinuity.None, cliTrace.CodexThreadContinuity);
    }

    private static string CreateManagedRepo(TemporaryWorkspace workspace, string repoName)
    {
        var root = Path.Combine(workspace.RootPath, repoName);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, ".ai"));
        return root;
    }

    private sealed class StubRoutingValidationLaneExecutor : IQualificationLaneExecutor
    {
        public WorkerExecutionResult Execute(ModelQualificationLane lane, ModelQualificationCase qualificationCase, int attempt)
        {
            var isJson = qualificationCase.ExpectedFormat == ModelQualificationExpectedFormat.Json;
            var output = isJson
                ? qualificationCase.RequiredJsonFields.Contains("files_touched", StringComparer.Ordinal)
                    ? """{"change_summary":"add schemaVersion","files_touched":["ResultEnvelope.cs","ResultEnvelopeTests.cs"],"validation_commands":["dotnet build","dotnet test"],"backward_compatibility_notes":"default schemaVersion to 1 for legacy callers"}"""
                    : """{"risk_level":"low","root_cause":"timeout","mitigation_steps":["retry"]}"""
                : $"lane={lane.LaneId}; case={qualificationCase.CaseId}; attempt={attempt}";
            return new WorkerExecutionResult
            {
                RunId = $"worker-run-{lane.LaneId}-{attempt}",
                TaskId = qualificationCase.CaseId,
                BackendId = lane.BackendId,
                ProviderId = lane.ProviderId,
                AdapterId = "stub-validation",
                ProtocolFamily = lane.ProviderId,
                RequestFamily = lane.RequestFamily,
                ProfileId = lane.RoutingProfileId ?? "default",
                TrustedProfile = false,
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
                Retryable = false,
                Configured = true,
                Model = lane.Model,
                ThreadId = lane.BackendId == "codex_sdk" ? $"codex-thread-{lane.LaneId}" : null,
                ThreadContinuity = lane.BackendId == "codex_sdk" ? WorkerThreadContinuity.NewThread : WorkerThreadContinuity.None,
                RequestId = $"req-{lane.LaneId}-{attempt}",
                Summary = output,
                ResponsePreview = output,
                Rationale = output,
                InputTokens = 20,
                OutputTokens = 30,
                ProviderLatencyMs = 75,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow.AddMilliseconds(75),
            };
        }
    }

    private static WorkerSelectionPolicyService CreateSelectionService(string rootPath)
    {
        var paths = ControlPlanePaths.FromRepoRoot(rootPath);
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(paths));
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(paths));
        var adapters = TestWorkerAdapterRegistryFactory.Create("codex");
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(paths), repoRegistry, governance, adapters);
        providers.List();
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(paths));
        var boundary = new WorkerExecutionBoundaryService(rootPath, repoRegistry, governance);
        var operationalPolicyService = new WorkerOperationalPolicyService(rootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var health = new ProviderHealthMonitorService(new JsonProviderHealthRepository(paths), providers, adapters);
        return new WorkerSelectionPolicyService(rootPath, repoRegistry, providers, routing, governance, adapters, boundary, health, operationalPolicyService);
    }
}
