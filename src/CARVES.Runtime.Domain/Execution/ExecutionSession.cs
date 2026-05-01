using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public record ExecutionSession(
    string TaskId,
    string Title,
    string RepoRoot,
    bool DryRun,
    string CurrentCommit,
    string WorkerAdapterName,
    string WorktreeRoot,
    DateTimeOffset RequestedAt)
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string? RepoId { get; init; }

    public string? WorkerProfileId { get; init; }

    public bool WorkerProfileTrusted { get; init; }

    public string? WorkerBackend { get; init; }

    public string? WorkerProviderId { get; init; }

    public string? WorkerRoutingProfileId { get; init; }

    public string? ActiveRoutingProfileId { get; init; }

    public string? WorkerRoutingRuleId { get; init; }

    public string? WorkerRoutingIntent { get; init; }

    public string? WorkerRoutingModuleId { get; init; }

    public string? WorkerModelId { get; init; }

    public string? WorkerRouteSource { get; init; }

    public string? WorkerSelectionSummary { get; init; }

    public WorkerRequestBudget WorkerRequestBudget { get; init; } = WorkerRequestBudget.None;

    public string? WorkerRunId { get; init; }

    public string? RequestedWorkerThreadId { get; init; }

    public string? WorkerThreadId { get; init; }

    public WorkerThreadContinuity WorkerThreadContinuity { get; init; } = WorkerThreadContinuity.None;
}
