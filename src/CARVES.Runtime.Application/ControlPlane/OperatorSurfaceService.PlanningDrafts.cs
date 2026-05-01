using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult CreateCardDraft(string jsonPath)
    {
        var draft = planningDraftService.CreateCardDraft(jsonPath);
        return OperatorCommandResult.Success(
            $"Created card draft {draft.CardId}.",
            $"Status: {draft.Status.ToString().ToLowerInvariant()}",
            $"Methodology coverage: {draft.MethodologyCoverageStatus ?? "not_applicable"}",
            $"Markdown: {draft.MarkdownPath}");
    }

    public OperatorCommandResult UpdateCardDraft(string cardId, string jsonPath)
    {
        var draft = planningDraftService.UpdateCardDraft(cardId, jsonPath);
        return OperatorCommandResult.Success(
            $"Updated card draft {draft.CardId}.",
            $"Status: {draft.Status.ToString().ToLowerInvariant()}",
            $"Methodology coverage: {draft.MethodologyCoverageStatus ?? "not_applicable"}",
            $"Markdown: {draft.MarkdownPath}");
    }

    public OperatorCommandResult SetCardStatus(string cardId, CardLifecycleState state, string? reason)
    {
        var draft = planningDraftService.SetCardStatus(cardId, state, reason);
        return OperatorCommandResult.Success(
            $"Updated card {draft.CardId}.",
            $"Status: {draft.Status.ToString().ToLowerInvariant()}",
            $"Reason: {draft.LifecycleReason ?? "(none)"}");
    }

    public OperatorCommandResult ListCards(string? state = null)
    {
        var cards = planningDraftService.ListCards();
        var filtered = string.IsNullOrWhiteSpace(state)
            ? cards
            : cards.Where(item => string.Equals(item.Status.ToString(), state, StringComparison.OrdinalIgnoreCase)).ToArray();
        var lines = new List<string>
        {
            $"Cards: {filtered.Count}",
        };
        lines.AddRange(filtered.Select(item => $"- {item.CardId} [{item.Status}] {item.Title}"));
        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult CreateTaskGraphDraft(string jsonPath)
    {
        var draft = planningDraftService.CreateTaskGraphDraft(jsonPath);
        var materialization = TaskGraphAcceptanceContractMaterializationGuard.Evaluate(draft);
        return OperatorCommandResult.Success(
            $"Created taskgraph draft {draft.DraftId} for {draft.CardId}.",
            $"Status: {draft.Status.ToString().ToLowerInvariant()}",
            $"Tasks: {draft.Tasks.Count}",
            $"Acceptance contract materialization: {materialization.State}",
            $"Executable tasks requiring contract: {materialization.ExecutableTaskCount}",
            $"Explicit acceptance contracts: {materialization.ExplicitContractCount}",
            $"Synthesized minimum acceptance contracts: {materialization.SynthesizedMinimumContractCount}",
            $"Materialization failures: {materialization.FailureCount}",
            $"Materialization next action: {materialization.RecommendedAction}");
    }

    public OperatorCommandResult ApproveTaskGraphDraft(string draftId, string reason)
    {
        var draft = planningDraftService.ApproveTaskGraphDraft(draftId, reason);
        markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
        var materialization = TaskGraphAcceptanceContractMaterializationGuard.Evaluate(draft);
        return OperatorCommandResult.Success(
            $"Approved taskgraph draft {draft.DraftId}.",
            $"Tasks promoted to pending: {draft.Tasks.Count}",
            $"Acceptance contract materialization: {materialization.State}",
            $"Explicit acceptance contracts: {materialization.ExplicitContractCount}",
            $"Synthesized minimum acceptance contracts: {materialization.SynthesizedMinimumContractCount}",
            $"Reason: {draft.ApprovalReason}");
    }

    public OperatorCommandResult InspectCardDraft(string draftIdOrCardId)
    {
        var draft = planningDraftService.TryGetCardDraft(draftIdOrCardId)
            ?? throw new InvalidOperationException($"Card draft '{draftIdOrCardId}' was not found.");
        return OperatorCommandResult.Success(
            $"Card draft: {draft.CardId}",
            $"Status: {draft.Status.ToString().ToLowerInvariant()}",
            $"Title: {draft.Title}",
            $"Goal: {draft.Goal}",
            $"Acceptance: {(draft.Acceptance.Count == 0 ? "(none)" : string.Join(" | ", draft.Acceptance))}",
            $"Acceptance contract: {draft.AcceptanceContract?.ContractId ?? "(none)"} [{draft.AcceptanceContract?.Status.ToString() ?? "none"}]",
            $"Notes: {(draft.Notes.Count == 0 ? "(none)" : string.Join(" | ", draft.Notes))}",
            $"Lifecycle reason: {draft.LifecycleReason ?? "(none)"}",
            $"Methodology required: {draft.MethodologyRequired}",
            $"Methodology acknowledged: {draft.MethodologyAcknowledged}",
            $"Methodology coverage: {draft.MethodologyCoverageStatus ?? "not_applicable"}",
            $"Methodology summary: {draft.MethodologySummary ?? "(none)"}",
            $"Methodology next action: {draft.MethodologyRecommendedAction ?? "(none)"}");
    }

    public OperatorCommandResult InspectTaskGraphDraft(string draftId)
    {
        var draft = planningDraftService.TryGetTaskGraphDraft(draftId)
            ?? throw new InvalidOperationException($"Taskgraph draft '{draftId}' was not found.");
        var lines = new List<string>
        {
            $"Taskgraph draft: {draft.DraftId}",
            $"Card: {draft.CardId}",
            $"Status: {draft.Status.ToString().ToLowerInvariant()}",
            $"Tasks: {draft.Tasks.Count}",
            $"Methodology required: {draft.MethodologyRequired}",
            $"Methodology acknowledged: {draft.MethodologyAcknowledged}",
            $"Methodology coverage: {draft.MethodologyCoverageStatus ?? "not_applicable"}",
            $"Methodology summary: {draft.MethodologySummary ?? "(none)"}",
        };
        var materialization = TaskGraphAcceptanceContractMaterializationGuard.Evaluate(draft);
        lines.Add($"Acceptance contract materialization: {materialization.State}");
        lines.Add($"Acceptance contract materialization failures: {materialization.FailureCount}");
        lines.Add($"Explicit acceptance contracts: {materialization.ExplicitContractCount}");
        lines.Add($"Synthesized minimum acceptance contracts: {materialization.SynthesizedMinimumContractCount}");
        lines.Add($"Materialization next action: {materialization.RecommendedAction}");
        lines.AddRange(draft.Tasks.Select(task =>
        {
            var projection = materialization.Tasks.First(item => string.Equals(item.TaskId, task.TaskId, StringComparison.Ordinal));
            return $"- {task.TaskId}: deps={(task.Dependencies.Count == 0 ? "(none)" : string.Join(", ", task.Dependencies))}; contract={task.AcceptanceContract?.ContractId ?? "(none)"}; contract_source={projection.ProjectionSource}; materialization={projection.State}; reason={projection.ReasonCode}";
        }));
        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult InspectDispatch()
    {
        var projection = dispatchProjectionService.Build(taskGraphService.Load(), devLoopService.GetSession(), systemConfig.MaxParallelTasks);
        return OperatorCommandResult.Success(
            $"Dispatch: {projection.State}",
            $"Summary: {projection.Summary}",
            $"Idle reason: {dispatchProjectionService.DescribeIdleReason(projection.IdleReason)} [{projection.IdleReason}]",
            $"Next task: {projection.NextTaskId ?? "(none)"}",
            $"Mode C/D entry first blocked task: {projection.FirstBlockedTaskId ?? "(none)"}",
            $"Mode C/D entry first blocker: {projection.FirstBlockingCheckId ?? "(none)"}",
            $"Mode C/D entry first blocker action: {projection.FirstBlockingCheckRequiredAction ?? "(none)"}",
            $"Mode C/D entry first blocker command: {projection.FirstBlockingCheckRequiredCommand ?? "(none)"}",
            $"Mode C/D entry next command: {projection.RecommendedNextCommand ?? "(none)"}",
            $"Auto continue on approve: {projection.AutoContinueOnApprove}");
    }
}
