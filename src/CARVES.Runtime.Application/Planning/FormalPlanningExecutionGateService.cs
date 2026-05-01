using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed class FormalPlanningExecutionGateService
{
    private readonly IntentDiscoveryService? intentDiscoveryService;
    private readonly IManagedWorkspaceLeaseRepository? managedWorkspaceLeaseRepository;

    public FormalPlanningExecutionGateService(
        IntentDiscoveryService? intentDiscoveryService = null,
        IManagedWorkspaceLeaseRepository? managedWorkspaceLeaseRepository = null)
    {
        this.intentDiscoveryService = intentDiscoveryService;
        this.managedWorkspaceLeaseRepository = managedWorkspaceLeaseRepository;
    }

    public FormalPlanningExecutionGateProjection Evaluate(TaskNode task)
    {
        if (!task.CanExecuteInWorker)
        {
            return new FormalPlanningExecutionGateProjection
            {
                Status = "not_required",
                ReasonCode = "not_required",
                Required = false,
                BlocksExecution = false,
                Summary = "Formal planning execution gating does not apply to this task type.",
                RecommendedNextAction = "follow the task-type specific host lane",
            };
        }

        var lineage = PlanningLineageMetadata.TryRead(task.Metadata);
        if (lineage is null || intentDiscoveryService is null || managedWorkspaceLeaseRepository is null)
        {
            return new FormalPlanningExecutionGateProjection
            {
                Status = "not_required",
                ReasonCode = lineage is null ? "task_not_bound_to_formal_planning" : "gate_not_enforced",
                Required = false,
                BlocksExecution = false,
                Summary = lineage is null
                    ? $"Execution task {task.TaskId} is not bound to formal planning lineage, so Phase 5 managed-workspace gating does not apply."
                    : $"Formal planning execution gating is not enforced for {task.TaskId} in this composition.",
                RecommendedNextAction = $"delegate through host: task run {task.TaskId}",
            };
        }

        var expectedPlanHandle = FormalPlanningPacketService.BuildPlanHandle(lineage);
        var currentPlanHandle = TryGetCurrentPlanHandle();
        if (string.IsNullOrWhiteSpace(currentPlanHandle))
        {
            return new FormalPlanningExecutionGateProjection
            {
                Status = "plan_required",
                ReasonCode = "formal_plan_required",
                Required = true,
                BlocksExecution = true,
                PlanHandle = expectedPlanHandle,
                Summary = $"Execution task {task.TaskId} is bound to plan handle '{expectedPlanHandle}', but no active formal planning card exists.",
                RecommendedNextAction = "run `plan init [candidate-card-id]` and restore the matching plan handle before dispatch",
            };
        }

        if (!string.Equals(expectedPlanHandle, currentPlanHandle, StringComparison.Ordinal))
        {
            return new FormalPlanningExecutionGateProjection
            {
                Status = "plan_required",
                ReasonCode = "plan_handle_mismatch",
                Required = true,
                BlocksExecution = true,
                PlanHandle = expectedPlanHandle,
                CurrentPlanHandle = currentPlanHandle,
                Summary = $"Execution task {task.TaskId} is bound to plan handle '{expectedPlanHandle}', not the current active plan handle '{currentPlanHandle}'.",
                RecommendedNextAction = "refresh the current planning packet and keep execution on one active plan handle before dispatch",
            };
        }

        var lease = managedWorkspaceLeaseRepository.Load().Leases
            .Where(existing => existing.Status == ManagedWorkspaceLeaseStatus.Active)
            .FirstOrDefault(existing =>
                string.Equals(existing.TaskId, task.TaskId, StringComparison.Ordinal)
                && string.Equals(existing.PlanHandle, currentPlanHandle, StringComparison.Ordinal));
        if (lease is null)
        {
            return new FormalPlanningExecutionGateProjection
            {
                Status = "workspace_required",
                ReasonCode = "managed_workspace_required",
                Required = true,
                BlocksExecution = true,
                PlanHandle = expectedPlanHandle,
                CurrentPlanHandle = currentPlanHandle,
                Summary = $"Execution task {task.TaskId} is bound to plan handle '{expectedPlanHandle}' but has no active managed workspace lease.",
                RecommendedNextAction = $"run `plan issue-workspace {task.TaskId}` before dispatch",
            };
        }

        return new FormalPlanningExecutionGateProjection
        {
            Status = "ready",
            ReasonCode = "none",
            Required = true,
            BlocksExecution = false,
            PlanHandle = expectedPlanHandle,
            CurrentPlanHandle = currentPlanHandle,
            ActiveLeaseId = lease.LeaseId,
            Summary = $"Formal planning handle '{expectedPlanHandle}' and managed workspace lease '{lease.LeaseId}' are active for {task.TaskId}.",
            RecommendedNextAction = $"delegate through host: task run {task.TaskId}",
        };
    }

    public FormalPlanningTaskGraphGateProjection EvaluateTaskGraphPersistence(TaskGraphDraftRecord draft)
    {
        var lineage = draft.PlanningLineage;
        if (lineage is null || intentDiscoveryService is null)
        {
            return new FormalPlanningTaskGraphGateProjection
            {
                Status = "not_required",
                ReasonCode = lineage is null ? "draft_not_bound_to_formal_planning" : "gate_not_enforced",
                Required = false,
                BlocksPersistence = false,
                Summary = lineage is null
                    ? $"Taskgraph draft {draft.DraftId} is not bound to formal planning lineage, so Phase 5 planning gating does not apply."
                    : $"Formal planning taskgraph gating is not enforced for {draft.DraftId} in this composition.",
                RecommendedNextAction = $"approve-taskgraph-draft {draft.DraftId} <reason...>",
            };
        }

        var expectedPlanHandle = FormalPlanningPacketService.BuildPlanHandle(lineage);
        var currentPlanHandle = TryGetCurrentPlanHandle();
        if (string.IsNullOrWhiteSpace(currentPlanHandle))
        {
            return new FormalPlanningTaskGraphGateProjection
            {
                Status = "plan_required",
                ReasonCode = "formal_plan_required",
                Required = true,
                BlocksPersistence = true,
                PlanHandle = expectedPlanHandle,
                Summary = $"Taskgraph draft {draft.DraftId} is bound to plan handle '{expectedPlanHandle}', but no active formal planning card exists.",
                RecommendedNextAction = "run `plan init [candidate-card-id]` and restore the matching plan handle before approving task truth",
            };
        }

        if (!string.Equals(expectedPlanHandle, currentPlanHandle, StringComparison.Ordinal))
        {
            return new FormalPlanningTaskGraphGateProjection
            {
                Status = "plan_required",
                ReasonCode = "plan_handle_mismatch",
                Required = true,
                BlocksPersistence = true,
                PlanHandle = expectedPlanHandle,
                CurrentPlanHandle = currentPlanHandle,
                Summary = $"Taskgraph draft {draft.DraftId} is bound to plan handle '{expectedPlanHandle}', not the current active plan handle '{currentPlanHandle}'.",
                RecommendedNextAction = "refresh the active planning packet and approve task truth on the matching plan handle",
            };
        }

        return new FormalPlanningTaskGraphGateProjection
        {
            Status = "ready",
            ReasonCode = "none",
            Required = true,
            BlocksPersistence = false,
            PlanHandle = expectedPlanHandle,
            CurrentPlanHandle = currentPlanHandle,
            Summary = $"Taskgraph draft {draft.DraftId} is aligned to the current active plan handle '{expectedPlanHandle}'.",
            RecommendedNextAction = $"approve-taskgraph-draft {draft.DraftId} <reason...>",
        };
    }

    public void EnsureReadyForExecution(TaskNode task)
    {
        var projection = Evaluate(task);
        if (!projection.BlocksExecution)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Task '{task.TaskId}' cannot execute because {projection.Summary} {projection.RecommendedNextAction}.");
    }

    public void EnsureReadyForTaskGraphPersistence(TaskGraphDraftRecord draft)
    {
        var projection = EvaluateTaskGraphPersistence(draft);
        if (!projection.BlocksPersistence)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Taskgraph draft '{draft.DraftId}' cannot be approved because {projection.Summary} {projection.RecommendedNextAction}.");
    }

    private string? TryGetCurrentPlanHandle()
    {
        var activePlanningCard = intentDiscoveryService?.GetStatus().Draft?.ActivePlanningCard;
        return activePlanningCard is null
            ? null
            : FormalPlanningPacketService.BuildPlanHandle(activePlanningCard);
    }
}

public sealed record FormalPlanningExecutionGateProjection
{
    public string Status { get; init; } = "not_required";

    public string ReasonCode { get; init; } = "not_required";

    public bool Required { get; init; }

    public bool BlocksExecution { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public string? PlanHandle { get; init; }

    public string? CurrentPlanHandle { get; init; }

    public string? ActiveLeaseId { get; init; }

    public bool PlanRequired => BlocksExecution
        && (string.Equals(ReasonCode, "formal_plan_required", StringComparison.Ordinal)
            || string.Equals(ReasonCode, "plan_handle_mismatch", StringComparison.Ordinal));

    public bool WorkspaceRequired => BlocksExecution
        && string.Equals(ReasonCode, "managed_workspace_required", StringComparison.Ordinal);
}

public sealed record FormalPlanningTaskGraphGateProjection
{
    public string Status { get; init; } = "not_required";

    public string ReasonCode { get; init; } = "not_required";

    public bool Required { get; init; }

    public bool BlocksPersistence { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public string? PlanHandle { get; init; }

    public string? CurrentPlanHandle { get; init; }
}
