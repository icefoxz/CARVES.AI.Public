using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialFirstRunValidationTests
{
    [Fact]
    public void TrialDemo_DefaultFirstRunStaysUnderCarvesTrialsAndUsesStrictVerifier()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "carves-trial-first-run-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(workingDirectory);

        try
        {
            Directory.SetCurrentDirectory(workingDirectory);
            var result = RunMatrixCli(
                "trial",
                "demo",
                "--pack-root",
                ResolvePublicPackRoot(),
                "--run-id",
                "first-run",
                "--json");

            Assert.Equal(0, result.ExitCode);
            var defaultTrialRoot = Path.Combine(workingDirectory, "carves-trials");
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("demo", root.GetProperty("command").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            AssertLocalOnlyNonClaims(root);

            var runRoot = root.GetProperty("run_root").GetString()!;
            var workspaceRoot = root.GetProperty("workspace_root").GetString()!;
            var bundleRoot = root.GetProperty("bundle_root").GetString()!;
            var historyRoot = root.GetProperty("history_root").GetString()!;
            AssertPathUnder(defaultTrialRoot, runRoot);
            AssertPathUnder(defaultTrialRoot, workspaceRoot);
            AssertPathUnder(defaultTrialRoot, bundleRoot);
            Assert.Equal(Path.Combine(defaultTrialRoot, "history"), historyRoot);

            Assert.True(root.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.Equal("matrix-agent-trial-result-card.md", root.GetProperty("result_card").GetProperty("card_path").GetString());
            Assert.Equal("runs/first-run.json", root.GetProperty("history_entry_ref").GetString());
            Assert.True(File.Exists(Path.Combine(bundleRoot, "matrix-agent-trial-result-card.md")));
            Assert.True(File.Exists(Path.Combine(historyRoot, "runs", "first-run.json")));
            Assert.True(File.Exists(Path.Combine(defaultTrialRoot, "latest.json")));
            AssertOutputOnlyUnderDefaultTrialRoot(workingDirectory, defaultTrialRoot);

            var verify = RunMatrixCli("trial", "verify", "--trial-root", defaultTrialRoot, "--json");
            Assert.Equal(0, verify.ExitCode);
            using (var verifyDocument = JsonDocument.Parse(verify.StandardOutput))
            {
                Assert.Equal("verified", verifyDocument.RootElement.GetProperty("status").GetString());
                Assert.True(verifyDocument.RootElement.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            }

            File.AppendAllText(Path.Combine(bundleRoot, "trial", "test-evidence.json"), Environment.NewLine);
            var tampered = RunMatrixCli("trial", "verify", "--trial-root", defaultTrialRoot, "--json");

            Assert.Equal(1, tampered.ExitCode);
            using var tamperedDocument = JsonDocument.Parse(tampered.StandardOutput);
            var tamperedRoot = tamperedDocument.RootElement;
            Assert.Equal("verification_failed", tamperedRoot.GetProperty("status").GetString());
            Assert.False(tamperedRoot.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.Contains(
                tamperedRoot.GetProperty("verification").GetProperty("reason_codes").EnumerateArray(),
                reason => reason.GetString() == "hash_mismatch");
            Assert.Contains(
                tamperedRoot.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "trial_verify_hash_mismatch");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(workingDirectory);
        }
    }

    [Fact]
    public void TrialPlay_DemoAgentFirstRunIsNonInteractiveLocalOnlyAndVerified()
    {
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-trial-play-first-run-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "play",
                "--trial-root",
                trialRoot,
                "--run-id",
                "agent-demo",
                "--demo-agent",
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("play", root.GetProperty("command").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            AssertLocalOnlyNonClaims(root);
            Assert.True(root.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.Equal("runs/agent-demo.json", root.GetProperty("history_entry_ref").GetString());
            Assert.True(File.Exists(Path.Combine(trialRoot, "history", "runs", "agent-demo.json")));
            Assert.True(File.Exists(Path.Combine(trialRoot, "latest.json")));
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    private static void AssertLocalOnlyNonClaims(JsonElement root)
    {
        Assert.True(root.GetProperty("offline").GetBoolean());
        Assert.False(root.GetProperty("server_submission").GetBoolean());
        var nonClaims = root.GetProperty("non_claims").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("local_only", nonClaims);
        Assert.Contains("does_not_submit_to_server", nonClaims);
        Assert.Contains("not_server_receipt", nonClaims);
        Assert.Contains("not_leaderboard_eligible", nonClaims);
        Assert.Contains("not_certification", nonClaims);
    }

    private static void AssertOutputOnlyUnderDefaultTrialRoot(string workingDirectory, string defaultTrialRoot)
    {
        var entries = Directory.EnumerateFileSystemEntries(workingDirectory).ToArray();
        Assert.Single(entries);
        Assert.Equal(defaultTrialRoot, entries[0]);
    }

    private static void AssertPathUnder(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        Assert.False(relative.StartsWith("..", StringComparison.Ordinal), $"{path} should stay under {root}");
        Assert.False(Path.IsPathRooted(relative), $"{path} should stay under {root}");
    }

    private static string ResolvePublicPackRoot()
    {
        return Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "docs",
            "matrix",
            "starter-packs",
            "official-agent-dev-safety-v1-local-mvp");
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
