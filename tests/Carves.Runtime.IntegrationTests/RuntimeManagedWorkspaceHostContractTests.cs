using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeManagedWorkspaceHostContractTests
{
    [Fact]
    public void PlanIssueWorkspace_ProjectsManagedWorkspaceLeaseThroughPlanAndRuntimeSurfaces()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var externalLeaseRoot = Path.Combine(Path.GetDirectoryName(sandbox.RootPath)!, $"phase4-leases-{Guid.NewGuid():N}");
        var worktreeRootRelative = $"../{Path.GetFileName(externalLeaseRoot)}";
        sandbox.WriteSystemConfig(
            $$"""
            {
              "repo_name": "Phase4LeaseRepo",
              "worktree_root": "{{worktreeRootRelative}}",
              "max_parallel_tasks": 1,
              "default_test_command": ["dotnet", "test", "CARVES.Runtime.sln"],
              "code_directories": ["src", "tests"],
              "excluded_directories": [".git", ".nuget", "bin", "obj", "TestResults", "coverage"],
              "sync_markdown_views": true,
              "remove_worktree_on_success": true
            }
            """);

        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        try
        {
            RunProgram("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
            RunProgram("--repo-root", sandbox.RootPath, "--cold", "intent", "focus", "candidate-first-slice");
            RunProgram("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_validation_artifact", "resolved");
            RunProgram("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_slice_boundary", "resolved");
            RunProgram("--repo-root", sandbox.RootPath, "--cold", "intent", "candidate", "candidate-first-slice", "ready_to_plan");

            var init = RunProgram("--repo-root", sandbox.RootPath, "--cold", "plan", "init");
            var exportCardPath = Path.Combine(sandbox.RootPath, "drafts", "plan-card.json");
            var exportCard = RunProgram("--repo-root", sandbox.RootPath, "--cold", "plan", "export-card", exportCardPath);
            var createCard = RunProgram("--repo-root", sandbox.RootPath, "--cold", "create-card-draft", exportCardPath);
            var cardId = ExtractFirstMatch(createCard.StandardOutput, @"Created card draft (?<card_id>CARD-[A-Za-z0-9-]+)\.", "card_id");
            var approveCard = RunProgram("--repo-root", sandbox.RootPath, "--cold", "approve-card", cardId, "approved for phase-4 workspace lease");

            var taskId = $"T-{cardId}-001";
            var taskGraphPayloadPath = Path.Combine(sandbox.RootPath, "drafts", "taskgraph-draft.json");
            File.WriteAllText(
                taskGraphPayloadPath,
                JsonSerializer.Serialize(
                    new
                    {
                        card_id = cardId,
                        tasks = new object[]
                        {
                            new
                            {
                                task_id = taskId,
                                title = "Issue managed workspace lease",
                                description = "Bind a managed workspace lease to task truth.",
                                scope = new[] { "src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs" },
                                acceptance = new[] { "managed workspace lease is issued" },
                                proof_target = new
                                {
                                    kind = "focused_behavior",
                                    description = "Prove the runtime issues a task-bound managed workspace lease from plan-bound task truth.",
                                },
                            },
                        },
                    },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        WriteIndented = true,
                    }));

            var createTaskGraph = RunProgram("--repo-root", sandbox.RootPath, "--cold", "create-taskgraph-draft", taskGraphPayloadPath);
            var taskGraphDraftId = ExtractFirstMatch(createTaskGraph.StandardOutput, @"Created taskgraph draft (?<draft_id>TG-[A-Za-z0-9-]+) for", "draft_id");
            var approveTaskGraph = RunProgram("--repo-root", sandbox.RootPath, "--cold", "approve-taskgraph-draft", taskGraphDraftId, "approved for phase-4 workspace lease");
            var issueWorkspace = RunProgram("--repo-root", sandbox.RootPath, "--cold", "plan", "issue-workspace", taskId);
            var planStatus = RunProgram("--repo-root", sandbox.RootPath, "--cold", "plan", "status");
            var inspectWorkingModes = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-working-modes");
            var apiWorkingModes = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-working-modes");
            var inspectPlanningPosture = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-formal-planning-posture");
            var apiPlanningPosture = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-formal-planning-posture");
            var inspectVendorAcceleration = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-vendor-native-acceleration");
            var apiVendorAcceleration = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-vendor-native-acceleration");
            var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-managed-workspace");
            var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-managed-workspace");

            Assert.Equal(0, init.ExitCode);
            Assert.Equal(0, exportCard.ExitCode);
            Assert.Equal(0, createCard.ExitCode);
            Assert.Equal(0, approveCard.ExitCode);
            Assert.Equal(0, createTaskGraph.ExitCode);
            Assert.Equal(0, approveTaskGraph.ExitCode);
            Assert.Equal(0, issueWorkspace.ExitCode);
            Assert.Equal(0, planStatus.ExitCode);
            Assert.Equal(0, inspectWorkingModes.ExitCode);
            Assert.Equal(0, apiWorkingModes.ExitCode);
            Assert.Equal(0, inspectPlanningPosture.ExitCode);
            Assert.Equal(0, apiPlanningPosture.ExitCode);
            Assert.Equal(0, inspectVendorAcceleration.ExitCode);
            Assert.Equal(0, apiVendorAcceleration.ExitCode);
            Assert.Equal(0, inspect.ExitCode);
            Assert.Equal(0, api.ExitCode);

            using var issueDocument = JsonDocument.Parse(issueWorkspace.StandardOutput);
            var issueRoot = issueDocument.RootElement;
            Assert.Equal("managed_workspace_issue_result", issueRoot.GetProperty("kind").GetString());
            var leaseNode = issueRoot.GetProperty("lease");
            var workspacePath = leaseNode.GetProperty("workspace_path").GetString();
            Assert.False(string.IsNullOrWhiteSpace(workspacePath));
            Assert.True(Directory.Exists(workspacePath));
            Assert.Contains(leaseNode.GetProperty("allowed_writable_paths").EnumerateArray(), item =>
                item.GetString() == "src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs");

            using var statusDocument = JsonDocument.Parse(planStatus.StandardOutput);
            var statusRoot = statusDocument.RootElement;
            var planHandle = statusRoot.GetProperty("plan_handle").GetString();
            Assert.Equal("task_bound_workspace_active", statusRoot.GetProperty("managed_workspace_posture").GetString());
            Assert.Equal("formal_planning_packet_available", statusRoot.GetProperty("formal_planning_entry_trigger_state").GetString());
            Assert.Equal("plan status", statusRoot.GetProperty("formal_planning_entry_command").GetString());
            Assert.Contains("plan status", statusRoot.GetProperty("formal_planning_entry_recommended_next_action").GetString(), StringComparison.Ordinal);
            Assert.Equal("occupied_by_packet", statusRoot.GetProperty("active_planning_slot_state").GetString());
            Assert.False(statusRoot.GetProperty("active_planning_slot_can_initialize").GetBoolean());
            Assert.Contains("plan status", statusRoot.GetProperty("active_planning_slot_remediation_action").GetString(), StringComparison.Ordinal);
            Assert.Contains(statusRoot.GetProperty("workspace_leases").EnumerateArray(), item =>
                item.GetProperty("task_id").GetString() == taskId);

            Assert.Contains("Runtime agent working modes", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Current mode: mode_d_scoped_task_workspace", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent recommended mode: mode_e_brokered_execution", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent recommendation posture: mode_e_recommended_for_packet_bound_handoff", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent constraint tier: hard_runtime_gate", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent stronger-mode blockers: 0", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent first stronger-mode blocker: (none)", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("- constraint-class: soft_advisory | enforcement=read_side_guidance_only | modes=mode_a_open_repo_advisory", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("- constraint-class: hard_runtime_gate | enforcement=portable_runtime_enforced | modes=mode_c_task_bound_workspace, mode_d_scoped_task_workspace, mode_e_brokered_execution", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Planning coupling posture: p2_task_bound_guidance", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Plan handle: {planHandle}", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E operational activation: task_scoped_activation_available", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation task: {taskId}", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E result return channel: .ai/execution/{taskId}/result.json", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation next action: Run `inspect runtime-brokered-execution {taskId}`", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation blocking checks: 0", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation first blocker: (none)", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation playbook steps: 4", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation first playbook command: inspect runtime-brokered-execution {taskId}", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"- mode-e command: inspect runtime-brokered-execution {taskId}", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"- mode-e playbook step 3: inspect_packet_enforcement | command=inspect packet-enforcement {taskId} | applies=task_scoped_activation_available", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("- mode-e check: bound_task_truth_available | state=satisfied", inspectWorkingModes.StandardOutput, StringComparison.Ordinal);

            using var apiWorkingModesDocument = JsonDocument.Parse(apiWorkingModes.StandardOutput);
            var apiWorkingModesRoot = apiWorkingModesDocument.RootElement;
            Assert.Equal("runtime-agent-working-modes", apiWorkingModesRoot.GetProperty("surface_id").GetString());
            Assert.Equal("mode_d_scoped_task_workspace", apiWorkingModesRoot.GetProperty("current_mode").GetString());
            Assert.Equal("mode_e_brokered_execution", apiWorkingModesRoot.GetProperty("external_agent_recommended_mode").GetString());
            Assert.Equal("mode_e_recommended_for_packet_bound_handoff", apiWorkingModesRoot.GetProperty("external_agent_recommendation_posture").GetString());
            Assert.Equal("hard_runtime_gate", apiWorkingModesRoot.GetProperty("external_agent_constraint_tier").GetString());
            Assert.Equal(0, apiWorkingModesRoot.GetProperty("external_agent_stronger_mode_blocker_count").GetInt32());
            Assert.Null(apiWorkingModesRoot.GetProperty("external_agent_first_stronger_mode_blocker_id").GetString());
            Assert.Empty(apiWorkingModesRoot.GetProperty("external_agent_stronger_mode_blockers").EnumerateArray());
            Assert.Contains(apiWorkingModesRoot.GetProperty("constraint_classes").EnumerateArray(), item =>
                item.GetProperty("class_id").GetString() == "soft_advisory"
                && item.GetProperty("enforcement_level").GetString() == "read_side_guidance_only");
            Assert.Contains(apiWorkingModesRoot.GetProperty("constraint_classes").EnumerateArray(), item =>
                item.GetProperty("class_id").GetString() == "hard_runtime_gate"
                && item.GetProperty("applies_to_modes").EnumerateArray().Any(mode => mode.GetString() == "mode_e_brokered_execution"));
            Assert.Equal("p2_task_bound_guidance", apiWorkingModesRoot.GetProperty("planning_coupling_posture").GetString());
            Assert.Equal("task_bound_workspace_active", apiWorkingModesRoot.GetProperty("managed_workspace_posture").GetString());
            Assert.Equal(planHandle, apiWorkingModesRoot.GetProperty("plan_handle").GetString());
            Assert.Equal("task_scoped_activation_available", apiWorkingModesRoot.GetProperty("mode_e_operational_activation_state").GetString());
            Assert.Equal(taskId, apiWorkingModesRoot.GetProperty("mode_e_activation_task_id").GetString());
            Assert.Equal($".ai/execution/{taskId}/result.json", apiWorkingModesRoot.GetProperty("mode_e_activation_result_return_channel").GetString());
            Assert.Contains($"inspect runtime-brokered-execution {taskId}", apiWorkingModesRoot.GetProperty("mode_e_activation_recommended_next_action").GetString(), StringComparison.Ordinal);
            Assert.Equal(0, apiWorkingModesRoot.GetProperty("mode_e_activation_blocking_check_count").GetInt32());
            Assert.Equal(4, apiWorkingModesRoot.GetProperty("mode_e_activation_playbook_step_count").GetInt32());
            Assert.Equal($"inspect runtime-brokered-execution {taskId}", apiWorkingModesRoot.GetProperty("mode_e_activation_first_playbook_step_command").GetString());
            Assert.Contains(apiWorkingModesRoot.GetProperty("mode_e_activation_commands").EnumerateArray(), item =>
                item.GetString() == $"inspect runtime-brokered-execution {taskId}");
            Assert.Contains(apiWorkingModesRoot.GetProperty("mode_e_activation_playbook_steps").EnumerateArray(), item =>
                item.GetProperty("step_id").GetString() == "inspect_packet_enforcement"
                && item.GetProperty("command").GetString() == $"inspect packet-enforcement {taskId}"
                && item.GetProperty("applies_when").GetString() == "task_scoped_activation_available");
            Assert.Contains(apiWorkingModesRoot.GetProperty("mode_e_activation_checks").EnumerateArray(), item =>
                item.GetProperty("check_id").GetString() == "bound_task_truth_available"
                && item.GetProperty("state").GetString() == "satisfied");

            Assert.Contains("Runtime formal planning posture", inspectPlanningPosture.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: execution_bound_with_active_workspace", inspectPlanningPosture.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry trigger: formal_planning_packet_available", inspectPlanningPosture.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry command: plan status", inspectPlanningPosture.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Active planning slot state: occupied_by_packet", inspectPlanningPosture.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Active planning slot remediation: Run `plan status`", inspectPlanningPosture.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Plan handle: {planHandle}", inspectPlanningPosture.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Managed workspace posture: task_bound_workspace_active", inspectPlanningPosture.StandardOutput, StringComparison.Ordinal);

            using var apiPlanningPostureDocument = JsonDocument.Parse(apiPlanningPosture.StandardOutput);
            var apiPlanningPostureRoot = apiPlanningPostureDocument.RootElement;
            Assert.Equal("runtime-formal-planning-posture", apiPlanningPostureRoot.GetProperty("surface_id").GetString());
            Assert.Equal("execution_bound_with_active_workspace", apiPlanningPostureRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("mode_d_scoped_task_workspace", apiPlanningPostureRoot.GetProperty("current_mode").GetString());
            Assert.Equal("p2_task_bound_guidance", apiPlanningPostureRoot.GetProperty("planning_coupling_posture").GetString());
            Assert.Equal("formal_planning_packet_available", apiPlanningPostureRoot.GetProperty("formal_planning_entry_trigger_state").GetString());
            Assert.Equal("plan status", apiPlanningPostureRoot.GetProperty("formal_planning_entry_command").GetString());
            Assert.Contains("plan status", apiPlanningPostureRoot.GetProperty("formal_planning_entry_recommended_next_action").GetString(), StringComparison.Ordinal);
            Assert.Equal("occupied_by_packet", apiPlanningPostureRoot.GetProperty("active_planning_slot_state").GetString());
            Assert.False(apiPlanningPostureRoot.GetProperty("active_planning_slot_can_initialize").GetBoolean());
            Assert.Contains("plan status", apiPlanningPostureRoot.GetProperty("active_planning_slot_remediation_action").GetString(), StringComparison.Ordinal);
            Assert.Equal(planHandle, apiPlanningPostureRoot.GetProperty("plan_handle").GetString());
            Assert.Equal("task_bound_workspace_active", apiPlanningPostureRoot.GetProperty("managed_workspace_posture").GetString());
            Assert.Equal(0, apiPlanningPostureRoot.GetProperty("workspace_required_block_count").GetInt32());

            Assert.Contains("Runtime vendor-native acceleration", inspectVendorAcceleration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: optional_vendor_native_acceleration_ready", inspectVendorAcceleration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Codex reinforcement: repo_guard_assets_ready", inspectVendorAcceleration.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Claude reinforcement: bounded_runtime_qualification_ready", inspectVendorAcceleration.StandardOutput, StringComparison.Ordinal);

            using var apiVendorAccelerationDocument = JsonDocument.Parse(apiVendorAcceleration.StandardOutput);
            var apiVendorAccelerationRoot = apiVendorAccelerationDocument.RootElement;
            Assert.Equal("runtime-vendor-native-acceleration", apiVendorAccelerationRoot.GetProperty("surface_id").GetString());
            Assert.Equal("optional_vendor_native_acceleration_ready", apiVendorAccelerationRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("repo_guard_assets_ready", apiVendorAccelerationRoot.GetProperty("codex_reinforcement_state").GetString());
            Assert.Equal("bounded_runtime_qualification_ready", apiVendorAccelerationRoot.GetProperty("claude_reinforcement_state").GetString());
            Assert.Contains(apiVendorAccelerationRoot.GetProperty("codex_governance_assets").EnumerateArray(), item => item.GetString() == ".codex/config.toml");

            Assert.Contains("Runtime managed workspace", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Runtime document root: {Path.GetFullPath(sandbox.RootPath)}", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime document root mode: repo_local", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: task_bound_workspace_active", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode D hardening state: active", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Recoverable residue count:", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Recoverable cleanup action id:", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Recoverable cleanup action mode:", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Recoverable residue next action:", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Available actions:", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("- check: path_policy_fail_closed | state=satisfied", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Path policy enforcement: active", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("- policy: scope_escape | effect=fail_closed_and_require_replan", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"- bound-task: {taskId}", inspect.StandardOutput, StringComparison.Ordinal);

            using var apiDocument = JsonDocument.Parse(api.StandardOutput);
            var apiRoot = apiDocument.RootElement;
            Assert.Equal("runtime-managed-workspace", apiRoot.GetProperty("surface_id").GetString());
            Assert.Equal(Path.GetFullPath(sandbox.RootPath), apiRoot.GetProperty("runtime_document_root").GetString());
            Assert.Equal("repo_local", apiRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Equal("task_bound_workspace_active", apiRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("healthy", apiRoot.GetProperty("operational_state").GetString());
            Assert.True(apiRoot.GetProperty("safe_to_start_new_execution").GetBoolean());
            Assert.Equal("mode_d_scoped_task_workspace_hardening", apiRoot.GetProperty("mode_d_profile_id").GetString());
            Assert.Equal("active", apiRoot.GetProperty("mode_d_hardening_state").GetString());
            Assert.Equal("active", apiRoot.GetProperty("path_policy_enforcement_state").GetString());
            Assert.Equal("no_recoverable_runtime_residue", apiRoot.GetProperty("recoverable_residue_posture").GetString());
            Assert.Equal(0, apiRoot.GetProperty("recoverable_residue_count").GetInt32());
            Assert.Equal("none", apiRoot.GetProperty("highest_recoverable_residue_severity").GetString());
            Assert.False(apiRoot.GetProperty("recoverable_residue_blocks_auto_run").GetBoolean());
            Assert.Equal(string.Empty, apiRoot.GetProperty("recoverable_cleanup_action_id").GetString());
            Assert.Equal("none", apiRoot.GetProperty("recoverable_cleanup_action_mode").GetString());
            Assert.Empty(apiRoot.GetProperty("available_actions").EnumerateArray());
            Assert.Contains(apiRoot.GetProperty("bound_task_ids").EnumerateArray(), item => item.GetString() == taskId);
            Assert.Contains(apiRoot.GetProperty("active_leases").EnumerateArray(), item => item.GetProperty("task_id").GetString() == taskId);
            Assert.Contains(apiRoot.GetProperty("path_policies").EnumerateArray(), item => item.GetProperty("policy_class").GetString() == "host_only");
            Assert.Contains(apiRoot.GetProperty("path_policies").EnumerateArray(), item =>
                item.GetProperty("policy_class").GetString() == "scope_escape"
                && item.GetProperty("enforcement_effect").GetString() == "fail_closed_and_require_replan");
            Assert.Contains(apiRoot.GetProperty("mode_d_hardening_checks").EnumerateArray(), item =>
                item.GetProperty("check_id").GetString() == "official_truth_host_ingress"
                && item.GetProperty("state").GetString() == "satisfied");

            var leaseStatePath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "managed_workspace_leases.json");
            Assert.True(File.Exists(leaseStatePath));
        }
        finally
        {
            TryDeleteDirectory(externalLeaseRoot);
        }
    }

    [Fact]
    public void TaskRun_RejectsPlanBoundTaskWithoutManagedWorkspaceLease()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var scenario = FormalPlanningScenarioHarness.ProvisionPlanBoundTaskWithoutWorkspaceLease(
            sandbox,
            "Phase 5 gated task",
            "Require a managed workspace lease before delegated execution.",
            ["src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs"],
            ["managed workspace lease exists before dispatch"]);

        var planStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "status");
        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "task", "run", scenario.TaskId);
        var combined = string.Concat(result.StandardOutput, result.StandardError);
        var runRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", scenario.TaskId);

        Assert.Equal(0, planStatus.ExitCode);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("managed workspace lease", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"plan issue-workspace {scenario.TaskId}", combined, StringComparison.Ordinal);
        Assert.False(Directory.Exists(runRoot));

        using var planStatusDocument = JsonDocument.Parse(planStatus.StandardOutput);
        var planStatusRoot = planStatusDocument.RootElement;
        Assert.Equal("task_bound_workspace_ready_to_issue", planStatusRoot.GetProperty("managed_workspace_posture").GetString());
        Assert.Contains($"plan issue-workspace {scenario.TaskId}", planStatusRoot.GetProperty("managed_workspace_next_action").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeManagedWorkspace_ProjectsRecoverableCleanupWhenTerminalTaskLeaseResidueRemains()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var scenario = FormalPlanningScenarioHarness.ProvisionPlanBoundTaskWithoutWorkspaceLease(
            sandbox,
            "Phase 12 recoverable cleanup task",
            "Project recoverable cleanup posture when a terminal task still has an active workspace lease.",
            ["src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs"],
            ["recoverable cleanup posture is projected"]);

        var issueWorkspace = RunProgram("--repo-root", sandbox.RootPath, "--cold", "plan", "issue-workspace", scenario.TaskId);
        Assert.Equal(0, issueWorkspace.ExitCode);

        var nodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{scenario.TaskId}.json");
        var node = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        node["status"] = "completed";
        File.WriteAllText(nodePath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var graphPath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "graph.json");
        var graph = JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        foreach (var taskNode in graph["tasks"]!.AsArray())
        {
            var taskObject = taskNode!.AsObject();
            if (string.Equals(taskObject["task_id"]?.GetValue<string>(), scenario.TaskId, StringComparison.Ordinal))
            {
                taskObject["status"] = "completed";
            }
        }

        File.WriteAllText(graphPath, graph.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-managed-workspace");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-managed-workspace");
        var pilotStatus = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pilot", "status");
        var pilotStatusJson = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pilot", "status", "--json");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Equal(0, pilotStatus.ExitCode);
        Assert.Equal(0, pilotStatusJson.ExitCode);
        Assert.Contains("Recoverable residue posture: recoverable_runtime_residue_present", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recoverable residue count: 1", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves cleanup", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recoverable cleanup required: True", pilotStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recoverable residue count: 1", pilotStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recoverable cleanup action id: cleanup_runtime_residue", pilotStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recoverable cleanup action mode: dry_run_first", pilotStatus.StandardOutput, StringComparison.Ordinal);

        using var apiDocument = JsonDocument.Parse(api.StandardOutput);
        var apiRoot = apiDocument.RootElement;
        Assert.Equal("recoverable_runtime_residue_present", apiRoot.GetProperty("recoverable_residue_posture").GetString());
        Assert.Equal(1, apiRoot.GetProperty("recoverable_residue_count").GetInt32());
        Assert.Equal("healthy_with_recoverable_residue", apiRoot.GetProperty("operational_state").GetString());
        Assert.False(apiRoot.GetProperty("safe_to_start_new_execution").GetBoolean());
        Assert.True(apiRoot.GetProperty("safe_to_cleanup").GetBoolean());
        Assert.Equal("warning", apiRoot.GetProperty("highest_recoverable_residue_severity").GetString());
        Assert.True(apiRoot.GetProperty("recoverable_residue_blocks_auto_run").GetBoolean());
        Assert.Equal("cleanup_runtime_residue", apiRoot.GetProperty("recoverable_cleanup_action_id").GetString());
        Assert.Equal("dry_run_first", apiRoot.GetProperty("recoverable_cleanup_action_mode").GetString());
        Assert.Equal(0, apiRoot.GetProperty("active_leases").GetArrayLength());
        Assert.Contains(apiRoot.GetProperty("available_actions").EnumerateArray(), item =>
            item.GetProperty("action_id").GetString() == "cleanup_runtime_residue"
            && item.GetProperty("kind").GetString() == "cleanup"
            && item.GetProperty("command").GetString() == "carves cleanup"
            && item.GetProperty("action_mode").GetString() == "dry_run_first");
        Assert.Contains("carves cleanup", apiRoot.GetProperty("recoverable_residue_recommended_next_action").GetString(), StringComparison.Ordinal);
        Assert.Contains(apiRoot.GetProperty("recoverable_residues").EnumerateArray(), item =>
            item.GetProperty("residue_class").GetString() == "terminal_task_active_lease"
            && item.GetProperty("task_id").GetString() == scenario.TaskId);

        using var pilotStatusDocument = JsonDocument.Parse(pilotStatusJson.StandardOutput);
        var pilotStatusRoot = pilotStatusDocument.RootElement;
        Assert.Equal("runtime-product-closure-pilot-status", pilotStatusRoot.GetProperty("surface_id").GetString());
        Assert.Equal("healthy_with_recoverable_residue", pilotStatusRoot.GetProperty("operational_state").GetString());
        Assert.False(pilotStatusRoot.GetProperty("safe_to_start_new_execution").GetBoolean());
        Assert.True(pilotStatusRoot.GetProperty("safe_to_discuss").GetBoolean());
        Assert.True(pilotStatusRoot.GetProperty("safe_to_cleanup").GetBoolean());
        Assert.True(pilotStatusRoot.GetProperty("recoverable_cleanup_required").GetBoolean());
        Assert.Equal(1, pilotStatusRoot.GetProperty("recoverable_residue_count").GetInt32());
        Assert.Equal("warning", pilotStatusRoot.GetProperty("highest_recoverable_residue_severity").GetString());
        Assert.True(pilotStatusRoot.GetProperty("recoverable_residue_blocks_auto_run").GetBoolean());
        Assert.Equal("cleanup_runtime_residue", pilotStatusRoot.GetProperty("recoverable_cleanup_action_id").GetString());
        Assert.Equal("dry_run_first", pilotStatusRoot.GetProperty("recoverable_cleanup_action_mode").GetString());
        Assert.Contains("carves cleanup", pilotStatusRoot.GetProperty("recoverable_cleanup_recommended_next_action").GetString(), StringComparison.Ordinal);
    }

    private static ProgramRunResult RunProgram(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = Host.Program.Main(args);
            return new ProgramRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static string ExtractFirstMatch(string text, string pattern, string groupName)
    {
        var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
        Assert.True(match.Success, text);
        return match.Groups[groupName].Value;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}
