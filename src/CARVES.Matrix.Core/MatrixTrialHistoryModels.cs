namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const string TrialHistoryEntrySchemaVersion = "matrix-agent-trial-local-history-entry.v0";
    private const string TrialHistoryRecordSchemaVersion = "matrix-agent-trial-local-history-record.v0";
    private const string TrialHistoryCompareSchemaVersion = "matrix-agent-trial-local-history-compare.v0";

    private sealed record TrialHistoryRecordResult(
        string SchemaVersion,
        string Command,
        string Status,
        bool Offline,
        bool ServerSubmission,
        string HistoryEntryRef,
        TrialHistoryEntry Entry,
        IReadOnlyList<string> NonClaims);

    private sealed record TrialHistoryCompareResult(
        string SchemaVersion,
        string Command,
        string Status,
        bool Offline,
        bool ServerSubmission,
        string BaselineRunId,
        string TargetRunId,
        string ComparisonMode,
        bool DirectComparable,
        IReadOnlyList<string> ReasonCodes,
        string Explanation,
        TrialHistoryScoreMovement AggregateScore,
        IReadOnlyList<TrialHistoryDimensionMovement> Dimensions,
        IReadOnlyList<string> NonClaims);

    private sealed record TrialHistoryEntry(
        string SchemaVersion,
        string RunId,
        string RecordedAt,
        string AuthorityMode,
        string VerificationStatus,
        bool MatrixVerified,
        TrialHistoryIdentity Identity,
        TrialHistoryScore Score,
        IReadOnlyList<string> EvidenceRefs,
        TrialHistoryArtifactAnchors ArtifactAnchors,
        IReadOnlyList<string> NonClaims);

    private sealed record TrialHistoryIdentity(
        string SuiteId,
        string PackId,
        string PackVersion,
        string TaskId,
        string TaskVersion,
        string PromptId,
        string PromptVersion,
        string ScoringProfileId,
        string ScoringProfileVersion);

    private sealed record TrialHistoryScore(
        string ScoreStatus,
        int? AggregateScore,
        int MaxScore,
        IReadOnlyList<TrialHistoryDimensionScore> Dimensions,
        IReadOnlyList<string> ReasonCodes);

    private sealed record TrialHistoryDimensionScore(
        string Dimension,
        int? Score,
        int MaxScore,
        string Level,
        IReadOnlyList<string> ReasonCodes);

    private sealed record TrialHistoryArtifactAnchors(
        string ManifestSha256,
        string TrialResultSha256);

    private sealed record TrialHistoryScoreMovement(
        int? Baseline,
        int? Target,
        int? Delta);

    private sealed record TrialHistoryDimensionMovement(
        string Dimension,
        int? Baseline,
        int? Target,
        int? Delta,
        string BaselineLevel,
        string TargetLevel);
}
