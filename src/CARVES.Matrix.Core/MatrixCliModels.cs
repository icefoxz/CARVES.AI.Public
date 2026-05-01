namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private sealed record MatrixVerifyResult(
        string SchemaVersion,
        string Status,
        string VerificationPosture,
        string ArtifactRoot,
        string VerificationMode,
        MatrixVerifyManifest Manifest,
        MatrixVerifyRequiredArtifacts RequiredArtifacts,
        MatrixVerifyTrialArtifacts TrialArtifacts,
        MatrixVerifyShieldEvaluation ShieldEvaluation,
        MatrixVerifyProofSummary Summary,
        MatrixVerifyTrustChainHardening TrustChainHardening,
        int IssueCount,
        IReadOnlyList<MatrixVerifyIssue> Issues)
    {
        public bool IsVerified => string.Equals(Status, "verified", StringComparison.Ordinal)
                                  && TrustChainHardening.GatesSatisfied;

        public int ExitCode => IsVerified ? 0 : 1;

        public IReadOnlyList<string> ReasonCodes => DistinctReasonCodes(Issues.Select(issue => issue.Code));
    }

    private sealed record MatrixVerifyManifest(
        string Path,
        string? Sha256,
        string VerificationPosture,
        int IssueCount);

    private sealed record MatrixVerifyRequiredArtifacts(
        int ExpectedCount,
        int PresentCount,
        int MissingCount,
        int MismatchCount);

    private sealed record MatrixVerifyTrialArtifacts(
        string Mode,
        bool Required,
        int ExpectedCount,
        int PresentCount,
        int MissingCount,
        int MismatchCount,
        int LooseFileCount,
        bool Verified);

    private sealed record MatrixVerifyShieldEvaluation(
        string Path,
        bool Present,
        bool ScoreVerified);

    private sealed record MatrixVerifyProofSummary(
        string Path,
        bool Present,
        bool Consistent);

    private sealed record MatrixVerifyTrustChainHardening(
        bool GatesSatisfied,
        string ComputedBy,
        IReadOnlyList<MatrixVerifyTrustChainGate> Gates);

    private sealed record MatrixVerifyTrustChainGate(
        string GateId,
        bool Satisfied,
        string Reason,
        IReadOnlyList<string> IssueCodes)
    {
        public IReadOnlyList<string> ReasonCodes => DistinctReasonCodes(IssueCodes);
    }

    private sealed record MatrixVerifyIssue(
        string Scope,
        string ArtifactKind,
        string Path,
        string Code,
        string? ExpectedValue,
        string? ActualValue)
    {
        public string ReasonCode => ClassifyIssueCode(Code);
    }

    private sealed record MatrixNativeProofStep(
        string StepId,
        string Command,
        int ExitCode,
        bool Passed,
        string? StdoutPath,
        string? StdoutPreview,
        string? StderrPreview)
    {
        public IReadOnlyList<string> ReasonCodes => Passed
            ? []
            : [$"native_{StepId}_failed", "native_proof_step_failed"];
    }

    private sealed record MatrixNativeProofStepCapture(
        MatrixNativeProofStep Step,
        string Stdout,
        string Stderr);

    private sealed record ScriptResult(
        int ExitCode,
        string Stdout,
        string Stderr,
        string Command,
        bool TimedOut = false,
        bool StdoutTruncated = false,
        bool StderrTruncated = false);
}
