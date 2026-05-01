namespace Carves.Runtime.Domain.Execution;

public sealed class TaskResultEnvelope
{
    public string SchemaVersion { get; init; } = "1.0";

    public string TaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public string? PacketId { get; init; }

    public string? ExecutionRunId { get; init; }

    public string? WorkerRunId { get; init; }

    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string SubmissionAction { get; init; } = "submit_result";

    public string RequestedFollowUp { get; init; } = "none";

    public bool LifecycleWritebackRequested { get; init; }

    public TaskResultEnvelopeOutcome Outcome { get; init; } = new();

    public TaskResultEnvelopeArtifacts Artifacts { get; init; } = new();

    public IReadOnlyList<string> PlannerOwnedNextActions { get; init; } = ["review_task", "sync_state"];

    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class TaskResultEnvelopeOutcome
{
    public string Status { get; init; } = string.Empty;

    public string? FailureKind { get; init; }

    public bool Retryable { get; init; }

    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();
}

public sealed class TaskResultEnvelopeArtifacts
{
    public string? ResultEnvelopePath { get; init; }

    public string? WorkerExecutionArtifactPath { get; init; }

    public string? ProviderArtifactPath { get; init; }

    public string? SafetyArtifactPath { get; init; }

    public IReadOnlyList<string> EvidencePaths { get; init; } = Array.Empty<string>();
}
