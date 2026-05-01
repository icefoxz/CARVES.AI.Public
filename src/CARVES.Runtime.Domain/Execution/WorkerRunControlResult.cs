namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerRunControlResult
{
    public string BackendId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public bool Supported { get; init; }

    public bool Succeeded { get; init; }

    public string Summary { get; init; } = string.Empty;
}
