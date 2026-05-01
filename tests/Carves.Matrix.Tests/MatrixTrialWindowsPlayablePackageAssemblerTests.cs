using System.IO.Compression;
using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialWindowsPlayablePackageAssemblerTests
{
    [Fact]
    public void TrialPackage_WindowsPlayableBundlesScorerManifestAndZipOutsideWorkspace()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-trial-win-playable-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(tempRoot, "package");
        var scorerRoot = Path.Combine(tempRoot, "scorer-publish");
        var zipPath = Path.Combine(tempRoot, "carves-agent-trial-pack-win-x64.zip");
        try
        {
            WriteFakeScorerPublish(scorerRoot);

            var result = RunMatrixCli(
                "trial",
                "package",
                "--output",
                packageRoot,
                "--windows-playable",
                "--scorer-root",
                scorerRoot,
                "--zip-output",
                zipPath,
                "--runtime-identifier",
                "win-x64",
                "--build-label",
                "test-build",
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.True(root.GetProperty("windows_playable").GetBoolean());
            Assert.Equal(Path.GetFullPath(zipPath), root.GetProperty("zip_path").GetString());
            Assert.Equal("tools/carves/carves.exe", root.GetProperty("scorer_entrypoint").GetString());
            Assert.Equal("win-x64", root.GetProperty("runtime_identifier").GetString());
            Assert.Equal("test-build", root.GetProperty("build_label").GetString());

            var scorerPackageRoot = Path.Combine(packageRoot, "tools", "carves");
            Assert.True(File.Exists(Path.Combine(scorerPackageRoot, "carves.exe")));
            Assert.True(File.Exists(Path.Combine(scorerPackageRoot, "Carves.Runtime.Cli.dll")));
            Assert.True(File.Exists(Path.Combine(scorerPackageRoot, "scorer-root-manifest.json")));
            Assert.True(File.Exists(Path.Combine(scorerPackageRoot, "scorer-manifest.json")));
            Assert.False(Directory.Exists(Path.Combine(packageRoot, "agent-workspace", "tools")));
            Assert.False(File.Exists(Path.Combine(packageRoot, "agent-workspace", "scorer-manifest.json")));
            Assert.True(File.Exists(zipPath));

            using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(scorerPackageRoot, "scorer-manifest.json")));
            var manifest = manifestDocument.RootElement;
            Assert.Equal("carves-portable-scorer.v0", manifest.GetProperty("schema_version").GetString());
            Assert.Equal("runtime_cli", manifest.GetProperty("scorer_kind").GetString());
            Assert.Equal("win-x64", manifest.GetProperty("runtime_identifier").GetString());
            Assert.Equal("tools/carves/carves.exe", manifest.GetProperty("entrypoint").GetString());
            Assert.Equal("test-build", manifest.GetProperty("build_label").GetString());
            Assert.True(manifest.GetProperty("self_contained").GetBoolean());
            Assert.False(manifest.GetProperty("requires_source_checkout_to_run").GetBoolean());
            Assert.False(manifest.GetProperty("requires_dotnet_to_run").GetBoolean());
            Assert.False(manifest.GetProperty("uses_dotnet_run").GetBoolean());
            Assert.Equal("tools/carves/scorer-root-manifest.json", manifest.GetProperty("scorer_root_manifest").GetString());
            Assert.True(manifest.GetProperty("local_only").GetBoolean());
            Assert.False(manifest.GetProperty("server_submission").GetBoolean());
            Assert.False(manifest.GetProperty("certification").GetBoolean());
            Assert.False(manifest.GetProperty("leaderboard_eligible").GetBoolean());
            Assert.Contains(manifest.GetProperty("supported_commands").EnumerateArray(), item => item.GetString() == "test collect");
            Assert.Contains(manifest.GetProperty("supported_commands").EnumerateArray(), item => item.GetString() == "test reset");
            Assert.Contains(manifest.GetProperty("supported_commands").EnumerateArray(), item => item.GetString() == "test verify");
            Assert.Contains(manifest.GetProperty("supported_commands").EnumerateArray(), item => item.GetString() == "test result");
            Assert.Contains(manifest.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_leaderboard_proof");

            var hashes = manifest.GetProperty("file_hashes").EnumerateArray().ToArray();
            Assert.Contains(hashes, hash => hash.GetProperty("path").GetString() == "tools/carves/carves.exe"
                && hash.GetProperty("sha256").GetString()!.StartsWith("sha256:", StringComparison.Ordinal));
            Assert.Contains(hashes, hash => hash.GetProperty("path").GetString() == "tools/carves/Carves.Runtime.Cli.dll");
            Assert.Contains(hashes, hash => hash.GetProperty("path").GetString() == "tools/carves/scorer-root-manifest.json");
            Assert.DoesNotContain(hashes, hash => hash.GetProperty("path").GetString() == "tools/carves/scorer-manifest.json");
            AssertNoLocalPathLeak(File.ReadAllText(Path.Combine(scorerPackageRoot, "scorer-root-manifest.json")), tempRoot, packageRoot, scorerRoot);
            AssertNoLocalPathLeak(File.ReadAllText(Path.Combine(scorerPackageRoot, "scorer-manifest.json")), tempRoot, packageRoot, scorerRoot);

            using var zip = ZipFile.OpenRead(zipPath);
            Assert.All(zip.Entries, entry =>
            {
                Assert.DoesNotContain(tempRoot, entry.FullName, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\\", entry.FullName, StringComparison.Ordinal);
                Assert.DoesNotContain(":", entry.FullName, StringComparison.Ordinal);
                Assert.False(entry.FullName.StartsWith("/", StringComparison.Ordinal), "Zip entries must be relative.");
            });
            Assert.Contains(zip.Entries, entry => entry.FullName == "tools/carves/carves.exe");
            Assert.Contains(zip.Entries, entry => entry.FullName == "tools/carves/scorer-root-manifest.json");
            Assert.Contains(zip.Entries, entry => entry.FullName == "tools/carves/scorer-manifest.json");
            Assert.Contains(zip.Entries, entry => entry.FullName == "SCORE.cmd");
            Assert.Contains(zip.Entries, entry => entry.FullName == ".carves-pack/state.json");
            Assert.Contains(zip.Entries, entry => entry.FullName == "agent-workspace/AGENTS.md");
            Assert.DoesNotContain(zip.Entries, entry => entry.FullName.StartsWith("agent-workspace/tools/", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void TrialPackage_WindowsPlayableFailsWhenScorerEntrypointIsMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-trial-win-playable-missing-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(tempRoot, "package");
        var scorerRoot = Path.Combine(tempRoot, "scorer-publish");
        try
        {
            Directory.CreateDirectory(scorerRoot);
            File.WriteAllText(Path.Combine(scorerRoot, "Carves.Runtime.Cli.dll"), "not enough");

            var result = RunMatrixCli(
                "trial",
                "package",
                "--output",
                packageRoot,
                "--windows-playable",
                "--scorer-root",
                scorerRoot,
                "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal("failed", document.RootElement.GetProperty("status").GetString());
            Assert.Contains(
                document.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "trial_package_scorer_missing"
                    && diagnostic.GetProperty("next_step").GetString()!.Contains("carves.exe", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void TrialPackage_WindowsPlayableFailsWhenScorerRootManifestIsMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-trial-win-playable-root-manifest-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(tempRoot, "package");
        var scorerRoot = Path.Combine(tempRoot, "scorer-publish");
        try
        {
            Directory.CreateDirectory(scorerRoot);
            File.WriteAllText(Path.Combine(scorerRoot, "carves.exe"), "fake windows scorer");

            var result = RunMatrixCli(
                "trial",
                "package",
                "--output",
                packageRoot,
                "--windows-playable",
                "--scorer-root",
                scorerRoot,
                "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal("failed", document.RootElement.GetProperty("status").GetString());
            Assert.Contains(
                document.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "trial_package_scorer_missing"
                    && diagnostic.GetProperty("next_step").GetString()!.Contains("self-contained", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void TrialPackage_WindowsPlayableFailsWhenScorerRootManifestDoesNotSupportVerify()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-trial-win-playable-root-commands-" + Guid.NewGuid().ToString("N"));
        var packageRoot = Path.Combine(tempRoot, "package");
        var scorerRoot = Path.Combine(tempRoot, "scorer-publish");
        try
        {
            WriteFakeScorerPublish(scorerRoot, """["test collect", "test reset"]""");

            var result = RunMatrixCli(
                "trial",
                "package",
                "--output",
                packageRoot,
                "--windows-playable",
                "--scorer-root",
                scorerRoot,
                "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal("failed", document.RootElement.GetProperty("status").GetString());
            Assert.Contains(
                document.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "trial_package_scorer_missing");
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    private static void WriteFakeScorerPublish(string scorerRoot, string supportedCommandsJson = """["test collect", "test reset", "test verify", "test result"]""")
    {
        Directory.CreateDirectory(scorerRoot);
        File.WriteAllText(Path.Combine(scorerRoot, "carves.exe"), "fake windows scorer");
        File.WriteAllText(Path.Combine(scorerRoot, "Carves.Runtime.Cli.dll"), "fake runtime payload");
        File.WriteAllText(
            Path.Combine(scorerRoot, "scorer-root-manifest.json"),
            $$"""
            {
              "schema_version": "carves-windows-scorer-root.v0",
              "scorer_kind": "runtime_cli",
              "runtime_identifier": "win-x64",
              "entrypoint": "carves.exe",
              "target_project": "src/CARVES.Runtime.Cli/carves.csproj",
              "configuration": "Release",
              "build_label": "test-build",
              "self_contained": true,
              "requires_source_checkout_to_run": false,
              "requires_dotnet_to_run": false,
              "uses_dotnet_run": false,
              "supported_commands": {{supportedCommandsJson}}
            }
            """);
        Directory.CreateDirectory(Path.Combine(scorerRoot, "runtimes", "win-x64"));
        File.WriteAllText(Path.Combine(scorerRoot, "runtimes", "win-x64", "native.dll"), "fake native payload");
    }

    private static void AssertNoLocalPathLeak(string text, params string[] roots)
    {
        foreach (var root in roots)
        {
            Assert.DoesNotContain(Path.GetFullPath(root), text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
