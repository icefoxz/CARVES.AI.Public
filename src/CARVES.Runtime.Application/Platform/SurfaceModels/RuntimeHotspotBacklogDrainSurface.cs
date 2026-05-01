using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeHotspotBacklogDrainSurface
{
    public string SchemaVersion { get; init; } = "runtime-hotspot-backlog-drain.v1";

    public string SurfaceId { get; init; } = "runtime-hotspot-backlog-drain";

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string BacklogPath { get; init; } = string.Empty;

    public string QueueIndexPath { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public DateTimeOffset? QueueSnapshotGeneratedAt { get; init; }

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public RuntimeHotspotBacklogDrainCountsSurface Counts { get; init; } = new();

    public IReadOnlyList<RuntimeHotspotBacklogDrainQueueSurface> Queues { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeHotspotBacklogDrainCountsSurface
{
    public int TotalBacklogItems { get; init; }

    public int OpenBacklogItems { get; init; }

    public int SuggestedBacklogItems { get; init; }

    public int ResolvedBacklogItems { get; init; }

    public int SuppressedBacklogItems { get; init; }

    public int QueueFamilyCount { get; init; }

    public int MaterializedTaskCount { get; init; }

    public int CompletedQueueCount { get; init; }

    public int AcceptedResidualQueueCount { get; init; }

    public int GovernedCompletedQueueCount { get; init; }

    public int CompletedWithRemainingBacklogCount { get; init; }

    public int ResidualOpenQueueCount { get; init; }

    public int ContinuedQueueCount { get; init; }

    public int ClosureBlockingBacklogItemCount { get; init; }

    public int NonBlockingBacklogItemCount { get; init; }

    public int UnselectedBacklogItemCount { get; init; }

    public int UnselectedClosureRelevantBacklogItemCount { get; init; }

    public int UnselectedMaintenanceNoiseBacklogItemCount { get; init; }
}

public sealed class RuntimeHotspotBacklogDrainQueueSurface
{
    public string QueueId { get; init; } = string.Empty;

    public string FamilyId { get; init; } = string.Empty;

    public int QueuePass { get; init; } = 1;

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string PlanningTaskId { get; init; } = string.Empty;

    public string? SuggestedTaskId { get; init; }

    public string? PreviousSuggestedTaskId { get; init; }

    public string SuggestedTaskStatus { get; init; } = "none";

    public string PreviousSuggestedTaskStatus { get; init; } = "none";

    public string DrainState { get; init; } = string.Empty;

    public string ClosureState { get; init; } = string.Empty;

    public string ProofTarget { get; init; } = string.Empty;

    public int SelectedBacklogItemCount { get; init; }

    public int OpenBacklogItemCount { get; init; }

    public int SuggestedBacklogItemCount { get; init; }

    public int ResolvedBacklogItemCount { get; init; }

    public int ClosureBlockingBacklogItemCount { get; init; }

    public int NonBlockingBacklogItemCount { get; init; }

    public IReadOnlyList<string> SelectedBacklogItemIds { get; init; } = [];

    public IReadOnlyList<string> ScopeRoots { get; init; } = [];

    public IReadOnlyList<string> HotspotPaths { get; init; } = [];

    public IReadOnlyList<string> ValidationSurface { get; init; } = [];

    public IReadOnlyList<string> PreservationConstraints { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
