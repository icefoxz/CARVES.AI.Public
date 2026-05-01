namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentQueueProjectionSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-queue-projection.v1";

    public string SurfaceId { get; init; } = "runtime-agent-queue-projection";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public RuntimeAgentQueueProjection Projection { get; init; } = new();
}

public sealed class RuntimeAgentQueueProjection
{
    public string ProjectionId { get; init; } = "agent-queue-projection";

    public string Summary { get; init; } = string.Empty;

    public string TruthBoundary { get; init; } = "derived_read_model_not_authoritative_truth";

    public RuntimeAgentCurrentTaskPosture CurrentTask { get; init; } = new();

    public RuntimeAgentQueueStatusCounts Counts { get; init; } = new();

    public RuntimeAgentQueueTaskSummary[] FirstActionableTasks { get; init; } = [];

    public RuntimeAgentQueueExpansionPointers ExpansionPointers { get; init; } = new();
}

public sealed class RuntimeAgentCurrentTaskPosture
{
    public string TaskId { get; init; } = "N/A";

    public string CardId { get; init; } = "N/A";

    public string Title { get; init; } = "N/A";

    public string Status { get; init; } = "none";

    public string Priority { get; init; } = "N/A";

    public string Source { get; init; } = "runtime_session";

    public string Actionability { get; init; } = "no_current_task";

    public string InspectCommand { get; init; } = "N/A";

    public string OverlayCommand { get; init; } = "N/A";
}

public sealed class RuntimeAgentQueueStatusCounts
{
    public int TotalCount { get; init; }

    public int SuggestedCount { get; init; }

    public int PendingCount { get; init; }

    public int DeferredCount { get; init; }

    public int RunningCount { get; init; }

    public int TestingCount { get; init; }

    public int ReviewCount { get; init; }

    public int ApprovalWaitCount { get; init; }

    public int BlockedCount { get; init; }

    public int FailedCount { get; init; }

    public int CompletedCount { get; init; }

    public int MergedCount { get; init; }

    public int DiscardedCount { get; init; }

    public int SupersededCount { get; init; }
}

public sealed class RuntimeAgentQueueTaskSummary
{
    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = "CARD-UNKNOWN";

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Priority { get; init; } = string.Empty;

    public string TaskType { get; init; } = string.Empty;

    public string InspectCommand { get; init; } = string.Empty;

    public string OverlayCommand { get; init; } = string.Empty;

    public string RunCommand { get; init; } = string.Empty;
}

public sealed class RuntimeAgentQueueExpansionPointers
{
    public string FullQueuePath { get; init; } = ".ai/TASK_QUEUE.md";

    public string FullGraphPath { get; init; } = ".ai/tasks/graph.json";

    public string CurrentTaskPath { get; init; } = ".ai/CURRENT_TASK.md";

    public string ReadMode { get; init; } = "explicit_expansion_only";

    public string CanonicalTruthNote { get; init; } = "Full queue and graph remain canonical; this surface is rebuildable projection only.";
}
