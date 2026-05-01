namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static List<MatrixVerifyIssue> CreateManifestIssues(
        MatrixArtifactManifestVerificationResult manifestVerification)
    {
        return manifestVerification.Issues
            .Select(issue => new MatrixVerifyIssue(
                Scope: issue.ArtifactKind == "manifest" ? "manifest" : "artifact",
                issue.ArtifactKind,
                issue.Path,
                issue.Code,
                ExpectedValue: issue.ExpectedSha256 ?? issue.ExpectedSize?.ToString(),
                ActualValue: issue.ActualSha256 ?? issue.ActualSize?.ToString()))
            .ToList();
    }
}
