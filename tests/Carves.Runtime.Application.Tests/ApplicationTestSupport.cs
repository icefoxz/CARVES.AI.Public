using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.AI;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

internal sealed class TemporaryWorkspace : IDisposable
{
    public TemporaryWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "carves-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public ControlPlanePaths Paths => ControlPlanePaths.FromRepoRoot(RootPath);

    public string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(RootPath, true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                TestWait.Delay(TimeSpan.FromMilliseconds(100));
            }
            catch (IOException) when (attempt < 4)
            {
                TestWait.Delay(TimeSpan.FromMilliseconds(100));
            }
            catch
            {
                return;
            }
        }

        try
        {
            Directory.Delete(RootPath, true);
        }
        catch
        {
        }
    }
}

internal sealed class InMemoryTaskGraphRepository : ITaskGraphRepository
{
    private DomainTaskGraph graph;

    public InMemoryTaskGraphRepository(DomainTaskGraph graph)
    {
        this.graph = graph;
    }

    public DomainTaskGraph Load()
    {
        return graph;
    }

    public void Save(DomainTaskGraph graph)
    {
        this.graph = graph;
    }

    public void Upsert(TaskNode task)
    {
        graph.AddOrReplace(task);
    }

    public void UpsertRange(IEnumerable<TaskNode> tasks)
    {
        foreach (var task in tasks)
        {
            graph.AddOrReplace(task);
        }
    }

    public T WithWriteLock<T>(Func<T> action)
    {
        return action();
    }
}

internal sealed class InMemoryRuntimeSessionRepository : IRuntimeSessionRepository
{
    private RuntimeSessionState? session;

    public RuntimeSessionState? Load()
    {
        return session;
    }

    public void Save(RuntimeSessionState session)
    {
        this.session = session;
    }

    public void Delete()
    {
        session = null;
    }
}

internal sealed class InMemoryWorkerPermissionAuditRepository : IWorkerPermissionAuditRepository
{
    private IReadOnlyList<WorkerPermissionAuditRecord> records = Array.Empty<WorkerPermissionAuditRecord>();

    public IReadOnlyList<WorkerPermissionAuditRecord> Load()
    {
        return records;
    }

    public void Save(IReadOnlyList<WorkerPermissionAuditRecord> records)
    {
        this.records = records.ToArray();
    }
}

internal sealed class InMemoryProviderHealthRepository : IProviderHealthRepository
{
    private ProviderHealthSnapshot snapshot = new();

    public ProviderHealthSnapshot Load()
    {
        return snapshot;
    }

