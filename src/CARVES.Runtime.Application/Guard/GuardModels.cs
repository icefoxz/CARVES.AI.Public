namespace Carves.Runtime.Application.Guard;

public enum GuardPolicyAction
{
    Allow,
    Review,
    Block,
}

public enum GuardFileChangeStatus
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Unknown,
}

public enum GuardSeverity
{
    Review,
    Block,
}

public enum GuardDecisionOutcome
{
    Allow,
    Review,
    Block,
}

public sealed record GuardDiffInput(
    string RepositoryRoot,
    string BaseRef,
    string? HeadRef,
    string PolicyPath,
    string? DiffText,
    IReadOnlyList<GuardChangedFileInput> ChangedFiles,
    string InvocationId,
    string SourceTool);

public sealed record GuardChangedFileInput(
    string Path,
    string? OldPath,
    GuardFileChangeStatus Status,
    int Additions,
    int Deletions,
    bool IsBinary,
    bool WasUntracked);

public sealed record GuardPolicySnapshot(
    int SchemaVersion,
    string PolicyId,
    string? Description,
    GuardPathPolicy PathPolicy,
    GuardChangeBudget ChangeBudget,
    GuardDependencyPolicy DependencyPolicy,
    GuardChangeShapePolicy ChangeShape,
    GuardDecisionPolicy Decision);

public sealed record GuardPathPolicy(
    bool CaseSensitive,
    IReadOnlyList<string> AllowedPathPrefixes,
    IReadOnlyList<string> ProtectedPathPrefixes,
    GuardPolicyAction OutsideAllowedAction,
    GuardPolicyAction ProtectedPathAction);

public sealed record GuardChangeBudget(
    int MaxChangedFiles,
    int? MaxTotalAdditions,
    int? MaxTotalDeletions,
    int? MaxFileAdditions,
    int? MaxFileDeletions,
    int? MaxRenames);

public sealed record GuardDependencyPolicy(
    IReadOnlyList<string> ManifestPaths,
    IReadOnlyList<string> LockfilePaths,
    GuardPolicyAction ManifestWithoutLockfileAction,
    GuardPolicyAction LockfileWithoutManifestAction,
    GuardPolicyAction NewDependencyAction);

public sealed record GuardChangeShapePolicy(
    bool AllowRenameWithContentChange,
    bool AllowDeleteWithoutReplacement,
    IReadOnlyList<string> GeneratedPathPrefixes,
    GuardPolicyAction GeneratedPathAction,
    GuardPolicyAction MixedFeatureAndRefactorAction,
    bool RequireTestsForSourceChanges,
    IReadOnlyList<string> SourcePathPrefixes,
    IReadOnlyList<string> TestPathPrefixes,
    GuardPolicyAction MissingTestsAction);

public sealed record GuardDecisionPolicy(
    bool FailClosed,
    GuardPolicyAction DefaultOutcome,
    bool ReviewIsPassing,
    bool EmitEvidence);

public sealed record GuardDiffContext(
    string RepositoryRoot,
    string BaseRef,
    string? HeadRef,
    GuardPolicySnapshot Policy,
    IReadOnlyList<GuardChangedFile> ChangedFiles,
    GuardPatchStats PatchStats,
    string ScopeSource,
    string? RuntimeTaskId,
    IReadOnlyList<GuardDiffDiagnostic>? Diagnostics = null);

public sealed record GuardDiffDiagnostic(
    string RuleId,
    GuardSeverity Severity,
    string Message,
    string Evidence);

public sealed record GuardChangedFile(
    string Path,
    string? OldPath,
    GuardFileChangeStatus Status,
    int Additions,
    int Deletions,
    bool IsBinary,
    bool WasUntracked,
    bool MatchesAllowedPath,
    bool MatchesProtectedPath,
    string? MatchedAllowedPrefix,
    string? MatchedProtectedPrefix);

public sealed record GuardPatchStats(
    int ChangedFileCount,
    int AddedFileCount,
    int ModifiedFileCount,
    int DeletedFileCount,
    int RenamedFileCount,
    int BinaryFileCount,
    int TotalAdditions,
    int TotalDeletions);

public sealed record GuardViolation(
    string RuleId,
    GuardSeverity Severity,
    string Message,
    string? FilePath,
    string Evidence,
    string EvidenceRef);

