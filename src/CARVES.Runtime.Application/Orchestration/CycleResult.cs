using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.Orchestration;

public sealed record CycleResult
{
    public IReadOnlyList<TaskNode> Tasks { get; init; } = Array.Empty<TaskNode>();

    public IReadOnlyList<WorkerRequest> Requests { get; init; } = Array.Empty<WorkerRequest>();

    public IReadOnlyList<TaskRunReport> Reports { get; init; } = Array.Empty<TaskRunReport>();

    public IReadOnlyList<PlannerReview> Reviews { get; init; } = Array.Empty<PlannerReview>();

    public IReadOnlyList<TaskTransitionDecision> Transitions { get; init; } = Array.Empty<TaskTransitionDecision>();

    public RuntimeSessionState? Session { get; init; }

    public TaskScheduleDecision? ScheduleDecision { get; init; }

    public PlannerReentryResult? PlannerReentry { get; init; }

    public string Message { get; init; } = string.Empty;

    public TaskNode? Task => Tasks.FirstOrDefault();

    public WorkerRequest? Request => Requests.FirstOrDefault();

    public TaskRunReport? Report => Reports.FirstOrDefault();

    public PlannerReview? Review => Reviews.FirstOrDefault();

    public TaskTransitionDecision? Transition => Transitions.FirstOrDefault();
}
