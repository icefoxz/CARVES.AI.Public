namespace Carves.Runtime.Domain.Runtime;

public sealed class RuntimeFailureRecord
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string FailureId { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public string AttachedRepoRoot { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public RuntimeFailureType FailureType { get; init; } = RuntimeFailureType.WorkerExecutionFailure;

    public RuntimeFailureAction Action { get; init; } = RuntimeFailureAction.EscalateToOperator;

    public RuntimeSessionStatus SessionStatus { get; init; } = RuntimeSessionStatus.Idle;

    public int TickCount { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string Source { get; init; } = "runtime";

    public string? ExceptionType { get; init; }

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}
