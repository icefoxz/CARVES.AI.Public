using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.Safety;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Infrastructure.AI;
using Carves.Runtime.Infrastructure.Audit;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.ControlPlane;
using Carves.Runtime.Infrastructure.Git;
using Carves.Runtime.Infrastructure.Persistence;
using Carves.Runtime.Infrastructure.Processes;

namespace Carves.Runtime.Host;

internal static class RuntimeCompositionFactories
{
    public static RuntimeBootstrapContext LoadBootstrapContext(string repoRoot)
    {
        var paths = ControlPlanePaths.FromRepoRoot(repoRoot);
        var configRepository = new FileControlPlaneConfigRepository(paths);
        return new RuntimeBootstrapContext(
            paths,
            configRepository,
            configRepository.LoadSystemConfig(),
            configRepository.LoadAiProviderConfig(),
            configRepository.LoadPlannerAutonomyPolicy(),
            configRepository.LoadWorkerOperationalPolicy(),
            configRepository.LoadSafetyRules(),
            configRepository.LoadModuleDependencyMap());
    }

    public static RuntimeInfrastructureServices CreateInfrastructure(RuntimeBootstrapContext bootstrap)
    {
        var processRunner = new ProcessRunner();
        var gitClient = new GitClient(processRunner);
        var aiClient = AiClientFactory.Create(bootstrap.AiProviderConfig);
        var plannerAdapterRegistry = PlannerAdapterFactory.Create(bootstrap.AiProviderConfig);
        var workerAdapterRegistry = WorkerAdapterFactory.Create(bootstrap.AiProviderConfig, aiClient);
        IRuntimeArtifactRepository artifactRepository = new JsonRuntimeArtifactRepository(bootstrap.Paths, new ControlPlaneLockService(bootstrap.Paths.RepoRoot));
        var workerExecutionAuditReadModel = new SqliteWorkerExecutionAuditReadModel(Path.Combine(bootstrap.Paths.RuntimeRoot, "audit.db"));
        return new RuntimeInfrastructureServices(processRunner, gitClient, aiClient, plannerAdapterRegistry, workerAdapterRegistry, artifactRepository, workerExecutionAuditReadModel);
    }

    public static PlannerService CreatePlannerService(
        TaskGraphService taskGraphService,
        ICodeGraphBuilder codeGraphBuilder,
        ICodeGraphQueryService codeGraphQueryService,
        Carves.Runtime.Application.Git.IGitClient gitClient,
        TaskDecomposer taskDecomposer)
    {
        return new PlannerService(new CardParser(), taskDecomposer, gitClient, taskGraphService, codeGraphBuilder, codeGraphQueryService);
    }

    public static WorkerRequestFactory CreateWorkerRequestFactory(
        string repoRoot,
        RuntimeBootstrapContext bootstrap,
        RuntimeInfrastructureServices infrastructure,
        MemoryService memoryService,
        ContextPackService contextPackService,
        WorkerExecutionBoundaryService workerExecutionBoundaryService,
        WorkerSelectionPolicyService workerSelectionPolicyService,
        WorktreeRuntimeService worktreeRuntimeService,
        RuntimeIncidentTimelineService incidentTimelineService,
        ExecutionPacketCompilerService executionPacketCompilerService,
        FormalPlanningExecutionGateService formalPlanningExecutionGateService)
    {
        var workerConfig = bootstrap.AiProviderConfig.ResolveForRole("worker");
        return new WorkerRequestFactory(
            repoRoot,
            bootstrap.SystemConfig,
            infrastructure.GitClient,
            infrastructure.WorkerAdapterRegistry,
            new WorkerAiRequestFactory(
                workerConfig.MaxOutputTokens,
                workerConfig.RequestTimeoutSeconds,
                workerConfig.Model,
                workerConfig.ReasoningEffort),
            new WorktreeManager(infrastructure.GitClient),
            worktreeRuntimeService,
            incidentTimelineService,
            memoryService,
            contextPackService,
            workerExecutionBoundaryService,
            workerSelectionPolicyService,
            executionPacketCompilerService,
            formalPlanningExecutionGateService,
            new RuntimePackVerificationRecipeAdmissionService(
                repoRoot,
                bootstrap.Paths,
                infrastructure.ArtifactRepository));
    }

