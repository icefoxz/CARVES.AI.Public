using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using System.Reflection;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Host;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class HostClientSurfaceTests
{
    private static readonly TimeSpan AcceptedOperationPollInterval = TimeSpan.FromMilliseconds(50);

    private static readonly JsonSerializerOptions CamelCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static void StopHost(string repoRoot)
    {
        ProgramHarness.Run("--repo-root", repoRoot, "host", "stop", "integration cleanup");
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
        var descriptor = new JsonObject
        {
            ["host_id"] = $"stale-host-{Guid.NewGuid():N}",
            ["machine_id"] = Environment.MachineName,
            ["repo_root"] = repoRoot,
            ["base_url"] = baseUrl,
            ["port"] = port,
            ["process_id"] = processId,
            ["runtime_directory"] = runtimeDirectory,
            ["deployment_directory"] = deploymentDirectory,
            ["executable_path"] = executablePath,
            ["started_at"] = DateTimeOffset.UtcNow.AddMinutes(-2).ToString("O"),
            ["version"] = "1.0.0",
            ["stage"] = "Stage-8A fleet discovery and registry completed",
        };

        File.WriteAllText(
            Path.Combine(descriptorDirectory, "descriptor.json"),
            descriptor.ToJsonString(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            }));
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveHostRuntimeDirectory(string repoRoot)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(repoRoot))))
            .ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "carves-runtime-host", hash[..16]);
    }

    private static string ResolveHostStartupLockPath(string repoRoot)
    {
        return Path.Combine(ResolveHostRuntimeDirectory(repoRoot), "startup.lock");
    }

    private static void AddCurrentCommandSurfaceMetadata(JsonObject payload)
    {
        var catalogType = typeof(Program).Assembly.GetType("Carves.Runtime.Host.HostCommandSurfaceCatalog", throwOnError: true)
            ?? throw new InvalidOperationException("Expected HostCommandSurfaceCatalog type.");
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var schemaVersion = catalogType.GetField("SchemaVersion", flags)?.GetRawConstantValue() as string
            ?? throw new InvalidOperationException("Expected HostCommandSurfaceCatalog.SchemaVersion.");
        var fingerprint = catalogType.GetProperty("Fingerprint", flags)?.GetValue(null) as string
            ?? throw new InvalidOperationException("Expected HostCommandSurfaceCatalog.Fingerprint.");
        var commandEntries = catalogType.GetProperty("CommandEntries", flags)?.GetValue(null) as System.Collections.IEnumerable
            ?? throw new InvalidOperationException("Expected HostCommandSurfaceCatalog.CommandEntries.");

        payload["command_surface_schema_version"] = schemaVersion;
        payload["command_surface_fingerprint"] = fingerprint;
        payload["command_surface_command_count"] = commandEntries.Cast<object>().Count();
    }

    private static string WriteHostRoutedPlannerCard(string repoRoot)
    {
        const string relativePath = ".ai/tasks/cards/CARD-HOST-PLAN.md";
        var fullPath = Path.Combine(repoRoot, ".ai", "tasks", "cards", "CARD-HOST-PLAN.md");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(
            fullPath,
            """
            # CARD-HOST-PLAN
            Title: Host routed planner mutation test
            Type: feature
            Priority: P1

            ## Goal
            Prove that `plan-card` can route through the resident host and still preserve explicit `--cold` fallback.

            ## Scope
            - src/CARVES.Runtime.Host/
            - tests/

            ## Acceptance
            - host-routed `plan-card` preview works
            - host-routed `plan-card --persist` works

            ## Constraints
            - extend the existing host-routed control-plane surface only
            - `.ai/memory/architecture/05_EXECUTION_OS_METHODOLOGY.md`

            ## Dependencies
            - none
            """);

        return relativePath;
    }

    private static IReadOnlyList<HostRegistryEntrySnapshot> ReadHostRegistry(string repoRoot)
    {
        var path = ControlPlanePaths.FromRepoRoot(repoRoot).PlatformHostRegistryLiveStateFile;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => new HostRegistryEntrySnapshot(
                item.GetProperty("host_id").GetString() ?? string.Empty,
                item.GetProperty("machine_id").GetString() ?? string.Empty,
                item.GetProperty("endpoint").GetString() ?? string.Empty,
                item.GetProperty("status").GetString() ?? string.Empty,
                item.GetProperty("last_seen").GetDateTimeOffset()))
            .ToArray();
    }

    private static IReadOnlyList<RepoRuntimeRegistryEntrySnapshot> ReadRepoRuntimeRegistry(string repoRoot)
    {
        var path = ControlPlanePaths.FromRepoRoot(repoRoot).PlatformRepoRuntimeRegistryLiveStateFile;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => new RepoRuntimeRegistryEntrySnapshot(
                item.GetProperty("repo_id").GetString() ?? string.Empty,
                item.GetProperty("repo_path").GetString() ?? string.Empty,
                item.GetProperty("host_id").GetString() ?? string.Empty,
                item.GetProperty("status").GetString() ?? string.Empty,
                item.GetProperty("last_seen").GetDateTimeOffset()))
            .ToArray();
    }

    private static string CreateApprovalBridgeScript()
    {
        var path = Path.Combine(Path.GetTempPath(), $"carves-codex-host-rehydrate-{Guid.NewGuid():N}.mjs");
        File.WriteAllText(path, """
import process from "node:process";

const chunks = [];
for await (const chunk of process.stdin) {
  chunks.push(chunk);
}

const request = JSON.parse(Buffer.concat(chunks.map((chunk) => Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk))).toString("utf8").replace(/^\uFEFF/, ""));
const now = new Date().toISOString();
process.stdout.write(JSON.stringify({
  runId: "stub-codex-host-rehydrate",
  requestId: request.requestId,
  status: "approval_wait",
  failureKind: "approval_required",
  retryable: false,
  summary: "worker is waiting for permission approval",
  rationale: "The worker requested permission before writing a file.",
  failureReason: "Permission approval is required before writing README.md.",
  model: request.model || "gpt-5-codex",
  changedFiles: [],
  events: [
    {
      runId: "stub-codex-host-rehydrate",
      taskId: request.taskId,
      eventType: "permission_requested",
      summary: "Permission required to write README.md",
      itemType: "approval",
      filePath: "README.md",
      rawPayload: "Permission required to write README.md",
      attributes: {
        permission_kind: "filesystem_write",
        scope: "workspace"
      },
      occurredAt: now
    },
    {
      runId: "stub-codex-host-rehydrate",
      taskId: request.taskId,
      eventType: "approval_wait",
      summary: "Awaiting operator approval",
      itemType: "approval",
      rawPayload: "Permission required before continuing.",
      attributes: {
        permission_kind: "filesystem_write"
      },
      occurredAt: now
    }
  ],
  commandTrace: [],
  startedAt: now,
  completedAt: now,
  inputTokens: 11,
  outputTokens: 7
}));
""");
        return path;
    }

    private const string CodexSdkWorkerOperationalPolicyJson = """
{
  "version": "1.0",
  "preferred_backend_id": "codex_sdk",
  "preferred_trust_profile_id": "workspace_build_test",
  "approval": {
    "outside_workspace_requires_review": true,
    "high_risk_requires_review": true,
    "manual_approval_mode_requires_review": true,
    "auto_allow_categories": ["filesystem_write", "process_control"],
    "auto_deny_categories": ["secret_access", "elevated_privilege", "system_configuration"],
    "force_review_categories": ["filesystem_delete", "outside_workspace_access", "network_access", "unknown_permission_request"]
  },
  "recovery": {
    "max_retry_count": 2,
    "transient_infra_backoff_seconds": 5,
    "timeout_backoff_seconds": 10,
    "invalid_output_backoff_seconds": 3,
    "environment_rebuild_backoff_seconds": 5,
    "switch_provider_on_environment_blocked": true,
    "switch_provider_on_unavailable_backend": true
  },
  "observability": {
    "provider_degraded_latency_ms": 1500,
    "approval_queue_preview_limit": 8,
    "blocked_queue_preview_limit": 8,
    "incident_preview_limit": 10,
    "governance_report_default_hours": 24
  }
}
""";

    private static PilotProofScenario ProvisionPilotProofScenario(string hostRepoRoot, string targetRepoRoot, string cardId, string draftId, string taskId)
    {
        var scenario = ProvisionPilotDryRunScenario(hostRepoRoot, targetRepoRoot, cardId, draftId, taskId);
        WritePilotFailedResultEnvelope(targetRepoRoot, taskId, scenario.RunId);
        return scenario;
    }

    private static PilotProofScenario ProvisionPilotDryRunScenario(string hostRepoRoot, string targetRepoRoot, string cardId, string draftId, string taskId)
    {
        var payloadDirectory = Path.Combine(hostRepoRoot, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadDirectory);
        var cardPayload = Path.Combine(payloadDirectory, "card-create.json");
        File.WriteAllText(cardPayload, JsonSerializer.Serialize(new
        {
            card_id = cardId,
            title = "Pilot proof card",
            goal = "Prove attach to task runtime truth.",
            acceptance = new[] { "approved card enters planning", "attach proof is capturable" },
        }));
        var taskGraphPayload = Path.Combine(payloadDirectory, "taskgraph-draft.json");
        File.WriteAllText(taskGraphPayload, JsonSerializer.Serialize(new
        {
            draft_id = draftId,
            card_id = cardId,
            tasks = new object[]
            {
                new
                {
                    task_id = taskId,
                    title = "Pilot proof task",
                    description = "Create the first governed attach-to-task proof.",
                    scope = new[] { "src/" },
                    acceptance = new[] { "execution run truth exists", "result ingestion produces replan truth" },
                    proof_target = new
                    {
                        kind = "boundary",
                        description = "Capture attach-to-task proof artifacts and review/replan truth for the scoped pilot execution task.",
                    },
                },
            },
        }));

        var attach = ProgramHarness.Run("--repo-root", targetRepoRoot, "attach");
        var create = ProgramHarness.Run("--repo-root", targetRepoRoot, "create-card-draft", cardPayload);
        var approve = ProgramHarness.Run("--repo-root", targetRepoRoot, "approve-card", cardId, "pilot approved");
        var createTaskGraph = ProgramHarness.Run("--repo-root", targetRepoRoot, "create-taskgraph-draft", taskGraphPayload);
        var approveTaskGraph = ProgramHarness.Run("--repo-root", targetRepoRoot, "approve-taskgraph-draft", draftId, "pilot taskgraph approved");
        var run = ProgramHarness.Run("--repo-root", targetRepoRoot, "task", "run", taskId, "--dry-run");
        var inspect = ProgramHarness.Run("--repo-root", targetRepoRoot, "task", "inspect", taskId, "--runs");

        Assert.True(attach.ExitCode == 0, attach.CombinedOutput);
        Assert.True(create.ExitCode == 0, create.CombinedOutput);
        Assert.True(approve.ExitCode == 0, approve.CombinedOutput);
        Assert.True(createTaskGraph.ExitCode == 0, createTaskGraph.CombinedOutput);
        Assert.True(approveTaskGraph.ExitCode == 0, approveTaskGraph.CombinedOutput);
        Assert.True(run.ExitCode == 0, run.CombinedOutput);
        Assert.True(inspect.ExitCode == 0, inspect.CombinedOutput);

        using var inspectDocument = ParseJsonOutput(inspect.StandardOutput);
        var runId = inspectDocument.RootElement.GetProperty("execution_run").GetProperty("latest_run_id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(runId));
        return new PilotProofScenario(cardId, taskId, runId!);
    }

    private static void WritePilotFailedResultEnvelope(string repoRoot, string taskId, string runId)
    {
        var evidenceDirectory = Path.Combine(repoRoot, ".ai", "artifacts", "worker-executions", runId);
        Directory.CreateDirectory(evidenceDirectory);
        var evidencePath = Path.Combine(evidenceDirectory, "evidence.json");
        var commandLogPath = Path.Combine(evidenceDirectory, "command.log");
        var buildLogPath = Path.Combine(evidenceDirectory, "build.log");
        var testLogPath = Path.Combine(evidenceDirectory, "test.log");
        var patchPath = Path.Combine(evidenceDirectory, "patch.diff");
        File.WriteAllText(commandLogPath, "dotnet build" + Environment.NewLine + "dotnet test");
        File.WriteAllText(buildLogPath, "Build succeeded.");
        File.WriteAllText(testLogPath, "Pilot proof intentionally recorded a bounded regression.");
        File.WriteAllText(
            patchPath,
            "diff --git a/src/PilotProof.cs b/src/PilotProof.cs" + Environment.NewLine
            + "--- a/src/PilotProof.cs" + Environment.NewLine
            + "+++ b/src/PilotProof.cs" + Environment.NewLine
            + "@@" + Environment.NewLine
            + "+// Pilot proof bounded failure");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
        {
            schema_version = 1,
            run_id = runId,
            task_id = taskId,
            worker_id = "CodexCliWorkerAdapter",
            evidence_source = "host",
            files_read = new[] { "src/PilotProof.cs" },
            files_written = new[] { "src/PilotProof.cs" },
            commands_executed = new[] { "dotnet build", "dotnet test" },
            evidence_path = $".ai/artifacts/worker-executions/{runId}/evidence.json",
            build_output_ref = $".ai/artifacts/worker-executions/{runId}/build.log",
            test_output_ref = $".ai/artifacts/worker-executions/{runId}/test.log",
            command_log_ref = $".ai/artifacts/worker-executions/{runId}/command.log",
            patch_ref = $".ai/artifacts/worker-executions/{runId}/patch.diff",
            artifacts = new[]
            {
                $".ai/artifacts/worker-executions/{runId}/evidence.json",
                $".ai/artifacts/worker-executions/{runId}/command.log",
                $".ai/artifacts/worker-executions/{runId}/build.log",
                $".ai/artifacts/worker-executions/{runId}/test.log",
                $".ai/artifacts/worker-executions/{runId}/patch.diff",
            },
            exit_status = 1,
            evidence_completeness = "complete",
            evidence_strength = "replayable",
        }, CamelCaseJsonOptions));
        File.WriteAllText(Path.Combine(repoRoot, ".ai", "artifacts", "worker-executions", $"{taskId}.json"), JsonSerializer.Serialize(new
        {
            schema_version = 1,
            captured_at = DateTimeOffset.UtcNow,
            task_id = taskId,
            result = new
            {
                schema_version = 1,
                run_id = runId,
                task_id = taskId,
                backend_id = "codex_cli",
                provider_id = "codex",
                adapter_id = "CodexCliWorkerAdapter",
                adapter_reason = "pilot",
                profile_id = "extended_dev_ops",
                trusted_profile = true,
                status = "failed",
                failure_kind = "test_failure",
                failure_layer = "worker_semantic",
                retryable = false,
                configured = true,
                model = "gpt-5-codex",
                request_preview = "pilot",
                request_hash = "pilot",
                summary = "Pilot proof intentionally recorded a bounded regression to exercise replan truth.",
                changed_files = new[] { "src/PilotProof.cs" },
                events = Array.Empty<object>(),
                permission_requests = Array.Empty<object>(),
                command_trace = Array.Empty<object>(),
                started_at = DateTimeOffset.UtcNow.AddSeconds(-30),
                completed_at = DateTimeOffset.UtcNow,
            },
            evidence = new
            {
                schema_version = 1,
                run_id = runId,
                task_id = taskId,
                worker_id = "CodexCliWorkerAdapter",
                started_at = DateTimeOffset.UtcNow.AddSeconds(-30),
                ended_at = DateTimeOffset.UtcNow,
                evidence_source = "host",
                files_read = new[] { "src/PilotProof.cs" },
                files_written = new[] { "src/PilotProof.cs" },
                commands_executed = new[] { "dotnet build", "dotnet test" },
                evidence_path = $".ai/artifacts/worker-executions/{runId}/evidence.json",
                build_output_ref = $".ai/artifacts/worker-executions/{runId}/build.log",
                test_output_ref = $".ai/artifacts/worker-executions/{runId}/test.log",
                command_log_ref = $".ai/artifacts/worker-executions/{runId}/command.log",
                patch_ref = $".ai/artifacts/worker-executions/{runId}/patch.diff",
                artifacts = new[]
                {
                    $".ai/artifacts/worker-executions/{runId}/evidence.json",
                    $".ai/artifacts/worker-executions/{runId}/command.log",
                    $".ai/artifacts/worker-executions/{runId}/build.log",
                    $".ai/artifacts/worker-executions/{runId}/test.log",
                    $".ai/artifacts/worker-executions/{runId}/patch.diff",
                },
                exit_status = 1,
                evidence_completeness = "complete",
                evidence_strength = "replayable",
            },
        }, CamelCaseJsonOptions));

        var resultPath = Path.Combine(repoRoot, ".ai", "execution", taskId, "result.json");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        File.WriteAllText(resultPath, JsonSerializer.Serialize(new ResultEnvelope
        {
            TaskId = taskId,
            ExecutionRunId = runId,
            ExecutionEvidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json",
            Status = "failed",
            Changes = new ResultEnvelopeChanges
            {
                FilesModified = ["src/PilotProof.cs"],
                LinesChanged = 8,
            },
            Validation = new ResultEnvelopeValidation
            {
                CommandsRun = ["dotnet build", "dotnet test"],
                Build = "success",
                Tests = "failed",
            },
            Result = new ResultEnvelopeOutcome
            {
                StopReason = "test_failure",
            },
            Failure = new ResultEnvelopeFailure
            {
                Type = nameof(FailureType.TestRegression),
                Message = "Pilot proof intentionally recorded a bounded regression to exercise replan truth.",
            },
            Next = new ResultEnvelopeNextAction
            {
                Suggested = "Generate a focused repair follow-up task.",
            },
            Telemetry = new ExecutionTelemetry
            {
                DurationSeconds = 45,
                ObservedPaths = ["src/PilotProof.cs"],
                ChangeKinds = [ExecutionChangeKind.SourceCode],
                BudgetExceeded = false,
                Summary = "Pilot proof stayed within the declared execution budget.",
            },
        }, CamelCaseJsonOptions));
    }

    private static void SeedFailedDelegatedRun(string repoRoot, string taskId)
    {
        var nodePath = Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"{taskId}.json");
        var node = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        node["last_worker_run_id"] = $"RUN-{taskId}-001";
        node["last_worker_backend"] = "codex_cli";
        node["last_worker_failure_kind"] = "timeout";
        node["last_worker_retryable"] = true;
        node["last_worker_summary"] = "Synthetic delegated timeout should remain as historical execution evidence.";
        node["last_worker_detail_ref"] = $".ai/artifacts/worker-executions/{taskId}.json";
        node["last_provider_detail_ref"] = $".ai/artifacts/provider/{taskId}.json";
        node["last_recovery_action"] = "retry";
        node["last_recovery_reason"] = "Retryable delegated timeout.";
        node["retry_not_before"] = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O");
        var metadata = node["metadata"]?.AsObject() ?? new JsonObject();
        metadata["execution_run_latest_id"] = $"RUN-{taskId}-001";
        metadata["execution_run_latest_status"] = "Failed";
        metadata["execution_run_current_step_index"] = "4";
        metadata["execution_run_current_step_title"] = "Clean execution residue and confirm shutdown.";
        metadata["execution_run_count"] = "1";
        node["metadata"] = metadata;
        File.WriteAllText(nodePath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var runRoot = Path.Combine(repoRoot, ".ai", "runtime", "runs", taskId);
        Directory.CreateDirectory(runRoot);
        var runId = $"RUN-{taskId}-001";
        var run = new ExecutionRun
        {
            RunId = runId,
            TaskId = taskId,
            Status = ExecutionRunStatus.Failed,
            CurrentStepIndex = 4,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            TriggerReason = ExecutionRunTriggerReason.Initial,
            Goal = "Synthetic delegated failure for manual fallback reconciliation.",
            Steps =
            [
                new ExecutionStep { StepId = $"{runId}-STEP-001", Title = "Inspect", Kind = ExecutionStepKind.Inspect, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-STEP-002", Title = "Implement", Kind = ExecutionStepKind.Implement, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-STEP-003", Title = "Verify", Kind = ExecutionStepKind.Verify, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-STEP-004", Title = "Writeback", Kind = ExecutionStepKind.Writeback, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-STEP-005", Title = "Cleanup", Kind = ExecutionStepKind.Cleanup, Status = ExecutionStepStatus.Completed, Notes = "Synthetic timeout during delegated cleanup." },
            ],
        };
        File.WriteAllText(Path.Combine(runRoot, $"{runId}.json"), JsonSerializer.Serialize(run, CamelCaseJsonOptions));

        var paths = ControlPlanePaths.FromRepoRoot(repoRoot);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = runId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                AdapterReason = "Synthetic manual fallback reconciliation fixture",
                Status = WorkerExecutionStatus.TimedOut,
                FailureKind = WorkerFailureKind.Timeout,
                FailureLayer = WorkerFailureLayer.Transport,
                Summary = "Synthetic delegated timeout should remain as historical execution evidence.",
                Retryable = true,
            },
        });
    }

    private static void AssignSyntheticCardId(string repoRoot, string cardId, params string[] taskIds)
    {
        var graphPath = Path.Combine(repoRoot, ".ai", "tasks", "graph.json");
        var graph = JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        foreach (var taskNode in graph["tasks"]!.AsArray())
        {
            var taskObject = taskNode!.AsObject();
            var taskId = taskObject["task_id"]?.GetValue<string>();
            if (taskId is null || !taskIds.Contains(taskId, StringComparer.Ordinal))
            {
                continue;
            }

            taskObject["card_id"] = cardId;
        }

        File.WriteAllText(graphPath, graph.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        foreach (var taskId in taskIds)
        {
            var nodePath = Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"{taskId}.json");
            var node = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
            node["card_id"] = cardId;
            File.WriteAllText(nodePath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static int CountTasksForCard(string repoRoot, string cardId)
    {
        var graphPath = Path.Combine(repoRoot, ".ai", "tasks", "graph.json");
        var graph = JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        return graph["tasks"]!.AsArray()
            .Count(taskNode => string.Equals(taskNode?["card_id"]?.GetValue<string>(), cardId, StringComparison.Ordinal));
    }

    private static string ReadTaskStatus(string repoRoot, string taskId)
    {
        var nodePath = Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"{taskId}.json");
        var node = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        return node["status"]!.GetValue<string>();
    }

    private static string ReadRepoHostBaseUrl(string repoRoot)
    {
        var descriptorPath = Path.Combine(repoRoot, ".carves-platform", "host", "descriptor.json");
        var descriptor = JsonNode.Parse(File.ReadAllText(descriptorPath))!.AsObject();
        return descriptor["base_url"]!.GetValue<string>();
    }

    private static JsonDocument ParseJsonOutput(string output)
    {
        var index = output.IndexOf('{');
        return index >= 0
            ? JsonDocument.Parse(output[index..])
            : throw new InvalidOperationException($"Command output did not contain JSON: {output}");
    }

    private static async Task<JsonElement> PollAcceptedOperationStatus(HttpClient client, string baseUrl, string operationId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        JsonDocument? latest = null;
        try
        {
            while (DateTimeOffset.UtcNow < deadline)
            {
                latest?.Dispose();
                latest = JsonDocument.Parse(
                    await client.GetStringAsync($"{baseUrl}/operations/{Uri.EscapeDataString(operationId)}"));
                if (latest.RootElement.GetProperty("completed").GetBoolean())
                {
                    return latest.RootElement.Clone();
                }

                await Task.Delay(AcceptedOperationPollInterval);
            }

            throw new TimeoutException($"Accepted operation '{operationId}' did not complete before the test deadline.");
        }
        finally
        {
            latest?.Dispose();
        }
    }

    private static string ExtractOutputValue(string output, string prefix)
    {
        return output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith(prefix, StringComparison.Ordinal))
            [prefix.Length..]
            .Trim();
    }

    private static void WriteHostDescriptor(
        string path,
        string repoRoot,
        string baseUrl,
        int processId,
        string hostId,
        string machineId)
    {
        var uri = new Uri(baseUrl);
        var descriptor = new JsonObject
        {
            ["host_id"] = hostId,
            ["machine_id"] = machineId,
            ["repo_root"] = repoRoot,
            ["base_url"] = baseUrl,
            ["port"] = uri.Port,
            ["process_id"] = processId,
            ["runtime_directory"] = $@"C:\fake\{hostId}",
            ["deployment_directory"] = $@"C:\fake\{hostId}\deployment",
            ["executable_path"] = $@"C:\fake\{hostId}\deployment\Carves.Runtime.Host.dll",
            ["started_at"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
            ["version"] = "1.0.0",
            ["stage"] = "Stage-8A fleet discovery and registry completed",
        };

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, descriptor.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        }));
    }

    private static void SeedStaleHostSnapshot(string repoRoot, string baseUrl, int processId)
    {
        var uri = new Uri(baseUrl);
        var snapshot = new JsonObject
        {
            ["schema_version"] = 1,
            ["repo_root"] = repoRoot,
            ["state"] = "stale",
            ["summary"] = "Host descriptor existed, but no live host responded.",
            ["base_url"] = baseUrl,
            ["port"] = uri.Port,
            ["process_id"] = processId,
            ["runtime_directory"] = @"C:\fake\snapshot",
            ["deployment_directory"] = @"C:\fake\snapshot\deployment",
            ["executable_path"] = @"C:\fake\snapshot\deployment\Carves.Runtime.Host.dll",
            ["version"] = "1.0.0",
            ["stage"] = "Stage-8A fleet discovery and registry completed",
            ["session_status"] = "Paused",
            ["host_control_state"] = "running",
            ["host_control_reason"] = "Persisted stale snapshot seed.",
            ["active_worker_count"] = 0,
            ["active_task_ids"] = new JsonArray(),
            ["pending_approval_count"] = 0,
            ["rehydrated"] = true,
            ["rehydration_summary"] = "Synthetic stale snapshot for CARD-309.",
            ["started_at"] = DateTimeOffset.UtcNow.AddMinutes(-2).ToString("O"),
            ["recorded_at"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
            ["request_count"] = 0,
        };

        var snapshotPath = ControlPlanePaths.FromRepoRoot(repoRoot).PlatformHostSnapshotLiveStateFile;
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        File.WriteAllText(snapshotPath, snapshot.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        }));
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed record HostRegistryEntrySnapshot(
        string HostId,
        string MachineId,
        string Endpoint,
        string Status,
        DateTimeOffset LastSeen);

    private sealed record RepoRuntimeRegistryEntrySnapshot(
        string RepoId,
        string RepoPath,
        string HostId,
        string Status,
        DateTimeOffset LastSeen);

    private sealed record PilotProofScenario(
        string CardId,
        string TaskId,
        string RunId);

    private static GitSandboxHandle CreateGitSandbox()
    {
        return GitSandboxHandle.Create();
    }

    private sealed class GitSandboxHandle : IDisposable
    {
        private GitSandboxHandle(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static GitSandboxHandle Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-runtime-host-surface", Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(rootPath);
            return new GitSandboxHandle(rootPath);
        }

        public void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void CommitAll(string message)
        {
            RunGit(RootPath, "add", ".");
            RunGit(RootPath, "commit", "-m", message);
        }

        public void Dispose()
        {
            if (!Directory.Exists(RootPath))
            {
                return;
            }

            try
            {
                Directory.Delete(RootPath, true);
            }
            catch
            {
            }
        }

        private static void RunGit(string workingDirectory, params string[] arguments)
        {
            GitTestHarness.Run(workingDirectory, arguments);
        }
    }

    private sealed class FakeDiscoveryHost : IAsyncDisposable
    {
        private readonly HttpListener listener;
        private readonly CancellationTokenSource cancellationSource = new();
        private readonly Task serverTask;
        private readonly int handshakeFailuresBeforeSuccess;
        private readonly string marker;
        private int handshakeRequestCount;

        private FakeDiscoveryHost(int handshakeFailuresBeforeSuccess, string marker, string baseUrl, HttpListener listener)
        {
            this.handshakeFailuresBeforeSuccess = handshakeFailuresBeforeSuccess;
            this.marker = marker;
            BaseUrl = baseUrl;
            this.listener = listener;
            serverTask = ListenAsync();
        }

        public string BaseUrl { get; }

        public int HandshakeRequestCount => Volatile.Read(ref handshakeRequestCount);

        public static FakeDiscoveryHost Start(int handshakeFailuresBeforeSuccess = 0, string marker = "fake-workbench")
        {
            var port = ReserveLoopbackPort();
            var baseUrl = $"http://127.0.0.1:{port}";
            var listener = new HttpListener();
            listener.Prefixes.Add($"{baseUrl}/");
            listener.Start();
            return new FakeDiscoveryHost(handshakeFailuresBeforeSuccess, marker, baseUrl, listener);
        }

        public async ValueTask DisposeAsync()
        {
            cancellationSource.Cancel();
            try
            {
                listener.Stop();
                listener.Close();
            }
            catch
            {
            }

            try
            {
                await serverTask.ConfigureAwait(false);
            }
            catch
            {
            }

            cancellationSource.Dispose();
        }

        private async Task ListenAsync()
        {
            while (!cancellationSource.IsCancellationRequested)
            {
                HttpListenerContext? context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (cancellationSource.IsCancellationRequested)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                HandleRequest(context);
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (string.Equals(path, "/handshake", StringComparison.OrdinalIgnoreCase))
            {
                var attempt = Interlocked.Increment(ref handshakeRequestCount);
                if (attempt <= handshakeFailuresBeforeSuccess)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    context.Response.Close();
                    return;
                }

                var payload = new JsonObject
                {
                    ["host_running"] = true,
                    ["version"] = "1.0.0",
                    ["standard_version"] = "1.0.0",
                    ["stage"] = "Stage-8A fleet discovery and registry completed",
                    ["host_session_id"] = $"fake-session-{marker}",
                    ["uptime_seconds"] = 12,
                    ["base_url"] = BaseUrl,
                    ["dashboard_url"] = $"{BaseUrl}/dashboard",
                    ["runtime_directory"] = $@"C:\fake\{marker}",
                    ["deployment_directory"] = $@"C:\fake\{marker}\deployment",
                    ["executable_path"] = $@"C:\fake\{marker}\deployment\Carves.Runtime.Host.dll",
                    ["repo_count"] = 1,
                    ["attached_repo_count"] = 1,
                    ["active_session_count"] = 0,
                    ["planner_state"] = "idle",
                    ["worker_count"] = 0,
                    ["active_worker_count"] = 0,
                    ["actor_session_count"] = 0,
                    ["operator_os_event_count"] = 0,
                    ["rehydrated"] = true,
                    ["pending_approval_count"] = 0,
                    ["recent_incident_count"] = 0,
                    ["stale_marker_count"] = 0,
                    ["paused_runtime_count"] = 0,
                    ["rehydration_summary"] = $"Fake host ready for {marker}.",
                    ["host_control_state"] = "running",
                    ["host_control_action"] = "started",
                    ["host_control_reason"] = $"Fake host {marker} started.",
                    ["host_control_at"] = DateTimeOffset.UtcNow.ToString("O"),
                    ["protocol_mode"] = "hosted",
                    ["conversation_phase"] = "execution",
                    ["intent_state"] = "active",
                    ["prompt_kernel"] = "fake-kernel@1.0.0",
                    ["project_understanding_state"] = "ready",
                    ["capabilities"] = new JsonArray(
                        "runtime-status",
                        "control-plane-mutation",
                        "dashboard",
                        "workbench",
                        "planner-inspect",
                        "worker-inspect",
                        "agent-gateway",
                        "session-gateway-v1",
                        "delegated-execution",
                        "card-task-inspect",
                        "discussion-surface",
                        "attach-flow",
                        "interaction-surface"),
                };
                AddCurrentCommandSurfaceMetadata(payload);
                WriteJson(context.Response, payload);
                return;
            }

            if (path.StartsWith("/workbench", StringComparison.OrdinalIgnoreCase))
            {
                WriteHtml(context.Response, $"<html><body><h1>CARVES Fake Workbench</h1><p>{marker}</p></body></html>");
                return;
            }

            if (string.Equals(path, "/dashboard", StringComparison.OrdinalIgnoreCase))
            {
                WriteHtml(context.Response, $"<html><body><h1>CARVES Fake Dashboard</h1><p>{marker}</p></body></html>");
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.Close();
        }

        private static void WriteHtml(HttpListenerResponse response, string html)
        {
            var buffer = System.Text.Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private static void WriteJson(HttpListenerResponse response, JsonObject payload)
        {
            var json = payload.ToJsonString(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            });
            var buffer = System.Text.Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }

    private sealed class LockedFileProcess : IDisposable
    {
        private readonly Process process;

        private LockedFileProcess(Process process)
        {
            this.process = process;
        }

        public int ProcessId => process.Id;

        public static LockedFileProcess Start(string filePath)
        {
            var escapedPath = filePath.Replace("'", "''", StringComparison.Ordinal);
            var command = "$path = '" + escapedPath + "'; "
                + "$stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None); "
                + "try { Start-Sleep -Seconds 120 } finally { $stream.Dispose() }";
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(command);

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start locking PowerShell process.");
            WaitForExclusiveLock(filePath, process);
            return new LockedFileProcess(process);
        }

        public void Dispose()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
            }

            process.Dispose();
        }

        private static void WaitForExclusiveLock(string filePath, Process process)
        {
            var acquired = IntegrationTestWait.WaitUntil(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(10),
                () =>
                {
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException($"Locking PowerShell process exited early: {process.StandardError.ReadToEnd()}");
                    }

                    try
                    {
                        using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        return false;
                    }
                    catch (IOException)
                    {
                        return true;
                    }
                });

            if (!acquired)
            {
                throw new InvalidOperationException("Timed out waiting for the stale deployment file lock to become active.");
            }
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
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = "powershell";
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add("Start-Sleep -Seconds 120");
            }
            else
            {
                startInfo.FileName = "bash";
                startInfo.ArgumentList.Add("-lc");
                startInfo.ArgumentList.Add("sleep 120");
            }

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start long-running placeholder process.");
            IntegrationTestWait.Delay(TimeSpan.FromMilliseconds(50));
            return new LongRunningProcess(process);
        }

        public void Dispose()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
            }

            process.Dispose();
        }
    }
}
