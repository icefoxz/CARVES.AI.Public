namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    internal sealed record MatrixNativePackagedProofResult(
        int ExitCode,
        string Status,
        string ArtifactRoot,
        string? PackagedMatrixOutputPath,
        IReadOnlyList<string> ReasonCodes);

    private sealed record MatrixNativePackagedFailure(
        string FailedStepId,
        IReadOnlyList<string> ReasonCodes,
        string Message);
}
