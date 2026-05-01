using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed record RuntimeFormalPlanningEntryProjection(
    string TriggerState,
    string Command,
    string RecommendedNextAction,
    string Summary);

public static class RuntimeFormalPlanningEntryProjectionResolver
{
    public static RuntimeFormalPlanningEntryProjection Resolve(
        IntentDiscoveryStatus status,
        FormalPlanningPacket? packet)
    {
        var state = packet?.FormalPlanningState
            ?? status.Draft?.FormalPlanningState
            ?? FormalPlanningState.Discuss;
        return Resolve(state, status, packet);
    }

    public static RuntimeFormalPlanningEntryProjection Resolve(
        FormalPlanningState state,
        IntentDiscoveryStatus status,
        FormalPlanningPacket? packet)
    {
        if (packet is not null && packet.FormalPlanningState == FormalPlanningState.Closed)
        {
            return new RuntimeFormalPlanningEntryProjection(
                "closed_historical",
                "plan init [candidate-card-id]",
                $"The prior formal planning packet is closed on plan handle {packet.PlanHandle}; run `plan init [candidate-card-id]` only for a new bounded slice.",
                $"Formal planning card {packet.PlanningCardId} in slot {packet.PlanningSlotId} is historical closed lineage, not a live active slot.");
        }

        if (packet is not null)
        {
            return new RuntimeFormalPlanningEntryProjection(
                "formal_planning_packet_available",
                "plan status",
                $"Continue the current formal planning packet through `plan status`; plan handle is {packet.PlanHandle}.",
                $"Formal planning already has one active planning card {packet.PlanningCardId} in slot {packet.PlanningSlotId}.");
        }

        if (status.Draft?.ActivePlanningCard is not null)
        {
            return new RuntimeFormalPlanningEntryProjection(
                "active_planning_card_present",
                "plan status",
                $"Continue the active planning card {status.Draft.ActivePlanningCard.PlanningCardId} through `plan status` before exporting durable card truth.",
                "Formal planning has already entered one active planning card; the next step is card/taskgraph lineage, not another planning slot.");
        }

        if (state == FormalPlanningState.PlanInitRequired)
        {
            return new RuntimeFormalPlanningEntryProjection(
                "plan_init_required",
                "plan init [candidate-card-id]",
                "Run `plan init [candidate-card-id]` before creating durable card, taskgraph, workspace, or Mode E handoff truth.",
                "Guided planning is ready for formal planning, but no active planning card exists yet.");
        }

        return new RuntimeFormalPlanningEntryProjection(
            "discussion_only",
            "plan init [candidate-card-id]",
            "When the conversation becomes durable planning, run `plan init [candidate-card-id]`; use `intent draft --persist` first if user intent is still missing.",
            "Runtime is in discussion-only posture; this projection is an entry trigger, not automatic truth mutation.");
    }
}
