using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerExecutionRequest
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string RequestId { get; init; } = $"worker-request-{Guid.NewGuid():N}";

    public string TaskId { get; init; } = string.Empty;

    public string RepoId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Instructions { get; init; } = string.Empty;

    public string Input { get; init; } = string.Empty;

    public int MaxOutputTokens { get; init; }

    public int TimeoutSeconds { get; init; }

    public WorkerRequestBudget RequestBudget { get; init; } = WorkerRequestBudget.None;

    public string RepoRoot { get; init; } = string.Empty;

    public string WorktreeRoot { get; init; } = string.Empty;

    public string WorktreePath => WorktreeRoot;

    public string BaseCommit { get; init; } = string.Empty;

    public string? PriorThreadId { get; init; }

    public string BackendHint { get; init; } = string.Empty;

    public string? ModelOverride { get; init; }

    public string? ReasoningEffort { get; init; }

    public string? RoutingIntent { get; init; }

    public string? RoutingModuleId { get; init; }

    public string? RoutingProfileId { get; init; }

    public string? RoutingRuleId { get; init; }

    public string? ActiveRoutingProfileId { get; init; }

    public bool DryRun { get; init; }

    public ExecutionPacket? Packet { get; init; }

    public WorkerExecutionPacket WorkerExecutionPacket { get; init; } = new();

    public WorkerExecutionProfile Profile { get; init; } = WorkerExecutionProfile.UntrustedDefault;

    public IReadOnlyList<string> AllowedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<IReadOnlyList<string>> ValidationCommands { get; init; } = Array.Empty<IReadOnlyList<string>>();

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public Carves.Runtime.Domain.AI.LlmRequestEnvelopeDraft? RequestEnvelopeDraft { get; init; }
}