public sealed record GuardWarning(
    string RuleId,
    string Message,
    string? FilePath,
    string Evidence,
    string EvidenceRef);

public sealed record GuardDecision(
    string RunId,
    GuardDecisionOutcome Outcome,
    string PolicyId,
    string Summary,
    IReadOnlyList<GuardViolation> Violations,
    IReadOnlyList<GuardWarning> Warnings,
    IReadOnlyList<string> EvidenceRefs,
    bool RequiresRuntimeTaskTruth);

public sealed record GuardCheckResult(
    GuardDecision Decision,
    GuardDiffContext? Context);

public sealed record GuardRuntimeExecutionResult(
    string TaskId,
    bool Accepted,
    string Outcome,
    string TaskStatus,
    string Summary,
    string? FailureKind,
    string? RunId,
    string? ExecutionRunId,
    IReadOnlyList<string> ChangedFiles,
    string NextAction);

public sealed record GuardRunResult(
    GuardDecision Decision,
    GuardRuntimeExecutionResult Execution,
    GuardDecision DisciplineDecision);

public sealed record GuardDecisionAuditRecord(
    int SchemaVersion,
    string RunId,
    DateTimeOffset RecordedAtUtc,
    string Source,
    string Outcome,
    string PolicyId,
    string Summary,
    bool RequiresRuntimeTaskTruth,
    string? TaskId,
    string? ExecutionOutcome,
    string? ExecutionFailureKind,
    IReadOnlyList<string> ChangedFiles,
    GuardDecisionAuditPatchStats? PatchStats,
    IReadOnlyList<GuardDecisionAuditIssue> Violations,
    IReadOnlyList<GuardDecisionAuditIssue> Warnings,
    IReadOnlyList<string> EvidenceRefs);

public sealed record GuardDecisionAuditPatchStats(
    int ChangedFileCount,
    int AddedFileCount,
    int ModifiedFileCount,
    int DeletedFileCount,
    int RenamedFileCount,
    int BinaryFileCount,
    int TotalAdditions,
    int TotalDeletions);

public sealed record GuardDecisionAuditIssue(
    string RuleId,
    string Severity,
    string Message,
    string? FilePath,
    string Evidence,
    string EvidenceRef);

public sealed record GuardDecisionReadDiagnostics(
    int RequestedLimit,
    int EffectiveLimit,
    int TotalLineCount,
    int EmptyLineCount,
    int LoadedRecordCount,
    int ReturnedRecordCount,
    int SkippedRecordCount,
    int MalformedRecordCount,
    int FutureVersionRecordCount,
    int MaxStoredLineCount)
{
    public bool IsDegraded => SkippedRecordCount > 0;
}

public sealed record GuardDecisionReadResult(
    IReadOnlyList<GuardDecisionAuditRecord> Records,
    GuardDecisionReadDiagnostics Diagnostics);

public sealed record GuardDecisionFindResult(
    GuardDecisionAuditRecord? Record,
    GuardDecisionReadDiagnostics Diagnostics);

public sealed record GuardAuditSnapshot(
    string AuditPath,
    IReadOnlyList<GuardDecisionAuditRecord> Decisions,
    GuardDecisionReadDiagnostics Diagnostics);

public sealed record GuardReportSnapshot(
    string AuditPath,
    GuardPolicyLoadResult PolicyLoad,
    IReadOnlyList<GuardDecisionAuditRecord> RecentDecisions,
    GuardReportPosture Posture,
    GuardDecisionReadDiagnostics Diagnostics);

public sealed record GuardReportPosture(
    string Status,
    string Summary,
    int TotalCount,
    int AllowCount,
    int ReviewCount,
    int BlockCount,
    string? LatestRunId);

public sealed record GuardExplainResult(
    string RunId,
    GuardDecisionAuditRecord? Record,
    GuardDecisionReadDiagnostics Diagnostics)
{
    public bool Found => Record is not null;
}

public sealed record GuardPolicyLoadResult(
    bool IsValid,
    GuardPolicySnapshot? Policy,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static GuardPolicyLoadResult Valid(GuardPolicySnapshot policy)
    {
        return new GuardPolicyLoadResult(true, policy, null, null);
    }

    public static GuardPolicyLoadResult Invalid(string errorCode, string errorMessage)
    {
        return new GuardPolicyLoadResult(false, null, errorCode, errorMessage);
    }
}