    public static WorkerService CreateWorkerService(
        RuntimeBootstrapContext bootstrap,
        RuntimeInfrastructureServices infrastructure,
        SafetyService safetyService,
        WorkerExecutionBoundaryService workerExecutionBoundaryService,
        WorkerPermissionOrchestrationService workerPermissionOrchestrationService,
        RuntimeIncidentTimelineService incidentTimelineService,
        ActorSessionService actorSessionService,
        OperatorOsEventStreamService operatorOsEventStreamService)
    {
        return new WorkerService(
            bootstrap.SystemConfig,
            bootstrap.SafetyRules,
            bootstrap.ModuleDependencyMap,
            infrastructure.WorkerAdapterRegistry,
            infrastructure.ProcessRunner,
            new WorktreeManager(infrastructure.GitClient),
            safetyService,
            infrastructure.ArtifactRepository,
            workerExecutionBoundaryService,
            workerPermissionOrchestrationService,
            incidentTimelineService,
            actorSessionService,
            operatorOsEventStreamService,
            new ExecutionEvidenceRecorder(bootstrap.Paths),
            infrastructure.WorkerExecutionAuditReadModel);
    }

    public static DevLoopService CreateDevLoopService(
        string repoRoot,
        TaskGraphService taskGraphService,
        WorkerRequestFactory workerRequestFactory,
        WorkerService workerService,
        WorkerBroker workerBroker,
        PlannerHostService plannerHostService,
        PlannerWakeBridgeService plannerWakeBridgeService,
        IRuntimeSessionRepository sessionRepository,
        IMarkdownSyncService markdownSyncService,
        IRuntimeArtifactRepository artifactRepository,
        FailureReportService failureReportService,
        PlannerReviewArtifactFactory reviewArtifactFactory,
        RuntimeFailurePolicy runtimeFailurePolicy,
        RecoveryPolicyEngine recoveryPolicyEngine,
        WorktreeRuntimeService worktreeRuntimeService,
        RuntimeIncidentTimelineService incidentTimelineService,
        ActorSessionService actorSessionService,
        FormalPlanningExecutionGateService formalPlanningExecutionGateService)
    {
        return new DevLoopService(
            repoRoot,
            taskGraphService,
            new PlannerWorkerCycle(
                taskGraphService,
                workerRequestFactory,
                workerService,
                new PlannerReviewService(),
                new TaskTransitionPolicy(),
                artifactRepository,
                reviewArtifactFactory,
                formalPlanningExecutionGateService),
            workerBroker,
            plannerHostService,
            plannerWakeBridgeService,
            sessionRepository,
            markdownSyncService,
            artifactRepository,
            failureReportService,
            runtimeFailurePolicy,
            recoveryPolicyEngine,
            worktreeRuntimeService,
            incidentTimelineService,
            actorSessionService,
            formalPlanningExecutionGateService);
    }

