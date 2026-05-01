using Carves.Matrix.Core;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class MatrixNativeMinimalProofTests
{
    [Fact]
    public void MatrixProofJson_RunsNativeMinimalLaneAndWritesVerifyCompatibleBundle()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-proof-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli("proof", "--lane", "native-minimal", "--artifact-root", artifactRoot, "--configuration", "Debug", "--json");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("matrix-native-proof.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.Equal("native_minimal", root.GetProperty("proof_mode").GetString());
            Assert.Equal(0, root.GetProperty("exit_code").GetInt32());
            Assert.Empty(root.GetProperty("reason_codes").EnumerateArray());

            var stepIds = root.GetProperty("steps")
                .EnumerateArray()
                .Select(step => step.GetProperty("step_id").GetString())
                .ToArray();
            Assert.Contains("guard_init", stepIds);
            Assert.Contains("guard_check", stepIds);
            Assert.Contains("handoff_draft", stepIds);
            Assert.Contains("audit_evidence", stepIds);
            Assert.Contains("shield_evaluate", stepIds);
            Assert.Contains("shield_badge", stepIds);

            foreach (var relativePath in RequiredNativeArtifacts)
            {
                Assert.True(File.Exists(Path.Combine(artifactRoot, relativePath)), $"Missing native artifact: {relativePath}");
            }

            using var matrixSummaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifactRoot, "project", "matrix-summary.json")));
            var matrixSummary = matrixSummaryDocument.RootElement;
            Assert.Equal("matrix-summary.v0", matrixSummary.GetProperty("schema_version").GetString());
            Assert.Equal("native_minimal", matrixSummary.GetProperty("proof_mode").GetString());
            Assert.Equal("shield", matrixSummary.GetProperty("scoring_owner").GetString());
            Assert.False(matrixSummary.GetProperty("alters_shield_score").GetBoolean());
            Assert.True(matrixSummary.GetProperty("privacy").GetProperty("summary_only").GetBoolean());
            Assert.False(matrixSummary.GetProperty("public_claims").GetProperty("certification").GetBoolean());

            var verify = RunMatrixCli("verify", artifactRoot, "--json");
            Assert.Equal(0, verify.ExitCode);
            using var verifyDocument = JsonDocument.Parse(verify.StandardOutput);
            Assert.Equal("verified", verifyDocument.RootElement.GetProperty("status").GetString());
            Assert.True(verifyDocument.RootElement.GetProperty("trust_chain_hardening").GetProperty("gates_satisfied").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixProofJson_FailureUsesStableReasonCodesWhenWorkRootIsInvalid()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-proof-fail-test-" + Guid.NewGuid().ToString("N"));
        var invalidWorkRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-native-proof-work-root-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(invalidWorkRoot, "not a directory");
        try
        {
            var result = RunMatrixCli("proof", "--lane", "native-minimal", "--artifact-root", artifactRoot, "--work-root", invalidWorkRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("matrix-native-proof.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("failed", root.GetProperty("status").GetString());
            Assert.Equal("work_repo_setup", root.GetProperty("failed_step_id").GetString());
            Assert.Contains(
                root.GetProperty("reason_codes").EnumerateArray(),
                code => code.GetString() == "native_work_repo_setup_failed");
        }
        finally
        {
            if (File.Exists(invalidWorkRoot))
            {
                File.Delete(invalidWorkRoot);
            }

            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixProofJson_IsNativePathAndFullProofLaneRemainsAvailable()
    {
        var repoRoot = LocateSourceRepoRoot();
        var proofPath = ReadMatrixCoreSource(repoRoot, "MatrixProofCommand.cs");
        var nativePath = ReadMatrixCoreSource(repoRoot, "MatrixNativeProofCommand.cs");
        var nativeRepositorySetup = ReadMatrixCoreSource(repoRoot, "MatrixNativeProofRepositorySetup.cs");
        var nativeProductChain = ReadMatrixCoreSource(repoRoot, "MatrixNativeProofProductChain.cs");
        var nativeFinalization = ReadMatrixCoreSource(repoRoot, "MatrixNativeProofFinalization.cs");
        var usage = ReadMatrixCoreSource(repoRoot, "MatrixCliUsage.cs");

        Assert.Contains("ResolveProofLane(options)", proofPath, StringComparison.Ordinal);
        Assert.Contains("options.Json ? MatrixProofLane.NativeMinimal : MatrixProofLane.FullRelease", proofPath, StringComparison.Ordinal);
        Assert.Contains("InvokeScript(", proofPath, StringComparison.Ordinal);
        Assert.Contains("GuardCliRunner.Run", nativeRepositorySetup, StringComparison.Ordinal);
        Assert.Contains("GuardCliRunner.Run", nativeProductChain, StringComparison.Ordinal);
        Assert.Contains("HandoffCliRunner.Run", nativeProductChain, StringComparison.Ordinal);
        Assert.Contains("AuditCliRunner.Run", nativeProductChain, StringComparison.Ordinal);
        Assert.Contains("ShieldCliRunner.Run", nativeProductChain, StringComparison.Ordinal);
        Assert.Contains("matrix-native-proof.v0", nativeFinalization, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeScript", nativePath, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveMatrixScript", nativePath, StringComparison.Ordinal);
        Assert.DoesNotContain("pwsh", nativePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("matrix-e2e-smoke.ps1", nativePath, StringComparison.Ordinal);
        Assert.DoesNotContain("matrix-packaged-install-smoke.ps1", nativePath, StringComparison.Ordinal);
        Assert.Contains("Proof --lane native-minimal runs the native minimal", usage, StringComparison.Ordinal);
        Assert.Contains("Proof --lane full-release runs the full source-repo PowerShell proof lane", usage, StringComparison.Ordinal);
        Assert.Contains("proof --json still selects native-minimal", usage, StringComparison.Ordinal);
    }

    private static readonly string[] RequiredNativeArtifacts =
    [
        "project/decisions.jsonl",
        "project/handoff.json",
        "project/shield-evidence.json",
        "project/shield-evaluate.json",
        "project/shield-badge.json",
        "project/shield-badge.svg",
        "project/matrix-summary.json",
        "matrix-proof-summary.json",
        "matrix-artifact-manifest.json",
    ];

    private static MatrixCliRunResult RunMatrixCli(params string[] arguments)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = MatrixCliRunner.Run(arguments);
            return new MatrixCliRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
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

    private static string ReadMatrixCoreSource(string repoRoot, string fileName)
    {
        return File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Matrix.Core", fileName));
    }

    private sealed record MatrixCliRunResult(int ExitCode, string StandardOutput, string StandardError);
}
