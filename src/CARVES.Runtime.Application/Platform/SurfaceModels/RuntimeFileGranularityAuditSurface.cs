namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeFileGranularityAuditSurface
{
    public string SchemaVersion { get; init; } = "runtime-file-granularity-audit.v1";
    public string SurfaceId { get; init; } = "runtime-file-granularity-audit";
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;
    public string RepoRoot { get; init; } = string.Empty;
    public string LifecycleClass { get; init; } = "active_maintenance_audit";
    public string ReadPathClass { get; init; } = "conditional_maintenance_audit";
    public string DefaultPathParticipation { get; init; } = "not_default";
    public bool IsValid { get; init; }
    public string OverallPosture { get; init; } = string.Empty;
    public IReadOnlyList<string> ScanRoots { get; init; } = [];
    public IReadOnlyList<string> ExcludedDirectoryNames { get; init; } = [];
    public RuntimeFileGranularityThresholdsSurface Thresholds { get; init; } = new();
    public RuntimeFileGranularityCountsSurface Counts { get; init; } = new();
    public IReadOnlyList<RuntimeFileGranularityProjectRollupSurface> ProjectRollups { get; init; } = [];
    public IReadOnlyList<RuntimeFileGranularityDirectoryRollupSurface> DirectoryPressure { get; init; } = [];
    public IReadOnlyList<RuntimeFileGranularityTinyClusterSurface> TinyClusters { get; init; } = [];
    public IReadOnlyList<RuntimeFileGranularityPartialClusterSurface> PartialFamilyClusters { get; init; } = [];
    public IReadOnlyList<RuntimeFileGranularityFileSurface> LargestFiles { get; init; } = [];
    public IReadOnlyList<RuntimeFileGranularityFileSurface> SmallestFiles { get; init; } = [];
    public IReadOnlyList<RuntimeFileGranularityCleanupCandidateSurface> CleanupCandidates { get; init; } = [];
    public RuntimeFileGranularityCleanupSelectionSurface CleanupSelection { get; init; } = new();
    public IReadOnlyList<string> Findings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string Summary { get; init; } = string.Empty;
    public string RecommendedNextAction { get; init; } = string.Empty;
    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeFileGranularityThresholdsSurface
{
    public int TinyFileLineThreshold { get; init; }
    public int SmallFileLineThreshold { get; init; }
    public int LargeFileLineThreshold { get; init; }
    public int HugeFileLineThreshold { get; init; }
    public int TinyClusterMinimumFileCount { get; init; }
    public int PartialClusterMinimumFileCount { get; init; }
}

public sealed class RuntimeFileGranularityCountsSurface
{
    public int TotalFileCount { get; init; }
    public int SourceFileCount { get; init; }
    public int TestFileCount { get; init; }
    public int TotalPhysicalLineCount { get; init; }
    public int TotalNonBlankLineCount { get; init; }
    public double AveragePhysicalLinesPerFile { get; init; }
    public double MedianPhysicalLinesPerFile { get; init; }
    public int TinyFileCount { get; init; }
    public int SmallFileCount { get; init; }
    public int MediumFileCount { get; init; }
    public int LargeFileCount { get; init; }
    public int HugeFileCount { get; init; }
    public double TinyFileRatio { get; init; }
    public double SmallFileRatio { get; init; }
    public int TinyClusterCount { get; init; }
    public int PartialFamilyClusterCount { get; init; }
    public int DirectoryPressureCount { get; init; }
}

public sealed class RuntimeFileGranularityProjectRollupSurface
{
    public string ProjectRoot { get; init; } = string.Empty;
    public int FileCount { get; init; }
    public int TotalPhysicalLineCount { get; init; }
    public double AveragePhysicalLinesPerFile { get; init; }
    public int TinyFileCount { get; init; }
    public int SmallFileCount { get; init; }
    public int LargeFileCount { get; init; }
    public int HugeFileCount { get; init; }
}

public sealed class RuntimeFileGranularityDirectoryRollupSurface
{
    public string DirectoryPath { get; init; } = string.Empty;
    public int FileCount { get; init; }
    public int TotalPhysicalLineCount { get; init; }
    public double AveragePhysicalLinesPerFile { get; init; }
    public int TinyFileCount { get; init; }
    public int SmallFileCount { get; init; }
    public int LargeFileCount { get; init; }
    public int HugeFileCount { get; init; }
    public int PressureScore { get; init; }
    public IReadOnlyList<string> ExampleFiles { get; init; } = [];
}

public sealed class RuntimeFileGranularityTinyClusterSurface
{
    public string ClusterId { get; init; } = string.Empty;
    public string DirectoryPath { get; init; } = string.Empty;
    public int TinyFileCount { get; init; }
    public int SmallFileCount { get; init; }
    public double AveragePhysicalLinesPerTinyFile { get; init; }
    public string ReviewPosture { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public IReadOnlyList<string> ExampleFiles { get; init; } = [];
}

public sealed class RuntimeFileGranularityPartialClusterSurface
{
    public string ClusterId { get; init; } = string.Empty;
    public string DirectoryPath { get; init; } = string.Empty;
    public string FamilyName { get; init; } = string.Empty;
    public int FileCount { get; init; }
    public int TotalPhysicalLineCount { get; init; }
    public double AveragePhysicalLinesPerFile { get; init; }
    public int TinyFileCount { get; init; }
    public string ReviewPosture { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public IReadOnlyList<string> Files { get; init; } = [];
}

public sealed class RuntimeFileGranularityFileSurface
{
    public string Path { get; init; } = string.Empty;
    public int PhysicalLineCount { get; init; }
    public int NonBlankLineCount { get; init; }
    public string FileClass { get; init; } = string.Empty;
    public string SuggestedReviewPosture { get; init; } = string.Empty;
}

public sealed class RuntimeFileGranularityCleanupCandidateSurface
{
    public string CandidateId { get; init; } = string.Empty;
    public string CandidateType { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string ScopePath { get; init; } = string.Empty;
    public int PressureScore { get; init; }
    public string Rationale { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public string ValidationHint { get; init; } = string.Empty;
    public IReadOnlyList<string> Files { get; init; } = [];
    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeFileGranularityCleanupSelectionSurface
{
    public string SelectionId { get; init; } = "runtime-file-granularity-cleanup-selection";
    public string Strategy { get; init; } = "bounded_low_risk_first";
    public int MaxBatchSize { get; init; }
    public int CandidateCount { get; init; }
    public int SelectedCandidateCount { get; init; }
    public int DeferredCandidateCount { get; init; }
    public IReadOnlyList<string> EligibilityRules { get; init; } = [];
    public IReadOnlyList<string> DeferralRules { get; init; } = [];
    public IReadOnlyList<RuntimeFileGranularitySelectedCleanupCandidateSurface> SelectedCandidates { get; init; } = [];
    public IReadOnlyList<string> DeferredCandidateIds { get; init; } = [];
    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeFileGranularitySelectedCleanupCandidateSurface
{
    public int SelectionRank { get; init; }
    public string CandidateId { get; init; } = string.Empty;
    public string CandidateType { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string RiskClass { get; init; } = string.Empty;
    public string ScopePath { get; init; } = string.Empty;
    public int PressureScore { get; init; }
    public string SelectionReason { get; init; } = string.Empty;
    public string RecommendedAction { get; init; } = string.Empty;
    public string ValidationHint { get; init; } = string.Empty;
    public IReadOnlyList<string> Files { get; init; } = [];
}
