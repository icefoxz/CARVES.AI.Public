namespace Carves.Runtime.Application.Platform;

public sealed record TaskSummaryDto(
    string TaskId,
    string Title,
    string Status,
    string TaskType,
    string ProposalSource);
