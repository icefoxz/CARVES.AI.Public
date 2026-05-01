using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Failures;
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
using Carves.Runtime.Application.Git;
using Carves.Runtime.Infrastructure.AI;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.ControlPlane;
using Carves.Runtime.Infrastructure.Git;
using Carves.Runtime.Infrastructure.Memory;
using Carves.Runtime.Infrastructure.Persistence;
using Carves.Runtime.Infrastructure.Platform;
using Carves.Runtime.Infrastructure.Processes;

namespace Carves.Runtime.Host;

public static partial class RuntimeComposition
{
    private static RuntimeServices CreateRuntimeServicesCore(string repoRoot)
    {
        var bootstrap = LoadBootstrapContext(repoRoot);
        var lockService = new ControlPlaneLockService(repoRoot);
        var infrastructure = CreateInfrastructure(bootstrap);
        var hostRegistryRepository = new JsonHostRegistryRepository(bootstrap.Paths, lockService);
        var repoRegistryRepository = new JsonRepoRegistryRepository(bootstrap.Paths, lockService);
        var repoRuntimeRegistryRepository = new JsonRepoRuntimeRegistryRepository(bootstrap.Paths, lockService);
        var runtimeInstanceRepository = new JsonRuntimeInstanceRepository(bootstrap.Paths, lockService);
        var providerRegistryRepository = new JsonProviderRegistryRepository(bootstrap.Paths, lockService);
        var providerQuotaRepository = new JsonProviderQuotaRepository(bootstrap.Paths, lockService);
        var runtimeRoutingProfileRepository = new JsonRuntimeRoutingProfileRepository(bootstrap.Paths, lockService);
        var qualificationRepository = new JsonCurrentModelQualificationRepository(bootstrap.Paths, lockService);
        var routingValidationRepository = new JsonRoutingValidationRepository(bootstrap.Paths, lockService);
        var platformGovernanceRepository = new JsonPlatformGovernanceRepository(bootstrap.Paths, lockService);
        var workerNodeRegistryRepository = new JsonWorkerNodeRegistryRepository(bootstrap.Paths, lockService);
        var workerLeaseRepository = new JsonWorkerLeaseRepository(bootstrap.Paths, lockService);
        var workerSupervisorStateRepository = new JsonWorkerSupervisorStateRepository(bootstrap.Paths, lockService);
        var delegatedRunLifecycleRepository = new JsonDelegatedRunLifecycleRepository(bootstrap.Paths, lockService);
        var delegatedRunRecoveryLedgerRepository = new JsonDelegatedRunRecoveryLedgerRepository(bootstrap.Paths, lockService);
        var providerHealthRepository = new JsonProviderHealthRepository(bootstrap.Paths, lockService);
        var incidentTimelineRepository = new JsonRuntimeIncidentTimelineRepository(bootstrap.Paths, lockService);
        var worktreeRuntimeRepository = new JsonWorktreeRuntimeRepository(bootstrap.Paths, lockService);
        var managedWorkspaceLeaseRepository = new JsonManagedWorkspaceLeaseRepository(bootstrap.Paths, lockService);
        var actorSessionRepository = new JsonActorSessionRepository(bootstrap.Paths, lockService);
        var ownershipRepository = new JsonOwnershipRepository(bootstrap.Paths, lockService);
        var operatorOsEventRepository = new JsonOperatorOsEventRepository(bootstrap.Paths, lockService);
        var platformGovernanceService = new PlatformGovernanceService(platformGovernanceRepository);
        var repoRuntimeGateway = new LocalRepoRuntimeGateway();
        var hostRegistryService = new HostRegistryService(hostRegistryRepository);
        var repoRegistryService = new RepoRegistryService(repoRegistryRepository);
        var repoRuntimeService = new RepoRuntimeService(repoRuntimeRegistryRepository, LocalHostPaths.GetHostId(repoRoot));
        var workerOperationalPolicyService = new WorkerOperationalPolicyService(repoRoot, repoRegistryService, bootstrap.WorkerOperationalPolicy);
        var providerRegistryService = new ProviderRegistryService(providerRegistryRepository, repoRegistryService, platformGovernanceService, infrastructure.WorkerAdapterRegistry);
        var runtimePolicyBundleService = new RuntimePolicyBundleService(bootstrap.Paths, platformGovernanceService, workerOperationalPolicyService, providerRegistryService);
        var providerHealthMonitorService = new ProviderHealthMonitorService(providerHealthRepository, providerRegistryService, infrastructure.WorkerAdapterRegistry, workerOperationalPolicyService);
        var runtimeRoutingProfileService = new RuntimeRoutingProfileService(runtimeRoutingProfileRepository, qualificationRepository);
        var qualificationLaneExecutor = new QualificationLaneExecutor(repoRoot, new HttpTransportClient());
        var currentModelQualificationService = new CurrentModelQualificationService(qualificationRepository, runtimeRoutingProfileRepository, qualificationLaneExecutor);
        var operatorOsEventStreamService = new OperatorOsEventStreamService(operatorOsEventRepository, bootstrap.Paths);
        var workerSupervisorStateService = new WorkerSupervisorStateService(workerSupervisorStateRepository);
        var actorSessionService = new ActorSessionService(actorSessionRepository, operatorOsEventStreamService, workerSupervisorStateService);
        var incidentTimelineService = new RuntimeIncidentTimelineService(incidentTimelineRepository, operatorOsEventStreamService);
        var sessionOwnershipService = new SessionOwnershipService(ownershipRepository, actorSessionService, operatorOsEventStreamService);
        var concurrentActorArbitrationService = new ConcurrentActorArbitrationService(sessionOwnershipService, incidentTimelineService, operatorOsEventStreamService);
        var providerRoutingService = new ProviderRoutingService(providerRegistryService, repoRegistryService, platformGovernanceService, providerQuotaRepository);
        var projectionService = new RepoTruthProjectionService(repoRuntimeGateway);
        var runtimeInstanceManager = new RuntimeInstanceManager(runtimeInstanceRepository, repoRegistryService, repoRuntimeService, repoRuntimeGateway, platformGovernanceService, projectionService);
        var platformSchedulerService = new PlatformSchedulerService(
            repoRegistryService,
            runtimeInstanceManager,
            platformGovernanceService,
            workerNodeRegistryRepository,
            providerRoutingService,
            runtimePolicyBundleService);
        var platformVocabularyLintService = new PlatformVocabularyLintService(repoRoot, bootstrap.ConfigRepository.LoadCarvesCodeStandard());
        var sessionRepository = new JsonRuntimeSessionRepository(bootstrap.Paths, lockService);
        var intentDraftRepository = new JsonIntentDraftRepository(bootstrap.Paths, lockService);
        var cardDraftRepository = new JsonCardDraftRepository(bootstrap.Paths, lockService);
        var taskGraphDraftRepository = new JsonTaskGraphDraftRepository(bootstrap.Paths, lockService);
        var workerPermissionAuditRepository = new JsonWorkerPermissionAuditRepository(bootstrap.Paths, lockService);
        var reviewArtifactFactory = new PlannerReviewArtifactFactory();
        var reviewEvidenceGateService = new ReviewEvidenceGateService();
        var codeGraphBuilder = new FileCodeGraphBuilder(repoRoot, bootstrap.Paths, bootstrap.SystemConfig);
        var codeGraphQueryService = new FileCodeGraphQueryService(bootstrap.Paths, codeGraphBuilder);
        var taskDecomposer = new TaskDecomposer();
        var projectUnderstandingProjectionService = new ProjectUnderstandingProjectionService(repoRoot, bootstrap.Paths, bootstrap.SystemConfig, codeGraphBuilder, codeGraphQueryService);
        var intentDiscoveryService = new IntentDiscoveryService(repoRoot, bootstrap.Paths, intentDraftRepository, projectUnderstandingProjectionService);
        var formalPlanningExecutionGateService = new FormalPlanningExecutionGateService(intentDiscoveryService, managedWorkspaceLeaseRepository);
        var taskGraphRepository = new JsonTaskGraphRepository(bootstrap.Paths, lockService);
        var taskGraphService = new TaskGraphService(taskGraphRepository, new Carves.Runtime.Application.TaskGraph.TaskScheduler(formalPlanningExecutionGateService: formalPlanningExecutionGateService), lockService);
        var plannerService = CreatePlannerService(taskGraphService, codeGraphBuilder, codeGraphQueryService, infrastructure.GitClient, taskDecomposer);
        var memoryService = new MemoryService(new FileMemoryRepository(bootstrap.Paths), new ExecutionContextBuilder());
        var safetyService = new SafetyService(SafetyValidatorCatalog.CreateDefault());
        var runtimeFailurePolicy = new RuntimeFailurePolicy();
        var failureReportRepository = new JsonFailureReportRepository(bootstrap.Paths, lockService);
        var failureReportService = new FailureReportService(
            repoRoot,
            failureReportRepository,
            new FailureClassificationService(),
            infrastructure.ArtifactRepository);
        var failureContextService = new FailureContextService(failureReportRepository);
        var failureSummaryService = new FailureSummaryService(failureContextService);
        var plannerTriggerService = new PlannerTriggerService(failureContextService);
        var planningDraftService = new PlanningDraftService(bootstrap.Paths, taskGraphService, cardDraftRepository, taskGraphDraftRepository, runtimePolicyBundleService, formalPlanningExecutionGateService);
        var formalPlanningPacketService = new FormalPlanningPacketService(intentDiscoveryService, planningDraftService, taskGraphService);
        var conversationProtocolService = new ConversationProtocolService(taskGraphService, intentDiscoveryService);
        var promptProtocolService = new PromptProtocolService(repoRoot);
        var promptKernelService = new PromptKernelService(repoRoot);
        var interactionLayerService = new InteractionLayerService(intentDiscoveryService, conversationProtocolService, promptProtocolService, promptKernelService, projectUnderstandingProjectionService);
        var workerPool = new WorkerPool(bootstrap.SystemConfig.MaxParallelTasks);
        var workerBroker = new WorkerBroker(workerPool, workerNodeRegistryRepository, workerLeaseRepository, repoRuntimeGateway);
        var workerExecutionBoundaryService = new WorkerExecutionBoundaryService(repoRoot, repoRegistryService, platformGovernanceService, workerOperationalPolicyService, runtimePolicyBundleService);
        var approvalPolicyEngine = new ApprovalPolicyEngine(repoRoot, repoRegistryService, platformGovernanceService, workerOperationalPolicyService, runtimePolicyBundleService);
        var workerSelectionPolicyService = new WorkerSelectionPolicyService(repoRoot, repoRegistryService, providerRegistryService, providerRoutingService, platformGovernanceService, infrastructure.WorkerAdapterRegistry, workerExecutionBoundaryService, providerHealthMonitorService, workerOperationalPolicyService, runtimePolicyBundleService, runtimeRoutingProfileService);
        var routingValidationService = new RoutingValidationService(routingValidationRepository, currentModelQualificationService, qualificationLaneExecutor, workerSelectionPolicyService, runtimeRoutingProfileService);
        var routingPromotionDecisionService = new RoutingPromotionDecisionService(currentModelQualificationService, routingValidationService);
        var validationCoverageMatrixService = new ValidationCoverageMatrixService(currentModelQualificationService, routingValidationService);
        var routingCandidateReadinessService = new RoutingCandidateReadinessService(routingPromotionDecisionService, validationCoverageMatrixService);
        var specificationValidationService = new SpecificationValidationService(repoRoot, bootstrap.Paths, bootstrap.ConfigRepository);
        var worktreeRuntimeService = new WorktreeRuntimeService(repoRoot, infrastructure.GitClient, worktreeRuntimeRepository);
        var managedWorkspacePathPolicyService = new ManagedWorkspacePathPolicyService(repoRoot, managedWorkspaceLeaseRepository);
        var memoryPatternWritebackRouteAuthorizationService = new MemoryPatternWritebackRouteAuthorizationService(repoRoot);
        var reviewWritebackService = new ReviewWritebackService(
            repoRoot,
            infrastructure.GitClient,
            managedWorkspacePathPolicyService,
            memoryPatternWritebackRouteAuthorizationService);
        var reviewEvidenceProjectionService = new ReviewEvidenceProjectionService(reviewEvidenceGateService, reviewWritebackService);
        var managedWorkspaceLeaseService = new ManagedWorkspaceLeaseService(
            repoRoot,
            bootstrap.SystemConfig,
            formalPlanningPacketService,
            taskGraphService,
            infrastructure.GitClient,
            new WorktreeManager(infrastructure.GitClient),
            worktreeRuntimeService,
            managedWorkspaceLeaseRepository);
        var markdownSyncService = new MarkdownSyncService(bootstrap.Paths, new MarkdownProjector(), lockService);
        var plannerWakeBridgeService = new PlannerWakeBridgeService(
            repoRoot,
            sessionRepository,
            markdownSyncService,
            taskGraphService,
            operatorOsEventStreamService);
        var workerPermissionOrchestrationService = new WorkerPermissionOrchestrationService(
            repoRoot,
            new WorkerPermissionInterpreter(),
            approvalPolicyEngine,
            infrastructure.ArtifactRepository,
            workerPermissionAuditRepository,
            platformGovernanceService,
            repoRegistryService,
            taskGraphService,
            sessionRepository,
            markdownSyncService,
            incidentTimelineService,
            plannerWakeBridgeService);
        var runtimeConsistencyCheckService = new RuntimeConsistencyCheckService(
            repoRoot,
            bootstrap.Paths,
            taskGraphService,
            sessionRepository,
            workerLeaseRepository,
            infrastructure.ArtifactRepository,
            delegatedRunLifecycleRepository);
        var executionRunService = new ExecutionRunService(bootstrap.Paths, infrastructure.ArtifactRepository);
        var delegatedWorkerLifecycleReconciliationService = new DelegatedWorkerLifecycleReconciliationService(
            bootstrap.Paths,
            taskGraphService,
            sessionRepository,
            workerLeaseRepository,
            worktreeRuntimeService,
            infrastructure.ArtifactRepository,
            delegatedRunLifecycleRepository,
            delegatedRunRecoveryLedgerRepository);
        var failureSummaryProjectionService = new FailureSummaryProjectionService(bootstrap.Paths, failureContextService, executionRunService);
        var contextPackService = new ContextPackService(bootstrap.Paths, taskGraphService, codeGraphQueryService, memoryService, failureSummaryProjectionService, executionRunService, infrastructure.ArtifactRepository);
        var plannerIntentRoutingService = new PlannerIntentRoutingService();
        var executionPacketCompilerService = new ExecutionPacketCompilerService(bootstrap.Paths, taskGraphService, codeGraphQueryService, memoryService, plannerIntentRoutingService, lockService, formalPlanningExecutionGateService);
        var workerRequestFactory = CreateWorkerRequestFactory(repoRoot, bootstrap, infrastructure, memoryService, contextPackService, workerExecutionBoundaryService, workerSelectionPolicyService, worktreeRuntimeService, incidentTimelineService, executionPacketCompilerService, formalPlanningExecutionGateService);
        var workerService = CreateWorkerService(bootstrap, infrastructure, safetyService, workerExecutionBoundaryService, workerPermissionOrchestrationService, incidentTimelineService, actorSessionService, operatorOsEventStreamService);
        var refactoringService = CreateRefactoringService(repoRoot, bootstrap.Paths, bootstrap.SystemConfig, infrastructure.GitClient, taskGraphService);
        var opportunityRepository = new JsonOpportunityRepository(bootstrap.Paths, lockService);
        var memoryAuditService = new MemoryAuditService(memoryService, codeGraphQueryService);
        var opportunityDetectorService = new OpportunityDetectorService(
            opportunityRepository,
            [
                new RefactoringOpportunityDetector(refactoringService),
                new FailureOpportunityDetector(infrastructure.ArtifactRepository),
                new CodeGraphOpportunityDetector(codeGraphQueryService),
                new MemoryDriftOpportunityDetector(memoryAuditService),
                new TestCoverageOpportunityDetector(codeGraphQueryService),
            ],
            codeGraphBuilder,
            taskGraphService);
        var opportunityTaskPipeline = new OpportunityTaskPipeline(repoRoot, taskGraphService, taskDecomposer, infrastructure.GitClient, bootstrap.SystemConfig, codeGraphQueryService);
        var opportunityEvaluator = new PlannerOpportunityEvaluator(bootstrap.PlannerAutonomyPolicy, opportunityTaskPipeline, opportunityRepository);
        var plannerContextAssembler = new PlannerContextAssembler(taskGraphService, codeGraphQueryService, memoryService, contextPackService, bootstrap.ConfigRepository.LoadCarvesCodeStandard(), bootstrap.PlannerAutonomyPolicy, plannerIntentRoutingService);
        var plannerProposalAcceptanceService = new PlannerProposalAcceptanceService(taskGraphService, opportunityRepository);
        var plannerHostService = new PlannerHostService(
            taskGraphService,
            opportunityDetectorService,
            opportunityEvaluator,
            infrastructure.PlannerAdapterRegistry,
            plannerContextAssembler,
            new PlannerProposalValidator(),
            plannerProposalAcceptanceService,
            infrastructure.ArtifactRepository);
        var recoveryPolicyEngine = new RecoveryPolicyEngine(bootstrap.SafetyRules, workerSelectionPolicyService, providerHealthMonitorService, workerOperationalPolicyService);
        var devLoopService = CreateDevLoopService(repoRoot, taskGraphService, workerRequestFactory, workerService, workerBroker, plannerHostService, plannerWakeBridgeService, sessionRepository, markdownSyncService, infrastructure.ArtifactRepository, failureReportService, reviewArtifactFactory, runtimeFailurePolicy, recoveryPolicyEngine, worktreeRuntimeService, incidentTimelineService, actorSessionService, formalPlanningExecutionGateService);
        var runtimeSessionGatewayService = new RuntimeSessionGatewayService(
            repoRoot,
            actorSessionService,
            operatorOsEventStreamService,
            () => devLoopService.GetSession()?.SessionId,
            roleGovernancePolicyProvider: runtimePolicyBundleService.LoadRoleGovernancePolicy);
        var operatorApiService = new OperatorApiService(repoRegistryService, runtimeInstanceManager, providerRegistryService, providerRoutingService, platformSchedulerService, workerNodeRegistryRepository, workerLeaseRepository, repoRuntimeGateway, workerExecutionBoundaryService, workerSelectionPolicyService, workerPermissionOrchestrationService, providerHealthMonitorService, runtimeRoutingProfileService, incidentTimelineService, actorSessionService, sessionOwnershipService, operatorOsEventStreamService);
        var runtimeNoiseAuditService = new RuntimeNoiseAuditService(taskGraphService, operatorApiService);
        var operatorActionabilityService = new OperatorActionabilityService();
        var providerHealthActionabilityProjectionService = new ProviderHealthActionabilityProjectionService();
        var dispatchProjectionService = new DispatchProjectionService(formalPlanningExecutionGateService: formalPlanningExecutionGateService);
        var operationalSummaryService = new OperationalSummaryService(bootstrap.Paths, taskGraphService, devLoopService, workerPermissionOrchestrationService, incidentTimelineService, providerHealthMonitorService, workerSelectionPolicyService, workerOperationalPolicyService, operatorOsEventStreamService, runtimeNoiseAuditService, operatorActionabilityService, providerHealthActionabilityProjectionService);
        RuntimeGovernanceProgramReauditSurface BuildRuntimeGovernanceProgramReauditSurface()
        {
            return new RuntimeGovernanceProgramReauditService(
                repoRoot,
                bootstrap.Paths,
                bootstrap.SystemConfig,
                runtimePolicyBundleService.LoadRoleGovernancePolicy(),
                infrastructure.ArtifactRepository,
                codeGraphQueryService,
                refactoringService,
                taskGraphService).Build();
        }

        RuntimeSessionGatewayDogfoodValidationSurface BuildRuntimeSessionGatewayDogfoodValidationSurface()
        {
            return new RuntimeSessionGatewayDogfoodValidationService(
                repoRoot,
                BuildRuntimeGovernanceProgramReauditSurface).Build();
        }

        RuntimeSessionGatewayPrivateAlphaHandoffSurface BuildRuntimeSessionGatewayPrivateAlphaHandoffSurface()
        {
            return new RuntimeSessionGatewayPrivateAlphaHandoffService(
                repoRoot,
                BuildRuntimeSessionGatewayDogfoodValidationSurface,
                () => operationalSummaryService.Build(refreshProviderHealth: false),
                () => new RuntimeHealthCheckService(bootstrap.Paths, taskGraphService).Evaluate()).Build();
        }

        RuntimeSessionGatewayRepeatabilitySurface BuildRuntimeSessionGatewayRepeatabilitySurface()
        {
            return new RuntimeSessionGatewayRepeatabilityService(
                repoRoot,
                BuildRuntimeSessionGatewayPrivateAlphaHandoffSurface,
                taskGraphService,
                infrastructure.ArtifactRepository,
                operatorOsEventStreamService,
                reviewEvidenceProjectionService).Build();
        }

        RuntimeSessionGatewayGovernanceAssistSurface BuildRuntimeSessionGatewayGovernanceAssistSurface()
        {
            return new RuntimeSessionGatewayGovernanceAssistService(
                repoRoot,
                BuildRuntimeSessionGatewayRepeatabilitySurface).Build();
        }

        RuntimeAcceptanceContractIngressPolicySurface BuildRuntimeAcceptanceContractIngressPolicySurface()
        {
            return new RuntimeAcceptanceContractIngressPolicyService(repoRoot).Build();
        }

        RuntimeAgentWorkingModesSurface BuildRuntimeAgentWorkingModesSurface()
        {
            return new RuntimeAgentWorkingModesService(
                repoRoot,
                intentDiscoveryService,
                formalPlanningPacketService,
                managedWorkspaceLeaseService).Build();
        }

        RuntimeFormalPlanningPostureSurface BuildRuntimeFormalPlanningPostureSurface()
        {
            return new RuntimeFormalPlanningPostureService(
                repoRoot,
                intentDiscoveryService,
                formalPlanningPacketService,
                managedWorkspaceLeaseService,
                taskGraphService,
                dispatchProjectionService,
                () => devLoopService.GetSession(),
                bootstrap.SystemConfig.MaxParallelTasks).Build();
        }

        RuntimeVendorNativeAccelerationSurface BuildRuntimeVendorNativeAccelerationSurface()
        {
            return new RuntimeVendorNativeAccelerationService(
                repoRoot,
                BuildRuntimeAgentWorkingModesSurface,
                BuildRuntimeFormalPlanningPostureSurface).Build();
        }
        var governanceReportingService = new GovernanceReportingService(repoRegistryService, repoRuntimeGateway, workerPermissionOrchestrationService, incidentTimelineService, providerHealthMonitorService, workerOperationalPolicyService);
        var delegationReportingService = new DelegationReportingService(runtimeNoiseAuditService, operatorOsEventStreamService, workerPermissionOrchestrationService, workerOperationalPolicyService);
        var workbenchSurfaceService = new WorkbenchSurfaceService(repoRoot, bootstrap.Paths, taskGraphService, planningDraftService, plannerService, dispatchProjectionService, devLoopService, executionRunService, infrastructure.ArtifactRepository, operatorApiService, operationalSummaryService, infrastructure.GitClient, bootstrap.SystemConfig.MaxParallelTasks, formalPlanningExecutionGateService, BuildRuntimeAgentWorkingModesSurface, BuildRuntimeFormalPlanningPostureSurface, BuildRuntimeVendorNativeAccelerationSurface);
        var worktreeResourceCleanupService = new WorktreeResourceCleanupService(
            repoRoot,
            bootstrap.Paths,
            bootstrap.SystemConfig,
            taskGraphService,
            sessionRepository,
            workerLeaseRepository,
            worktreeRuntimeRepository,
            infrastructure.GitClient);
        var packetEnforcementService = new PacketEnforcementService(
            bootstrap.Paths,
            taskGraphService,
            infrastructure.ArtifactRepository);
        var executionEnvelopeService = new ExecutionEnvelopeService(bootstrap.Paths);
        var resultIngestionService = new ResultIngestionService(
            bootstrap.Paths,
            taskGraphService,
            failureReportService,
            plannerTriggerService,
            markdownSyncService,
            () => devLoopService.GetSession(),
            infrastructure.ArtifactRepository,
            executionRunService: executionRunService,
            executionBoundaryService: new ExecutionBoundaryService(
                new ExecutionBudgetFactory(new ExecutionPathClassifier()),
                new ExecutionPathClassifier(),
                managedWorkspacePathPolicyService),
            runToReviewSubmissionService: new RunToReviewSubmissionService(
                bootstrap.Paths,
                infrastructure.GitClient));
        var dashboardService = new PlatformDashboardService(
            bootstrap.Paths,
            operatorApiService,
            operationalSummaryService,
            governanceReportingService,
            planningDraftService,
            dispatchProjectionService,
            taskGraphService,
            devLoopService,
            executionRunService,
            infrastructure.ArtifactRepository,
            bootstrap.SystemConfig.MaxParallelTasks,
            BuildRuntimeSessionGatewayGovernanceAssistSurface,
            BuildRuntimeAcceptanceContractIngressPolicySurface,
            BuildRuntimeAgentWorkingModesSurface,
            BuildRuntimeFormalPlanningPostureSurface,
            BuildRuntimeVendorNativeAccelerationSurface);
        var operatorSurfaceService = CreateOperatorSurfaceService(repoRoot, bootstrap, infrastructure, lockService, taskGraphService, plannerService, devLoopService, markdownSyncService, codeGraphBuilder, codeGraphQueryService, refactoringService, safetyService, reviewArtifactFactory, reviewWritebackService, reviewEvidenceGateService, runtimeFailurePolicy, failureReportService, failureContextService, failureSummaryService, plannerTriggerService, dispatchProjectionService, executionRunService, resultIngestionService, opportunityDetectorService, hostRegistryService, repoRegistryService, repoRuntimeService, runtimeInstanceManager, providerRegistryService, providerRoutingService, platformGovernanceService, platformSchedulerService, actorSessionService, sessionOwnershipService, concurrentActorArbitrationService, operatorOsEventStreamService, platformVocabularyLintService, operatorApiService, dashboardService, runtimeConsistencyCheckService, delegatedWorkerLifecycleReconciliationService, operationalSummaryService, governanceReportingService, delegationReportingService, worktreeResourceCleanupService, workerBroker, interactionLayerService, workerExecutionBoundaryService, workerSelectionPolicyService, approvalPolicyEngine, workerPermissionOrchestrationService, infrastructure.WorkerExecutionAuditReadModel, runtimePolicyBundleService, planningDraftService, managedWorkspaceLeaseService, contextPackService, currentModelQualificationService, routingValidationService, routingPromotionDecisionService, validationCoverageMatrixService, routingCandidateReadinessService, specificationValidationService, executionPacketCompilerService);
        return BuildRuntimeServices(
            bootstrap,
            infrastructure,
            taskGraphService,
            plannerService,
            codeGraphQueryService,
            interactionLayerService,
            intentDiscoveryService,
            planningDraftService,
            formalPlanningPacketService,
            managedWorkspaceLeaseService,
            conversationProtocolService,
            promptProtocolService,
            promptKernelService,
            projectUnderstandingProjectionService,
            approvalPolicyEngine,
            workerPermissionOrchestrationService,
            workerSelectionPolicyService,
            workerRequestFactory,
            workerService,
            safetyService,
            devLoopService,
            markdownSyncService,
            failureReportService,
            failureContextService,
            failureSummaryService,
            plannerTriggerService,
            dispatchProjectionService,
            executionRunService,
            resultIngestionService,
            codeGraphBuilder,
            refactoringService,
            operatorSurfaceService,
            hostRegistryService,
            repoRegistryService,
            repoRuntimeService,
            runtimeInstanceManager,
            providerRegistryService,
            providerRoutingService,
            runtimeRoutingProfileService,
            currentModelQualificationService,
            platformGovernanceService,
            platformSchedulerService,
            actorSessionService,
            runtimeSessionGatewayService,
            sessionOwnershipService,
            concurrentActorArbitrationService,
            operatorOsEventStreamService,
            platformVocabularyLintService,
            operatorApiService,
            dashboardService,
            workbenchSurfaceService,
            workerBroker,
            workerOperationalPolicyService,
            runtimePolicyBundleService,
            providerHealthMonitorService,
            runtimeConsistencyCheckService,
            delegatedWorkerLifecycleReconciliationService,
            operationalSummaryService,
            governanceReportingService,
            delegationReportingService,
            worktreeResourceCleanupService);
    }
}
