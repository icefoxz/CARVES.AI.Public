using System.Globalization;
using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionRunService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ControlPlanePaths paths;
    private readonly RuntimePackExecutionAttributionService? runtimePackExecutionAttributionService;

    public ExecutionRunService(ControlPlanePaths paths, IRuntimeArtifactRepository? artifactRepository = null)
    {
        this.paths = paths;
        runtimePackExecutionAttributionService = artifactRepository is null
            ? null
            : new RuntimePackExecutionAttributionService(paths.RepoRoot, artifactRepository);
    }

    public TaskNode ApplyTaskMetadata(TaskNode task, ExecutionRun latestRun, string? activeRunId)
    {
        var metadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            ["execution_run_latest_id"] = latestRun.RunId,
            ["execution_run_latest_status"] = latestRun.Status.ToString(),
            ["execution_run_current_step_index"] = latestRun.CurrentStepIndex.ToString(CultureInfo.InvariantCulture),
            ["execution_run_current_step_title"] = ResolveStepTitle(latestRun),
            ["execution_run_count"] = ListRuns(task.TaskId).Count.ToString(CultureInfo.InvariantCulture),
        };

        if (string.IsNullOrWhiteSpace(activeRunId))
        {
            metadata.Remove("execution_run_active_id");
        }
        else
        {
            metadata["execution_run_active_id"] = activeRunId;
        }

        return CloneTask(task, metadata);
    }
}
