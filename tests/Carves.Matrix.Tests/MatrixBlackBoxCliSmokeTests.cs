using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Carves.Matrix.Tests;

public sealed class MatrixBlackBoxCliSmokeTests
{
    private const string NativeQuickstartArtifactRoot = "artifacts/matrix/native-quickstart";

    [Fact]
    public void BlackBoxProofCli_NativeQuickstartCommandShapeWritesVerifyCompatibleBundle()
    {
        var workspaceRoot = CreateWorkspaceRoot("carves-matrix-black-box-proof-");
        var artifactRoot = Path.Combine(workspaceRoot, NativeQuickstartArtifactRoot.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            var proof = RunCarvesMatrixCli(
                "proof",
                "--lane",
                "native-minimal",
                "--artifact-root",
                artifactRoot,
                "--configuration",
                "Debug",
                "--json");

            Assert.Equal(0, proof.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(proof.StandardError), proof.CombinedOutput);
            using var proofDocument = JsonDocument.Parse(proof.StandardOutput);
            var proofRoot = proofDocument.RootElement;
            Assert.Equal("matrix-native-proof.v0", proofRoot.GetProperty("schema_version").GetString());
            Assert.Equal("verified", proofRoot.GetProperty("status").GetString());
            Assert.Equal("native_minimal", proofRoot.GetProperty("proof_mode").GetString());
            var capabilities = proofRoot.GetProperty("proof_capabilities");
            Assert.Equal("native_minimal", capabilities.GetProperty("proof_lane").GetString());
            Assert.Equal("dotnet_runner_chain", capabilities.GetProperty("execution_backend").GetString());
            Assert.False(capabilities.GetProperty("coverage").GetProperty("packaged_install").GetBoolean());
            Assert.False(capabilities.GetProperty("coverage").GetProperty("full_release").GetBoolean());
            Assert.False(capabilities.GetProperty("requirements").GetProperty("powershell").GetBoolean());
            Assert.Empty(proofRoot.GetProperty("reason_codes").EnumerateArray());

            var verify = RunCarvesMatrixCli("verify", artifactRoot, "--json");
            Assert.Equal(0, verify.ExitCode);
            using var verifyDocument = JsonDocument.Parse(verify.StandardOutput);
            Assert.Equal("matrix-verify.v0", verifyDocument.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal("verified", verifyDocument.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            DeleteWorkspaceRoot(workspaceRoot);
        }
    }

    [Fact]
    public void BlackBoxProofCli_LegacyJsonShorthandStillSelectsNativeMinimalLane()
    {
        var workspaceRoot = CreateWorkspaceRoot("carves-matrix-black-box-proof-legacy-");
        var artifactRoot = Path.Combine(workspaceRoot, NativeQuickstartArtifactRoot.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            var proof = RunCarvesMatrixCli(
                "proof",
                "--artifact-root",
                artifactRoot,
                "--configuration",
                "Debug",
                "--json");

            Assert.Equal(0, proof.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(proof.StandardError), proof.CombinedOutput);
            using var proofDocument = JsonDocument.Parse(proof.StandardOutput);
            Assert.Equal("native_minimal", proofDocument.RootElement.GetProperty("proof_mode").GetString());
        }
        finally
        {
            DeleteWorkspaceRoot(workspaceRoot);
        }
    }

    [Fact]
    public void BlackBoxProofCli_InvalidLaneFailsAsUsage()
    {
        var result = RunCarvesMatrixCli("proof", "--lane", "native-project", "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid --lane value", result.StandardError, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.CombinedOutput);
    }

    [Fact]
    public void BlackBoxProofCli_InvalidConfigurationFailsAsUsage()
    {
        var result = RunCarvesMatrixCli("proof", "--lane", "native-minimal", "--configuration", "RelWithDebInfo", "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid --configuration value", result.StandardError, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.CombinedOutput);
    }

    [Fact]
    public void BlackBoxProofCli_NativeMinimalRejectsRuntimeRoot()
    {
        var result = RunCarvesMatrixCli("proof", "--lane", "native-minimal", "--runtime-root", ".", "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--runtime-root is not supported by proof --lane native-minimal", result.StandardError, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.CombinedOutput);
    }

    [Fact]
    public void BlackBoxProofCli_FullReleaseRejectsNativeWorkRoot()
    {
        var result = RunCarvesMatrixCli("proof", "--lane", "full-release", "--work-root", "artifacts/matrix/work");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--work-root is only supported by proof --lane native-minimal", result.StandardError, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.CombinedOutput);
    }

    [Fact]
    public void BlackBoxProofCli_FullReleaseRejectsNativeKeepFlag()
    {
        var result = RunCarvesMatrixCli("proof", "--lane", "full-release", "--keep");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--keep is only supported by proof --lane native-minimal", result.StandardError, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.CombinedOutput);
    }

    [Fact]
    public void BlackBoxProofCli_RejectsScriptPassthroughOptions()
    {
        var result = RunCarvesMatrixCli("proof", "--lane", "native-minimal", "--tool-mode", "Project", "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--tool-mode is not supported by proof", result.StandardError, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardOutput), result.CombinedOutput);
    }

    [Fact]
    public void BlackBoxVerifyCli_ValidBundlePassesWithMatrixVerifyJson()
    {
        var workspaceRoot = CreateWorkspaceRoot("carves-matrix-black-box-verify-");
        var artifactRoot = Path.Combine(workspaceRoot, NativeQuickstartArtifactRoot.Replace('/', Path.DirectorySeparatorChar));
        using var bundle = MatrixBundleFixture.Create(artifactRoot: artifactRoot);

        try
        {
            var result = RunCarvesMatrixCli("verify", bundle.ArtifactRoot, "--json");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.CombinedOutput);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("matrix-verify.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.Equal("verified", root.GetProperty("verification_posture").GetString());
            Assert.Equal(0, root.GetProperty("issue_count").GetInt32());
            Assert.Empty(root.GetProperty("reason_codes").EnumerateArray());
        }
        finally
        {
            DeleteWorkspaceRoot(workspaceRoot);
        }
    }

    [Fact]
    public void BlackBoxVerifyCli_MutatedBundleFailsWithHashMismatch()
    {
        var workspaceRoot = CreateWorkspaceRoot("carves-matrix-black-box-mutated-");
        var artifactRoot = Path.Combine(workspaceRoot, NativeQuickstartArtifactRoot.Replace('/', Path.DirectorySeparatorChar));
        using var bundle = MatrixBundleFixture.Create(artifactRoot: artifactRoot);
        try
        {
            File.AppendAllText(Path.Combine(bundle.ArtifactRoot, "project", "shield-evidence.json"), "\n{\"mutated\":true}");

            var result = RunCarvesMatrixCli("verify", bundle.ArtifactRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.CombinedOutput);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("matrix-verify.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("failed", root.GetProperty("status").GetString());
            Assert.Contains(
                root.GetProperty("reason_codes").EnumerateArray(),
                code => code.GetString() == "hash_mismatch");
            Assert.Contains(
                root.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("artifact_kind").GetString() == "audit_evidence"
                         && issue.GetProperty("reason_code").GetString() == "hash_mismatch");
        }
        finally
        {
            DeleteWorkspaceRoot(workspaceRoot);
        }
    }

    private static CommandResult RunCarvesMatrixCli(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(LocateBuiltMatrixCliDll());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start carves-matrix CLI process.");
        using var standardOutputClosed = new ManualResetEventSlim();
        using var standardErrorClosed = new ManualResetEventSlim();
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => AppendProcessLine(standardOutput, standardOutputClosed, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => AppendProcessLine(standardError, standardErrorClosed, eventArgs.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(60_000))
        {
            TryKill(process);
            throw new TimeoutException("carves-matrix CLI process did not exit within the expected timeout.");
        }

        standardOutputClosed.Wait(TimeSpan.FromSeconds(5));
        standardErrorClosed.Wait(TimeSpan.FromSeconds(5));
        return new CommandResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private static string LocateBuiltMatrixCliDll()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = string.Equals(baseDirectory.Name, "net10.0", StringComparison.Ordinal)
            ? baseDirectory.Parent?.Name
            : null;

        var candidates = new[]
        {
            configuration is null
                ? null
                : Path.Combine(repoRoot, "src", "CARVES.Matrix.Cli", "bin", configuration, "net10.0", "carves-matrix.dll"),
            Path.Combine(AppContext.BaseDirectory, "carves-matrix.dll"),
        };

        return candidates.FirstOrDefault(path => path is not null && File.Exists(path))
            ?? throw new FileNotFoundException("Unable to locate built carves-matrix.dll. The Matrix CLI project reference should build it before this smoke runs.");
    }

    private static string CreateWorkspaceRoot(string prefix)
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        return workspaceRoot;
    }

    private static void DeleteWorkspaceRoot(string workspaceRoot)
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static void AppendProcessLine(StringBuilder builder, ManualResetEventSlim completed, string? line)
    {
        if (line is null)
        {
            completed.Set();
            return;
        }

        builder.AppendLine(line);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }
        catch
        {
        }
    }

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Concat(StandardOutput, StandardError);
    }
}
