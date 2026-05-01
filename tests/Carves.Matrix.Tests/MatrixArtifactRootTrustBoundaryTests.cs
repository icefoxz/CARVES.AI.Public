using Carves.Matrix.Core;
using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;
using static Carves.Matrix.Tests.MatrixVerifyJsonAssertions;

namespace Carves.Matrix.Tests;

public sealed class MatrixArtifactRootTrustBoundaryTests
{
    [Fact]
    public void ManifestWriter_UsesPortableArtifactRootMetadata()
    {
        using var bundle = MatrixBundleFixture.Create();

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            bundle.ArtifactRoot,
            MatrixArtifactManifestWriter.DefaultManifestFileName)));
        var root = document.RootElement;

        Assert.Equal(MatrixArtifactManifestWriter.PortableArtifactRoot, root.GetProperty("artifact_root").GetString());
        Assert.Equal(MatrixArtifactManifestWriter.RedactedLocalArtifactRoot, root.GetProperty("producer_artifact_root").GetString());
    }

    [Fact]
    public void PublicProofArtifacts_RedactLocalAbsoluteArtifactRootByDefault()
    {
        using var bundle = MatrixBundleFixture.Create();
        var fullArtifactRoot = Path.GetFullPath(bundle.ArtifactRoot);
        var manifestPath = Path.Combine(bundle.ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        var matrixSummaryPath = Path.Combine(bundle.ArtifactRoot, "project", "matrix-summary.json");
        var proofSummaryPath = Path.Combine(bundle.ArtifactRoot, "matrix-proof-summary.json");

        var manifestJson = File.ReadAllText(manifestPath);
        var matrixSummaryJson = File.ReadAllText(matrixSummaryPath);
        var proofSummaryJson = File.ReadAllText(proofSummaryPath);
        var verifyResult = RunMatrixCli("verify", bundle.ArtifactRoot, "--json");

        Assert.Equal(0, verifyResult.ExitCode);
        Assert.DoesNotContain(fullArtifactRoot, manifestJson, StringComparison.Ordinal);
        Assert.DoesNotContain(fullArtifactRoot, matrixSummaryJson, StringComparison.Ordinal);
        Assert.DoesNotContain(fullArtifactRoot, proofSummaryJson, StringComparison.Ordinal);
        Assert.DoesNotContain(fullArtifactRoot, verifyResult.StandardOutput, StringComparison.Ordinal);

        using var manifestDocument = JsonDocument.Parse(manifestJson);
        Assert.Equal(MatrixArtifactManifestWriter.PortableArtifactRoot, manifestDocument.RootElement.GetProperty("artifact_root").GetString());
        Assert.Equal(MatrixArtifactManifestWriter.RedactedLocalArtifactRoot, manifestDocument.RootElement.GetProperty("producer_artifact_root").GetString());

        using var matrixSummaryDocument = JsonDocument.Parse(matrixSummaryJson);
        Assert.Equal(MatrixArtifactManifestWriter.PortableArtifactRoot, matrixSummaryDocument.RootElement.GetProperty("artifact_root").GetString());

        using var proofSummaryDocument = JsonDocument.Parse(proofSummaryJson);
        Assert.Equal(MatrixArtifactManifestWriter.PortableArtifactRoot, proofSummaryDocument.RootElement.GetProperty("artifact_root").GetString());
        Assert.Equal(MatrixArtifactManifestWriter.PortableArtifactRoot, proofSummaryDocument.RootElement.GetProperty("native").GetProperty("artifact_root").GetString());
    }

    [Fact]
    public void VerifyCommand_RelocatedPortableBundleStillPasses()
    {
        using var bundle = MatrixBundleFixture.Create();
        var relocatedRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-relocated-bundle-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(bundle.ArtifactRoot, relocatedRoot);

            var result = RunMatrixCli("verify", relocatedRoot, "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.Equal(MatrixArtifactManifestWriter.PortableArtifactRoot, root.GetProperty("artifact_root").GetString());
        }
        finally
        {
            if (Directory.Exists(relocatedRoot))
            {
                Directory.Delete(relocatedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void VerifyCommand_UsesRequestedRootWhenManifestPointsElsewhere()
    {
        using var externalBundle = MatrixBundleFixture.Create();
        using var bundle = MatrixBundleFixture.Create();
        bundle.SetManifestArtifactRoot(externalBundle.ArtifactRoot);
        bundle.DeleteArtifact("project/handoff.json");
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsIssue(root, "manifest", "artifact_root_not_portable", "schema_mismatch");
        AssertContainsIssue(root, "handoff_packet", "artifact_missing", "missing_artifact");
    }

    [Fact]
    public void VerifyCommand_ReportsNonPortableArtifactRootWhenLocalArtifactsExist()
    {
        using var externalBundle = MatrixBundleFixture.Create();
        using var bundle = MatrixBundleFixture.Create();
        bundle.SetManifestArtifactRoot(externalBundle.ArtifactRoot);
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsIssue(root, "manifest", "artifact_root_not_portable", "schema_mismatch");
        Assert.DoesNotContain(
            root.GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("code").GetString() == "artifact_missing");
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relativeDirectory));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativeFile = Path.GetRelativePath(sourceRoot, file);
            var destinationFile = Path.Combine(destinationRoot, relativeFile);
            var destinationDirectory = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(file, destinationFile, overwrite: true);
        }
    }
}
