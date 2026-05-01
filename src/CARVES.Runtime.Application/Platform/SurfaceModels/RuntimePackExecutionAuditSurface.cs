using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackExecutionAuditSurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-execution-audit.v1";

    public string SurfaceId { get; init; } = "runtime-pack-execution-audit";

    public string Summary { get; init; } = string.Empty;

    public RuntimePackExecutionAttribution? CurrentSelection { get; init; }

    public RuntimePackExecutionAuditCoverage Coverage { get; init; } = new();

    public IReadOnlyList<RuntimePackExecutionAuditRunEntry> RecentRuns { get; init; } = [];

    public IReadOnlyList<RuntimePackExecutionAuditReportEntry> RecentReports { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed class RuntimePackExecutionAuditCoverage
{
    public bool HasCurrentSelection { get; init; }

    public int RecentAttributedRunCount { get; init; }

    public int RecentAttributedReportCount { get; init; }

    public int CurrentSelectionRunCount { get; init; }

    public int CurrentSelectionReportCount { get; init; }

    public int RecentDeclarativeRunCount { get; init; }

    public int RecentDeclarativeReportCount { get; init; }

    public int CurrentSelectionContributionRunCount { get; init; }

    public int CurrentSelectionContributionReportCount { get; init; }

    public int DivergentContributionRunCount { get; init; }

    public int DivergentContributionReportCount { get; init; }

    public string? LatestRunId { get; init; }

    public string? LatestReportRunId { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimePackExecutionAuditRunEntry
{
    public string RunId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public ExecutionRunStatus RunStatus { get; init; } = ExecutionRunStatus.Planned;

    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public string? ArtifactRef { get; init; }

    public string SelectionMode { get; init; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool MatchesCurrentSelection { get; init; }

    public RuntimePackDeclarativeContribution? DeclarativeContribution { get; init; }

    public bool MatchesCurrentDeclarativeContribution { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimePackExecutionAuditReportEntry
{
    public string RunId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public ExecutionRunStatus RunStatus { get; init; } = ExecutionRunStatus.Planned;

    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public string? ArtifactRef { get; init; }

    public string SelectionMode { get; init; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool MatchesCurrentSelection { get; init; }

    public RuntimePackDeclarativeContribution? DeclarativeContribution { get; init; }

    public bool MatchesCurrentDeclarativeContribution { get; init; }

    public int FilesChanged { get; init; }

    public IReadOnlyList<string> ModulesTouched { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}
