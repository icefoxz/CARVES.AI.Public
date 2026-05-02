using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class GuardCheckCliTests
{
    [Fact]
    public void GuardCheckJson_AllowsExternalDiffWithoutRuntimeTaskTruth()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy();
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");
        repo.CommitAll("baseline");
        repo.AppendFile("src/todo.ts", "export function countTodos() { return todos.length; }\n");
        repo.WriteFile("tests/todo-count.test.ts", "test('count', () => expect(0).toBe(0));\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("guard-check.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("allow", root.GetProperty("decision").GetString());
        Assert.Equal("alpha-cli-test", root.GetProperty("policy_id").GetString());
        Assert.Contains("Patch allowed", root.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.False(root.GetProperty("requires_runtime_task_truth").GetBoolean());
        Assert.Equal(2, root.GetProperty("changed_files").GetArrayLength());
        Assert.Empty(root.GetProperty("violations").EnumerateArray());
        Assert.Contains(root.GetProperty("evidence_refs").EnumerateArray(), item => item.GetString()!.StartsWith("guard-run:", StringComparison.Ordinal));
    }

    [Fact]
    public void GuardCheckJson_BlocksProtectedControlPlanePath()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy();
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");
        repo.CommitAll("baseline");
        repo.WriteFile(".ai/tasks/generated.json", "{ \"task_id\": \"external\" }\n");
        repo.AppendFile("src/todo.ts", "export const unsafe = true;\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("block", root.GetProperty("decision").GetString());
        Assert.Contains("Patch blocked", root.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Contains(root.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "path.protected_prefix"
            && violation.GetProperty("file_path").GetString() == ".ai/tasks/generated.json");
        Assert.Contains(root.GetProperty("evidence_refs").EnumerateArray(), item => item.GetString()!.Contains("path.protected_prefix", StringComparison.Ordinal));
    }

    [Fact]
    public void GuardCheckText_BlocksBudgetViolation()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy(maxChangedFiles: 1);
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");
        repo.CommitAll("baseline");
        repo.AppendFile("src/todo.ts", "export const changed = true;\n");
        repo.AppendFile("tests/todo.test.ts", "test('changed', () => expect(true).toBe(true));\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("CARVES Guard check", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Decision: block", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("budget.max_changed_files", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardCheckJson_FailsClosedForMalformedPolicy()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteFile(".ai/guard-policy.json", "{ malformed policy");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("guard-check.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("block", root.GetProperty("decision").GetString());
        Assert.Equal("unknown", root.GetProperty("policy_id").GetString());
        Assert.Contains(root.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "policy.invalid");
    }

    [Fact]
    public void GuardCheckJson_FailsClosedForUnsupportedPolicySchema()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteFile(".ai/guard-policy.json", """
        {
          "schema_version": 2,
          "policy_id": "future-policy"
        }
        """);

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("guard-check.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("block", root.GetProperty("decision").GetString());
        Assert.Contains(root.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "policy.unsupported_schema_version");
    }

    [Fact]
    public void GuardInitJson_CreatesStarterPolicyAndCheckCanUseItImmediately()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");

        var init = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "init", "--json");
        repo.CommitAll("baseline");
        repo.AppendFile("src/todo.ts", "export function countTodos() { return todos.length; }\n");
        repo.WriteFile("tests/todo-count.test.ts", "test('count', () => expect(0).toBe(0));\n");
        var check = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");

        Assert.Equal(0, init.ExitCode);
        using var initDocument = JsonDocument.Parse(init.StandardOutput);
        var initRoot = initDocument.RootElement;
        Assert.Equal("guard-init.v1", initRoot.GetProperty("schema_version").GetString());
        Assert.Equal("created", initRoot.GetProperty("status").GetString());
        Assert.Equal(".ai/guard-policy.json", initRoot.GetProperty("policy_path").GetString());
        Assert.False(initRoot.GetProperty("requires_runtime_task_truth").GetBoolean());
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "guard-policy.json")));

        Assert.Equal(0, check.ExitCode);
        using var checkDocument = JsonDocument.Parse(check.StandardOutput);
        var checkRoot = checkDocument.RootElement;
        Assert.Equal("allow", checkRoot.GetProperty("decision").GetString());
        Assert.Equal("carves-guard-starter", checkRoot.GetProperty("policy_id").GetString());
        Assert.False(checkRoot.GetProperty("requires_runtime_task_truth").GetBoolean());
    }

    [Fact]
    public void GuardInit_DoesNotOverwriteExistingPolicyWithoutForce()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteFile(".ai/guard-policy.json", "{ \"schema_version\": 1, \"policy_id\": \"existing\" }\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "init");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("already exists", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("{ \"schema_version\": 1, \"policy_id\": \"existing\" }\n", File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "guard-policy.json")));
    }

    [Fact]
    public void GuardInitForce_OverwritesExistingPolicyIntentionally()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteFile(".ai/guard-policy.json", "{ \"schema_version\": 1, \"policy_id\": \"existing\" }\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "init", "--force", "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("overwritten", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("overwritten").GetBoolean());
        Assert.Contains("\"policy_id\": \"carves-guard-starter\"", File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "guard-policy.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void GuardInit_RejectsPolicyPathOutsideRepository()
    {
        using var repo = GuardGitSandbox.Create();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "init", "--policy", "../outside-guard-policy.json");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("inside the target repository", result.StandardError, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Directory.GetParent(repo.RootPath)!.FullName, "outside-guard-policy.json")));
    }

    [Fact]
    public void GuardCheckJson_FailsClosedWithGitDiffDiagnosticForInvalidBaseRef()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy();
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.CommitAll("baseline");
        repo.AppendFile("src/todo.ts", "export const changed = true;\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--base", "refs/does-not-exist", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("guard-check.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("block", root.GetProperty("decision").GetString());
        Assert.Contains(root.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "git.diff_failed"
            && violation.GetProperty("evidence").GetString()!.Contains("git diff", StringComparison.Ordinal));
    }

    [Fact]
    public void GuardRunWithoutTaskId_PrintsTaskAwareUsage()
    {
        using var repo = GuardGitSandbox.Create();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "run");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("carves guard run <task-id>", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Experimental task-aware mode", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("stable external Beta entry", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardRunJson_EmitsTaskAwareDecisionEnvelopeWhenHostUnavailable()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "--host", "guard", "run", "T-GUARD-001", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("guard-run.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("experimental", root.GetProperty("mode_stability").GetString());
        Assert.False(root.GetProperty("stable_external_entry").GetBoolean());
        Assert.Equal("T-GUARD-001", root.GetProperty("task_id").GetString());
        Assert.Equal("block", root.GetProperty("decision").GetString());
        Assert.True(root.GetProperty("requires_runtime_task_truth").GetBoolean());
        Assert.Equal("host_unavailable", root.GetProperty("execution").GetProperty("outcome").GetString());
        Assert.Contains(root.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "runtime.execution_blocked");
    }

    [Fact]
    public void GuardRunJson_WithHostSessionConflict_ProjectsReconcileNextAction()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy();

        var runtimeDirectory = ResolveHostRuntimeDirectory(repo.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "guard-stale-generation");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            repo.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "--host", "guard", "run", "T-GUARD-001", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("host_unavailable", root.GetProperty("execution").GetProperty("outcome").GetString());
        var nextAction = root.GetProperty("execution").GetProperty("next_action").GetString();
        Assert.True(
            nextAction?.Contains("host reconcile --replace-stale", StringComparison.Ordinal) == true
            || nextAction?.Contains("carves host ensure --json", StringComparison.Ordinal) == true,
            $"Unexpected next_action: {nextAction}");
    }

    [Fact]
    public void GuardHelp_MarksTaskAwareRunExperimental()
    {
        using var repo = GuardGitSandbox.Create();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "help", "guard");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("carves guard run <task-id>", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("experimental", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Diff-only Alpha Guard Beta patch admission", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("experimental for this Beta", result.StandardOutput, StringComparison.Ordinal);
    }

    private static string ResolveHostRuntimeDirectory(string repoRoot)
    {
        var repoHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(repoRoot).ToUpperInvariant())))
            .ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "carves-runtime-host", repoHash);
    }

    private static void WriteHostDescriptor(
        string repoRoot,
        int processId,
        string runtimeDirectory,
        string deploymentDirectory,
        string executablePath,
        string baseUrl,
        int port)
    {
        var descriptorDirectory = Path.Combine(repoRoot, ".carves-platform", "host");
        Directory.CreateDirectory(descriptorDirectory);
        var descriptorPath = Path.Combine(descriptorDirectory, "descriptor.json");
        var now = DateTimeOffset.UtcNow;
        var payload = $$"""
        {
          "repo_root": "{{Path.GetFullPath(repoRoot)}}",
          "runtime_directory": "{{runtimeDirectory}}",
          "deployment_directory": "{{deploymentDirectory}}",
          "executable_path": "{{executablePath}}",
          "base_url": "{{baseUrl}}",
          "port": {{port}},
          "process_id": {{processId}},
          "started_at": "{{now:O}}",
          "version": "test",
          "stage": "integration"
        }
        """;
        File.WriteAllText(descriptorPath, payload);
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class LongRunningProcess : IDisposable
    {
        private readonly Process process;

        private LongRunningProcess(Process process)
        {
            this.process = process;
        }

        public int ProcessId => process.Id;

        public static LongRunningProcess Start()
        {
            var startInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = "cmd.exe";
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add("ping -n 300 127.0.0.1 > nul");
            }
            else
            {
                startInfo.FileName = "/bin/sh";
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add("sleep 300");
            }

            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start long-running process.");
            return new LongRunningProcess(process);
        }

        public void Dispose()
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
                catch
                {
                }
            }

            process.Dispose();
        }
    }

    [Fact]
    public void GuardAuditAndExplainJson_ReadBackRecordedBlockDecision()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy();
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");
        repo.CommitAll("baseline");
        repo.WriteFile(".ai/tasks/generated.json", "{ \"task_id\": \"external\" }\n");

        var check = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");
        var runId = JsonDocument.Parse(check.StandardOutput).RootElement.GetProperty("run_id").GetString()!;

        var audit = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "audit", "--json");
        var explain = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "explain", runId, "--json");

        Assert.Equal(1, check.ExitCode);
        Assert.Equal(0, audit.ExitCode);
        Assert.Equal(0, explain.ExitCode);
        using var auditDocument = JsonDocument.Parse(audit.StandardOutput);
        var auditRoot = auditDocument.RootElement;
        Assert.Equal("guard-audit.v1", auditRoot.GetProperty("schema_version").GetString());
        Assert.Contains(auditRoot.GetProperty("decisions").EnumerateArray(), decision =>
            decision.GetProperty("run_id").GetString() == runId
            && decision.GetProperty("outcome").GetString() == "block");

        using var explainDocument = JsonDocument.Parse(explain.StandardOutput);
        var explainRoot = explainDocument.RootElement;
        Assert.Equal("guard-explain.v1", explainRoot.GetProperty("schema_version").GetString());
        Assert.True(explainRoot.GetProperty("found").GetBoolean());
        var decision = explainRoot.GetProperty("decision");
        Assert.Equal(runId, decision.GetProperty("run_id").GetString());
        Assert.Contains(decision.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "path.protected_prefix"
            && violation.GetProperty("evidence_ref").GetString()!.Contains("path.protected_prefix", StringComparison.Ordinal));
    }

    [Fact]
    public void GuardAuditTextAndReportJson_SummarizePolicyAndRecentOutcomes()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy();
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");
        repo.CommitAll("baseline");
        repo.AppendFile("src/todo.ts", "export function countTodos() { return todos.length; }\n");
        repo.WriteFile("tests/todo-count.test.ts", "test('count', () => expect(0).toBe(0));\n");

        var check = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");
        var auditText = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "audit");
        var reportText = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "report");
        var reportJson = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "report", "--json");

        Assert.Equal(0, check.ExitCode);
        Assert.Equal(0, auditText.ExitCode);
        Assert.Contains("CARVES Guard audit", auditText.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("allow", auditText.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, reportText.ExitCode);
        Assert.Contains("CARVES Guard report", reportText.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Policy: alpha-cli-test", reportText.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, reportJson.ExitCode);
        using var reportDocument = JsonDocument.Parse(reportJson.StandardOutput);
        var reportRoot = reportDocument.RootElement;
        Assert.Equal("guard-report.v1", reportRoot.GetProperty("schema_version").GetString());
        Assert.Equal("alpha-cli-test", reportRoot.GetProperty("policy").GetProperty("policy_id").GetString());
        Assert.Equal("ready", reportRoot.GetProperty("posture").GetProperty("status").GetString());
        Assert.Equal(1, reportRoot.GetProperty("posture").GetProperty("allow_count").GetInt32());
    }

    [Fact]
    public void GuardReadbackJson_SurfacesDegradedDecisionStoreDiagnostics()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy();
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");
        repo.CommitAll("baseline");
        repo.AppendFile("src/todo.ts", "export function countTodos() { return todos.length; }\n");
        repo.WriteFile("tests/todo-count.test.ts", "test('count', () => expect(0).toBe(0));\n");

        var check = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");
        var runId = JsonDocument.Parse(check.StandardOutput).RootElement.GetProperty("run_id").GetString()!;
        repo.AppendFile(".ai/runtime/guard/decisions.jsonl", "\n{ malformed\n{\"schema_version\":999,\"run_id\":\"future\"}\n");

        var audit = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "audit", "--json");
        var report = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "report", "--json");
        var explain = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "explain", runId, "--json");

        Assert.Equal(0, audit.ExitCode);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal(0, explain.ExitCode);
        AssertDegradedDiagnostics(JsonDocument.Parse(audit.StandardOutput).RootElement.GetProperty("diagnostics"));
        AssertDegradedDiagnostics(JsonDocument.Parse(report.StandardOutput).RootElement.GetProperty("diagnostics"));
        AssertDegradedDiagnostics(JsonDocument.Parse(explain.StandardOutput).RootElement.GetProperty("diagnostics"));
    }

    [Fact]
    public void GuardCheckJson_BlocksOversizedExternalPilotPatch()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy(maxTotalAdditions: 5, maxFileAdditions: 5);
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");
        repo.CommitAll("baseline");
        repo.WriteFile("src/large.ts", string.Join(Environment.NewLine, Enumerable.Range(1, 7).Select(index => $"export const value{index} = {index};")) + Environment.NewLine);
        repo.WriteFile("tests/large.test.ts", "test('large', () => expect(true).toBe(true));\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("block", root.GetProperty("decision").GetString());
        Assert.Contains(root.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "budget.max_total_additions");
        Assert.Contains(root.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "budget.max_file_additions");
    }

    [Fact]
    public void GuardCheckJson_BlocksMissingTestsWhenPolicyRequiresBlock()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy(missingTestsAction: "block");
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");
        repo.CommitAll("baseline");
        repo.AppendFile("src/todo.ts", "export function firstTodo() { return todos[0]; }\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("block", root.GetProperty("decision").GetString());
        Assert.Contains(root.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "shape.missing_tests_for_source_changes");
    }

    [Fact]
    public void GuardCheckJson_BlocksDeletedSourceWhenAddedFileIsUnrelated()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteDefaultPolicy();
        repo.WriteFile("src/removed.ts", "export const removed = true;\n");
        repo.WriteFile("tests/removed.test.ts", "test('removed', () => expect(true).toBe(true));\n");
        repo.CommitAll("baseline");
        repo.DeleteFile("src/removed.ts");
        repo.WriteFile("src/unrelated.ts", "export const unrelated = true;\n");
        repo.AppendFile("tests/removed.test.ts", "test('replacement review', () => expect(true).toBe(true));\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "guard", "check", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("block", root.GetProperty("decision").GetString());
        Assert.Contains(root.GetProperty("violations").EnumerateArray(), violation =>
            violation.GetProperty("rule_id").GetString() == "shape.delete_without_replacement"
            && violation.GetProperty("file_path").GetString() == "src/removed.ts"
            && !violation.GetProperty("evidence").GetString()!.Contains("added replacement", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class GuardGitSandbox : IDisposable
    {
        private GuardGitSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static GuardGitSandbox Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "carves-guard-cli-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var sandbox = new GuardGitSandbox(root);
            sandbox.RunGit("init");
            sandbox.RunGit("config", "user.email", "guard-test@example.invalid");
            sandbox.RunGit("config", "user.name", "Guard Test");
            return sandbox;
        }

        public void WriteDefaultPolicy(
            int maxChangedFiles = 5,
            int maxTotalAdditions = 100,
            int maxFileAdditions = 50,
            string missingTestsAction = "review")
        {
            WriteFile(".ai/guard-policy.json", $$"""
            {
              "schema_version": 1,
              "policy_id": "alpha-cli-test",
              "path_policy": {
                "path_case": "case_sensitive",
                "allowed_path_prefixes": [ "src/", "tests/", "package.json", "package-lock.json" ],
                "protected_path_prefixes": [ ".ai/tasks/", ".ai/memory/", ".git/" ],
                "outside_allowed_action": "review",
                "protected_path_action": "block"
              },
              "change_budget": {
                "max_changed_files": {{maxChangedFiles}},
                "max_total_additions": {{maxTotalAdditions}},
                "max_total_deletions": 100,
                "max_file_additions": {{maxFileAdditions}},
                "max_file_deletions": 50,
                "max_renames": 1
              },
              "dependency_policy": {
                "manifest_paths": [ "package.json", "*.csproj" ],
                "lockfile_paths": [ "package-lock.json", "packages.lock.json" ],
                "manifest_without_lockfile_action": "review",
                "lockfile_without_manifest_action": "review",
                "new_dependency_action": "review"
              },
              "change_shape": {
                "allow_rename_with_content_change": false,
                "allow_delete_without_replacement": false,
                "generated_path_prefixes": [ "dist/", "build/", "coverage/" ],
                "generated_path_action": "review",
                "mixed_feature_and_refactor_action": "review",
                "require_tests_for_source_changes": true,
                "source_path_prefixes": [ "src/" ],
                "test_path_prefixes": [ "tests/" ],
                "missing_tests_action": "{{missingTestsAction}}"
              },
              "decision": {
                "fail_closed": true,
                "default_outcome": "allow",
                "review_is_passing": false,
                "emit_evidence": true
              }
            }
            """);
        }

        public void WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        public void AppendFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.AppendAllText(fullPath, content);
        }

        public void DeleteFile(string relativePath)
        {
            File.Delete(Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        public void CommitAll(string message)
        {
            RunGit("add", ".");
            RunGit("commit", "-m", message);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch
            {
            }
        }

        private void RunGit(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = RootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {stdout}{stderr}");
            }
        }
    }

    private static void AssertDegradedDiagnostics(JsonElement diagnostics)
    {
        Assert.True(diagnostics.GetProperty("is_degraded").GetBoolean());
        Assert.Equal(1, diagnostics.GetProperty("empty_line_count").GetInt32());
        Assert.Equal(2, diagnostics.GetProperty("skipped_record_count").GetInt32());
        Assert.Equal(1, diagnostics.GetProperty("malformed_record_count").GetInt32());
        Assert.Equal(1, diagnostics.GetProperty("future_version_record_count").GetInt32());
    }
}
