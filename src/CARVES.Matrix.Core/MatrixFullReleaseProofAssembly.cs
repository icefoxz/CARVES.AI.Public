using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static int CompleteFullReleaseProof(
        string artifactRoot,
        JsonElement projectRoot,
        JsonElement packagedRoot,
        object proofCapabilities,
        bool emitSummary = true)
    {
        var manifestPath = MatrixArtifactManifestWriter.WriteDefaultProofManifest(artifactRoot);
        var manifestSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(manifestPath);
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);
        var manifestRelativePath = Path.GetRelativePath(artifactRoot, manifestPath).Replace('\\', '/');

        var projectedTrustChainHardening = BuildProjectedProofSummaryTrustChainHardening(
            artifactRoot,
            manifestPath,
            manifestVerification);
        var pendingSummary = BuildProofSummary(
            artifactRoot,
            manifestRelativePath,
            manifestSha256,
            manifestVerification,
            projectRoot,
            packagedRoot,
            projectedTrustChainHardening,
            proofCapabilities);
        File.WriteAllText(
            Path.Combine(artifactRoot, "matrix-proof-summary.json"),
            JsonSerializer.Serialize(pendingSummary, JsonOptions));

        var verifyResult = BuildVerifyResult(artifactRoot, requireTrial: false);
        var summary = BuildProofSummary(
            artifactRoot,
            manifestRelativePath,
            manifestSha256,
            manifestVerification,
            projectRoot,
            packagedRoot,
            verifyResult.TrustChainHardening,
            proofCapabilities);

        var summaryJson = JsonSerializer.Serialize(summary, JsonOptions);
        File.WriteAllText(Path.Combine(artifactRoot, "matrix-proof-summary.json"), summaryJson);
        if (emitSummary)
        {
            Console.WriteLine(summaryJson);
        }

        return verifyResult.IsVerified && verifyResult.TrustChainHardening.GatesSatisfied ? 0 : 1;
    }
}
