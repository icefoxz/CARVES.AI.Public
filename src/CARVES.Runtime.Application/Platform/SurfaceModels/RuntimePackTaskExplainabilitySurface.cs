using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackTaskExplainabilitySurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-task-explainability.v1";

    public string SurfaceId { get; init; } = "runtime-pack-task-explainability";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public RuntimePackExecutionAttribution? CurrentSelection { get; init; }

    public RuntimePackTaskExplainabilityCoverage Coverage { get; init; } = new();

    public RuntimePackReviewRubricProjection? CurrentReviewRubricProjection { get; init; }

    public IReadOnlyList<RuntimePackExecutionAuditRunEntry> RecentRuns { get; init; } = [];

    public IReadOnlyList<RuntimePackExecutionAuditReportEntry> RecentReports { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimePackTaskExplainabilityCoverage
{
    public bool HasCurrentSelection { get; init; }

    public int AttributedRunCount { get; init; }

    public int AttributedReportCount { get; init; }

    public int CurrentSelectionRunCount { get; init; }

    public int CurrentSelectionReportCount { get; init; }

    public int DivergentRunCount { get; init; }

    public int DivergentReportCount { get; init; }

    public string? LatestRunId { get; init; }

    public string? LatestReportRunId { get; init; }

    public string Summary { get; init; } = string.Empty;
}
