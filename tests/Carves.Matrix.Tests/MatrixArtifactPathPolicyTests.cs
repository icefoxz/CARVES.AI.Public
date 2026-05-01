using Carves.Matrix.Core;
using System.Reflection;
using static Carves.Matrix.Tests.MatrixVerifyJsonAssertions;

namespace Carves.Matrix.Tests;

public sealed class MatrixArtifactPathPolicyTests
{
    [Theory]
    [InlineData("../outside/shield-evaluate.json")]
    [InlineData("/tmp/outside/shield-evaluate.json")]
    [InlineData("C:/outside/shield-evaluate.json")]
    [InlineData("\\\\server\\share\\shield-evaluate.json")]
    public void VerifyCommand_RejectsManifestArtifactPathsOutsideBundleRoot(string escapedPath)
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.SetArtifactManifestStringField("shield_evaluation", "path", escapedPath);
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsIssue(root, "shield_evaluation", "artifact_path_escapes_root", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_RejectsCaseDifferingSiblingEscapeOnCaseSensitiveFileSystems()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var parent = Path.Combine(Path.GetTempPath(), "carves-matrix-case-policy-" + Guid.NewGuid().ToString("N"));
        var artifactRoot = Path.Combine(parent, "Bundle");
        var siblingRoot = Path.Combine(parent, "bundle");
        MatrixBundleFixture? bundle = null;
        try
        {
            bundle = MatrixBundleFixture.Create(artifactRoot: artifactRoot);
            Directory.CreateDirectory(Path.Combine(siblingRoot, "project"));
            File.Copy(
                Path.Combine(artifactRoot, "project", "shield-evaluate.json"),
                Path.Combine(siblingRoot, "project", "shield-evaluate.json"));
            bundle.SetArtifactManifestStringField("shield_evaluation", "path", "../bundle/project/shield-evaluate.json");
            bundle.WriteProofSummary();

            var root = RunVerifyJson(bundle.ArtifactRoot);

            Assert.Equal("failed", root.GetProperty("status").GetString());
            AssertContainsIssue(root, "shield_evaluation", "artifact_path_escapes_root", "schema_mismatch");
        }
        finally
        {
            bundle?.Dispose();
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    [Fact]
    public void VerifyCommand_RejectsSymlinkArtifactInsideBundleRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var bundle = MatrixBundleFixture.Create();
        var outsideRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-symlink-target-" + Guid.NewGuid().ToString("N"));
        try
        {
            var artifactPath = Path.Combine(bundle.ArtifactRoot, "project", "shield-evidence.json");
            Directory.CreateDirectory(outsideRoot);
            var outsidePath = Path.Combine(outsideRoot, "shield-evidence.json");
            File.WriteAllText(outsidePath, File.ReadAllText(artifactPath));
            File.Delete(artifactPath);
            File.CreateSymbolicLink(artifactPath, outsidePath);
            bundle.WriteProofSummary();

            var root = RunVerifyJson(bundle.ArtifactRoot);

            Assert.Equal("failed", root.GetProperty("status").GetString());
            AssertContainsIssue(root, "audit_evidence", "artifact_reparse_point_rejected", "schema_mismatch");
        }
        finally
        {
            if (Directory.Exists(outsideRoot))
            {
                Directory.Delete(outsideRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ManifestWriter_RejectsSymlinkArtifactInsideBundleRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var bundle = MatrixBundleFixture.Create();
        var outsideRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-manifest-symlink-target-" + Guid.NewGuid().ToString("N"));
        try
        {
            var artifactPath = Path.Combine(bundle.ArtifactRoot, "project", "shield-evidence.json");
            Directory.CreateDirectory(outsideRoot);
            var outsidePath = Path.Combine(outsideRoot, "shield-evidence.json");
            File.WriteAllText(outsidePath, File.ReadAllText(artifactPath));
            File.Delete(artifactPath);
            File.CreateSymbolicLink(artifactPath, outsidePath);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                MatrixArtifactManifestWriter.WriteDefaultProofManifest(bundle.ArtifactRoot));

            Assert.Contains("symbolic link or reparse point", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outsideRoot))
            {
                Directory.Delete(outsideRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void NativeProofArtifactCopy_RejectsSymlinkSourceBeforeCopying()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var sourceRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-copy-source-" + Guid.NewGuid().ToString("N"));
        var destinationRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-copy-destination-" + Guid.NewGuid().ToString("N"));
        var outsideRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-copy-target-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(sourceRoot, ".carves"));
            Directory.CreateDirectory(destinationRoot);
            Directory.CreateDirectory(outsideRoot);
            var outsidePath = Path.Combine(outsideRoot, "shield-evidence.json");
            File.WriteAllText(outsidePath, """{"schema_version":"shield-evidence.v0"}""");
            var sourcePath = Path.Combine(sourceRoot, ".carves", "shield-evidence.json");
            File.CreateSymbolicLink(sourcePath, outsidePath);

            var method = typeof(MatrixCliRunner).GetMethod("CopyNativeArtifact", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(
                null,
                [sourceRoot, ".carves/shield-evidence.json", destinationRoot, "project/shield-evidence.json"]));

            var inner = Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.Contains("symbolic link or reparse point", inner.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(destinationRoot, "project", "shield-evidence.json")));
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }

            if (Directory.Exists(destinationRoot))
            {
                Directory.Delete(destinationRoot, recursive: true);
            }

            if (Directory.Exists(outsideRoot))
            {
                Directory.Delete(outsideRoot, recursive: true);
            }
        }
    }

}
