using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class WorkerAutomationSmokeTests
{
    private const string RealExternalWorkerSmokeEnabledEnvironmentVariable = "CARVES_RUN_REAL_EXTERNAL_WORKER_SMOKE";
    private const string RealExternalWorkerCliPathEnvironmentVariable = "CARVES_REAL_EXTERNAL_WORKER_CLI_PATH";

    [Fact]
    public void ExternalCliWorkerAutomationSmoke_RunsScheduleHandoffEvidenceResultAndReviewApprovalEndToEnd()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var timeoutScope = DelegatedExecutionTimeoutScope.Apply(workerRequestTimeoutCapSeconds: 60);
        const string taskId = "T-EXTERNAL-CLI-WORKER-AUTOMATION-E2E-SMOKE";
        const string changedPath = "docs/runtime/external-cli-worker-automation-e2e-smoke.md";
        const string repoId = "repo-external-cli-worker-automation-smoke";
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask(taskId, scope: [changedPath]);
        ClearRuntimePackSelection(sandbox.RootPath);
        sandbox.WriteDelegatedExecutionHostInvokePolicy(requestTimeoutSeconds: 90);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        sandbox.WriteAiProviderConfig("""
{
  "provider": "codex",
  "enabled": true,
  "model": "gpt-5-codex",
  "base_url": "https://api.openai.com/v1",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "medium",
  "organization": null,
  "project": null
}
""");

        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var cliPath = CreateCodexCliScript(
            changedPath,
            includeCompletionClaim: false,
            materializeChangedFile: true);
        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", repoId);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", repoId, "codex-worker-local-cli");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            RegisterExternalCliWorkerSession(
                sandbox.RootPath,
                repoId,
                identity: "external-cli-worker-automation-e2e-smoke",
                scheduleBinding: "external-cli-worker-automation-e2e-smoke-schedule",
                contextReceipt: "external-cli-worker-automation-e2e-smoke-context",
                reason: "external CLI worker automation e2e smoke schedule binding");

            var scheduleTick = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                repoId,
                "--refresh-worker-health");
            AssertScheduleTickHandoff(scheduleTick, taskId);

            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", taskId);
            Assert.True(run.ExitCode == 0, run.CombinedOutput);
            using var runDocument = ParseJsonOutput(run.StandardOutput);
            var executionRunId = runDocument.RootElement.GetProperty("execution_run_id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(executionRunId));
            Assert.Equal("codex_cli", runDocument.RootElement.GetProperty("backend_id").GetString());
            Assert.Equal("ingested", runDocument.RootElement.GetProperty("result_submission_status").GetString());
            Assert.True(runDocument.RootElement.GetProperty("host_result_ingestion_applied").GetBoolean());
            Assert.True(runDocument.RootElement.GetProperty("safety_gate_allowed").GetBoolean());

            AssertWorkerExecutionArtifact(sandbox.RootPath, taskId, changedPath);
            AssertPreApprovalEvidence(sandbox.RootPath, taskId, executionRunId);

            var approve = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "approve-review",
                taskId,
                "worker",
                "automation",
                "e2e",
                "smoke",
                "approved");
            Assert.True(approve.ExitCode == 0, approve.CombinedOutput);
            Assert.Contains("Connected to host:", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Approved review for {taskId}", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Materialized 1 approved file(s)", approve.StandardOutput, StringComparison.Ordinal);
            AssertApprovedWriteback(sandbox.RootPath, taskId, changedPath);
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            if (File.Exists(cliPath))
            {
                File.Delete(cliPath);
            }
        }
    }

    [Trait("Category", "ManualExternalWorker")]
    [Fact]
    public void RealExternalCliWorkerAutomationSmoke_RunsConfiguredExternalCliWhenExplicitlyEnabled()
    {
        if (!IsRealExternalCliWorkerSmokeEnabled())
        {
            return;
        }

        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var timeoutScope = DelegatedExecutionTimeoutScope.Apply(workerRequestTimeoutCapSeconds: 180);
        const string taskId = "T-REAL-EXTERNAL-CLI-WORKER-AUTOMATION-SMOKE";
        const string changedPath = "docs/runtime/real-external-cli-worker-automation-smoke.md";
        const string repoId = "repo-real-external-cli-worker-automation-smoke";
        var cliPath = ResolveRealExternalWorkerCliPath();
        Assert.False(string.IsNullOrWhiteSpace(cliPath), $"Set {RealExternalWorkerCliPathEnvironmentVariable}, CARVES_CODEX_CLI_PATH, or put `codex` on PATH before enabling {RealExternalWorkerSmokeEnabledEnvironmentVariable}=1.");
        Assert.True(File.Exists(cliPath), $"Configured real external worker CLI was not found: {cliPath}");
        Assert.False(LooksLikeGeneratedCodexCliStub(cliPath), $"Refusing to run the real external worker smoke against a generated test stub: {cliPath}");

        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask(
            taskId,
            scope: [changedPath],
            validationCommands: [BuildFileExistsValidationCommand(changedPath)]);
        ConfigureExternalCliWorkerSmokeTask(sandbox.RootPath, taskId, changedPath);
        ClearRuntimePackSelection(sandbox.RootPath);
        sandbox.WriteDelegatedExecutionHostInvokePolicy(requestTimeoutSeconds: 180);
        WriteEnabledRoleGovernancePolicy(sandbox.RootPath);
        sandbox.WriteAiProviderConfig("""
{
  "provider": "codex",
  "enabled": true,
  "model": "codex-default",
  "base_url": "https://api.openai.com/v1",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 180,
  "max_output_tokens": 600,
  "reasoning_effort": "low",
  "organization": null,
  "project": null
}
""");

        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        try
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", repoId);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", repoId, "codex-worker-local-cli");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            RegisterExternalCliWorkerSession(
                sandbox.RootPath,
                repoId,
                identity: "real-external-cli-worker-automation-smoke",
                scheduleBinding: "real-external-cli-worker-automation-smoke-schedule",
                contextReceipt: "real-external-cli-worker-automation-smoke-context",
                reason: "manual real external CLI worker automation smoke");

            var scheduleTick = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-automation-schedule-tick",
                "--repo-id",
                repoId,
                "--refresh-worker-health");
            AssertScheduleTickHandoff(scheduleTick, taskId);

            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "run", taskId);
            Assert.True(run.ExitCode == 0, run.CombinedOutput);
            using var runDocument = ParseJsonOutput(run.StandardOutput);
            var executionRunId = runDocument.RootElement.GetProperty("execution_run_id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(executionRunId));
            Assert.Equal("codex_cli", runDocument.RootElement.GetProperty("backend_id").GetString());
            Assert.Equal("ingested", runDocument.RootElement.GetProperty("result_submission_status").GetString());
            Assert.True(runDocument.RootElement.GetProperty("host_result_ingestion_applied").GetBoolean());
            Assert.True(runDocument.RootElement.GetProperty("safety_gate_allowed").GetBoolean());

            AssertWorkerExecutionArtifact(sandbox.RootPath, taskId, changedPath);
            AssertPreApprovalEvidence(sandbox.RootPath, taskId, executionRunId);

            var approve = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "approve-review",
                taskId,
                "worker",
                "automation",
                "manual",
                "real-cli-smoke",
                "approved");
            Assert.True(approve.ExitCode == 0, approve.CombinedOutput);
            Assert.Contains($"Approved review for {taskId}", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Materialized 1 approved file(s)", approve.StandardOutput, StringComparison.Ordinal);
            AssertApprovedWriteback(sandbox.RootPath, taskId, changedPath);
        }
        finally
        {
            StopHost(sandbox.RootPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    private static void RegisterExternalCliWorkerSession(
        string repoRoot,
        string repoId,
        string identity,
        string scheduleBinding,
        string contextReceipt,
        string reason)
    {
        var result = ProgramHarness.Run(
            "--repo-root",
            repoRoot,
            "api",
            "actor-session-register",
            "--kind",
            "worker",
            "--identity",
            identity,
            "--repo-id",
            repoId,
            "--provider-profile",
            "external-cli",
            "--capability-profile",
            "external-cli-worker",
            "--scope",
            "host-dispatch-only",
            "--schedule-binding",
            scheduleBinding,
            "--context-receipt",
            contextReceipt,
            "--process-id",
            Environment.ProcessId.ToString(),
            "--health",
            "healthy",
            "--reason",
            reason);

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
    }

    private static void WriteEnabledRoleGovernancePolicy(string repoRoot)
    {
        var policyPath = Path.Combine(repoRoot, ".carves-platform", "policies", "role-governance.policy.json");
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllText(policyPath, """
{
  "version": "1.0",
  "controlled_mode_default": false,
  "producer_cannot_self_approve": true,
  "reviewer_cannot_approve_same_task": true,
  "default_role_binding": {
    "producer": "planner",
    "executor": "worker",
    "reviewer": "planner",
    "approver": "operator",
    "scope_steward": "operator",
    "policy_owner": "operator"
  },
  "validation_lab_follow_on_lanes": [
    "approval_recovery",
    "controlled_mode_governance"
  ],
  "role_mode": "enabled",
  "planner_worker_split_enabled": true,
  "worker_delegation_enabled": true,
  "scheduler_auto_dispatch_enabled": true
}
""");
    }

    private static void AssertScheduleTickHandoff(ProgramHarness.ProgramRunResult scheduleTick, string taskId)
    {
        Assert.True(scheduleTick.ExitCode == 0, scheduleTick.CombinedOutput);
        using var scheduleDocument = ParseJsonOutput(scheduleTick.StandardOutput);
        var root = scheduleDocument.RootElement;
        Assert.Equal("worker_automation_schedule_tick", root.GetProperty("kind").GetString());
        Assert.Equal("blocked", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("dispatch_requested").GetBoolean());
        Assert.True(root.GetProperty("dry_run").GetBoolean());
        Assert.False(root.GetProperty("schedule_tick_can_dispatch").GetBoolean());
        Assert.False(root.GetProperty("host_dispatch_attempted").GetBoolean());
        Assert.True(root.GetProperty("schedule_tick_wakes_host_only").GetBoolean());
        Assert.True(root.GetProperty("uses_existing_task_run_lifecycle").GetBoolean());
        Assert.False(root.GetProperty("creates_second_scheduler").GetBoolean());
        Assert.False(root.GetProperty("creates_task_queue").GetBoolean());
        Assert.False(root.GetProperty("grants_execution_authority").GetBoolean());
        Assert.False(root.GetProperty("grants_truth_write_authority").GetBoolean());
        var handoff = root.GetProperty("host_lifecycle_handoff");
        Assert.Equal("blocked", handoff.GetProperty("status").GetString());
        Assert.False(handoff.GetProperty("ready_for_task_run").GetBoolean());
        Assert.False(handoff.GetProperty("schedule_tick_executes_task").GetBoolean());
        Assert.Equal($"task run {taskId}", handoff.GetProperty("task_run_command").GetString());
        Assert.Equal($"api worker-dispatch-pilot-evidence {taskId}", handoff.GetProperty("post_run_readback_command").GetString());
    }

    private static void AssertWorkerExecutionArtifact(string repoRoot, string taskId, string changedPath)
    {
        var workerArtifactPath = Path.Combine(repoRoot, ".ai", "artifacts", "worker-executions", $"{taskId}.json");
        Assert.True(File.Exists(workerArtifactPath), $"Expected worker execution artifact at {workerArtifactPath}.");
        using var workerArtifactDocument = JsonDocument.Parse(File.ReadAllText(workerArtifactPath));
        var workerResult = workerArtifactDocument.RootElement.GetProperty("result");
        Assert.Equal(taskId, workerResult.GetProperty("task_id").GetString());
        Assert.Equal("succeeded", workerResult.GetProperty("status").GetString());
        Assert.Equal("codex_cli", workerResult.GetProperty("backend_id").GetString());
        Assert.Equal("codex", workerResult.GetProperty("provider_id").GetString());
        Assert.Equal("CodexCliWorkerAdapter", workerResult.GetProperty("adapter_id").GetString());
        Assert.Equal("local_cli", workerResult.GetProperty("protocol_family").GetString());
        Assert.Equal("delegated_worker_launch", workerResult.GetProperty("request_family").GetString());
        Assert.Contains(
            workerResult.GetProperty("changed_files").EnumerateArray(),
            item => string.Equals(item.GetString(), changedPath, StringComparison.Ordinal));

        var claim = workerResult.GetProperty("completion_claim");
        Assert.True(claim.GetProperty("required").GetBoolean());
        Assert.Equal("present", claim.GetProperty("status").GetString());
        Assert.Equal("worker_execution_packet_adapter_generated", claim.GetProperty("source").GetString());
        Assert.False(claim.GetProperty("claim_is_truth").GetBoolean());
        Assert.True(claim.GetProperty("host_validation_required").GetBoolean());
        Assert.Equal("passed", claim.GetProperty("packet_validation_status").GetString());
        Assert.Empty(claim.GetProperty("packet_validation_blockers").EnumerateArray());
        Assert.Empty(claim.GetProperty("missing_fields").EnumerateArray());
        Assert.Empty(claim.GetProperty("missing_contract_items").EnumerateArray());
        Assert.Empty(claim.GetProperty("disallowed_changed_files").EnumerateArray());
        Assert.Empty(claim.GetProperty("forbidden_vocabulary_hits").EnumerateArray());
        Assert.Contains(
            claim.GetProperty("changed_files").EnumerateArray(),
            item => string.Equals(item.GetString(), changedPath, StringComparison.Ordinal));
        Assert.Contains(
            claim.GetProperty("contract_items_satisfied").EnumerateArray(),
            item => string.Equals(item.GetString(), "patch_scope_recorded", StringComparison.Ordinal));
        Assert.Contains(
            claim.GetProperty("contract_items_satisfied").EnumerateArray(),
            item => string.Equals(item.GetString(), "scope_hygiene", StringComparison.Ordinal));
        Assert.NotEmpty(claim.GetProperty("tests_run").EnumerateArray());
        Assert.NotEmpty(claim.GetProperty("evidence_paths").EnumerateArray());

        var evidence = workerArtifactDocument.RootElement.GetProperty("evidence");
        Assert.Equal(taskId, evidence.GetProperty("task_id").GetString());
        Assert.Equal(workerResult.GetProperty("run_id").GetString(), evidence.GetProperty("run_id").GetString());
        Assert.Equal("host", evidence.GetProperty("evidence_source").GetString());
        Assert.Equal("complete", evidence.GetProperty("evidence_completeness").GetString());
        Assert.Equal("replayable", evidence.GetProperty("evidence_strength").GetString());
        Assert.Equal(0, evidence.GetProperty("exit_status").GetInt32());
        Assert.Contains(
            evidence.GetProperty("files_written").EnumerateArray(),
            item => string.Equals(item.GetString(), changedPath, StringComparison.Ordinal));
        Assert.NotEmpty(evidence.GetProperty("commands_executed").EnumerateArray());
    }

    private static void AssertPreApprovalEvidence(string repoRoot, string taskId, string? executionRunId)
    {
        var evidence = ProgramHarness.Run("--repo-root", repoRoot, "api", "worker-dispatch-pilot-evidence", taskId);
        Assert.True(evidence.ExitCode == 0, evidence.CombinedOutput);
        using var evidenceDocument = ParseJsonOutput(evidence.StandardOutput);
        var root = evidenceDocument.RootElement;
        Assert.True(root.GetProperty("pilot_chain_complete").GetBoolean(), root.ToString());
        Assert.Equal(executionRunId, root.GetProperty("dispatch").GetProperty("latest_run_id").GetString());
        Assert.True(root.GetProperty("worker_evidence").GetProperty("present").GetBoolean());
        Assert.Equal(executionRunId, root.GetProperty("result_ingestion").GetProperty("result_execution_run_id").GetString());
        Assert.True(root.GetProperty("result_ingestion").GetProperty("review_submission_present").GetBoolean());
        Assert.True(root.GetProperty("host_validation").GetProperty("valid").GetBoolean());
        Assert.True(root.GetProperty("host_validation").GetProperty("writeback_allowed_by_host_validator").GetBoolean());
        Assert.Equal("present", root.GetProperty("host_validation").GetProperty("completion_claim_status").GetString());
        Assert.Equal("worker_execution_packet_adapter_generated", root.GetProperty("host_validation").GetProperty("completion_claim_source").GetString());
        Assert.Equal("passed", root.GetProperty("host_validation").GetProperty("completion_claim_packet_validation_status").GetString());
        Assert.Empty(root.GetProperty("host_validation").GetProperty("completion_claim_packet_validation_blockers").EnumerateArray());
        Assert.True(root.GetProperty("review").GetProperty("review_bundle_present").GetBoolean());
        Assert.True(root.GetProperty("review").GetProperty("closure_decision_present").GetBoolean());

        var closureDecision = root.GetProperty("review").GetProperty("closure_decision");
        Assert.Equal("writeback_blocked", closureDecision.GetProperty("status").GetString());
        Assert.Equal("block_writeback", closureDecision.GetProperty("decision").GetString());
        Assert.False(closureDecision.GetProperty("writeback_allowed").GetBoolean());
        Assert.Equal("worker", closureDecision.GetProperty("result_source").GetString());
        Assert.Equal("none", closureDecision.GetProperty("accepted_patch_source").GetString());
        Assert.Equal("review_pending", closureDecision.GetProperty("worker_result_verdict").GetString());
        Assert.Equal("pending_review", closureDecision.GetProperty("reviewer_decision").GetString());
        Assert.Equal("passed", closureDecision.GetProperty("required_gate_status").GetString());
        Assert.Equal("passed", closureDecision.GetProperty("contract_matrix_status").GetString());
        Assert.Equal("passed", closureDecision.GetProperty("safety_status").GetString());
        Assert.Contains(
            closureDecision.GetProperty("blockers").EnumerateArray(),
            item => string.Equals(item.GetString(), "reviewer_decision_not_approved:pending_review", StringComparison.Ordinal));
        Assert.True(root.GetProperty("schedule_callback_readback").GetProperty("callback_can_report_closure").GetBoolean());
    }

    private static void AssertApprovedWriteback(string repoRoot, string taskId, string changedPath)
    {
        var targetPath = Path.Combine(repoRoot, changedPath.Replace('/', Path.DirectorySeparatorChar));
        var taskNodePath = Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"{taskId}.json");
        var reviewPath = Path.Combine(repoRoot, ".ai", "artifacts", "reviews", $"{taskId}.json");
        var mergeCandidatePath = Path.Combine(repoRoot, ".ai", "artifacts", "merge-candidates", $"{taskId}.json");
        Assert.True(File.Exists(targetPath), $"Expected approved worker file at {targetPath}.");
        Assert.True(File.Exists(mergeCandidatePath), $"Expected merge candidate at {mergeCandidatePath}.");

        using var taskNode = JsonDocument.Parse(File.ReadAllText(taskNodePath));
        using var reviewNode = JsonDocument.Parse(File.ReadAllText(reviewPath));
        using var mergeCandidate = JsonDocument.Parse(File.ReadAllText(mergeCandidatePath));
        Assert.Equal("completed", taskNode.RootElement.GetProperty("status").GetString());
        Assert.Equal("approved", reviewNode.RootElement.GetProperty("decision_status").GetString());
        Assert.True(reviewNode.RootElement.GetProperty("writeback").GetProperty("applied").GetBoolean());

        var closureBundle = reviewNode.RootElement.GetProperty("closure_bundle");
        Assert.Equal("worker", closureBundle.GetProperty("candidate_result_source").GetString());
        Assert.Equal("accepted", closureBundle.GetProperty("worker_result_verdict").GetString());
        Assert.Equal("worker_patch", closureBundle.GetProperty("accepted_patch_source").GetString());
        Assert.Equal("worker_review_approved", closureBundle.GetProperty("completion_mode").GetString());
        Assert.Equal("approved", closureBundle.GetProperty("reviewer_decision").GetString());
        Assert.Equal("writeback_applied", closureBundle.GetProperty("writeback_recommendation").GetString());
        Assert.Equal("passed", closureBundle.GetProperty("validation").GetProperty("required_gate_status").GetString());
        Assert.Equal("passed", closureBundle.GetProperty("host_validation").GetProperty("status").GetString());
        Assert.Equal("passed", closureBundle.GetProperty("contract_matrix").GetProperty("status").GetString());
        Assert.Empty(closureBundle.GetProperty("closure_decision").GetProperty("blockers").EnumerateArray());
        Assert.Equal(taskId, mergeCandidate.RootElement.GetProperty("task_id").GetString());
    }

    private static JsonDocument ParseJsonOutput(string output)
    {
        var index = output.IndexOf('{');
        return index >= 0
            ? JsonDocument.Parse(output[index..])
            : throw new InvalidOperationException($"Command output did not contain JSON: {output}");
    }

    private static bool IsRealExternalCliWorkerSmokeEnabled()
    {
        var value = Environment.GetEnvironmentVariable(RealExternalWorkerSmokeEnabledEnvironmentVariable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveRealExternalWorkerCliPath()
    {
        var explicitExternalWorkerPath = Environment.GetEnvironmentVariable(RealExternalWorkerCliPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(explicitExternalWorkerPath))
        {
            return Path.GetFullPath(explicitExternalWorkerPath);
        }

        var codexCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(codexCliPath))
        {
            return Path.GetFullPath(codexCliPath);
        }

        return FindExecutableOnPath("codex");
    }

    private static string? FindExecutableOnPath(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            IEnumerable<string> candidates = OperatingSystem.IsWindows()
                ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(extension => Path.Combine(entry, executableName + extension.ToLowerInvariant()))
                    .Concat([Path.Combine(entry, executableName)])
                : [Path.Combine(entry, executableName)];
            var match = candidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(match))
            {
                return Path.GetFullPath(match);
            }
        }

        return null;
    }

    private static bool LooksLikeGeneratedCodexCliStub(string cliPath)
    {
        var fileName = Path.GetFileName(cliPath);
        return fileName.StartsWith("carves-codex-cli-", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("carves-codex-bridge-", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> BuildFileExistsValidationCommand(string relativePath)
    {
        return OperatingSystem.IsWindows()
            ? ["cmd", "/c", $"if exist \"{relativePath.Replace('/', '\\')}\" (exit /b 0) else (exit /b 1)"]
            : ["test", "-f", relativePath];
    }

    private static void ConfigureExternalCliWorkerSmokeTask(string repoRoot, string taskId, string changedPath)
    {
        var taskNodePath = Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"{taskId}.json");
        var taskNode = JsonNode.Parse(File.ReadAllText(taskNodePath))!.AsObject();
        taskNode["title"] = "Real external CLI worker automation smoke";
        taskNode["description"] = $"Create or update `{changedPath}` with a short note proving the real external CLI worker executed inside the delegated worktree.";
        taskNode["acceptance"] = new JsonArray(
            $"create or update {changedPath}",
            "report the changed file in the completion claim",
            "leave lifecycle truth and review/writeback authority to Host");
        taskNode["constraints"] = new JsonArray(
            "do not write .ai truth roots directly",
            "do not write .carves-platform truth directly",
            "do not modify files outside declared scope");
        var acceptanceContract = taskNode["acceptance_contract"]?.AsObject();
        if (acceptanceContract is not null)
        {
            acceptanceContract["title"] = $"Real external CLI worker automation smoke acceptance for {taskId}";
            acceptanceContract["intent"] = new JsonObject
            {
                ["goal"] = $"Prove a real external CLI worker can create {changedPath} through Host-routed task run and review approval.",
                ["business_value"] = "Manual/nightly smoke verifies the real adapter path without entering default CI.",
            };
            acceptanceContract["evidence_required"] = new JsonArray(
                ".ai/artifacts/worker-executions/" + taskId + ".json",
                ".ai/artifacts/reviews/" + taskId + ".json",
                changedPath);
            acceptanceContract["constraints"] = new JsonObject
            {
                ["must_not"] = new JsonArray(
                    "write .ai truth roots directly",
                    "write .carves-platform truth directly",
                    "create a second task queue"),
                ["architecture"] = new JsonArray("Host owns lifecycle truth and review/writeback gates."),
                ["scope_limit"] = changedPath,
            };
        }

        var validation = taskNode["validation"]!.AsObject();
        validation["checks"] = new JsonArray("target file exists after worker run");
        validation["expected_evidence"] = new JsonArray("worker artifact exists", $"{changedPath} exists");
        File.WriteAllText(taskNodePath, taskNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ClearRuntimePackSelection(string repoRoot)
    {
        var selectionRoot = Path.Combine(repoRoot, ".ai", "artifacts", "runtime-pack-selection");
        if (Directory.Exists(selectionRoot))
        {
            Directory.Delete(selectionRoot, recursive: true);
        }
    }

    private static string CreateCodexCliScript(
        string changedPath = "README.md",
        bool includeCompletionClaim = true,
        bool materializeChangedFile = false)
    {
        var agentMessage = includeCompletionClaim
            ? "- changed_files: __CHANGED_PATH__\\n- contract_items_satisfied: patch_scope_recorded; scope_hygiene\\n- tests_run: dotnet test\\n- evidence_paths: worker execution artifact\\n- known_limitations: none\\n- next_recommendation: submit for Host review"
            : "Completed scoped edits for __CHANGED_PATH__.";
        if (OperatingSystem.IsWindows())
        {
            var changedDirectory = Path.GetDirectoryName(changedPath.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
            var materializeCommand = materializeChangedFile
                ? """
  if not "__CHANGED_DIRECTORY__"=="" if not exist "__CHANGED_DIRECTORY__" mkdir "__CHANGED_DIRECTORY__"
  > "__CHANGED_PATH__" echo # External CLI worker automation e2e smoke
  >> "__CHANGED_PATH__" echo.
  >> "__CHANGED_PATH__" echo Generated by external CLI worker integration smoke.
"""
                : string.Empty;
            var path = Path.Combine(Path.GetTempPath(), $"carves-codex-cli-{Guid.NewGuid():N}.cmd");
            File.WriteAllText(path, """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
__MATERIALIZE_CHANGED_FILE__
  echo {"type":"thread.started","thread_id":"stub-codex-cli-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"dotnet test","exit_code":0,"aggregated_output":"ok","error":""}}
  echo {"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"__CHANGED_PATH__","kind":"modify"}]}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"__AGENT_MESSAGE__"}}
  echo {"type":"turn.completed","usage":{"input_tokens":13,"output_tokens":6}}
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
"""
                .Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal)
                .Replace("__CHANGED_DIRECTORY__", changedDirectory, StringComparison.Ordinal)
                .Replace("__MATERIALIZE_CHANGED_FILE__", materializeCommand
                    .Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal)
                    .Replace("__CHANGED_DIRECTORY__", changedDirectory, StringComparison.Ordinal), StringComparison.Ordinal)
                .Replace("__AGENT_MESSAGE__", agentMessage.Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal), StringComparison.Ordinal));
            return path;
        }

        var materializeShell = materializeChangedFile
            ? """
  mkdir -p "$(dirname "__CHANGED_PATH__")"
  cat >"__CHANGED_PATH__" <<'CARVES_SMOKE_EOF'
# External CLI worker automation e2e smoke

Generated by external CLI worker integration smoke.
CARVES_SMOKE_EOF
"""
            : string.Empty;
        var scriptPath = Path.Combine(Path.GetTempPath(), $"carves-codex-cli-{Guid.NewGuid():N}.sh");
        File.WriteAllText(scriptPath, """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
__MATERIALIZE_CHANGED_FILE__
  echo '{"type":"thread.started","thread_id":"stub-codex-cli-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"dotnet test","exit_code":0,"aggregated_output":"ok","error":""}}'
  echo '{"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"__CHANGED_PATH__","kind":"modify"}]}}'
  printf '%s\n' '{"type":"item.completed","item":{"type":"agent_message","text":"__AGENT_MESSAGE__"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":13,"output_tokens":6}}'
  exit 0
fi
echo unsupported >&2
exit 1
"""
            .Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal)
            .Replace("__MATERIALIZE_CHANGED_FILE__", materializeShell.Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace("__AGENT_MESSAGE__", agentMessage.Replace("__CHANGED_PATH__", changedPath, StringComparison.Ordinal), StringComparison.Ordinal));
        File.SetUnixFileMode(scriptPath, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return scriptPath;
    }

    private static void StopHost(string repoRoot)
    {
        ProgramHarness.Run("--repo-root", repoRoot, "host", "stop", "integration cleanup");
    }
}
