using Carves.Matrix.Core;
using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixNativeFullReleaseAtomicityTests
{
    [Fact]
    public void NativeFullReleaseProjectFailureDoesNotOverwriteExistingBundleOrPublishStaleSuccess()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        var originalSummary = File.ReadAllText(BundlePath(bundle.ArtifactRoot, "matrix-proof-summary.json"));
        var originalManifest = File.ReadAllText(BundlePath(bundle.ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName));
        string? failureEvidencePath = null;

        try
        {
            var result = MatrixCliRunner.ProduceNativeFullReleaseProofArtifacts(
                bundle.ArtifactRoot,
                MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
                configuration: "Debug",
                projectProducer: stagingRoot =>
                {
                    Directory.CreateDirectory(stagingRoot);
                    File.WriteAllText(BundlePath(stagingRoot, MatrixArtifactManifestWriter.DefaultManifestFileName), """{"stale":true}""");
                    File.WriteAllText(BundlePath(stagingRoot, "matrix-proof-summary.json"), """{"stale":true}""");
                    Directory.CreateDirectory(BundlePath(stagingRoot, "project"));
                    File.WriteAllText(BundlePath(stagingRoot, "project/partial-project-marker.txt"), "partial");
                    return new MatrixCliRunner.MatrixNativeProjectProofResult(
                        1,
                        "failed",
                        ".",
                        "project-matrix-output.json",
                        ["forced_project_failure"]);
                });
            failureEvidencePath = result.FailureEvidencePath;

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Status);
            Assert.Contains("forced_project_failure", result.ReasonCodes);
            AssertFailureEvidence(result, bundle.ArtifactRoot, "project", "forced_project_failure");
            Assert.Equal(originalSummary, File.ReadAllText(BundlePath(bundle.ArtifactRoot, "matrix-proof-summary.json")));
            Assert.Equal(originalManifest, File.ReadAllText(BundlePath(bundle.ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName)));
            Assert.False(File.Exists(BundlePath(bundle.ArtifactRoot, "project/partial-project-marker.txt")));
            AssertNoStagingDirectoriesRemain(bundle.ArtifactRoot);

            var verify = RunMatrixCli("verify", bundle.ArtifactRoot, "--json");
            Assert.Equal(0, verify.ExitCode);
        }
        finally
        {
            DeleteFailureEvidenceRoot(failureEvidencePath);
        }
    }

    [Fact]
    public void NativeFullReleasePackagedFailureKeepsPartialArtifactsIsolatedFromExistingBundle()
    {
        using var bundle = MatrixFullReleaseBundleFixture.Create();
        var originalSummary = File.ReadAllText(BundlePath(bundle.ArtifactRoot, "matrix-proof-summary.json"));
        var originalManifest = File.ReadAllText(BundlePath(bundle.ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName));
        string? failureEvidencePath = null;

        try
        {
            var result = MatrixCliRunner.ProduceNativeFullReleaseProofArtifacts(
                bundle.ArtifactRoot,
                MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
                configuration: "Debug",
                projectProducer: stagingRoot =>
                {
                    Directory.CreateDirectory(BundlePath(stagingRoot, "project"));
                    File.WriteAllText(BundlePath(stagingRoot, "project/partial-project-marker.txt"), "partial");
                    return new MatrixCliRunner.MatrixNativeProjectProofResult(
                        0,
                        "passed",
                        ".",
                        "project-matrix-output.json",
                        []);
                },
                packagedProducer: stagingRoot =>
                {
                    Directory.CreateDirectory(BundlePath(stagingRoot, "packaged"));
                    File.WriteAllText(BundlePath(stagingRoot, "packaged/partial-packaged-marker.txt"), "partial");
                    return new MatrixCliRunner.MatrixNativePackagedProofResult(
                        1,
                        "failed",
                        ".",
                        "packaged-matrix-output.json",
                        ["forced_packaged_failure"]);
                });
            failureEvidencePath = result.FailureEvidencePath;

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Status);
            Assert.Contains("forced_packaged_failure", result.ReasonCodes);
            AssertFailureEvidence(result, bundle.ArtifactRoot, "packaged", "forced_packaged_failure");
            Assert.Equal(originalSummary, File.ReadAllText(BundlePath(bundle.ArtifactRoot, "matrix-proof-summary.json")));
            Assert.Equal(originalManifest, File.ReadAllText(BundlePath(bundle.ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName)));
            Assert.False(File.Exists(BundlePath(bundle.ArtifactRoot, "packaged/partial-packaged-marker.txt")));
            AssertNoStagingDirectoriesRemain(bundle.ArtifactRoot);

            var verify = RunMatrixCli("verify", bundle.ArtifactRoot, "--json");
            Assert.Equal(0, verify.ExitCode);
        }
        finally
        {
            DeleteFailureEvidenceRoot(failureEvidencePath);
        }
    }

    private static void AssertFailureEvidence(
        MatrixCliRunner.MatrixNativeFullReleaseProofResult result,
        string artifactRoot,
        string expectedStep,
        string expectedReasonCode)
    {
        Assert.False(string.IsNullOrWhiteSpace(result.FailureEvidencePath));
        Assert.True(File.Exists(result.FailureEvidencePath));
        var artifactRootWithSeparator = Path.GetFullPath(artifactRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.False(Path.GetFullPath(result.FailureEvidencePath).StartsWith(artifactRootWithSeparator, StringComparison.Ordinal));
        using var document = JsonDocument.Parse(File.ReadAllText(result.FailureEvidencePath));
        var root = document.RootElement;
        Assert.Equal("matrix-native-full-release-attempt.v0", root.GetProperty("schema_version").GetString());
        Assert.Equal("failed", root.GetProperty("status").GetString());
        Assert.Equal("isolated_failed_attempt", root.GetProperty("staging_posture").GetString());
        Assert.Equal(expectedStep, root.GetProperty("failed_step").GetString());
        Assert.Contains(
            root.GetProperty("reason_codes").EnumerateArray(),
            code => code.GetString() == expectedReasonCode);
    }

    private static void AssertNoStagingDirectoriesRemain(string artifactRoot)
    {
        var fullArtifactRoot = Path.GetFullPath(artifactRoot);
        var parent = Path.GetDirectoryName(fullArtifactRoot)!;
        var name = Path.GetFileName(fullArtifactRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        Assert.Empty(Directory.EnumerateDirectories(parent, $".{name}.native-full-release-staging-*"));
    }

    private static void DeleteFailureEvidenceRoot(string? failureEvidencePath)
    {
        if (string.IsNullOrWhiteSpace(failureEvidencePath))
        {
            return;
        }

        var root = Path.GetDirectoryName(failureEvidencePath);
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string BundlePath(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
