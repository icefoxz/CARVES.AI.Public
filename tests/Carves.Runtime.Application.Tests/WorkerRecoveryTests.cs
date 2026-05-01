using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class WorkerRecoveryTests
{
    [Theory]
    [InlineData("codex_sdk", "codex", "bridge script is not configured", WorkerFailureKind.EnvironmentBlocked)]
    [InlineData("openai_api", "openai", "429 rate limit while contacting backend", WorkerFailureKind.TransientInfra)]
    [InlineData("claude_api", "claude", "permission denied while accessing file", WorkerFailureKind.PolicyDenied)]
    public void WorkerFailureInterpreter_UnifiesBackendSpecificFailures(
        string backendId,
        string providerId,
        string evidence,
        WorkerFailureKind expectedFailureKind)
    {
        var interpreter = new WorkerFailureInterpreter();
        var result = new WorkerExecutionResult
        {
            BackendId = backendId,
            ProviderId = providerId,
            AdapterId = "TestWorkerAdapter",
            Status = WorkerExecutionStatus.Failed,
            FailureKind = WorkerFailureKind.Unknown,
            FailureReason = evidence,
            Summary = evidence,
        };

        var interpreted = interpreter.Interpret(result);

        Assert.Equal(expectedFailureKind, interpreted.FailureKind);
        Assert.Equal(WorkerFailureInterpreter.IsRetryable(expectedFailureKind), interpreted.Retryable);
    }

    [Fact]
    public void WorkerFailureInterpreter_RefinesGenericTaskFailureIntoBuildFailure()
    {
        var interpreter = new WorkerFailureInterpreter();
        var result = new WorkerExecutionResult
        {
            BackendId = "codex_cli",
            ProviderId = "codex",
            AdapterId = "CodexCliWorkerAdapter",
            Status = WorkerExecutionStatus.Failed,
            FailureKind = WorkerFailureKind.TaskLogicFailed,
            FailureReason = "Build failed because ResultEnvelope no longer matches the contract.",
            Summary = "Build failed because ResultEnvelope no longer matches the contract.",
        };

        var interpreted = interpreter.Interpret(result);

        Assert.Equal(WorkerFailureKind.BuildFailure, interpreted.FailureKind);
        Assert.False(interpreted.Retryable);
    }

    [Fact]
    public void RecoveryPolicyEngine_SwitchesProviderWhenCurrentBackendIsUnavailable()
    {
        using var workspace = new TemporaryWorkspace();
        var managedRepo = CreateManagedRepo(workspace, "RepoRecoverySwitch");
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        repoRegistry.Register(managedRepo, "repo-recovery-switch", "default", "balanced");
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var adapters = new WorkerAdapterRegistry(
        [
            new TestWorkerAdapter("codex_sdk", "codex"),
            new TestWorkerAdapter("openai_api", "openai"),
        ], new TestWorkerAdapter("codex_sdk", "codex"));
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var providerHealthRepository = new JsonProviderHealthRepository(workspace.Paths);
        providerHealthRepository.Save(new ProviderHealthSnapshot
        {
            Entries =
            [
                new ProviderHealthRecord
                {
                    BackendId = "codex_sdk",
                    ProviderId = "codex",
                    AdapterId = "TestWorkerAdapter",
                    State = WorkerBackendHealthState.Unavailable,
                    Summary = "backend unavailable",
                    CheckedAt = DateTimeOffset.UtcNow,
                },
            ],
        });
        var providerHealth = new ProviderHealthMonitorService(providerHealthRepository, providers, adapters);
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var selection = new WorkerSelectionPolicyService(workspace.RootPath, repoRegistry, providers, routing, governance, adapters, boundary, providerHealth);
        var engine = new RecoveryPolicyEngine(SafetyRules.CreateDefault(), selection, providerHealth);
        var task = BuildTask("T-RECOVER-SWITCH");
        var result = new WorkerExecutionResult
        {
            TaskId = task.TaskId,
            BackendId = "codex_sdk",
            ProviderId = "codex",
            AdapterId = "TestWorkerAdapter",
            Status = WorkerExecutionStatus.Failed,
            FailureKind = WorkerFailureKind.TransientInfra,
            FailureReason = "backend unavailable",
        };

        var decision = engine.Evaluate(task, result, "repo-recovery-switch");

        Assert.Equal(WorkerRecoveryAction.SwitchProvider, decision.Action);
        Assert.Equal("openai_api", decision.AlternateBackendId);
        Assert.True(decision.AutoApplied);
    }

    [Fact]
    public void RecoveryPolicyEngine_RebuildsWorktreeWhenEnvironmentIsBlockedAndNoAlternativeExists()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var nullAdapter = new TestWorkerAdapter("null_worker", "null");
        var adapters = new WorkerAdapterRegistry([nullAdapter], nullAdapter);
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var providerHealth = new ProviderHealthMonitorService(new InMemoryProviderHealthRepository(), providers, adapters);
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var selection = new WorkerSelectionPolicyService(workspace.RootPath, repoRegistry, providers, routing, governance, adapters, boundary, providerHealth);
        var engine = new RecoveryPolicyEngine(SafetyRules.CreateDefault(), selection, providerHealth);
        var task = BuildTask("T-RECOVER-REBUILD", metadataBackendId: "null_worker");
        var result = new WorkerExecutionResult
        {
            TaskId = task.TaskId,
            BackendId = "null_worker",
            ProviderId = "null",
            AdapterId = "NullWorkerAdapter",
            Status = WorkerExecutionStatus.Failed,
            FailureKind = WorkerFailureKind.EnvironmentBlocked,
            FailureReason = "workspace is blocked",
        };

        var decision = engine.Evaluate(task, result);

        Assert.Equal(WorkerRecoveryAction.RebuildWorktree, decision.Action);
        Assert.NotNull(decision.RetryNotBefore);
        Assert.True(decision.AutoApplied);
    }

    [Fact]
    public void RecoveryPolicyEngine_BlocksTaskWhenRetryBudgetIsExhausted()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var nullAdapter = new TestWorkerAdapter("null_worker", "null");
        var adapters = new WorkerAdapterRegistry([nullAdapter], nullAdapter);
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var providerHealth = new ProviderHealthMonitorService(new InMemoryProviderHealthRepository(), providers, adapters);
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var selection = new WorkerSelectionPolicyService(workspace.RootPath, repoRegistry, providers, routing, governance, adapters, boundary, providerHealth);
        var engine = new RecoveryPolicyEngine(SafetyRules.CreateDefault(), selection, providerHealth);
        var task = BuildTask("T-RECOVER-BLOCK", retryCount: SafetyRules.CreateDefault().MaxRetryCount);
        var result = new WorkerExecutionResult
        {
            TaskId = task.TaskId,
            BackendId = "null_worker",
            ProviderId = "null",
            AdapterId = "NullWorkerAdapter",
            Status = WorkerExecutionStatus.Failed,
            FailureKind = WorkerFailureKind.InvalidOutput,
            FailureReason = "invalid structured output",
        };

        var decision = engine.Evaluate(task, result);

        Assert.Equal(WorkerRecoveryAction.BlockTask, decision.Action);
        Assert.Equal("retry_budget_exhausted", decision.ReasonCode);
    }

    [Fact]
    public void RecoveryPolicyEngine_BlocksTaskWhenEnvironmentCredentialsAreMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var nullAdapter = new TestWorkerAdapter("null_worker", "null");
        var adapters = new WorkerAdapterRegistry([nullAdapter], nullAdapter);
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var providerHealth = new ProviderHealthMonitorService(new InMemoryProviderHealthRepository(), providers, adapters);
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var selection = new WorkerSelectionPolicyService(workspace.RootPath, repoRegistry, providers, routing, governance, adapters, boundary, providerHealth);
        var engine = new RecoveryPolicyEngine(SafetyRules.CreateDefault(), selection, providerHealth);
        var task = BuildTask("T-RECOVER-SECRET");
        var result = new WorkerExecutionResult
        {
            TaskId = task.TaskId,
            BackendId = "openai_api",
            ProviderId = "openai",
            AdapterId = "OpenAiWorkerAdapter",
            Status = WorkerExecutionStatus.Blocked,
            FailureKind = WorkerFailureKind.EnvironmentBlocked,
            FailureReason = "OPENAI_API_KEY is required for worker execution.",
        };

        var decision = engine.Evaluate(task, result);

        Assert.Equal(WorkerRecoveryAction.BlockTask, decision.Action);
        Assert.Equal("environment_configuration_required", decision.ReasonCode);
    }

    [Fact]
    public void RecoveryPolicyEngine_EscalatesProtocolBootstrapFailureInsteadOfSemanticRetry()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var nullAdapter = new TestWorkerAdapter("null_worker", "null");
        var adapters = new WorkerAdapterRegistry([nullAdapter], nullAdapter);
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var providerHealth = new ProviderHealthMonitorService(new InMemoryProviderHealthRepository(), providers, adapters);
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var selection = new WorkerSelectionPolicyService(workspace.RootPath, repoRegistry, providers, routing, governance, adapters, boundary, providerHealth);
        var engine = new RecoveryPolicyEngine(SafetyRules.CreateDefault(), selection, providerHealth);
        var task = BuildTask("T-RECOVER-PROTOCOL");
        var result = new WorkerExecutionResult
        {
            TaskId = task.TaskId,
            BackendId = "codex_cli",
            ProviderId = "codex",
            AdapterId = "CodexCliWorkerAdapter",
            Status = WorkerExecutionStatus.Failed,
            FailureKind = WorkerFailureKind.InvalidOutput,
            FailureLayer = WorkerFailureLayer.Protocol,
            FailureReason = "Build FAILED. 0 Warning(s) 0 Error(s)",
        };

        var decision = engine.Evaluate(task, result);

        Assert.Equal(WorkerRecoveryAction.EscalateToOperator, decision.Action);
        Assert.Equal("delegated_worker_invalid_bootstrap_output", decision.ReasonCode);
    }

    [Fact]
    public void RecoveryPolicyEngine_BlocksSemanticBuildFailureInsteadOfTreatingItAsSubstrate()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var nullAdapter = new TestWorkerAdapter("null_worker", "null");
        var adapters = new WorkerAdapterRegistry([nullAdapter], nullAdapter);
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var providerHealth = new ProviderHealthMonitorService(new InMemoryProviderHealthRepository(), providers, adapters);
        var routing = new ProviderRoutingService(providers, repoRegistry, governance, new JsonProviderQuotaRepository(workspace.Paths));
        var boundary = new WorkerExecutionBoundaryService(workspace.RootPath, repoRegistry, governance);
        var selection = new WorkerSelectionPolicyService(workspace.RootPath, repoRegistry, providers, routing, governance, adapters, boundary, providerHealth);
        var engine = new RecoveryPolicyEngine(SafetyRules.CreateDefault(), selection, providerHealth);
        var task = BuildTask("T-RECOVER-BUILD");
        var result = new WorkerExecutionResult
        {
            TaskId = task.TaskId,
            BackendId = "null_worker",
            ProviderId = "null",
            AdapterId = "NullWorkerAdapter",
            Status = WorkerExecutionStatus.Failed,
            FailureKind = WorkerFailureKind.BuildFailure,
            FailureLayer = WorkerFailureLayer.WorkerSemantic,
            FailureReason = "Build failed after patch application.",
        };

        var decision = engine.Evaluate(task, result);

        Assert.Equal(WorkerRecoveryAction.BlockTask, decision.Action);
        Assert.Equal("build_failure", decision.ReasonCode);
    }

    [Fact]
    public void ProviderHealthMonitorService_RefreshesAndPersistsDegradedLatency()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRegistry = new RepoRegistryService(new JsonRepoRegistryRepository(workspace.Paths));
        var governance = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));
        var slowCodex = new TestWorkerAdapter("codex_sdk", "codex", latencyMs: 2000);
        var adapters = new WorkerAdapterRegistry([slowCodex], slowCodex);
        var providers = new ProviderRegistryService(new JsonProviderRegistryRepository(workspace.Paths), repoRegistry, governance, adapters);
        providers.List();
        var repository = new JsonProviderHealthRepository(workspace.Paths);
        var monitor = new ProviderHealthMonitorService(repository, providers, adapters);

        var snapshot = monitor.Refresh();
        var persisted = repository.Load();
        var codex = Assert.Single(snapshot.Entries, entry => entry.BackendId == "codex_sdk");

        Assert.Equal(WorkerBackendHealthState.Degraded, codex.State);
        Assert.Equal("high_latency", codex.DegradationReason);
        Assert.Contains(persisted.Entries, entry => entry.BackendId == "codex_sdk" && entry.State == WorkerBackendHealthState.Degraded);
    }

    [Fact]
    public void RuntimeIncidentTimelineService_AppendsPermissionAuditAndFiltersByTask()
    {
        var repository = new InMemoryRuntimeIncidentTimelineRepository();
        var service = new RuntimeIncidentTimelineService(repository, new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository()));
        var audit = new WorkerPermissionAuditRecord
        {
            RepoId = "repo",
            TaskId = "T-INCIDENT",
            RunId = "run-1",
            BackendId = "codex_sdk",
            ProviderId = "codex",
            PermissionRequestId = "perm-1",
            ActorKind = WorkerPermissionDecisionActorKind.Provider,
            ActorIdentity = "CodexWorkerAdapter",
            ReasonCode = "permission_requested",
            Reason = "Provider requested workspace write approval.",
            ConsequenceSummary = "Execution paused for approval.",
        };

        service.AppendPermissionAudit(audit);
        var filtered = service.Load(taskId: "T-INCIDENT");

        var incident = Assert.Single(filtered);
        Assert.Equal(RuntimeIncidentType.PermissionEvent, incident.IncidentType);
        Assert.Equal(RuntimeIncidentActorKind.Provider, incident.ActorKind);
        Assert.Equal("CodexWorkerAdapter", incident.ActorIdentity);
    }

    [Fact]
    public void WorktreeRuntimeService_QuarantinesWorktreeAndRequestsRebuild()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRoot = workspace.RootPath;
        var worktreePath = Path.Combine(repoRoot, ".carves-worktrees", "T-QUARANTINE");
        Directory.CreateDirectory(worktreePath);
        File.WriteAllText(Path.Combine(worktreePath, "README.md"), "quarantine me");
        var repository = new InMemoryWorktreeRuntimeRepository();
        var service = new WorktreeRuntimeService(repoRoot, new StubGitClient(), repository);

        service.RecordPrepared("T-QUARANTINE", worktreePath, "abc123");
        var record = service.QuarantineAndRequestRebuild("T-QUARANTINE", worktreePath, "worktree corrupted");
        var snapshot = repository.Load();

        Assert.NotNull(record);
        Assert.Equal(WorktreeRuntimeState.Quarantined, record!.State);
        Assert.True(Directory.Exists(record.WorktreePath));
        Assert.False(Directory.Exists(worktreePath));
        Assert.Contains(snapshot.PendingRebuilds, item => item.TaskId == "T-QUARANTINE");
    }

    private static string CreateManagedRepo(TemporaryWorkspace workspace, string repoName)
    {
        var root = Path.Combine(workspace.RootPath, repoName);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, ".ai"));
        return root;
    }

    private static TaskNode BuildTask(string taskId, int retryCount = 0, string? metadataBackendId = null)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(metadataBackendId))
        {
            metadata["worker_backend"] = metadataBackendId;
        }

        return new TaskNode
        {
            TaskId = taskId,
            Title = taskId,
            TaskType = TaskType.Execution,
            RetryCount = retryCount,
            Scope = ["src/Test.cs"],
            Acceptance = ["recovery is explicit"],
            Validation = new ValidationPlan
            {
                Commands =
                [
                    ["dotnet", "test", "CARVES.Runtime.sln"],
                ],
            },
            Metadata = metadata,
        };
    }

    private sealed class TestWorkerAdapter : IWorkerAdapter
    {
        private readonly long latencyMs;

        public TestWorkerAdapter(string backendId, string providerId, long latencyMs = 10)
        {
            BackendId = backendId;
            ProviderId = providerId;
            this.latencyMs = latencyMs;
        }

        public string AdapterId => "TestWorkerAdapter";

        public string BackendId { get; }

        public string ProviderId { get; }

        public bool IsConfigured => true;

        public bool IsRealAdapter => true;

        public string SelectionReason => "test";

        public WorkerProviderCapabilities GetCapabilities()
        {
            return new WorkerProviderCapabilities
            {
                SupportsExecution = true,
                SupportsEventStream = true,
                SupportsHealthProbe = true,
                SupportsTrustedProfiles = true,
                SupportsNetworkAccess = true,
                SupportsDotNetBuild = true,
                SupportsLongRunningTasks = true,
            };
        }

        public WorkerBackendHealthSummary CheckHealth()
        {
            return new WorkerBackendHealthSummary
            {
                State = WorkerBackendHealthState.Healthy,
                Summary = "healthy",
                LatencyMs = latencyMs,
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
            return new WorkerExecutionResult
            {
                TaskId = request.TaskId,
                BackendId = BackendId,
                ProviderId = ProviderId,
                AdapterId = AdapterId,
                Status = WorkerExecutionStatus.Succeeded,
                Summary = "test execution",
            };
        }
    }
}
