using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static int CompleteNativeMinimalProof(
        string artifactRoot,
        MatrixOptions options,
        string workRepoRoot,
        IReadOnlyList<MatrixNativeProofStep> steps,
        MatrixNativeProofProductChainResult productChain)
    {
        var artifactCopyError = WriteNativeProofArtifacts(artifactRoot, workRepoRoot, productChain.ShieldEvaluateStdout, productChain.ShieldBadgeStdout, out var matrixSummaryRoot);
        if (!string.IsNullOrWhiteSpace(artifactCopyError))
        {
            return WriteNativeProofFailure(artifactRoot, options, workRepoRoot, steps, "artifact_materialization", ["native_artifact_materialization_failed"], artifactCopyError);
        }

        var manifestPath = MatrixArtifactManifestWriter.WriteDefaultProofManifest(artifactRoot);
        var manifestSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(manifestPath);
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);
        var manifestRelativePath = Path.GetRelativePath(artifactRoot, manifestPath).Replace('\\', '/');
        var projectedTrustChainHardening = BuildProjectedProofSummaryTrustChainHardening(
            artifactRoot,
            manifestPath,
            manifestVerification);
        var pendingSummary = BuildNativeProofSummary(
            artifactRoot,
            manifestRelativePath,
            manifestSha256,
            manifestVerification,
            matrixSummaryRoot,
            projectedTrustChainHardening);
        File.WriteAllText(
            Path.Combine(artifactRoot, "matrix-proof-summary.json"),
            JsonSerializer.Serialize(pendingSummary, JsonOptions));

        var verifyResult = BuildVerifyResult(artifactRoot, requireTrial: false);
        var proofSummary = BuildNativeProofSummary(
            artifactRoot,
            manifestRelativePath,
            manifestSha256,
            manifestVerification,
            matrixSummaryRoot,
            verifyResult.TrustChainHardening);
        File.WriteAllText(
            Path.Combine(artifactRoot, "matrix-proof-summary.json"),
            JsonSerializer.Serialize(proofSummary, JsonOptions));

        var result = new
        {
            schema_version = "matrix-native-proof.v0",
            status = verifyResult.IsVerified ? "verified" : "failed",
            proof_mode = "native_minimal",
            proof_capabilities = BuildNativeMinimalProofCapabilities(),
            configuration = options.Configuration,
            artifact_root = ToPublicArtifactRootMarker(),
            work_repo = BuildNativeWorkRepoOutput(workRepoRoot, options.Keep),
            steps,
            artifacts = BuildNativeProofArtifactIndex(),
            verify = verifyResult,
            exit_code = verifyResult.ExitCode,
            reason_codes = verifyResult.ReasonCodes,
        };
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return verifyResult.ExitCode;
    }
}
