using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using Carves.Runtime.Infrastructure.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed partial class Stage5PlatformTests
{
    [Fact]
    public void RepoRegistry_RoundTripsDescriptorFiles()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoAlpha");
        var service = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));

        var descriptor = service.Register(managedRepo, "repo-alpha", "default", "balanced");
        var loaded = service.Inspect("repo-alpha");

        Assert.Equal("repo-alpha", descriptor.RepoId);
        Assert.Equal(managedRepo, loaded.RepoPath);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.PlatformReposRoot, "repo-alpha.json")));
        Assert.True(File.Exists(workspace.Paths.PlatformRepoRegistryFile));
    }

    [Fact]
    public void RuntimeInstanceManager_ControlsRepoLifecycle()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoBeta");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-beta", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var gateway = new LocalRepoRuntimeGateway();
        var manager = CreateRuntimeInstanceManager(workspace, repoRegistry, governance, gateway);

        var started = manager.Start("repo-beta", dryRun: true);
        var paused = manager.Pause("repo-beta", "Pause for inspection.");
        var stopped = manager.Stop("repo-beta", "Stop after inspection.");
        var sessionPath = Path.Combine(managedRepo, ".ai", "runtime", "live-state", "session.json");

        Assert.Equal(RuntimeInstanceStatus.Running, started.Status);
        Assert.Equal(RuntimeInstanceStatus.Paused, paused.Status);
        Assert.Equal(RuntimeInstanceStatus.Stopped, stopped.Status);
        Assert.True(File.Exists(sessionPath));
        Assert.Contains("\"status\": \"stopped\"", File.ReadAllText(sessionPath), StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderRegistry_CreatesDefaultsAndBindsRepoProfile()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoGamma");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-gamma", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var service = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, TestWorkerAdapterRegistryFactory.Create());

        var providers = service.List();
        var updated = service.Bind("repo-gamma", "planner-high-context");

        Assert.Contains(providers, provider => provider.ProviderId == "openai");
        Assert.Equal("planner-high-context", updated.ProviderProfile);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.PlatformProvidersRoot, "openai.json")));
    }

    [Fact]
    public void PlatformGovernance_PersistsPoliciesAndEvents()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));

        var snapshot = service.GetSnapshot();
        service.RecordEvent(GovernanceEventType.AutonomyLimitReached, "repo-delta", "Planner cap reached.");
        var events = service.LoadEvents();

        Assert.Equal("default-platform", snapshot.PlatformPolicy.PolicyId);
        Assert.Contains(snapshot.RepoPolicies, policy => policy.ProfileId == "balanced");
        Assert.Contains(events, item => item.EventType == GovernanceEventType.AutonomyLimitReached);
        Assert.True(File.Exists(workspace.Paths.PlatformGovernanceFile));
        Assert.True(File.Exists(workspace.Paths.PlatformGovernanceEventsRuntimeFile));
    }

    [Fact]
    public void WorkerBroker_TracksLeaseHeartbeatAndQuarantine()
    {
        using var workspace = new TemporaryWorkspace();
        var broker = new WorkerBroker(
            new WorkerPool(2),
            new JsonWorkerNodeRegistryRepository(workspace.Paths),
            new JsonWorkerLeaseRepository(workspace.Paths),
            new LocalRepoRuntimeGateway());
        var session = RuntimeSessionState.Start(workspace.RootPath, dryRun: true);

        var initialNodes = broker.ListNodes();
        var beforeAcquire = DateTimeOffset.UtcNow;
        var lease = broker.Acquire(session, "T-BROKER");
        var afterHeartbeat = broker.Heartbeat("local-default");
        var activeLease = broker.ListLeases().Single(item => item.LeaseId == lease.LeaseId);
        var quarantined = broker.Quarantine("local-default", "Node unhealthy.");
        broker.Release(session, lease);

        Assert.Single(initialNodes);
        Assert.True(lease.Acquired);
        Assert.Equal("local-default", lease.NodeId);
        Assert.True(
            lease.ExpiresAt >= beforeAcquire.Add(WorkerLeasePolicy.ExecutionIsolationBudget),
            $"Lease expires at {lease.ExpiresAt:O}, before acquire was {beforeAcquire:O}.");
        Assert.True(
            activeLease.ExpiresAt >= activeLease.LastHeartbeatAt.Add(WorkerLeasePolicy.ExecutionIsolationBudget),
            $"Renewed lease expires at {activeLease.ExpiresAt:O}, heartbeat was {activeLease.LastHeartbeatAt:O}.");
        Assert.Equal(WorkerNodeStatus.Busy, afterHeartbeat.Status);
        Assert.Equal(WorkerNodeStatus.Quarantined, quarantined.Status);
        Assert.True(File.Exists(workspace.Paths.PlatformWorkerRegistryLiveStateFile));
    }

    [Fact]
    public void OperatorApiService_ReturnsPlatformStatusSummary()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoEpsilon");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-epsilon", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var gateway = new LocalRepoRuntimeGateway();
        var instanceManager = CreateRuntimeInstanceManager(workspace, repoRegistry, governance, gateway);
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, TestWorkerAdapterRegistryFactory.Create());
        providers.List();
        var providerRouting = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var scheduler = new PlatformSchedulerService(repoRegistry, instanceManager, governance, new JsonWorkerNodeRegistryRepository(workspace.Paths), providerRouting);
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var adapters = TestWorkerAdapterRegistryFactory.Create();
        var providerHealth = new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters);
        var selection = new WorkerSelectionPolicyService(workspace.RootPath, repoRegistry, providers, providerRouting, governance, adapters, boundary, providerHealth);
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), operatorOsEventStream);
        var ownershipService = new SessionOwnershipService(new InMemoryOwnershipRepository(), actorSessionService, operatorOsEventStream);
        var api = new OperatorApiService(
            repoRegistry,
            instanceManager,
            providers,
            providerRouting,
            scheduler,
            new JsonWorkerNodeRegistryRepository(workspace.Paths),
            new JsonWorkerLeaseRepository(workspace.Paths),
            gateway,
            boundary,
            selection,
            CreatePermissionOrchestrationService(workspace, repoRegistry, governance),
            providerHealth,
            new RuntimeRoutingProfileService(new JsonRuntimeRoutingProfileRepository(workspace.Paths)),
            new RuntimeIncidentTimelineService(new JsonRuntimeIncidentTimelineRepository(workspace.Paths), operatorOsEventStream),
            actorSessionService,
            ownershipService,
            operatorOsEventStream);

        var status = api.GetPlatformStatus();

        Assert.Equal(1, status.RegisteredRepoCount);
        Assert.Equal(1, status.RuntimeInstanceCount);
        Assert.Equal("repo-epsilon", status.Repos[0].RepoId);
    }

    [Fact]
    public void OperatorApiService_ReturnsActiveRoutingProfile()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoRouting");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-routing", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var gateway = new LocalRepoRuntimeGateway();
        var instanceManager = CreateRuntimeInstanceManager(workspace, repoRegistry, governance, gateway);
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, TestWorkerAdapterRegistryFactory.Create());
        providers.List();
        var providerRouting = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var scheduler = new PlatformSchedulerService(repoRegistry, instanceManager, governance, new JsonWorkerNodeRegistryRepository(workspace.Paths), providerRouting);
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var adapters = TestWorkerAdapterRegistryFactory.Create();
        var providerHealth = new ProviderHealthMonitorService(new JsonProviderHealthRepository(workspace.Paths), providers, adapters);
        var selection = new WorkerSelectionPolicyService(workspace.RootPath, repoRegistry, providers, providerRouting, governance, adapters, boundary, providerHealth);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        routingRepository.SaveActive(new RuntimeRoutingProfile
        {
            ProfileId = "profile-alpha",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "patch",
                    RoutingIntent = "patch_draft",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "gemini",
                        BackendId = "gemini_api",
                        RoutingProfileId = "gemini-worker-balanced",
                        Model = "gemini-2.5-pro",
                    },
                },
            ],
        });
        var routingService = new RuntimeRoutingProfileService(routingRepository);
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), operatorOsEventStream);
        var ownershipService = new SessionOwnershipService(new InMemoryOwnershipRepository(), actorSessionService, operatorOsEventStream);
        var api = new OperatorApiService(
            repoRegistry,
            instanceManager,
            providers,
            providerRouting,
            scheduler,
            new JsonWorkerNodeRegistryRepository(workspace.Paths),
            new JsonWorkerLeaseRepository(workspace.Paths),
            gateway,
            boundary,
            selection,
            CreatePermissionOrchestrationService(workspace, repoRegistry, governance),
            providerHealth,
            routingService,
            new RuntimeIncidentTimelineService(new JsonRuntimeIncidentTimelineRepository(workspace.Paths), operatorOsEventStream),
            actorSessionService,
            ownershipService,
            operatorOsEventStream);

        var activeProfile = api.GetActiveRoutingProfile();

        Assert.NotNull(activeProfile);
        Assert.Equal("profile-alpha", activeProfile!.ProfileId);
        Assert.Single(activeProfile.Rules);
        Assert.Equal("patch_draft", activeProfile.Rules[0].RoutingIntent);
    }

    [Fact]
    public void RepoTruthProjectionService_ReconcilesDriftAndMarksUnavailable()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoTheta");
        var descriptor = new RepoDescriptor
        {
            RepoId = "repo-theta",
            RepoPath = managedRepo,
            Stage = "Stage-4 continuous development runtime",
        };
        var projectionService = new RepoTruthProjectionService(new LocalRepoRuntimeGateway());
        var instance = new RuntimeInstance
        {
            RepoId = descriptor.RepoId,
            RepoPath = descriptor.RepoPath,
            Projection = new RepoRuntimeProjection
            {
                SummaryFingerprint = "outdated",
                ProjectedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
                Stage = "Unknown",
            },
        };

        var refreshed = projectionService.Refresh(descriptor, instance);

        Assert.Equal(ProjectionFreshness.Fresh, refreshed.Projection.Freshness);
        Assert.Equal(ProjectionReconciliationOutcome.ReconciledDrift, refreshed.Projection.LastReconciliationOutcome);
        Assert.Equal(RepoRuntimeGatewayHealthState.Healthy, refreshed.GatewayHealth);

        Directory.Delete(Path.Combine(managedRepo, ".ai"), recursive: true);
        refreshed.Projection.ProjectedAt = DateTimeOffset.UtcNow.AddMinutes(-30);

        var unavailable = projectionService.Refresh(descriptor, refreshed);

        Assert.Equal(ProjectionFreshness.Stale, unavailable.Projection.Freshness);
        Assert.Equal(ProjectionReconciliationOutcome.MarkedUnavailable, unavailable.Projection.LastReconciliationOutcome);
        Assert.Equal(RepoRuntimeGatewayHealthState.Unreachable, unavailable.GatewayHealth);
    }

    [Fact]
    public void ProviderRoutingService_UsesFallbackAndDeniesExhaustedQuota()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoZeta");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-zeta", "planner-high-context", "balanced");
        var governanceRepository = new JsonPlatformGovernanceRepository(workspace.Paths);
        var governance = new PlatformGovernanceService(governanceRepository);
        var defaults = governance.GetSnapshot();
        var providerQuotaPerHour = defaults.PlatformPolicy.ProviderQuotaPerHour;
        var snapshot = new PlatformGovernanceSnapshot
        {
            Version = defaults.Version,
            PlatformPolicy = defaults.PlatformPolicy,
            WorkerPolicies = defaults.WorkerPolicies,
            ReviewPolicies = defaults.ReviewPolicies,
            RepoPolicies =
            [
                new RepoPolicy
                {
                    ProfileId = "balanced",
                    MaxPlannerRounds = 3,
                    MaxGeneratedTasks = 20,
                    MaxConcurrentExecutions = 4,
                    RuntimeSelectionPriority = 10,
                    StarvationWindowMinutes = 30,
                    AllowAutonomousRefactor = true,
                    AllowAutonomousMemoryUpdate = true,
                    ManualApprovalMode = false,
                    ProviderPolicyProfile = "worker-only",
                    WorkerPolicyProfile = "trusted-dotnet-only",
                    ReviewPolicyProfile = "manual-on-architecture-change",
                },
            ],
            ProviderPolicies =
            [
                new ProviderPolicy
                {
                    PolicyId = "worker-only",
                    AllowedProviderProfiles = ["worker-codegen-fast"],
                    AllowCodeGeneration = true,
                    AllowPlanning = true,
                    AllowedRepoScopes = ["*"],
                    AllowFallbackProfiles = true,
                    FallbackProviderProfiles = ["worker-codegen-fast"],
                },
            ],
        };
        governanceRepository.Save(snapshot);

        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, TestWorkerAdapterRegistryFactory.Create());
        providers.List();
        var quotaRepository = new JsonProviderQuotaRepository(workspace.Paths);
        quotaRepository.Save(new ProviderQuotaSnapshot
        {
            Entries =
            [
                new ProviderQuotaEntry
                {
                    ProfileId = "worker-codegen-fast",
                    UsedThisHour = 0,
                    LimitPerHour = providerQuotaPerHour,
                    WindowStartedAt = DateTimeOffset.UtcNow,
                },
            ],
        });
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, quotaRepository);

        var fallbackDecision = routing.Route("repo-zeta", "worker", allowFallback: true);

        Assert.True(fallbackDecision.Allowed);
        Assert.True(fallbackDecision.UsedFallback);
        Assert.Equal("worker-codegen-fast", fallbackDecision.ProfileId);

        quotaRepository.Save(new ProviderQuotaSnapshot
        {
            Entries =
            [
                new ProviderQuotaEntry
                {
                    ProfileId = "worker-codegen-fast",
                    UsedThisHour = providerQuotaPerHour,
                    LimitPerHour = providerQuotaPerHour,
                    WindowStartedAt = DateTimeOffset.UtcNow,
                },
            ],
        });

        var deniedDecision = routing.Route("repo-zeta", "worker", allowFallback: true);

        Assert.False(deniedDecision.Allowed);
        Assert.Equal(ProviderRoutingDenialReason.QuotaExhausted, deniedDecision.DenialReason);
    }

    [Fact]
    public void PlatformSchedulerService_PrefersStarvedRepoWhenCapacityIsLimited()
    {
        using var workspace = new TemporaryWorkspace();
        var repoAlpha = CreateManagedRepo(workspace, "RepoAlphaFair");
        var repoBeta = CreateManagedRepo(workspace, "RepoBetaFair");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(repoAlpha, "repo-alpha", "default", "balanced");
        repoRegistry.Register(repoBeta, "repo-beta", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var gateway = new LocalRepoRuntimeGateway();
        var instanceManager = CreateRuntimeInstanceManager(workspace, repoRegistry, governance, gateway);
        var providerRouting = new ProviderRoutingService(
            new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, TestWorkerAdapterRegistryFactory.Create()),
            repoRegistry,
            governance,
            new JsonProviderQuotaRepository(workspace.Paths));
        providerRouting.GetQuotaSnapshot();
        var workerNodes = new JsonWorkerNodeRegistryRepository(workspace.Paths);
        workerNodes.Save(
        [
            new WorkerNode
            {
                NodeId = "local-default",
                Capabilities = new WorkerNodeCapabilities(true, false, false, false, 1, ["*"]),
                Status = WorkerNodeStatus.Healthy,
            },
        ]);
        instanceManager.List();
        var alphaInstance = instanceManager.Inspect("repo-alpha");
        alphaInstance.LastPlatformScheduledAt = DateTimeOffset.UtcNow;
        instanceManager.Update(alphaInstance);
        var betaInstance = instanceManager.Inspect("repo-beta");
        betaInstance.LastPlatformScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-90);
        instanceManager.Update(betaInstance);

        var scheduler = new PlatformSchedulerService(repoRegistry, instanceManager, governance, workerNodes, providerRouting);
        var decision = scheduler.Plan(1);

        Assert.Single(decision.SelectedRepoIds);
        Assert.Equal("repo-beta", decision.SelectedRepoIds[0]);
        Assert.Contains(decision.Candidates, candidate => candidate.RepoId == "repo-alpha" && !candidate.Selected);
        Assert.Equal(1, instanceManager.Inspect("repo-beta").PlatformSelectionCount);
    }

    [Fact]
    public void PlatformSchedulerService_BlocksAutoDispatchWhenRoleModeIsDisabled()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoSchedulerRoleDisabled");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-scheduler-disabled", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var gateway = new LocalRepoRuntimeGateway();
        var instanceManager = CreateRuntimeInstanceManager(workspace, repoRegistry, governance, gateway);
        var providers = new ProviderRegistryService(
            new JsonProviderRegistryRepository(workspace.Paths),
            repoRegistry,
            governance,
            TestWorkerAdapterRegistryFactory.Create());
        providers.List();
        var providerRouting = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        providerRouting.GetQuotaSnapshot();
        var workerNodes = new JsonWorkerNodeRegistryRepository(workspace.Paths);
        workerNodes.Save(
        [
            new WorkerNode
            {
                NodeId = "local-default",
                Capabilities = new WorkerNodeCapabilities(true, false, false, false, 1, ["*"]),
                Status = WorkerNodeStatus.Healthy,
            },
        ]);
        instanceManager.List();
        var runtimePolicyBundleService = new RuntimePolicyBundleService(
            workspace.Paths,
            governance,
            new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault()),
            providers);
        var scheduler = new PlatformSchedulerService(
            repoRegistry,
            instanceManager,
            governance,
            workerNodes,
            providerRouting,
            runtimePolicyBundleService);

        var decision = scheduler.Plan(1);

        Assert.Equal(0, decision.GrantedSlots);
        Assert.Empty(decision.SelectedRepoIds);
        Assert.Empty(decision.Candidates);
        Assert.Contains("scheduler auto-dispatch is closed", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, instanceManager.Inspect("repo-scheduler-disabled").PlatformSelectionCount);
    }

    [Fact]
    public void WorkerBroker_ExpireLease_BlocksTaskWhenTrackingIsLost()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoLease");
        var repoPaths = ControlPlanePaths.FromRepoRoot(managedRepo);
        var sessionRepository = new JsonRuntimeSessionRepository(repoPaths);
        var session = RuntimeSessionState.Start(managedRepo, dryRun: true);
        sessionRepository.Save(session);
        var graph = new Carves.Runtime.Domain.Tasks.TaskGraph();
        graph.AddOrReplace(new TaskNode
        {
            TaskId = "T-LEASE",
            Title = "Lease recovery task",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Running,
            Scope = ["src/Lease.cs"],
            Acceptance = ["lease recovery returns the task to pending"],
        });
        new JsonTaskGraphRepository(repoPaths).Save(graph);

        var broker = new WorkerBroker(
            new WorkerPool(1),
            new JsonWorkerNodeRegistryRepository(workspace.Paths),
            new JsonWorkerLeaseRepository(workspace.Paths),
            new LocalRepoRuntimeGateway());

        var lease = broker.Acquire(session, "T-LEASE");
        sessionRepository.Save(session);
        var expired = broker.ExpireLease(lease.LeaseId!, "Heartbeat lost.");
        var recoveredSession = sessionRepository.Load()!;
        var recoveredTask = new JsonTaskGraphRepository(repoPaths).Load().Tasks["T-LEASE"];

        Assert.True(lease.Acquired);
        Assert.Equal(WorkerLeaseStatus.Expired, expired.Status);
        Assert.Equal(Carves.Runtime.Domain.Tasks.TaskStatus.Blocked, recoveredTask.Status);
        Assert.Equal(RuntimeSessionStatus.Idle, recoveredSession.Status);
        Assert.Equal(0, recoveredSession.ActiveWorkerCount);
        Assert.Equal(WorkerRecoveryAction.EscalateToOperator, recoveredTask.LastRecoveryAction);
        Assert.Contains("expired", recoveredTask.LastRecoveryReason, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(workspace.Paths.PlatformWorkerLeasesLiveStateFile));
    }

    [Fact]
    public void PlatformVocabularyLintService_FlagsNamingDriftAndAllowsCanonicalVocabulary()
    {
        using var workspace = new TemporaryWorkspace();
        var allowedPath = Path.Combine(workspace.RootPath, "src", "CARVES.Runtime.Application", "Platform", "RepoRegistryService.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(allowedPath)!);
        File.WriteAllText(allowedPath, "namespace Carves.Runtime.Application.Platform; public sealed class RepoRegistryService { }");
        var warningPath = Path.Combine(workspace.RootPath, "src", "CARVES.Runtime.Application", "Platform", "RuntimeThingManager.cs");
        File.WriteAllText(warningPath, "namespace Carves.Runtime.Application.Platform; public sealed class RuntimeThingManager { }");
        var suggestionPath = Path.Combine(workspace.RootPath, "src", "CARVES.Runtime.Host", "ProviderHub.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(suggestionPath)!);
        File.WriteAllText(suggestionPath, "namespace Carves.Runtime.Host; public sealed class ProviderHub { }");

        var lint = new PlatformVocabularyLintService(workspace.RootPath, Carves.Runtime.Application.Configuration.CarvesCodeStandard.CreateDefault());
        var report = lint.Run();

        Assert.True(report.HasBlockingViolations);
        Assert.Contains(report.Findings, finding => finding.RuleId == "CARVES003" && finding.SymbolName == "RuntimeThingManager");
        Assert.Contains(report.Findings, finding => finding.RuleId == "CARVES012" && finding.SymbolName == "ProviderHub");
        Assert.DoesNotContain(report.Findings, finding => finding.SymbolName == "RepoRegistryService");
    }

    private static string CreateManagedRepo(TemporaryWorkspace workspace, string name)
    {
        var repoRoot = Path.Combine(workspace.RootPath, "managed", name);
        Directory.CreateDirectory(repoRoot);
        Directory.CreateDirectory(Path.Combine(repoRoot, ".ai", "config"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".ai", "runtime"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".ai", "tasks", "nodes"));
        Directory.CreateDirectory(Path.Combine(repoRoot, ".ai", "opportunities"));
        File.WriteAllText(Path.Combine(repoRoot, ".ai", "tasks", "graph.json"), """
{
  "version": 1,
  "updated_at": "2026-03-16T00:00:00Z",
  "cards": [],
  "tasks": []
}
""");
        File.WriteAllText(Path.Combine(repoRoot, ".ai", "STATE.md"), "Runtime stage: Stage-4 continuous development runtime");
        return repoRoot;
    }

    private static WorkerPermissionOrchestrationService CreatePermissionOrchestrationService(
        TemporaryWorkspace workspace,
        RepoRegistryService repoRegistryService,
        PlatformGovernanceService governanceService)
    {
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var sessionRepository = new JsonRuntimeSessionRepository(workspace.Paths);
        var taskGraphService = new TaskGraphService(new JsonTaskGraphRepository(workspace.Paths), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        return new WorkerPermissionOrchestrationService(
            workspace.RootPath,
            new WorkerPermissionInterpreter(),
            new ApprovalPolicyEngine(workspace.RootPath, repoRegistryService, governanceService),
            new JsonRuntimeArtifactRepository(workspace.Paths),
            new InMemoryWorkerPermissionAuditRepository(),
            governanceService,
            repoRegistryService,
            taskGraphService,
            sessionRepository,
            new NoOpMarkdownSyncService(),
            new RuntimeIncidentTimelineService(new JsonRuntimeIncidentTimelineRepository(workspace.Paths), operatorOsEventStream),
            new PlannerWakeBridgeService(
                workspace.RootPath,
                sessionRepository,
                new NoOpMarkdownSyncService(),
                taskGraphService,
                operatorOsEventStream));
    }

    private static RuntimeInstanceManager CreateRuntimeInstanceManager(
        TemporaryWorkspace workspace,
        RepoRegistryService repoRegistryService,
        PlatformGovernanceService governanceService,
        IRepoRuntimeGateway gateway)
    {
        var repoRuntimeService = new RepoRuntimeService(
            new JsonRepoRuntimeRegistryRepository(workspace.Paths),
            PlatformIdentity.CreateHostId(workspace.RootPath, Environment.MachineName));
        return new RuntimeInstanceManager(
            new JsonRuntimeInstanceRepository(workspace.Paths),
            repoRegistryService,
            repoRuntimeService,
            gateway,
            governanceService,
            new RepoTruthProjectionService(gateway));
    }
}
