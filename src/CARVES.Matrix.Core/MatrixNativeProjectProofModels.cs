namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    internal sealed record MatrixNativeProjectProofResult(
        int ExitCode,
        string Status,
        string ArtifactRoot,
        string? ProjectMatrixOutputPath,
        IReadOnlyList<string> ReasonCodes);

    private sealed record MatrixNativeProjectProofChainResult(
        string GuardRunId,
        string GuardCheckJson,
        string HandoffInspectJson,
        string AuditSummaryJson,
        string AuditEvidenceJson,
        string ShieldEvaluateJson,
        string ShieldBadgeJson);
}
