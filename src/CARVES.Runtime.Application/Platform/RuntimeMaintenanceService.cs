using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeMaintenanceService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;
    private readonly TaskGraphService taskGraphService;
    private readonly RuntimeManifestService manifestService;
    private readonly RuntimeHealthCheckService healthCheckService;
    private readonly ICodeGraphBuilder codeGraphBuilder;
    private readonly ICodeGraphQueryService codeGraphQueryService;

    public RuntimeMaintenanceService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        TaskGraphService taskGraphService,
        RuntimeManifestService manifestService,
        RuntimeHealthCheckService healthCheckService,
        ICodeGraphBuilder codeGraphBuilder,
        ICodeGraphQueryService codeGraphQueryService)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.taskGraphService = taskGraphService;
        this.manifestService = manifestService;
        this.healthCheckService = healthCheckService;
        this.codeGraphBuilder = codeGraphBuilder;
        this.codeGraphQueryService = codeGraphQueryService;
    }

    public RuntimeMaintenanceResult Repair()
    {
        EnsureRuntimeScaffold();
        var repairedTasks = NormalizeInterruptedTasks();
        var health = healthCheckService.Evaluate();
        var sustainabilityAudit = CreateSustainabilityAuditService().Audit();
        UpdateManifestAfterMaintenance(health, "repair completed");
        return new RuntimeMaintenanceResult("repair", health, repairedTasks, SustainabilityAudit: sustainabilityAudit);
    }

    public RuntimeMaintenanceResult Rebuild()
    {
        EnsureRuntimeScaffold();
        DeleteDerivedState();
        EnsureRuntimeScaffold();
        var repairedTasks = NormalizeInterruptedTasks();
        var build = codeGraphBuilder.Build();
        var audit = new CodeGraphAuditService(repoRoot, paths, systemConfig, codeGraphQueryService).Audit();
        var health = ApplyCodeGraphAudit(healthCheckService.Evaluate(), audit);
        var sustainabilityAudit = CreateSustainabilityAuditService().Audit();
        UpdateManifestAfterMaintenance(health, audit.StrictPassed ? "rebuild completed" : "rebuild completed with codegraph audit findings");
        return new RuntimeMaintenanceResult(
            "rebuild",
            health,
            repairedTasks,
            build.OutputPath,
            build.IndexPath,
            audit,
            sustainabilityAudit);
    }

    public RuntimeMaintenanceResult ResetDerived()
    {
        DeleteDerivedState();
        EnsureRuntimeScaffold();
        var health = healthCheckService.Evaluate();
        var sustainabilityAudit = CreateSustainabilityAuditService().Audit();
        UpdateManifestAfterMaintenance(health, "derived state reset");
        return new RuntimeMaintenanceResult("reset-derived", health, Array.Empty<string>(), SustainabilityAudit: sustainabilityAudit);
    }

    public RuntimeMaintenanceResult CompactHistory()
    {
        EnsureRuntimeScaffold();
        var compaction = CreateOperationalHistoryCompactionService().Compact();
        var health = healthCheckService.Evaluate();
        var sustainabilityAudit = CreateSustainabilityAuditService().Audit();
        UpdateManifestAfterMaintenance(health, "history compaction completed");
        return new RuntimeMaintenanceResult(
            "compact-history",
            health,
            Array.Empty<string>(),
            SustainabilityAudit: sustainabilityAudit,
            HistoryCompaction: compaction);
    }

    private void EnsureRuntimeScaffold()
    {
        Directory.CreateDirectory(paths.AiRoot);
        Directory.CreateDirectory(Path.Combine(paths.AiRoot, "memory"));
        Directory.CreateDirectory(paths.TasksRoot);
        Directory.CreateDirectory(paths.CardsRoot);
        Directory.CreateDirectory(paths.TaskNodesRoot);
        Directory.CreateDirectory(Path.Combine(paths.AiRoot, "codegraph"));
        Directory.CreateDirectory(Path.Combine(paths.AiRoot, "patches"));
        Directory.CreateDirectory(Path.Combine(paths.AiRoot, "reviews"));
        Directory.CreateDirectory(paths.ArtifactsRoot);
        Directory.CreateDirectory(paths.RuntimeRoot);
        Directory.CreateDirectory(RuntimeArtifactCatalogService.GetSustainabilityRoot(paths));
    }

    private string[] NormalizeInterruptedTasks()
    {
        var graph = taskGraphService.Load();
        var repaired = new List<string>();
        foreach (var task in graph.ListTasks().Where(task => task.Status is Carves.Runtime.Domain.Tasks.TaskStatus.Running or Carves.Runtime.Domain.Tasks.TaskStatus.Testing))
        {
            var repairedTask = new TaskNode
            {
                TaskId = task.TaskId,
                Title = task.Title,
                Description = task.Description,
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Blocked,
                TaskType = task.TaskType,
                Priority = task.Priority,
                Source = task.Source,
                CardId = task.CardId,
                ProposalSource = task.ProposalSource,
                ProposalReason = task.ProposalReason,
                ProposalConfidence = task.ProposalConfidence,
                ProposalPriorityHint = task.ProposalPriorityHint,
                BaseCommit = task.BaseCommit,
                ResultCommit = task.ResultCommit,
                Dependencies = task.Dependencies,
                Scope = task.Scope,
                Acceptance = task.Acceptance,
                Constraints = task.Constraints,
                AcceptanceContract = task.AcceptanceContract,
                Validation = task.Validation,
                RetryCount = task.RetryCount,
                Capabilities = task.Capabilities,
                Metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
                {
                    ["runtime_maintenance_interrupted"] = "true",
                    ["runtime_maintenance_action"] = "blocked_during_repair",
                },
                LastWorkerRunId = task.LastWorkerRunId,
                LastWorkerBackend = task.LastWorkerBackend,
                LastWorkerFailureKind = task.LastWorkerFailureKind,
                LastWorkerRetryable = task.LastWorkerRetryable,
                LastWorkerSummary = task.LastWorkerSummary,
                LastWorkerDetailRef = task.LastWorkerDetailRef,
                LastProviderDetailRef = task.LastProviderDetailRef,
                LastRecoveryAction = task.LastRecoveryAction,
                LastRecoveryReason = task.LastRecoveryReason,
                RetryNotBefore = task.RetryNotBefore,
                PlannerReview = new PlannerReview
                {
                    Verdict = PlannerVerdict.Blocked,
                    Reason = "Runtime repair blocked an interrupted execution so it can re-enter governed planning.",
                    AcceptanceMet = false,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = false,
                    FollowUpSuggestions = task.PlannerReview.FollowUpSuggestions,
                },
                CreatedAt = task.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            taskGraphService.ReplaceTask(repairedTask);
            repaired.Add(task.TaskId);
        }

        return repaired.OrderBy(taskId => taskId, StringComparer.Ordinal).ToArray();
    }

    private void DeleteDerivedState()
    {
        SafeDeleteDirectory(Path.Combine(paths.AiRoot, "codegraph"));
        SafeDeleteDirectory(Path.Combine(paths.AiRoot, "patches"));
        SafeDeleteDirectory(paths.ArtifactsRoot);
        SafeDeleteFile(paths.OpportunitiesFile);
        SafeDeleteFile(Path.Combine(paths.RuntimeRoot, "summary.json"));
    }

    private void UpdateManifestAfterMaintenance(RepoRuntimeHealthCheckResult health, string summary)
    {
        var manifest = manifestService.Load();
        if (manifest is null)
        {
            return;
        }

        var state = health.State switch
        {
            RepoRuntimeHealthState.Healthy => RepoRuntimeManifestState.Healthy,
            RepoRuntimeHealthState.Dirty => RepoRuntimeManifestState.Dirty,
            _ => RepoRuntimeManifestState.Repairing,
        };
        manifestService.MarkRepair(manifest, state, runtimeStatus: health.State.ToString().ToLowerInvariant(), summary);
    }

    private static RepoRuntimeHealthCheckResult ApplyCodeGraphAudit(RepoRuntimeHealthCheckResult health, CodeGraphAuditReport audit)
    {
        if (audit.StrictPassed)
        {
            return health;
        }

        var issues = health.Issues
            .Concat(
                audit.Findings
                    .Take(10)
                    .Select(finding => new RepoRuntimeHealthIssue(
                        "codegraph_audit_failed",
                        $"CodeGraph audit flagged {finding.Category} at '{finding.Path}'.",
                        "error",
                        finding.Path)))
            .ToArray();

        return new RepoRuntimeHealthCheckResult
        {
            State = health.State == RepoRuntimeHealthState.Broken ? RepoRuntimeHealthState.Broken : RepoRuntimeHealthState.Dirty,
            Issues = issues,
            MissingDirectories = health.MissingDirectories,
            InterruptedTaskIds = health.InterruptedTaskIds,
            DanglingArtifactPaths = health.DanglingArtifactPaths,
            Summary = "Runtime rebuild completed, but codegraph audit found source-of-truth leakage or non-source entries.",
            SuggestedAction = "audit codegraph --strict",
        };
    }

    private static void SafeDeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void SafeDeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private SustainabilityAuditService CreateSustainabilityAuditService()
    {
        return new SustainabilityAuditService(repoRoot, paths, systemConfig, codeGraphQueryService);
    }

    private OperationalHistoryCompactionService CreateOperationalHistoryCompactionService()
    {
        return new OperationalHistoryCompactionService(repoRoot, paths, systemConfig);
    }
}

public sealed record RuntimeMaintenanceResult(
    string Operation,
    RepoRuntimeHealthCheckResult Health,
    IReadOnlyList<string> RepairedTaskIds,
    string? CodeGraphOutputPath = null,
    string? CodeGraphIndexPath = null,
    CodeGraphAuditReport? CodeGraphAudit = null,
    SustainabilityAuditReport? SustainabilityAudit = null,
    OperationalHistoryCompactionReport? HistoryCompaction = null);
