using System.IO.Compression;
using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialPortableZipSmokeTests
{
    [Fact]
    public void TrialPackage_ZipExtractedPackageScoresFromRootAndProducesVerifiedSubmitBundle()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-trial-zip-smoke-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var packageRoot = CreateExtractedPortablePackage(tempRoot);
            AssertPortableRootLayout(packageRoot);

            var workspaceRoot = Path.Combine(packageRoot, "agent-workspace");
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            Directory.SetCurrentDirectory(packageRoot);
            var result = RunMatrixCli("trial", "collect", "--json");

            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("--workspace", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("--history-root", result.StandardOutput, StringComparison.Ordinal);

            using var resultDocument = JsonDocument.Parse(result.StandardOutput);
            var root = resultDocument.RootElement;
            Assert.Equal("collect", root.GetProperty("command").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.True(root.GetProperty("offline").GetBoolean());
            Assert.False(root.GetProperty("server_submission").GetBoolean());
            Assert.True(root.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.Equal("scored", root.GetProperty("local_score").GetProperty("score_status").GetString());
            Assert.Equal(100, root.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
            Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "does_not_submit_to_server");
            Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_certification");
            Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_leaderboard_eligible");

            var bundleRoot = Path.Combine(packageRoot, "results", "submit-bundle");
            Assert.Equal(Path.GetFullPath(bundleRoot), root.GetProperty("bundle_root").GetString());
            AssertSubmitBundle(bundleRoot);

            var verify = RunMatrixCli("trial", "verify", "--bundle-root", bundleRoot, "--json");
            Assert.Equal(0, verify.ExitCode);
            using var verifyDocument = JsonDocument.Parse(verify.StandardOutput);
            Assert.Equal("verified", verifyDocument.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(tempRoot);
        }
    }

    [Theory]
    [InlineData("task_contract", "portable_task_contract_hash_mismatch")]
    [InlineData("baseline_metadata", "portable_baseline_metadata_invalid")]
    public void TrialPackage_ZipExtractedPackageTamperProbeFailsClosed(
        string tamperKind,
        string expectedReason)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-trial-zip-tamper-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var packageRoot = CreateExtractedPortablePackage(tempRoot);
            var workspaceRoot = Path.Combine(packageRoot, "agent-workspace");
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);
            TamperPackage(packageRoot, workspaceRoot, tamperKind);

            Directory.SetCurrentDirectory(packageRoot);
            var result = RunMatrixCli("trial", "collect", "--json");

            Assert.Equal(1, result.ExitCode);
            Assert.DoesNotContain("--workspace", result.StandardOutput, StringComparison.Ordinal);
            using var resultDocument = JsonDocument.Parse(result.StandardOutput);
            var root = resultDocument.RootElement;
            Assert.Equal("collection_failed", root.GetProperty("status").GetString());
            Assert.Contains(
                root.GetProperty("collection").GetProperty("failure_reasons").EnumerateArray(),
                reason => reason.GetString() == expectedReason);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(tempRoot);
        }
    }

    private static string CreateExtractedPortablePackage(string tempRoot)
    {
        var sourceRoot = Path.Combine(tempRoot, "source-package");
        var zipPath = Path.Combine(tempRoot, "carves-agent-trial-pack.zip");
        var extractedRoot = Path.Combine(tempRoot, "extracted-package");
        Directory.CreateDirectory(tempRoot);

        var packageResult = RunMatrixCli("trial", "package", "--output", sourceRoot, "--json");
        Assert.Equal(0, packageResult.ExitCode);

        ZipFile.CreateFromDirectory(sourceRoot, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);
        ZipFile.ExtractToDirectory(zipPath, extractedRoot);
        return extractedRoot;
    }

    private static void AssertPortableRootLayout(string packageRoot)
    {
        Assert.True(File.Exists(Path.Combine(packageRoot, "README-FIRST.md")));
        Assert.True(File.Exists(Path.Combine(packageRoot, "COPY_THIS_TO_AGENT_BLIND.txt")));
        Assert.True(File.Exists(Path.Combine(packageRoot, "COPY_THIS_TO_AGENT_GUIDED.txt")));
        Assert.True(File.Exists(Path.Combine(packageRoot, "SCORE.cmd")));
        Assert.True(File.Exists(Path.Combine(packageRoot, "score.sh")));
        Assert.True(Directory.Exists(Path.Combine(packageRoot, "agent-workspace")));
        Assert.True(Directory.Exists(Path.Combine(packageRoot, ".carves-pack")));
        Assert.True(Directory.Exists(Path.Combine(packageRoot, "results", "submit-bundle")));
        Assert.False(Directory.Exists(Path.Combine(packageRoot, "agent-workspace", ".carves-pack")));
        Assert.False(File.Exists(Path.Combine(packageRoot, "agent-workspace", "SCORE.cmd")));
        Assert.False(File.Exists(Path.Combine(packageRoot, "agent-workspace", "score.sh")));
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

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundleRoot, "matrix-proof-summary.json")));
        var summary = summaryDocument.RootElement;
        Assert.False(summary.GetProperty("public_claims").GetProperty("certification").GetBoolean());
        Assert.False(summary.GetProperty("public_claims").GetProperty("hosted_verification").GetBoolean());
        Assert.False(summary.GetProperty("public_claims").GetProperty("public_leaderboard").GetBoolean());
        Assert.True(summary.GetProperty("privacy").GetProperty("summary_only").GetBoolean());
        Assert.False(summary.GetProperty("privacy").GetProperty("hosted_api_required").GetBoolean());

        using var trialResultDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundleRoot, "trial", "carves-agent-trial-result.json")));
        Assert.Equal("scored", trialResultDocument.RootElement.GetProperty("local_score").GetProperty("score_status").GetString());
    }

    private static void TamperPackage(string packageRoot, string workspaceRoot, string tamperKind)
    {
        switch (tamperKind)
        {
            case "task_contract":
                File.AppendAllText(
                    Path.Combine(workspaceRoot, ".carves", "trial", "task-contract.json"),
                    Environment.NewLine + "tampered" + Environment.NewLine);
                break;
            case "baseline_metadata":
                File.AppendAllText(
                    Path.Combine(packageRoot, ".carves-pack", "baseline-manifest.json"),
                    Environment.NewLine + "tampered" + Environment.NewLine);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(tamperKind), tamperKind, null);
        }
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
