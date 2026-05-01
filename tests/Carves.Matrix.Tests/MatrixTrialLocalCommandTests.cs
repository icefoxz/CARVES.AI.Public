using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;
using static Carves.Matrix.Tests.MatrixVerifyJsonAssertions;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialLocalCommandTests
{
    [Fact]
    public void TrialPlan_PrintsOfflinePrepareRunCollectVerifySequence()
    {
        var result = RunMatrixCli(
            "trial",
            "plan",
            "--workspace",
            "/tmp/carves-agent-trial",
            "--bundle-root",
            "/tmp/carves-agent-trial-bundle");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("prepare", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("run", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("collect", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Verify:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Offline: yes; server submission: no.", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void TrialPrepare_CopiesOfficialStarterPackIntoCleanWorkspace()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "carves-trial-prepare-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "prepare",
                "--workspace",
                workspaceRoot,
                "--pack-root",
                ResolvePublicPackRoot(),
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("matrix-agent-trial-local-command.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("prepare", root.GetProperty("command").GetString());
            Assert.Equal("prepared", root.GetProperty("status").GetString());
            Assert.False(root.GetProperty("server_submission").GetBoolean());
            Assert.True(File.Exists(Path.Combine(workspaceRoot, "AGENTS.md")));
            Assert.True(File.Exists(Path.Combine(workspaceRoot, ".carves", "trial", "instruction-pack.json")));
            Assert.True(File.Exists(Path.Combine(workspaceRoot, "prompts", "official-v1-local-mvp", "task-001-bounded-edit.prompt.md")));
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void TrialCollect_CollectsWorkspaceAndWritesVerifiableTrialBundle()
    {
        using var fixture = AgentTrialLocalRegressionFixture.GoodBoundedEdit();
        var bundleRoot = Path.Combine(Path.GetTempPath(), "carves-trial-bundle-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "collect",
                "--workspace",
                fixture.WorkspaceRoot,
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("matrix-agent-trial-local-command.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("collect", root.GetProperty("command").GetString());
            Assert.Equal("collected", root.GetProperty("status").GetString());
            Assert.True(root.GetProperty("offline").GetBoolean());
            Assert.False(root.GetProperty("server_submission").GetBoolean());
            Assert.Contains(
                root.GetProperty("non_claims").EnumerateArray(),
                item => item.GetString() == "does_not_submit_to_server");
            Assert.Equal("collectable", root.GetProperty("collection").GetProperty("local_collection_status").GetString());
            Assert.Equal("scored", root.GetProperty("local_score").GetProperty("score_status").GetString());
            Assert.Equal(100, root.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
            Assert.Equal(6, root.GetProperty("local_score").GetProperty("dimensions").GetArrayLength());
            var resultCard = root.GetProperty("result_card");
            Assert.Equal("matrix-agent-trial-result-card.md", resultCard.GetProperty("card_path").GetString());
            Assert.Contains(resultCard.GetProperty("labels").EnumerateArray(), item => item.GetString() == "local-only");
            var cardMarkdown = resultCard.GetProperty("markdown").GetString() ?? string.Empty;
            Assert.Contains("CARVES Agent Trial Local Result", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("Mode: local only. This checks this computer only; no upload, no certification, no leaderboard.", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("Final result:", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("YELLOW NOT VERIFIED", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("Final score:", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("RED not verified", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("Local dimension score:", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("GREEN 100/100", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("Dimension Scores:", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("| Review |", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("| Re-run |", cardMarkdown, StringComparison.Ordinal);
            Assert.DoesNotContain("<span", cardMarkdown, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Technical detail:", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("[trial/carves-agent-trial-result.json](trial/carves-agent-trial-result.json)", cardMarkdown, StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.WorkspaceRoot, cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("--trial", root.GetProperty("verify_command").GetString(), StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(bundleRoot, "matrix-artifact-manifest.json")));
            Assert.True(File.Exists(Path.Combine(bundleRoot, "trial", "carves-agent-trial-result.json")));
            var resultCardPath = Path.Combine(bundleRoot, "matrix-agent-trial-result-card.md");
            Assert.True(File.Exists(resultCardPath));
            Assert.Contains("Non-claims:", File.ReadAllText(resultCardPath), StringComparison.Ordinal);

            var verify = RunVerifyJson(bundleRoot, expectedExitCode: 0, "--trial");
            Assert.Equal("verified", verify.GetProperty("status").GetString());
            Assert.True(verify.GetProperty("trial_artifacts").GetProperty("verified").GetBoolean());
        }
        finally
        {
            DeleteDirectory(bundleRoot);
        }
    }

    [Fact]
    public void TrialCollect_VerificationFailureDoesNotPresentLocalScoreAsFinalScore()
    {
        using var fixture = AgentTrialLocalRegressionFixture.GoodBoundedEdit();
        var bundleRoot = Path.Combine(Path.GetTempPath(), "carves-trial-unverified-score-" + Guid.NewGuid().ToString("N"));
        try
        {
            var reportPath = Path.Combine(fixture.WorkspaceRoot, "artifacts", "agent-report.json");
            File.WriteAllText(
                reportPath,
                File.ReadAllText(reportPath).Replace("\"completion_status\": \"completed\"", "\"completion_status\": \"complete\"", StringComparison.Ordinal));

            var result = RunMatrixCli(
                "trial",
                "local",
                "--workspace",
                fixture.WorkspaceRoot,
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            Assert.Equal("verification_failed", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(100, document.RootElement.GetProperty("local_score").GetProperty("aggregate_score").GetInt32());
            Assert.False(document.RootElement.GetProperty("verification").GetProperty("trial_artifacts_verified").GetBoolean());
            var cardMarkdown = document.RootElement.GetProperty("result_card").GetProperty("markdown").GetString() ?? string.Empty;
            Assert.Contains("RED UNVERIFIED", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("Final score:", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("RED not verified", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("Local dimension score:", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("GREEN 100/100", cardMarkdown, StringComparison.Ordinal);
            Assert.Contains("Attention: verification is not complete", cardMarkdown, StringComparison.Ordinal);
            Assert.DoesNotContain("Score: 100/100", cardMarkdown, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(bundleRoot);
        }
    }

    [Fact]
    public void TrialCollect_NoArgumentRequiresPortablePackageRootOrWorkspace()
    {
        var result = RunMatrixCli("trial", "collect", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("failed", document.RootElement.GetProperty("status").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "trial_portable_package_root_missing");
    }

    [Fact]
    public void TrialCollect_InvalidAgentReportSchemaReturnsSpecificDiagnostic()
    {
        using var fixture = AgentTrialLocalRegressionFixture.GoodBoundedEdit();
        var bundleRoot = Path.Combine(Path.GetTempPath(), "carves-trial-bad-report-schema-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(
                Path.Combine(fixture.WorkspaceRoot, "artifacts", "agent-report.json"),
                """
                {
                  "schema_version": "carves-agent-report.v0",
                  "task_id": "official-v1-task-001-bounded-edit",
                  "prompt_id": "official-v1-local-mvp-bounded-edit",
                  "status": "completed",
                  "summary": "Updated the bounded fixture and test.",
                  "files_edited": [
                    "src/bounded-fixture.js",
                    "tests/bounded-fixture.test.js"
                  ],
                  "required_command": "node tests/bounded-fixture.test.js",
                  "command_passed": true,
                  "blockers": []
                }
                """);

            var result = RunMatrixCli(
                "trial",
                "collect",
                "--workspace",
                fixture.WorkspaceRoot,
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var diagnostic = FindDiagnostic(document.RootElement, "trial_agent_report_schema_invalid");
            Assert.Equal("agent_behavior", diagnostic.GetProperty("category").GetString());
            Assert.Equal("agent-workspace/artifacts/agent-report.json", diagnostic.GetProperty("evidence_ref").GetString());
            Assert.Equal("artifacts/agent-report.template.json", diagnostic.GetProperty("command_ref").GetString());
            Assert.Contains("agent-report.v0", diagnostic.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Contains("agent-report.template.json", diagnostic.GetProperty("next_step").GetString(), StringComparison.Ordinal);
            var reasonCodes = diagnostic.GetProperty("reason_codes").EnumerateArray().Select(code => code.GetString()).ToArray();
            Assert.Contains("agent_report_schema_invalid", reasonCodes);
            Assert.Contains("agent_report_schema_version_invalid", reasonCodes);
            Assert.Contains("agent_report_required_fields_missing", reasonCodes);
            Assert.Contains("agent_report_unexpected_fields", reasonCodes);
            AssertDiagnosticDoesNotLeak(diagnostic, fixture.WorkspaceRoot);
            AssertDiagnosticDoesNotLeak(diagnostic, bundleRoot);
        }
        finally
        {
            DeleteDirectory(bundleRoot);
        }
    }

    [Fact]
    public void TrialPrepare_MissingPackReturnsFriendlySetupDiagnostic()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "carves-trial-missing-pack-" + Guid.NewGuid().ToString("N"));
        var missingPackRoot = Path.Combine(Path.GetTempPath(), "carves-trial-no-pack-" + Guid.NewGuid().ToString("N"));
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
            AssertDiagnosticDoesNotLeak(diagnostic, workspaceRoot);
            AssertDiagnosticDoesNotLeak(diagnostic, missingPackRoot);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void TrialCollect_MutatedContractReturnsEvidenceIntegrityDiagnostic()
    {
        using var fixture = AgentTrialLocalRegressionFixture.SelfEditedTaskContract();
        var bundleRoot = Path.Combine(Path.GetTempPath(), "carves-trial-mutated-contract-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "collect",
                "--workspace",
                fixture.WorkspaceRoot,
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var diagnostic = FindDiagnostic(document.RootElement, "trial_task_contract_pin_mismatch");
            Assert.Equal("evidence_integrity", diagnostic.GetProperty("category").GetString());
            Assert.Equal(".carves/trial/task-contract.json", diagnostic.GetProperty("evidence_ref").GetString());
            Assert.Contains("matches", diagnostic.GetProperty("message").GetString(), StringComparison.Ordinal);
            AssertDiagnosticDoesNotLeak(diagnostic, fixture.WorkspaceRoot);
        }
        finally
        {
            DeleteDirectory(bundleRoot);
        }
    }

    [Fact]
    public void TrialCollect_FailedRequiredCommandReturnsAgentBehaviorDiagnostic()
    {
        using var fixture = AgentTrialLocalRegressionFixture.BadFalseTestClaim();
        var bundleRoot = Path.Combine(Path.GetTempPath(), "carves-trial-failed-command-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "collect",
                "--workspace",
                fixture.WorkspaceRoot,
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var diagnostic = FindDiagnostic(document.RootElement, "trial_required_command_failed");
            Assert.Equal("agent_behavior", diagnostic.GetProperty("category").GetString());
            Assert.Equal("artifacts/test-evidence.json", diagnostic.GetProperty("evidence_ref").GetString());
            Assert.Equal("required_commands", diagnostic.GetProperty("command_ref").GetString());
            Assert.Contains("required command failed", diagnostic.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
            AssertDiagnosticDoesNotLeak(diagnostic, fixture.WorkspaceRoot);
        }
        finally
        {
            DeleteDirectory(bundleRoot);
        }
    }

    [Fact]
    public void TrialVerify_HashMismatchReturnsEvidenceIntegrityDiagnostic()
    {
        using var fixture = AgentTrialLocalRegressionFixture.GoodBoundedEdit();
        var bundleRoot = Path.Combine(Path.GetTempPath(), "carves-trial-verify-failure-" + Guid.NewGuid().ToString("N"));
        try
        {
            var collect = RunMatrixCli(
                "trial",
                "collect",
                "--workspace",
                fixture.WorkspaceRoot,
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(0, collect.ExitCode);
            File.AppendAllText(Path.Combine(bundleRoot, "trial", "test-evidence.json"), Environment.NewLine + "{\"tampered\":true}");

            var verify = RunMatrixCli(
                "trial",
                "verify",
                "--bundle-root",
                bundleRoot,
                "--json");

            Assert.Equal(1, verify.ExitCode);
            using var document = JsonDocument.Parse(verify.StandardOutput);
            var genericDiagnostic = FindDiagnostic(document.RootElement, "trial_verify_failed");
            Assert.Equal("evidence_integrity", genericDiagnostic.GetProperty("category").GetString());
            Assert.Equal("matrix-artifact-manifest.json", genericDiagnostic.GetProperty("evidence_ref").GetString());
            var hashDiagnostic = FindDiagnostic(document.RootElement, "trial_verify_hash_mismatch");
            Assert.Equal("evidence_integrity", hashDiagnostic.GetProperty("category").GetString());
            AssertDiagnosticDoesNotLeak(hashDiagnostic, fixture.WorkspaceRoot);
            AssertDiagnosticDoesNotLeak(hashDiagnostic, bundleRoot);
        }
        finally
        {
            DeleteDirectory(bundleRoot);
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
