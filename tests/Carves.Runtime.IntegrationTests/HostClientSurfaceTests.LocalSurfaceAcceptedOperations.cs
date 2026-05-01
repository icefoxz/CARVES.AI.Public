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
    public void DashboardSummary_ProjectsRecentAcceptedOperations()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var services = RuntimeComposition.Create(sandbox.RootPath);
        var hostAssembly = typeof(Program).Assembly;
        var storeType = hostAssembly.GetType("Carves.Runtime.Host.HostAcceptedOperationStore", throwOnError: true)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore type.");
        var surfaceType = hostAssembly.GetType("Carves.Runtime.Host.LocalHostSurfaceService", throwOnError: true)
            ?? throw new InvalidOperationException("Expected LocalHostSurfaceService type.");
        var stateType = hostAssembly.GetType("Carves.Runtime.Host.LocalHostState", throwOnError: true)
            ?? throw new InvalidOperationException("Expected LocalHostState type.");

        var store = Activator.CreateInstance(storeType)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore instance.");
        var accept = storeType.GetMethod(
            "Accept",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(string), typeof(string)],
            null)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.Accept.");
        var markDispatching = storeType.GetMethod("MarkDispatching", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.MarkDispatching.");
        var markRunning = storeType.GetMethod("MarkRunning", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.MarkRunning.");

        var accepted = accept.Invoke(store, ["review-task", null])!;
        var operationId = (string)accepted.GetType().GetProperty("OperationId")!.GetValue(accepted)!;
        markDispatching.Invoke(store, [operationId]);
        markRunning.Invoke(store, [operationId]);

        var surface = Activator.CreateInstance(surfaceType, services, store)
            ?? throw new InvalidOperationException("Expected LocalHostSurfaceService instance.");
        var hostState = Activator.CreateInstance(
            stateType,
            sandbox.RootPath,
            "http://127.0.0.1:43111",
            Path.Combine(sandbox.RootPath, ".carves-platform", "host", "runtime"),
            AppContext.BaseDirectory,
            typeof(Program).Assembly.Location,
            43111)
            ?? throw new InvalidOperationException("Expected LocalHostState instance.");
        var buildDashboardSummary = surfaceType.GetMethod("BuildDashboardSummary", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected LocalHostSurfaceService.BuildDashboardSummary.");

        var summary = (JsonObject)buildDashboardSummary.Invoke(surface, [hostState])!;
        var operations = summary["accepted_operations"]!.AsArray();
        var acceptanceContractIngressPolicy = summary["acceptance_contract_ingress_policy"]!.AsObject();
        var agentWorkingModes = summary["agent_working_modes"]!.AsObject();
        var formalPlanningPosture = summary["formal_planning_posture"]!.AsObject();
        var sessionGatewayGovernanceAssist = summary["session_gateway_governance_assist"]!.AsObject();
        var highestPriorityPressure = sessionGatewayGovernanceAssist["highest_priority_pressure"]!.AsObject();

        Assert.NotEmpty(operations);
        Assert.Equal("review-task", operations[0]!["command"]!.GetValue<string>());
        Assert.Equal("running", operations[0]!["progress_marker"]!.GetValue<string>());
        Assert.False(operations[0]!["completed"]!.GetValue<bool>());
        Assert.Equal("planning_truth_mutation=auto_minimum_contract; execution_dispatch=explicit_gap_required", acceptanceContractIngressPolicy["policy_summary"]!.GetValue<string>());
        Assert.NotNull(agentWorkingModes["current_mode"]!.GetValue<string>());
        Assert.Equal("mode_a_open_repo_advisory", agentWorkingModes["external_agent_recommended_mode"]!.GetValue<string>());
        Assert.Equal("advisory_until_formal_planning", agentWorkingModes["external_agent_recommendation_posture"]!.GetValue<string>());
        Assert.Equal("soft_advisory", agentWorkingModes["external_agent_constraint_tier"]!.GetValue<string>());
        Assert.True(agentWorkingModes["external_agent_stronger_mode_blocker_count"]!.GetValue<int>() > 0);
        Assert.Equal("formal_planning_packet_available", agentWorkingModes["external_agent_first_stronger_mode_blocker_id"]!.GetValue<string>());
        Assert.Contains("plan init", agentWorkingModes["external_agent_first_stronger_mode_blocker_required_action"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.NotNull(agentWorkingModes["planning_coupling_posture"]!.GetValue<string>());
        Assert.Equal("plan_init_required_before_mode_e_activation", agentWorkingModes["mode_e_operational_activation_state"]!.GetValue<string>());
        Assert.Empty(agentWorkingModes["mode_e_activation_commands"]!.AsArray());
        Assert.Contains("plan init", agentWorkingModes["mode_e_activation_recommended_next_action"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(agentWorkingModes["mode_e_activation_blocking_check_count"]!.GetValue<int>() > 0);
        Assert.Equal("formal_planning_packet_available", agentWorkingModes["mode_e_activation_first_blocking_check_id"]!.GetValue<string>());
        Assert.Contains("plan init", agentWorkingModes["mode_e_activation_first_blocking_check_required_action"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.True(agentWorkingModes["mode_e_activation_playbook_step_count"]!.GetValue<int>() > 0);
        Assert.Equal("plan init [candidate-card-id]", agentWorkingModes["mode_e_activation_first_playbook_step_command"]!.GetValue<string>());
        Assert.NotNull(formalPlanningPosture["overall_posture"]!.GetValue<string>());
        Assert.Equal("discussion_only", formalPlanningPosture["formal_planning_entry_trigger_state"]!.GetValue<string>());
        Assert.Equal("plan init [candidate-card-id]", formalPlanningPosture["formal_planning_entry_command"]!.GetValue<string>());
        Assert.Contains("plan init [candidate-card-id]", formalPlanningPosture["formal_planning_entry_recommended_next_action"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.NotNull(formalPlanningPosture["managed_workspace_posture"]!.GetValue<string>());
        Assert.NotNull(sessionGatewayGovernanceAssist["overall_posture"]!.GetValue<string>());
        Assert.NotNull(sessionGatewayGovernanceAssist["recommended_next_action"]!.GetValue<string>());
        Assert.NotNull(highestPriorityPressure["pressure_kind"]!.GetValue<string>());
    }


    [Fact]
    public void WorkbenchReviewText_ProjectsRecentAcceptedOperations()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var services = RuntimeComposition.Create(sandbox.RootPath);
        var hostAssembly = typeof(Program).Assembly;
        var storeType = hostAssembly.GetType("Carves.Runtime.Host.HostAcceptedOperationStore", throwOnError: true)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore type.");
        var surfaceType = hostAssembly.GetType("Carves.Runtime.Host.LocalHostSurfaceService", throwOnError: true)
            ?? throw new InvalidOperationException("Expected LocalHostSurfaceService type.");

        var store = Activator.CreateInstance(storeType)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore instance.");
        var accept = storeType.GetMethod(
            "Accept",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(string), typeof(string)],
            null)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.Accept.");
        var markDispatching = storeType.GetMethod("MarkDispatching", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected HostAcceptedOperationStore.MarkDispatching.");

        var accepted = accept.Invoke(store, ["sync-state", null])!;
        var operationId = (string)accepted.GetType().GetProperty("OperationId")!.GetValue(accepted)!;
        markDispatching.Invoke(store, [operationId]);

        var surface = Activator.CreateInstance(surfaceType, services, store)
            ?? throw new InvalidOperationException("Expected LocalHostSurfaceService instance.");
        var renderWorkbenchTextReview = surfaceType.GetMethod("RenderWorkbenchTextReview", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Expected LocalHostSurfaceService.RenderWorkbenchTextReview.");

        var lines = ((IReadOnlyList<string>)renderWorkbenchTextReview.Invoke(surface, Array.Empty<object>())!).ToArray();

        Assert.Contains("Accepted operations:", lines);
        Assert.Contains(lines, line => line.Contains("sync-state", StringComparison.Ordinal) && line.Contains("[dispatching]", StringComparison.Ordinal));
    }

}
