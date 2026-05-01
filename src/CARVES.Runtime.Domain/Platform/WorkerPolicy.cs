using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Domain.Platform;

public sealed class WorkerPolicy
{
    public string PolicyId { get; init; } = string.Empty;

    public int MaxConcurrentTasks { get; init; }

    public bool RequireTrustedNodes { get; init; }

    public IReadOnlyList<string> AllowedRepoScopes { get; init; } = Array.Empty<string>();

    public string DefaultProfileId { get; init; } = "workspace_build_test";

    public IReadOnlyList<WorkerExecutionProfile> Profiles { get; init; } = Array.Empty<WorkerExecutionProfile>();
}
