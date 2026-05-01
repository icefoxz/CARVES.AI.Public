using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialOneCommandTests
{
    [Fact]
    public void TrialDemo_CreatesVerifiedLocalRunUnderTrialRoot()
    {
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-trial-demo-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "demo",
                "--trial-root",
                trialRoot,
                "--run-id",
                "day-1",
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("demo", root.GetProperty("command").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.True(root.GetProperty("offline").GetBoolean());
            Assert.False(root.GetProperty("server_submission").GetBoolean());
            Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_server_receipt");
            Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_leaderboard_eligible");
            Assert.Equal(100, root.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
            Assert.Equal("verified", root.GetProperty("verification").GetProperty("status").GetString());
            Assert.True(root.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());

            var runRoot = root.GetProperty("run_root").GetString()!;
            var workspaceRoot = root.GetProperty("workspace_root").GetString()!;
            var bundleRoot = root.GetProperty("bundle_root").GetString()!;
            var historyRoot = root.GetProperty("history_root").GetString()!;
            Assert.Equal(Path.Combine(Path.GetFullPath(trialRoot), "run-day-1"), runRoot);
            Assert.True(File.Exists(Path.Combine(workspaceRoot, "artifacts", "agent-report.json")));
            Assert.True(File.Exists(Path.Combine(bundleRoot, "matrix-artifact-manifest.json")));
            Assert.True(File.Exists(Path.Combine(bundleRoot, "matrix-agent-trial-result-card.md")));
            Assert.True(File.Exists(Path.Combine(historyRoot, "runs", "day-1.json")));
            Assert.False(File.Exists(Path.Combine(bundleRoot, "latest.json")));

            var latestPath = Path.Combine(trialRoot, "latest.json");
            Assert.True(File.Exists(latestPath));
            using var latestDocument = JsonDocument.Parse(File.ReadAllText(latestPath));
            var latest = latestDocument.RootElement;
            Assert.Equal("day-1", latest.GetProperty("run_id").GetString());
            Assert.Equal("verified", latest.GetProperty("status").GetString());
            Assert.Equal(workspaceRoot, latest.GetProperty("workspace_root").GetString());
            Assert.Equal(bundleRoot, latest.GetProperty("bundle_root").GetString());
            Assert.True(latest.GetProperty("non_authoritative").GetBoolean());
            Assert.False(latest.GetProperty("manifest_covered").GetBoolean());
            Assert.Equal(
                Path.Combine(bundleRoot, "matrix-agent-trial-result-card.md"),
                latest.GetProperty("result_card_path").GetString());
            Assert.Equal(
                Path.Combine(historyRoot, "runs/day-1.json"),
                latest.GetProperty("history_entry_path").GetString());
            Assert.Contains(latest.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_manifest_covered");

            var verify = RunMatrixCli(
                "trial",
                "verify",
                "--trial-root",
                trialRoot,
                "--json");

            Assert.Equal(0, verify.ExitCode);
            using var verifyDocument = JsonDocument.Parse(verify.StandardOutput);
            Assert.Equal(bundleRoot, verifyDocument.RootElement.GetProperty("bundle_root").GetString());

            var latestReadback = RunMatrixCli(
                "trial",
                "latest",
                "--trial-root",
                trialRoot,
                "--json");

            Assert.Equal(0, latestReadback.ExitCode);
            using var latestReadbackDocument = JsonDocument.Parse(latestReadback.StandardOutput);
            Assert.Equal("latest_found", latestReadbackDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal("day-1", latestReadbackDocument.RootElement.GetProperty("latest").GetProperty("run_id").GetString());
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void TrialPlay_JsonModePreparesWorkspaceWithoutCollecting()
    {
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-trial-play-ready-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "play",
                "--trial-root",
                trialRoot,
                "--run-id",
                "agent-run",
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("play", root.GetProperty("command").GetString());
            Assert.Equal("ready_for_agent", root.GetProperty("status").GetString());
            Assert.True(root.GetProperty("offline").GetBoolean());
            Assert.False(root.GetProperty("server_submission").GetBoolean());
            var instruction = root.GetProperty("agent_instruction").GetString();
            Assert.Contains("artifacts/agent-report.json", instruction, StringComparison.Ordinal);

            var workspaceRoot = root.GetProperty("workspace_root").GetString()!;
            var bundleRoot = root.GetProperty("bundle_root").GetString()!;
            Assert.True(Directory.Exists(Path.Combine(workspaceRoot, ".git")));
            Assert.True(File.Exists(Path.Combine(workspaceRoot, "AGENTS.md")));
            Assert.False(File.Exists(Path.Combine(bundleRoot, "matrix-artifact-manifest.json")));
            Assert.False(File.Exists(Path.Combine(trialRoot, "latest.json")));
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void TrialDemo_SecondRunUpdatesLatestPointer()
    {
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-trial-latest-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            var first = RunMatrixCli(
                "trial",
                "demo",
                "--trial-root",
                trialRoot,
                "--run-id",
                "first",
                "--json");
            var second = RunMatrixCli(
                "trial",
                "demo",
                "--trial-root",
                trialRoot,
                "--run-id",
                "second",
                "--json");

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(0, second.ExitCode);
            using var latestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(trialRoot, "latest.json")));
            var latest = latestDocument.RootElement;
            Assert.Equal("second", latest.GetProperty("run_id").GetString());
            Assert.Equal(Path.Combine(Path.GetFullPath(trialRoot), "run-second"), latest.GetProperty("run_root").GetString());
            Assert.True(File.Exists(latest.GetProperty("result_card_path").GetString()!));
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void TrialDemo_SharedHistorySupportsTrialRootCompareConvenience()
    {
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-trial-compare-latest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var first = RunMatrixCli(
                "trial",
                "demo",
                "--trial-root",
                trialRoot,
                "--run-id",
                "first",
                "--json");
            var second = RunMatrixCli(
                "trial",
                "demo",
                "--trial-root",
                trialRoot,
                "--run-id",
                "second",
                "--json");

            var compare = RunMatrixCli(
                "trial",
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
            Assert.Equal(0, compare.ExitCode);
            Assert.True(File.Exists(Path.Combine(trialRoot, "history", "runs", "first.json")));
            Assert.True(File.Exists(Path.Combine(trialRoot, "history", "runs", "second.json")));

            using var document = JsonDocument.Parse(compare.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("compare", root.GetProperty("command").GetString());
            Assert.Equal("compared", root.GetProperty("status").GetString());
            Assert.Equal("first", root.GetProperty("baseline_run_id").GetString());
            Assert.Equal("second", root.GetProperty("target_run_id").GetString());
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void TrialLatest_MissingOrInvalidPointerFailsFriendly()
    {
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-trial-latest-missing-" + Guid.NewGuid().ToString("N"));
        try
        {
            var missing = RunMatrixCli(
                "trial",
                "verify",
                "--trial-root",
                trialRoot,
                "--json");

            Assert.Equal(1, missing.ExitCode);
            using var missingDocument = JsonDocument.Parse(missing.StandardOutput);
            Assert.Equal("failed", missingDocument.RootElement.GetProperty("status").GetString());
            Assert.Contains(
                missingDocument.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "trial_latest_missing");

            Directory.CreateDirectory(trialRoot);
            File.WriteAllText(Path.Combine(trialRoot, "latest.json"), "{}");
            var invalid = RunMatrixCli(
                "trial",
                "latest",
                "--trial-root",
                trialRoot,
                "--json");

            Assert.Equal(1, invalid.ExitCode);
            using var invalidDocument = JsonDocument.Parse(invalid.StandardOutput);
            Assert.Contains(
                invalidDocument.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "trial_latest_invalid");
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void TrialDemo_DoesNotCoverLatestPointerInManifest()
    {
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-trial-latest-manifest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "demo",
                "--trial-root",
                trialRoot,
                "--run-id",
                "manifest-check",
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var bundleRoot = document.RootElement.GetProperty("bundle_root").GetString()!;
            using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundleRoot, "matrix-artifact-manifest.json")));
            var artifactPaths = manifestDocument.RootElement
                .GetProperty("artifacts")
                .EnumerateArray()
                .Select(artifact => artifact.GetProperty("path").GetString())
                .ToArray();

            Assert.DoesNotContain("latest.json", artifactPaths);
            Assert.DoesNotContain(artifactPaths, path => path?.Contains("latest", StringComparison.OrdinalIgnoreCase) == true);
        }
        finally
        {
            DeleteDirectory(trialRoot);
        }
    }

    [Fact]
    public void TrialPlay_DemoAgentCompletesVerifiedRun()
    {
        var trialRoot = Path.Combine(Path.GetTempPath(), "carves-trial-play-demo-agent-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "play",
                "--trial-root",
                trialRoot,
                "--run-id",
                "play-demo",
                "--demo-agent",
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("play", root.GetProperty("command").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.Equal("verified", root.GetProperty("verification").GetProperty("status").GetString());
            Assert.True(root.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            Assert.Equal("runs/play-demo.json", root.GetProperty("history_entry_ref").GetString());
        }
        finally
        {
            DeleteDirectory(trialRoot);
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
