using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class WorkerPermissionTests
{
    [Fact]
    public void WorkerPermissionInterpreter_NormalizesPermissionEventIntoCarvesSemantics()
    {
        using var workspace = new TemporaryWorkspace();
        var interpreter = new WorkerPermissionInterpreter();
        var request = BuildWorkerRequest(workspace.RootPath, WorkerExecutionProfile.UntrustedDefault);
        var result = new WorkerExecutionResult
        {
            RunId = "run-approval",
            TaskId = request.Task.TaskId,
            BackendId = "codex_sdk",
            ProviderId = "codex",
            AdapterId = "CodexWorkerAdapter",
            ProfileId = request.ExecutionRequest!.Profile.ProfileId,
            Status = WorkerExecutionStatus.ApprovalWait,
            FailureKind = WorkerFailureKind.ApprovalRequired,
            Events =
            [
                new WorkerEvent
                {
                    RunId = "run-approval",
                    TaskId = request.Task.TaskId,
                    EventType = WorkerEventType.PermissionRequested,
                    Summary = "Permission required to write file README.md",
                    FilePath = Path.Combine(request.Session.WorktreeRoot, "README.md"),
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["permission_kind"] = "filesystem_write",
                    },
                },
            ],
        };

        var normalized = interpreter.Interpret(request, result);

        var permission = Assert.Single(normalized);
        Assert.Equal(WorkerPermissionKind.FilesystemWrite, permission.Kind);
        Assert.Equal(WorkerPermissionRiskLevel.Moderate, permission.RiskLevel);
        Assert.Equal("workspace", permission.ScopeSummary);
        Assert.Equal(WorkerPermissionDecision.Review, permission.RecommendedDecision);
        Assert.Contains("write file", permission.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkerPermissionInterpreter_DoesNotDuplicateApprovalWaitWhenPermissionRequestExists()
    {
        using var workspace = new TemporaryWorkspace();
        var interpreter = new WorkerPermissionInterpreter();
        var request = BuildWorkerRequest(workspace.RootPath, WorkerExecutionProfile.UntrustedDefault);
        var now = DateTimeOffset.UtcNow;
        var result = new WorkerExecutionResult
        {
            RunId = "run-approval",
            TaskId = request.Task.TaskId,
            BackendId = "codex_sdk",
            ProviderId = "codex",
            AdapterId = "CodexWorkerAdapter",
            ProfileId = request.ExecutionRequest!.Profile.ProfileId,
            Status = WorkerExecutionStatus.ApprovalWait,
            FailureKind = WorkerFailureKind.ApprovalRequired,
            Events =
            [
                new WorkerEvent
                {
                    RunId = "run-approval",
                    TaskId = request.Task.TaskId,
                    EventType = WorkerEventType.PermissionRequested,
                    Summary = "Permission required to write README.md",
                    FilePath = Path.Combine(request.Session.WorktreeRoot, "README.md"),
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["permission_kind"] = "filesystem_write",
                        ["scope"] = "workspace",
                    },
                    OccurredAt = now,
                },
                new WorkerEvent
                {
                    RunId = "run-approval",
                    TaskId = request.Task.TaskId,
                    EventType = WorkerEventType.ApprovalWait,
                    Summary = "Awaiting operator approval",
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["permission_kind"] = "filesystem_write",
                    },
                    OccurredAt = now,
                },
            ],
        };

        var normalized = interpreter.Interpret(request, result);

        var permission = Assert.Single(normalized);
        Assert.Equal(WorkerPermissionKind.FilesystemWrite, permission.Kind);
        Assert.EndsWith("README.md", permission.ResourcePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApprovalPolicyEngine_UsesTrustProfileAndRiskToAllowDenyOrReview()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var policy = new ApprovalPolicyEngine(workspace.RootPath, repoRegistry, governance);

        var writeProfile = new WorkerExecutionProfile
        {
            ProfileId = "workspace-safe-write",
            Trusted = true,
            SandboxMode = WorkerSandboxMode.WorkspaceWrite,
            ApprovalMode = WorkerApprovalMode.OnRequest,
            NetworkAccessEnabled = false,
            WorkspaceBoundary = "workspace",
            FilesystemScope = "workspace",
            AllowedPermissionCategories = ["filesystem_write", "filesystem_delete"],
        };

        var allowRequest = BuildWorkerRequest(workspace.RootPath, writeProfile);
        var allowDecision = policy.Evaluate(
            allowRequest,
            new WorkerPermissionRequest
            {
                TaskId = allowRequest.Task.TaskId,
                Kind = WorkerPermissionKind.FilesystemWrite,
                RiskLevel = WorkerPermissionRiskLevel.Moderate,
                ScopeSummary = "workspace",
                Summary = "Write README.md inside the workspace.",
            });

        Assert.Equal(WorkerPermissionDecision.Allow, allowDecision.Decision);

        var reviewRequest = BuildWorkerRequest(workspace.RootPath, writeProfile);
        var reviewDecision = policy.Evaluate(
            reviewRequest,
            new WorkerPermissionRequest
            {
                TaskId = reviewRequest.Task.TaskId,
                Kind = WorkerPermissionKind.FilesystemDelete,
                RiskLevel = WorkerPermissionRiskLevel.High,
                ScopeSummary = "workspace",
                Summary = "Delete temp file inside the workspace.",
            });

        Assert.Equal(WorkerPermissionDecision.Review, reviewDecision.Decision);
        Assert.Equal("high_risk_permission", reviewDecision.ReasonCode);

        var denyRequest = BuildWorkerRequest(workspace.RootPath, WorkerExecutionProfile.UntrustedDefault);
        var denyDecision = policy.Evaluate(
            denyRequest,
            new WorkerPermissionRequest
            {
                TaskId = denyRequest.Task.TaskId,
                Kind = WorkerPermissionKind.NetworkAccess,
                RiskLevel = WorkerPermissionRiskLevel.High,
                ScopeSummary = "workspace",
                Summary = "Download dependency metadata.",
            });

        Assert.Equal(WorkerPermissionDecision.Deny, denyDecision.Decision);
        Assert.Equal("network_disabled", denyDecision.ReasonCode);
    }

    [Fact]
    public void WorkerPermissionOrchestration_KeepsRecommendedAllowPendingWhenProviderIsPaused()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var approvalPolicy = new ApprovalPolicyEngine(workspace.RootPath, repoRegistry, governance);
        var artifactRepository = new RecordingRuntimeArtifactRepository();
        var markdownSync = new NoOpMarkdownSyncService();
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        sessionRepository.Save(RuntimeSessionState.Start(workspace.RootPath, false));
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var taskGraphService = CreateTaskGraphService(
            new TaskNode
            {
                TaskId = "T-PERMISSION",
                Title = "Permission task",
                TaskType = TaskType.Execution,
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                Scope = ["README.md"],
                Acceptance = ["permission request is interpreted"],
            });
        var plannerWakeBridge = new PlannerWakeBridgeService(
            workspace.RootPath,
            sessionRepository,
            markdownSync,
            taskGraphService,
            operatorOsEventStream);
        var orchestration = new WorkerPermissionOrchestrationService(
            workspace.RootPath,
            new WorkerPermissionInterpreter(),
            approvalPolicy,
            artifactRepository,
            new InMemoryWorkerPermissionAuditRepository(),
            governance,
            repoRegistry,
            taskGraphService,
            sessionRepository,
            markdownSync,
            new RuntimeIncidentTimelineService(new JsonRuntimeIncidentTimelineRepository(workspace.Paths), operatorOsEventStream),
            plannerWakeBridge);

        var profile = new WorkerExecutionProfile
        {
            ProfileId = "workspace-safe-write",
            Trusted = true,
            SandboxMode = WorkerSandboxMode.WorkspaceWrite,
            ApprovalMode = WorkerApprovalMode.OnRequest,
            NetworkAccessEnabled = false,
            WorkspaceBoundary = "workspace",
            FilesystemScope = "workspace",
            AllowedPermissionCategories = ["filesystem_write"],
        };
        var request = BuildWorkerRequest(workspace.RootPath, profile);
        var result = new WorkerExecutionResult
        {
            RunId = "run-approval",
            TaskId = request.Task.TaskId,
            BackendId = "codex_sdk",
            ProviderId = "codex",
            AdapterId = "CodexWorkerAdapter",
            ProfileId = profile.ProfileId,
            Status = WorkerExecutionStatus.ApprovalWait,
            FailureKind = WorkerFailureKind.ApprovalRequired,
            Summary = "worker is waiting for permission approval",
            FailureReason = "Permission approval is required before writing README.md.",
            Events =
            [
                new WorkerEvent
                {
                    RunId = "run-approval",
                    TaskId = request.Task.TaskId,
                    EventType = WorkerEventType.PermissionRequested,
                    Summary = "Permission required to write README.md",
                    FilePath = Path.Combine(request.Session.WorktreeRoot, "README.md"),
                    Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["permission_kind"] = "filesystem_write",
                        ["scope"] = "workspace",
                    },
                },
            ],
        };

        var evaluated = orchestration.Evaluate(request, result);

        Assert.Equal(WorkerExecutionStatus.ApprovalWait, evaluated.Status);
        var permission = Assert.Single(evaluated.PermissionRequests);
        Assert.Equal(WorkerPermissionDecision.Allow, permission.RecommendedDecision);
        Assert.Equal(WorkerPermissionState.Pending, permission.State);
    }

    [Fact]
    public void ResolveApprove_QueuesPlannerWakeForApprovalResolution()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var approvalPolicy = new ApprovalPolicyEngine(workspace.RootPath, repoRegistry, governance);
        var artifactRepository = new RecordingRuntimeArtifactRepository();
        var markdownSync = new NoOpMarkdownSyncService();
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        sessionRepository.Save(RuntimeSessionState.Start(workspace.RootPath, false));
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var taskGraphService = CreateTaskGraphService(
            new TaskNode
            {
                TaskId = "T-PERMISSION",
                Title = "Permission task",
                TaskType = TaskType.Execution,
                Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
                Scope = ["README.md"],
                Acceptance = ["permission request is interpreted"],
            });
        var orchestration = new WorkerPermissionOrchestrationService(
            workspace.RootPath,
            new WorkerPermissionInterpreter(),
            approvalPolicy,
            artifactRepository,
            new InMemoryWorkerPermissionAuditRepository(),
            governance,
            repoRegistry,
            taskGraphService,
            sessionRepository,
            markdownSync,
            new RuntimeIncidentTimelineService(new JsonRuntimeIncidentTimelineRepository(workspace.Paths), operatorOsEventStream),
            new PlannerWakeBridgeService(
                workspace.RootPath,
                sessionRepository,
                markdownSync,
                taskGraphService,
                operatorOsEventStream));

        var profile = new WorkerExecutionProfile
        {
            ProfileId = "workspace-safe-write",
            Trusted = true,
            SandboxMode = WorkerSandboxMode.WorkspaceWrite,
            ApprovalMode = WorkerApprovalMode.OnRequest,
            NetworkAccessEnabled = false,
            WorkspaceBoundary = "workspace",
            FilesystemScope = "workspace",
            AllowedPermissionCategories = ["filesystem_write"],
        };
        var request = BuildWorkerRequest(workspace.RootPath, profile);
        var evaluated = orchestration.Evaluate(
            request,
            new WorkerExecutionResult
            {
                RunId = "run-approval",
                TaskId = request.Task.TaskId,
                BackendId = "codex_sdk",
                ProviderId = "codex",
                AdapterId = "CodexWorkerAdapter",
                ProfileId = profile.ProfileId,
                Status = WorkerExecutionStatus.ApprovalWait,
                FailureKind = WorkerFailureKind.ApprovalRequired,
                Summary = "worker is waiting for permission approval",
                FailureReason = "Permission approval is required before writing README.md.",
                Events =
                [
                    new WorkerEvent
                    {
                        RunId = "run-approval",
                        TaskId = request.Task.TaskId,
                        EventType = WorkerEventType.PermissionRequested,
                        Summary = "Permission required to write README.md",
                        FilePath = Path.Combine(request.Session.WorktreeRoot, "README.md"),
                        Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["permission_kind"] = "filesystem_write",
                            ["scope"] = "workspace",
                        },
                    },
                ],
            });

        var pending = Assert.Single(evaluated.PermissionRequests);
        orchestration.ResolveApprove(pending.PermissionRequestId, "operator");
        var session = sessionRepository.Load();

        Assert.NotNull(session);
        Assert.Contains(session!.PendingPlannerWakeSignals, signal => signal.WakeReason == PlannerWakeReason.ApprovalResolved);
        Assert.Contains(session.PendingPlannerWakeSignals, signal => signal.TaskId == "T-PERMISSION");
    }

    [Fact]
    public void ApprovalPolicyEngine_UsesExternalizedApprovalPolicyBeforeRepoLocalDefaults()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var providers = new ProviderRegistryService(
            new JsonProviderRegistryRepository(workspace.Paths),
            repoRegistry,
            governance,
            TestWorkerAdapterRegistryFactory.Create("codex"));
        providers.List();
        var operationalPolicyService = new WorkerOperationalPolicyService(workspace.RootPath, repoRegistry, WorkerOperationalPolicy.CreateDefault());
        var runtimePolicyBundleService = new RuntimePolicyBundleService(workspace.Paths, governance, operationalPolicyService, providers);
        File.WriteAllText(workspace.Paths.PlatformApprovalPolicyFile, """
{
  "version": "1.0",
  "outside_workspace_requires_review": true,
  "high_risk_requires_review": true,
  "manual_approval_mode_requires_review": true,
  "auto_allow_categories": ["filesystem_write"],
  "auto_deny_categories": ["process_control"],
  "force_review_categories": ["filesystem_delete", "outside_workspace_access", "network_access", "unknown_permission_request"]
}
""");
        var policy = new ApprovalPolicyEngine(workspace.RootPath, repoRegistry, governance, operationalPolicyService, runtimePolicyBundleService);
        var profile = new WorkerExecutionProfile
        {
            ProfileId = "workspace-build-test",
            Trusted = false,
            SandboxMode = WorkerSandboxMode.WorkspaceWrite,
            ApprovalMode = WorkerApprovalMode.OnRequest,
            NetworkAccessEnabled = false,
            WorkspaceBoundary = "workspace",
            FilesystemScope = "workspace",
            AllowedPermissionCategories = ["filesystem_write", "process_control"],
        };

        var request = BuildWorkerRequest(workspace.RootPath, profile);
        var decision = policy.Evaluate(
            request,
            new WorkerPermissionRequest
            {
                TaskId = request.Task.TaskId,
                Kind = WorkerPermissionKind.ProcessControl,
                RiskLevel = WorkerPermissionRiskLevel.Moderate,
                ScopeSummary = "workspace",
                Summary = "Run a governed build command.",
            });

        Assert.Equal(WorkerPermissionDecision.Deny, decision.Decision);
        Assert.Equal("policy_auto_deny", decision.ReasonCode);
    }

    private static WorkerRequest BuildWorkerRequest(string repoRoot, WorkerExecutionProfile profile)
    {
        var worktreeRoot = Path.Combine(repoRoot, ".carves-worktrees", "T-PERMISSION");
        Directory.CreateDirectory(worktreeRoot);
        var task = new TaskNode
        {
            TaskId = "T-PERMISSION",
            Title = "Permission task",
            TaskType = TaskType.Execution,
            Scope = ["README.md"],
            Acceptance = ["permission request is interpreted"],
        };

        return new WorkerRequest
        {
            Task = task,
            Session = new ExecutionSession(task.TaskId, task.Title, repoRoot, false, "abc123", "CodexWorkerAdapter", worktreeRoot, DateTimeOffset.UtcNow),
            ExecutionRequest = new WorkerExecutionRequest
            {
                TaskId = task.TaskId,
                Title = task.Title,
                Description = task.Description,
                Instructions = "Execute the task.",
                Input = "Update README.md.",
                MaxOutputTokens = 200,
                TimeoutSeconds = 30,
                RepoRoot = repoRoot,
                WorktreeRoot = worktreeRoot,
                BaseCommit = "abc123",
                Profile = profile,
                AllowedFiles = ["README.md"],
            },
            Selection = new WorkerSelectionDecision
            {
                RepoId = "local-repo",
                RequestedTrustProfileId = profile.ProfileId,
                Allowed = true,
            },
        };
    }

    private static TaskGraphService CreateTaskGraphService(params TaskNode[] tasks)
    {
        var graph = new Carves.Runtime.Domain.Tasks.TaskGraph(tasks);
        var repository = new InMemoryTaskGraphRepository(graph);
        return new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
    }

    private sealed class RecordingRuntimeArtifactRepository : IRuntimeArtifactRepository
    {
        private readonly Dictionary<string, WorkerPermissionArtifact> workerPermissionArtifacts = new(StringComparer.Ordinal);

        public void SaveWorkerArtifact(TaskRunArtifact artifact)
        {
        }

        public TaskRunArtifact? TryLoadWorkerArtifact(string taskId)
        {
            return null;
        }

        public void SaveWorkerExecutionArtifact(WorkerExecutionArtifact artifact)
        {
        }

        public WorkerExecutionArtifact? TryLoadWorkerExecutionArtifact(string taskId)
        {
            return null;
        }

        public void SaveWorkerPermissionArtifact(WorkerPermissionArtifact artifact)
        {
            workerPermissionArtifacts[artifact.TaskId] = artifact;
        }

        public WorkerPermissionArtifact? TryLoadWorkerPermissionArtifact(string taskId)
        {
            return workerPermissionArtifacts.TryGetValue(taskId, out var artifact) ? artifact : null;
        }

        public IReadOnlyList<WorkerPermissionArtifact> LoadWorkerPermissionArtifacts()
        {
            return workerPermissionArtifacts.Values.ToArray();
        }

        public void SaveProviderArtifact(AiExecutionArtifact artifact)
        {
        }

        public AiExecutionArtifact? TryLoadProviderArtifact(string taskId)
        {
            return null;
        }

        public void SavePlannerProposalArtifact(PlannerProposalEnvelope artifact)
        {
        }

        public PlannerProposalEnvelope? TryLoadPlannerProposalArtifact(string proposalId)
        {
            return null;
        }

        public void SaveSafetyArtifact(SafetyArtifact artifact)
        {
        }

        public SafetyArtifact? TryLoadSafetyArtifact(string taskId)
        {
            return null;
        }

        public void SavePlannerReviewArtifact(PlannerReviewArtifact artifact)
        {
        }

        public PlannerReviewArtifact? TryLoadPlannerReviewArtifact(string taskId)
        {
            return null;
        }

        public void SaveMergeCandidateArtifact(MergeCandidateArtifact artifact)
        {
        }

        public void SaveRuntimeFailureArtifact(RuntimeFailureRecord artifact)
        {
        }

        public RuntimeFailureRecord? TryLoadLatestRuntimeFailure()
        {
            return null;
        }

        public void SaveRuntimePackAdmissionArtifact(RuntimePackAdmissionArtifact artifact)
        {
        }

        public RuntimePackAdmissionArtifact? TryLoadCurrentRuntimePackAdmissionArtifact()
        {
            return null;
        }

        public void SaveRuntimePackSelectionArtifact(RuntimePackSelectionArtifact artifact)
        {
        }

        public RuntimePackSelectionArtifact? TryLoadCurrentRuntimePackSelectionArtifact()
        {
            return null;
        }
    }
}
