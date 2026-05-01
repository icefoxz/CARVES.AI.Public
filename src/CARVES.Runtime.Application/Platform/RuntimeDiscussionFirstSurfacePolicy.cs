using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.Application.Platform;

internal static class RuntimeDiscussionFirstSurfacePolicy
{
    private static readonly RuntimeInteractionActionSurface[] SafeMenu =
    [
        new()
        {
            ActionId = "view_pilot_status",
            Label = "View pilot status",
            Kind = "read_only",
            Command = "carves pilot status --json",
            Summary = "Re-read the current Runtime stage without creating intent, card, taskgraph, or worker truth.",
        },
        new()
        {
            ActionId = "open_dashboard",
            Label = "Open dashboard",
            Kind = "read_only",
            Command = "carves dashboard --text",
            Summary = "Inspect current Runtime and task posture without entering planning or execution.",
        },
        new()
        {
            ActionId = "continue_discussion",
            Label = "Continue discussion",
            Kind = "read_only",
            Command = "carves discuss context",
            Summary = "Stay in ordinary discussion and clarify project purpose, scope, and whether engineering work is actually requested.",
        },
        new()
        {
            ActionId = "project_brief_preview",
            Label = "Project brief preview",
            Kind = "preview",
            Command = "carves discuss brief-preview",
            Summary = "Use ordinary discussion to draft a non-durable brief preview of purpose, first proof target, boundaries, risks, and open questions without writing PROJECT.md or creating planning truth.",
        },
    ];

    private static readonly string[] ForbiddenAutoActions =
    [
        "carves intent draft",
        "carves intent draft --persist",
        "carves plan init [candidate-card-id]",
        "carves plan export-card <json-path>",
        "carves approve-card <card-id> <reason...>",
        "carves create-taskgraph-draft <json-path>",
        "carves approve-taskgraph-draft <draft-id> <reason...>",
        "carves task run <task-id>",
        "carves approve-review <task-id> <reason...>",
    ];

    public static bool IsDiscussionFirstStage(string? stageId)
    {
        return string.Equals(stageId, "intent_capture", StringComparison.OrdinalIgnoreCase)
               || string.Equals(stageId, "ready_for_new_intent", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDiscussionFirstCommand(string? command)
    {
        return string.Equals(command, "carves discuss context", StringComparison.OrdinalIgnoreCase)
               || string.Equals(command, "carves discuss brief-preview", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<RuntimeInteractionActionSurface> BuildSafeMenu()
    {
        return SafeMenu;
    }

    public static IReadOnlyList<string> BuildForbiddenAutoActions()
    {
        return ForbiddenAutoActions;
    }
}
