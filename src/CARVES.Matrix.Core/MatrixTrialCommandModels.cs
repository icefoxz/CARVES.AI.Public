namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private sealed record TrialCommandResult(
        string SchemaVersion,
        string Command,
        string Status,
        bool Offline,
        bool ServerSubmission,
        string? WorkspaceRoot,
        string? EvidenceRoot,
        string BundleRoot,
        string? AgentRunDirectory,
        string? PromptPath,
        string? AgentReportPath,
        string VerifyCommand,
        IReadOnlyList<string> Steps,
        IReadOnlyList<string> NonClaims,
        IReadOnlyList<TrialDiagnosticReadback> Diagnostics,
        TrialCollectionReadback? Collection,
        TrialVerificationReadback? Verification,
        TrialLocalScoreReadback? LocalScore,
        TrialResultCardReadback? ResultCard);

    private sealed record TrialCollectionReadback(
        string LocalCollectionStatus,
        IReadOnlyList<string> MissingRequiredArtifacts,
        IReadOnlyList<string> FailureReasons,
        string DiffScopeSummaryPath,
        string TestEvidencePath,
        string TrialResultPath);

    private sealed record TrialVerificationReadback(
        string Status,
        string VerificationPosture,
        int ExitCode,
        bool TrialArtifactsVerified,
        IReadOnlyList<string> ReasonCodes);

    private sealed record TrialLocalScoreReadback(
        string ProfileId,
        string ProfileVersion,
        string ProfileName,
        string ScoreStatus,
        int? AggregateScore,
        int MaxScore,
        IReadOnlyList<TrialLocalDimensionScoreReadback> Dimensions,
        IReadOnlyList<string> ReasonCodes);

    private sealed record TrialLocalDimensionScoreReadback(
        string Dimension,
        int? Score,
        int MaxScore,
        string Level,
        IReadOnlyList<string> ReasonCodes,
        string Explanation);

    private sealed record TrialResultCardReadback(
        string Title,
        string? CardPath,
        IReadOnlyList<string> EvidenceRefs,
        IReadOnlyList<string> Labels,
        string Markdown);

    private sealed record TrialDiagnosticReadback(
        string Code,
        string Category,
        string Severity,
        string Message,
        string EvidenceRef,
        string? CommandRef,
        string NextStep,
        IReadOnlyList<string> ReasonCodes);

    private sealed record TrialCommandFailureResult(
        string SchemaVersion,
        string Command,
        string Status,
        bool Offline,
        bool ServerSubmission,
        IReadOnlyList<TrialDiagnosticReadback> Diagnostics);
}
