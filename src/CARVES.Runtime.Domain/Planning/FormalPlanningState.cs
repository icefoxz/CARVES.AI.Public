using System.Text.Json.Serialization;

namespace Carves.Runtime.Domain.Planning;

public enum FormalPlanningState
{
    Discuss,
    PlanInitRequired,
    Planning,
    PlanBound,
    ExecutionBound,
    ReviewBound,
    Closed,
}

public sealed class PlanningLineage
{
    [JsonPropertyName("planning_slot_id")]
    public string PlanningSlotId { get; init; } = string.Empty;

    [JsonPropertyName("active_planning_card_id")]
    public string ActivePlanningCardId { get; init; } = string.Empty;

    [JsonPropertyName("source_intent_draft_id")]
    public string SourceIntentDraftId { get; init; } = string.Empty;

    [JsonPropertyName("source_candidate_card_id")]
    public string? SourceCandidateCardId { get; init; }

    [JsonPropertyName("formal_planning_state")]
    public FormalPlanningState FormalPlanningState { get; init; } = FormalPlanningState.Discuss;
}
