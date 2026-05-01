using System.Text.Json;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.Persistence;
using Carves.Runtime.Domain.Planning;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class ManagedWorkspaceLeaseServiceTests
{
    [Fact]
    public void IssueForTask_CreatesTaskBoundLeaseAndProjectsSurface()
    {
        using var fixture = ManagedWorkspaceLeaseFixture.Create();

        var lease = fixture.Service.IssueForTask(fixture.TaskId);
        var surface = fixture.Service.BuildSurface();

        Assert.True(Directory.Exists(lease.WorkspacePath));
        Assert.True(File.Exists(Path.Combine(lease.WorkspacePath, "src", "Sample.cs")));
        Assert.StartsWith("plan-primary_formal_planning-", lease.PlanHandle, StringComparison.Ordinal);
        Assert.Contains("src/Sample.cs", lease.AllowedWritablePaths, StringComparer.Ordinal);
        Assert.Equal("host_routed_review_and_writeback_required", lease.ApprovalPosture);
        Assert.Equal("task_bound_workspace_active", surface.OverallPosture);
        Assert.Equal("mode_d_scoped_task_workspace_hardening", surface.ModeDProfileId);
        Assert.Equal("active", surface.ModeDHardeningState);
        Assert.Equal(Path.GetFullPath(fixture.Workspace.RootPath), surface.RuntimeDocumentRoot);
        Assert.Equal("repo_local", surface.RuntimeDocumentRootMode);
        Assert.Equal(lease.PlanHandle, surface.PlanHandle);
        Assert.Contains(surface.BoundTaskIds, item => string.Equals(item, fixture.TaskId, StringComparison.Ordinal));
        var activeLease = Assert.Single(surface.ActiveLeases);
        Assert.Equal(fixture.TaskId, activeLease.TaskId);
        Assert.Equal(lease.WorkspacePath, activeLease.WorkspacePath);
        Assert.Contains(surface.PathPolicies, item => item.PolicyClass == "host_only");
        Assert.Contains(surface.PathPolicies, item => item.PolicyClass == "scope_escape" && item.EnforcementEffect == "fail_closed_and_require_replan");
        Assert.Contains(surface.ModeDHardeningChecks, item => item.CheckId == "official_truth_host_ingress" && item.State == "satisfied");
        Assert.Contains("review/writeback", surface.RecommendedNextAction, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSubmissionCandidate_ReturnsChangedFilesInsideLeaseScope()
    {
        using var fixture = ManagedWorkspaceLeaseFixture.Create();
        var lease = fixture.Service.IssueForTask(fixture.TaskId);
        var workspaceFile = Path.Combine(lease.WorkspacePath, "src", "Sample.cs");
        File.WriteAllText(workspaceFile, "namespace Sample; public sealed class SampleService { public string Value => \"changed\"; }");

        var candidate = fixture.Service.BuildSubmissionCandidate(fixture.TaskId);

        Assert.Equal(fixture.TaskId, candidate.TaskId);
        Assert.Equal(lease.LeaseId, candidate.Lease.LeaseId);
        Assert.Contains("src/Sample.cs", candidate.ChangedPaths, StringComparer.Ordinal);
        Assert.Equal("workspace_open", candidate.PathPolicy.Status);
        Assert.Equal(0, candidate.PathPolicy.ScopeEscapeCount);
        Assert.Equal(0, candidate.PathPolicy.HostOnlyCount);
        Assert.Equal(0, candidate.PathPolicy.DenyCount);
    }

    [Fact]
    public void BuildSubmissionCandidate_DetectsManagedCopyChangesWithoutGitDiff()
    {
        using var fixture = ManagedWorkspaceLeaseFixture.Create(new NonRepositoryThrowingGitClient());
        var lease = fixture.Service.IssueForTask(fixture.TaskId);
        var workspaceFile = Path.Combine(lease.WorkspacePath, "src", "Sample.cs");
        File.WriteAllText(workspaceFile, "namespace Sample; public sealed class SampleService { public string Value => \"changed\"; }");

        var candidate = fixture.Service.BuildSubmissionCandidate(fixture.TaskId);

        Assert.Contains("src/Sample.cs", candidate.ChangedPaths, StringComparer.Ordinal);
    }

    [Fact]
    public void ReleaseForTask_RemovesLeaseFromActiveSurface()
    {
        using var fixture = ManagedWorkspaceLeaseFixture.Create();
        var lease = fixture.Service.IssueForTask(fixture.TaskId);

        var releasedCount = fixture.Service.ReleaseForTask(fixture.TaskId);
        var surface = fixture.Service.BuildSurface();

        Assert.Equal(1, releasedCount);
        Assert.Empty(fixture.Service.LoadActive(fixture.TaskId));
        Assert.DoesNotContain(surface.ActiveLeases, item => item.LeaseId == lease.LeaseId);
        Assert.Equal("task_bound_workspace_ready_to_issue", surface.OverallPosture);
    }

    [Fact]
    public void BuildSurface_ProjectsRecoverableResidueWhenActiveLeaseIsBoundToTerminalTask()
    {
        using var fixture = ManagedWorkspaceLeaseFixture.Create();
        var lease = fixture.Service.IssueForTask(fixture.TaskId);
        ReplaceTaskStatus(fixture.TaskGraphService, fixture.TaskId, Carves.Runtime.Domain.Tasks.TaskStatus.Completed);

        var surface = fixture.Service.BuildSurface();

        Assert.Empty(surface.ActiveLeases);
        Assert.Equal("recoverable_runtime_residue_present", surface.RecoverableResiduePosture);
        Assert.Equal(1, surface.RecoverableResidueCount);
        Assert.Equal("healthy_with_recoverable_residue", surface.OperationalState);
        Assert.False(surface.SafeToStartNewExecution);
        Assert.True(surface.SafeToDiscuss);
        Assert.True(surface.SafeToCleanup);
        Assert.Equal("warning", surface.HighestRecoverableResidueSeverity);
        Assert.True(surface.RecoverableResidueBlocksAutoRun);
        Assert.Contains("terminal", surface.RecoverableResidueSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("carves cleanup", surface.RecoverableResidueRecommendedNextAction, StringComparison.Ordinal);
        Assert.Equal("cleanup_runtime_residue", surface.RecoverableCleanupActionId);
        Assert.Equal("dry_run_first", surface.RecoverableCleanupActionMode);
        var cleanupAction = Assert.Single(surface.AvailableActions);
        Assert.Equal("cleanup_runtime_residue", cleanupAction.ActionId);
        Assert.Equal("cleanup", cleanupAction.Kind);
        Assert.Equal("carves cleanup", cleanupAction.Command);
        Assert.Equal("dry_run_first", cleanupAction.ActionMode);
        var residue = Assert.Single(surface.RecoverableResidues);
        Assert.Equal("terminal_task_active_lease", residue.ResidueClass);
        Assert.Equal("terminal_task_active_lease", residue.Kind);
        Assert.Equal("warning", residue.Severity);
        Assert.Equal(lease.LeaseId, residue.LeaseId);
        Assert.Equal(fixture.TaskId, residue.TaskId);
        Assert.Equal(lease.WorkspacePath, residue.WorkspacePath);
        Assert.True(residue.Recoverable);
        Assert.True(residue.BlocksAutoRun);
        Assert.True(residue.BlocksHealthyIdle);
    }

    [Fact]
    public void BuildSurface_KeepsCleanupActionSurfaceCompactWhenNoResidueExists()
    {
        using var fixture = ManagedWorkspaceLeaseFixture.Create();

        var surface = fixture.Service.BuildSurface();

        Assert.Equal("no_recoverable_runtime_residue", surface.RecoverableResiduePosture);
        Assert.Equal(0, surface.RecoverableResidueCount);
        Assert.Equal("none", surface.HighestRecoverableResidueSeverity);
        Assert.False(surface.RecoverableResidueBlocksAutoRun);
        Assert.Equal(string.Empty, surface.RecoverableCleanupActionId);
        Assert.Equal("none", surface.RecoverableCleanupActionMode);
        Assert.Empty(surface.AvailableActions);
    }

    [Fact]
    public void IssueForTask_RejectsTaskOutsideCurrentPlanningLineage()
    {
        using var fixture = ManagedWorkspaceLeaseFixture.Create();
        var graph = fixture.TaskGraphService.Load();
        var task = graph.Tasks[fixture.TaskId];
        var mutatedMetadata = new Dictionary<string, string>(task.Metadata, StringComparer.Ordinal)
        {
            [PlanningLineageMetadata.ActivePlanningCardIdKey] = "planning-card-other",
        };
        graph.AddOrReplace(new Carves.Runtime.Domain.Tasks.TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource,
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies,
            Scope = task.Scope,
            Acceptance = task.Acceptance,
            Constraints = task.Constraints,
            AcceptanceContract = task.AcceptanceContract,
            Validation = task.Validation,
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities,
            Metadata = mutatedMetadata,
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind,
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction,
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore,
            PlannerReview = task.PlannerReview,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
        });

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Service.IssueForTask(fixture.TaskId));

        Assert.Contains("not the current active plan handle", error.Message, StringComparison.Ordinal);
    }

    private sealed class ManagedWorkspaceLeaseFixture : IDisposable
    {
        private ManagedWorkspaceLeaseFixture(
            TemporaryWorkspace workspace,
            ManagedWorkspaceLeaseService service,
            TaskGraphService taskGraphService,
            string taskId,
            string leaseRoot)
        {
            Workspace = workspace;
            Service = service;
            TaskGraphService = taskGraphService;
            TaskId = taskId;
            LeaseRoot = leaseRoot;
        }

        public TemporaryWorkspace Workspace { get; }

        public ManagedWorkspaceLeaseService Service { get; }

        public TaskGraphService TaskGraphService { get; }

        public string TaskId { get; }

        public string LeaseRoot { get; }

        public static ManagedWorkspaceLeaseFixture Create(IGitClient? gitClient = null)
        {
            var workspace = new TemporaryWorkspace();
            var leaseRootRelative = $"../phase4-workspaces-{Guid.NewGuid():N}";
            var leaseRoot = Path.GetFullPath(Path.Combine(workspace.RootPath, leaseRootRelative));
            workspace.WriteFile("README.md", "# Sample Repo");
            workspace.WriteFile("docs/runtime/runtime-governed-agent-handoff-proof.md", "# governed handoff proof");
            workspace.WriteFile("docs/runtime/runtime-first-run-operator-packet.md", "# first-run operator packet");
            workspace.WriteFile("docs/runtime/runtime-managed-workspace-file-operation-model.md", "# managed workspace");
            workspace.WriteFile("docs/runtime/runtime-agent-working-modes-and-constraint-ladder.md", "# working modes");
            workspace.WriteFile("docs/runtime/runtime-agent-working-modes-implementation-plan.md", "# implementation plan");
            workspace.WriteFile("docs/runtime/runtime-mode-d-scoped-task-workspace-hardening.md", "# mode d hardening");
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
            planningDraftService.SetCardStatus(card.CardId, CardLifecycleState.Approved, "approved for managed workspace issuance");
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
                                title = "Lease task",
                                description = "Issue a managed workspace lease.",
                                scope = new[] { "src/Sample.cs" },
                                acceptance = new[] { "managed workspace lease exists" },
                                proof_target = new
                                {
                                    kind = "focused_behavior",
                                    description = "Prove the managed workspace lease is issued from plan-bound task truth.",
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
            planningDraftService.ApproveTaskGraphDraft(taskGraphDraft.DraftId, "approved for managed workspace lease");

            var formalPlanningPacketService = new FormalPlanningPacketService(intentDiscoveryService, planningDraftService, taskGraphService);
            gitClient ??= new StubGitClient();
            var worktreeRuntimeService = new WorktreeRuntimeService(workspace.RootPath, gitClient, new InMemoryWorktreeRuntimeRepository());
            var leaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
            var service = new ManagedWorkspaceLeaseService(
                workspace.RootPath,
                systemConfig,
                formalPlanningPacketService,
                taskGraphService,
                gitClient,
                new Carves.Runtime.Infrastructure.Git.WorktreeManager(gitClient),
                worktreeRuntimeService,
                leaseRepository);

            return new ManagedWorkspaceLeaseFixture(workspace, service, taskGraphService, taskId, leaseRoot);
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

    private sealed class NonRepositoryThrowingGitClient : StubGitClient
    {
        public override bool IsRepository(string repoRoot)
        {
            return false;
        }

        public override IReadOnlyList<string> GetChangedPathsSince(string repoRoot, string baseCommit)
        {
            throw new InvalidOperationException("Managed copy workspaces must not require git diff.");
        }

        public override IReadOnlyList<string> GetUncommittedPaths(string repoRoot)
        {
            throw new InvalidOperationException("Managed copy workspaces must not require git status.");
        }
    }

    private static void ReplaceTaskStatus(
        TaskGraphService taskGraphService,
        string taskId,
        Carves.Runtime.Domain.Tasks.TaskStatus status)
    {
        var graph = taskGraphService.Load();
        var task = graph.Tasks[taskId];
        graph.AddOrReplace(new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = status,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource,
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies,
            Scope = task.Scope,
            Acceptance = task.Acceptance,
            Constraints = task.Constraints,
            AcceptanceContract = task.AcceptanceContract,
            Validation = task.Validation,
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities,
            Metadata = task.Metadata,
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind,
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction,
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore,
            PlannerReview = task.PlannerReview,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
        });
    }
}
