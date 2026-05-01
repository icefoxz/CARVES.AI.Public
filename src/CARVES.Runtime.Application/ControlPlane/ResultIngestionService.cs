using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Domain.Safety;
using System.Globalization;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ResultIngestionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly FailureReportService failureReportService;
    private readonly PlannerTriggerService plannerTriggerService;
    private readonly IMarkdownSyncService markdownSyncService;
    private readonly Func<Carves.Runtime.Domain.Runtime.RuntimeSessionState?> sessionAccessor;
    private readonly ExecutionBoundaryService executionBoundaryService;
    private readonly ExecutionBoundaryArtifactService executionBoundaryArtifactService;
    private readonly ExecutionRunService executionRunService;
    private readonly ExecutionRunReportService executionRunReportService;
    private readonly ExecutionPatternService executionPatternService;
    private readonly ExecutionPatternGuardService executionPatternGuardService;
    private readonly PlannerEmergenceService plannerEmergenceService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly ResultValidityPolicy resultValidityPolicy;
    private readonly BoundaryDecisionService boundaryDecisionService;
    private readonly PacketEnforcementService packetEnforcementService;
    private readonly RunToReviewSubmissionService runToReviewSubmissionService;
    private readonly IStateTransitionCertificateService stateTransitionCertificateService;
    private readonly ResourceLeaseService resourceLeaseService;
    private readonly GovernedTruthTransitionProfileService governedTruthTransitionProfileService;

    public ResultIngestionService(
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        FailureReportService failureReportService,
        PlannerTriggerService plannerTriggerService,
        IMarkdownSyncService markdownSyncService,
        Func<Carves.Runtime.Domain.Runtime.RuntimeSessionState?> sessionAccessor,
        IRuntimeArtifactRepository artifactRepository,
        ExecutionRunService? executionRunService = null,
        ExecutionBoundaryService? executionBoundaryService = null,
        ExecutionBoundaryArtifactService? executionBoundaryArtifactService = null,
        ExecutionRunReportService? executionRunReportService = null,
        ExecutionPatternService? executionPatternService = null,
        ExecutionPatternGuardService? executionPatternGuardService = null,
        PlannerEmergenceService? plannerEmergenceService = null,
        ResultValidityPolicy? resultValidityPolicy = null,
        BoundaryDecisionService? boundaryDecisionService = null,
        PacketEnforcementService? packetEnforcementService = null,
        RunToReviewSubmissionService? runToReviewSubmissionService = null,
        IStateTransitionCertificateService? stateTransitionCertificateService = null,
        ResourceLeaseService? resourceLeaseService = null)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.failureReportService = failureReportService;
        this.plannerTriggerService = plannerTriggerService;
        this.markdownSyncService = markdownSyncService;
        this.sessionAccessor = sessionAccessor;
        this.artifactRepository = artifactRepository;
        this.executionRunService = executionRunService ?? new ExecutionRunService(paths);
        this.executionBoundaryService = executionBoundaryService ?? new ExecutionBoundaryService(
            new ExecutionBudgetFactory(new ExecutionPathClassifier()),
            new ExecutionPathClassifier());
        this.executionBoundaryArtifactService = executionBoundaryArtifactService ?? new ExecutionBoundaryArtifactService(paths.AiRoot);
        this.executionRunReportService = executionRunReportService ?? new ExecutionRunReportService(paths);
        this.executionPatternService = executionPatternService ?? new ExecutionPatternService();
        this.executionPatternGuardService = executionPatternGuardService ?? new ExecutionPatternGuardService();
        this.plannerEmergenceService = plannerEmergenceService ?? new PlannerEmergenceService(paths, taskGraphService, this.executionRunService);
        this.resultValidityPolicy = resultValidityPolicy ?? new ResultValidityPolicy(paths);
        this.boundaryDecisionService = boundaryDecisionService ?? new BoundaryDecisionService();
        this.packetEnforcementService = packetEnforcementService ?? new PacketEnforcementService(paths, taskGraphService, artifactRepository);
        this.runToReviewSubmissionService = runToReviewSubmissionService ?? new RunToReviewSubmissionService(paths);
        this.stateTransitionCertificateService = stateTransitionCertificateService ?? new StateTransitionCertificateService(paths);
        this.resourceLeaseService = resourceLeaseService ?? new ResourceLeaseService(paths);
        this.governedTruthTransitionProfileService = new GovernedTruthTransitionProfileService();
    }

}

public sealed record ResultIngestionOutcome(
    string TaskId,
    string ResultStatus,
    DomainTaskStatus TaskStatus,
    bool AlreadyApplied,
    string? FailureId,
    bool BoundaryStopped = false,
    string? BoundaryReason = null,
    string? ReviewSubmissionPath = null,
    string? EffectLedgerPath = null,
    string? ResultCommit = null);
