using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialCleanPlayablePackageSmokeTests
{
    [Fact]
    public void TrialPackage_CleanPlayableZipScoresThroughPackageLocalScorerWithIsolatedPath()
    {
        if (!CanRunPosixSmoke())
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-trial-clean-playable-" + Guid.NewGuid().ToString("N"));
        try
        {
            var packageRoot = CreateExtractedPlayablePackage(tempRoot);
            var isolatedPath = CreateIsolatedToolPath(tempRoot);
            Assert.False(File.Exists(Path.Combine(isolatedPath, "carves")));
            AssertOutsideSourceCheckout(packageRoot);
            var workspaceRoot = Path.Combine(packageRoot, "agent-workspace");
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            var result = RunScoreSh(packageRoot, isolatedPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("CARVES Agent Trial portable score", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("Mode: local only", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("Final result:", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("GREEN VERIFIED", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("Final score:", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("GREEN 100/100", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("Review", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("GREEN 10/10", result.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("<span", result.Stdout, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Result card: results/local/matrix-agent-trial-result-card.md", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("Run ./result.sh to view this result again.", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("Run ./reset.sh before testing another agent", result.Stdout, StringComparison.Ordinal);
            Assert.Contains("Verify again (developer): ./tools/carves/carves test verify results/submit-bundle --trial --json", result.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("Missing scorer", result.Stderr, StringComparison.OrdinalIgnoreCase);

            var localCollectPath = Path.Combine(packageRoot, "results", "local", "matrix-agent-trial-collect.json");
            Assert.True(File.Exists(localCollectPath));
            using var collectDocument = JsonDocument.Parse(File.ReadAllText(localCollectPath));
            var collect = collectDocument.RootElement;
            Assert.Equal("verified", collect.GetProperty("status").GetString());
            Assert.True(collect.GetProperty("offline").GetBoolean());
            Assert.False(collect.GetProperty("server_submission").GetBoolean());
            Assert.Equal("scored", collect.GetProperty("local_score").GetProperty("score_status").GetString());
            Assert.Equal(100, collect.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
            Assert.True(collect.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.Contains(collect.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_certification");
            Assert.Contains(collect.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_leaderboard_eligible");

            AssertSubmitBundle(Path.Combine(packageRoot, "results", "submit-bundle"));
            Assert.False(File.Exists(Path.Combine(packageRoot, "agent-workspace", "tools", "carves", "carves")));

            var repeat = RunScoreSh(packageRoot, isolatedPath);
            Assert.Equal(0, repeat.ExitCode);
            Assert.Contains("Package already scored. Showing the previous local result.", repeat.Stdout, StringComparison.Ordinal);
            Assert.Contains("To test another agent in this same folder, run ./reset.sh first.", repeat.Stdout, StringComparison.Ordinal);
            Assert.Contains("Final result:", repeat.Stdout, StringComparison.Ordinal);
            Assert.Contains("GREEN VERIFIED", repeat.Stdout, StringComparison.Ordinal);
            Assert.Contains("GREEN 100/100", repeat.Stdout, StringComparison.Ordinal);
            Assert.DoesNotContain("<span", repeat.Stdout, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Previous result card was not found", repeat.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void TrialPackage_CleanPlayableZipReportsMissingNodeAsDependency()
    {
        if (!CanRunPosixSmoke())
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-trial-clean-playable-missing-node-" + Guid.NewGuid().ToString("N"));
        try
        {
            var packageRoot = CreateExtractedPlayablePackage(tempRoot);
            var isolatedPath = CreateIsolatedToolPath(tempRoot, includeNode: false);
            AssertOutsideSourceCheckout(packageRoot);
            var workspaceRoot = Path.Combine(packageRoot, "agent-workspace");
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            var result = RunScoreSh(packageRoot, isolatedPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Missing dependency: Node.js is required for the official starter-pack task command.", result.Stderr, StringComparison.Ordinal);
            Assert.Contains("Install Node.js or put node on PATH", result.Stderr, StringComparison.Ordinal);
            Assert.DoesNotContain("Result: collection_failed", result.Stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public void TrialPackage_CleanPlayableZipReportsMissingBundledScorerWithoutGlobalFallback()
    {
        if (!CanRunPosixSmoke())
        {
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-trial-clean-playable-missing-" + Guid.NewGuid().ToString("N"));
        try
        {
            var packageRoot = CreateExtractedPlayablePackage(tempRoot);
            var isolatedPath = CreateIsolatedToolPath(tempRoot);
            Assert.False(File.Exists(Path.Combine(isolatedPath, "carves")));
            AssertOutsideSourceCheckout(packageRoot);
            var workspaceRoot = Path.Combine(packageRoot, "agent-workspace");
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);
            File.Move(
                Path.Combine(packageRoot, "tools", "carves", "carves"),
                Path.Combine(packageRoot, "tools", "carves", "carves.hidden"));

            var result = RunScoreSh(packageRoot, isolatedPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("CARVES scorer/service was not found.", result.Stderr, StringComparison.Ordinal);
            Assert.Contains("Missing scorer:", result.Stderr, StringComparison.Ordinal);
            Assert.Contains("no package-local scorer", result.Stderr, StringComparison.Ordinal);
            Assert.Contains("not a complete playable package", result.Stderr, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Missing dependency", result.Stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    private static string CreateExtractedPlayablePackage(string tempRoot)
    {
        var scorerRoot = Path.Combine(tempRoot, "scorer");
        var sourcePackageRoot = Path.Combine(tempRoot, "source-package");
        var zipPath = Path.Combine(tempRoot, "carves-agent-trial-pack-win-x64.zip");
        var extractedRoot = Path.Combine(tempRoot, "extracted-package");
        Directory.CreateDirectory(tempRoot);
        CopyMatrixScorerShim(scorerRoot);

        var packageResult = RunMatrixCli(
            "trial",
            "package",
            "--output",
            sourcePackageRoot,
            "--windows-playable",
            "--scorer-root",
            scorerRoot,
            "--zip-output",
            zipPath,
            "--runtime-identifier",
            "win-x64",
            "--build-label",
            "clean-smoke",
            "--json");
        Assert.Equal(0, packageResult.ExitCode);

        ZipFile.ExtractToDirectory(zipPath, extractedRoot);
        Assert.True(File.Exists(Path.Combine(extractedRoot, "tools", "carves", "carves.exe")));
        Assert.True(File.Exists(Path.Combine(extractedRoot, "tools", "carves", "carves")));
        Assert.True(File.Exists(Path.Combine(extractedRoot, "tools", "carves", "scorer-manifest.json")));
        Assert.True(File.Exists(Path.Combine(extractedRoot, "SCORE.cmd")));
        Assert.True(File.Exists(Path.Combine(extractedRoot, "score.sh")));
        Assert.True(File.Exists(Path.Combine(extractedRoot, "RESULT.cmd")));
        Assert.True(File.Exists(Path.Combine(extractedRoot, "result.sh")));
        Assert.True(File.Exists(Path.Combine(extractedRoot, "RESET.cmd")));
        Assert.True(File.Exists(Path.Combine(extractedRoot, "reset.sh")));
        return extractedRoot;
    }

    private static void CopyMatrixScorerShim(string scorerRoot)
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var sourceRoot = ResolveBuiltMatrixCliRoot(repoRoot);
        Assert.True(File.Exists(Path.Combine(sourceRoot, "carves-matrix.dll")), "Matrix CLI output must be built before this smoke runs.");
        CopyDirectory(sourceRoot, scorerRoot);

        var shimPath = Path.Combine(scorerRoot, "carves");
        File.WriteAllText(
            shimPath,
            """
            #!/usr/bin/env sh
            set -eu
            DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
            if [ "${1:-}" = "test" ] && [ "${2:-}" = "collect" ]; then
              shift 2
              exec dotnet "$DIR/carves-matrix.dll" trial collect "$@"
            fi
            if [ "${1:-}" = "test" ] && [ "${2:-}" = "verify" ]; then
              shift 2
              exec dotnet "$DIR/carves-matrix.dll" trial verify "$@"
            fi
            if [ "${1:-}" = "test" ] && [ "${2:-}" = "result" ]; then
              shift 2
              exec dotnet "$DIR/carves-matrix.dll" trial result "$@"
            fi
            echo "unsupported package-local scorer command: $*" >&2
            exit 2
            """ + Environment.NewLine);
        TryMakeExecutable(shimPath);
        File.WriteAllText(Path.Combine(scorerRoot, "carves.exe"), "test windows entrypoint placeholder");
        File.WriteAllText(
            Path.Combine(scorerRoot, "scorer-root-manifest.json"),
            """
            {
              "schema_version": "carves-windows-scorer-root.v0",
              "scorer_kind": "runtime_cli",
              "runtime_identifier": "win-x64",
              "entrypoint": "carves.exe",
              "target_project": "src/CARVES.Runtime.Cli/carves.csproj",
              "configuration": "Debug",
              "build_label": "clean-smoke",
              "self_contained": true,
              "requires_source_checkout_to_run": false,
              "requires_dotnet_to_run": false,
              "uses_dotnet_run": false,
              "supported_commands": ["test collect", "test reset", "test verify", "test result"]
            }
            """);
    }

    private static string ResolveBuiltMatrixCliRoot(string repoRoot)
    {
        var testOutputDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var currentConfiguration = testOutputDirectory.Parent?.Name;
        foreach (var configuration in new[] { currentConfiguration, "Release", "Debug" }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal))
        {
            var candidate = Path.Combine(repoRoot, "src", "CARVES.Matrix.Cli", "bin", configuration!, "net10.0");
            if (File.Exists(Path.Combine(candidate, "carves-matrix.dll")))
            {
                return candidate;
            }
        }

        return Path.Combine(repoRoot, "src", "CARVES.Matrix.Cli", "bin", currentConfiguration ?? "Debug", "net10.0");
    }

    private static string CreateIsolatedToolPath(string tempRoot, bool includeNode = true)
    {
        var toolRoot = Path.Combine(tempRoot, "isolated-path");
        Directory.CreateDirectory(toolRoot);
        LinkTool(toolRoot, "git");
        LinkTool(toolRoot, "dotnet");
        LinkTool(toolRoot, "grep");
        LinkTool(toolRoot, "dirname");
        LinkTool(toolRoot, "cat");
        LinkTool(toolRoot, "sh");
        if (includeNode)
        {
            LinkTool(toolRoot, "node");
        }

        return toolRoot;
    }

    private static void LinkTool(string toolRoot, string toolName)
    {
        var source = FindTool(toolName);
        var linkPath = Path.Combine(toolRoot, toolName);
        File.CreateSymbolicLink(linkPath, source);
    }

    private static string FindTool(string toolName)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var candidate = Path.Combine(directory, toolName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Required tool not found for smoke: {toolName}");
    }

    private static ProcessResult RunScoreSh(string packageRoot, string isolatedPath)
    {
        using var process = new Process();
        process.StartInfo.FileName = "/bin/sh";
        process.StartInfo.ArgumentList.Add("score.sh");
        process.StartInfo.WorkingDirectory = packageRoot;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.Environment["PATH"] = isolatedPath;
        process.StartInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("score.sh did not exit within the smoke timeout.");
        }

        return new ProcessResult(process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
    }

    private static void AssertOutsideSourceCheckout(string packageRoot)
    {
        var repoRoot = Path.GetFullPath(MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPackageRoot = Path.GetFullPath(packageRoot);
        Assert.False(fullPackageRoot.StartsWith(repoRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static void ApplyGoodBoundedEdit(string workspaceRoot)
    {
        var sourcePath = Path.Combine(workspaceRoot, "src", "bounded-fixture.js");
        var testPath = Path.Combine(workspaceRoot, "tests", "bounded-fixture.test.js");
        File.WriteAllText(
            sourcePath,
            File.ReadAllText(sourcePath)
                .Replace("return `${normalizedComponent}:${mode}`;", "return `component=${normalizedComponent}; mode=${mode}; trial=bounded`;", StringComparison.Ordinal));
        File.WriteAllText(
            testPath,
            File.ReadAllText(testPath)
                .Replace("collector:safe", "component=collector; mode=safe; trial=bounded", StringComparison.Ordinal)
                .Replace("unknown:standard", "component=unknown; mode=standard; trial=bounded", StringComparison.Ordinal));
    }

    private static void CopyAgentReportFixture(string workspaceRoot)
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "tests", "fixtures", "agent-trial-v1", "local-mvp-schema-examples", "agent-report.json");
        var destinationPath = Path.Combine(workspaceRoot, "artifacts", "agent-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void AssertSubmitBundle(string bundleRoot)
    {
        Assert.True(File.Exists(Path.Combine(bundleRoot, "matrix-artifact-manifest.json")));
        Assert.True(File.Exists(Path.Combine(bundleRoot, "matrix-proof-summary.json")));
        Assert.True(File.Exists(Path.Combine(bundleRoot, "trial", "task-contract.json")));
        Assert.True(File.Exists(Path.Combine(bundleRoot, "trial", "agent-report.json")));
        Assert.True(File.Exists(Path.Combine(bundleRoot, "trial", "diff-scope-summary.json")));
        Assert.True(File.Exists(Path.Combine(bundleRoot, "trial", "test-evidence.json")));
        Assert.True(File.Exists(Path.Combine(bundleRoot, "trial", "carves-agent-trial-result.json")));
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var destinationPath = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, sourcePath));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }

    private static void TryMakeExecutable(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private static bool CanRunPosixSmoke()
    {
        return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
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

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
