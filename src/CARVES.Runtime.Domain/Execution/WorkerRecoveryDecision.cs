using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerRecoveryDecision
{
    public WorkerRecoveryAction Action { get; init; } = WorkerRecoveryAction.None;

    public string ReasonCode { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public RuntimeActionability Actionability { get; init; } = RuntimeActionability.WorkerActionable;

    public DateTimeOffset? RetryNotBefore { get; init; }

    public string? AlternateBackendId { get; init; }

    public string? AlternateProviderId { get; init; }

    public bool AutoApplied { get; init; }
}
