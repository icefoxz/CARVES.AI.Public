namespace Carves.Runtime.Domain.Platform;

public enum RuntimeArtifactClass
{
    CanonicalTruth,
    GovernedMirror,
    DerivedTruth,
    OperationalHistory,
    LiveState,
    EphemeralResidue,
    AuditArchive,
}

public enum RuntimeArtifactRetentionMode
{
    Permanent,
    SingleVersion,
    RollingWindow,
    AutoExpire,
    ArchiveSummary,
}

public enum RuntimeArtifactReadVisibility
{
    Summary,
    OnDemandDetail,
    Hidden,
    ArchiveOnly,
}

public enum RuntimeMaintenanceActionKind
{
    None,
    RebuildDerived,
    CompactHistory,
    PruneEphemeral,
}

public sealed class RuntimeArtifactBudgetPolicy
{
    public int? MaxOnlineFiles { get; init; }

    public long? MaxOnlineBytes { get; init; }

    public int? HotWindowCount { get; init; }

    public int? MaxAgeDays { get; init; }
}

public sealed class RuntimeArtifactFamilyPolicy
{
    public string FamilyId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public RuntimeArtifactClass ArtifactClass { get; init; } = RuntimeArtifactClass.DerivedTruth;

    public RuntimeArtifactRetentionMode RetentionMode { get; init; } = RuntimeArtifactRetentionMode.SingleVersion;

    public RuntimeArtifactReadVisibility DefaultReadVisibility { get; init; } = RuntimeArtifactReadVisibility.Hidden;

    public bool CleanupEligible { get; init; }

    public bool CompactEligible { get; init; }

    public bool RebuildEligible { get; init; }

    public string[] Roots { get; init; } = [];

    public string[] AllowedContents { get; init; } = [];

    public string[] ForbiddenContents { get; init; } = [];

    public RuntimeArtifactBudgetPolicy Budget { get; init; } = new();

    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeArtifactCatalog
{
    public string SchemaVersion { get; init; } = "runtime-artifact-catalog.v6";

    public string CatalogId { get; init; } = "runtime-artifact-catalog";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public RuntimeArtifactFamilyPolicy[] Families { get; init; } = [];
}

public sealed class RuntimeArtifactBudgetProjection
{
    public string FamilyId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public RuntimeArtifactClass ArtifactClass { get; init; } = RuntimeArtifactClass.DerivedTruth;

    public RuntimeArtifactRetentionMode RetentionMode { get; init; } = RuntimeArtifactRetentionMode.SingleVersion;

    public RuntimeArtifactReadVisibility DefaultReadVisibility { get; init; } = RuntimeArtifactReadVisibility.Hidden;

    public int? HotWindowCount { get; init; }

    public int? MaxAgeDays { get; init; }

    public string RetentionDiscipline { get; init; } = string.Empty;

    public string ClosureDiscipline { get; init; } = string.Empty;

    public string ArchiveReadinessState { get; init; } = string.Empty;

    public int FileCount { get; init; }

    public long TotalBytes { get; init; }

    public int RetentionOverdueCount { get; init; }

    public int ReadPathPressureCount { get; init; }

    public int HotWindowExcessCount { get; init; }

    public int? OldestItemAgeDays { get; init; }

    public bool OverFileBudget { get; init; }

    public bool OverByteBudget { get; init; }

    public bool WithinBudget { get; init; } = true;

    public RuntimeMaintenanceActionKind RecommendedAction { get; init; } = RuntimeMaintenanceActionKind.None;

    public string Summary { get; init; } = string.Empty;
}

public sealed class SustainabilityAuditReport
{
    public string SchemaVersion { get; init; } = "runtime-sustainability-audit.v1";

    public string AuditId { get; init; } = $"runtime-sustainability-{Guid.NewGuid():N}";

