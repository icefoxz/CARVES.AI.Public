using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeExecutionKernelService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;

    public RuntimeExecutionKernelService(string repoRoot, ControlPlanePaths paths, SystemConfig systemConfig)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.systemConfig = systemConfig;
    }

    public RuntimeKernelBoundarySurface Build()
    {
        return new RuntimeKernelBoundarySurface
        {
            SurfaceId = "runtime-execution-kernel",
            KernelId = "execution",
            Summary = "Execution Kernel freezes implementation actors, verifier/review actors, maintenance actors, and workspace lifecycle as one governed execution truth spine.",
            TruthRoots =
            [
                Root(
                    "execution_task_truth",
                    "canonical_truth",
                    "Task graph, task nodes, and review writeback remain the canonical execution truth spine for implementation, review, and completion transitions.",
                    [
                        ToRepoRelative(paths.TaskGraphFile),
                        ToRepoRelative(paths.TaskNodesRoot),
                        ToRepoRelative(paths.CardsRoot),
                    ],
                    [
                        "TaskGraphService",
                        "ReviewWritebackService",
                        "task inspect <task-id> --runs",
                    ]),
                Root(
                    "execution_run_history",
                    "operational_history",
                    "Execution runs and run reports remain the replayable history layer over the same task truth spine.",
                    [
                        ".ai/runtime/runs/",
                        ".ai/runtime/run-reports/",
                    ],
                    [
                        "ExecutionRunService",
                        "ExecutionRunReportService",
                        "inspect run <run-id>",
                    ]),
                Root(
                    "actor_runtime_live_state",
                    "live_state",
                    "Actor sessions, worker leases, delegated run lifecycle, and runtime session posture stay in live-state roots instead of growing a second execution store.",
                    [
                        ToRepoRelative(paths.PlatformSessionLiveStateRoot),
                        ToRepoRelative(paths.PlatformWorkerLiveStateRoot),
                        ToRepoRelative(paths.PlatformDelegationLiveStateRoot),
                        ToRepoRelative(paths.RuntimeSessionFile),
                    ],
                    [
                        "ActorSessionService",
                        "ConcurrentActorArbitrationService",
                        "WorkerBroker",
                        "api actor-sessions",
                        "api worker-leases",
                    ]),
                Root(
                    "workspace_lifecycle_truth",
                    "live_state",
                    "Workspace lifecycle remains governed by runtime worktree state, runtime instance state, and cleanup policy rather than ad-hoc filesystem mutation.",
                    [
                        ToRepoRelative(paths.RuntimeWorktreeStateFile),
                        ToRepoRelative(paths.PlatformRuntimeInstancesLiveStateFile),
                        ToRepoRelative(Path.GetFullPath(Path.Combine(repoRoot, systemConfig.WorktreeRoot))),
                    ],
                    [
                        "RepoRuntimeService",
                        "RuntimeInstanceManager",
                        "WorktreeResourceCleanupService",
                        "runtime inspect <repo-id>",
                    ],
                    [
                        "The worktree root may resolve outside the repo, but it remains part of the governed execution boundary.",
                    ]),
            ],
            BoundaryRules =
            [
                Rule(
                    "one_execution_truth_spine",
                    "Implementation actors, verifier/review actors, and maintenance actors must all reconcile through the existing task truth and execution-run lineage.",
                    [
                        "TaskGraphService",
                        "ExecutionRunService",
                        "ReviewWritebackService",
                        "RuntimeMaintenanceService",
                    ],
                    [
                        "parallel_execution_pipeline",
                        "actor_specific_task_store",
                    ]),
                Rule(
                    "actor_state_stays_runtime_local",
                    "Actor ownership, lease, and arbitration state stays in runtime live-state roots instead of leaking into memory or card/task truth.",
                    [
                        "ActorSessionService",
                        "SessionOwnershipService",
                        "ConcurrentActorArbitrationService",
                    ],
                    [
                        "actor_state_in_memory_truth",
                        "worker_lease_state_in_task_metadata",
                    ]),
                Rule(
                    "workspace_cleanup_is_maintenance_not_writeback",
                    "Worktree cleanup, rebuild, and compaction remain maintenance actions over existing truth roots and must not be used as alternate completion or review writeback paths.",
                    [
                        "WorktreeResourceCleanupService",
                        "RuntimeMaintenanceService",
                        "compact-history",
                        "cleanup",
                    ],
                    [
                        "delete_artifacts_to_imply_success",
                        "workspace_cleanup_as_review_outcome",
                    ]),
            ],
            GovernedReadPaths =
            [
                ReadPath(
                    "task_execution_lifecycle",
                    "task inspect <task-id> --runs",
                    "Task inspection remains the governed read path for seeing task truth together with its execution-run lineage.",
                    [
                        "TaskGraphService",
                        "ExecutionRunService",
                        ".ai/runtime/runs/",
                    ]),
                ReadPath(
                    "actor_runtime_observability",
                    "actor sessions | api worker-leases",
                    "Actor sessions and worker lease reads expose who currently owns runtime work without inventing a second execution ledger.",
                    [
                        "ActorSessionService",
                        "WorkerBroker",
                        ToRepoRelative(paths.PlatformSessionLiveStateRoot),
                    ]),
                ReadPath(
                    "workspace_runtime_lifecycle",
                    "runtime inspect <repo-id> | cleanup | compact-history",
                    "Runtime instance inspection and maintenance commands remain the governed read and action surfaces over workspace lifecycle.",
                    [
                        "RuntimeInstanceManager",
                        "WorktreeResourceCleanupService",
                        "RuntimeMaintenanceService",
                    ]),
            ],
            SuccessCriteria =
            [
                "Execution actor and runtime boundaries are queryable through one runtime surface.",
                "Implementation, verifier, and maintenance lanes all point back to the same task and run truth.",
                "Workspace lifecycle remains part of runtime maintenance rather than a parallel execution subsystem.",
            ],
            StopConditions =
            [
                "Do not introduce a second execution pipeline, queue, or actor-specific task store.",
                "Do not move actor or workspace live state into durable memory roots.",
                "Do not treat cleanup or rebuild as an alternate review/writeback mechanism.",
            ],
            Notes =
            [
                "This card line classifies the current execution stack; it does not rename existing services into actor-runtime packages yet.",
                "Maintenance actors remain execution-kernel participants because they operate on the same governed truth roots and workspace lifecycle.",
            ],
        };
    }

    private static RuntimeKernelTruthRootDescriptor Root(
        string rootId,
        string classification,
        string summary,
        string[] pathRefs,
        string[] truthRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelTruthRootDescriptor
        {
            RootId = rootId,
            Classification = classification,
            Summary = summary,
            PathRefs = pathRefs,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelBoundaryRule Rule(
        string ruleId,
        string summary,
        string[] allowedRefs,
        string[] forbiddenRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelBoundaryRule
        {
            RuleId = ruleId,
            Summary = summary,
            AllowedRefs = allowedRefs,
            ForbiddenRefs = forbiddenRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelGovernedReadPath ReadPath(
        string pathId,
        string entryPoint,
        string summary,
        string[] truthRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelGovernedReadPath
        {
            PathId = pathId,
            EntryPoint = entryPoint,
            Summary = summary,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }
}
