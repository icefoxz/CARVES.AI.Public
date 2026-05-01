using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeHealthCheckService
{
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;

    public RuntimeHealthCheckService(ControlPlanePaths paths, TaskGraphService taskGraphService)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
    }

    public RepoRuntimeHealthCheckResult Evaluate()
    {
        var requiredDirectories = new[]
        {
            paths.AiRoot,
            Path.Combine(paths.AiRoot, "memory"),
            paths.CardsRoot,
            paths.TaskNodesRoot,
            Path.Combine(paths.AiRoot, "codegraph"),
            Path.Combine(paths.AiRoot, "patches"),
            Path.Combine(paths.AiRoot, "reviews"),
            paths.ArtifactsRoot,
            paths.RuntimeRoot,
        };
        var missingDirectories = requiredDirectories
            .Where(path => !Directory.Exists(path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var graph = taskGraphService.Load();
        var interruptedTaskIds = graph.ListTasks()
            .Where(task => task.Status is Carves.Runtime.Domain.Tasks.TaskStatus.Running or Carves.Runtime.Domain.Tasks.TaskStatus.Testing)
            .Select(task => task.TaskId)
            .OrderBy(taskId => taskId, StringComparer.Ordinal)
            .ToArray();

        var danglingArtifacts = Directory.Exists(paths.WorkerExecutionArtifactsRoot)
            ? Directory.GetFiles(paths.WorkerExecutionArtifactsRoot, "*.json", SearchOption.TopDirectoryOnly)
                .Where(path =>
                {
                    var taskId = Path.GetFileNameWithoutExtension(path);
                    return !graph.Tasks.ContainsKey(taskId);
                })
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();

        var issues = new List<RepoRuntimeHealthIssue>();
        if (missingDirectories.Length > 0)
        {
            issues.AddRange(missingDirectories.Select(path => new RepoRuntimeHealthIssue(
                "missing_directory",
                $"Required runtime directory '{path}' is missing.",
                "error",
                path)));
        }

        if (interruptedTaskIds.Length > 0)
        {
            issues.AddRange(interruptedTaskIds.Select(taskId => new RepoRuntimeHealthIssue(
                "interrupted_task",
                $"Task '{taskId}' is still marked as running/testing.",
                "warning",
                Path.Combine(paths.TaskNodesRoot, $"{taskId}.json"))));
        }

        if (danglingArtifacts.Length > 0)
        {
            issues.AddRange(danglingArtifacts.Select(path => new RepoRuntimeHealthIssue(
                "dangling_artifact",
                $"Execution artifact '{path}' is not anchored to any governed task.",
                "warning",
                path)));
        }

        var state = issues.Count == 0
            ? RepoRuntimeHealthState.Healthy
            : missingDirectories.Length > 0
                ? RepoRuntimeHealthState.Broken
                : RepoRuntimeHealthState.Dirty;

        var summary = state switch
        {
            RepoRuntimeHealthState.Healthy => "Runtime health is healthy.",
            RepoRuntimeHealthState.Dirty => "Runtime has interrupted or stale derived-state residue.",
            _ => "Runtime is missing required control-plane state and needs repair.",
        };

        var suggestedAction = state switch
        {
            RepoRuntimeHealthState.Healthy => "observe",
            RepoRuntimeHealthState.Dirty => "repair",
            _ => "rebuild",
        };

        return new RepoRuntimeHealthCheckResult
        {
            State = state,
            Issues = issues,
            MissingDirectories = missingDirectories,
            InterruptedTaskIds = interruptedTaskIds,
            DanglingArtifactPaths = danglingArtifacts,
            Summary = summary,
            SuggestedAction = suggestedAction,
        };
    }
}
