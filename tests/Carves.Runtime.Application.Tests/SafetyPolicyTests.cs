using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Processes;
using Carves.Runtime.Application.Safety;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class SafetyPolicyTests
{
    [Fact]
    public void SafetyLayerSemantics_SeparatesPreExecutionObservationAndPostExecutionGates()
    {
        var layers = SafetyLayerSemantics.WorkerExecutionLayers.ToDictionary(layer => layer.LayerId, StringComparer.Ordinal);

        Assert.Equal("pre_execution", layers[SafetyLayerSemantics.PreExecutionBoundaryLayerId].Phase);
        Assert.Equal("blocking_request_gate", layers[SafetyLayerSemantics.PreExecutionBoundaryLayerId].Authority);
        Assert.Contains("does_not_know_future_patch_paths", layers[SafetyLayerSemantics.PreExecutionBoundaryLayerId].NonClaims);
        Assert.Equal("post_execution_observation", layers[SafetyLayerSemantics.ChangeObservationLayerId].Phase);
        Assert.Equal("evidence_collection", layers[SafetyLayerSemantics.ChangeObservationLayerId].Authority);
        Assert.Contains("does_not_prevent_worker_writes", layers[SafetyLayerSemantics.ChangeObservationLayerId].NonClaims);
        Assert.Equal("post_execution", layers[SafetyLayerSemantics.PostExecutionSafetyLayerId].Phase);
        Assert.Equal("blocking_report_gate", layers[SafetyLayerSemantics.PostExecutionSafetyLayerId].Authority);
        Assert.Contains("does_not_rollback_filesystem_mutations", layers[SafetyLayerSemantics.PostExecutionSafetyLayerId].NonClaims);
    }

    [Fact]
    public void DescribeBaseline_FlagsInvalidRetryAndMissingDerivedViews()
    {
        var service = new SafetyService(SafetyValidatorCatalog.CreateDefault());
        var rules = new SafetyRules(
            20,
            500,
            21,
            200,
            0,
            [".git/"],
            [".ai/"],
            ["src/", "tests/"],
            [".ai/tasks/"],
            [".ai/memory/"],
            "memory_write",
            true,
            true);

        var violations = service.DescribeBaseline(rules);

        Assert.Contains(violations, violation => violation.Code == "RETRY_LIMIT_INVALID");
        Assert.Contains(violations, violation => violation.Code == "REVIEW_THRESHOLD_INERT");
        Assert.Contains(violations, violation => violation.Code == "DERIVED_VIEW_PATH_MISSING");
    }

    [Fact]
    public void Evaluate_BlocksDerivedMarkdownViewWrites()
    {
        var service = new SafetyService(SafetyValidatorCatalog.CreateDefault());
        var task = BuildTask(".ai/STATE.md");
        var report = BuildReport(task, true, ".ai/STATE.md");

        var decision = service.Evaluate(new SafetyContext(task, report, SafetyValidationMode.Execution, SafetyRules.CreateDefault(), ModuleDependencyMap.Empty));

        Assert.Equal(SafetyOutcome.Blocked, decision.Outcome);
        Assert.Contains(decision.Issues, issue => issue.Code == "DERIVED_VIEW_WRITE_FORBIDDEN");
    }

    [Fact]
    public void Evaluate_RequestsReviewForMachineStateWrites()
    {
        var service = new SafetyService(SafetyValidatorCatalog.CreateDefault());
        var task = BuildTask(".ai/tasks/graph.json");
        var report = BuildReport(task, true, ".ai/tasks/graph.json");

        var decision = service.Evaluate(new SafetyContext(task, report, SafetyValidationMode.Execution, SafetyRules.CreateDefault(), ModuleDependencyMap.Empty));

        Assert.Equal(SafetyOutcome.NeedsReview, decision.Outcome);
        Assert.Contains(decision.Issues, issue => issue.Code == "CONTROL_PLANE_WRITE_REVIEW_REQUIRED");
    }

    [Fact]
    public void Evaluate_BlocksRepeatedValidationFailureAtRetryCeiling()
    {
        var service = new SafetyService(SafetyValidatorCatalog.CreateDefault());
        var task = BuildTask("tests/RetryLoopTests.cs", retryCount: 1);
        var report = BuildReport(task, false, "tests/RetryLoopTests.cs");

        var decision = service.Evaluate(new SafetyContext(task, report, SafetyValidationMode.Execution, SafetyRules.CreateDefault(), ModuleDependencyMap.Empty));

        Assert.Equal(SafetyOutcome.Blocked, decision.Outcome);
        Assert.Contains(decision.Issues, issue => issue.Code == "REPEATED_FAILURE_LOOP");
    }

    [Fact]
    public void Evaluate_AllowsValidatedExecutionAtRetryCeiling()
    {
        var service = new SafetyService(SafetyValidatorCatalog.CreateDefault());
        var task = BuildTask("tests/RetryLoopTests.cs", retryCount: 2);
        var report = BuildReport(task, true, "tests/RetryLoopTests.cs");

        var decision = service.Evaluate(new SafetyContext(task, report, SafetyValidationMode.Execution, SafetyRules.CreateDefault(), ModuleDependencyMap.Empty));

        Assert.Equal(SafetyOutcome.Allow, decision.Outcome);
        Assert.DoesNotContain(decision.Issues, issue => issue.Code == "RETRY_LIMIT_REACHED");
        Assert.DoesNotContain(decision.Issues, issue => issue.Code == "REPEATED_FAILURE_LOOP");
    }

    [Fact]
    public void Evaluate_BlocksPatchPathsOutsideDeclaredTaskScope()
    {
        var service = new SafetyService(SafetyValidatorCatalog.CreateDefault());
        var task = BuildTask("src/ScopedArea/");
        var report = BuildReport(task, true, "src/ScopedArea/FileA.cs", "src/Outside/FileB.cs");

        var decision = service.Evaluate(new SafetyContext(task, report, SafetyValidationMode.Execution, SafetyRules.CreateDefault(), ModuleDependencyMap.Empty));

        Assert.Equal(SafetyOutcome.Blocked, decision.Outcome);
        Assert.Contains(decision.Issues, issue => issue.Code == "TASK_SCOPE_VIOLATION");
    }

    [Fact]
    public void Evaluate_PlanningModeSkipsExecutionOnlyValidators()
    {
        var service = new SafetyService(SafetyValidatorCatalog.CreateDefault());
        var task = BuildTask("src/PlanOnly.cs", taskType: TaskType.Planning);
        var report = BuildReport(task, true, "src/PlanOnly.cs");

        var decision = service.Evaluate(new SafetyContext(task, report, SafetyValidationMode.Planning, SafetyRules.CreateDefault(), ModuleDependencyMap.Empty));

        Assert.Equal(SafetyValidationMode.Planning, decision.ValidationMode);
        Assert.DoesNotContain(decision.Issues, issue => issue.Code == "TEST_GATE_MISSING");
    }

    [Fact]
    public void WorkerService_SkipsAiAndValidationForPlanningTasks()
    {
        using var workspace = new TemporaryWorkspace();
        var processRunner = new RecordingProcessRunner();
        var workerAdapter = new RecordingWorkerAdapter();
        var artifacts = new RecordingRuntimeArtifactRepository();
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), operatorOsEventStream);
        var worker = new WorkerService(
            TestSystemConfigFactory.Create(["src", "tests"]),
            SafetyRules.CreateDefault(),
            ModuleDependencyMap.Empty,
            new WorkerAdapterRegistry([workerAdapter], workerAdapter),
            processRunner,
            new StubWorktreeManager(),
            new SafetyService(SafetyValidatorCatalog.CreateDefault()),
            artifacts,
            CreateBoundaryService(),
            CreatePermissionOrchestrationService(artifacts),
            new RuntimeIncidentTimelineService(new InMemoryRuntimeIncidentTimelineRepository(), operatorOsEventStream),
            actorSessionService,
            operatorOsEventStream,
            new ExecutionEvidenceRecorder(workspace.Paths));
        var task = BuildTask("docs/runtime/planner-reentry.md", taskType: TaskType.Planning);
        var request = new WorkerRequest
        {
            Task = task,
            Session = new ExecutionSession(task.TaskId, task.Title, "repo", false, "abc123", "NullAiClient", "worktree", DateTimeOffset.UtcNow),
            ValidationCommands =
            [
                ["dotnet", "test", "CARVES.Runtime.sln"],
            ],
        };

        var report = worker.Execute(request);

        Assert.Empty(processRunner.Commands);
        Assert.Equal(0, workerAdapter.ExecutionCount);
        Assert.Equal(SafetyValidationMode.Planning, report.SafetyDecision.ValidationMode);
        Assert.DoesNotContain(report.Validation.Evidence, evidence => evidence.Contains("command passed", StringComparison.OrdinalIgnoreCase));
    }

    private static TaskNode BuildTask(string scopePath, int retryCount = 0, TaskType taskType = TaskType.Execution)
    {
        return new TaskNode
        {
            TaskId = "T-SAFETY",
            Title = "Safety policy task",
            Status = DomainTaskStatus.Pending,
            TaskType = taskType,
            RetryCount = retryCount,
            Scope = [scopePath],
            Acceptance = ["safe"],
        };
    }

    private static TaskRunReport BuildReport(TaskNode task, bool passed, params string[] paths)
    {
        var session = new ExecutionSession(task.TaskId, task.Title, "repo", false, "abc123", "NullAiClient", "worktree", DateTimeOffset.UtcNow);
        return new TaskRunReport
        {
            TaskId = task.TaskId,
            Request = new WorkerRequest
            {
                Task = task,
                Session = session,
                ValidationCommands =
                [
                    ["dotnet", "test", "CARVES.Runtime.sln"],
                ],
            },
            Session = session,
            Patch = new PatchSummary(paths.Length, 0, 0, true, paths),
            Validation = new ValidationResult
            {
                Passed = passed,
                Commands =
                [
                    ["dotnet", "test", "CARVES.Runtime.sln"],
                ],
            },
        };
    }

    private static WorkerExecutionBoundaryService CreateBoundaryService()
    {
        return new WorkerExecutionBoundaryService(
            repoRoot: "repo",
            new RepoRegistryService(new InMemoryRepoRegistryRepository()),
            new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository()));
    }

    private static WorkerPermissionOrchestrationService CreatePermissionOrchestrationService(IRuntimeArtifactRepository artifacts)
    {
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var taskGraph = new Carves.Runtime.Domain.Tasks.TaskGraph();
        taskGraph.AddOrReplace(new TaskNode
        {
            TaskId = "T-SAFETY",
            Title = "Safety policy task",
            Status = DomainTaskStatus.Pending,
            Scope = ["docs/runtime/planner-reentry.md"],
            Acceptance = ["safe"],
        });
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(taskGraph), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        var markdownSync = new NoOpMarkdownSyncService();

        return new WorkerPermissionOrchestrationService(
            "repo",
            new WorkerPermissionInterpreter(),
            new ApprovalPolicyEngine("repo", repoRegistry, governance),
            artifacts,
            new InMemoryWorkerPermissionAuditRepository(),
            governance,
            repoRegistry,
            taskGraphService,
            sessionRepository,
            markdownSync,
            new RuntimeIncidentTimelineService(new InMemoryRuntimeIncidentTimelineRepository(), operatorOsEventStream),
            new PlannerWakeBridgeService("repo", sessionRepository, markdownSync, taskGraphService, operatorOsEventStream));
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<IReadOnlyList<string>> Commands { get; } = [];

        public ProcessExecutionResult Run(IReadOnlyList<string> command, string workingDirectory)
        {
            Commands.Add(command.ToArray());
            return new ProcessExecutionResult(0, string.Empty, string.Empty);
        }
    }

    private sealed class RecordingWorkerAdapter : IWorkerAdapter
    {
        public string AdapterId => "RecordingWorkerAdapter";

        public string BackendId => "recording-backend";

        public string ProviderId => "recording";

        public bool IsConfigured => true;

        public bool IsRealAdapter => true;

        public string SelectionReason => "Test adapter.";

        public int ExecutionCount { get; private set; }

        public WorkerProviderCapabilities GetCapabilities()
        {
            return new WorkerProviderCapabilities
            {
                SupportsExecution = true,
                SupportsEventStream = true,
                SupportsHealthProbe = true,
                SupportsDotNetBuild = true,
            };
        }

        public WorkerBackendHealthSummary CheckHealth()
        {
            return new WorkerBackendHealthSummary
            {
                State = WorkerBackendHealthState.Healthy,
                Summary = "healthy",
            };
        }

        public WorkerRunControlResult Cancel(string runId, string reason)
        {
            return new WorkerRunControlResult
            {
                BackendId = BackendId,
                RunId = runId,
                Supported = false,
                Succeeded = false,
                Summary = "not supported",
            };
        }

        public WorkerExecutionResult Execute(WorkerExecutionRequest request)
        {
            ExecutionCount += 1;
            return WorkerExecutionResult.Skipped(
                request.TaskId,
                BackendId,
                ProviderId,
                AdapterId,
                request.Profile,
                "not-used",
                request.Input,
                "hash") with
            {
                AdapterReason = SelectionReason,
                Model = "test-model",
            };
        }
    }

    private sealed class StubWorktreeManager : IWorktreeManager
    {
        public string ResolveWorktreeRoot(SystemConfig systemConfig, string repoRoot)
        {
            return Path.Combine(repoRoot, ".carves-worktrees");
        }

        public string PrepareWorktree(SystemConfig systemConfig, string repoRoot, string taskId, string? startPoint)
        {
            return Path.Combine(repoRoot, ".carves-worktrees", taskId);
        }

        public void CleanupWorktree(string worktreePath)
        {
        }
    }

    private sealed class RecordingRuntimeArtifactRepository : IRuntimeArtifactRepository
    {
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
        }

        public WorkerPermissionArtifact? TryLoadWorkerPermissionArtifact(string taskId)
        {
            return null;
        }

        public IReadOnlyList<WorkerPermissionArtifact> LoadWorkerPermissionArtifacts()
        {
            return Array.Empty<WorkerPermissionArtifact>();
        }

        public void SaveProviderArtifact(AiExecutionArtifact artifact)
        {
        }

        public AiExecutionArtifact? TryLoadProviderArtifact(string taskId)
        {
            return null;
        }

        public void SavePlannerProposalArtifact(Carves.Runtime.Domain.Planning.PlannerProposalEnvelope artifact)
        {
        }

        public Carves.Runtime.Domain.Planning.PlannerProposalEnvelope? TryLoadPlannerProposalArtifact(string proposalId)
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

    private sealed class InMemoryRepoRegistryRepository : IRepoRegistryRepository
    {
        private RepoRegistry registry = new();

        public RepoRegistry Load()
        {
            return registry;
        }

        public void Save(RepoRegistry registry)
        {
            this.registry = registry;
        }
    }

    private sealed class InMemoryPlatformGovernanceRepository : IPlatformGovernanceRepository
    {
        private PlatformGovernanceSnapshot snapshot = new();
        private IReadOnlyList<GovernanceEvent> events = Array.Empty<GovernanceEvent>();

        public PlatformGovernanceSnapshot Load()
        {
            return snapshot;
        }

        public IReadOnlyList<GovernanceEvent> LoadEvents()
        {
            return events;
        }

        public void Save(PlatformGovernanceSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public void SaveEvents(IReadOnlyList<GovernanceEvent> events)
        {
            this.events = events;
        }
    }

    private sealed class InMemoryRuntimeIncidentTimelineRepository : IRuntimeIncidentTimelineRepository
    {
        private IReadOnlyList<RuntimeIncidentRecord> records = Array.Empty<RuntimeIncidentRecord>();

        public IReadOnlyList<RuntimeIncidentRecord> Load()
        {
            return records;
        }

        public void Save(IReadOnlyList<RuntimeIncidentRecord> records)
        {
            this.records = records;
        }
    }
}
