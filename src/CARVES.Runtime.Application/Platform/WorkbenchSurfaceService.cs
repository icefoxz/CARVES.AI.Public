using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using TaskGraphModel = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Platform;

public sealed partial class WorkbenchSurfaceService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly PlanningDraftService planningDraftService;
    private readonly PlannerService plannerService;
    private readonly DispatchProjectionService dispatchProjectionService;
    private readonly DevLoopService devLoopService;
    private readonly ExecutionRunService executionRunService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly OperatorApiService operatorApiService;
    private readonly OperationalSummaryService operationalSummaryService;
    private readonly ReviewEvidenceProjectionService reviewEvidenceProjectionService;
    private readonly FormalPlanningExecutionGateService formalPlanningExecutionGateService;
    private readonly Func<RuntimeAgentWorkingModesSurface> runtimeAgentWorkingModesFactory;
    private readonly Func<RuntimeFormalPlanningPostureSurface> runtimeFormalPlanningPostureFactory;
    private readonly Func<RuntimeVendorNativeAccelerationSurface> runtimeVendorNativeAccelerationFactory;
    private readonly int maxParallelTasks;

    public WorkbenchSurfaceService(
        string repoRoot,
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        PlanningDraftService planningDraftService,
        PlannerService plannerService,
        DispatchProjectionService dispatchProjectionService,
        DevLoopService devLoopService,
        ExecutionRunService executionRunService,
        IRuntimeArtifactRepository artifactRepository,
        OperatorApiService operatorApiService,
        OperationalSummaryService operationalSummaryService,
        IGitClient gitClient,
        int maxParallelTasks,
        FormalPlanningExecutionGateService? formalPlanningExecutionGateService = null,
        Func<RuntimeAgentWorkingModesSurface>? runtimeAgentWorkingModesFactory = null,
        Func<RuntimeFormalPlanningPostureSurface>? runtimeFormalPlanningPostureFactory = null,
        Func<RuntimeVendorNativeAccelerationSurface>? runtimeVendorNativeAccelerationFactory = null)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.planningDraftService = planningDraftService;
        this.plannerService = plannerService;
        this.dispatchProjectionService = dispatchProjectionService;
        this.devLoopService = devLoopService;
        this.executionRunService = executionRunService;
        this.artifactRepository = artifactRepository;
        this.operatorApiService = operatorApiService;
        this.operationalSummaryService = operationalSummaryService;
        reviewEvidenceProjectionService = new ReviewEvidenceProjectionService(repoRoot, gitClient);
        this.formalPlanningExecutionGateService = formalPlanningExecutionGateService ?? new FormalPlanningExecutionGateService();
        this.runtimeAgentWorkingModesFactory = runtimeAgentWorkingModesFactory ?? (() => new RuntimeAgentWorkingModesSurface());
        this.runtimeFormalPlanningPostureFactory = runtimeFormalPlanningPostureFactory ?? (() => new RuntimeFormalPlanningPostureSurface());
        this.runtimeVendorNativeAccelerationFactory = runtimeVendorNativeAccelerationFactory ?? (() => new RuntimeVendorNativeAccelerationSurface());
        this.maxParallelTasks = Math.Max(1, maxParallelTasks);
    }

}
