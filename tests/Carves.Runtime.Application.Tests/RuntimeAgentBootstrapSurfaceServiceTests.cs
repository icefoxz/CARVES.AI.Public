using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Safety;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeAgentBootstrapSurfaceServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [Fact]
    public void BootstrapPacket_BuildsCompactStartupPacketFromRepoAndHostPosture()
    {
        using var workspace = new TemporaryWorkspace();
        WriteSession(
            workspace,
            new RuntimeSessionState
            {
                SessionId = "default",
                AttachedRepoRoot = workspace.RootPath,
                Status = RuntimeSessionStatus.Paused,
                LoopMode = RuntimeLoopMode.ManualTick,
                PlannerLifecycleState = PlannerLifecycleState.Sleeping,
                CurrentTaskId = "T-BOOT-001",
            });
        workspace.WriteFile(
            ".carves-platform/runtime-state/host_snapshot.json",
            """
            {
              "state": "live",
              "session_status": "Running",
              "host_control_state": "running",
              "recorded_at": "2026-04-02T00:00:00Z"
            }
            """);
        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph(
            [
                new TaskNode
                {
                    TaskId = "T-BOOT-001",
                    CardId = "CARD-BOOT",
                    Title = "Bootstrap hot path",
                    Description = "Project compact startup context.",
                    Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                    Priority = "P1",
                    TaskType = TaskType.Execution,
                },
            ])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());

        var service = new RuntimeAgentBootstrapPacketService(workspace.RootPath, workspace.Paths, taskGraphService);

        var surface = service.Build();

        Assert.Equal("runtime-agent-bootstrap-packet", surface.SurfaceId);
        Assert.Equal("bootstrap_packet_default", surface.Packet.StartupMode);
        Assert.Equal("dual_report", surface.Packet.PostureBasis);
        Assert.Equal("paused", surface.Packet.RepoPosture.SessionStatus);
        Assert.Equal("manual_tick", surface.Packet.RepoPosture.LoopMode);
        Assert.Equal("worker", surface.Packet.RepoPosture.CurrentActionability);
        Assert.Equal("Live/running", surface.Packet.HostSnapshot.State);
        Assert.Contains(".ai/tasks/nodes/T-BOOT-001.json", surface.Packet.CurrentCardMemoryRefs);
        Assert.Contains("inspect runtime-agent-bootstrap-packet", surface.Packet.StartupInspectCommands);
        Assert.Contains("inspect runtime-agent-bootstrap-receipt [<receipt-json-path>]", surface.Packet.WarmResumeInspectCommands);
        Assert.Equal("bootstrap_packet_then_task_overlay", surface.Packet.HotPathContext.RecommendedStartupRoute);
        Assert.Equal("compact_context_does_not_replace_initialization_report", surface.Packet.HotPathContext.GovernanceBoundary);
        Assert.Contains("inspect runtime-agent-queue-projection", surface.Packet.HotPathContext.DefaultInspectCommands);
        Assert.Contains("inspect runtime-agent-queue-projection", surface.Packet.HotPathContext.BoundedNextCommands);
        Assert.Contains("inspect runtime-agent-task-overlay T-BOOT-001", surface.Packet.HotPathContext.TaskOverlayCommands);
        Assert.Contains("inspect execution-packet T-BOOT-001", surface.Packet.HotPathContext.BoundedNextCommands);
        Assert.Contains("task run T-BOOT-001", surface.Packet.HotPathContext.BoundedNextCommands);
        Assert.Contains(surface.Packet.HotPathContext.FullGovernanceReadTriggers, item => item.Contains("cleanup/commit hygiene", StringComparison.Ordinal));
        Assert.Contains(surface.Packet.HotPathContext.ActiveTasks, item => item.TaskId == "T-BOOT-001" && item.Title == "Bootstrap hot path");
        Assert.Equal("machine_surface_first", surface.Packet.HotPathContext.MarkdownReadPolicy.DefaultPostInitializationMode);
        Assert.Equal("classification_reduces_read_frequency_only", surface.Packet.HotPathContext.MarkdownReadPolicy.GovernanceBoundary);
        Assert.Contains("AGENTS.md", surface.Packet.HotPathContext.MarkdownReadPolicy.RequiredInitialSources);
        Assert.Contains("docs/guides/AGENT_INITIALIZATION_REPORT_SPEC.md", surface.Packet.HotPathContext.MarkdownReadPolicy.RequiredInitialSources);
        Assert.Contains("AGENTS.md", surface.Packet.HotPathContext.MarkdownReadPolicy.NeverReplacedSources);
        Assert.Contains("inspect runtime-agent-bootstrap-packet", surface.Packet.HotPathContext.MarkdownReadPolicy.PostInitializationHotPathSurfaces);
        Assert.Contains("inspect runtime-agent-queue-projection", surface.Packet.HotPathContext.MarkdownReadPolicy.PostInitializationHotPathSurfaces);
        Assert.DoesNotContain("inspect runtime-beta-program-status", surface.Packet.HotPathContext.DefaultInspectCommands);
        Assert.DoesNotContain("inspect runtime-pack-policy-audit", surface.Packet.HotPathContext.DefaultInspectCommands);
        Assert.DoesNotContain("api runtime-worker-execution-audit [<query>]", surface.Packet.HotPathContext.BoundedNextCommands);
        Assert.DoesNotContain(surface.Packet.HotPathContext.MarkdownReadPolicy.PostInitializationHotPathSurfaces, command => command.Contains("runtime-pack-policy-audit", StringComparison.Ordinal));
        Assert.Contains(surface.Packet.HotPathContext.MarkdownReadPolicy.ReadTiers, tier => tier.TierId == "cold_init_mandatory");
        Assert.Contains(surface.Packet.HotPathContext.MarkdownReadPolicy.ReadTiers, tier => tier.TierId == "daily_hot_path" && tier.DefaultAction == "prefer_machine_surfaces");
        Assert.Contains(surface.Packet.HotPathContext.MarkdownReadPolicy.EscalationTriggers, item => item.Contains("mixed diff judgment", StringComparison.Ordinal));
    }

    [Fact]
    public void SurfaceRegistry_ClassifiesKnownCompactAndExpansionSurfaces()
    {
        AssertContextTier("runtime-agent-bootstrap-packet", RuntimeSurfaceContextTier.StartupSafe);
        AssertContextTier("runtime-agent-bootstrap-receipt", RuntimeSurfaceContextTier.StartupSafe);
        AssertContextTier("runtime-agent-queue-projection", RuntimeSurfaceContextTier.StartupSafe);
        AssertContextTier("runtime-agent-short-context", RuntimeSurfaceContextTier.TaskScoped);
        AssertContextTier("runtime-agent-task-overlay", RuntimeSurfaceContextTier.TaskScoped);
        AssertContextTier("runtime-beta-program-status", RuntimeSurfaceContextTier.OperatorExpansion);
        AssertContextTier("runtime-pack-policy-audit", RuntimeSurfaceContextTier.AuditOnly);
        AssertContextTier("runtime-worker-execution-audit", RuntimeSurfaceContextTier.AuditOnly);
        AssertContextTier("runtime-governance-archive-status", RuntimeSurfaceContextTier.AuditOnly);
        AssertContextTier("runtime-hotspot-backlog-drain", RuntimeSurfaceContextTier.AuditOnly);
        AssertDefaultVisibility("runtime-agent-thread-start", RuntimeSurfaceDefaultVisibility.DefaultVisible);
        AssertDefaultVisibility("runtime-agent-bootstrap-packet", RuntimeSurfaceDefaultVisibility.DefaultVisible);
        AssertDefaultVisibility("runtime-agent-task-overlay", RuntimeSurfaceDefaultVisibility.DefaultVisible);
        AssertDefaultVisibility("execution-packet", RuntimeSurfaceDefaultVisibility.DefaultVisible);
        AssertDefaultVisibility("execution-hardening", RuntimeSurfaceDefaultVisibility.DefaultVisible);
        AssertDefaultVisibility("runtime-product-closure-pilot-guide", RuntimeSurfaceDefaultVisibility.ExplicitOnly);
        AssertDefaultVisibility("runtime-agent-problem-intake", RuntimeSurfaceDefaultVisibility.ExplicitOnly);
        AssertDefaultVisibility("runtime-governance-program-reaudit", RuntimeSurfaceDefaultVisibility.ExplicitOnly);
        AssertDefaultVisibility("runtime-pack-policy-audit", RuntimeSurfaceDefaultVisibility.ExplicitOnly);
        Assert.Empty(RuntimeSurfaceCommandRegistry.CompatibilityOnlyCommandNames);
        Assert.Equal(
            [
                "runtime-validationlab-proof-handoff",
                "runtime-controlled-governance-proof",
                "runtime-packaging-proof-federation-maturity",
                "runtime-hotspot-backlog-drain",
                "runtime-hotspot-cross-family-patterns",
                "runtime-governance-program-reaudit",
            ],
            RuntimeSurfaceCommandRegistry.CompatibilityAliasCommandNames);
        AssertSurfaceRole("runtime-governance-archive-status", RuntimeSurfaceRole.Primary);
        AssertSurfaceRole("runtime-governance-program-reaudit", RuntimeSurfaceRole.CompatibilityAlias);
        Assert.True(RuntimeSurfaceCommandRegistry.DefaultVisibleCommandMetadata.Count <= RuntimeSurfaceCommandRegistry.MaxDefaultVisibleSurfaceCount);

        var startupInspectCommands = RuntimeSurfaceCommandRegistry.BuildInspectCommands(RuntimeSurfaceContextTier.StartupSafe);
        var defaultHelpLines = RuntimeSurfaceCommandRegistry.BuildHelpLines("inspect");

        Assert.Contains("inspect runtime-agent-bootstrap-packet", startupInspectCommands);
        Assert.Contains("inspect runtime-agent-bootstrap-receipt [<receipt-json-path>]", startupInspectCommands);
        Assert.Contains("inspect runtime-agent-queue-projection", startupInspectCommands);
        Assert.DoesNotContain(startupInspectCommands, command => command.Contains("runtime-beta-program-status", StringComparison.Ordinal));
        Assert.DoesNotContain(startupInspectCommands, command => command.Contains("runtime-pack-policy-audit", StringComparison.Ordinal));
        Assert.Contains(defaultHelpLines, command => command.Contains("runtime-agent-thread-start", StringComparison.Ordinal));
        Assert.Contains(defaultHelpLines, command => command.Contains("execution-hardening", StringComparison.Ordinal));
        Assert.DoesNotContain(defaultHelpLines, command => command.Contains("runtime-agent-problem-intake", StringComparison.Ordinal));
        Assert.DoesNotContain(defaultHelpLines, command => command.Contains("runtime-governance-program-reaudit", StringComparison.Ordinal));
    }

    [Fact]
    public void QueueProjection_BuildsCompactCountsAndActionableTasksWithoutHistoryExpansion()
    {
        using var workspace = new TemporaryWorkspace();
        WriteSession(
            workspace,
            new RuntimeSessionState
            {
                SessionId = "default",
                AttachedRepoRoot = workspace.RootPath,
                Status = RuntimeSessionStatus.Executing,
                LoopMode = RuntimeLoopMode.ManualTick,
                PlannerLifecycleState = PlannerLifecycleState.Sleeping,
                CurrentTaskId = "T-RUN-001",
            });
        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph(
            [
                new TaskNode
                {
                    TaskId = "T-READY-001",
                    CardId = "CARD-READY",
                    Title = "Ready bounded work",
                    Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                    Priority = "P1",
                    TaskType = TaskType.Execution,
                },
                new TaskNode
                {
                    TaskId = "T-WAIT-001",
                    CardId = "CARD-WAIT",
                    Title = "Waiting on missing dependency",
                    Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                    Priority = "P1",
                    Dependencies = ["T-MISSING"],
                },
                new TaskNode
                {
                    TaskId = "T-RUN-001",
                    CardId = "CARD-RUN",
                    Title = "Running work",
                    Status = Carves.Runtime.Domain.Tasks.TaskStatus.Running,
                    Priority = "P1",
                },
                new TaskNode
                {
                    TaskId = "T-REVIEW-001",
                    CardId = "CARD-REVIEW",
                    Title = "Review work",
                    Status = Carves.Runtime.Domain.Tasks.TaskStatus.Review,
                    Priority = "P1",
                },
                new TaskNode
                {
                    TaskId = "T-BLOCKED-001",
                    CardId = "CARD-BLOCKED",
                    Title = "Blocked work",
                    Status = Carves.Runtime.Domain.Tasks.TaskStatus.Blocked,
                    Priority = "P2",
                },
                new TaskNode
                {
                    TaskId = "T-DEFERRED-001",
                    CardId = "CARD-DEFERRED",
                    Title = "Deferred work",
                    Status = Carves.Runtime.Domain.Tasks.TaskStatus.Deferred,
                    Priority = "P3",
                },
                new TaskNode
                {
                    TaskId = "T-DONE-001",
                    CardId = "CARD-DONE",
                    Title = "Completed history",
                    Status = Carves.Runtime.Domain.Tasks.TaskStatus.Completed,
                    Priority = "P1",
                },
            ])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = new RuntimeAgentQueueProjectionService(workspace.RootPath, workspace.Paths, taskGraphService);

        var surface = service.Build();

        Assert.Equal("runtime-agent-queue-projection", surface.SurfaceId);
        Assert.Equal("derived_read_model_not_authoritative_truth", surface.Projection.TruthBoundary);
        Assert.Equal("T-RUN-001", surface.Projection.CurrentTask.TaskId);
        Assert.Equal("running", surface.Projection.CurrentTask.Status);
        Assert.Equal("worker_execution_in_progress", surface.Projection.CurrentTask.Actionability);
        Assert.Equal(7, surface.Projection.Counts.TotalCount);
        Assert.Equal(2, surface.Projection.Counts.PendingCount);
        Assert.Equal(1, surface.Projection.Counts.RunningCount);
        Assert.Equal(1, surface.Projection.Counts.ReviewCount);
        Assert.Equal(1, surface.Projection.Counts.BlockedCount);
        Assert.Equal(1, surface.Projection.Counts.DeferredCount);
        Assert.Equal(1, surface.Projection.Counts.CompletedCount);
        var actionableTask = Assert.Single(surface.Projection.FirstActionableTasks);
        Assert.Equal("T-READY-001", actionableTask.TaskId);
        Assert.Equal("Ready bounded work", actionableTask.Title);
        Assert.DoesNotContain(surface.Projection.FirstActionableTasks, task => task.TaskId == "T-DONE-001");
        Assert.Equal(".ai/TASK_QUEUE.md", surface.Projection.ExpansionPointers.FullQueuePath);
        Assert.Equal(".ai/tasks/graph.json", surface.Projection.ExpansionPointers.FullGraphPath);
        Assert.Equal("explicit_expansion_only", surface.Projection.ExpansionPointers.ReadMode);
    }

    [Fact]
    public void BootstrapReceipt_ComparesPriorReceiptAndReportsEligibilityOrInvalidation()
    {
        using var workspace = new TemporaryWorkspace();
        WriteSession(
            workspace,
            new RuntimeSessionState
            {
                SessionId = "default",
                AttachedRepoRoot = workspace.RootPath,
                Status = RuntimeSessionStatus.Paused,
                LoopMode = RuntimeLoopMode.ManualTick,
                PlannerLifecycleState = PlannerLifecycleState.Sleeping,
                CurrentTaskId = "T-BOOT-RECEIPT-001",
            });
        workspace.WriteFile(
            ".carves-platform/runtime-state/host_snapshot.json",
            """
            {
              "state": "stopped",
              "session_status": "Idle",
              "host_control_state": "unknown",
              "recorded_at": "2026-04-02T00:00:00Z"
            }
            """);

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph(
            [
                new TaskNode
                {
                    TaskId = "T-BOOT-RECEIPT-001",
                    CardId = "CARD-BOOT-RECEIPT",
                    Title = "Receipt working context",
                    Description = "Project short working context.",
                    Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                    Priority = "P1",
                    TaskType = TaskType.Execution,
                    LastWorkerRunId = "worker-run-receipt-001",
                    LastWorkerBackend = "codex_cli",
                    LastWorkerSummary = "Previous execution produced a bounded result.",
                    LastWorkerDetailRef = ".ai/artifacts/worker-executions/T-BOOT-RECEIPT-001.json",
                },
            ])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var service = new RuntimeAgentBootstrapReceiptService(workspace.RootPath, workspace.Paths, taskGraphService);
        var receiptPath = Path.Combine(workspace.RootPath, "warm-receipt.json");
        var baseline = service.Build();
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(baseline));

        var matched = service.Build(receiptPath);
        Assert.Equal("warm_resume_eligible", matched.Receipt.ResumeDecision);
        Assert.Equal("matched", matched.Receipt.ComparisonStatus);
        Assert.Empty(matched.Receipt.InvalidationReasons);
        Assert.Equal("compact_context_does_not_replace_initialization_report", matched.Receipt.HotPathContext.GovernanceBoundary);
        Assert.Contains("inspect runtime-agent-bootstrap-packet", matched.Receipt.HotPathContext.DefaultInspectCommands);
        Assert.Equal("receipt_validated_machine_surface_first", matched.Receipt.HotPathContext.MarkdownReadPolicy.WarmResumeMode);
        Assert.Contains(matched.Receipt.HotPathContext.MarkdownReadPolicy.ReadTiers, tier => tier.TierId == "warm_resume_validation");
        Assert.Equal("receipt_working_context_then_task_overlay", matched.Receipt.WorkingContext.DefaultEntryMode);
        Assert.Contains(matched.Receipt.WorkingContext.ActiveTasks, task => task.TaskId == "T-BOOT-RECEIPT-001");
        Assert.Contains(matched.Receipt.WorkingContext.RecentExecutions, execution => execution.RunId == "worker-run-receipt-001");
        Assert.Contains("inspect runtime-agent-model-profile-routing", matched.Receipt.WorkingContext.RecommendedNextCommands);
        Assert.True(matched.Receipt.ResumeGuidance.MachineSurfaceFirst);
        Assert.Contains(matched.Receipt.ResumeGuidance.SkipActions, action => action.Contains("broad .ai/memory/architecture", StringComparison.Ordinal));

        WriteSession(
            workspace,
            new RuntimeSessionState
            {
                SessionId = "default",
                AttachedRepoRoot = workspace.RootPath,
                Status = RuntimeSessionStatus.Paused,
                LoopMode = RuntimeLoopMode.ManualTick,
                PlannerLifecycleState = PlannerLifecycleState.Sleeping,
                CurrentTaskId = "T-BOOT-RECEIPT-002",
            });

        var invalidated = service.Build(receiptPath);
        Assert.Equal("cold_init_required", invalidated.Receipt.ResumeDecision);
        Assert.Equal("mismatched", invalidated.Receipt.ComparisonStatus);
        Assert.Contains("current_task_id changed", invalidated.Receipt.InvalidationReasons);
    }

    [Fact]
    public void TaskOverlay_ProjectsScopeProtectedRootsAndVerificationFromExecutionPacket()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/CARVES.Runtime.Application/Platform/RuntimeAgentBootstrapPacketService.cs", "namespace Test;");
        var task = new TaskNode
        {
            TaskId = "T-OVERLAY-001",
            CardId = "CARD-371",
            Title = "Project overlay",
            Description = "Project task overlay from task truth.",
            TaskType = TaskType.Execution,
            Scope = ["src/CARVES.Runtime.Application/Platform/RuntimeAgentBootstrapPacketService.cs"],
            Acceptance = ["overlay carries acceptance truth"],
            Constraints = ["do not widen overlay scope"],
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = "AC-T-OVERLAY-001",
                Title = "Overlay acceptance contract",
                Status = AcceptanceContractLifecycleStatus.Compiled,
                Intent = new AcceptanceContractIntent
                {
                    Goal = "Keep task contract visible in task overlay.",
                    BusinessValue = "Bounded agents can work from compact task context.",
                },
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement
                    {
                        Type = "result_commit",
                        Description = "Review writeback evidence exists.",
                    },
                ],
                Constraints = new AcceptanceContractConstraintSet
                {
                    MustNot = ["Do not widen task overlay scope"],
                    Architecture = ["Preserve runtime bootstrap governance"],
                },
                HumanReview = new AcceptanceContractHumanReviewPolicy
                {
                    Required = true,
                    ProvisionalAllowed = true,
                },
            },
            Validation = new ValidationPlan
            {
                Commands = [["dotnet", "test", "tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj"]],
                Checks = ["surface smoke"],
                ExpectedEvidence = ["runtime-agent-task-overlay api readback"],
            },
            LastWorkerRunId = "run-overlay-001",
            LastWorkerBackend = "codex_cli",
            LastWorkerFailureKind = WorkerFailureKind.BuildFailure,
            LastWorkerRetryable = true,
            LastWorkerSummary = "Previous run failed before validation.",
            LastWorkerDetailRef = ".ai/artifacts/worker-executions/run-overlay-001/execution.json",
            LastProviderDetailRef = ".ai/artifacts/provider/T-OVERLAY-001.json",
            LastRecoveryAction = WorkerRecoveryAction.Retry,
            LastRecoveryReason = "retry after narrowing validation",
            PlannerReview = new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                DecisionStatus = ReviewDecisionStatus.NeedsAttention,
                Reason = "Needs validation readback.",
                AcceptanceMet = false,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
                FollowUpSuggestions = ["rerun targeted test"],
            },
        };

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var memoryService = new MemoryService(new OverlayMemoryRepository(), new ExecutionContextBuilder());
        var compiler = new ExecutionPacketCompilerService(
            workspace.Paths,
            taskGraphService,
            new OverlayCodeGraphQueryService(),
            memoryService,
            new PlannerIntentRoutingService());
        var service = new RuntimeAgentTaskBootstrapOverlayService(
            workspace.RootPath,
            workspace.Paths,
            taskGraphService,
            compiler,
            new OverlayGitClient(["src/CARVES.Runtime.Application/Platform/RuntimeAgentBootstrapPacketService.cs"]));

        var surface = service.Build(task.TaskId);

        Assert.Equal("runtime-agent-task-overlay", surface.SurfaceId);
        Assert.Equal(task.TaskId, surface.Overlay.TaskId);
        Assert.Contains("src/CARVES.Runtime.Application/Platform/RuntimeAgentBootstrapPacketService.cs", surface.Overlay.ScopeFiles);
        Assert.Contains("docs/", surface.Overlay.ProtectedRoots);
        Assert.Contains("carves://truth/tasks", surface.Overlay.ProtectedRoots);
        Assert.Contains("dotnet test tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj", surface.Overlay.RequiredVerification);
        Assert.Contains($"inspect execution-packet {task.TaskId}", surface.Overlay.StableEvidenceSurfaces);
        Assert.Contains("overlay carries acceptance truth", surface.Overlay.Acceptance);
        Assert.Contains("do not widen overlay scope", surface.Overlay.Constraints);
        Assert.Equal("task_truth_bound", surface.Overlay.AcceptanceContract.BindingState);
        Assert.Equal("AC-T-OVERLAY-001", surface.Overlay.AcceptanceContract.ContractId);
        Assert.Equal("compiled", surface.Overlay.AcceptanceContract.Status);
        Assert.Contains("result_commit: Review writeback evidence exists.", surface.Overlay.AcceptanceContract.EvidenceRequired);
        Assert.Contains("Do not widen task overlay scope", surface.Overlay.AcceptanceContract.MustNot);
        Assert.True(surface.Overlay.AcceptanceContract.ProvisionalAllowed);
        var scopeContext = Assert.Single(surface.Overlay.ScopeFileContexts);
        Assert.Equal("src/CARVES.Runtime.Application/Platform/RuntimeAgentBootstrapPacketService.cs", scopeContext.Path);
        Assert.True(scopeContext.Exists);
        Assert.Equal("modified", scopeContext.GitStatus);
        Assert.Equal("editable_root", scopeContext.BoundaryClass);
        Assert.Contains("max_files_changed=", surface.Overlay.SafetyContext.Summary);
        Assert.Equal(SafetyLayerSemantics.Summary, surface.Overlay.SafetyContext.LayerSummary);
        Assert.Contains(surface.Overlay.SafetyContext.Layers, layer => layer.LayerId == SafetyLayerSemantics.PreExecutionBoundaryLayerId && layer.Phase == "pre_execution");
        Assert.Contains(surface.Overlay.SafetyContext.Layers, layer => layer.LayerId == SafetyLayerSemantics.ChangeObservationLayerId && layer.Authority == "evidence_collection");
        Assert.Contains(surface.Overlay.SafetyContext.Layers, layer => layer.LayerId == SafetyLayerSemantics.PostExecutionSafetyLayerId && layer.Authority == "blocking_report_gate");
        Assert.Contains("does_not_provide_process_filesystem_containment", surface.Overlay.SafetyContext.NonClaims);
        Assert.Contains("edit", surface.Overlay.SafetyContext.WorkerAllowedActions);
        Assert.Contains("carves.review_task", surface.Overlay.SafetyContext.PlannerOnlyActions);
        Assert.Contains("src/CARVES.Runtime.Application/Platform/RuntimeAgentBootstrapPacketService.cs", surface.Overlay.EditableRoots);
        Assert.Contains("docs/", surface.Overlay.ReadOnlyRoots);
        Assert.Contains("carves://truth/tasks", surface.Overlay.TruthRoots);
        Assert.Contains(".ai/", surface.Overlay.RepoMirrorRoots);
        Assert.Contains("dotnet test tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj", surface.Overlay.ValidationContext.Commands);
        Assert.Contains("surface smoke", surface.Overlay.ValidationContext.Checks);
        Assert.Contains("runtime-agent-task-overlay api readback", surface.Overlay.ValidationContext.ExpectedEvidence);
        Assert.Equal("build_failure", surface.Overlay.LastWorker.FailureKind);
        Assert.Equal("Previous run failed before validation.", surface.Overlay.LastWorker.Summary);
        Assert.Equal("pause_for_review", surface.Overlay.PlannerReview.Verdict);
        Assert.Contains("rerun targeted test", surface.Overlay.PlannerReview.FollowUpSuggestions);
        Assert.Equal("task_overlay_first_after_initialization", surface.Overlay.MarkdownReadGuidance.DefaultReadMode);
        Assert.Equal("task_refs_are_targeted_reads_not_new_truth", surface.Overlay.MarkdownReadGuidance.GovernanceBoundary);
        Assert.Contains(".ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md", surface.Overlay.MarkdownReadGuidance.TaskScopedMarkdownRefs);
        Assert.Contains($"inspect runtime-agent-task-overlay {task.TaskId}", surface.Overlay.MarkdownReadGuidance.ReplacementSurfaces);
        Assert.Contains($"inspect execution-packet {task.TaskId}", surface.Overlay.MarkdownReadGuidance.ReplacementSurfaces);
        Assert.Contains(surface.Overlay.MarkdownReadGuidance.EscalationTriggers, item => item.Contains("requires_new_card_or_taskgraph", StringComparison.Ordinal));
    }

    [Fact]
    public void ModelProfileRouting_MapsConnectedLanesIntoGovernanceProfiles()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationService = CreateQualificationService(new ModelQualificationMatrix
        {
            MatrixId = "test-matrix",
            Lanes =
            [
                new ModelQualificationLane
                {
                    LaneId = "lane-codex-sdk",
                    ProviderId = "codex",
                    BackendId = "codex_sdk",
                    RequestFamily = "codex_sdk",
                    Model = "gpt-5-codex",
                },
                new ModelQualificationLane
                {
                    LaneId = "lane-openai",
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    RequestFamily = "responses_api",
                    Model = "gpt-5-mini",
                },
            ],
        });
        var service = new RuntimeAgentModelProfileRoutingService(workspace.RootPath, workspace.Paths, qualificationService);

        var surface = service.Build();

        Assert.Equal("runtime-agent-model-profile-routing", surface.SurfaceId);
        Assert.Contains(surface.Routing.AvailableLanes, item => item.LaneId == "lane-codex-sdk" && item.MatchedProfileId == "strong");
        Assert.Contains(surface.Routing.AvailableLanes, item => item.LaneId == "lane-openai" && item.MatchedProfileId == "standard");
    }

    [Fact]
    public void LoopStallGuard_ProjectsProfileSpecificForcedOutcomes()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-GUARD-001",
            CardId = "CARD-373",
            Title = "Guard loop",
            Description = "Project loop and stall guard from execution reports.",
            TaskType = TaskType.Execution,
        };
        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        WriteRunReports(workspace, task.TaskId,
            new ExecutionRunReport
            {
                RunId = "run-1",
                TaskId = task.TaskId,
                RunStatus = ExecutionRunStatus.Stopped,
                BoundaryReason = ExecutionBoundaryStopReason.SizeExceeded,
                ReplanStrategy = ExecutionBoundaryReplanStrategy.SplitTask,
                FilesChanged = 0,
                CompletedSteps = 0,
                TotalSteps = 3,
                RecordedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3),
            },
            new ExecutionRunReport
            {
                RunId = "run-2",
                TaskId = task.TaskId,
                RunStatus = ExecutionRunStatus.Stopped,
                BoundaryReason = ExecutionBoundaryStopReason.SizeExceeded,
                ReplanStrategy = ExecutionBoundaryReplanStrategy.SplitTask,
                FilesChanged = 0,
                CompletedSteps = 0,
                TotalSteps = 3,
                RecordedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            },
            new ExecutionRunReport
            {
                RunId = "run-3",
                TaskId = task.TaskId,
                RunStatus = ExecutionRunStatus.Stopped,
                BoundaryReason = ExecutionBoundaryStopReason.SizeExceeded,
                ReplanStrategy = ExecutionBoundaryReplanStrategy.SplitTask,
                FilesChanged = 0,
                CompletedSteps = 0,
                TotalSteps = 3,
                RecordedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            });

        var service = new RuntimeAgentLoopStallGuardService(workspace.RootPath, workspace.Paths, taskGraphService);

        var surface = service.Build(task.TaskId);

        Assert.Equal("runtime-agent-loop-stall-guard", surface.SurfaceId);
        Assert.Equal(ExecutionPatternType.BoundaryLoop, surface.Guard.Pattern.Type);
        Assert.Contains(surface.Guard.ProfileOutcomes, item => item.ProfileId == "standard" && item.ForcedAction == "narrow_scope");
        Assert.Contains(surface.Guard.ProfileOutcomes, item => item.ProfileId == "weak" && item.ForcedAction == "stop_and_replan");
    }

    [Fact]
    public void WeakModelExecutionLane_ProjectsWeakLanePolicyWithoutGuessingQualifiedMatches()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationService = CreateQualificationService(new ModelQualificationMatrix
        {
            MatrixId = "test-matrix",
            Lanes =
            [
                new ModelQualificationLane
                {
                    LaneId = "lane-standard",
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    RequestFamily = "responses_api",
                    Model = "gpt-5-mini",
                },
            ],
        });
        var service = new RuntimeWeakModelExecutionLaneService(workspace.RootPath, workspace.Paths, qualificationService);

        var surface = service.Build();

        Assert.Equal("runtime-agent-weak-model-lane", surface.SurfaceId);
        var lane = Assert.Single(surface.LaneSnapshot.Lanes);
        Assert.Equal("weak-model-bounded-execution", lane.LaneId);
        Assert.Equal("weak", lane.ModelProfileId);
        Assert.Empty(surface.LaneSnapshot.MatchedQualifiedLanes);
    }

    private static CurrentModelQualificationService CreateQualificationService(ModelQualificationMatrix matrix)
    {
        var repository = new InMemoryCurrentModelQualificationRepository();
        repository.SaveMatrix(matrix);
        return new CurrentModelQualificationService(
            repository,
            new InMemoryRuntimeRoutingProfileRepository(),
            new NullQualificationLaneExecutor());
    }

    private static void AssertContextTier(string name, RuntimeSurfaceContextTier expected)
    {
        Assert.True(RuntimeSurfaceCommandRegistry.TryGetContextTier(name, out var actual), $"Missing registry surface: {name}");
        Assert.Equal(expected, actual);
    }

    private static void AssertDefaultVisibility(string name, RuntimeSurfaceDefaultVisibility expected)
    {
        var metadata = RuntimeSurfaceCommandRegistry.CommandMetadata.Single(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        Assert.Equal(expected, metadata.DefaultVisibility);
    }

    private static void AssertSurfaceRole(string name, RuntimeSurfaceRole expected)
    {
        var metadata = RuntimeSurfaceCommandRegistry.CommandMetadata.Single(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        Assert.Equal(expected, metadata.SurfaceRole);
    }

    private static void WriteSession(TemporaryWorkspace workspace, RuntimeSessionState session)
    {
        workspace.WriteFile(
            ".ai/runtime/live-state/session.json",
            JsonSerializer.Serialize(session, JsonOptions));
    }

    private static void WriteRunReports(TemporaryWorkspace workspace, string taskId, params ExecutionRunReport[] reports)
    {
        foreach (var report in reports)
        {
            workspace.WriteFile(
                $".ai/runtime/run-reports/{taskId}/{report.RunId}.json",
                JsonSerializer.Serialize(report, ReportJsonOptions));
        }
    }

    private sealed class OverlayMemoryRepository : IMemoryRepository
    {
        public IReadOnlyList<MemoryDocument> LoadCategory(string category)
        {
            return category switch
            {
                "architecture" =>
                [
                    new MemoryDocument(".ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md", "architecture", "AI Entry", "Entry protocol"),
                ],
                "project" =>
                [
                    new MemoryDocument(".ai/PROJECT_BOUNDARY.md", "project", "Project Boundary", "Boundary"),
                ],
                _ => Array.Empty<MemoryDocument>(),
            };
        }

        public IReadOnlyList<MemoryDocument> LoadRelevantModules(IReadOnlyList<string> moduleNames)
        {
            return
            [
                new MemoryDocument(".ai/memory/modules/CARVES.Runtime.Application.md", "modules", "CARVES.Runtime.Application", "Application module"),
            ];
        }
    }

    private sealed class OverlayCodeGraphQueryService : ICodeGraphQueryService
    {
        public CodeGraphManifest LoadManifest() => new();

        public IReadOnlyList<CodeGraphModuleEntry> LoadModuleSummaries() => [];

        public CodeGraphIndex LoadIndex() => new();

        public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries)
        {
            return new CodeGraphScopeAnalysis(
                scopeEntries.ToArray(),
                ["CARVES.Runtime.Application"],
                ["src/CARVES.Runtime.Application/Platform/RuntimeAgentBootstrapPacketService.cs"],
                [],
                [],
                ["agent bootstrap packet service"]);
        }

        public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries) => CodeGraphImpactAnalysis.Empty;
    }

    private sealed class OverlayGitClient(IReadOnlyList<string> modifiedPaths) : StubGitClient
    {
        public override bool IsRepository(string repoRoot)
        {
            return true;
        }

        public override IReadOnlyList<string> GetUncommittedPaths(string repoRoot)
        {
            return modifiedPaths;
        }
    }

    private sealed class InMemoryCurrentModelQualificationRepository : ICurrentModelQualificationRepository
    {
        private ModelQualificationMatrix? matrix;
        private ModelQualificationRunLedger? latestRun;
        private ModelQualificationCandidateProfile? candidate;

        public ModelQualificationMatrix? LoadMatrix() => matrix;

        public void SaveMatrix(ModelQualificationMatrix matrix) => this.matrix = matrix;

        public ModelQualificationRunLedger? LoadLatestRun() => latestRun;

        public void SaveLatestRun(ModelQualificationRunLedger run) => latestRun = run;

        public ModelQualificationCandidateProfile? LoadCandidate() => candidate;

        public void SaveCandidate(ModelQualificationCandidateProfile candidate) => this.candidate = candidate;
    }

    private sealed class InMemoryRuntimeRoutingProfileRepository : IRuntimeRoutingProfileRepository
    {
        private RuntimeRoutingProfile? profile;

        public RuntimeRoutingProfile? LoadActive() => profile;

        public void SaveActive(RuntimeRoutingProfile profile) => this.profile = profile;
    }

    private sealed class NullQualificationLaneExecutor : IQualificationLaneExecutor
    {
        public WorkerExecutionResult Execute(ModelQualificationLane lane, ModelQualificationCase qualificationCase, int attempt)
        {
            throw new NotSupportedException("Qualification execution is not used in these surface tests.");
        }
    }
}
