using System.Diagnostics;
using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class AgentTrialLocalE2ESmokeTests
{
    [Fact]
    public void LocalUserSmoke_OfficialStarterPackReachesVerifiedResultAndHistoryWithoutServer()
    {
        var root = Path.Combine(Path.GetTempPath(), "carves-trial-user-smoke-" + Guid.NewGuid().ToString("N"));
        var workspaceRoot = Path.Combine(root, "carves-agent-trial");
        var bundleRoot = Path.Combine(workspaceRoot, "artifacts", "matrix-trial-bundle");
        var historyRoot = Path.Combine(root, "carves-agent-trial-history");
        try
        {
            var plan = RunMatrixCli(
                "trial",
                "plan",
                "--workspace",
                workspaceRoot,
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(0, plan.ExitCode);
            using (var document = JsonDocument.Parse(plan.StandardOutput))
            {
                var planRoot = document.RootElement;
                Assert.Equal("plan", planRoot.GetProperty("command").GetString());
                Assert.Equal("ready", planRoot.GetProperty("status").GetString());
                AssertTrialCommandLocalOnly(planRoot);
                AssertStepOrder(planRoot);
            }

            var prepare = RunMatrixCli(
                "trial",
                "prepare",
                "--workspace",
                workspaceRoot,
                "--bundle-root",
                bundleRoot,
                "--pack-root",
                ResolvePublicPackRoot(),
                "--json");

            Assert.Equal(0, prepare.ExitCode);
            using (var document = JsonDocument.Parse(prepare.StandardOutput))
            {
                var prepareRoot = document.RootElement;
                Assert.Equal("prepare", prepareRoot.GetProperty("command").GetString());
                Assert.Equal("prepared", prepareRoot.GetProperty("status").GetString());
                AssertTrialCommandLocalOnly(prepareRoot);
            }

            InitializeGitBaseline(workspaceRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            var local = RunMatrixCli(
                "trial",
                "local",
                "--workspace",
                workspaceRoot,
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(0, local.ExitCode);
            using (var document = JsonDocument.Parse(local.StandardOutput))
            {
                var localRoot = document.RootElement;
                Assert.Equal("local", localRoot.GetProperty("command").GetString());
                Assert.Equal("verified", localRoot.GetProperty("status").GetString());
                AssertTrialCommandLocalOnly(localRoot);
                Assert.Equal("collectable", localRoot.GetProperty("collection").GetProperty("local_collection_status").GetString());
                Assert.Equal("verified", localRoot.GetProperty("verification").GetProperty("status").GetString());
                Assert.True(localRoot.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
                Assert.Equal("scored", localRoot.GetProperty("local_score").GetProperty("score_status").GetString());
                Assert.Equal(100, localRoot.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
                Assert.Equal("matrix-agent-trial-result-card.md", localRoot.GetProperty("result_card").GetProperty("card_path").GetString());
                Assert.Empty(localRoot.GetProperty("diagnostics").EnumerateArray());
            }

            AssertLocalSmokeOutputs(workspaceRoot, bundleRoot);

            var verify = RunMatrixCli(
                "trial",
                "verify",
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(0, verify.ExitCode);
            using (var document = JsonDocument.Parse(verify.StandardOutput))
            {
                var verifyRoot = document.RootElement;
                Assert.Equal("verify", verifyRoot.GetProperty("command").GetString());
                Assert.Equal("verified", verifyRoot.GetProperty("status").GetString());
                AssertTrialCommandLocalOnly(verifyRoot);
                Assert.Equal("verified", verifyRoot.GetProperty("verification").GetProperty("status").GetString());
                Assert.True(verifyRoot.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            }

            var record = RunMatrixCli(
                "trial",
                "record",
                "--bundle-root",
                bundleRoot,
                "--history-root",
                historyRoot,
                "--run-id",
                "day-1",
                "--json");

            Assert.Equal(0, record.ExitCode);
            using (var document = JsonDocument.Parse(record.StandardOutput))
            {
                var recordRoot = document.RootElement;
                Assert.Equal("record", recordRoot.GetProperty("command").GetString());
                Assert.Equal("recorded", recordRoot.GetProperty("status").GetString());
                AssertOfflineNoServer(recordRoot);
                Assert.Equal("runs/day-1.json", recordRoot.GetProperty("history_entry_ref").GetString());
                Assert.Contains(
                    recordRoot.GetProperty("non_claims").EnumerateArray(),
                    item => item.GetString() == "not_server_receipt");
            }

            Assert.True(File.Exists(Path.Combine(historyRoot, "runs", "day-1.json")));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void LocalUserSmoke_MissingStarterPackReturnsFriendlyDiagnostic()
    {
        var root = Path.Combine(Path.GetTempPath(), "carves-trial-missing-pack-smoke-" + Guid.NewGuid().ToString("N"));
        var workspaceRoot = Path.Combine(root, "carves-agent-trial");
        var missingPackRoot = Path.Combine(root, "missing-pack");
        try
        {
            var result = RunMatrixCli(
                "trial",
                "prepare",
                "--workspace",
                workspaceRoot,
                "--pack-root",
                missingPackRoot,
                "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var diagnostic = FindDiagnostic(document.RootElement, "trial_setup_pack_missing");
            Assert.Equal("user_setup", diagnostic.GetProperty("category").GetString());
            Assert.Equal("--pack-root", diagnostic.GetProperty("evidence_ref").GetString());
            Assert.Contains("starter pack", diagnostic.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
            AssertDiagnosticDoesNotLeak(diagnostic, root);
            AssertDiagnosticDoesNotLeak(diagnostic, workspaceRoot);
            AssertDiagnosticDoesNotLeak(diagnostic, missingPackRoot);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void AssertTrialCommandLocalOnly(JsonElement root)
    {
        AssertOfflineNoServer(root);
        Assert.Contains(
            root.GetProperty("non_claims").EnumerateArray(),
            item => item.GetString() == "does_not_submit_to_server");
        Assert.Contains(
            root.GetProperty("non_claims").EnumerateArray(),
            item => item.GetString() == "not_leaderboard_eligible");
        Assert.Contains(
            root.GetProperty("non_claims").EnumerateArray(),
            item => item.GetString() == "not_certification");
    }

    private static void AssertOfflineNoServer(JsonElement root)
    {
        Assert.True(root.GetProperty("offline").GetBoolean());
        Assert.False(root.GetProperty("server_submission").GetBoolean());
    }

    private static void AssertStepOrder(JsonElement root)
    {
        var steps = root.GetProperty("steps").EnumerateArray()
            .Select(step => step.GetString() ?? string.Empty)
            .ToArray();

        Assert.Equal(4, steps.Length);
        Assert.StartsWith("prepare:", steps[0], StringComparison.Ordinal);
        Assert.StartsWith("run:", steps[1], StringComparison.Ordinal);
        Assert.StartsWith("collect:", steps[2], StringComparison.Ordinal);
        Assert.StartsWith("verify:", steps[3], StringComparison.Ordinal);
    }

    private static void AssertLocalSmokeOutputs(string workspaceRoot, string bundleRoot)
    {
        foreach (var path in new[]
        {
            Path.Combine(workspaceRoot, "artifacts", "diff-scope-summary.json"),
            Path.Combine(workspaceRoot, "artifacts", "test-evidence.json"),
            Path.Combine(workspaceRoot, "artifacts", "carves-agent-trial-result.json"),
            Path.Combine(bundleRoot, "matrix-artifact-manifest.json"),
            Path.Combine(bundleRoot, "matrix-proof-summary.json"),
            Path.Combine(bundleRoot, "matrix-agent-trial-result-card.md"),
            Path.Combine(bundleRoot, "trial", "carves-agent-trial-result.json")
        })
        {
            Assert.True(File.Exists(path), "Expected local smoke output: " + path);
        }
    }

    private static JsonElement FindDiagnostic(JsonElement root, string code)
    {
        foreach (var diagnostic in root.GetProperty("diagnostics").EnumerateArray())
        {
            if (diagnostic.GetProperty("code").GetString() == code)
            {
                return diagnostic;
            }
        }

        throw new InvalidOperationException($"Diagnostic was not found: {code}");
    }

    private static void AssertDiagnosticDoesNotLeak(JsonElement diagnostic, string privatePath)
    {
        foreach (var propertyName in new[] { "message", "evidence_ref", "command_ref", "next_step" })
        {
            if (diagnostic.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                Assert.DoesNotContain(privatePath, property.GetString() ?? string.Empty, StringComparison.Ordinal);
            }
        }
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

    private static void ApplyGoodBoundedEdit(string workspaceRoot)
    {
        var sourcePath = Path.Combine(workspaceRoot, "src", "bounded-fixture.js");
        File.WriteAllText(
            sourcePath,
            File.ReadAllText(sourcePath)
                .Replace("return `${normalizedComponent}:${mode}`;", "return `component=${normalizedComponent}; mode=${mode}; trial=bounded`;", StringComparison.Ordinal));

        var testPath = Path.Combine(workspaceRoot, "tests", "bounded-fixture.test.js");
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

    private static void InitializeGitBaseline(string workspaceRoot)
    {
        RunGit(workspaceRoot, "init");
        RunGit(workspaceRoot, "config", "user.email", "agent-trial-local@example.test");
        RunGit(workspaceRoot, "config", "user.name", "Agent Trial Local");
        RunGit(workspaceRoot, "add", ".");
        RunGit(workspaceRoot, "commit", "-m", "baseline");
    }

    private static void RunGit(string workspaceRoot, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git.");
        _ = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("git command timed out.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git exited {process.ExitCode}: {stderr}");
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
