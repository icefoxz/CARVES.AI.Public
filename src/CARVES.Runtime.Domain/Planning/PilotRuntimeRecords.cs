namespace Carves.Runtime.Domain.Planning;

public sealed class AttachToTaskProofRecord
{
    public string SchemaVersion { get; init; } = "attach-to-task-proof.v1";

    public string ProofId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string? CardId { get; init; }

    public string RepoRoot { get; init; } = string.Empty;

    public string? RepoId { get; init; }

    public string? HostSessionId { get; init; }

    public bool RuntimeManifestPresent { get; init; }

    public bool AttachHandshakePresent { get; init; }

    public string? CardLifecycleState { get; init; }

    public string TaskStatus { get; init; } = string.Empty;

    public string DispatchState { get; init; } = string.Empty;

    public string? ExecutionRunId { get; init; }

    public string? ExecutionRunStatus { get; init; }

    public string? ResultStatus { get; init; }

    public string? ReviewVerdict { get; init; }

    public string? ReplanEntryId { get; init; }

    public IReadOnlyList<string> SuggestedTaskIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ExecutionMemoryIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EvidencePaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class PilotEvidenceRecord
{
    public string SchemaVersion { get; init; } = "pilot-evidence-record.v1";

    public string EvidenceId { get; init; } = string.Empty;

    public string RepoRoot { get; init; } = string.Empty;

    public string? RepoId { get; init; }

    public string? TaskId { get; init; }

    public string? CardId { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Observations { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> FrictionPoints { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> FailedExpectations { get; init; } = Array.Empty<string>();

    public IReadOnlyList<PilotFollowUpRecord> FollowUps { get; init; } = Array.Empty<PilotFollowUpRecord>();

    public IReadOnlyList<string> RelatedSuggestedTaskIds { get; init; } = Array.Empty<string>();

    public string? AttachProofId { get; init; }

    public string? AttachProofPath { get; init; }

    public string Status { get; init; } = "recorded";

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class PilotProblemIntakeRecord
{
    public string SchemaVersion { get; init; } = "pilot-problem-intake-record.v1";

    public string ProblemId { get; init; } = string.Empty;

    public string EvidenceId { get; init; } = string.Empty;

    public string RepoRoot { get; init; } = string.Empty;

    public string? RepoId { get; init; }

    public string? TaskId { get; init; }

    public string? CardId { get; init; }

    public string CurrentStageId { get; init; } = string.Empty;

    public string NextGovernedCommand { get; init; } = string.Empty;

    public string ProblemKind { get; init; } = string.Empty;

    public string Severity { get; init; } = "blocking";

    public string Summary { get; init; } = string.Empty;

    public string BlockedCommand { get; init; } = string.Empty;

    public int? CommandExitCode { get; init; }

    public string CommandOutput { get; init; } = string.Empty;

    public string StopTrigger { get; init; } = string.Empty;

    public IReadOnlyList<string> Observations { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AffectedPaths { get; init; } = Array.Empty<string>();

    public string RecommendedFollowUp { get; init; } = string.Empty;

    public string Status { get; init; } = "recorded";

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class PilotFollowUpRecord
{
    public string SchemaVersion { get; init; } = "pilot-follow-up.v1";

    public string Kind { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

public sealed class PilotCloseLoopRequest
{
    public string SchemaVersion { get; init; } = "pilot-close-loop-request.v1";

    public string TaskId { get; init; } = string.Empty;

    public string? ChangedFile { get; init; }

    public string? Summary { get; init; }

    public string? FailureMessage { get; init; }

    public string? ReviewReason { get; init; }

    public string? PilotEvidencePath { get; init; }
}

public sealed class PilotCloseLoopRecord
{
    public string SchemaVersion { get; init; } = "pilot-close-loop.v1";

    public string TaskId { get; init; } = string.Empty;

    public string ExecutionRunId { get; init; } = string.Empty;

    public string ChangedFile { get; init; } = string.Empty;

    public string ResultEnvelopePath { get; init; } = string.Empty;

    public string WorkerExecutionArtifactPath { get; init; } = string.Empty;

    public string EvidenceDirectory { get; init; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class TargetIgnoreDecisionRecordRequest
{
    public string SchemaVersion { get; init; } = "target-ignore-decision-record-request.v1";

    public string Decision { get; init; } = string.Empty;

    public bool AllEntries { get; init; }

    public IReadOnlyList<string> Entries { get; init; } = Array.Empty<string>();

    public string Reason { get; init; } = string.Empty;

    public string Operator { get; init; } = "operator";

    public string? PlanId { get; init; }
}

public sealed class AgentProblemFollowUpDecisionRecordRequest
{
    public string SchemaVersion { get; init; } = "agent-problem-follow-up-decision-record-request.v1";

    public string Decision { get; init; } = string.Empty;

    public bool AllCandidates { get; init; }

    public IReadOnlyList<string> CandidateIds { get; init; } = Array.Empty<string>();

    public string Reason { get; init; } = string.Empty;

    public string Operator { get; init; } = "operator";

    public string AcceptanceEvidence { get; init; } = string.Empty;

    public string ReadbackCommand { get; init; } = string.Empty;

    public string? PlanId { get; init; }
}
