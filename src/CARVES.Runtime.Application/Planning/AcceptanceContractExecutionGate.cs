using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public static class AcceptanceContractExecutionGate
{
    public static AcceptanceContractExecutionGateProjection Evaluate(TaskNode task)
    {
        var contract = task.AcceptanceContract;
        if (!task.CanExecuteInWorker)
        {
            return new AcceptanceContractExecutionGateProjection
            {
                Status = "not_required",
                ReasonCode = "not_required",
                Required = false,
                Projected = contract is not null,
                BlocksExecution = false,
                Summary = "Acceptance contract execution gating does not apply to this task type.",
                RecommendedNextAction = "follow the task-type specific host lane",
                ContractId = contract?.ContractId,
                ContractLifecycleStatus = contract?.Status.ToString(),
            };
        }

        if (contract is not null)
        {
            return new AcceptanceContractExecutionGateProjection
            {
                Status = "projected",
                ReasonCode = "none",
                Required = true,
                Projected = true,
                BlocksExecution = false,
                Summary = $"Acceptance contract {contract.ContractId} is projected with status {contract.Status}.",
                RecommendedNextAction = $"delegate through host: task run {task.TaskId}",
                ContractId = contract.ContractId,
                ContractLifecycleStatus = contract.Status.ToString(),
            };
        }

        return new AcceptanceContractExecutionGateProjection
        {
            Status = "missing",
            ReasonCode = "acceptance_contract_missing",
            Required = true,
            Projected = false,
            BlocksExecution = true,
            Summary = $"Execution task {task.TaskId} is missing an acceptance contract and cannot be dispatched.",
            RecommendedNextAction = "project a minimum acceptance contract onto task truth before dispatch",
        };
    }

    public static bool IsReadyForDispatch(TaskNode task, IReadOnlySet<string> completedTaskIds)
    {
        return task.Status == Carves.Runtime.Domain.Tasks.TaskStatus.Pending
            && task.CanDispatchToWorkerPool
            && task.IsReady(completedTaskIds)
            && !Evaluate(task).BlocksExecution;
    }

    public static void EnsureReadyForExecution(TaskNode task)
    {
        var projection = Evaluate(task);
        if (!projection.BlocksExecution)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Task '{task.TaskId}' cannot execute because acceptance contract projection is missing. {projection.RecommendedNextAction}.");
    }
}

public sealed record AcceptanceContractExecutionGateProjection
{
    public string Status { get; init; } = "not_required";

    public string ReasonCode { get; init; } = "not_required";

    public bool Required { get; init; }

    public bool Projected { get; init; }

    public bool BlocksExecution { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public string? ContractId { get; init; }

    public string? ContractLifecycleStatus { get; init; }
}
