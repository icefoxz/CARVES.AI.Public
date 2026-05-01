using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class CarvesTestEntryTests
{
    [Fact]
    public void Help_IncludesCarvesTestAgentTrialEntry()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("carves test [demo|agent|package|collect|verify|result|history|compare]", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void CarvesTestGuide_LabelsAgentTrialAndRejectsGenericUnitTestMeaning()
    {
        var repoRoot = LocateSourceRepoRoot();

        var result = CliProgramHarness.RunInDirectory(repoRoot, "test");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CARVES Agent Trial local test", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("not generic project unit tests", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CARVES.Matrix.Core", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void CarvesTestDemoJson_DelegatesToMatrixVerifiedPath()
    {
        var repoRoot = LocateSourceRepoRoot();
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-test-demo-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = CliProgramHarness.RunInDirectory(
                repoRoot,
                "test",
                "demo",
                "--trial-root",
                trialRoot,
                "--run-id",
                "public-demo",
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("demo", root.GetProperty("command").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.Equal(100, root.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
            Assert.True(root.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_certification");
            Assert.True(File.Exists(Path.Combine(trialRoot, "latest.json")));
            Assert.True(File.Exists(Path.Combine(root.GetProperty("bundle_root").GetString()!, "matrix-artifact-manifest.json")));
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void CarvesTestLatestVerifyAndCompare_UseLatestConvenience()
    {
        var repoRoot = LocateSourceRepoRoot();
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-test-latest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var first = CliProgramHarness.RunInDirectory(repoRoot, "test", "demo", "--trial-root", trialRoot, "--run-id", "first", "--json");
            var second = CliProgramHarness.RunInDirectory(repoRoot, "test", "demo", "--trial-root", trialRoot, "--run-id", "second", "--json");
            var result = CliProgramHarness.RunInDirectory(repoRoot, "test", "result", "--trial-root", trialRoot, "--json");
            var verify = CliProgramHarness.RunInDirectory(repoRoot, "test", "verify", "--trial-root", trialRoot, "--json");
            var compare = CliProgramHarness.RunInDirectory(
                repoRoot,
                "test",
                "compare",
                "--trial-root",
                trialRoot,
                "--baseline",
                "first",
                "--target",
                "second",
                "--json");

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(0, second.ExitCode);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(0, verify.ExitCode);
            Assert.Equal(0, compare.ExitCode);

            using var latestDocument = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal("latest_found", latestDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("second", latestDocument.RootElement.GetProperty("latest").GetProperty("run_id").GetString());

            using var verifyDocument = JsonDocument.Parse(verify.StandardOutput);
            Assert.Equal("verified", verifyDocument.RootElement.GetProperty("status").GetString());

            using var compareDocument = JsonDocument.Parse(compare.StandardOutput);
            Assert.Equal("compare", compareDocument.RootElement.GetProperty("command").GetString());
            Assert.Equal("compared", compareDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("first", compareDocument.RootElement.GetProperty("baseline_run_id").GetString());
            Assert.Equal("second", compareDocument.RootElement.GetProperty("target_run_id").GetString());
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void CarvesTestAgentDemoAgent_DelegatesToMatrixPlayPath()
    {
        var repoRoot = LocateSourceRepoRoot();
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-test-agent-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = CliProgramHarness.RunInDirectory(
                repoRoot,
                "test",
                "agent",
                "--trial-root",
                trialRoot,
                "--run-id",
                "agent-demo",
                "--demo-agent",
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal("play", document.RootElement.GetProperty("command").GetString());
            Assert.Equal("verified", document.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void CarvesTestPackageJson_DelegatesToMatrixPortablePackageWriter()
    {
        var repoRoot = LocateSourceRepoRoot();
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-test-package-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = CliProgramHarness.RunInDirectory(
                repoRoot,
                "test",
                "package",
                "--output",
                packageRoot,
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("package", root.GetProperty("command").GetString());
            Assert.Equal("prepared", root.GetProperty("status").GetString());
            Assert.Equal(Path.Combine(Path.GetFullPath(packageRoot), "agent-workspace"), root.GetProperty("agent_workspace_root").GetString());
            Assert.True(File.Exists(Path.Combine(packageRoot, ".carves-pack", "pack-manifest.json")));
            Assert.True(Directory.Exists(Path.Combine(packageRoot, "agent-workspace", ".git")));
            Assert.DoesNotContain("--workspace", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void CarvesTestCollectJson_ScoresPortablePackageFromPackageRoot()
    {
        var repoRoot = LocateSourceRepoRoot();
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-test-collect-package-" + Guid.NewGuid().ToString("N"));
        try
        {
            var package = CliProgramHarness.RunInDirectory(
                repoRoot,
                "test",
                "package",
                "--output",
                packageRoot,
                "--json");
            Assert.Equal(0, package.ExitCode);

            var workspaceRoot = Path.Combine(packageRoot, "agent-workspace");
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(repoRoot, workspaceRoot);

            var result = CliProgramHarness.RunInDirectory(packageRoot, "test", "collect", "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("collect", root.GetProperty("command").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.Equal(Path.GetFullPath(workspaceRoot), root.GetProperty("workspace_root").GetString());
            Assert.Equal(Path.Combine(Path.GetFullPath(packageRoot), "results", "submit-bundle"), root.GetProperty("bundle_root").GetString());
            Assert.Equal(100, root.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
            Assert.True(root.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.True(File.Exists(Path.Combine(packageRoot, "results", "submit-bundle", "matrix-artifact-manifest.json")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "results", "local", "matrix-agent-trial-result-card.md")));
            Assert.DoesNotContain("--workspace", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    private static string LocateSourceRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CARVES.Runtime.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root.");
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

    private static void CopyAgentReportFixture(string repoRoot, string workspaceRoot)
    {
        var sourcePath = Path.Combine(repoRoot, "tests", "fixtures", "agent-trial-v1", "local-mvp-schema-examples", "agent-report.json");
        var destinationPath = Path.Combine(workspaceRoot, "artifacts", "agent-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }
}