    public static IRefactoringService CreateRefactoringService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        Carves.Runtime.Application.Git.IGitClient gitClient,
        TaskGraphService taskGraphService)
    {
        return new RefactoringService(
            repoRoot,
            systemConfig,
            gitClient,
            taskGraphService,
            new RefactoringBacklogRepository(paths));
    }

    public static OperatorSurfaceService CreateOperatorSurfaceService(
        string repoRoot,
        RuntimeBootstrapContext bootstrap,
        RuntimeInfrastructureServices infrastructure,
        IControlPlaneLockService controlPlaneLockService,
        TaskGraphService taskGraphService,
        PlannerService plannerService,
        DevLoopService devLoopService,
        IMarkdownSyncService markdownSyncService,
        ICodeGraphBuilder codeGraphBuilder,
        ICodeGraphQueryService codeGraphQueryService,
        IRefactoringService refactoringService,
        SafetyService safetyService,
        PlannerReviewArtifactFactory reviewArtifactFactory,
        ReviewWritebackService reviewWritebackService,
        ReviewEvidenceGateService reviewEvidenceGateService,
        RuntimeFailurePolicy runtimeFailurePolicy,
        FailureReportService failureReportService,
        FailureContextService failureContextService,
        FailureSummaryService failureSummaryService,
        PlannerTriggerService plannerTriggerService,
        DispatchProjectionService dispatchProjectionService,
        ExecutionRunService executionRunService,
        ResultIngestionService resultIngestionService,
        OpportunityDetectorService opportunityDetectorService,
        HostRegistryService hostRegistryService,
        RepoRegistryService repoRegistryService,
        RepoRuntimeService repoRuntimeService,
        RuntimeInstanceManager runtimeInstanceManager,
        ProviderRegistryService providerRegistryService,
        ProviderRoutingService providerRoutingService,
        PlatformGovernanceService platformGovernanceService,
        PlatformSchedulerService platformSchedulerService,
        ActorSessionService actorSessionService,
        SessionOwnershipService sessionOwnershipService,
        ConcurrentActorArbitrationService concurrentActorArbitrationService,
        OperatorOsEventStreamService operatorOsEventStreamService,
        PlatformVocabularyLintService platformVocabularyLintService,
        OperatorApiService operatorApiService,
        PlatformDashboardService platformDashboardService,
        RuntimeConsistencyCheckService runtimeConsistencyCheckService,
        DelegatedWorkerLifecycleReconciliationService delegatedWorkerLifecycleReconciliationService,
        OperationalSummaryService operationalSummaryService,
        GovernanceReportingService governanceReportingService,
        DelegationReportingService delegationReportingService,
        WorktreeResourceCleanupService worktreeResourceCleanupService,
        WorkerBroker workerBroker,
        InteractionLayerService interactionLayerService,
        WorkerExecutionBoundaryService workerExecutionBoundaryService,
        WorkerSelectionPolicyService workerSelectionPolicyService,
        ApprovalPolicyEngine approvalPolicyEngine,
        WorkerPermissionOrchestrationService workerPermissionOrchestrationService,
        IWorkerExecutionAuditReadModel workerExecutionAuditReadModel,
        RuntimePolicyBundleService runtimePolicyBundleService,
        PlanningDraftService planningDraftService,
        ManagedWorkspaceLeaseService managedWorkspaceLeaseService,
        ContextPackService contextPackService,
        CurrentModelQualificationService currentModelQualificationService,
        RoutingValidationService routingValidationService,
        RoutingPromotionDecisionService routingPromotionDecisionService,
        ValidationCoverageMatrixService validationCoverageMatrixService,
        RoutingCandidateReadinessService routingCandidateReadinessService,
        SpecificationValidationService specificationValidationService,
        ExecutionPacketCompilerService executionPacketCompilerService)
    {
        return new OperatorSurfaceService(
            repoRoot,
            bootstrap.Paths,
            bootstrap.SystemConfig,
            bootstrap.AiProviderConfig,
            infrastructure.AiClient,
            infrastructure.GitClient,
            controlPlaneLockService,
            bootstrap.ConfigRepository,
            taskGraphService,
            plannerService,
            devLoopService,
            markdownSyncService,
            codeGraphBuilder,
            codeGraphQueryService,
            refactoringService,
            safetyService,
            infrastructure.ArtifactRepository,
            reviewArtifactFactory,
            reviewWritebackService,
            reviewEvidenceGateService,
            runtimeFailurePolicy,
            failureReportService,
            failureContextService,
            failureSummaryService,
            plannerTriggerService,
            dispatchProjectionService,
            executionRunService,
            resultIngestionService,
            opportunityDetectorService,
            hostRegistryService,
            repoRegistryService,
            repoRuntimeService,
            runtimeInstanceManager,
            providerRegistryService,
            providerRoutingService,
            platformGovernanceService,
            platformSchedulerService,
            actorSessionService,
            sessionOwnershipService,
            concurrentActorArbitrationService,
            operatorOsEventStreamService,
            platformVocabularyLintService,
            operatorApiService,
            platformDashboardService,
            runtimeConsistencyCheckService,
            delegatedWorkerLifecycleReconciliationService,
            operationalSummaryService,
            governanceReportingService,
            delegationReportingService,
            worktreeResourceCleanupService,
            workerBroker,
            interactionLayerService,
            workerExecutionBoundaryService,
            workerSelectionPolicyService,
            approvalPolicyEngine,
            workerPermissionOrchestrationService,
            workerExecutionAuditReadModel,
            runtimePolicyBundleService,
            planningDraftService,
            managedWorkspaceLeaseService,
            contextPackService,
            currentModelQualificationService,
            routingValidationService,
            routingPromotionDecisionService,
            validationCoverageMatrixService,
            routingCandidateReadinessService,
            specificationValidationService,
            executionPacketCompilerService);
    }
}
