using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult SupersedeCardTasks(string cardId, string reason)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return OperatorCommandResult.Failure("Card id is required.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return OperatorCommandResult.Failure($"Usage: supersede-card-tasks {cardId} <reason...>");
        }

        var graph = taskGraphService.Load();
        var tasksForCard = graph.ListTasks()
            .Where(task => string.Equals(task.CardId, cardId, StringComparison.Ordinal))
            .ToArray();
        if (tasksForCard.Length == 0)
        {
            return OperatorCommandResult.Failure($"Card {cardId} has no task lineage in the current task graph.");
        }

        var supersededTaskIds = taskGraphService.SupersedeCardTasks(cardId, reason);
        if (supersededTaskIds.Count == 0)
        {
            return OperatorCommandResult.Failure($"Card {cardId} has no non-finalized tasks to supersede.");
        }

        var session = devLoopService.ReconcileReviewBoundary() ?? devLoopService.GetSession();
        markdownSyncService.Sync(taskGraphService.Load(), session: session);

        var preservedTaskIds = tasksForCard
            .Where(task => task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Superseded or DomainTaskStatus.Discarded)
            .Select(task => task.TaskId)
            .Where(taskId => !supersededTaskIds.Contains(taskId, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(taskId => taskId, StringComparer.Ordinal)
            .ToArray();

        var lines = new List<string>
        {
            $"Superseded {supersededTaskIds.Count} non-finalized task(s) for {cardId}: {string.Join(", ", supersededTaskIds)}",
        };
        if (preservedTaskIds.Length > 0)
        {
            lines.Add($"Preserved finalized lineage: {string.Join(", ", preservedTaskIds)}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
