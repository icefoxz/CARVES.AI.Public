namespace Carves.Audit.Core;

public sealed record AuditSummaryResult(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string ConfidencePosture,
    int EventCount,
    AuditGuardSummary Guard,
    AuditHandoffSummary Handoff);

public sealed record AuditGuardSummary(
    string InputPath,
    string InputStatus,
    int TotalCount,
    int AllowCount,
    int ReviewCount,
    int BlockCount,
    IReadOnlyDictionary<string, int> SourceCounts,
    string? LatestRunId,
    GuardDecisionDiagnostics Diagnostics);

public sealed record AuditHandoffSummary(
    int SuppliedPacketCount,
    int LoadedPacketCount,
    int InputErrorCount,
    IReadOnlyDictionary<string, int> ResumeStatusCounts,
    IReadOnlyDictionary<string, int> ConfidenceCounts,
    string? LatestHandoffId,
    IReadOnlyList<AuditHandoffInputSummary> Inputs);

public sealed record AuditHandoffInputSummary(
    string InputPath,
    string InputStatus,
    string? HandoffId,
    string? ResumeStatus,
    string? Confidence,
    IReadOnlyList<AuditInputDiagnostic> Diagnostics);

public sealed record AuditTimelineResult(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string ConfidencePosture,
    int EventCount,
    IReadOnlyList<AuditEvent> Events);

public sealed record AuditEvent(
    string EventType,
    string SourceProduct,
    string SourcePath,
    string SourceSchemaVersion,
    string SubjectId,
    DateTimeOffset OccurredAtUtc,
    string Status,
    string Confidence,
    string? Summary,
    IReadOnlyList<string> Refs);

public sealed record AuditExplainResult(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string Id,
    bool Found,
    bool Ambiguous,
    string ConfidencePosture,
    IReadOnlyList<AuditExplainMatch> Matches);

public sealed record AuditExplainMatch(
    string SourceProduct,
    string SourcePath,
    string SubjectId,
    DateTimeOffset OccurredAtUtc,
    string Status,
    string? Summary,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> ContextRefs,
    IReadOnlyList<AuditExplainIssue> Issues);

public sealed record AuditExplainIssue(
    string? RuleId,
    string? Severity,
    string? Message,
    string? FilePath,
    string? EvidenceRef);

public sealed record ShieldEvidenceDocument(
    string SchemaVersion,
    string EvidenceId,
    DateTimeOffset GeneratedAtUtc,
    string ModeHint,
    int? SampleWindowDays,
    ShieldEvidenceRepository Repository,
    ShieldEvidencePrivacy Privacy,
    ShieldEvidenceDimensions Dimensions,
    ShieldEvidenceProvenance Provenance);

public sealed record ShieldEvidenceRepository(
    string Host,
    string Visibility,
    string? DefaultBranch,
    string? CommitSha);

public sealed record ShieldEvidencePrivacy(
    bool SourceIncluded,
    bool RawDiffIncluded,
    bool PromptIncluded,
    bool SecretsIncluded,
    bool RedactionApplied,
    string UploadIntent);

public sealed record ShieldEvidenceDimensions(
    ShieldGuardEvidence Guard,
    ShieldHandoffEvidence Handoff,
    ShieldAuditEvidence Audit);

public sealed record ShieldGuardEvidence(
    bool Enabled,
    ShieldGuardPolicyEvidence? Policy,
    ShieldGuardCiEvidence? Ci,
    ShieldGuardDecisionsEvidence? Decisions,
    IReadOnlyList<ShieldProofRef>? Proofs);

public sealed record ShieldGuardPolicyEvidence(
    bool Present,
    string? Path,
    bool? SchemaValid,
    int? SchemaVersion,
    string? PolicyId,
    IReadOnlyList<string>? EffectiveProtectedPathPrefixes,
    string? ProtectedPathAction,
    string? OutsideAllowedAction,
    bool? FailClosed,
    bool? ReviewIsPassing,
    bool? EmitEvidence,
    bool? ChangeBudgetPresent,
    bool? DependencyPolicyPresent,
    bool? SourceTestRulePresent,
    bool? MixedFeatureRefactorRulePresent);

public sealed record ShieldGuardCiEvidence(
    bool Detected,
    string? Provider,
    IReadOnlyList<string>? WorkflowPaths,
    bool? GuardCheckCommandDetected,
    bool? FailsOnReviewOrBlock);

public sealed record ShieldGuardDecisionsEvidence(
    bool Present,
    int? WindowDays,
    int AllowCount,
    int ReviewCount,
    int BlockCount,
    int? UnresolvedReviewCount,
    int? UnresolvedBlockCount);

public sealed record ShieldHandoffEvidence(
    bool Enabled,
    ShieldHandoffPacketsEvidence? Packets,
    ShieldLatestHandoffPacketEvidence? LatestPacket,
    ShieldHandoffContinuityEvidence? Continuity);

public sealed record ShieldHandoffPacketsEvidence(
    bool Present,
    int Count,
    int? WindowDays);

public sealed record ShieldLatestHandoffPacketEvidence(
    bool SchemaValid,
    int AgeDays,
    bool? RepoOrientationFresh,
    bool? TargetRepoMatches,
    bool CurrentObjectivePresent,
    bool RemainingWorkPresent,
    bool MustNotRepeatPresent,
    int CompletedFactsWithEvidenceCount,
    int DecisionRefsCount,
    string? Confidence);

public sealed record ShieldHandoffContinuityEvidence(
    int SessionSwitchCount,
    int SessionSwitchesWithPacket,
    int StalePacketCount);

public sealed record ShieldAuditEvidence(
    bool Enabled,
    ShieldAuditLogEvidence? Log,
    ShieldAuditRecordsEvidence? Records,
    ShieldAuditCoverageEvidence? Coverage,
    ShieldAuditReportsEvidence? Reports);

public sealed record ShieldAuditLogEvidence(
    bool Present,
    bool Readable,
    bool SchemaSupported,
    bool AppendOnlyClaimed,
    bool IntegrityCheckPassed);

public sealed record ShieldAuditRecordsEvidence(
    int RecordCount,
    int MalformedRecordCount,
    int FutureSchemaRecordCount,
    int OversizedRecordCount,
    DateTimeOffset? EarliestRecordedAtUtc,
    DateTimeOffset? LatestRecordedAtUtc,
    int RecordsWithRuleIdCount,
    int RecordsWithEvidenceCount);

public sealed record ShieldAuditCoverageEvidence(
    int BlockDecisionCount,
    int BlockExplainCoveredCount,
    int ReviewDecisionCount,
    int ReviewExplainCoveredCount);

public sealed record ShieldAuditReportsEvidence(
    bool SummaryGeneratedInWindow,
    bool ChangeReportGeneratedInWindow,
    bool FailurePatternDistributionPresent);

public sealed record ShieldEvidenceProvenance(
    string Producer,
    string ProducerVersion,
    string GeneratedBy,
    string Source,
    IReadOnlyList<ShieldEvidenceInputArtifact> InputArtifacts,
    IReadOnlyList<string> Warnings);

public sealed record ShieldEvidenceInputArtifact(
    string Kind,
    string Path,
    string Sha256,
    long Size,
    string InputStatus);

public sealed record ShieldProofRef(
    string Kind,
    string Ref);