    public void Save(ProviderHealthSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

internal sealed class InMemoryActorSessionRepository : IActorSessionRepository
{
    private ActorSessionSnapshot snapshot = new();

    public ActorSessionSnapshot Load()
    {
        return snapshot;
    }

    public void Save(ActorSessionSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

internal sealed class InMemoryOwnershipRepository : IOwnershipRepository
{
    private OwnershipSnapshot snapshot = new();

    public OwnershipSnapshot Load()
    {
        return snapshot;
    }

    public void Save(OwnershipSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

internal sealed class InMemoryOperatorOsEventRepository : IOperatorOsEventRepository
{
    private OperatorOsEventSnapshot snapshot = new();

    public OperatorOsEventSnapshot Load()
    {
        return snapshot;
    }

    public void Save(OperatorOsEventSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

internal sealed class InMemoryRuntimeIncidentTimelineRepository : IRuntimeIncidentTimelineRepository
{
    private IReadOnlyList<RuntimeIncidentRecord> records = Array.Empty<RuntimeIncidentRecord>();

    public IReadOnlyList<RuntimeIncidentRecord> Load()
    {
        return records;
    }

    public void Save(IReadOnlyList<RuntimeIncidentRecord> records)
    {
        this.records = records.ToArray();
    }
}

internal sealed class InMemoryWorktreeRuntimeRepository : IWorktreeRuntimeRepository
{
    private WorktreeRuntimeSnapshot snapshot = new();

    public WorktreeRuntimeSnapshot Load()
    {
        return snapshot;
    }

    public void Save(WorktreeRuntimeSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

internal sealed class InMemoryManagedWorkspaceLeaseRepository : IManagedWorkspaceLeaseRepository
{
    private ManagedWorkspaceLeaseSnapshot snapshot = new();

    public ManagedWorkspaceLeaseSnapshot Load()
    {
        return snapshot;
    }

    public void Save(ManagedWorkspaceLeaseSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }
}

internal sealed class InMemoryRepoRegistryRepository : IRepoRegistryRepository
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

internal sealed class InMemoryPlatformGovernanceRepository : IPlatformGovernanceRepository
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
        this.events = events.ToArray();
    }
}

internal sealed class NoOpMarkdownSyncService : IMarkdownSyncService
{
    public void Sync(
        DomainTaskGraph graph,
        TaskNode? currentTask = null,
        TaskRunReport? report = null,
        PlannerReview? review = null,
        RuntimeSessionState? session = null,
        TaskScheduleDecision? schedulerDecision = null)
    {
    }
}

internal class StubGitClient : IGitClient
{
    public virtual string TryGetCurrentCommit(string repoRoot)
    {
        return "abc123";
    }

    public virtual bool IsRepository(string repoRoot)
    {
        return false;
    }

    public virtual bool HasUncommittedChanges(string repoRoot)
    {
        return false;
    }

    public virtual IReadOnlyList<string> GetUncommittedPaths(string repoRoot)
    {
        return Array.Empty<string>();
    }

    public virtual IReadOnlyList<string> GetUntrackedPaths(string repoRoot)
    {
        return Array.Empty<string>();
    }

    public virtual IReadOnlyList<string> GetChangedPathsSince(string repoRoot, string baseCommit)
    {
        return Array.Empty<string>();
    }

    public virtual bool TryCreateDetachedWorktree(string repoRoot, string worktreePath, string startPoint)
    {
        return false;
    }

    public virtual void TryRemoveWorktree(string repoRoot, string worktreePath)
    {
    }

    public virtual string? TryGetUncommittedDiff(string repoRoot, IReadOnlyList<string>? paths = null)
    {
        return null;
    }

    public virtual string? TryCreateScopedSnapshotCommit(string repoRoot, IReadOnlyList<string> paths, string message)
    {
        return null;
    }
}

internal static class TestSystemConfigFactory
{
    public static SystemConfig Create(IReadOnlyList<string>? codeDirectories = null)
    {
        return new SystemConfig(
            "TestRepo",
            "../.carves-worktrees/TestRepo",
            1,
            ["dotnet", "test", "CARVES.Runtime.sln"],
            codeDirectories ?? ["src"],
            [".git", ".nuget", "bin", "obj", "TestResults", "coverage", ".ai/worktrees"],
            true,
            true);
    }
}

internal static class TestWorkerAdapterRegistryFactory
{
    private static readonly object EnvironmentLock = new();

    public static WorkerAdapterRegistry Create(
        string provider = "codex",
        IHttpTransport? transport = null,
        string? model = null,
        string? baseUrl = null,
        string? apiKeyEnvironmentVariable = null,
        string? requestFamily = null)
    {
        var normalizedProvider = provider.ToLowerInvariant();
        var config = new AiProviderConfig(
            provider,
            Enabled: true,
            Model: model ?? (normalizedProvider switch
            {
                "gemini" => "gemini-2.5-pro",
                "claude" => "claude-sonnet-4-5",
                _ => "gpt-5-mini",
            }),
            BaseUrl: baseUrl ?? (normalizedProvider switch
            {
                "gemini" => "https://generativelanguage.googleapis.com/v1beta",
                "claude" => "https://api.anthropic.com/v1",
                _ => "https://api.openai.com/v1",
            }),
            ApiKeyEnvironmentVariable: apiKeyEnvironmentVariable ?? (normalizedProvider switch
            {
                "gemini" => "GEMINI_API_KEY",
                "claude" => "ANTHROPIC_API_KEY",
                _ => "OPENAI_API_KEY",
            }),
            AllowFallbackToNull: true,
            RequestTimeoutSeconds: 30,
            MaxOutputTokens: 500,
            ReasoningEffort: "low",
            RequestFamily: requestFamily,
            Organization: null,
            Project: null);
        lock (EnvironmentLock)
        {
            var maskedVariables = GetMaskedEnvironmentVariables(normalizedProvider);
            var savedValues = maskedVariables.ToDictionary(variable => variable, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
            try
            {
                foreach (var variable in maskedVariables)
                {
                    Environment.SetEnvironmentVariable(variable, null);
                }

                var baseRegistry = WorkerAdapterFactory.Create(config, new NullAiClient(), transport);
                return BuildDeterministicRegistry(baseRegistry, normalizedProvider);
            }
            finally
            {
                foreach (var entry in savedValues)
                {
                    Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                }
            }
        }
    }

    private static IReadOnlyList<string> GetMaskedEnvironmentVariables(string provider)
    {
        return provider switch
        {
            "openai" => ["ANTHROPIC_API_KEY", "GEMINI_API_KEY"],
            "claude" => ["OPENAI_API_KEY", "CODEX_API_KEY", "GEMINI_API_KEY"],
            "gemini" => ["OPENAI_API_KEY", "CODEX_API_KEY", "ANTHROPIC_API_KEY"],
            "codex" => ["ANTHROPIC_API_KEY", "GEMINI_API_KEY"],
            "null" => ["OPENAI_API_KEY", "CODEX_API_KEY", "ANTHROPIC_API_KEY", "GEMINI_API_KEY"],
            _ => ["OPENAI_API_KEY", "CODEX_API_KEY", "ANTHROPIC_API_KEY", "GEMINI_API_KEY"],
        };
    }

    private static WorkerAdapterRegistry BuildDeterministicRegistry(WorkerAdapterRegistry baseRegistry, string provider)
    {
        var adapters = baseRegistry.Adapters
            .Select(adapter => ShouldForceCodexSdkHealthy(provider, adapter)
                ? new ForcedHealthWorkerAdapter(
                    adapter,
                    configured: true,
                    new WorkerBackendHealthSummary
                    {
                        State = WorkerBackendHealthState.Healthy,
                        Summary = "Codex SDK worker is forced healthy for deterministic selection tests.",
                    })
                : adapter)
            .ToArray();
        var activeAdapter = adapters.First(adapter => string.Equals(adapter.BackendId, baseRegistry.ActiveAdapter.BackendId, StringComparison.OrdinalIgnoreCase));
        return new WorkerAdapterRegistry(
            adapters,
            activeAdapter);
    }

    private static bool ShouldForceCodexSdkHealthy(string provider, IWorkerAdapter adapter)
    {
        return string.Equals(provider, "codex", StringComparison.OrdinalIgnoreCase)
               && string.Equals(adapter.BackendId, "codex_sdk", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ForcedHealthWorkerAdapter : IWorkerAdapter
    {
        private readonly IWorkerAdapter inner;
        private readonly WorkerBackendHealthSummary health;
        private readonly bool configured;

        public ForcedHealthWorkerAdapter(IWorkerAdapter inner, bool configured, WorkerBackendHealthSummary health)
        {
            this.inner = inner;
            this.configured = configured;
            this.health = health;
        }

        public string AdapterId => inner.AdapterId;

        public string BackendId => inner.BackendId;

        public string ProviderId => inner.ProviderId;

        public bool IsConfigured => configured;

        public bool IsRealAdapter => inner.IsRealAdapter;

        public string SelectionReason => inner.SelectionReason;

        public WorkerProviderCapabilities GetCapabilities()
        {
            return inner.GetCapabilities();
        }

        public WorkerBackendHealthSummary CheckHealth()
        {
            return health;
        }

        public WorkerRunControlResult Cancel(string runId, string reason)
        {
            return inner.Cancel(runId, reason);
        }

        public WorkerExecutionResult Execute(WorkerExecutionRequest request)
        {
            return inner.Execute(request);
        }
    }
}

internal sealed class StubHttpTransport : IHttpTransport
{
    private readonly Func<HttpTransportRequest, HttpTransportResponse> handler;

    public StubHttpTransport(Func<HttpTransportRequest, HttpTransportResponse> handler)
    {
        this.handler = handler;
    }

    public HttpTransportRequest? LastRequest { get; private set; }

    public HttpTransportResponse Send(HttpTransportRequest request)
    {
        LastRequest = request;
        return handler(request);
    }
}

internal sealed class StubCodeGraphQueryService : ICodeGraphQueryService
{
    public CodeGraphManifest LoadManifest()
    {
        return new CodeGraphManifest();
    }

    public IReadOnlyList<CodeGraphModuleEntry> LoadModuleSummaries()
    {
        return Array.Empty<CodeGraphModuleEntry>();
    }

    public CodeGraphIndex LoadIndex()
    {
        return new CodeGraphIndex();
    }

    public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries)
    {
        return CodeGraphScopeAnalysis.Empty;
    }

    public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries)
    {
        return CodeGraphImpactAnalysis.Empty;
    }
}
