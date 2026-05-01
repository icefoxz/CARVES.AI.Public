using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class MarkdownProjector
{
    private static readonly DomainTaskStatus[] StatusOrder =
    {
        DomainTaskStatus.Suggested,
        DomainTaskStatus.Pending,
        DomainTaskStatus.Deferred,
        DomainTaskStatus.Running,
        DomainTaskStatus.Testing,
        DomainTaskStatus.Review,
        DomainTaskStatus.Completed,
        DomainTaskStatus.Merged,
        DomainTaskStatus.Failed,
        DomainTaskStatus.Blocked,
        DomainTaskStatus.Discarded,
        DomainTaskStatus.Superseded,
    };

    public MarkdownProjection Build(
        DomainTaskGraph graph,
        TaskNode? currentTask = null,
        TaskRunReport? report = null,
        PlannerReview? review = null,
        RuntimeSessionState? session = null,
        TaskScheduleDecision? schedulerDecision = null)
    {
        return new MarkdownProjection(
            BuildTaskQueue(graph),
            BuildState(graph, review, session, schedulerDecision),
            BuildCurrentTask(currentTask, report, review, session, schedulerDecision));
    }

    private static string BuildTaskQueue(DomainTaskGraph graph)
    {
        var lines = new List<string> { "# TASK_QUEUE", string.Empty, "Generated from `.ai/tasks/graph.json`.", string.Empty };
        foreach (var status in StatusOrder)
        {
            lines.Add($"## {ToTitle(status)}");
            lines.Add(string.Empty);
            var tasks = graph.ByStatus(status);
            if (tasks.Count == 0)
            {
                lines.Add("(none)");
                lines.Add(string.Empty);
                continue;
            }

            foreach (var task in tasks)
            {
                lines.Add($"### {task.TaskId}");
                lines.Add($"Status: {ToUpperSnake(status)}");
                lines.Add($"Task Type: {ToUpperSnake(task.TaskType)}");
                lines.Add($"Dispatch Eligibility: {task.TaskType.DescribeDispatchEligibility()}");
                lines.Add($"Priority: {task.Priority}");
                lines.Add($"Title: {task.Title}");
                lines.Add(string.Empty);
                lines.Add("Scope:");
                lines.AddRange(task.Scope.Any() ? task.Scope.Select(item => $"- {item}") : ["- (none)"]);
                lines.Add(string.Empty);
                lines.Add("Acceptance:");
                lines.AddRange(task.Acceptance.Any() ? task.Acceptance.Select(item => $"- {item}") : ["- (none)"]);
                lines.Add(string.Empty);
            }
        }

        return string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine;
    }

    private static string BuildState(DomainTaskGraph graph, PlannerReview? review, RuntimeSessionState? session, TaskScheduleDecision? schedulerDecision)
    {
        var completedTaskIds = graph.CompletedTaskIds();
        var acceptanceContractGapCount = graph.Tasks.Values.Count(task =>
            task.Status == DomainTaskStatus.Pending
            && task.CanDispatchToWorkerPool
            && task.IsReady(completedTaskIds)
            && AcceptanceContractExecutionGate.Evaluate(task).BlocksExecution);
        var verdict = review is not null
            ? ToSnakeCase(review.Verdict.ToString())
            : graph.ReadyTasks().Count == 0 && graph.ByStatus(DomainTaskStatus.Pending).Count == 0 && graph.ByStatus(DomainTaskStatus.Running).Count == 0
                ? "complete"
                : "n/a";

        return string.Join(Environment.NewLine,
        [
            "# STATE",
            string.Empty,
            "Phase: runtime",
            "Mode: ACTIVE",
            $"Runtime stage: {RuntimeStageInfo.CurrentStage}",
            $"Next stage: {RuntimeStageInfo.NextStage}",
            $"Last completed task count: {graph.ByStatus(DomainTaskStatus.Completed).Count + graph.ByStatus(DomainTaskStatus.Merged).Count}",
            string.Empty,
            "Current facts:",
            "- task state source of truth: `.ai/tasks/graph.json`",
            "- markdown views are generated from machine state",
            $"- runtime session: {(session is null ? "detached" : ToSnakeCase(session.Status.ToString()))}",
            $"- runtime loop mode: {(session is null ? "n/a" : ToSnakeCase(session.LoopMode.ToString()))}",
            $"- planner round: {(session is null ? "n/a" : session.PlannerRound.ToString(System.Globalization.CultureInfo.InvariantCulture))}",
            string.Empty,
            "Decision posture:",
            $"- last planner verdict: {verdict}",
            $"- last scheduler reason: {schedulerDecision?.Reason ?? session?.LastReason ?? "n/a"}",
            $"- dispatch-blocking acceptance contract gaps: {acceptanceContractGapCount}",
            $"- current actionability: {(session is null ? "n/a" : RuntimeActionabilitySemantics.Describe(session.CurrentActionability))}",
            $"- next action: {(session is null ? "n/a" : RuntimeActionabilitySemantics.DescribeNextAction(session))}",
            $"- analysis reason: {session?.AnalysisReason ?? "n/a"}",
            $"- opportunity source: {session?.LastOpportunitySource ?? "n/a"}",
            $"- opportunities detected/evaluated: {(session is null ? "n/a" : $"{session.DetectedOpportunityCount}/{session.EvaluatedOpportunityCount}")}",
            $"- waiting reason: {session?.WaitingReason ?? "n/a"}",
            $"- stop reason: {session?.StopReason ?? "n/a"}",
            string.Empty,
        ]);
    }

    private static string BuildCurrentTask(
        TaskNode? task,
        TaskRunReport? report,
        PlannerReview? review,
        RuntimeSessionState? session,
        TaskScheduleDecision? schedulerDecision)
    {
        if (task is null)
        {
            var idleLines = new List<string>
            {
                "# CURRENT_TASK",
                string.Empty,
                "Task ID: (none)",
                $"Status: {(session is null ? "IDLE" : ToSnakeCase(session.Status.ToString()).ToUpperInvariant())}",
                $"Session: {(session is null ? "(none)" : session.SessionId)}",
                $"Attached Repo: {session?.AttachedRepoRoot ?? "(none)"}",
            };

            if (session is not null)
            {
                idleLines.Add($"Active Workers: {session.ActiveWorkerCount}");
                idleLines.Add($"Active Tasks: {(session.ActiveTaskIds.Count == 0 ? "(none)" : string.Join(", ", session.ActiveTaskIds))}");
                idleLines.Add($"Review Pending Tasks: {(session.ReviewPendingTaskIds.Count == 0 ? "(none)" : string.Join(", ", session.ReviewPendingTaskIds))}");
                idleLines.Add($"Loop Mode: {ToSnakeCase(session.LoopMode.ToString()).ToUpperInvariant()}");
                idleLines.Add($"Current Actionability: {RuntimeActionabilitySemantics.Describe(session.CurrentActionability)}");
                idleLines.Add($"Next Action: {RuntimeActionabilitySemantics.DescribeNextAction(session)}");
                idleLines.Add($"Planner Round: {session.PlannerRound}");
                idleLines.Add($"Detected Opportunities: {session.DetectedOpportunityCount}");
                idleLines.Add($"Evaluated Opportunities: {session.EvaluatedOpportunityCount}");
                idleLines.Add($"Opportunity Source: {session.LastOpportunitySource ?? "(none)"}");
                idleLines.Add($"Analysis Reason: {session.AnalysisReason ?? "(none)"}");
                idleLines.Add($"Waiting Reason: {session.WaitingReason ?? "(none)"}");
                idleLines.Add($"Stop Reason: {session.StopReason ?? "(none)"}");
                idleLines.Add($"Planner Re-entry: {session.LastPlannerReentryOutcome ?? "(none)"}");
                idleLines.Add($"Last Reason: {schedulerDecision?.Reason ?? session.LastReason}");
            }

            return string.Join(Environment.NewLine, idleLines) + Environment.NewLine;
        }

        var lines = new List<string>
        {
            "# CURRENT_TASK",
            string.Empty,
            $"Task ID: {task.TaskId}",
            $"Status: {ToUpperSnake(task.Status)}",
            $"Task Type: {ToUpperSnake(task.TaskType)}",
            $"Mode: {(report?.DryRun == true ? "DRY_RUN" : "ACTIVE")}",
            string.Empty,
            $"Title: {task.Title}",
            string.Empty,
            "Scope:",
        };
        lines.AddRange(task.Scope.Any() ? task.Scope.Select(item => $"- {item}") : ["- (none)"]);
        lines.Add(string.Empty);
        lines.Add("Acceptance:");
        lines.AddRange(task.Acceptance.Any() ? task.Acceptance.Select(item => $"- {item}") : ["- (none)"]);
        lines.Add(string.Empty);

        if (report is not null)
        {
            lines.Add("Validation:");
            lines.Add($"- passed: {(report.Validation.Passed ? "yes" : "no")}");
            lines.Add($"- patch files changed: {report.Patch.FilesChanged}");
            lines.Add($"- safety mode: {ToSnakeCase(report.SafetyDecision.ValidationMode.ToString())}");
            lines.Add($"- safety outcome: {ToSnakeCase(report.SafetyDecision.Outcome.ToString())}");
            lines.AddRange(report.Validation.Evidence.Take(5).Select(item => $"- evidence: {item}"));
            lines.Add(string.Empty);
        }

        if (review is not null)
        {
            lines.Add("Planner Review:");
            lines.Add($"- verdict: {ToSnakeCase(review.Verdict.ToString())}");
            lines.Add($"- reason: {review.Reason}");
            lines.Add(string.Empty);
        }

        if (session is not null)
        {
            lines.Add("Runtime Session:");
            lines.Add($"- status: {ToSnakeCase(session.Status.ToString())}");
            lines.Add($"- active workers: {session.ActiveWorkerCount}");
            lines.Add($"- active tasks: {(session.ActiveTaskIds.Count == 0 ? "(none)" : string.Join(", ", session.ActiveTaskIds))}");
            lines.Add($"- review pending tasks: {(session.ReviewPendingTaskIds.Count == 0 ? "(none)" : string.Join(", ", session.ReviewPendingTaskIds))}");
            lines.Add($"- loop mode: {ToSnakeCase(session.LoopMode.ToString())}");
            lines.Add($"- current actionability: {RuntimeActionabilitySemantics.Describe(session.CurrentActionability)}");
            lines.Add($"- next action: {RuntimeActionabilitySemantics.DescribeNextAction(session)}");
            lines.Add($"- planner round: {session.PlannerRound}");
            lines.Add($"- detected opportunities: {session.DetectedOpportunityCount}");
            lines.Add($"- evaluated opportunities: {session.EvaluatedOpportunityCount}");
            lines.Add($"- opportunity source: {session.LastOpportunitySource ?? "(none)"}");
            lines.Add($"- analysis reason: {session.AnalysisReason ?? "(none)"}");
            lines.Add($"- waiting reason: {session.WaitingReason ?? "(none)"}");
            lines.Add($"- stop reason: {session.StopReason ?? "(none)"}");
            lines.Add($"- planner re-entry: {session.LastPlannerReentryOutcome ?? "(none)"}");
            lines.Add($"- last reason: {schedulerDecision?.Reason ?? session.LastReason}");
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines).TrimEnd() + Environment.NewLine;
    }

    private static string ToTitle(DomainTaskStatus status)
    {
        var words = ToSnakeCase(status.ToString())
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..])
            .ToArray();

        return string.Join(' ', words).Replace("Ai", "AI", StringComparison.Ordinal);
    }

    private static string ToUpperSnake(DomainTaskStatus status)
    {
        return ToSnakeCase(status.ToString()).ToUpperInvariant();
    }

    private static string ToUpperSnake(TaskType taskType)
    {
        return ToSnakeCase(taskType.ToString()).ToUpperInvariant();
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var chars = new List<char>(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string(chars.ToArray());
    }
}
