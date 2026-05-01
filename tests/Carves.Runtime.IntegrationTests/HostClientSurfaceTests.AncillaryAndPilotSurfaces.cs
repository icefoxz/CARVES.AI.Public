using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Host;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class HostClientSurfaceTests
{
    [Fact]
    public void AncillarySurfaces_ProjectAcceptanceContractGapsAsDispatchBlocked()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-ANCILLARY-MISSING-CONTRACT";
        sandbox.AddSyntheticPendingTask(taskId, includeAcceptanceContract: false);

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var dashboard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "dashboard", "--text");
            var discussBlocked = ProgramHarness.Run("--repo-root", sandbox.RootPath, "discuss", "blocked");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "status");

            Assert.True(dashboard.ExitCode == 0, dashboard.CombinedOutput);
            Assert.True(discussBlocked.ExitCode == 0, discussBlocked.CombinedOutput);
            Assert.True(status.ExitCode == 0, status.CombinedOutput);

            Assert.Contains("Dispatch state: dispatch_blocked", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Dispatch next task: (none)", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Next ready task:", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Acceptance contract gaps: 1", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode C/D entry first blocker: acceptance_contract_projected", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry first blocker command: inspect task {taskId}", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry next command: inspect task {taskId}", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Acceptance contract ingress policy: planning_truth_mutation=auto_minimum_contract; execution_dispatch=explicit_gap_required", dashboard.StandardOutput, StringComparison.Ordinal);

            Assert.Contains("\"acceptance_contract_gap_count\": 1", discussBlocked.StandardOutput, StringComparison.Ordinal);
            Assert.Contains(taskId, discussBlocked.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("project a minimum acceptance contract onto blocked pending tasks before dispatch", discussBlocked.StandardOutput, StringComparison.Ordinal);

            Assert.Contains("Dispatch state: dispatch_blocked", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Next dispatchable task: (none)", status.CombinedOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Next ready task:", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Mode C/D entry first blocker: acceptance_contract_projected", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry first blocker command: inspect task {taskId}", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry next command: inspect task {taskId}", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Acceptance contract ingress policy: planning_truth_mutation=auto_minimum_contract; execution_dispatch=explicit_gap_required", status.CombinedOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void AncillarySurfaces_ProjectManagedWorkspaceGapsAsDispatchBlocked()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var scenario = FormalPlanningScenarioHarness.ProvisionPlanBoundTaskWithoutWorkspaceLease(
            sandbox,
            "Phase 5 ancillary surface proof",
            "Project managed workspace execution gaps into ancillary surfaces.",
            ["src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs"],
            ["managed workspace lease exists before dispatch"]);

        try
        {
            var dashboard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "dashboard", "--text");
            var workbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "workbench", "overview");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "status");

            Assert.True(dashboard.ExitCode == 0, dashboard.CombinedOutput);
            Assert.True(workbench.ExitCode == 0, workbench.CombinedOutput);
            Assert.True(status.ExitCode == 0, status.CombinedOutput);

            Assert.Contains("Agent working mode: mode_c_task_bound_workspace", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent recommended mode: mode_e_brokered_execution", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent recommendation posture: mode_e_recommended_for_packet_bound_handoff", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent constraint tier: hard_runtime_gate", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent stronger-mode blockers: 0", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent first stronger-mode blocker: (none)", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation: task_scoped_activation_available", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation task: {scenario.TaskId}", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation command: inspect runtime-brokered-execution {scenario.TaskId}", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E result return channel: .ai/execution/{scenario.TaskId}/result.json", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation next action: Run `inspect runtime-brokered-execution {scenario.TaskId}`", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation blocking checks: 0", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation first blocker: (none)", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation playbook steps: 4", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation first playbook command: inspect runtime-brokered-execution {scenario.TaskId}", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Planning coupling: p2_task_bound_guidance", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning posture: execution_blocked_by_workspace_gap", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry trigger: formal_planning_packet_available", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry command: plan status", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry next action: Continue the current formal planning packet through `plan status`", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Active planning slot state: occupied_by_packet", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Active planning slot remediation: Run `plan status`", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Active plan handle: plan-", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Managed workspace posture: task_bound_workspace_ready_to_issue", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Vendor-native acceleration: optional_vendor_native_acceleration_ready", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Codex reinforcement: repo_guard_assets_ready", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Claude reinforcement: bounded_runtime_qualification_ready", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Dispatch state: dispatch_blocked", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Dispatch next task: (none)", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Plan-required execution gaps: 0", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Managed workspace gaps: 1", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode C/D entry first blocker: managed_workspace_lease_available", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry first blocker command: plan issue-workspace {scenario.TaskId}", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry next command: plan issue-workspace {scenario.TaskId}", dashboard.StandardOutput, StringComparison.Ordinal);

            Assert.Contains("Agent working mode: mode_c_task_bound_workspace", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("External agent recommended mode: mode_e_brokered_execution", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("External agent recommendation posture: mode_e_recommended_for_packet_bound_handoff", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("External agent constraint tier: hard_runtime_gate", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("External agent stronger-mode blockers: 0", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("External agent first stronger-mode blocker: (none)", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation: task_scoped_activation_available", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation task: {scenario.TaskId}", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation command: inspect runtime-brokered-execution {scenario.TaskId}", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E result return channel: .ai/execution/{scenario.TaskId}/result.json", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation next action: Run `inspect runtime-brokered-execution {scenario.TaskId}`", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation blocking checks: 0", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation first blocker: (none)", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation playbook steps: 4", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation first playbook command: inspect runtime-brokered-execution {scenario.TaskId}", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Planning coupling posture: p2_task_bound_guidance", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning posture: execution_blocked_by_workspace_gap", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry trigger: formal_planning_packet_available", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry command: plan status", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry next action: Continue the current formal planning packet through `plan status`", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Active planning slot state: occupied_by_packet", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Active planning slot remediation: Run `plan status`", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Managed workspace posture: task_bound_workspace_ready_to_issue", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Vendor-native acceleration: optional_vendor_native_acceleration_ready", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Codex reinforcement: repo_guard_assets_ready", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Claude reinforcement: bounded_runtime_qualification_ready", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Dispatch state: dispatch_blocked", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Dispatch-blocking formal planning gaps: 0", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Dispatch-blocking managed workspace gaps: 1", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains("Mode C/D entry first blocker: managed_workspace_lease_available", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry first blocker command: plan issue-workspace {scenario.TaskId}", status.CombinedOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry next command: plan issue-workspace {scenario.TaskId}", status.CombinedOutput, StringComparison.Ordinal);

            Assert.Contains("Surface: overview", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Agent working mode: mode_c_task_bound_workspace", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent recommended mode: mode_e_brokered_execution", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent recommendation posture: mode_e_recommended_for_packet_bound_handoff", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent constraint tier: hard_runtime_gate", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent stronger-mode blockers: 0", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("External agent first stronger-mode blocker: (none)", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation: task_scoped_activation_available", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation task: {scenario.TaskId}", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation command: inspect runtime-brokered-execution {scenario.TaskId}", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E result return channel: .ai/execution/{scenario.TaskId}/result.json", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation next action: Run `inspect runtime-brokered-execution {scenario.TaskId}`", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation blocking checks: 0", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation first blocker: (none)", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode E activation playbook steps: 4", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode E activation first playbook command: inspect runtime-brokered-execution {scenario.TaskId}", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Planning coupling: p2_task_bound_guidance", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning posture: execution_blocked_by_workspace_gap", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry trigger: formal_planning_packet_available", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry command: plan status", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Formal planning entry next action: Continue the current formal planning packet through `plan status`", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Active planning slot state: occupied_by_packet", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Active planning slot remediation: Run `plan status`", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Managed workspace posture: task_bound_workspace_ready_to_issue", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Vendor-native acceleration: optional_vendor_native_acceleration_ready", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Codex reinforcement: repo_guard_assets_ready", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Claude reinforcement: bounded_runtime_qualification_ready", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Managed workspace gaps: 1", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode C/D entry first blocker: managed_workspace_lease_available", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry first blocker command: plan issue-workspace {scenario.TaskId}", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Mode C/D entry next command: plan issue-workspace {scenario.TaskId}", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("mode_e_activation=task_scoped_activation_available", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("external_agent_recommended_mode=mode_e_brokered_execution", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("planning_entry=formal_planning_packet_available", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("active_planning_slot=occupied_by_packet", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("workspace_required=1", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("vendor_native_acceleration=optional_vendor_native_acceleration_ready", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"{scenario.TaskId} [Pending]", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"plan issue-workspace {scenario.TaskId}", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("managed workspace lease", workbench.StandardOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void Console_ConnectsToHostAndSupportsInspectById()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "console", "--script", "1,p,card CARD-077,task T-CARD-077-001,0");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Connected to host:", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"card_id\": \"CARD-077\"", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"task_id\": \"T-CARD-077-001\"", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void HostRoutedWorkerGovernanceSurface_ListsProvidersProfilesAndSelection()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-worker-surface");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-worker-surface", "gemini-worker-balanced");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var providers = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "providers");
            var profiles = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "profiles", "repo-worker-surface");
            var selection = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "select", "repo-worker-surface");
            var selectionApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "worker-selection", "repo-worker-surface");

            Assert.Equal(0, providers.ExitCode);
            Assert.Contains("codex_sdk", providers.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, profiles.ExitCode);
            Assert.Contains("workspace_build_test", profiles.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, selection.ExitCode);
            Assert.Contains("Selected backend:", selection.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("codex_cli", selection.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, selectionApi.ExitCode);
            Assert.Contains("\"requested_trust_profile_id\": \"workspace_build_test\"", selectionApi.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"selected_backend_id\": \"codex_cli\"", selectionApi.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void HostRoutedPolicySurface_InspectsAndValidatesExternalizedPolicies()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "policy", "inspect");
            var validate = ProgramHarness.Run("--repo-root", sandbox.RootPath, "policy", "validate");

            Assert.Equal(0, inspect.ExitCode);
            Assert.Contains("delegation.policy.json", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("role-governance.policy.json", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("export-profiles.policy.json", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("worker-selection.policy.json", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("allowed backends: codex_cli, null_worker", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("host-invoke.policy.json", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("control_plane_mutation: request_timeout=", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("base_wait=", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("hard_max_wait=", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, validate.ExitCode);
            Assert.Contains("Policy validation: valid", validate.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void HostRestart_RehydratesPendingApprovalAndPreservesExplainability()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-HOST-REHYDRATION", scope: ["README.md"]);
        sandbox.SetTaskMetadata("T-HOST-REHYDRATION", "worker_backend", "codex_sdk");
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
        sandbox.WriteWorkerOperationalPolicy(CodexSdkWorkerOperationalPolicyJson);
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "repo", "register", sandbox.RootPath, "--repo-id", "repo-host-rehydration");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "provider", "bind", "repo-host-rehydration", "codex-worker-trusted");

        var bridgeScript = CreateApprovalBridgeScript();
        var originalBridge = Environment.GetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
            Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", bridgeScript);
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "run-next");
            StopHost(sandbox.RootPath);

            var restart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status");
            var approvals = ProgramHarness.Run("--repo-root", sandbox.RootPath, "worker", "approvals");
            var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "task", "inspect", "T-HOST-REHYDRATION");
            var operational = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "operational-summary");

            Assert.Equal(0, restart.ExitCode);
            Assert.Contains("Rehydrated: True", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Pending approvals: 1", status.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Rehydration summary:", status.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, approvals.ExitCode);
            Assert.Contains("Pending permission requests: 1", approvals.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, inspect.ExitCode);
            Assert.Contains("\"status\": \"ApprovalWait\"", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, operational.ExitCode);
            Assert.Contains("\"pending_approval_count\": 1", operational.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
            Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", originalBridge);
            StopHost(sandbox.RootPath);
            if (File.Exists(bridgeScript))
            {
                File.Delete(bridgeScript);
            }
        }
    }


    [Fact]
    public void AttachToTaskProof_CapturesAuditableAttachRunResultAndReplanTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/PilotProof.cs", "namespace Pilot.Proof; public sealed class PilotProofService { }");
        targetRepo.CommitAll("Initial commit");

        const string cardId = "CARD-241-PILOT";
        const string draftId = "TG-CARD-241-PILOT-001";
        const string taskId = "T-CARD-241-PILOT-001";

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var scenario = ProvisionPilotProofScenario(sandbox.RootPath, targetRepo.RootPath, cardId, draftId, taskId);

            var ingest = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "task", "ingest-result", scenario.TaskId);
            var proof = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "inspect", "attach-proof", scenario.TaskId);
            var task = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "task", "inspect", scenario.TaskId, "--runs");
            var proofPath = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "proofs", "attach-to-task", $"{scenario.TaskId}.json");

            Assert.Equal(0, ingest.ExitCode);
            Assert.Contains("Result status: failed", ingest.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, proof.ExitCode);
            Assert.Contains("Runtime manifest: present", proof.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Attach handshake: present", proof.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Result: failed", proof.StandardOutput, StringComparison.Ordinal);
            Assert.True(File.Exists(proofPath));

            using var taskDocument = ParseJsonOutput(task.StandardOutput);
            using var proofDocument = JsonDocument.Parse(File.ReadAllText(proofPath));
            Assert.Equal(0, task.ExitCode);
            Assert.Contains(taskDocument.RootElement.GetProperty("status").GetString(), new[] { "Review", "Blocked" });
            var taskReplanEntryId = taskDocument.RootElement.GetProperty("planner_emergence").GetProperty("replan_entry_id").GetString();
            Assert.Equal("attach-to-task-proof.v1", proofDocument.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal(scenario.TaskId, proofDocument.RootElement.GetProperty("task_id").GetString());
            Assert.False(string.IsNullOrWhiteSpace(proofDocument.RootElement.GetProperty("execution_run_id").GetString()));
            Assert.Equal("failed", proofDocument.RootElement.GetProperty("result_status").GetString());
            var proofReplanEntryId = proofDocument.RootElement.GetProperty("replan_entry_id").GetString();
            var reviewVerdict = proofDocument.RootElement.GetProperty("review_verdict").GetString();
            Assert.True(
                !string.IsNullOrWhiteSpace(taskReplanEntryId)
                || !string.IsNullOrWhiteSpace(proofReplanEntryId)
                || !string.IsNullOrWhiteSpace(reviewVerdict),
                "attach proof should retain either replan truth or review truth.");
            Assert.Contains(
                proofDocument.RootElement.GetProperty("evidence_paths").EnumerateArray().Select(item => item.GetString()),
                path => path is not null && path.EndsWith(Path.Combine(".ai", "runtime.json"), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void PilotEvidence_HostSurfaceRecordsListsAndInspectsStructuredFeedback()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/PilotEvidence.cs", "namespace Pilot.Evidence; public sealed class PilotEvidenceService { }");
        targetRepo.CommitAll("Initial commit");

        const string cardId = "CARD-243-PILOT";
        const string draftId = "TG-CARD-243-PILOT-001";
        const string taskId = "T-CARD-243-PILOT-001";

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var scenario = ProvisionPilotProofScenario(sandbox.RootPath, targetRepo.RootPath, cardId, draftId, taskId);
            ProgramHarness.Run("--repo-root", targetRepo.RootPath, "task", "ingest-result", scenario.TaskId);

            var payloadDirectory = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(payloadDirectory);
            var evidencePayload = Path.Combine(payloadDirectory, "pilot-evidence.json");
            File.WriteAllText(evidencePayload, JsonSerializer.Serialize(new
            {
                task_id = scenario.TaskId,
                card_id = scenario.CardId,
                summary = "First pilot exposed a repeatable replan path.",
                observations = new[] { "attach and card lifecycle completed through host", "dry-run plus result ingestion produced governed proof" },
                friction_points = new[] { "manual result envelope creation was still required" },
                failed_expectations = new[] { "delegated worker completion was not part of this pilot" },
                follow_ups = new object[]
                {
                    new
                    {
                        kind = "governance_debt",
                        title = "Tighten delegated completion proof",
                        reason = "The pilot still required manual result ingestion."
                    }
                }
            }));

            var record = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "pilot", "record-evidence", evidencePayload);
            var evidenceDirectory = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "pilot-evidence");
            var evidencePath = Directory.EnumerateFiles(evidenceDirectory, "*.json", SearchOption.TopDirectoryOnly).Single();
            var evidenceId = Path.GetFileNameWithoutExtension(evidencePath);
            var list = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "pilot", "list-evidence");
            var inspect = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "pilot", "inspect-evidence", evidenceId);

            Assert.Equal(0, record.ExitCode);
            Assert.Contains($"Recorded pilot evidence {evidenceId}", record.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, list.ExitCode);
            Assert.Contains(evidenceId, list.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, inspect.ExitCode);
            Assert.Contains("First pilot exposed a repeatable replan path.", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("[governance_debt] Tighten delegated completion proof", inspect.StandardOutput, StringComparison.Ordinal);

            using var document = JsonDocument.Parse(File.ReadAllText(evidencePath));
            Assert.Equal("pilot-evidence-record.v1", document.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal(scenario.TaskId, document.RootElement.GetProperty("task_id").GetString());
            Assert.Equal(scenario.CardId, document.RootElement.GetProperty("card_id").GetString());
            Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("attach_proof_id").GetString()));
            Assert.Equal("recorded", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(1, document.RootElement.GetProperty("follow_ups").GetArrayLength());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void PilotProblemIntake_HostSurfaceRecordsProblemAndMirrorsPilotEvidence()
    {
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/PilotProblemIntake.cs", "namespace Pilot.ProblemIntake; public sealed class PilotProblemIntakeService { }");
        targetRepo.CommitAll("Initial commit");

        var payloadDirectory = Path.Combine(targetRepo.RootPath, ".carves-agent");
        Directory.CreateDirectory(payloadDirectory);
        var problemPayload = Path.Combine(payloadDirectory, "problem-intake.json");
        File.WriteAllText(problemPayload, JsonSerializer.Serialize(new
        {
            repo_id = "problem-intake-target",
            summary = "Agent stopped because CARVES next command was ambiguous.",
            problem_kind = "next_command_ambiguous",
            severity = "blocking",
            current_stage_id = "intent_capture",
            next_governed_command = "carves intent draft --persist",
            blocked_command = "carves pilot next --json",
            command_exit_code = 1,
            command_output = "next_governed_command was empty for the requested scope",
            stop_trigger = "next_governed_command is empty",
            observations = new[]
            {
                "agent did not continue into edits",
                "agent captured the blocker as bounded evidence",
            },
            affected_paths = new[]
            {
                ".ai/tasks/graph.json",
                "src/PilotProblemIntake.cs",
            },
            recommended_follow_up = "operator should clarify the active planning card before worker execution",
        }));

        var surface = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "pilot", "report-problem", problemPayload, "--json");
        Assert.Equal(0, surface.ExitCode);

        using var surfaceDocument = JsonDocument.Parse(surface.StandardOutput);
        var surfaceRoot = surfaceDocument.RootElement;
        var problemId = surfaceRoot.GetProperty("problem_id").GetString();
        var evidenceId = surfaceRoot.GetProperty("evidence_id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(problemId));
        Assert.False(string.IsNullOrWhiteSpace(evidenceId));
        Assert.Equal("pilot-problem-intake-record.v1", surfaceRoot.GetProperty("schema_version").GetString());
        Assert.Equal("next_command_ambiguous", surfaceRoot.GetProperty("problem_kind").GetString());
        Assert.Equal("blocking", surfaceRoot.GetProperty("severity").GetString());

        var list = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "pilot", "list-problems");
        var inspect = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "pilot", "inspect-problem", problemId!);
        var evidenceList = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "pilot", "list-evidence");
        var evidenceInspect = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "pilot", "inspect-evidence", evidenceId!);
        var problemPath = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "pilot-problems", $"{problemId}.json");
        var evidencePath = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "pilot-evidence", $"{evidenceId}.json");

        Assert.Equal(0, list.ExitCode);
        Assert.Contains(problemId!, list.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, inspect.ExitCode);
        Assert.Contains("Agent stopped because CARVES next command was ambiguous.", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Next governed command: carves intent draft --persist", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, evidenceList.ExitCode);
        Assert.Contains(evidenceId!, evidenceList.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, evidenceInspect.ExitCode);
        Assert.Contains("Agent problem intake: Agent stopped because CARVES next command was ambiguous.", evidenceInspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[agent_problem_intake] Agent stopped because CARVES next command was ambiguous.", evidenceInspect.StandardOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(problemPath));
        Assert.True(File.Exists(evidencePath));

        using var problemDocument = JsonDocument.Parse(File.ReadAllText(problemPath));
        var problemRoot = problemDocument.RootElement;
        Assert.Equal("pilot-problem-intake-record.v1", problemRoot.GetProperty("schema_version").GetString());
        Assert.Equal(evidenceId, problemRoot.GetProperty("evidence_id").GetString());
        Assert.Equal("problem-intake-target", problemRoot.GetProperty("repo_id").GetString());
        Assert.Equal("recorded", problemRoot.GetProperty("status").GetString());
        Assert.Equal(2, problemRoot.GetProperty("affected_paths").GetArrayLength());

        using var evidenceDocument = JsonDocument.Parse(File.ReadAllText(evidencePath));
        var evidenceRoot = evidenceDocument.RootElement;
        Assert.Equal("pilot-evidence-record.v1", evidenceRoot.GetProperty("schema_version").GetString());
        Assert.Equal("problem-intake-target", evidenceRoot.GetProperty("repo_id").GetString());
        Assert.Equal("Agent problem intake: Agent stopped because CARVES next command was ambiguous.", evidenceRoot.GetProperty("summary").GetString());
        Assert.Contains(evidenceRoot.GetProperty("follow_ups").EnumerateArray(), followUp =>
            followUp.GetProperty("kind").GetString() == "agent_problem_intake");
    }


    [Fact]
    public void PilotCloseLoop_HostSurfaceCreatesCanonicalResultAndClosesThePilotLoop()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = CreateGitSandbox();
        targetRepo.WriteFile("src/PilotCloseLoop.cs", "namespace Pilot.CloseLoop; public sealed class PilotCloseLoopService { }");
        targetRepo.CommitAll("Initial commit");

        const string cardId = "CARD-307-PILOT";
        const string draftId = "TG-CARD-307-PILOT-001";
        const string taskId = "T-CARD-307-PILOT-001";

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var scenario = ProvisionPilotDryRunScenario(sandbox.RootPath, targetRepo.RootPath, cardId, draftId, taskId);

            var payloadDirectory = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(payloadDirectory);
            var evidencePayload = Path.Combine(payloadDirectory, "pilot-close-loop-evidence.json");
            File.WriteAllText(evidencePayload, JsonSerializer.Serialize(new
            {
                task_id = scenario.TaskId,
                card_id = scenario.CardId,
                summary = "Runtime-native pilot close-loop recorded the bounded failure proof without manual result authoring.",
                observations = new[] { "dry-run execution seeded the canonical run id", "pilot close-loop generated admissible worker evidence" },
                friction_points = new[] { "close-loop still records a bounded failed proof rather than a worker-completed success path" },
            }));

            var closeLoop = ProgramHarness.Run(
                "--repo-root",
                targetRepo.RootPath,
                "pilot",
                "close-loop",
                scenario.TaskId,
                "--changed-file",
                "src/PilotCloseLoop.cs",
                "--review-reason",
                "pilot close-loop proof approved",
                "--record-evidence",
                evidencePayload);
            var inspect = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "task", "inspect", scenario.TaskId, "--runs");
            var proof = ProgramHarness.Run("--repo-root", targetRepo.RootPath, "inspect", "attach-proof", scenario.TaskId);
            var resultPath = Path.Combine(targetRepo.RootPath, ".ai", "execution", scenario.TaskId, "result.json");
            var workerArtifactPath = Path.Combine(targetRepo.RootPath, ".ai", "artifacts", "worker-executions", $"{scenario.TaskId}.json");
            var evidenceDirectory = Path.Combine(targetRepo.RootPath, ".ai", "artifacts", "worker-executions", scenario.RunId);
            var evidencePath = Path.Combine(evidenceDirectory, "evidence.json");
            var commandLogPath = Path.Combine(evidenceDirectory, "command.log");
            var buildLogPath = Path.Combine(evidenceDirectory, "build.log");
            var testLogPath = Path.Combine(evidenceDirectory, "test.log");
            var patchPath = Path.Combine(evidenceDirectory, "patch.diff");
            var pilotEvidenceDirectory = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "pilot-evidence");

            Assert.True(closeLoop.ExitCode == 0, closeLoop.CombinedOutput);
            Assert.Contains($"Closed pilot loop for {scenario.TaskId}.", closeLoop.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Execution run: {scenario.RunId}", closeLoop.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Ingested result envelope", closeLoop.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Recorded review for {scenario.TaskId}", closeLoop.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Recorded pilot evidence", closeLoop.StandardOutput, StringComparison.Ordinal);

            Assert.True(File.Exists(resultPath));
            Assert.True(File.Exists(workerArtifactPath));
            Assert.True(File.Exists(evidencePath));
            Assert.True(File.Exists(commandLogPath));
            Assert.True(File.Exists(buildLogPath));
            Assert.True(File.Exists(testLogPath));
            Assert.True(File.Exists(patchPath));
            var pilotEvidencePath = Directory.EnumerateFiles(pilotEvidenceDirectory, "*.json", SearchOption.TopDirectoryOnly).Single();

            using var inspectDocument = ParseJsonOutput(inspect.StandardOutput);
            using var resultDocument = JsonDocument.Parse(File.ReadAllText(resultPath));
            using var workerDocument = JsonDocument.Parse(File.ReadAllText(workerArtifactPath));
            using var evidenceDocument = JsonDocument.Parse(File.ReadAllText(evidencePath));
            using var pilotEvidenceDocument = JsonDocument.Parse(File.ReadAllText(pilotEvidencePath));

            Assert.Equal(0, inspect.ExitCode);
            Assert.Equal("Completed", inspectDocument.RootElement.GetProperty("status").GetString());
            var latestRunId = inspectDocument.RootElement.GetProperty("execution_run").GetProperty("latest_run_id").GetString();
            Assert.StartsWith($"RUN-{scenario.TaskId}-", latestRunId, StringComparison.Ordinal);
            Assert.Equal("ManualFallbackCompleted", inspectDocument.RootElement.GetProperty("execution_run").GetProperty("latest_status").GetString());
            Assert.True(inspectDocument.RootElement.GetProperty("execution_run").GetProperty("active_run_id").ValueKind == JsonValueKind.Null);
            var runs = inspectDocument.RootElement.GetProperty("runs");
            Assert.Equal("Stopped", runs[0].GetProperty("status").GetString());
            Assert.Equal("Planned", runs[1].GetProperty("status").GetString());

            Assert.Equal("failed", resultDocument.RootElement.GetProperty("status").GetString());
            Assert.Equal(scenario.RunId, resultDocument.RootElement.GetProperty("executionRunId").GetString());
            Assert.Equal(".ai/artifacts/worker-executions/" + scenario.RunId + "/evidence.json", resultDocument.RootElement.GetProperty("executionEvidencePath").GetString());

            Assert.Equal("failed", workerDocument.RootElement.GetProperty("result").GetProperty("status").GetString());
            Assert.Equal(scenario.RunId, workerDocument.RootElement.GetProperty("result").GetProperty("run_id").GetString());
            Assert.Equal("pilot_close_loop", workerDocument.RootElement.GetProperty("result").GetProperty("adapter_reason").GetString());

            Assert.Equal("replayable", evidenceDocument.RootElement.GetProperty("evidence_strength").GetString());
            Assert.Equal("complete", evidenceDocument.RootElement.GetProperty("evidence_completeness").GetString());
            Assert.Equal(".ai/artifacts/worker-executions/" + scenario.RunId + "/build.log", evidenceDocument.RootElement.GetProperty("build_output_ref").GetString());
            Assert.Equal(".ai/artifacts/worker-executions/" + scenario.RunId + "/test.log", evidenceDocument.RootElement.GetProperty("test_output_ref").GetString());

            Assert.Equal("pilot-evidence-record.v1", pilotEvidenceDocument.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal(scenario.TaskId, pilotEvidenceDocument.RootElement.GetProperty("task_id").GetString());
            Assert.Equal(scenario.CardId, pilotEvidenceDocument.RootElement.GetProperty("card_id").GetString());
            Assert.Contains("Review verdict: Complete", proof.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Result: failed", proof.StandardOutput, StringComparison.Ordinal);

            var taskNodePath = Path.Combine(targetRepo.RootPath, ".ai", "tasks", "nodes", $"{scenario.TaskId}.json");
            using var taskNodeDocument = JsonDocument.Parse(File.ReadAllText(taskNodePath));
            Assert.Equal("ManualFallbackCompleted", taskNodeDocument.RootElement.GetProperty("metadata").GetProperty("execution_run_latest_status").GetString());
            Assert.False(taskNodeDocument.RootElement.GetProperty("metadata").TryGetProperty("execution_run_active_id", out _));

            var latestRunPath = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "runs", scenario.TaskId, $"{latestRunId}.json");
            using var latestRunDocument = JsonDocument.Parse(File.ReadAllText(latestRunPath));
            Assert.Equal("Planned", latestRunDocument.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

}
