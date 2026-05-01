using System.Text.Json;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.Git;
using Carves.Runtime.Infrastructure.Persistence;
using Carves.Runtime.Domain.Planning;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeWorkingModesAndFormalPlanningPostureServiceTests
{
    [Fact]
    public void AgentWorkingModesSurface_ProjectsCurrentModeAndPlanningCoupling()
    {
        using var fixture = RuntimeWorkingModesFixture.Create();

        var surface = fixture.AgentWorkingModesService.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-agent-working-modes", surface.SurfaceId);
        Assert.Equal("runtime_agent_working_modes_ready", surface.OverallPosture);
        Assert.Equal("mode_d_scoped_task_workspace", surface.CurrentMode);
        Assert.Equal("mode_e_brokered_execution", surface.StrongestRuntimeSupportedMode);
        Assert.Equal("mode_e_brokered_execution", surface.ExternalAgentRecommendedMode);
        Assert.Equal("mode_e_recommended_for_packet_bound_handoff", surface.ExternalAgentRecommendationPosture);
        Assert.Equal("hard_runtime_gate", surface.ExternalAgentConstraintTier);
        Assert.Contains("packet/result mediation", surface.ExternalAgentConstraintSummary, StringComparison.Ordinal);
        Assert.Equal(0, surface.ExternalAgentStrongerModeBlockerCount);
        Assert.Null(surface.ExternalAgentFirstStrongerModeBlockerId);
        Assert.Empty(surface.ExternalAgentStrongerModeBlockers);
        Assert.Contains(surface.ConstraintClasses, item =>
            item.ClassId == "soft_advisory"
            && item.EnforcementLevel == "read_side_guidance_only"
            && item.AppliesToModes.Contains("mode_a_open_repo_advisory"));
        Assert.Contains(surface.ConstraintClasses, item =>
            item.ClassId == "hard_runtime_gate"
            && item.EnforcementLevel == "portable_runtime_enforced"
            && item.AppliesToModes.Contains("mode_e_brokered_execution"));
        Assert.Equal("p2_task_bound_guidance", surface.PlanningCouplingPosture);
        Assert.Equal("task_bound_workspace_active", surface.ManagedWorkspacePosture);
        Assert.Equal("active", surface.PathPolicyEnforcementState);
        Assert.Equal(fixture.PlanHandle, surface.PlanHandle);
        Assert.Equal("task_scoped_activation_available", surface.ModeEOperationalActivationState);
        Assert.Equal(fixture.TaskId, surface.ModeEActivationTaskId);
        Assert.Equal($".ai/execution/{fixture.TaskId}/result.json", surface.ModeEActivationResultReturnChannel);
        Assert.Contains($"inspect runtime-brokered-execution {fixture.TaskId}", surface.ModeEActivationCommands);
        Assert.Contains($"inspect runtime-brokered-execution {fixture.TaskId}", surface.ModeEActivationRecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains($".ai/execution/{fixture.TaskId}/result.json", surface.ModeEActivationRecommendedNextAction, StringComparison.Ordinal);
        Assert.Equal(0, surface.ModeEActivationBlockingCheckCount);
        Assert.Null(surface.ModeEActivationFirstBlockingCheckId);
        Assert.Equal(4, surface.ModeEActivationPlaybookStepCount);
        Assert.Equal($"inspect runtime-brokered-execution {fixture.TaskId}", surface.ModeEActivationFirstPlaybookStepCommand);
        Assert.Contains("task-scoped", surface.ModeEActivationPlaybookSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(surface.ModeEActivationPlaybookSteps, item =>
            item.StepId == "inspect_packet_enforcement"
            && item.Command == $"inspect packet-enforcement {fixture.TaskId}"
            && item.AppliesWhen == "task_scoped_activation_available");
        Assert.Contains(surface.ModeEActivationChecks, item => item.CheckId == "formal_planning_packet_available" && item.State == "satisfied");
        Assert.Contains(surface.ModeEActivationChecks, item => item.CheckId == "bound_task_truth_available" && item.State == "satisfied");
        Assert.Contains("runtime-brokered-execution", surface.RecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains(surface.SupportedModes, item => item.ModeId == "mode_c_task_bound_workspace" && item.RuntimeStatus == "implemented");
        Assert.Contains(surface.SupportedModes, item => item.ModeId == "mode_d_scoped_task_workspace" && item.RuntimeStatus == "hardening_active");
        Assert.Contains(surface.SupportedModes, item => item.ModeId == "mode_e_brokered_execution" && item.RuntimeStatus == "implemented_task_scoped");
    }

    [Fact]
    public void AgentWorkingModesSurface_RecommendsAdvisoryModeAndExplainsHardBlockersWithoutFormalPlanning()
    {
        using var fixture = RuntimeWorkingModesFixture.CreateFormalPlanningEntryScenario(prepareReadyToPlan: false);

        var surface = fixture.AgentWorkingModesService.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("mode_a_open_repo_advisory", surface.CurrentMode);
        Assert.Equal("mode_a_open_repo_advisory", surface.ExternalAgentRecommendedMode);
        Assert.Equal("advisory_until_formal_planning", surface.ExternalAgentRecommendationPosture);
        Assert.Equal("soft_advisory", surface.ExternalAgentConstraintTier);
        Assert.Contains("cannot prevent direct file edits", surface.ExternalAgentConstraintSummary, StringComparison.OrdinalIgnoreCase);
        Assert.True(surface.ExternalAgentStrongerModeBlockerCount >= 3);
        Assert.Equal("formal_planning_packet_available", surface.ExternalAgentFirstStrongerModeBlockerId);
        Assert.Equal("mode_c_task_bound_workspace", surface.ExternalAgentFirstStrongerModeBlockerTargetMode);
        Assert.Equal("hard_runtime_prerequisite", surface.ExternalAgentFirstStrongerModeBlockerConstraintClass);
        Assert.Equal("formal_planning_gate", surface.ExternalAgentFirstStrongerModeBlockerEnforcementLevel);
        Assert.Contains("plan init [candidate-card-id]", surface.ExternalAgentFirstStrongerModeBlockerRequiredAction, StringComparison.Ordinal);
        Assert.Contains(surface.ExternalAgentStrongerModeBlockers, item =>
            item.BlockerId == "managed_workspace_lease_available"
            && item.TargetModeId == "mode_d_scoped_task_workspace"
            && item.ConstraintClass == "hard_runtime_prerequisite"
            && item.RequiredCommand == "plan issue-workspace <task-id>");
        Assert.Contains(surface.ExternalAgentStrongerModeBlockers, item =>
            item.BlockerId == "mode_e_task_scoped_activation_available"
            && item.TargetModeId == "mode_e_brokered_execution"
            && item.EnforcementLevel == "host_mediated_packet_result_gate");
        Assert.Contains(surface.ConstraintClasses, item =>
            item.ClassId == "vendor_native_optional"
            && item.AppliesToModes.Contains("mode_b_open_repo_with_vendor_native_guard"));
    }

    [Fact]
    public void FormalPlanningPostureSurface_ProjectsExecutionBoundHandleWorkspaceAndMissingPrerequisites()
    {
        using var fixture = RuntimeWorkingModesFixture.Create();

        var surface = fixture.FormalPlanningPostureService.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-formal-planning-posture", surface.SurfaceId);
        Assert.Equal("execution_bound_with_active_workspace", surface.OverallPosture);
        Assert.Equal("drafted", surface.IntentState);
        Assert.Equal("ready_to_plan", surface.GuidedPlanningPosture);
        Assert.Equal("execution_bound", surface.FormalPlanningState);
        Assert.Equal("formal_planning_packet_available", surface.FormalPlanningEntryTriggerState);
        Assert.Equal("plan status", surface.FormalPlanningEntryCommand);
        Assert.Contains("plan status", surface.FormalPlanningEntryRecommendedNextAction, StringComparison.Ordinal);
        Assert.Equal("occupied_by_packet", surface.ActivePlanningSlotState);
        Assert.False(surface.ActivePlanningSlotCanInitialize);
        Assert.Contains("opening another active planning card is rejected", surface.ActivePlanningSlotConflictReason, StringComparison.Ordinal);
        Assert.Contains("plan status", surface.ActivePlanningSlotRemediationAction, StringComparison.Ordinal);
        Assert.Equal(PlanningCardInvariantService.ValidState, surface.PlanningCardInvariantState);
        Assert.True(surface.PlanningCardInvariantCanExportGovernedTruth);
        Assert.Equal(5, surface.PlanningCardInvariantBlockCount);
        Assert.Equal(0, surface.PlanningCardInvariantViolationCount);
        Assert.Equal(PlanningCardFillGuidanceService.ReadyToExportState, surface.ActivePlanningCardFillState);
        Assert.True(surface.ActivePlanningCardFillReadyForRecommendedExport);
        Assert.Equal(0, surface.ActivePlanningCardFillMissingRequiredFieldCount);
        Assert.Contains("plan export-card", surface.ActivePlanningCardFillRecommendedNextAction, StringComparison.Ordinal);
        Assert.Equal("mode_d_scoped_task_workspace", surface.CurrentMode);
        Assert.Equal("p2_task_bound_guidance", surface.PlanningCouplingPosture);
        Assert.Equal(fixture.PlanHandle, surface.PlanHandle);
        Assert.Equal(fixture.TaskId, Assert.Single(surface.ActiveLeaseTaskIds));
        Assert.Equal("task_bound_workspace_active", surface.ManagedWorkspacePosture);
        Assert.Equal("active", surface.PathPolicyEnforcementState);
        Assert.True(surface.PacketAvailable);
        Assert.Equal(0, surface.PlanRequiredBlockCount);
        Assert.Equal(0, surface.WorkspaceRequiredBlockCount);
        Assert.Null(surface.ModeExecutionEntryFirstBlockingCheckId);
        Assert.Null(surface.ModeExecutionEntryRecommendedNextCommand);
        Assert.Empty(surface.MissingPrerequisites);
        Assert.Contains("bound to 1 task", surface.PacketSummary!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormalPlanningPostureSurface_ProjectsDiscussionOnlyAndPlanInitRequiredEntryTriggers()
    {
        using var discussionOnly = RuntimeWorkingModesFixture.CreateFormalPlanningEntryScenario(prepareReadyToPlan: false);
        using var planInitRequired = RuntimeWorkingModesFixture.CreateFormalPlanningEntryScenario(prepareReadyToPlan: true);

        var discussionSurface = discussionOnly.FormalPlanningPostureService.Build();
        var planInitSurface = planInitRequired.FormalPlanningPostureService.Build();

        Assert.Equal("discussion_only", discussionSurface.OverallPosture);
        Assert.Equal("discuss", discussionSurface.FormalPlanningState);
        Assert.Equal("discussion_only", discussionSurface.FormalPlanningEntryTriggerState);
        Assert.Equal("plan init [candidate-card-id]", discussionSurface.FormalPlanningEntryCommand);
        Assert.Contains("plan init [candidate-card-id]", discussionSurface.FormalPlanningEntryRecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains("discussion-only", discussionSurface.FormalPlanningEntrySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no_intent_draft", discussionSurface.ActivePlanningSlotState);
        Assert.False(discussionSurface.ActivePlanningSlotCanInitialize);
        Assert.Contains("intent draft", discussionSurface.ActivePlanningSlotRemediationAction, StringComparison.Ordinal);
        Assert.Equal(PlanningCardInvariantService.NoActivePlanningCardState, discussionSurface.PlanningCardInvariantState);
        Assert.False(discussionSurface.PlanningCardInvariantCanExportGovernedTruth);
        Assert.Equal(PlanningCardFillGuidanceService.NoActivePlanningCardState, discussionSurface.ActivePlanningCardFillState);
        Assert.Contains("plan init", discussionSurface.ActivePlanningCardFillRecommendedNextAction, StringComparison.Ordinal);

        Assert.Equal("plan_init_required", planInitSurface.OverallPosture);
        Assert.Equal("plan_init_required", planInitSurface.FormalPlanningState);
        Assert.Equal("plan_init_required", planInitSurface.FormalPlanningEntryTriggerState);
        Assert.Equal("plan init [candidate-card-id]", planInitSurface.FormalPlanningEntryCommand);
        Assert.Contains("plan init [candidate-card-id]", planInitSurface.FormalPlanningEntryRecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains("no active planning card", planInitSurface.FormalPlanningEntrySummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("empty_ready_to_initialize", planInitSurface.ActivePlanningSlotState);
        Assert.True(planInitSurface.ActivePlanningSlotCanInitialize);
        Assert.Empty(planInitSurface.ActivePlanningSlotConflictReason);
        Assert.Contains("single active formal planning slot", planInitSurface.ActivePlanningSlotRemediationAction, StringComparison.Ordinal);
        Assert.Equal(PlanningCardInvariantService.NoActivePlanningCardState, planInitSurface.PlanningCardInvariantState);
        Assert.False(planInitSurface.PlanningCardInvariantCanExportGovernedTruth);
        Assert.Equal(PlanningCardFillGuidanceService.NoActivePlanningCardState, planInitSurface.ActivePlanningCardFillState);
        Assert.Contains("plan init", planInitSurface.ActivePlanningCardFillRecommendedNextAction, StringComparison.Ordinal);
    }

    [Fact]
    public void ActivePlanningSlotProjection_TreatsClosedPacketAsHistorical()
    {
        var activePlanningCard = new ActivePlanningCard
        {
            PlanningCardId = "PLANCARD-CLOSED",
            PlanningSlotId = ActivePlanningSlotProjectionResolver.PrimaryFormalPlanningSlotId,
            SourceIntentDraftId = "intent-draft-closed",
            State = FormalPlanningState.Planning,
        };
        var status = new IntentDiscoveryStatus(
            IntentDiscoveryState.Drafted,
            string.Empty,
            false,
            string.Empty,
            new IntentDiscoveryDraft
            {
                DraftId = "intent-draft-closed",
                ActivePlanningCard = activePlanningCard,
                FormalPlanningState = FormalPlanningState.Planning,
            },
            true,
            string.Empty,
            string.Empty);
        var packet = new FormalPlanningPacket
        {
            PlanningSlotId = activePlanningCard.PlanningSlotId,
            PlanningCardId = activePlanningCard.PlanningCardId,
            PlanHandle = FormalPlanningPacketService.BuildPlanHandle(activePlanningCard),
            FormalPlanningState = FormalPlanningState.Closed,
        };

        var slot = ActivePlanningSlotProjectionResolver.Resolve(status, packet);
        var entry = RuntimeFormalPlanningEntryProjectionResolver.Resolve(FormalPlanningState.Closed, status, packet);

        Assert.Equal("closed_historical", slot.State);
        Assert.True(slot.CanInitializeFormalPlanning);
        Assert.Empty(slot.ConflictReason);
        Assert.Contains("plan init [candidate-card-id]", slot.RemediationAction, StringComparison.Ordinal);
        Assert.Equal("closed_historical", entry.TriggerState);
        Assert.Equal("plan init [candidate-card-id]", entry.Command);
    }

    [Fact]
    public void VendorNativeAccelerationSurface_ProjectsOptionalCodexAndClaudeReinforcement()
    {
        using var fixture = RuntimeWorkingModesFixture.Create();

        var surface = fixture.VendorNativeAccelerationService.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-vendor-native-acceleration", surface.SurfaceId);
        Assert.Equal("optional_vendor_native_acceleration_ready", surface.OverallPosture);
        Assert.Equal("mode_d_scoped_task_workspace", surface.CurrentMode);
        Assert.Equal("p2_task_bound_guidance", surface.PlanningCouplingPosture);
        Assert.Equal("execution_bound_with_active_workspace", surface.FormalPlanningPosture);
        Assert.Equal("task_bound_workspace_active", surface.ManagedWorkspacePosture);
        Assert.Equal("repo_guard_assets_ready", surface.CodexReinforcementState);
        Assert.Equal("bounded_runtime_qualification_ready", surface.ClaudeReinforcementState);
        Assert.Contains(".codex/config.toml", surface.CodexGovernanceAssets);
        Assert.Contains("review_summary", surface.ClaudeQualifiedRoutingIntents);
        Assert.Contains("patch_draft", surface.ClaudeClosedRoutingIntents);
    }

    private sealed class RuntimeWorkingModesFixture : IDisposable
    {
        private RuntimeWorkingModesFixture(
            TemporaryWorkspace workspace,
            RuntimeAgentWorkingModesService agentWorkingModesService,
            RuntimeFormalPlanningPostureService formalPlanningPostureService,
            RuntimeVendorNativeAccelerationService vendorNativeAccelerationService,
            string taskId,
            string planHandle,
            string leaseRoot)
        {
            Workspace = workspace;
            AgentWorkingModesService = agentWorkingModesService;
            FormalPlanningPostureService = formalPlanningPostureService;
            VendorNativeAccelerationService = vendorNativeAccelerationService;
            TaskId = taskId;
            PlanHandle = planHandle;
            LeaseRoot = leaseRoot;
        }

        public TemporaryWorkspace Workspace { get; }

        public RuntimeAgentWorkingModesService AgentWorkingModesService { get; }

        public RuntimeFormalPlanningPostureService FormalPlanningPostureService { get; }

        public RuntimeVendorNativeAccelerationService VendorNativeAccelerationService { get; }

        public string TaskId { get; }

        public string PlanHandle { get; }

        public string LeaseRoot { get; }

        public static RuntimeWorkingModesFixture Create()
        {
            var workspace = new TemporaryWorkspace();
            var leaseRootRelative = $"../phase7-workspaces-{Guid.NewGuid():N}";
            var leaseRoot = Path.GetFullPath(Path.Combine(workspace.RootPath, leaseRootRelative));
            workspace.WriteFile("README.md", "# Sample Repo");
            workspace.WriteFile("docs/runtime/runtime-managed-workspace-file-operation-model.md", "# managed workspace file operations");
            workspace.WriteFile("docs/runtime/runtime-agent-working-modes-and-constraint-ladder.md", "# working modes");
            workspace.WriteFile("docs/runtime/runtime-agent-working-modes-implementation-plan.md", "# implementation plan");
            workspace.WriteFile("docs/runtime/runtime-mode-d-scoped-task-workspace-hardening.md", "# mode d hardening");
            workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# mode e brokered execution");
            workspace.WriteFile("docs/runtime/runtime-constraint-ladder-and-collaboration-plane.md", "# collaboration plane");
            workspace.WriteFile("docs/runtime/runtime-cli-first-architecture.md", "# cli first");
            workspace.WriteFile("docs/runtime/runtime-plan-mode-and-active-planning-card.md", "# plan mode");
            workspace.WriteFile("docs/runtime/runtime-planning-packet-and-replan-rules.md", "# planning packet");
            workspace.WriteFile("docs/runtime/runtime-plan-required-and-workspace-required-gates.md", "# planning gates");
            workspace.WriteFile("docs/runtime/runtime-managed-workspace-lease.md", "# managed workspace lease");
            workspace.WriteFile("docs/runtime/runtime-collaboration-and-surface-projection.md", "# collaboration and surface projection");
            workspace.WriteFile("docs/runtime/runtime-vendor-native-acceleration.md", "# vendor native acceleration");
            workspace.WriteFile("docs/runtime/codex-native-governance-layer.md", "# codex native governance");
            workspace.WriteFile("docs/runtime/claude-worker-qualification.md", "# claude worker qualification");
            workspace.WriteFile(".codex/config.toml", "model = \"gpt-5\"\n");
            workspace.WriteFile(".codex/rules/carves-control-plane.md", "# control plane rules");
            workspace.WriteFile(".codex/rules/carves-execution-boundary.md", "# execution boundary rules");
            workspace.WriteFile(".codex/skills/carves-runtime/SKILL.md", "# carves runtime skill");
            workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");

            var paths = workspace.Paths;
            var systemConfig = new Carves.Runtime.Application.Configuration.SystemConfig(
                "TestRepo",
                leaseRootRelative,
                1,
                ["dotnet", "test", "CARVES.Runtime.sln"],
                ["src", "tests"],
                [".git", "bin", "obj", "TestResults"],
                true,
                true);
            var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
            var query = new FileCodeGraphQueryService(paths, builder);
            var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
            var intentDiscoveryService = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);
            var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
            var planningDraftService = new PlanningDraftService(paths, taskGraphService, new JsonCardDraftRepository(paths), new JsonTaskGraphDraftRepository(paths));

            intentDiscoveryService.GenerateDraft();
            intentDiscoveryService.SetFocusCard("candidate-first-slice");
            intentDiscoveryService.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
            intentDiscoveryService.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
            intentDiscoveryService.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
            intentDiscoveryService.InitializeFormalPlanning();

            var exportCardPath = Path.Combine(workspace.RootPath, "drafts", "plan-card.json");
            intentDiscoveryService.ExportActivePlanningCardPayload(exportCardPath);
            var card = planningDraftService.CreateCardDraft(exportCardPath);
            planningDraftService.SetCardStatus(card.CardId, CardLifecycleState.Approved, "approved for phase-7 surfaces");
            var taskId = $"T-{card.CardId}-001";
            var taskGraphPayloadPath = Path.Combine(workspace.RootPath, "drafts", "taskgraph-draft.json");
            File.WriteAllText(
                taskGraphPayloadPath,
                JsonSerializer.Serialize(
                    new
                    {
                        card_id = card.CardId,
                        tasks = new object[]
                        {
                            new
                            {
                                task_id = taskId,
                                title = "Project phase-7 surfaces",
                                description = "Issue a plan-bound managed workspace and project working-mode posture.",
                                scope = new[] { "src/Sample.cs" },
                                acceptance = new[] { "working-mode surface exists" },
                                proof_target = new
                                {
                                    kind = "focused_behavior",
                                    description = "Prove Phase 7 working-mode and planning posture surfaces project active plan and workspace truth.",
                                },
                            },
                        },
                    },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        WriteIndented = true,
                    }));
            var taskGraphDraft = planningDraftService.CreateTaskGraphDraft(taskGraphPayloadPath);
            planningDraftService.ApproveTaskGraphDraft(taskGraphDraft.DraftId, "approved for phase-7 surfaces");

            var formalPlanningPacketService = new FormalPlanningPacketService(intentDiscoveryService, planningDraftService, taskGraphService);
            var gitClient = new StubGitClient();
            var worktreeRuntimeService = new WorktreeRuntimeService(workspace.RootPath, gitClient, new InMemoryWorktreeRuntimeRepository());
            var managedWorkspaceLeaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
            var managedWorkspaceLeaseService = new ManagedWorkspaceLeaseService(
                workspace.RootPath,
                systemConfig,
                formalPlanningPacketService,
                taskGraphService,
                gitClient,
                new WorktreeManager(gitClient),
                worktreeRuntimeService,
                managedWorkspaceLeaseRepository);
            managedWorkspaceLeaseService.IssueForTask(taskId);

            var formalPlanningExecutionGateService = new FormalPlanningExecutionGateService(intentDiscoveryService, managedWorkspaceLeaseRepository);
            var dispatchProjectionService = new DispatchProjectionService(formalPlanningExecutionGateService: formalPlanningExecutionGateService);

            var agentWorkingModesService = new RuntimeAgentWorkingModesService(
                workspace.RootPath,
                intentDiscoveryService,
                formalPlanningPacketService,
                managedWorkspaceLeaseService);
            var formalPlanningPostureService = new RuntimeFormalPlanningPostureService(
                workspace.RootPath,
                intentDiscoveryService,
                formalPlanningPacketService,
                managedWorkspaceLeaseService,
                taskGraphService,
                dispatchProjectionService,
                () => null,
                1);
            var vendorNativeAccelerationService = new RuntimeVendorNativeAccelerationService(
                workspace.RootPath,
                agentWorkingModesService.Build,
                formalPlanningPostureService.Build);

            var planHandle = formalPlanningPacketService.BuildCurrentPacket().PlanHandle;
            return new RuntimeWorkingModesFixture(workspace, agentWorkingModesService, formalPlanningPostureService, vendorNativeAccelerationService, taskId, planHandle, leaseRoot);
        }

        public static RuntimeWorkingModesFixture CreateFormalPlanningEntryScenario(bool prepareReadyToPlan)
        {
            var workspace = new TemporaryWorkspace();
            var leaseRootRelative = $"../phase7-entry-workspaces-{Guid.NewGuid():N}";
            var leaseRoot = Path.GetFullPath(Path.Combine(workspace.RootPath, leaseRootRelative));
            workspace.WriteFile("README.md", "# Sample Repo");
            workspace.WriteFile("docs/runtime/runtime-managed-workspace-file-operation-model.md", "# managed workspace file operations");
            workspace.WriteFile("docs/runtime/runtime-agent-working-modes-and-constraint-ladder.md", "# working modes");
            workspace.WriteFile("docs/runtime/runtime-agent-working-modes-implementation-plan.md", "# implementation plan");
            workspace.WriteFile("docs/runtime/runtime-mode-d-scoped-task-workspace-hardening.md", "# mode d hardening");
            workspace.WriteFile("docs/runtime/runtime-mode-e-brokered-execution.md", "# mode e brokered execution");
            workspace.WriteFile("docs/runtime/runtime-constraint-ladder-and-collaboration-plane.md", "# collaboration plane");
            workspace.WriteFile("docs/runtime/runtime-cli-first-architecture.md", "# cli first");
            workspace.WriteFile("docs/runtime/runtime-plan-mode-and-active-planning-card.md", "# plan mode");
            workspace.WriteFile("docs/runtime/runtime-planning-packet-and-replan-rules.md", "# planning packet");
            workspace.WriteFile("docs/runtime/runtime-plan-required-and-workspace-required-gates.md", "# planning gates");
            workspace.WriteFile("docs/runtime/runtime-managed-workspace-lease.md", "# managed workspace lease");
            workspace.WriteFile("docs/runtime/runtime-collaboration-and-surface-projection.md", "# collaboration and surface projection");
            workspace.WriteFile("docs/runtime/runtime-vendor-native-acceleration.md", "# vendor native acceleration");
            workspace.WriteFile("docs/runtime/codex-native-governance-layer.md", "# codex native governance");
            workspace.WriteFile("docs/runtime/claude-worker-qualification.md", "# claude worker qualification");
            workspace.WriteFile(".codex/config.toml", "model = \"gpt-5\"\n");
            workspace.WriteFile(".codex/rules/carves-control-plane.md", "# control plane rules");
            workspace.WriteFile(".codex/rules/carves-execution-boundary.md", "# execution boundary rules");
            workspace.WriteFile(".codex/skills/carves-runtime/SKILL.md", "# carves runtime skill");
            workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");

            var paths = workspace.Paths;
            var systemConfig = new Carves.Runtime.Application.Configuration.SystemConfig(
                "TestRepo",
                leaseRootRelative,
                1,
                ["dotnet", "test", "CARVES.Runtime.sln"],
                ["src", "tests"],
                [".git", "bin", "obj", "TestResults"],
                true,
                true);
            var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
            var query = new FileCodeGraphQueryService(paths, builder);
            var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
            var intentDiscoveryService = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);
            var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
            var planningDraftService = new PlanningDraftService(paths, taskGraphService, new JsonCardDraftRepository(paths), new JsonTaskGraphDraftRepository(paths));

            if (prepareReadyToPlan)
            {
                intentDiscoveryService.GenerateDraft();
                intentDiscoveryService.SetFocusCard("candidate-first-slice");
                intentDiscoveryService.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
                intentDiscoveryService.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
                intentDiscoveryService.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
            }

            var formalPlanningPacketService = new FormalPlanningPacketService(intentDiscoveryService, planningDraftService, taskGraphService);
            var gitClient = new StubGitClient();
            var worktreeRuntimeService = new WorktreeRuntimeService(workspace.RootPath, gitClient, new InMemoryWorktreeRuntimeRepository());
            var managedWorkspaceLeaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
            var managedWorkspaceLeaseService = new ManagedWorkspaceLeaseService(
                workspace.RootPath,
                systemConfig,
                formalPlanningPacketService,
                taskGraphService,
                gitClient,
                new WorktreeManager(gitClient),
                worktreeRuntimeService,
                managedWorkspaceLeaseRepository);
            var formalPlanningExecutionGateService = new FormalPlanningExecutionGateService(intentDiscoveryService, managedWorkspaceLeaseRepository);
            var dispatchProjectionService = new DispatchProjectionService(formalPlanningExecutionGateService: formalPlanningExecutionGateService);

            var agentWorkingModesService = new RuntimeAgentWorkingModesService(
                workspace.RootPath,
                intentDiscoveryService,
                formalPlanningPacketService,
                managedWorkspaceLeaseService);
            var formalPlanningPostureService = new RuntimeFormalPlanningPostureService(
                workspace.RootPath,
                intentDiscoveryService,
                formalPlanningPacketService,
                managedWorkspaceLeaseService,
                taskGraphService,
                dispatchProjectionService,
                () => null,
                1);
            var vendorNativeAccelerationService = new RuntimeVendorNativeAccelerationService(
                workspace.RootPath,
                agentWorkingModesService.Build,
                formalPlanningPostureService.Build);

            return new RuntimeWorkingModesFixture(workspace, agentWorkingModesService, formalPlanningPostureService, vendorNativeAccelerationService, string.Empty, string.Empty, leaseRoot);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(LeaseRoot))
                {
                    Directory.Delete(LeaseRoot, recursive: true);
                }
            }
            catch
            {
            }

            Workspace.Dispose();
        }
    }
}
