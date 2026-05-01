using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    internal static MatrixNativeFullReleaseProofResult ProduceNativeFullReleaseProofArtifacts(
        string artifactRoot,
        string runtimeRoot,
        string configuration = "Release",
        Func<string, MatrixNativeProjectProofResult>? projectProducer = null,
        Func<string, MatrixNativePackagedProofResult>? packagedProducer = null)
    {
        var fullArtifactRoot = Path.GetFullPath(artifactRoot);
        var stagingRoot = CreateNativeFullReleaseStagingRoot(fullArtifactRoot);
        try
        {
            Directory.CreateDirectory(stagingRoot);
            var projectResult = (projectProducer ?? (root => ProduceNativeFullReleaseProjectArtifacts(
                root,
                configuration: configuration)))(stagingRoot);
            if (projectResult.ExitCode != 0)
            {
                return WriteNativeFullReleaseAttemptFailure(
                    fullArtifactRoot,
                    stagingRoot,
                    new MatrixNativeFullReleaseFailure("project", projectResult.ReasonCodes, "Native full-release project producer failed."));
            }

            var packagedResult = (packagedProducer ?? (root => ProduceNativeFullReleasePackagedArtifacts(
                root,
                runtimeRoot: runtimeRoot,
                configuration: configuration)))(stagingRoot);
            if (packagedResult.ExitCode != 0)
            {
                return WriteNativeFullReleaseAttemptFailure(
                    fullArtifactRoot,
                    stagingRoot,
                    new MatrixNativeFullReleaseFailure("packaged", packagedResult.ReasonCodes, "Native full-release packaged producer failed."));
            }

            using var projectJson = JsonDocument.Parse(File.ReadAllText(ResolveNativeRelativePath(stagingRoot, "project/matrix-summary.json")));
            using var packagedJson = JsonDocument.Parse(File.ReadAllText(ResolveNativeRelativePath(stagingRoot, "packaged/matrix-packaged-summary.json")));
            var proofExitCode = CompleteFullReleaseProof(
                stagingRoot,
                projectJson.RootElement,
                packagedJson.RootElement,
                BuildNativeFullReleaseProofCapabilities(),
                emitSummary: false);
            if (proofExitCode != 0)
            {
                return WriteNativeFullReleaseAttemptFailure(
                    fullArtifactRoot,
                    stagingRoot,
                    new MatrixNativeFullReleaseFailure("proof_assembly", ["native_full_release_proof_assembly_failed"], "Native full-release proof assembly did not verify."));
            }

            var summaryJson = File.ReadAllText(ResolveNativeRelativePath(stagingRoot, "matrix-proof-summary.json"));
            PromoteNativeFullReleaseStagingRoot(stagingRoot, fullArtifactRoot);
            return new MatrixNativeFullReleaseProofResult(
                ExitCode: 0,
                Status: "passed",
                ArtifactRoot: ToPublicArtifactRootMarker(),
                ProofSummaryJson: summaryJson,
                FailureEvidencePath: null,
                ReasonCodes: []);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or InvalidOperationException
                                   or JsonException
                                   or System.ComponentModel.Win32Exception)
        {
            return WriteNativeFullReleaseAttemptFailure(
                fullArtifactRoot,
                stagingRoot,
                new MatrixNativeFullReleaseFailure(
                    "native_full_release",
                    ["native_full_release_failed"],
                    $"Native full-release proof failed: {ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static MatrixNativeFullReleaseProofResult WriteNativeFullReleaseAttemptFailure(
        string artifactRoot,
        string stagingRoot,
        MatrixNativeFullReleaseFailure failure)
    {
        var failureEvidencePath = PreserveNativeFullReleaseFailure(artifactRoot, stagingRoot, failure);
        return new MatrixNativeFullReleaseProofResult(
            ExitCode: 1,
            Status: "failed",
            ArtifactRoot: ToPublicArtifactRootMarker(),
            ProofSummaryJson: null,
            FailureEvidencePath: failureEvidencePath,
            ReasonCodes: DistinctNativeReasonCodes(failure.ReasonCodes));
    }
}
