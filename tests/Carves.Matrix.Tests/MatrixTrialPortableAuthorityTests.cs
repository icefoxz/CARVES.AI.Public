using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialPortableAuthorityTests
{
    [Fact]
    public void TrialLocal_PortablePackageValidatesExternalAuthorityAndBaseline()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-valid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            var result = RunMatrixCli("trial", "local", "--workspace", workspaceRoot, "--json");

            Assert.Equal(0, result.ExitCode);
            using var resultDocument = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal("verified", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("collectable", resultDocument.RootElement.GetProperty("collection").GetProperty("local_collection_status").GetString());
            Assert.Equal(100, resultDocument.RootElement.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());

            using var baselineDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageRoot, ".carves-pack", "baseline-manifest.json")));
            var baseline = baselineDocument.RootElement;
            Assert.False(string.IsNullOrWhiteSpace(baseline.GetProperty("baseline_tree_sha").GetString()));
            Assert.True(baseline.GetProperty("initial_files").GetArrayLength() > 0);
            Assert.Contains(
                baseline.GetProperty("initial_files").EnumerateArray(),
                file => file.GetProperty("path").GetString() == ".carves/trial/task-contract.json"
                    && file.GetProperty("protected").GetBoolean());
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialCollect_NoArgumentScoresPortablePackageRoot()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-no-arg-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            Directory.SetCurrentDirectory(packageRoot);
            var result = RunMatrixCli("trial", "collect", "--json");

            Assert.Equal(0, result.ExitCode);
            using var resultDocument = JsonDocument.Parse(result.StandardOutput);
            var root = resultDocument.RootElement;
            Assert.Equal("collect", root.GetProperty("command").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.Equal(Path.GetFullPath(workspaceRoot), root.GetProperty("workspace_root").GetString());
            Assert.Equal(Path.Combine(Path.GetFullPath(packageRoot), "results", "submit-bundle"), root.GetProperty("bundle_root").GetString());
            Assert.True(root.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.Equal(100, root.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
            Assert.True(File.Exists(Path.Combine(packageRoot, "results", "submit-bundle", "matrix-artifact-manifest.json")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "results", "local", "matrix-agent-trial-result-card.md")));
            Assert.DoesNotContain("--workspace", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(packageRoot);
        }
    }

    [Theory]
    [InlineData(".carves/trial/task-contract.json", "portable_task_contract_hash_mismatch")]
    [InlineData(".carves/trial/instruction-pack.json", "portable_instruction_pack_hash_mismatch")]
    [InlineData("AGENTS.md", "portable_protected_metadata_changed")]
    public void TrialLocal_PortablePackageFailsClosedWhenWorkspaceAuthorityInputsChange(
        string workspaceRelativePath,
        string expectedReason)
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-tamper-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);
            File.AppendAllText(
                Path.Combine(workspaceRoot, workspaceRelativePath.Replace('/', Path.DirectorySeparatorChar)),
                Environment.NewLine + "tampered" + Environment.NewLine);

            var result = RunMatrixCli("trial", "local", "--workspace", workspaceRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            using var resultDocument = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal("collection_failed", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Contains(
                resultDocument.RootElement.GetProperty("collection").GetProperty("failure_reasons").EnumerateArray(),
                reason => reason.GetString() == expectedReason);
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialLocal_PortablePackageFailsClosedWhenBaselineMetadataIsMissing()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-missing-baseline-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);
            File.Delete(Path.Combine(packageRoot, ".carves-pack", "baseline-manifest.json"));

            var result = RunMatrixCli("trial", "local", "--workspace", workspaceRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            using var resultDocument = JsonDocument.Parse(result.StandardOutput);
            Assert.Contains(
                resultDocument.RootElement.GetProperty("collection").GetProperty("failure_reasons").EnumerateArray(),
                reason => reason.GetString() == "portable_baseline_manifest_missing");
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialLocal_PortablePackageFailsClosedWhenGitBaselineIsMissing()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-missing-git-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);
            DeleteDirectory(Path.Combine(workspaceRoot, ".git"));

            var result = RunMatrixCli("trial", "local", "--workspace", workspaceRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            using var resultDocument = JsonDocument.Parse(result.StandardOutput);
            Assert.Contains(
                resultDocument.RootElement.GetProperty("collection").GetProperty("failure_reasons").EnumerateArray(),
                reason => reason.GetString() == "portable_baseline_git_missing");
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    private static string PreparePortableWorkspace(string packageRoot)
    {
        var packageResult = RunMatrixCli("trial", "package", "--output", packageRoot, "--json");
        Assert.Equal(0, packageResult.ExitCode);
        return Path.Combine(packageRoot, "agent-workspace");
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
