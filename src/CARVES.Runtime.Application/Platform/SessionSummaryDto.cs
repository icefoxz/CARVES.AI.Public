using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform;

public sealed record SessionSummaryDto(
    string RepoId,
    RuntimeSessionStatus? Status,
    string Actionability,
    string? StopReason,
    string? WaitingReason,
    int PlannerRound,
    int ActiveWorkers,
    IReadOnlyList<string> ActiveTasks);
