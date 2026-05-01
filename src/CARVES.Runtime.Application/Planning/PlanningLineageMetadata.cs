using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public static class PlanningLineageMetadata
{
    public const string PlanningSlotIdKey = "planning_slot_id";
    public const string ActivePlanningCardIdKey = "active_planning_card_id";
    public const string SourceIntentDraftIdKey = "source_intent_draft_id";
    public const string SourceCandidateCardIdKey = "source_candidate_card_id";
    public const string FormalPlanningStateKey = "formal_planning_state";

    public static IReadOnlyDictionary<string, string> Merge(IReadOnlyDictionary<string, string> metadata, PlanningLineage? lineage)
    {
        var merged = new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        if (lineage is null)
        {
            merged.Remove(PlanningSlotIdKey);
            merged.Remove(ActivePlanningCardIdKey);
            merged.Remove(SourceIntentDraftIdKey);
            merged.Remove(SourceCandidateCardIdKey);
            merged.Remove(FormalPlanningStateKey);
            return merged;
        }

        merged[PlanningSlotIdKey] = lineage.PlanningSlotId;
        merged[ActivePlanningCardIdKey] = lineage.ActivePlanningCardId;
        merged[SourceIntentDraftIdKey] = lineage.SourceIntentDraftId;
        if (string.IsNullOrWhiteSpace(lineage.SourceCandidateCardId))
        {
            merged.Remove(SourceCandidateCardIdKey);
        }
        else
        {
            merged[SourceCandidateCardIdKey] = lineage.SourceCandidateCardId;
        }

        merged[FormalPlanningStateKey] = ToSnakeCase(lineage.FormalPlanningState);
        return merged;
    }

    public static PlanningLineage? TryRead(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null
            || !metadata.TryGetValue(PlanningSlotIdKey, out var planningSlotId)
            || string.IsNullOrWhiteSpace(planningSlotId)
            || !metadata.TryGetValue(ActivePlanningCardIdKey, out var activePlanningCardId)
            || string.IsNullOrWhiteSpace(activePlanningCardId)
            || !metadata.TryGetValue(SourceIntentDraftIdKey, out var sourceIntentDraftId)
            || string.IsNullOrWhiteSpace(sourceIntentDraftId))
        {
            return null;
        }

        metadata.TryGetValue(SourceCandidateCardIdKey, out var sourceCandidateCardId);
        metadata.TryGetValue(FormalPlanningStateKey, out var formalPlanningState);
        return new PlanningLineage
        {
            PlanningSlotId = planningSlotId.Trim(),
            ActivePlanningCardId = activePlanningCardId.Trim(),
            SourceIntentDraftId = sourceIntentDraftId.Trim(),
            SourceCandidateCardId = string.IsNullOrWhiteSpace(sourceCandidateCardId) ? null : sourceCandidateCardId.Trim(),
            FormalPlanningState = ParseState(formalPlanningState),
        };
    }

    private static FormalPlanningState ParseState(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FormalPlanningState.Planning;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim();
        return Enum.TryParse<FormalPlanningState>(normalized, ignoreCase: true, out var parsed)
            ? parsed
            : FormalPlanningState.Planning;
    }

    private static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
