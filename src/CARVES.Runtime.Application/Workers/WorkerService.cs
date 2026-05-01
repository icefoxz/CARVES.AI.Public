using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Processes;
using Carves.Runtime.Application.Safety;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerService
{
    private readonly SystemConfig systemConfig;
    private readonly SafetyRules safetyRules;
    private readonly ModuleDependencyMap moduleDependencyMap;
    private readonly SafetyService safetyService;
    private readonly IWorktreeManager worktreeManager;
    private readonly WorkerValidationRunner validationRunner;
    private readonly SafetyTaskClassifier safetyTaskClassifier;
    private readonly WorkerExecutionPipeline executionPipeline;
    private readonly WorkerExecutionLifecycleRecorder lifecycleRecorder;
    private readonly WorkerExecutionArtifactRecorder artifactRecorder;

    public WorkerService(
        SystemConfig systemConfig,
        SafetyRules safetyRules,
        ModuleDependencyMap moduleDependencyMap,
        WorkerAdapterRegistry workerAdapterRegistry,
        IProcessRunner processRunner,
        IWorktreeManager worktreeManager,
        SafetyService safetyService,
        IRuntimeArtifactRepository artifactRepository,
        WorkerExecutionBoundaryService boundaryService,
        WorkerPermissionOrchestrationService permissionOrchestrationService,
        RuntimeIncidentTimelineService incidentTimelineService,
        ActorSessionService actorSessionService,
        OperatorOsEventStreamService operatorOsEventStreamService,
        ExecutionEvidenceRecorder executionEvidenceRecorder,
        IWorkerExecutionAuditReadModel? workerExecutionAuditReadModel = null)
    {
        this.systemConfig = systemConfig;
        this.safetyRules = safetyRules;
        this.moduleDependencyMap = moduleDependencyMap;
        this.worktreeManager = worktreeManager;
        this.safetyService = safetyService;
        validationRunner = new WorkerValidationRunner(processRunner);
        safetyTaskClassifier = new SafetyTaskClassifier();
        var failureClassifier = new WorkerFailureClassifier();
        executionPipeline = new WorkerExecutionPipeline(
            workerAdapterRegistry,
            boundaryService,
            permissionOrchestrationService,
            failureClassifier);
        lifecycleRecorder = new WorkerExecutionLifecycleRecorder(
            incidentTimelineService,
            actorSessionService,
            operatorOsEventStreamService,
            workerExecutionAuditReadModel);
        artifactRecorder = new WorkerExecutionArtifactRecorder(
            artifactRepository,
            executionEvidenceRecorder);
    }

    public TaskRunReport Execute(WorkerRequest request)
    {
        var lifecycle = lifecycleRecorder.Start(request);
        var validationMode = safetyTaskClassifier.Classify(request.Task);
        var evidence = new List<string>
        {
            $"memory architecture docs: {request.Memory.Architecture.Count}",
            $"memory module docs: {request.Memory.Modules.Count}",
            $"worker adapter: {request.Session.WorkerAdapterName}",
            $"worktree: {request.Session.WorktreeRoot}",
            $"task type: {request.Task.TaskType}",
            $"safety validation mode: {validationMode}",
        };

        var workerExecution = executionPipeline.Execute(request, evidence);
        var validationOutcome = validationRunner.Execute(request, validationMode, evidence, workerExecution);
        workerExecution = executionPipeline.NormalizeAfterValidation(workerExecution, validationOutcome);
        var workerProjection = PromptSafeArtifactProjectionFactory.ForWorkerExecution(request.Task.TaskId, workerExecution);
        workerExecution = workerExecution with
        {
            Summary = workerProjection.Summary,
        };
        AddExpectedEvidenceReceipts(request.Task, workerExecution, evidence);
        var preliminaryReport = WorkerReportFactory.CreatePreliminary(request, systemConfig, workerExecution, validationOutcome, evidence);

        evidence.Add(SafetyLayerSemantics.FormatEvidence(SafetyLayerSemantics.PostExecutionSafetyLayerId));
        var safetyDecision = safetyService.Evaluate(new SafetyContext(request.Task, preliminaryReport, validationMode, safetyRules, moduleDependencyMap));
        var finalReport = WorkerReportFactory.CreateFinal(preliminaryReport, safetyDecision);
        artifactRecorder.Record(finalReport, workerProjection);

        lifecycleRecorder.Complete(lifecycle, request, finalReport);

        if (ShouldCleanupWorktree(request, finalReport))
        {
            worktreeManager.CleanupWorktree(request.Session.WorktreeRoot);
        }

        return finalReport;
    }

    private static void AddExpectedEvidenceReceipts(TaskNode task, WorkerExecutionResult workerExecution, ICollection<string> evidence)
    {
        if (!workerExecution.Succeeded)
        {
            return;
        }

        foreach (var expected in task.Validation.ExpectedEvidence)
        {
            if (string.Equals(expected, "worker artifact exists", StringComparison.OrdinalIgnoreCase))
            {
                evidence.Add("worker artifact exists");
            }
        }
    }

    private bool ShouldCleanupWorktree(WorkerRequest request, TaskRunReport report)
    {
        if (request.Session.DryRun)
        {
            return true;
        }

        if (!(systemConfig.RemoveWorktreeOnSuccess && report.Validation.Passed && report.SafetyDecision.Allowed))
        {
            return false;
        }

        return report.Patch.Paths.Count == 0;
    }
}
