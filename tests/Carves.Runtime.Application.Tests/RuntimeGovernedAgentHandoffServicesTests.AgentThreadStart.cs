using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed partial class RuntimeGovernedAgentHandoffServicesTests
{
    [Fact]
    public void AgentThreadStart_AggregatesStartGateStatusAndHandoffIntoOneCommand()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# first run");

        var surface = new RuntimeAgentThreadStartService(
            workspace.RootPath,
            () => new RuntimeExternalTargetPilotStartSurface
            {
                OverallPosture = "external_target_pilot_start_bundle_ready",
                PilotStartBundleReady = true,
                StopAndReportTriggers = ["blocked posture"],
                IsValid = true,
            },
            () => new RuntimeAgentProblemFollowUpPlanningGateSurface
            {
                OverallPosture = "agent_problem_follow_up_planning_gate_waiting_for_intent_draft",
                PlanningGateReady = true,
                AcceptedPlanningItemCount = 1,
                ReadyForPlanInitCount = 0,
                NextGovernedCommand = "carves intent draft --persist",
                IsValid = true,
            },
            () => new RuntimeProductClosurePilotStatusSurface
            {
                OverallPosture = "pilot_status_intent_capture_required",
                CurrentStageId = "intent_capture",
                CurrentStageOrder = 8,
                CurrentStageStatus = "ready",
                NextCommand = "carves pilot status --json",
                IsValid = true,
            },
            () => new RuntimeGovernedAgentHandoffProofSurface
            {
                OverallPosture = "bounded_governed_agent_handoff_proof_ready",
                WorkingModeRecommendationPosture = "mode_e_recommended_for_packet_bound_handoff",
                ProtectedTruthRootPosture = "protected_truth_root_policy_ready",
                AdapterContractPosture = "adapter_handoff_contract_ready",
                IsValid = true,
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-agent-thread-start", surface.SurfaceId);
        Assert.Equal("agent_thread_start_ready", surface.OverallPosture);
        Assert.True(surface.ThreadStartReady);
        Assert.Equal("carves agent start --json", surface.OneCommandForNewThread);
        Assert.Equal("carves intent draft --persist", surface.NextGovernedCommand);
        Assert.Equal("follow_up_planning_gate", surface.NextCommandSource);
        Assert.False(surface.DiscussionFirstSurface);
        Assert.False(surface.AutoRunAllowed);
        Assert.Null(surface.RecommendedActionId);
        Assert.True(surface.StartupBoundaryReady);
        Assert.Equal("runtime_repo_startup_boundary_ready", surface.StartupBoundaryPosture);
        Assert.Empty(surface.StartupBoundaryGaps);
        Assert.Empty(surface.AvailableActions);
        Assert.Empty(surface.ForbiddenAutoActions);
        Assert.True(surface.FollowUpGateReady);
        Assert.True(surface.GovernedAgentHandoffReady);
        Assert.Empty(surface.Gaps);
        Assert.Contains(surface.MinimalAgentRules, rule => rule.Contains("carves agent start --json", StringComparison.Ordinal));
        Assert.Contains(surface.MinimalAgentRules, rule => rule.Contains("project-local launcher", StringComparison.Ordinal));
        Assert.Contains(surface.MinimalAgentRules, rule => rule.Contains(".carves/carves gateway status", StringComparison.Ordinal));
        Assert.Contains(surface.TroubleshootingReadbacks, command => command == "carves pilot start --json");
        Assert.Contains(surface.TroubleshootingReadbacks, command => command == ".carves/carves gateway status");
        Assert.Contains(surface.TroubleshootingReadbacks, command => command == ".carves/carves status --watch --iterations 1 --interval-ms 0");
        Assert.Contains(surface.StopAndReportTriggers, trigger => trigger == "blocked posture");
    }


    [Fact]
    public void AgentThreadStart_ProjectsTargetStartupClassificationAndRuntimeBindingBoundary()
    {
        using var runtime = new TemporaryWorkspace();
        WritePilotStatusDocs(runtime);
        runtime.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# first run");
        using var target = new TemporaryWorkspace();
        target.WriteFile(".ai/runtime.json", JsonSerializer.Serialize(new { runtime_root = runtime.RootPath }));
        target.WriteFile(
            ".ai/runtime/attach-handshake.json",
            JsonSerializer.Serialize(new { request = new { runtime_root = runtime.RootPath } }));
        target.WriteFile(".carves/agent-start.json", "{}");
        target.WriteFile(".carves/AGENT_START.md", "# CARVES Agent Start");
        target.WriteFile("CARVES_START.md", "# Start CARVES");

        var surface = new RuntimeAgentThreadStartService(
            target.RootPath,
            () => new RuntimeExternalTargetPilotStartSurface
            {
                OverallPosture = "external_target_pilot_start_bundle_ready",
                PilotStartBundleReady = true,
                IsValid = true,
            },
            () => new RuntimeAgentProblemFollowUpPlanningGateSurface
            {
                OverallPosture = "agent_problem_follow_up_planning_gate_ready",
                PlanningGateReady = true,
                AcceptedPlanningItemCount = 0,
                NextGovernedCommand = "carves pilot follow-up-gate --json",
                IsValid = true,
            },
            () => new RuntimeProductClosurePilotStatusSurface
            {
                OverallPosture = "pilot_status_intent_capture_required",
                CurrentStageId = "intent_capture",
                CurrentStageOrder = 8,
                CurrentStageStatus = "ready",
                NextCommand = "carves discuss context",
                IsValid = true,
            },
            () => new RuntimeGovernedAgentHandoffProofSurface
            {
                OverallPosture = "bounded_governed_agent_handoff_proof_ready",
                IsValid = true,
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal(Path.GetFullPath(runtime.RootPath), surface.RuntimeDocumentRoot);
        Assert.Equal("attach_handshake_runtime_root", surface.RuntimeDocumentRootMode);
        Assert.Equal("target_project_agent_start", surface.StartupEntrySource);
        Assert.Equal("existing_carves_project", surface.TargetProjectClassification);
        Assert.Equal("carves_up", surface.TargetClassificationOwner);
        Assert.Equal(".carves/agent-start.json", surface.TargetClassificationSource);
        Assert.False(surface.AgentTargetClassificationAllowed);
        Assert.Equal("reuse_existing_runtime", surface.TargetStartupMode);
        Assert.Equal("use_existing_carves_project_agent_start_readback", surface.ExistingProjectHandling);
        Assert.True(surface.StartupBoundaryReady);
        Assert.Equal("target_startup_boundary_ready", surface.StartupBoundaryPosture);
        Assert.Empty(surface.StartupBoundaryGaps);
        Assert.Equal(Path.GetFullPath(runtime.RootPath), surface.TargetBoundRuntimeRoot);
        Assert.Equal("runtime_binding_matches_runtime_document_root", surface.TargetRuntimeBindingStatus);
        Assert.Equal("attach_handshake_and_runtime_manifest", surface.TargetRuntimeBindingSource);
        Assert.False(surface.AgentRuntimeRebindAllowed);
        Assert.Equal("null_worker_current_version_no_api_sdk_worker_execution", surface.WorkerExecutionBoundary);
        Assert.Contains(".ai/runtime.json", surface.RuntimeBindingRule, StringComparison.Ordinal);
        Assert.Contains(surface.MinimalAgentRules, rule => rule.Contains("target_project_classification", StringComparison.Ordinal));
        Assert.Contains(surface.MinimalAgentRules, rule => rule.Contains("target_runtime_binding_status", StringComparison.Ordinal));
        Assert.Contains(surface.MinimalAgentRules, rule => rule.Contains("target-bound `.carves/carves`", StringComparison.Ordinal));
        Assert.Contains(surface.MinimalAgentRules, rule => rule.Contains("target-local visibility readbacks", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("repair Runtime binding", StringComparison.Ordinal));
    }


    [Fact]
    public void AgentThreadStart_BlocksWhenTargetRuntimeBindingNeedsOperatorRebind()
    {
        using var runtime = new TemporaryWorkspace();
        WritePilotStatusDocs(runtime);
        runtime.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# first run");
        using var conflictingRuntime = new TemporaryWorkspace();
        using var target = new TemporaryWorkspace();
        target.WriteFile(".ai/runtime.json", JsonSerializer.Serialize(new { runtime_root = conflictingRuntime.RootPath }));
        target.WriteFile(
            ".ai/runtime/attach-handshake.json",
            JsonSerializer.Serialize(new { request = new { runtime_root = runtime.RootPath } }));
        target.WriteFile(".carves/agent-start.json", "{}");
        target.WriteFile(".carves/AGENT_START.md", "# CARVES Agent Start");
        target.WriteFile("CARVES_START.md", "# Start CARVES");

        var surface = new RuntimeAgentThreadStartService(
            target.RootPath,
            () => new RuntimeExternalTargetPilotStartSurface
            {
                OverallPosture = "external_target_pilot_start_bundle_ready",
                PilotStartBundleReady = true,
                IsValid = true,
            },
            () => new RuntimeAgentProblemFollowUpPlanningGateSurface
            {
                OverallPosture = "agent_problem_follow_up_planning_gate_ready",
                PlanningGateReady = true,
                AcceptedPlanningItemCount = 0,
                NextGovernedCommand = "carves pilot follow-up-gate --json",
                IsValid = true,
            },
            () => new RuntimeProductClosurePilotStatusSurface
            {
                OverallPosture = "pilot_status_product_pilot_proof_complete",
                CurrentStageId = "ready_for_new_intent",
                CurrentStageOrder = 26,
                CurrentStageStatus = "ready",
                NextCommand = "carves discuss context",
                IsValid = true,
            },
            () => new RuntimeGovernedAgentHandoffProofSurface
            {
                OverallPosture = "bounded_governed_agent_handoff_proof_ready",
                IsValid = true,
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.False(surface.ThreadStartReady);
        Assert.Equal("agent_thread_start_blocked", surface.OverallPosture);
        Assert.False(surface.StartupBoundaryReady);
        Assert.Equal("target_startup_blocked_by_runtime_binding", surface.StartupBoundaryPosture);
        Assert.Equal("blocked_rebind_required", surface.TargetStartupMode);
        Assert.Equal("operator_rebind_required_agent_must_stop", surface.ExistingProjectHandling);
        Assert.Equal("runtime_binding_internal_mismatch", surface.TargetRuntimeBindingStatus);
        Assert.Contains(surface.StartupBoundaryGaps, gap => gap == "runtime_binding_internal_mismatch");
        Assert.Contains(surface.Gaps, gap => gap == "startup_boundary:runtime_binding_internal_mismatch");
        Assert.Empty(surface.AvailableActions);
        Assert.Contains("Stop.", surface.RecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains("target startup boundary", surface.Summary, StringComparison.Ordinal);
    }


    [Fact]
    public void AgentThreadStart_FallsBackToPilotStatusWhenNoAcceptedFollowUpInput()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# first run");

        var surface = new RuntimeAgentThreadStartService(
            workspace.RootPath,
            () => new RuntimeExternalTargetPilotStartSurface
            {
                OverallPosture = "external_target_pilot_start_bundle_ready",
                PilotStartBundleReady = true,
                IsValid = true,
            },
            () => new RuntimeAgentProblemFollowUpPlanningGateSurface
            {
                OverallPosture = "agent_problem_follow_up_planning_gate_ready",
                PlanningGateReady = true,
                AcceptedPlanningItemCount = 0,
                NextGovernedCommand = "carves pilot follow-up-gate --json",
                IsValid = true,
            },
            () => new RuntimeProductClosurePilotStatusSurface
            {
                OverallPosture = "pilot_status_blocked_by_runtime_init",
                CurrentStageId = "attach_target",
                CurrentStageOrder = 1,
                CurrentStageStatus = "blocked",
                NextCommand = "carves init [target-path] --json",
                IsValid = true,
            },
            () => new RuntimeGovernedAgentHandoffProofSurface
            {
                OverallPosture = "bounded_governed_agent_handoff_proof_ready",
                IsValid = true,
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.True(surface.ThreadStartReady);
        Assert.Equal("carves init [target-path] --json", surface.NextGovernedCommand);
        Assert.Equal("pilot_status", surface.NextCommandSource);
        Assert.False(surface.DiscussionFirstSurface);
        Assert.False(surface.AutoRunAllowed);
        Assert.Null(surface.RecommendedActionId);
        Assert.Empty(surface.AvailableActions);
        Assert.Equal("attach_target", surface.CurrentStageId);
        Assert.Empty(surface.Gaps);
    }


    [Fact]
    public void AgentThreadStart_PrefersPilotStatusWhenPilotProofIsComplete()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# first run");

        var surface = new RuntimeAgentThreadStartService(
            workspace.RootPath,
            () => new RuntimeExternalTargetPilotStartSurface
            {
                OverallPosture = "external_target_pilot_start_bundle_ready",
                PilotStartBundleReady = true,
                IsValid = true,
            },
            () => new RuntimeAgentProblemFollowUpPlanningGateSurface
            {
                OverallPosture = "agent_problem_follow_up_planning_gate_waiting_for_intent_draft_candidate_projection",
                PlanningGateReady = true,
                AcceptedPlanningItemCount = 1,
                ReadyForPlanInitCount = 0,
                NextGovernedCommand = "carves intent draft --persist",
                IsValid = true,
            },
            () => new RuntimeProductClosurePilotStatusSurface
            {
                OverallPosture = "pilot_status_product_pilot_proof_complete",
                CurrentStageId = "ready_for_new_intent",
                CurrentStageOrder = 26,
                CurrentStageStatus = "ready",
                NextCommand = "carves discuss context",
                IsValid = true,
            },
            () => new RuntimeGovernedAgentHandoffProofSurface
            {
                OverallPosture = "bounded_governed_agent_handoff_proof_ready",
                IsValid = true,
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.True(surface.ThreadStartReady);
        Assert.Equal("carves discuss context", surface.NextGovernedCommand);
        Assert.Equal("pilot_status", surface.NextCommandSource);
        Assert.True(surface.LegacyNextCommandProjectionOnly);
        Assert.True(surface.LegacyNextCommandDoNotAutoRun);
        Assert.Equal("available_actions", surface.PreferredActionSource);
        Assert.True(surface.DiscussionFirstSurface);
        Assert.False(surface.AutoRunAllowed);
        Assert.Null(surface.RecommendedActionId);
        Assert.Equal(4, surface.AvailableActions.Count);
        Assert.All(surface.AvailableActions, action => Assert.Contains(action.Kind, new[] { "read_only", "preview" }));
        Assert.Contains(surface.AvailableActions, action => action.ActionId == "continue_discussion" && action.Command == "carves discuss context");
        Assert.Contains(surface.AvailableActions, action => action.ActionId == "project_brief_preview"
            && action.Kind == "preview"
            && action.Command == "carves discuss brief-preview");
        Assert.Contains(surface.ForbiddenAutoActions, action => action == "carves intent draft --persist");
        Assert.Equal("ready_for_new_intent", surface.CurrentStageId);
        Assert.Equal(1, surface.AcceptedPlanningItemCount);
        Assert.Equal("carves intent draft --persist", surface.FollowUpGateNextCommand);
        Assert.Empty(surface.Gaps);
        Assert.Contains("normal chat", surface.RecommendedNextAction, StringComparison.OrdinalIgnoreCase);
    }

}
