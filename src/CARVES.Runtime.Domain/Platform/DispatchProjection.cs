namespace Carves.Runtime.Domain.Platform;

public sealed record DispatchProjection(
    string State,
    string Summary,
    string IdleReason,
    string? NextTaskId,
    int ReadyTaskCount,
    int ActiveWorkerCount,
    int MaxWorkerCount,
    bool AutoContinueOnApprove,
    int AcceptanceContractGapCount = 0,
    int PlanRequiredBlockCount = 0,
    int WorkspaceRequiredBlockCount = 0,
    string? FirstBlockedTaskId = null,
    string? FirstBlockingCheckId = null,
    string? FirstBlockingCheckSummary = null,
    string? FirstBlockingCheckRequiredAction = null,
    string? FirstBlockingCheckRequiredCommand = null,
    string? RecommendedNextAction = null,
    string? RecommendedNextCommand = null);