    public string CatalogId { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool StrictPassed { get; init; }

    public SustainabilityAuditFinding[] Findings { get; init; } = [];

    public RuntimeArtifactBudgetProjection[] Families { get; init; } = [];
}

public sealed class SustainabilityAuditFinding
{
    public string Category { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string FamilyId { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public RuntimeMaintenanceActionKind RecommendedAction { get; init; } = RuntimeMaintenanceActionKind.None;
}

public sealed class OperationalHistoryArchiveIndex
{
    public string SchemaVersion { get; init; } = "operational-history-archive-index.v1";

    public string ArchiveId { get; init; } = "operational-history-archive";

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public OperationalHistoryArchiveEntry[] Entries { get; init; } = [];
}

public sealed class OperationalHistoryArchiveEntry
{
    public string EntryId { get; init; } = $"archive-entry-{Guid.NewGuid():N}";

    public string FamilyId { get; init; } = string.Empty;

    public string OriginalPath { get; init; } = string.Empty;

    public string ArchivedPath { get; init; } = string.Empty;

    public DateTimeOffset ArchivedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SourceLastWriteAt { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class OperationalHistoryCompactionFamily
{
    public string FamilyId { get; init; } = string.Empty;

    public int ArchivedFileCount { get; init; }

    public int PreservedHotFileCount { get; init; }

    public int ArchiveEntryCount { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class OperationalHistoryCompactionReport
{
    public string SchemaVersion { get; init; } = "operational-history-compaction.v1";

    public string ReportId { get; init; } = $"operational-history-compaction-{Guid.NewGuid():N}";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ArchiveRoot { get; init; } = string.Empty;

    public int ArchivedFileCount { get; init; }

    public int PreservedHotFileCount { get; init; }

    public OperationalHistoryCompactionFamily[] Families { get; init; } = [];

    public OperationalHistoryArchiveEntry[] ArchivedEntries { get; init; } = [];

    public string[] Actions { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}

public sealed class OperationalHistoryArchiveReadinessFamily
{
    public string FamilyId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public RuntimeArtifactRetentionMode RetentionMode { get; init; } = RuntimeArtifactRetentionMode.RollingWindow;

    public int? HotWindowCount { get; init; }

    public int? MaxAgeDays { get; init; }

    public string RetentionDiscipline { get; init; } = string.Empty;

    public string ClosureDiscipline { get; init; } = string.Empty;

    public string ArchiveReadinessState { get; init; } = string.Empty;

    public int ArchivedFileCount { get; init; }

    public int PreservedHotFileCount { get; init; }

    public string ArchiveReason { get; init; } = string.Empty;

    public int PromotionRelevantCount { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class OperationalHistoryArchiveReadinessEntry
{
    public string EntryId { get; init; } = string.Empty;

    public string FamilyId { get; init; } = string.Empty;

    public string OriginalPath { get; init; } = string.Empty;

    public string ArchivedPath { get; init; } = string.Empty;

    public DateTimeOffset ArchivedAt { get; init; }

    public string WhyArchived { get; init; } = string.Empty;

    public string ArchiveReadinessState { get; init; } = string.Empty;

    public bool PromotionRelevant { get; init; }

    public string PromotionReason { get; init; } = string.Empty;
}

public sealed class OperationalHistoryArchiveReadinessReport
{
    public string SchemaVersion { get; init; } = "operational-history-archive-readiness.v1";

    public string ReportId { get; init; } = $"operational-history-archive-readiness-{Guid.NewGuid():N}";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ArchiveRoot { get; init; } = string.Empty;

    public string? SourceCompactionReportId { get; init; }

    public OperationalHistoryArchiveReadinessFamily[] Families { get; init; } = [];

    public OperationalHistoryArchiveReadinessEntry[] PromotionRelevantEntries { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}

public sealed class OperationalHistoryArchiveFollowUpGroup
{
    public string GroupId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int ItemCount { get; init; }

    public string RecommendedAction { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class OperationalHistoryArchiveFollowUpEntry
{
    public string FollowUpId { get; init; } = string.Empty;

    public string GroupId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string OriginalPath { get; init; } = string.Empty;

    public DateTimeOffset ArchivedAt { get; init; }

    public string WhyArchived { get; init; } = string.Empty;

    public string PromotionReason { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class OperationalHistoryArchiveFollowUpQueue
{
    public string SchemaVersion { get; init; } = "operational-history-archive-followup.v1";

    public string QueueId { get; init; } = $"operational-history-archive-followup-{Guid.NewGuid():N}";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string? SourceArchiveReadinessReportId { get; init; }

    public OperationalHistoryArchiveFollowUpGroup[] Groups { get; init; } = [];

    public OperationalHistoryArchiveFollowUpEntry[] Entries { get; init; } = [];

    public string Summary { get; init; } = string.Empty;
}
