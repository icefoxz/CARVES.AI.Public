using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed record ActivePlanningSlotProjection(
    string SlotId,
    string State,
    bool CanInitializeFormalPlanning,
    string? ActivePlanningCardId,
    string? PlanHandle,
    string ConflictReason,
    string RemediationAction);

public static class ActivePlanningSlotProjectionResolver
{
    public const string PrimaryFormalPlanningSlotId = "primary_formal_planning";

    public static ActivePlanningSlotProjection Resolve(IntentDiscoveryStatus status, FormalPlanningPacket? packet)
    {
        if (packet is not null)
        {
            if (packet.FormalPlanningState == FormalPlanningState.Closed)
            {
                return new ActivePlanningSlotProjection(
                    packet.PlanningSlotId,
                    "closed_historical",
                    true,
                    packet.PlanningCardId,
                    packet.PlanHandle,
                    string.Empty,
                    $"Plan handle '{packet.PlanHandle}' is closed; run `plan init [candidate-card-id]` if a new bounded planning slice is required.");
            }

            return BuildOccupiedProjection(packet.PlanningSlotId, packet.PlanningCardId, packet.PlanHandle, "occupied_by_packet");
        }

        var activePlanningCard = status.Draft?.ActivePlanningCard;
        if (activePlanningCard is not null)
        {
            return BuildOccupiedProjection(
                activePlanningCard.PlanningSlotId,
                activePlanningCard.PlanningCardId,
                FormalPlanningPacketService.BuildPlanHandle(activePlanningCard),
                "occupied_by_active_card");
        }

        if (status.Draft is null)
        {
            return new ActivePlanningSlotProjection(
                PrimaryFormalPlanningSlotId,
                "no_intent_draft",
                false,
                null,
                null,
                string.Empty,
                "Run `intent draft --persist` before `plan init`; no formal planning slot can be initialized without an intent draft.");
        }

        var canInitialize = status.Draft.FormalPlanningState == FormalPlanningState.PlanInitRequired;
        return new ActivePlanningSlotProjection(
            PrimaryFormalPlanningSlotId,
            canInitialize ? "empty_ready_to_initialize" : "empty_discussion_only",
            canInitialize,
            null,
            null,
            string.Empty,
            canInitialize
                ? "Run `plan init [candidate-card-id]` to occupy the single active formal planning slot."
                : "Keep the conversation in guided planning until one candidate is grounded or ready_to_plan, then run `plan init [candidate-card-id]`.");
    }

    public static string BuildConflictExceptionMessage(ActivePlanningCard activePlanningCard)
    {
        var projection = BuildOccupiedProjection(
            activePlanningCard.PlanningSlotId,
            activePlanningCard.PlanningCardId,
            FormalPlanningPacketService.BuildPlanHandle(activePlanningCard),
            "occupied_by_active_card");
        return $"{projection.ConflictReason} {projection.RemediationAction}";
    }

    private static ActivePlanningSlotProjection BuildOccupiedProjection(
        string slotId,
        string planningCardId,
        string planHandle,
        string state)
    {
        return new ActivePlanningSlotProjection(
            string.IsNullOrWhiteSpace(slotId) ? PrimaryFormalPlanningSlotId : slotId,
            state,
            false,
            planningCardId,
            planHandle,
            $"Formal planning slot '{(string.IsNullOrWhiteSpace(slotId) ? PrimaryFormalPlanningSlotId : slotId)}' is already occupied by active planning card '{planningCardId}'; opening another active planning card is rejected.",
            $"Run `plan status` and continue plan handle '{planHandle}'; export, complete, or intentionally invalidate the current active planning card before running another `plan init`.");
    }
}
