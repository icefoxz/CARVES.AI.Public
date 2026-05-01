namespace Carves.Runtime.Domain.Execution;

public sealed class ResultEnvelope
{
    public string SchemaVersion { get; init; } = "1.0";

    public string TaskId { get; init; } = string.Empty;

    public string? ExecutionRunId { get; init; }

    public string? ExecutionEvidencePath { get; init; }

    public int? CompletedStepCount { get; init; }

    public int? TotalStepCount { get; init; }

    public string Status { get; init; } = string.Empty;

    public ResultEnvelopeChanges Changes { get; init; } = new();

    public ResultEnvelopeValidation Validation { get; init; } = new();

    public ResultEnvelopeOutcome Result { get; init; } = new();

    public ResultEnvelopeFailure Failure { get; init; } = new();

    public ResultEnvelopeNextAction Next { get; init; } = new();

    public ExecutionTelemetry Telemetry { get; init; } = new();
}

public sealed class ResultEnvelopeChanges
{
    public IReadOnlyList<string> FilesModified { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> FilesAdded { get; init; } = Array.Empty<string>();

    public int LinesChanged { get; init; }
}

public sealed class ResultEnvelopeValidation
{
    public IReadOnlyList<string> CommandsRun { get; init; } = Array.Empty<string>();

    public string Build { get; init; } = "not_run";

    public string Tests { get; init; } = "not_run";
}

public sealed class ResultEnvelopeOutcome
{
    public string StopReason { get; init; } = "unknown";
}

public sealed class ResultEnvelopeFailure
{
    public string? Type { get; init; }

    public string? Message { get; init; }
}

public sealed class ResultEnvelopeNextAction
{
    public string? Suggested { get; init; }
}
