namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static MatrixVerifyTrustChainHardening BuildProjectedProofSummaryTrustChainHardening(
        string artifactRoot,
        string manifestPath,
        MatrixArtifactManifestVerificationResult manifestVerification)
    {
        var issues = CreateManifestIssues(manifestVerification);
        var requiredArtifacts = VerifyRequiredArtifacts(manifestPath, issues);
        var trialArtifacts = VerifyTrialArtifacts(artifactRoot, manifestVerification, issues, requireTrial: false);
        var shieldEvaluation = VerifyShieldEvaluation(artifactRoot, manifestVerification, issues);
        return BuildVerifyTrustChainHardening(
            manifestVerification,
            requiredArtifacts,
            trialArtifacts,
            shieldEvaluation,
            new MatrixVerifyProofSummary("matrix-proof-summary.json", Present: true, Consistent: true),
            issues);
    }
}
