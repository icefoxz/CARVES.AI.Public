namespace Carves.Runtime.Domain.Failures;

public sealed class FailureReport
{
    public string SchemaVersion { get; init; } = "1.0";

    public string Id { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public string? CardId { get; init; }

    public string? TaskId { get; init; }

    public string Repo { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string? Worktree { get; init; }

    public string? Provider { get; init; }

    public string? ModelProfile { get; init; }

    public string Objective { get; init; } = string.Empty;

    public FailureInputSummary InputSummary { get; init; } = new();

    public FailureExecutionSummary Execution { get; init; } = new();

    public FailureResultSummary Result { get; init; } = new();

    public FailureDetails Failure { get; init; } = new();

    public FailureAttribution Attribution { get; init; } = new();

    public FailureReviewSummary Review { get; init; } = new();

    public FailureContextSnapshot ContextSnapshot { get; init; } = new();
}

public sealed class FailureInputSummary
{
    public IReadOnlyList<string> FilesInvolved { get; init; } = Array.Empty<string>();

    public string EstimatedScope { get; init; } = "unknown";
}

public sealed class FailureExecutionSummary
{
    public int PatchFiles { get; init; }

    public int PatchLines { get; init; }

    public IReadOnlyList<string> CommandsRun { get; init; } = Array.Empty<string>();

    public int DurationSeconds { get; init; }
}

public sealed class FailureResultSummary
{
    public string Status { get; init; } = string.Empty;

    public string StopReason { get; init; } = string.Empty;
}

public sealed class FailureDetails
{
    public FailureType Type { get; init; } = FailureType.Unknown;

    public string? Lane { get; init; }

    public string? ReasonCode { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? Details { get; init; }

    public string? StackTrace { get; init; }

    public string? NextAction { get; init; }
}

public sealed class FailureAttribution
{
    public FailureAttributionLayer Layer { get; init; } = FailureAttributionLayer.Environment;

    public double Confidence { get; init; }

    public string? Notes { get; init; }
}

public sealed class FailureReviewSummary
{
    public bool Required { get; init; }

    public bool Rejected { get; init; }

    public string? Reason { get; init; }
}

public sealed class FailureContextSnapshot
{
    public string State { get; init; } = string.Empty;

    public int PreviousFailures { get; init; }

    public int RetryCount { get; init; }
}
