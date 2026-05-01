namespace Carves.Audit.Core;

public sealed record AuditInputOptions(
    string WorkingDirectory,
    string GuardDecisionPath,
    bool GuardDecisionPathExplicit,
    IReadOnlyList<string> HandoffPacketPaths,
    bool HandoffPacketPathsExplicit = true);

public sealed record AuditInputSnapshot(
    GuardAuditInput Guard,
    IReadOnlyList<HandoffAuditInput> HandoffPackets)
{
    public bool HasUsableInput => Guard.Records.Count > 0 || HandoffPackets.Any(input => input.Packet is not null);

    public bool HasFatalInputError => Guard.IsFatal || HandoffPackets.Any(input => input.IsFatal);

    public bool IsDegraded => Guard.Diagnostics.IsDegraded
        || HandoffPackets.Any(input =>
            input.InputStatus is not "loaded" and not "missing");

    public string ConfidencePosture
    {
        get
        {
            if (HasFatalInputError)
            {
                return "input_error";
            }

            if (IsDegraded)
            {
                return "degraded";
            }

            return HasUsableInput ? "complete_for_supplied_inputs" : "empty";
        }
    }
}

public sealed record GuardAuditInput(
    string InputPath,
    string InputStatus,
    bool IsFatal,
    IReadOnlyList<GuardDecisionRecord> Records,
    GuardDecisionDiagnostics Diagnostics);

public sealed record GuardDecisionDiagnostics(
    int TotalLineCount,
    int EffectiveLineCount,
    int EmptyLineCount,
    int LoadedRecordCount,
    int SkippedRecordCount,
    int MalformedRecordCount,
    int FutureVersionRecordCount,
    int OversizedRecordCount,
    int MaxStoredLineCount,
    int MaxRecordByteCount,
    bool IsDegraded);

public sealed class GuardDecisionRecord
{
    public int SchemaVersion { get; init; }

    public string? RunId { get; init; }

    public DateTimeOffset RecordedAtUtc { get; init; }

    public string? Source { get; init; }

    public string? Outcome { get; init; }

    public string? PolicyId { get; init; }

    public string? Summary { get; init; }

    public bool RequiresRuntimeTaskTruth { get; init; }

    public string? TaskId { get; init; }

    public string? ExecutionOutcome { get; init; }

    public string? ExecutionFailureKind { get; init; }

    public IReadOnlyList<string> ChangedFiles { get; init; } = [];

    public GuardDecisionPatchStats? PatchStats { get; init; }

    public IReadOnlyList<GuardDecisionIssue> Violations { get; init; } = [];

    public IReadOnlyList<GuardDecisionIssue> Warnings { get; init; } = [];

    public IReadOnlyList<string> EvidenceRefs { get; init; } = [];
}

public sealed class GuardDecisionPatchStats
{
    public int ChangedFileCount { get; init; }

    public int AddedFileCount { get; init; }

    public int ModifiedFileCount { get; init; }

    public int DeletedFileCount { get; init; }

    public int RenamedFileCount { get; init; }

    public int BinaryFileCount { get; init; }

    public int TotalAdditions { get; init; }

    public int TotalDeletions { get; init; }
}

public sealed class GuardDecisionIssue
{
    public string? RuleId { get; init; }

    public string? Severity { get; init; }

    public string? Message { get; init; }

    public string? FilePath { get; init; }

    public string? Evidence { get; init; }

    public string? EvidenceRef { get; init; }
}

public sealed record HandoffAuditInput(
    string InputPath,
    string InputStatus,
    bool IsFatal,
    HandoffPacketRecord? Packet,
    IReadOnlyList<AuditInputDiagnostic> Diagnostics);

public sealed record HandoffPacketRecord(
    string SchemaVersion,
    string HandoffId,
    DateTimeOffset CreatedAtUtc,
    string? ResumeStatus,
    string? CurrentObjective,
    string? Confidence,
    int RemainingWorkCount,
    int CompletedFactsWithEvidenceCount,
    int BlockedReasonCount,
    int MustNotRepeatCount,
    int DecisionRefCount,
    int ContextRefCount,
    int EvidenceRefCount,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> ContextRefs);

public sealed record AuditInputDiagnostic(
    string Severity,
    string Code,
    string Message,
    string? Path);
