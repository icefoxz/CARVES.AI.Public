using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionRunService
{
    public TaskNode? TryReconcileInactiveTaskRun(TaskNode task, string? notes = null)
    {
        var latestRun = ListRuns(task.TaskId).LastOrDefault();
        if (latestRun is null
            || latestRun.Status is not (ExecutionRunStatus.Planned or ExecutionRunStatus.Running)
            || task.Status is Carves.Runtime.Domain.Tasks.TaskStatus.Running or Carves.Runtime.Domain.Tasks.TaskStatus.ApprovalWait)
        {
            return null;
        }

        var abandonedRun = AbandonRun(latestRun, notes);
        return ApplyTaskMetadata(task, abandonedRun, activeRunId: null);
    }

    public IReadOnlyList<ExecutionRun> ListRuns(string taskId)
    {
        var taskRoot = GetTaskRoot(taskId);
        if (!Directory.Exists(taskRoot))
        {
            return Array.Empty<ExecutionRun>();
        }

        return Directory.GetFiles(taskRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Read)
            .OrderBy(run => ExtractRunSequence(run.RunId))
            .ThenBy(run => run.CreatedAtUtc)
            .ToArray();
    }

    public IReadOnlyList<ExecutionRun> ListActiveRuns()
    {
        var runsRoot = RunsRoot;
        if (!Directory.Exists(runsRoot))
        {
            return Array.Empty<ExecutionRun>();
        }

        return Directory.GetDirectories(runsRoot)
            .SelectMany(taskRoot => Directory.GetFiles(taskRoot, "*.json", SearchOption.TopDirectoryOnly))
            .Select(Read)
            .Where(run => run.Status is ExecutionRunStatus.Planned or ExecutionRunStatus.Running)
            .OrderBy(run => run.TaskId, StringComparer.Ordinal)
            .ThenBy(run => ExtractRunSequence(run.RunId))
            .ToArray();
    }

    public IReadOnlyList<ExecutionRun> ListRecentRuns(int limit)
    {
        if (limit <= 0)
        {
            return Array.Empty<ExecutionRun>();
        }

        var runsRoot = RunsRoot;
        if (!Directory.Exists(runsRoot))
        {
            return Array.Empty<ExecutionRun>();
        }

        return Directory.EnumerateFiles(runsRoot, "*.json", SearchOption.AllDirectories)
            .Select(Read)
            .OrderByDescending(run => run.EndedAtUtc ?? run.StartedAtUtc ?? run.CreatedAtUtc)
            .ThenByDescending(run => run.RunId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }
}
