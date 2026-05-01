using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
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
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

public static partial class RuntimeComposition
{
    private static RuntimeBootstrapContext LoadBootstrapContext(string repoRoot) => RuntimeCompositionFactories.LoadBootstrapContext(repoRoot);

    private static RuntimeInfrastructureServices CreateInfrastructure(RuntimeBootstrapContext bootstrap) => RuntimeCompositionFactories.CreateInfrastructure(bootstrap);

    private static PlannerService CreatePlannerService(
        TaskGraphService taskGraphService,
        ICodeGraphBuilder codeGraphBuilder,
        ICodeGraphQueryService codeGraphQueryService,
        Carves.Runtime.Application.Git.IGitClient gitClient,
        TaskDecomposer taskDecomposer) =>
        RuntimeCompositionFactories.CreatePlannerService(taskGraphService, codeGraphBuilder, codeGraphQueryService, gitClient, taskDecomposer);

    private static WorkerRequestFactory CreateWorkerRequestFactory(
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
        FormalPlanningExecutionGateService formalPlanningExecutionGateService) =>
        RuntimeCompositionFactories.CreateWorkerRequestFactory(
            repoRoot,
            bootstrap,
            infrastructure,
            memoryService,
            contextPackService,
            workerExecutionBoundaryService,
            workerSelectionPolicyService,
            worktreeRuntimeService,
            incidentTimelineService,
            executionPacketCompilerService,
            formalPlanningExecutionGateService);

    private static WorkerService CreateWorkerService(
        RuntimeBootstrapContext bootstrap,
        RuntimeInfrastructureServices infrastructure,
        SafetyService safetyService,
        WorkerExecutionBoundaryService workerExecutionBoundaryService,
        WorkerPermissionOrchestrationService workerPermissionOrchestrationService,
        RuntimeIncidentTimelineService incidentTimelineService,
        ActorSessionService actorSessionService,
        OperatorOsEventStreamService operatorOsEventStreamService) =>
        RuntimeCompositionFactories.CreateWorkerService(
            bootstrap,
            infrastructure,
            safetyService,
            workerExecutionBoundaryService,
            workerPermissionOrchestrationService,
            incidentTimelineService,
            actorSessionService,
            operatorOsEventStreamService);

    private static DevLoopService CreateDevLoopService(
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
        FormalPlanningExecutionGateService formalPlanningExecutionGateService) =>
        RuntimeCompositionFactories.CreateDevLoopService(
            repoRoot,
            taskGraphService,
            workerRequestFactory,
            workerService,
            workerBroker,
            plannerHostService,
            plannerWakeBridgeService,
            sessionRepository,
            markdownSyncService,
            artifactRepository,
            failureReportService,
            reviewArtifactFactory,
            runtimeFailurePolicy,
            recoveryPolicyEngine,
            worktreeRuntimeService,
            incidentTimelineService,
            actorSessionService,
            formalPlanningExecutionGateService);

    private static IRefactoringService CreateRefactoringService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        Carves.Runtime.Application.Git.IGitClient gitClient,
        TaskGraphService taskGraphService) =>
        RuntimeCompositionFactories.CreateRefactoringService(repoRoot, paths, systemConfig, gitClient, taskGraphService);

    private static OperatorSurfaceService CreateOperatorSurfaceService(
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
        ExecutionPacketCompilerService executionPacketCompilerService) =>
        RuntimeCompositionFactories.CreateOperatorSurfaceService(
            repoRoot,
            bootstrap,
            infrastructure,
            controlPlaneLockService,
            taskGraphService,
            plannerService,
            devLoopService,
            markdownSyncService,
            codeGraphBuilder,
            codeGraphQueryService,
            refactoringService,
            safetyService,
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
