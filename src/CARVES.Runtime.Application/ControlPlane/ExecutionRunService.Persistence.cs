using System.Globalization;
using System.Text.Json;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionRunService
{
    public ExecutionRun? TryLoad(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return null;
        }

        var runsRoot = RunsRoot;
        if (!Directory.Exists(runsRoot))
        {
            return null;
        }

        var file = Directory.EnumerateFiles(runsRoot, $"{runId}.json", SearchOption.AllDirectories).FirstOrDefault();
        return file is null ? null : Read(file);
    }

    public ExecutionRun Get(string runId)
    {
        return TryLoad(runId) ?? throw new InvalidOperationException($"Execution run '{runId}' was not found.");
    }

    public ExecutionRun? TryGetActiveRun(TaskNode task)
    {
        return TryLoadActiveRun(task);
    }

    public string GetRunPath(string taskId, string runId)
    {
        return Path.Combine(GetTaskRoot(taskId), $"{runId}.json");
    }

    private ExecutionRun CreateAndStart(TaskNode task, ExecutionRunPlan plan)
    {
        return StartRun(CreateRun(task, plan));
    }

    private ExecutionRun CreateRun(TaskNode task, ExecutionRunPlan plan)
    {
        Directory.CreateDirectory(GetTaskRoot(task.TaskId));
        var sequence = ListRuns(task.TaskId).Count + 1;
        var runId = $"RUN-{task.TaskId}-{sequence:000}";
        var steps = plan.Steps.Select((step, index) => new ExecutionStep
        {
            StepId = $"{runId}-STEP-{index + 1:000}",
            Title = step.Title,
            Kind = step.Kind,
            Status = ExecutionStepStatus.Pending,
            Metadata = step.Metadata,
        }).ToArray();

        var run = new ExecutionRun
        {
            RunId = runId,
            TaskId = task.TaskId,
            Status = ExecutionRunStatus.Planned,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            TriggerReason = plan.TriggerReason,
            Goal = plan.Goal,
            CurrentStepIndex = 0,
            SelectedPack = runtimePackExecutionAttributionService?.TryLoadCurrentSelectionReference(),
            PlannerContextId = plan.Metadata.TryGetValue("plannerContextId", out var plannerContextId)
                ? plannerContextId
                : null,
            Metadata = plan.Metadata,
            Steps = steps,
        };
        Save(run);
        return run;
    }

    private ExecutionRun StartRun(ExecutionRun run)
    {
        var startedAt = run.StartedAtUtc ?? DateTimeOffset.UtcNow;
        var steps = run.Steps.ToArray();
        if (steps.Length > 0)
        {
            steps = steps
                .Select((step, index) =>
                {
                    if (index == 0 && step.Kind == ExecutionStepKind.Inspect)
                    {
                        return step with
                        {
                            Status = ExecutionStepStatus.Completed,
                            StartedAtUtc = step.StartedAtUtc ?? startedAt,
                            EndedAtUtc = step.EndedAtUtc ?? startedAt,
                        };
                    }

                    if (index == ResolveStartStepIndex(steps))
                    {
                        return step with
                        {
                            Status = ExecutionStepStatus.InProgress,
                            StartedAtUtc = step.StartedAtUtc ?? startedAt,
                        };
                    }

                    return step;
                })
                .ToArray();
        }

        var running = run with
        {
            Status = ExecutionRunStatus.Running,
            StartedAtUtc = startedAt,
            CurrentStepIndex = ResolveStartStepIndex(steps),
            Steps = steps,
        };
        Save(running);
        return running;
    }

    private ExecutionRun? TryLoadActiveRun(TaskNode task)
    {
        if (task.Metadata.TryGetValue("execution_run_active_id", out var activeRunId))
        {
            return TryLoad(activeRunId);
        }

        return ListRuns(task.TaskId)
            .LastOrDefault(run => run.Status is ExecutionRunStatus.Planned or ExecutionRunStatus.Running);
    }

    private void Save(ExecutionRun run)
    {
        var taskRoot = GetTaskRoot(run.TaskId);
        Directory.CreateDirectory(taskRoot);
        File.WriteAllText(GetRunPath(run.TaskId, run.RunId), JsonSerializer.Serialize(run, JsonOptions));
    }

    private static ExecutionRun Read(string path)
    {
        return JsonSerializer.Deserialize<ExecutionRun>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"Execution run at '{path}' could not be deserialized.");
    }

    private string GetTaskRoot(string taskId)
    {
        return Path.Combine(RunsRoot, taskId);
    }

    private string RunsRoot => Path.Combine(paths.AiRoot, "runtime", "runs");

    private static TaskNode CloneTask(TaskNode task, IReadOnlyDictionary<string, string> metadata)
    {
        return new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
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
            Metadata = metadata,
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
            PlannerReview = task.PlannerReview,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static int ExtractRunSequence(string runId)
    {
        var separator = runId.LastIndexOf('-');
        if (separator < 0)
        {
            return 0;
        }

        return int.TryParse(runId[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}
