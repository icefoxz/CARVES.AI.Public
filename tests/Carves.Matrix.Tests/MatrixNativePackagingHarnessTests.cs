using Carves.Matrix.Core;

namespace Carves.Matrix.Tests;

public sealed class MatrixNativePackagingHarnessTests
{
    [Fact]
    public void NativePackagingHarness_BuildsPackagesInstallsToolsAndDoesNotWriteProofBundle()
    {
        var repoRoot = LocateSourceRepoRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-packaging-" + Guid.NewGuid().ToString("N"));
        try
        {
            var packageRoot = Path.Combine(tempRoot, "packages");
            var toolRoot = Path.Combine(tempRoot, "tools");
            var result = MatrixCliRunner.RunNativePackagingHarness(
                new MatrixCliRunner.MatrixNativePackagingHarnessOptions(
                    RuntimeRoot: repoRoot,
                    PackageRoot: packageRoot,
                    ToolRoot: toolRoot,
                    Configuration: "Debug",
                    Version: "0.1.0-card886.1"));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("passed", result.Status);
            Assert.Empty(result.ReasonCodes);
            Assert.Equal(Path.GetFullPath(packageRoot), result.PackageRoot);
            Assert.Equal(Path.GetFullPath(toolRoot), result.ToolRoot);
            Assert.NotNull(result.InstalledCommands);

            Assert.Equal(ExpectedTools().Length, result.Packages.Count);
            Assert.Equal(ExpectedTools().Length, result.ToolInstalls.Count);
            foreach (var tool in ExpectedTools())
            {
                var package = Assert.Single(result.Packages, candidate => candidate.ToolName == tool.Name);
                Assert.True(package.Passed);
                Assert.Empty(package.ReasonCodes);
                Assert.Equal(0, package.ExitCode);
                Assert.True(File.Exists(package.PackagePath), $"Missing package: {package.PackagePath}");
                Assert.StartsWith(Path.GetFullPath(packageRoot), package.PackagePath, StringComparison.Ordinal);

                var install = Assert.Single(result.ToolInstalls, candidate => candidate.ToolName == tool.Name);
                Assert.True(install.Passed);
                Assert.Empty(install.ReasonCodes);
                Assert.Equal(0, install.ExitCode);
                Assert.NotNull(install.CommandPath);
                Assert.True(File.Exists(install.CommandPath), $"Missing installed command: {install.CommandPath}");
                Assert.Equal(ExpectedCommandFileName(tool.CommandName), Path.GetFileName(install.CommandPath));
                Assert.StartsWith(Path.GetFullPath(toolRoot), install.CommandPath, StringComparison.Ordinal);
            }

            Assert.Equal(CommandPath(toolRoot, "carves-guard"), result.InstalledCommands!.CarvesGuard);
            Assert.Equal(CommandPath(toolRoot, "carves-handoff"), result.InstalledCommands.CarvesHandoff);
            Assert.Equal(CommandPath(toolRoot, "carves-audit"), result.InstalledCommands.CarvesAudit);
            Assert.Equal(CommandPath(toolRoot, "carves-shield"), result.InstalledCommands.CarvesShield);
            Assert.Equal(CommandPath(toolRoot, "carves-matrix"), result.InstalledCommands.CarvesMatrix);
            AssertNoProofBundleArtifacts(tempRoot);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void NativePackagingHarness_InvalidConfigurationReturnsStableReasonWithoutProofBundle()
    {
        var repoRoot = LocateSourceRepoRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-packaging-invalid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = MatrixCliRunner.RunNativePackagingHarness(
                new MatrixCliRunner.MatrixNativePackagingHarnessOptions(
                    RuntimeRoot: repoRoot,
                    PackageRoot: Path.Combine(tempRoot, "packages"),
                    ToolRoot: Path.Combine(tempRoot, "tools"),
                    Configuration: "RelWithDebInfo"));

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Status);
            Assert.Contains("native_packaging_configuration_invalid", result.ReasonCodes);
            Assert.Empty(result.Packages);
            Assert.Empty(result.ToolInstalls);
            Assert.Null(result.InstalledCommands);
            AssertNoProofBundleArtifacts(tempRoot);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void NativePackagingHarness_PackFailureReturnsBoundedPreviewAndStableReason()
    {
        var repoRoot = LocateSourceRepoRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-packaging-pack-failure-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = MatrixCliRunner.RunNativePackagingHarness(
                new MatrixCliRunner.MatrixNativePackagingHarnessOptions(
                    RuntimeRoot: repoRoot,
                    PackageRoot: Path.Combine(tempRoot, "packages"),
                    ToolRoot: Path.Combine(tempRoot, "tools"),
                    Configuration: "Debug",
                    Version: "not a version"));

            Assert.Equal(1, result.ExitCode);
            Assert.Equal("failed", result.Status);
            Assert.Contains("native_packaging_pack_failed", result.ReasonCodes);
            Assert.Single(result.Packages);
            var package = result.Packages[0];
            Assert.False(package.Passed);
            Assert.Contains("native_packaging_pack_failed", package.ReasonCodes);
            Assert.True(package.StdoutPreview is not null || package.StderrPreview is not null);
            Assert.True((package.StdoutPreview?.Length ?? 0) <= 4014);
            Assert.True((package.StderrPreview?.Length ?? 0) <= 4014);
            Assert.Empty(result.ToolInstalls);
            Assert.Null(result.InstalledCommands);
            AssertNoProofBundleArtifacts(tempRoot);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static (string Name, string CommandName)[] ExpectedTools()
    {
        return
        [
            ("guard", "carves-guard"),
            ("handoff", "carves-handoff"),
            ("audit", "carves-audit"),
            ("shield", "carves-shield"),
            ("matrix", "carves-matrix"),
        ];
    }

    private static string CommandPath(string toolRoot, string commandName)
    {
        return Path.Combine(Path.GetFullPath(toolRoot), ExpectedCommandFileName(commandName));
    }

    private static string ExpectedCommandFileName(string commandName)
    {
        return OperatingSystem.IsWindows() ? commandName + ".exe" : commandName;
    }

    private static void AssertNoProofBundleArtifacts(string root)
    {
        Assert.False(File.Exists(Path.Combine(root, "matrix-artifact-manifest.json")));
        Assert.False(File.Exists(Path.Combine(root, "matrix-proof-summary.json")));
        Assert.False(Directory.Exists(Path.Combine(root, "project")));
        Assert.False(Directory.Exists(Path.Combine(root, "packaged")));
    }

    private static string LocateSourceRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CARVES.Runtime.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root from test output directory.");
    }
}
