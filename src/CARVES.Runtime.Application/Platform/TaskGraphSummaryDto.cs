namespace Carves.Runtime.Application.Platform;

public sealed record TaskGraphSummaryDto(
    string RepoId,
    int TotalTasks,
    int PendingTasks,
    int ReviewTasks,
    int CompletedTasks,
    IReadOnlyList<TaskSummaryDto> Tasks);
