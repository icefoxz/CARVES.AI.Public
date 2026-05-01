namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    internal sealed record MatrixNativeFullReleaseProofResult(
        int ExitCode,
        string Status,
        string ArtifactRoot,
        string? ProofSummaryJson,
        string? FailureEvidencePath,
        IReadOnlyList<string> ReasonCodes);

    private sealed record MatrixNativeFullReleaseFailure(
        string FailedStepId,
        IReadOnlyList<string> ReasonCodes,
        string Message);
}
