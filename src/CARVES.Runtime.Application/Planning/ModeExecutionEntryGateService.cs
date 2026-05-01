using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed class ModeExecutionEntryGateService
{
    private readonly FormalPlanningExecutionGateService formalPlanningExecutionGateService;

    public ModeExecutionEntryGateService(FormalPlanningExecutionGateService? formalPlanningExecutionGateService = null)
    {
        this.formalPlanningExecutionGateService = formalPlanningExecutionGateService ?? new FormalPlanningExecutionGateService();
    }

    public ModeExecutionEntryGateProjection Evaluate(TaskNode task)
    {
        var acceptanceContractGate = AcceptanceContractExecutionGate.Evaluate(task);
        var formalPlanningGate = formalPlanningExecutionGateService.Evaluate(task);
        var checks = BuildChecks(task, acceptanceContractGate, formalPlanningGate);
        var firstBlocker = checks.FirstOrDefault(static check => check.BlocksExecution);

        if (!task.CanExecuteInWorker)
        {
            return new ModeExecutionEntryGateProjection
            {
                Status = "not_required",
                ReasonCode = "not_required",
                TargetMode = "not_applicable",
                BlocksExecution = false,
                Summary = "Mode C/D execution entry gating does not apply to this task type.",
                RecommendedNextAction = "follow the task-type specific host lane",
                RecommendedNextCommand = "none",
                Checks = checks,
            };
        }

        if (firstBlocker is null)
        {
            return new ModeExecutionEntryGateProjection
            {
                Status = "ready",
                ReasonCode = "none",
                TargetMode = ResolveTargetMode(formalPlanningGate),
                BlocksExecution = false,
                Summary = $"Mode C/D execution entry prerequisites are satisfied for {task.TaskId}.",
                RecommendedNextAction = $"delegate through host: task run {task.TaskId}",
                RecommendedNextCommand = $"task run {task.TaskId}",
                Checks = checks,
            };
        }

        return new ModeExecutionEntryGateProjection
        {
            Status = "blocked",
            ReasonCode = firstBlocker.ReasonCode,
            TargetMode = ResolveTargetMode(formalPlanningGate),
            BlocksExecution = true,
            FirstBlockingCheckId = firstBlocker.CheckId,
            FirstBlockingCheckSummary = firstBlocker.Summary,
            FirstBlockingCheckRequiredAction = firstBlocker.RequiredAction,
            FirstBlockingCheckRequiredCommand = firstBlocker.RequiredCommand,
            Summary = firstBlocker.Summary,
            RecommendedNextAction = firstBlocker.RequiredAction,
            RecommendedNextCommand = firstBlocker.RequiredCommand,
            AcceptanceContractGap = acceptanceContractGate.BlocksExecution,
            PlanRequired = formalPlanningGate.PlanRequired,
            WorkspaceRequired = formalPlanningGate.WorkspaceRequired,
            Checks = checks,
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
            $"Task '{task.TaskId}' cannot enter {projection.TargetMode} because {projection.Summary} {projection.RecommendedNextAction}. Next command: {projection.RecommendedNextCommand}.");
    }

    private static IReadOnlyList<ModeExecutionEntryPrerequisiteProjection> BuildChecks(
        TaskNode task,
        AcceptanceContractExecutionGateProjection acceptanceContractGate,
        FormalPlanningExecutionGateProjection formalPlanningGate)
    {
        return
        [
            BuildFormalPlanningPacketCheck(task, formalPlanningGate),
            BuildAcceptanceContractCheck(task, acceptanceContractGate),
            BuildWorkspaceLeaseCheck(task, formalPlanningGate),
        ];
    }

    private static ModeExecutionEntryPrerequisiteProjection BuildFormalPlanningPacketCheck(
        TaskNode task,
        FormalPlanningExecutionGateProjection formalPlanningGate)
    {
        var blocks = formalPlanningGate.PlanRequired;
        return new ModeExecutionEntryPrerequisiteProjection
        {
            CheckId = "formal_planning_packet_available",
            ReasonCode = blocks ? formalPlanningGate.ReasonCode : "none",
            State = blocks ? "missing" : formalPlanningGate.Required ? "satisfied" : "not_required",
            BlocksExecution = blocks,
            Summary = blocks
                ? formalPlanningGate.Summary
                : formalPlanningGate.Required
                    ? $"Execution task {task.TaskId} is bound to the active formal planning packet."
                    : formalPlanningGate.Summary,
            RequiredAction = blocks ? formalPlanningGate.RecommendedNextAction : "none",
            RequiredCommand = blocks ? ResolveFormalPlanningCommand(formalPlanningGate) : "none",
        };
    }

    private static ModeExecutionEntryPrerequisiteProjection BuildAcceptanceContractCheck(
        TaskNode task,
        AcceptanceContractExecutionGateProjection acceptanceContractGate)
    {
        var blocks = acceptanceContractGate.BlocksExecution;
        return new ModeExecutionEntryPrerequisiteProjection
        {
            CheckId = "acceptance_contract_projected",
            ReasonCode = blocks ? acceptanceContractGate.ReasonCode : "none",
            State = blocks ? "missing" : acceptanceContractGate.Required ? "satisfied" : "not_required",
            BlocksExecution = blocks,
            Summary = acceptanceContractGate.Summary,
            RequiredAction = blocks ? acceptanceContractGate.RecommendedNextAction : "none",
            RequiredCommand = blocks ? $"inspect task {task.TaskId}" : "none",
        };
    }

    private static ModeExecutionEntryPrerequisiteProjection BuildWorkspaceLeaseCheck(
        TaskNode task,
        FormalPlanningExecutionGateProjection formalPlanningGate)
    {
        var blocks = formalPlanningGate.WorkspaceRequired;
        return new ModeExecutionEntryPrerequisiteProjection
        {
            CheckId = "managed_workspace_lease_available",
            ReasonCode = blocks ? formalPlanningGate.ReasonCode : "none",
            State = blocks ? "missing" : formalPlanningGate.Required ? "satisfied" : "not_required",
            BlocksExecution = blocks,
            Summary = blocks
                ? formalPlanningGate.Summary
                : formalPlanningGate.ActiveLeaseId is null
                    ? $"Execution task {task.TaskId} does not require a managed workspace lease under the current entry posture."
                    : $"Execution task {task.TaskId} has active managed workspace lease '{formalPlanningGate.ActiveLeaseId}'.",
            RequiredAction = blocks ? formalPlanningGate.RecommendedNextAction : "none",
            RequiredCommand = blocks ? $"plan issue-workspace {task.TaskId}" : "none",
        };
    }

    private static string ResolveFormalPlanningCommand(FormalPlanningExecutionGateProjection formalPlanningGate)
    {
        return formalPlanningGate.ReasonCode switch
        {
            "formal_plan_required" => "plan init [candidate-card-id]",
            "plan_handle_mismatch" => "plan status",
            _ => "plan status",
        };
    }

    private static string ResolveTargetMode(FormalPlanningExecutionGateProjection formalPlanningGate)
    {
        if (formalPlanningGate.Required && !string.IsNullOrWhiteSpace(formalPlanningGate.ActiveLeaseId))
        {
            return "mode_d_scoped_task_workspace";
        }

        if (formalPlanningGate.Required || formalPlanningGate.WorkspaceRequired)
        {
            return "mode_c_task_bound_workspace";
        }

        return "mode_a_open_repo_advisory";
    }
}

public sealed record ModeExecutionEntryGateProjection
{
    public string Status { get; init; } = "not_required";

    public string ReasonCode { get; init; } = "not_required";

    public string TargetMode { get; init; } = "mode_a_open_repo_advisory";

    public bool BlocksExecution { get; init; }

    public string? FirstBlockingCheckId { get; init; }

    public string? FirstBlockingCheckSummary { get; init; }

    public string? FirstBlockingCheckRequiredAction { get; init; }

    public string? FirstBlockingCheckRequiredCommand { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public string RecommendedNextCommand { get; init; } = string.Empty;

    public bool AcceptanceContractGap { get; init; }

    public bool PlanRequired { get; init; }

    public bool WorkspaceRequired { get; init; }

    public IReadOnlyList<ModeExecutionEntryPrerequisiteProjection> Checks { get; init; } = [];
}

public sealed record ModeExecutionEntryPrerequisiteProjection
{
    public string CheckId { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public bool BlocksExecution { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string RequiredAction { get; init; } = string.Empty;

    public string RequiredCommand { get; init; } = string.Empty;
}
