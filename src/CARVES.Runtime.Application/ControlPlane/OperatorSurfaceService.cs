using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.Safety;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Domain.Platform;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorSurfaceService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        AiProviderConfig aiProviderConfig,
        IAiClient aiClient,
        IGitClient gitClient,
        IControlPlaneLockService controlPlaneLockService,
        IControlPlaneConfigRepository configRepository,
        TaskGraphService taskGraphService,
        PlannerService plannerService,
        DevLoopService devLoopService,
        IMarkdownSyncService markdownSyncService,
        ICodeGraphBuilder codeGraphBuilder,
        ICodeGraphQueryService codeGraphQueryService,
        IRefactoringService refactoringService,
        SafetyService safetyService,
        IRuntimeArtifactRepository artifactRepository,
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
        Carves.Runtime.Application.Workers.WorkerBroker workerBroker,
        InteractionLayerService interactionLayerService,
        Carves.Runtime.Application.Workers.WorkerExecutionBoundaryService workerExecutionBoundaryService,
        Carves.Runtime.Application.Workers.WorkerSelectionPolicyService workerSelectionPolicyService,
        Carves.Runtime.Application.Workers.ApprovalPolicyEngine approvalPolicyEngine,
        Carves.Runtime.Application.Workers.WorkerPermissionOrchestrationService workerPermissionOrchestrationService,
        Carves.Runtime.Application.Workers.IWorkerExecutionAuditReadModel? workerExecutionAuditReadModel,
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
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.aiProviderConfig = aiProviderConfig;
        this.aiClient = aiClient;
        this.gitClient = gitClient;
        this.controlPlaneLockService = controlPlaneLockService;
        this.configRepository = configRepository;
        this.taskGraphService = taskGraphService;
        this.plannerService = plannerService;
        this.devLoopService = devLoopService;
        this.markdownSyncService = markdownSyncService;
        this.codeGraphBuilder = codeGraphBuilder;
        this.codeGraphQueryService = codeGraphQueryService;
        this.refactoringService = refactoringService;
        this.safetyService = safetyService;
        this.artifactRepository = artifactRepository;
        this.reviewArtifactFactory = reviewArtifactFactory;
        this.reviewWritebackService = reviewWritebackService;
        this.reviewEvidenceGateService = reviewEvidenceGateService;
        this.runtimeFailurePolicy = runtimeFailurePolicy;
        this.failureReportService = failureReportService;
        this.failureContextService = failureContextService;
        this.failureSummaryService = failureSummaryService;
        this.plannerTriggerService = plannerTriggerService;
        this.dispatchProjectionService = dispatchProjectionService;
        this.executionRunService = executionRunService;
        this.resultIngestionService = resultIngestionService;
        this.opportunityDetectorService = opportunityDetectorService;
        this.hostRegistryService = hostRegistryService;
        this.repoRegistryService = repoRegistryService;
        this.repoRuntimeService = repoRuntimeService;
        this.runtimeInstanceManager = runtimeInstanceManager;
        this.providerRegistryService = providerRegistryService;
        this.providerRoutingService = providerRoutingService;
        this.platformGovernanceService = platformGovernanceService;
        this.platformSchedulerService = platformSchedulerService;
        this.actorSessionService = actorSessionService;
        this.sessionOwnershipService = sessionOwnershipService;
        this.concurrentActorArbitrationService = concurrentActorArbitrationService;
        this.operatorOsEventStreamService = operatorOsEventStreamService;
        this.platformVocabularyLintService = platformVocabularyLintService;
        this.operatorApiService = operatorApiService;
        this.platformDashboardService = platformDashboardService;
        this.runtimeConsistencyCheckService = runtimeConsistencyCheckService;
        this.delegatedWorkerLifecycleReconciliationService = delegatedWorkerLifecycleReconciliationService;
        this.operationalSummaryService = operationalSummaryService;
        this.governanceReportingService = governanceReportingService;
        this.delegationReportingService = delegationReportingService;
        this.worktreeResourceCleanupService = worktreeResourceCleanupService;
        this.workerBroker = workerBroker;
        this.interactionLayerService = interactionLayerService;
        this.workerExecutionBoundaryService = workerExecutionBoundaryService;
        this.workerSelectionPolicyService = workerSelectionPolicyService;
        this.approvalPolicyEngine = approvalPolicyEngine;
        this.workerPermissionOrchestrationService = workerPermissionOrchestrationService;
        this.workerExecutionAuditReadModel = workerExecutionAuditReadModel;
        this.runtimePolicyBundleService = runtimePolicyBundleService;
        this.planningDraftService = planningDraftService;
        this.managedWorkspaceLeaseService = managedWorkspaceLeaseService;
        this.contextPackService = contextPackService;
        this.currentModelQualificationService = currentModelQualificationService;
        this.routingValidationService = routingValidationService;
        this.routingPromotionDecisionService = routingPromotionDecisionService;
        this.validationCoverageMatrixService = validationCoverageMatrixService;
        this.routingCandidateReadinessService = routingCandidateReadinessService;
        this.specificationValidationService = specificationValidationService;
        this.executionPacketCompilerService = executionPacketCompilerService;
    }

}
