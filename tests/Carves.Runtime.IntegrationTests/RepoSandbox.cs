using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

internal sealed class RepoSandbox : IDisposable
{
    private static readonly HashSet<string> PlatformStateDirectoriesToReset = new(StringComparer.OrdinalIgnoreCase)
    {
        "repos",
        "events",
        "fleet",
        "runtime-state",
        "sessions",
        "workers",
    };

    private RepoSandbox(string rootPath)
    {
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public static RepoSandbox CreateFromCurrentRepo()
    {
        return CreateFromCurrentRepo(includeCodegraph: true);
    }

    public static RepoSandbox CreateFromCurrentRepoWithoutCodegraph()
    {
        return CreateFromCurrentRepo(includeCodegraph: false);
    }

    private static RepoSandbox CreateFromCurrentRepo(bool includeCodegraph)
    {
        PruneLeakedIntegrationHost();

        var sourceRoot = ResolveSourceRepoRoot();
        var sandboxRoot = Path.Combine(Path.GetTempPath(), "carves-runtime-integration", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandboxRoot);

        CopyDirectory(
            Path.Combine(sourceRoot, ".ai"),
            Path.Combine(sandboxRoot, ".ai"),
            relativePath => string.Equals(relativePath, "validation", StringComparison.OrdinalIgnoreCase)
                || (!includeCodegraph && string.Equals(relativePath, "codegraph", StringComparison.OrdinalIgnoreCase)));
        CopyDirectory(
            Path.Combine(sourceRoot, ".carves-platform"),
            Path.Combine(sandboxRoot, ".carves-platform"),
            static relativePath => PlatformStateDirectoriesToReset.Contains(relativePath));
        CopyDirectory(Path.Combine(sourceRoot, ".codex"), Path.Combine(sandboxRoot, ".codex"));
        CopyDirectory(Path.Combine(sourceRoot, "templates"), Path.Combine(sandboxRoot, "templates"));
        CopyDirectory(Path.Combine(sourceRoot, "docs"), Path.Combine(sandboxRoot, "docs"));
        CopyDirectory(Path.Combine(sourceRoot, "scripts"), Path.Combine(sandboxRoot, "scripts"));
        CopyDirectory(Path.Combine(sourceRoot, "src"), Path.Combine(sandboxRoot, "src"));
        CopyDirectory(Path.Combine(sourceRoot, "tests"), Path.Combine(sandboxRoot, "tests"));
        File.Copy(Path.Combine(sourceRoot, "carves"), Path.Combine(sandboxRoot, "carves"));
        File.Copy(Path.Combine(sourceRoot, "carves.ps1"), Path.Combine(sandboxRoot, "carves.ps1"));
        File.Copy(Path.Combine(sourceRoot, "carves.cmd"), Path.Combine(sandboxRoot, "carves.cmd"));
        File.Copy(Path.Combine(sourceRoot, "README.md"), Path.Combine(sandboxRoot, "README.md"));
        File.Copy(Path.Combine(sourceRoot, "AGENTS.md"), Path.Combine(sandboxRoot, "AGENTS.md"));
        File.Copy(Path.Combine(sourceRoot, "CARVES.Runtime.sln"), Path.Combine(sandboxRoot, "CARVES.Runtime.sln"));
        EnsureTaskGraphNodeFiles(sourceRoot, sandboxRoot);
        ResetRuntimeSession(Path.Combine(sandboxRoot, ".ai", "runtime"));
        ResetValidationState(Path.Combine(sandboxRoot, ".ai", "validation"));
        ResetPlatformState(Path.Combine(sandboxRoot, ".carves-platform"));
        SeedIsolatedHostDescriptor(sandboxRoot);

        return new RepoSandbox(sandboxRoot);
    }

    public void MarkAllTasksCompleted()
    {
        var graphPath = Path.Combine(RootPath, ".ai", "tasks", "graph.json");
        var graphNode = JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        var tasks = graphNode["tasks"]!.AsArray();
        foreach (var taskNode in tasks)
        {
            var taskObject = taskNode!.AsObject();
            taskObject["status"] = "completed";
            var nodeFile = taskObject["node_file"]!.GetValue<string>();
            var nodePath = Path.Combine(RootPath, ".ai", "tasks", nodeFile.Replace('/', Path.DirectorySeparatorChar));
            var taskDocument = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
            taskDocument["status"] = "completed";
            File.WriteAllText(nodePath, taskDocument.ToJsonString(new() { WriteIndented = true }));
        }

        File.WriteAllText(graphPath, graphNode.ToJsonString(new() { WriteIndented = true }));
    }

    public void AddSyntheticTask(
        string taskId,
        string status,
        string title = "Synthetic integration task",
        IReadOnlyList<string>? scope = null,
        IReadOnlyList<IReadOnlyList<string>>? validationCommands = null,
        IReadOnlyList<string>? dependencies = null,
        bool includeAcceptanceContract = true)
    {
        var resolvedScope = scope ?? [$"tests/{taskId}.cs"];
        var resolvedValidationCommands = validationCommands ?? Array.Empty<IReadOnlyList<string>>();
        var resolvedDependencies = dependencies ?? Array.Empty<string>();
        var graphPath = Path.Combine(RootPath, ".ai", "tasks", "graph.json");
        var graphNode = JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        var tasks = graphNode["tasks"]!.AsArray();
        tasks.Add(new JsonObject
        {
            ["task_id"] = taskId,
            ["title"] = title,
            ["status"] = status,
            ["priority"] = "P1",
            ["card_id"] = "CARD-INTEGRATION",
            ["dependencies"] = ToJsonArray(resolvedDependencies),
            ["node_file"] = $"nodes/{taskId}.json",
        });
        File.WriteAllText(graphPath, graphNode.ToJsonString(new() { WriteIndented = true }));

        var nodePath = Path.Combine(RootPath, ".ai", "tasks", "nodes", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(nodePath)!);
        var taskNode = new JsonObject
        {
            ["schema_version"] = 1,
            ["task_id"] = taskId,
            ["title"] = title,
            ["description"] = "Ensures operator and runtime loop commands have a deterministic task fixture during integration tests.",
            ["status"] = status,
            ["task_type"] = "execution",
            ["priority"] = "P1",
            ["source"] = "INTEGRATION",
            ["card_id"] = "CARD-INTEGRATION",
            ["base_commit"] = "abc123",
            ["result_commit"] = null,
            ["dependencies"] = ToJsonArray(resolvedDependencies),
            ["scope"] = ToJsonArray(resolvedScope),
            ["acceptance"] = new JsonArray("worker artifact is written"),
            ["acceptance_contract"] = includeAcceptanceContract ? CreateSyntheticAcceptanceContract(taskId) : null,
            ["constraints"] = new JsonArray("keep integration fixture deterministic"),
            ["validation"] = new JsonObject
            {
                ["commands"] = ToCommandJsonArray(resolvedValidationCommands),
                ["checks"] = new JsonArray("dry-run command succeeds"),
                ["expected_evidence"] = new JsonArray("worker artifact exists"),
            },
            ["retry_count"] = 0,
            ["capabilities"] = new JsonArray(),
            ["metadata"] = new JsonObject(),
            ["planner_review"] = new JsonObject
            {
                ["verdict"] = "continue",
                ["reason"] = "Synthetic integration task is ready for execution.",
                ["acceptance_met"] = false,
                ["boundary_preserved"] = true,
                ["scope_drift_detected"] = false,
                ["follow_up_suggestions"] = new JsonArray(),
            },
            ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["updated_at"] = DateTimeOffset.UtcNow.ToString("O"),
        };
        File.WriteAllText(nodePath, taskNode.ToJsonString(new() { WriteIndented = true }));
    }

    public void AddSyntheticPendingTask(
        string taskId,
        IReadOnlyList<string>? scope = null,
        IReadOnlyList<IReadOnlyList<string>>? validationCommands = null,
        IReadOnlyList<string>? dependencies = null,
        bool includeAcceptanceContract = true)
    {
        AddSyntheticTask(taskId, "pending", "Synthetic integration dry-run task", scope, validationCommands, dependencies, includeAcceptanceContract);
    }

    public void AddSyntheticReviewTask(
        string taskId,
        IReadOnlyList<string>? scope = null,
        IReadOnlyList<IReadOnlyList<string>>? validationCommands = null,
        IReadOnlyList<string>? dependencies = null,
        bool includeAcceptanceContract = true)
    {
        AddSyntheticTask(taskId, "review", "Synthetic integration review task", scope, validationCommands, dependencies, includeAcceptanceContract);

        var nodePath = Path.Combine(RootPath, ".ai", "tasks", "nodes", $"{taskId}.json");
        var taskNode = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        var resolvedScope = taskNode["scope"]?.AsArray()
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray()
            ?? Array.Empty<string>();
        taskNode["planner_review"] = new JsonObject
        {
            ["verdict"] = "pause_for_review",
            ["reason"] = $"Synthetic review fixture awaits human review for {taskId}.",
            ["acceptance_met"] = true,
            ["boundary_preserved"] = true,
            ["scope_drift_detected"] = false,
            ["follow_up_suggestions"] = new JsonArray(),
        };
        File.WriteAllText(nodePath, taskNode.ToJsonString(new() { WriteIndented = true }));

        var reviewArtifactPath = Path.Combine(RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reviewArtifactPath)!);
        var reviewArtifact = new JsonObject
        {
            ["schema_version"] = 1,
            ["task_id"] = taskId,
            ["captured_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["review"] = new JsonObject
            {
                ["verdict"] = "pause_for_review",
                ["reason"] = $"Synthetic review fixture awaits human review for {taskId}.",
                ["acceptance_met"] = true,
                ["boundary_preserved"] = true,
                ["scope_drift_detected"] = false,
                ["follow_up_suggestions"] = new JsonArray(),
            },
            ["resulting_status"] = "review",
            ["transition_reason"] = "Synthetic integration fixture stops at the review boundary.",
            ["planner_comment"] = $"Synthetic review fixture awaits human review for {taskId}.",
            ["patch_summary"] = resolvedScope.Length == 0
                ? "files=0; lines=0; paths=(none)"
                : $"files={resolvedScope.Length}; added=0; removed=0; estimated=True; paths={string.Join(",", resolvedScope)}",
            ["result_commit"] = null,
            ["validation_passed"] = true,
            ["validation_evidence"] = new JsonArray("synthetic review fixture", "worker artifact exists"),
            ["safety_outcome"] = "allow",
            ["safety_issues"] = new JsonArray(),
            ["decision_status"] = "pending_review",
            ["decision_reason"] = null,
            ["decision_at"] = null,
        };
        File.WriteAllText(reviewArtifactPath, reviewArtifact.ToJsonString(new() { WriteIndented = true }));

        new JsonRuntimeArtifactRepository(ControlPlanePaths.FromRepoRoot(RootPath)).SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
                Summary = $"Synthetic review fixture persisted bounded worker execution evidence for {taskId}.",
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = $"RUN-{taskId}-001",
                WorkerId = "CodexCliWorkerAdapter",
                EvidenceSource = ExecutionEvidenceSource.Host,
                CommandsExecuted = ["synthetic review fixture"],
                CommandLogRef = $".ai/artifacts/worker-executions/{taskId}.log",
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Partial,
                EvidenceStrength = ExecutionEvidenceStrength.Observed,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedAt = DateTimeOffset.UtcNow,
                ExitStatus = 0,
            },
        });

        WriteReviewPendingSession(taskId);
    }

    public void AddSyntheticHistoricalExecutionRunExceptionTask(
        string taskId,
        string taskStatus = "completed",
        bool validationPassed = false,
        string safetyOutcome = "blocked",
        string decisionStatus = "approved",
        string reviewResultingStatus = "pending",
        bool substrateFailure = false,
        string? substrateCategory = null,
        string workerSummary = "Synthetic historical execution-run exception.")
    {
        AddSyntheticTask(taskId, taskStatus, "Synthetic historical execution-run exception");

        var nodePath = Path.Combine(RootPath, ".ai", "tasks", "nodes", $"{taskId}.json");
        var taskNode = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        taskNode["status"] = taskStatus;
        taskNode["last_worker_summary"] = workerSummary;
        taskNode["last_worker_failure_kind"] = substrateFailure ? "transient_infra" : "task_logic_failed";
        taskNode["planner_review"] = new JsonObject
        {
            ["verdict"] = "complete",
            ["reason"] = $"Synthetic historical override retained task truth for {taskId}.",
            ["acceptance_met"] = true,
            ["boundary_preserved"] = true,
            ["scope_drift_detected"] = false,
            ["follow_up_suggestions"] = new JsonArray(),
        };

        var metadata = taskNode["metadata"]?.AsObject() ?? new JsonObject();
        var runId = $"RUN-{taskId}-001";
        metadata["execution_run_latest_id"] = runId;
        metadata["execution_run_latest_status"] = "Failed";
        if (substrateFailure)
        {
            metadata["execution_substrate_failure"] = "true";
            metadata["execution_failure_lane"] = "substrate";
            metadata["execution_substrate_category"] = substrateCategory ?? "delegated_worker_launch_failed";
        }

        taskNode["metadata"] = metadata;
        File.WriteAllText(nodePath, taskNode.ToJsonString(new() { WriteIndented = true }));

        var runRoot = Path.Combine(RootPath, ".ai", "runtime", "runs", taskId);
        Directory.CreateDirectory(runRoot);
        var run = new ExecutionRun
        {
            RunId = runId,
            TaskId = taskId,
            Status = ExecutionRunStatus.Failed,
            CurrentStepIndex = 4,
            Steps =
            [
                new ExecutionStep { StepId = $"{runId}-STEP-001", Title = "Inspect", Kind = ExecutionStepKind.Inspect, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-STEP-002", Title = "Implement", Kind = ExecutionStepKind.Implement, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-STEP-003", Title = "Verify", Kind = ExecutionStepKind.Verify, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-STEP-004", Title = "Writeback", Kind = ExecutionStepKind.Writeback, Status = ExecutionStepStatus.Completed },
                new ExecutionStep { StepId = $"{runId}-STEP-005", Title = "Cleanup", Kind = ExecutionStepKind.Cleanup, Status = ExecutionStepStatus.Completed },
            ],
        };
        File.WriteAllText(
            Path.Combine(runRoot, $"{runId}.json"),
            JsonSerializer.Serialize(
                run,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }));

        var reviewArtifactPath = Path.Combine(RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reviewArtifactPath)!);
        var reviewArtifact = new JsonObject
        {
            ["schema_version"] = 1,
            ["task_id"] = taskId,
            ["captured_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["review"] = new JsonObject
            {
                ["verdict"] = "complete",
                ["reason"] = $"Synthetic historical override retained task truth for {taskId}.",
                ["acceptance_met"] = true,
                ["boundary_preserved"] = true,
                ["scope_drift_detected"] = false,
                ["follow_up_suggestions"] = new JsonArray(),
            },
            ["resulting_status"] = reviewResultingStatus,
            ["transition_reason"] = "Synthetic historical exception fixture preserves a failed execution run.",
            ["planner_comment"] = $"Synthetic historical override retained task truth for {taskId}.",
            ["patch_summary"] = "files=0; lines=0; paths=(none)",
            ["result_commit"] = null,
            ["validation_passed"] = validationPassed,
            ["validation_evidence"] = validationPassed ? new JsonArray("synthetic valid review") : new JsonArray("synthetic invalid review"),
            ["safety_outcome"] = safetyOutcome,
            ["safety_issues"] = safetyOutcome == "allow" ? new JsonArray() : new JsonArray("SYNTHETIC_BLOCKED_REVIEW_OVERRIDE"),
            ["decision_status"] = decisionStatus,
            ["decision_reason"] = "Synthetic historical exception fixture",
            ["decision_at"] = DateTimeOffset.UtcNow.ToString("O"),
        };
        File.WriteAllText(reviewArtifactPath, reviewArtifact.ToJsonString(new() { WriteIndented = true }));
    }

    public void AddSyntheticRoutingPilotTask(
        string taskId,
        string pilotTaskFamily,
        string routingIntent,
        string moduleId,
        IReadOnlyList<string>? scope = null,
        IReadOnlyList<IReadOnlyList<string>>? validationCommands = null,
        IReadOnlyList<string>? dependencies = null)
    {
        AddSyntheticPendingTask(taskId, scope, validationCommands, dependencies);
        SetTaskMetadata(taskId, "pilot_task_family", pilotTaskFamily);
        SetTaskMetadata(taskId, "routing_intent", routingIntent);
        SetTaskMetadata(taskId, "module_id", moduleId);
    }

    public void WriteActiveRoutingProfile(
        string profileId,
        string ruleId,
        string routingIntent,
        string? moduleId,
        string providerId,
        string backendId,
        string routingProfileId,
        string model,
        IReadOnlyList<(string providerId, string backendId, string routingProfileId, string model)>? fallbackRoutes = null)
    {
        var runtimeStateRoot = Path.Combine(RootPath, ".carves-platform", "runtime-state");
        Directory.CreateDirectory(runtimeStateRoot);

        var profile = new JsonObject
        {
            ["schema_version"] = "runtime-routing-profile.v1",
            ["profile_id"] = profileId,
            ["version"] = "test-1",
            ["source_qualification_id"] = "integration-routing-profile",
            ["summary"] = "Integration test active routing profile.",
            ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["activated_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["rules"] = new JsonArray(
                new JsonObject
                {
                    ["rule_id"] = ruleId,
                    ["routing_intent"] = routingIntent,
                    ["module_id"] = moduleId,
                    ["summary"] = "Integration test route.",
                    ["preferred_route"] = new JsonObject
                    {
                        ["provider_id"] = providerId,
                        ["backend_id"] = backendId,
                        ["routing_profile_id"] = routingProfileId,
                        ["model"] = model,
                    },
                    ["fallback_routes"] = new JsonArray((fallbackRoutes ?? [])
                        .Select(route => (JsonNode)new JsonObject
                        {
                            ["provider_id"] = route.providerId,
                            ["backend_id"] = route.backendId,
                            ["routing_profile_id"] = route.routingProfileId,
                            ["model"] = route.model,
                        })
                        .ToArray()),
                }),
        };

        var profilePath = Path.Combine(runtimeStateRoot, "active_routing_profile.json");
        File.WriteAllText(profilePath, profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public void AddSyntheticRefactoringSmell()
    {
        var filePath = Path.Combine(RootPath, "src", "Synthetic.Refactoring", "LargeFile.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var lines = new List<string>
        {
            "namespace Synthetic.Refactoring;",
            string.Empty,
            "public sealed class LargeFile",
            "{",
        };

        for (var index = 0; index < 220; index++)
        {
            lines.Add($"    public int Value{index} {{ get; }} = {index};");
        }

        lines.Add("}");
        File.WriteAllText(filePath, string.Join(Environment.NewLine, lines));
    }

    public void ClearRefactoringSuggestions()
    {
        var graphPath = Path.Combine(RootPath, ".ai", "tasks", "graph.json");
        var graphNode = JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        var tasks = graphNode["tasks"]!.AsArray();
        for (var index = tasks.Count - 1; index >= 0; index--)
        {
            var taskObject = tasks[index]!.AsObject();
            var taskId = taskObject["task_id"]?.GetValue<string>() ?? string.Empty;
            if (!taskId.StartsWith("T-REF-", StringComparison.Ordinal)
                && !taskId.StartsWith("T-REFQ-", StringComparison.Ordinal))
            {
                continue;
            }

            var nodeFile = taskObject["node_file"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(nodeFile))
            {
                var nodePath = Path.Combine(RootPath, ".ai", "tasks", nodeFile.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(nodePath))
                {
                    File.Delete(nodePath);
                }
            }

            tasks.RemoveAt(index);
        }

        File.WriteAllText(graphPath, graphNode.ToJsonString(new() { WriteIndented = true }));

        var queueRoot = Path.Combine(RootPath, ".ai", "refactoring", "queues");
        if (Directory.Exists(queueRoot))
        {
            Directory.Delete(queueRoot, recursive: true);
        }

        var backlogPath = Path.Combine(RootPath, ".ai", "refactoring", "backlog.json");
        if (!File.Exists(backlogPath))
        {
            return;
        }

        var backlogNode = JsonNode.Parse(File.ReadAllText(backlogPath))!.AsObject();
        var items = backlogNode["items"]?.AsArray();
        if (items is null)
        {
            return;
        }

        foreach (var itemNode in items)
        {
            var item = itemNode?.AsObject();
            if (item is null)
            {
                continue;
            }

            var suggestedTaskId = item["suggested_task_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(suggestedTaskId)
                || (!suggestedTaskId.StartsWith("T-REF-", StringComparison.Ordinal)
                    && !suggestedTaskId.StartsWith("T-REFQ-", StringComparison.Ordinal)))
            {
                continue;
            }

            item["suggested_task_id"] = null;
            if (string.Equals(item["status"]?.GetValue<string>(), "suggested", StringComparison.OrdinalIgnoreCase))
            {
                item["status"] = "open";
            }
        }

        File.WriteAllText(backlogPath, backlogNode.ToJsonString(new() { WriteIndented = true }));
    }

    public void ClearOpportunities()
    {
        var opportunitiesPath = Path.Combine(RootPath, ".ai", "opportunities", "index.json");
        if (File.Exists(opportunitiesPath))
        {
            File.Delete(opportunitiesPath);
        }
    }

    public void ClearPlannerGeneratedTasks()
    {
        var graphPath = Path.Combine(RootPath, ".ai", "tasks", "graph.json");
        var graphNode = JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        var tasks = graphNode["tasks"]!.AsArray();
        for (var index = tasks.Count - 1; index >= 0; index--)
        {
            var taskObject = tasks[index]!.AsObject();
            var taskId = taskObject["task_id"]?.GetValue<string>() ?? string.Empty;
            if (!taskId.StartsWith("T-PLAN-", StringComparison.Ordinal))
            {
                continue;
            }

            var nodeFile = taskObject["node_file"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(nodeFile))
            {
                var nodePath = Path.Combine(RootPath, ".ai", "tasks", nodeFile.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(nodePath))
                {
                    File.Delete(nodePath);
                }
            }

            tasks.RemoveAt(index);
        }

        File.WriteAllText(graphPath, graphNode.ToJsonString(new() { WriteIndented = true }));
    }

    public void WriteAiProviderConfig(string json)
    {
        var configPath = Path.Combine(RootPath, ".ai", "config", "ai_provider.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, json);
    }

    public void WriteSystemConfig(string json)
    {
        var configPath = Path.Combine(RootPath, ".ai", "config", "system.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, json);
    }

    public void WriteDelegatedExecutionHostInvokePolicy(int requestTimeoutSeconds)
    {
        if (requestTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeoutSeconds));
        }

        var policyPath = Path.Combine(RootPath, ".carves-platform", "policies", "host-invoke.policy.json");
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllText(
            policyPath,
            $$"""
{
  "version": "1.0",
  "default_read": {
    "request_timeout_seconds": 5,
    "use_accepted_operation_polling": false,
    "poll_interval_ms": 250,
    "base_wait_seconds": 0,
    "stall_timeout_seconds": 0,
    "max_wait_seconds": 0
  },
  "control_plane_mutation": {
    "request_timeout_seconds": 5,
    "use_accepted_operation_polling": true,
    "poll_interval_ms": 250,
    "base_wait_seconds": 15,
    "stall_timeout_seconds": 10,
    "max_wait_seconds": 45
  },
  "attach_flow": {
    "request_timeout_seconds": 30,
    "use_accepted_operation_polling": false,
    "poll_interval_ms": 250,
    "base_wait_seconds": 0,
    "stall_timeout_seconds": 0,
    "max_wait_seconds": 0
  },
  "delegated_execution": {
    "request_timeout_seconds": {{requestTimeoutSeconds}},
    "use_accepted_operation_polling": false,
    "poll_interval_ms": 250,
    "base_wait_seconds": 0,
    "stall_timeout_seconds": 0,
    "max_wait_seconds": 0
  }
}
""");
    }

    public void WriteWorkerOperationalPolicy(string json)
    {
        var configPath = Path.Combine(RootPath, ".ai", "config", "worker_operational_policy.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, json);
    }

    public void ResetHistoricalDelegatedExecutionTruth()
    {
        var taskNodeRoot = Path.Combine(RootPath, ".ai", "tasks", "nodes");
        if (Directory.Exists(taskNodeRoot))
        {
            foreach (var nodePath in Directory.EnumerateFiles(taskNodeRoot, "*.json", SearchOption.TopDirectoryOnly))
            {
                var taskNode = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
                taskNode["last_worker_run_id"] = null;
                taskNode["last_worker_backend"] = null;
                taskNode["last_worker_failure_kind"] = null;
                taskNode["last_worker_retryable"] = null;
                taskNode["last_worker_summary"] = null;
                taskNode["last_worker_detail_ref"] = null;
                taskNode["last_provider_detail_ref"] = null;
                taskNode["last_recovery_action"] = null;
                taskNode["last_recovery_reason"] = null;
                taskNode["retry_not_before"] = null;

                if (taskNode["metadata"] is JsonObject metadata)
                {
                    foreach (var key in metadata
                                 .Select(item => item.Key)
                                 .Where(key =>
                                     key.StartsWith("execution_run_", StringComparison.Ordinal)
                                     || key.StartsWith("execution_pattern_", StringComparison.Ordinal)
                                     || key.StartsWith("execution_failure_", StringComparison.Ordinal)
                                     || key.StartsWith("execution_substrate_", StringComparison.Ordinal))
                                 .ToArray())
                    {
                        metadata.Remove(key);
                    }
                }

                File.WriteAllText(nodePath, taskNode.ToJsonString(new() { WriteIndented = true }));
            }
        }

        DeleteDirectoryIfExists(Path.Combine(RootPath, ".ai", "artifacts", "worker-executions"));
        DeleteDirectoryIfExists(Path.Combine(RootPath, ".ai", "artifacts", "worker"));
        DeleteDirectoryIfExists(Path.Combine(RootPath, ".ai", "artifacts", "provider"));
        DeleteDirectoryIfExists(Path.Combine(RootPath, ".ai", "artifacts", "runtime-failures"));
        DeleteDirectoryIfExists(Path.Combine(RootPath, ".ai", "failures"));
        DeleteDirectoryIfExists(Path.Combine(RootPath, ".ai", "runtime", "runs"));
        DeleteDirectoryIfExists(Path.Combine(RootPath, ".ai", "runtime", "run-reports"));
        DeleteDirectoryIfExists(Path.Combine(RootPath, ".carves-worktrees"));
        DeleteIfExists(Path.Combine(RootPath, ".ai", "runtime", "live-state", "worktrees.json"));
        DeleteIfExists(Path.Combine(RootPath, ".ai", "runtime", "live-state", "last_failure.json"));
    }

    public void SetTaskMetadata(string taskId, string key, string value)
    {
        var nodePath = Path.Combine(RootPath, ".ai", "tasks", "nodes", $"{taskId}.json");
        var taskNode = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        var metadata = taskNode["metadata"]?.AsObject() ?? new JsonObject();
        metadata[key] = value;
        taskNode["metadata"] = metadata;
        File.WriteAllText(nodePath, taskNode.ToJsonString(new() { WriteIndented = true }));
    }

    private void WriteReviewPendingSession(string taskId)
    {
        var sessionPath = Path.Combine(RootPath, ".ai", "runtime", "live-state", "session.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
        var session = new JsonObject
        {
            ["schema_version"] = 1,
            ["session_id"] = "default",
            ["attached_repo_root"] = RootPath,
            ["status"] = "review_wait",
            ["loop_mode"] = "manual_tick",
            ["dry_run"] = false,
            ["active_worker_count"] = 0,
            ["tick_count"] = 1,
            ["active_task_ids"] = new JsonArray(),
            ["review_pending_task_ids"] = new JsonArray(taskId),
            ["pending_permission_request_ids"] = new JsonArray(),
            ["current_task_id"] = null,
            ["last_task_id"] = taskId,
            ["last_review_task_id"] = taskId,
            ["last_reason"] = $"Synthetic review fixture awaiting human review for {taskId}.",
            ["loop_reason"] = $"Synthetic review fixture awaiting human review for {taskId}.",
            ["loop_actionability"] = "human_actionable",
            ["waiting_reason"] = $"Synthetic review fixture awaiting human review for {taskId}.",
            ["waiting_actionability"] = "human_actionable",
            ["stop_reason"] = null,
            ["stop_actionability"] = "none",
            ["started_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["updated_at"] = DateTimeOffset.UtcNow.ToString("O"),
        };
        File.WriteAllText(sessionPath, session.ToJsonString(new() { WriteIndented = true }));
    }

    public void Dispose()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CARVES_TEST_KEEP_SANDBOXES"), "1", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Preserved integration sandbox: {RootPath}");
            return;
        }

        if (Directory.Exists(RootPath))
        {
            TryKillLeakedSandboxHost(RootPath);

            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    PrepareDirectoryForDeletion(RootPath);
                    Directory.Delete(RootPath, recursive: true);
                    return;
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    IntegrationTestWait.Delay(TimeSpan.FromMilliseconds(100));
                }
                catch (IOException) when (attempt < 4)
                {
                    IntegrationTestWait.Delay(TimeSpan.FromMilliseconds(100));
                }
                catch
                {
                    return;
                }
            }

            try
            {
                PrepareDirectoryForDeletion(RootPath);
                Directory.Delete(RootPath, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void CopyDirectory(
        string source,
        string destination,
        Func<string, bool>? shouldSkipRelativeDirectory = null,
        string? relativePath = null)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (name is "bin" or "obj" or "TestResults" or ".git")
            {
                continue;
            }

            var childRelativePath = string.IsNullOrWhiteSpace(relativePath)
                ? name
                : Path.Combine(relativePath, name);
            if (shouldSkipRelativeDirectory?.Invoke(childRelativePath) == true)
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(destination, name), shouldSkipRelativeDirectory, childRelativePath);
        }

        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }
    }

    private static void EnsureTaskGraphNodeFiles(string sourceRoot, string sandboxRoot)
    {
        var sandboxGraphPath = Path.Combine(sandboxRoot, ".ai", "tasks", "graph.json");
        if (!File.Exists(sandboxGraphPath))
        {
            var sourceGraphPath = Path.Combine(sourceRoot, ".ai", "tasks", "graph.json");
            if (!File.Exists(sourceGraphPath))
            {
                throw new FileNotFoundException("Sandbox copy could not locate source task graph.", sourceGraphPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(sandboxGraphPath)!);
            File.Copy(sourceGraphPath, sandboxGraphPath, overwrite: true);
        }

        var graphNode = JsonNode.Parse(File.ReadAllText(sandboxGraphPath))?.AsObject();
        var tasks = graphNode?["tasks"]?.AsArray();
        if (tasks is null)
        {
            return;
        }

        foreach (var task in tasks)
        {
            var taskObject = task?.AsObject();
            var nodeFile = taskObject?["node_file"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(nodeFile))
            {
                continue;
            }

            var sandboxNodePath = Path.Combine(sandboxRoot, ".ai", "tasks", nodeFile.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(sandboxNodePath))
            {
                continue;
            }

            var sourceNodePath = Path.Combine(sourceRoot, ".ai", "tasks", nodeFile.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourceNodePath))
            {
                throw new FileNotFoundException($"Sandbox graph referenced missing source task node '{nodeFile}'.", sourceNodePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(sandboxNodePath)!);
            File.Copy(sourceNodePath, sandboxNodePath, overwrite: true);
        }
    }

    private static void ResetRuntimeSession(string runtimeRoot)
    {
        if (!Directory.Exists(runtimeRoot))
        {
            return;
        }

        DeleteIfExists(Path.Combine(runtimeRoot, "session.json"));
        DeleteIfExists(Path.Combine(runtimeRoot, "last_failure.json"));
        DeleteIfExists(Path.Combine(runtimeRoot, "worktrees.json"));

        var liveStateRoot = Path.Combine(runtimeRoot, "live-state");
        DeleteIfExists(Path.Combine(liveStateRoot, "session.json"));
        DeleteIfExists(Path.Combine(liveStateRoot, "last_failure.json"));
        DeleteIfExists(Path.Combine(liveStateRoot, "worktrees.json"));
    }

    private static void ResetValidationState(string validationRoot)
    {
        if (!Directory.Exists(validationRoot))
        {
            return;
        }

        Directory.Delete(validationRoot, recursive: true);
    }

    private static void ResetPlatformState(string platformRoot)
    {
        if (!Directory.Exists(platformRoot))
        {
            return;
        }

        DeleteDirectoryIfExists(Path.Combine(platformRoot, "repos"));
        DeleteDirectoryIfExists(Path.Combine(platformRoot, "events"));
        DeleteDirectoryIfExists(Path.Combine(platformRoot, "fleet"));
        DeleteDirectoryIfExists(Path.Combine(platformRoot, "runtime-state"));
        DeleteDirectoryIfExists(Path.Combine(platformRoot, "sessions"));
        DeleteDirectoryIfExists(Path.Combine(platformRoot, "workers"));
    }

    private static void SeedIsolatedHostDescriptor(string repoRoot)
    {
        var descriptorDirectory = Path.Combine(repoRoot, ".carves-platform", "host");
        Directory.CreateDirectory(descriptorDirectory);

        var descriptor = new JsonObject
        {
            ["host_id"] = $"integration-sandbox-{Guid.NewGuid():N}",
            ["machine_id"] = Environment.MachineName,
            ["repo_root"] = repoRoot,
            ["base_url"] = "http://127.0.0.1:1",
            ["port"] = 1,
            // Seed a clearly stale descriptor without pretending the current test runner is the host.
            ["process_id"] = 0,
            ["runtime_directory"] = Path.Combine(Path.GetTempPath(), "carves-runtime-host", "integration-sandbox"),
            ["deployment_directory"] = Path.Combine(Path.GetTempPath(), "carves-runtime-host", "integration-sandbox", "deployment"),
            ["executable_path"] = Path.Combine(Path.GetTempPath(), "carves-runtime-host", "integration-sandbox", "deployment", "Carves.Runtime.Host.dll"),
            ["started_at"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
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

        var platformRuntimeStateHostRoot = Path.Combine(repoRoot, ".carves-platform", "runtime-state", "host");
        if (Directory.Exists(platformRuntimeStateHostRoot))
        {
            Directory.Delete(platformRuntimeStateHostRoot, recursive: true);
        }
    }

    private static string ResolveSourceRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static void PruneLeakedIntegrationHost()
    {
        var integrationRoot = Path.Combine(Path.GetTempPath(), "carves-runtime-integration");
        var descriptorPath = Path.Combine(Path.GetTempPath(), "carves-runtime-host", "active-host.json");
        if (File.Exists(descriptorPath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(descriptorPath));
                var root = document.RootElement;
                var repoRoot = root.TryGetProperty("repo_root", out var repoRootProperty)
                    ? repoRootProperty.GetString()
                    : null;
                var runtimeDirectory = root.TryGetProperty("runtime_directory", out var runtimeDirectoryProperty)
                    ? runtimeDirectoryProperty.GetString()
                    : null;
                var leakedIntegrationHost =
                    IsUnderPath(repoRoot, integrationRoot)
                    || IsUnderPath(runtimeDirectory, integrationRoot);

                if (leakedIntegrationHost
                    && root.TryGetProperty("process_id", out var processIdProperty)
                    && processIdProperty.TryGetInt32(out var processId)
                    && processId > 0)
                {
                    TryKillProcess(processId);
                }
            }
            catch
            {
            }

            TryDeleteIfExists(descriptorPath);
        }

        if (!Directory.Exists(integrationRoot))
        {
            return;
        }

        foreach (var sandboxRoot in Directory.EnumerateDirectories(integrationRoot))
        {
            TryKillLeakedSandboxHost(sandboxRoot);
            TryDeleteDirectoryIfExists(sandboxRoot);
        }
    }

    private static bool IsUnderPath(string? candidate, string root)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            var fullCandidate = Path.GetFullPath(candidate);
            var fullRoot = Path.GetFullPath(root);
            return fullCandidate.StartsWith(
                fullRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static void TryKillProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
        }
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonArray ToCommandJsonArray(IEnumerable<IReadOnlyList<string>> commands)
    {
        var array = new JsonArray();
        foreach (var command in commands)
        {
            array.Add(ToJsonArray(command));
        }

        return array;
    }

    private static JsonObject CreateSyntheticAcceptanceContract(string taskId)
    {
        return new JsonObject
        {
            ["contract_id"] = $"AC-{taskId}",
            ["title"] = $"Acceptance contract for {taskId}",
            ["status"] = "compiled",
            ["owner"] = "planner",
            ["created_at_utc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["intent"] = new JsonObject
            {
                ["goal"] = "Keep integration-task execution governed by explicit acceptance truth.",
                ["business_value"] = "Synthetic integration tasks should exercise the same acceptance contract gate as real task truth.",
            },
            ["acceptance_examples"] = new JsonArray(),
            ["checks"] = new JsonObject
            {
                ["unit_tests"] = new JsonArray(),
                ["integration_tests"] = new JsonArray(),
                ["regression_tests"] = new JsonArray(),
                ["policy_checks"] = new JsonArray(),
                ["additional_checks"] = new JsonArray(),
            },
            ["constraints"] = new JsonObject
            {
                ["must_not"] = new JsonArray("keep integration fixture deterministic"),
                ["architecture"] = new JsonArray(),
                ["scope_limit"] = null,
            },
            ["non_goals"] = new JsonArray("Do not bypass acceptance contract gating in synthetic integration fixtures."),
            ["evidence_required"] = new JsonArray(),
            ["human_review"] = new JsonObject
            {
                ["required"] = true,
                ["provisional_allowed"] = false,
                ["decisions"] = new JsonArray("accept", "reject", "reopen"),
            },
            ["traceability"] = new JsonObject
            {
                ["source_card_id"] = "CARD-INTEGRATION",
                ["source_task_id"] = taskId,
                ["derived_task_ids"] = new JsonArray(),
                ["related_artifacts"] = new JsonArray(),
            },
        };
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void TryDeleteIfExists(string path)
    {
        try
        {
            DeleteIfExists(path);
        }
        catch
        {
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void TryDeleteDirectoryIfExists(string path)
    {
        try
        {
            DeleteDirectoryIfExists(path);
        }
        catch
        {
        }
    }

    private static void PrepareDirectoryForDeletion(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            TrySetNormalAttributes(file);
        }

        foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
        {
            TrySetNormalAttributes(directory);
        }

        TrySetNormalAttributes(path);
    }

    private static void TrySetNormalAttributes(string path)
    {
        try
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
        catch
        {
        }
    }

    private static void TryKillLeakedSandboxHost(string sandboxRoot)
    {
        var descriptorPath = Path.Combine(sandboxRoot, ".carves-platform", "host", "descriptor.json");
        if (!File.Exists(descriptorPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            var root = document.RootElement;
            if (root.TryGetProperty("process_id", out var processIdProperty)
                && processIdProperty.TryGetInt32(out var processId)
                && processId > 0)
            {
                TryKillProcess(processId);
            }
        }
        catch
        {
        }
    }
}
