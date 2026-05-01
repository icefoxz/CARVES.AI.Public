using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static int RunVerify(IReadOnlyList<string> arguments)
    {
        var options = MatrixVerifyOptions.Parse(arguments);
        if (!string.IsNullOrWhiteSpace(options.Error))
        {
            Console.Error.WriteLine(options.Error);
            return 2;
        }

        var result = BuildVerifyResult(options.ArtifactRoot!, options.RequireTrial);
        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            WriteVerifyText(result);
        }

        return result.IsVerified ? 0 : 1;
    }

    private static MatrixVerifyResult BuildVerifyResult(string artifactRoot, bool requireTrial)
    {
        var fullArtifactRoot = Path.GetFullPath(artifactRoot);
        var manifestRelativePath = MatrixArtifactManifestWriter.DefaultManifestFileName;
        var manifestPath = Path.Combine(fullArtifactRoot, manifestRelativePath);
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath, fullArtifactRoot);
        var manifestSha256 = File.Exists(manifestPath)
            ? MatrixArtifactManifestWriter.ComputeFileSha256(manifestPath)
            : null;

        var issues = CreateManifestIssues(manifestVerification);

        var requiredArtifacts = VerifyRequiredArtifacts(manifestPath, issues);
        var trialArtifacts = VerifyTrialArtifacts(fullArtifactRoot, manifestVerification, issues, requireTrial);
        var shieldEvaluation = VerifyShieldEvaluation(fullArtifactRoot, manifestVerification, issues);
        var proofSummary = VerifyProofSummary(
            fullArtifactRoot,
            manifestRelativePath,
            manifestSha256,
            manifestVerification,
            requiredArtifacts,
            trialArtifacts,
            shieldEvaluation,
            issues);
        var verificationPosture = ResolveVerifyPosture(manifestVerification, issues);
        var trustChainHardening = BuildVerifyTrustChainHardening(
            manifestVerification,
            requiredArtifacts,
            trialArtifacts,
            shieldEvaluation,
            proofSummary,
            issues);

        return new MatrixVerifyResult(
            "matrix-verify.v0",
            verificationPosture == "verified" ? "verified" : "failed",
            verificationPosture,
            MatrixArtifactManifestWriter.PortableArtifactRoot,
            requireTrial ? "trial" : "ordinary",
            new MatrixVerifyManifest(
                manifestRelativePath,
                manifestSha256,
                manifestVerification.VerificationPosture,
                manifestVerification.Issues.Count),
            requiredArtifacts,
            trialArtifacts,
            shieldEvaluation,
            proofSummary,
            trustChainHardening,
            issues.Count,
            issues);
    }
}
