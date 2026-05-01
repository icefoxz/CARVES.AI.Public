using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class CarvesTestFirstRunValidationTests
{
    [Fact]
    public void CarvesTestVerifyAndResult_ReportFriendlyLatestDiagnostics()
    {
        var repoRoot = LocateSourceRepoRoot();
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-test-latest-diagnostic-" + Guid.NewGuid().ToString("N"));
        try
        {
            var missing = CliProgramHarness.RunInDirectory(repoRoot, "test", "verify", "--trial-root", trialRoot, "--json");

            Assert.Equal(1, missing.ExitCode);
            using (var document = JsonDocument.Parse(missing.StandardOutput))
            {
                Assert.Equal("failed", document.RootElement.GetProperty("status").GetString());
                Assert.Contains(
                    document.RootElement.GetProperty("diagnostics").EnumerateArray(),
                    diagnostic => diagnostic.GetProperty("code").GetString() == "trial_latest_missing");
            }

            Directory.CreateDirectory(trialRoot);
            File.WriteAllText(Path.Combine(trialRoot, "latest.json"), "{}");
            var corrupt = CliProgramHarness.RunInDirectory(repoRoot, "test", "result", "--trial-root", trialRoot, "--json");

            Assert.Equal(1, corrupt.ExitCode);
            using var corruptDocument = JsonDocument.Parse(corrupt.StandardOutput);
            Assert.Equal("failed", corruptDocument.RootElement.GetProperty("status").GetString());
            Assert.Contains(
                corruptDocument.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "trial_latest_invalid");
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void CarvesTestDemo_FirstRunKeepsRuntimeAsThinAliasOverMatrixVerification()
    {
        var repoRoot = LocateSourceRepoRoot();
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-test-first-run-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = CliProgramHarness.RunInDirectory(
                repoRoot,
                "test",
                "demo",
                "--trial-root",
                trialRoot,
                "--run-id",
                "first-run",
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("demo", root.GetProperty("command").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.True(root.GetProperty("offline").GetBoolean());
            Assert.False(root.GetProperty("server_submission").GetBoolean());
            Assert.True(root.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.Equal(0, root.GetProperty("verification").GetProperty("exit_code").GetInt32());
            Assert.Equal(100, root.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
            Assert.Contains(
                root.GetProperty("non_claims").EnumerateArray(),
                item => item.GetString() == "not_server_receipt");
            Assert.Contains(
                root.GetProperty("non_claims").EnumerateArray(),
                item => item.GetString() == "not_leaderboard_eligible");
            Assert.Contains(
                root.GetProperty("non_claims").EnumerateArray(),
                item => item.GetString() == "not_certification");

            var bundleRoot = root.GetProperty("bundle_root").GetString()!;
            Assert.StartsWith(Path.GetFullPath(trialRoot), bundleRoot, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(bundleRoot, "matrix-artifact-manifest.json")));
            Assert.True(File.Exists(Path.Combine(bundleRoot, "matrix-proof-summary.json")));
            Assert.True(File.Exists(Path.Combine(bundleRoot, "trial", "carves-agent-trial-result.json")));
            Assert.True(File.Exists(Path.Combine(trialRoot, "history", "runs", "first-run.json")));
            Assert.True(File.Exists(Path.Combine(trialRoot, "latest.json")));
            Assert.DoesNotContain("\"certification\":true", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"public_leaderboard\":true", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(trialRoot);
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
}
