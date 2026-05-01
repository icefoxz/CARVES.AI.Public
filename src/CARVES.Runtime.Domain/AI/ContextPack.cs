using System.Text.Json.Serialization;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Domain.AI;

[JsonConverter(typeof(JsonStringEnumConverter<ContextPackAudience>))]
public enum ContextPackAudience
{
    Worker = 0,
    Planner = 1,
}

[JsonConverter(typeof(JsonStringEnumConverter<ContextPackLayer>))]
public enum ContextPackLayer
{
    Core = 0,
    Relevant = 1,
    Expandable = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<ContextPackPriority>))]
public enum ContextPackPriority
{
    Task = 0,
    Goal = 1,
    Failure = 2,
    Modules = 3,
    History = 4,
}

public static class ContextBudgetPostures
{
    public const string WithinTarget = "within_target";
    public const string OverTarget = "over_target";
    public const string OverAdvisory = "over_advisory";
    public const string HardCapEnforced = "hard_cap_enforced";
    public const string DegradedContext = "degraded_context";
}

public static class ContextBudgetReasonCodes
{
    public const string UserSuppliedContext = "user_supplied_context";
    public const string RepoScale = "repo_scale";
    public const string TaskComplexity = "task_complexity";
    public const string EvidenceRequired = "evidence_required";
    public const string ReviewBundleRequired = "review_bundle_required";
    public const string FullDocBlocked = "full_doc_blocked";
    public const string EstimatorUncertain = "estimator_uncertain";
}

public sealed record ContextPackScopeItem
{
    public string TaskId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? Summary { get; init; }
}

public sealed record TaskGraphLocalProjection
{
    public string CurrentTaskId { get; init; } = string.Empty;

    public string CurrentTaskTitle { get; init; } = string.Empty;

    public IReadOnlyList<ContextPackScopeItem> Dependencies { get; init; } = Array.Empty<ContextPackScopeItem>();

    public IReadOnlyList<ContextPackScopeItem> Blockers { get; init; } = Array.Empty<ContextPackScopeItem>();
}

public sealed record ContextPackModuleProjection
{
    public string Module { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
}

public sealed record ContextPackArtifactReference
{
    public string Kind { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public ContextPackLayer Layer { get; init; } = ContextPackLayer.Expandable;
}

public sealed record ContextPackFacetNarrowing
{
    public string Repo { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public string? CardId { get; init; }

    public string Phase { get; init; } = string.Empty;

    public IReadOnlyList<string> Modules { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ScopeFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ArtifactTypes { get; init; } = Array.Empty<string>();
}

public sealed record ContextPackRecallItem
{
    public string Kind { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public double Score { get; init; }

    public int Chars { get; init; }

    public int TokenEstimate { get; init; }

    public string Text { get; init; } = string.Empty;
}

public sealed record ContextPackWindowedRead
{
    public string Path { get; init; } = string.Empty;

    public int TotalLines { get; init; }

    public int StartLine { get; init; } = 1;

    public int EndLine { get; init; }

    public string Reason { get; init; } = string.Empty;

    public bool Truncated { get; init; }

    public string Snippet { get; init; } = string.Empty;
}

public sealed record CompactFailureSummary
{
    public string FailureType { get; init; } = string.Empty;

    public string FailureLane { get; init; } = string.Empty;

    public string? AffectedFile { get; init; }

    public string? AffectedModule { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string BuildStatus { get; init; } = "unknown";

    public string TestStatus { get; init; } = "unknown";

    public string RuntimeStatus { get; init; } = "unknown";

    public IReadOnlyList<string> ArtifactReferences { get; init; } = Array.Empty<string>();
}

public sealed record ExecutionHistorySummary
{
    public string RunId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string? BoundaryReason { get; init; }

    public string? ReplanStrategy { get; init; }
}

public sealed record ContextPackBudget
{
    public string PolicyVersion { get; init; } = "context-pack-budget.v2";

    public string ProfileId { get; init; } = "worker";

    public string Model { get; init; } = string.Empty;

    public string EstimatorVersion { get; init; } = "chars_div_4.v1";

    public int ModelLimitTokens { get; init; }

    public int TargetTokens { get; init; }

    public int AdvisoryTokens { get; init; }

    public int HardSafetyTokens { get; init; }

    public int MaxContextTokens { get; init; }

    public int ReservedHeadroomTokens { get; init; }

    public int CoreBudgetTokens { get; init; }

    public int RelevantBudgetTokens { get; init; }

    public int UsedTokens { get; init; }

    public int TrimmedTokens { get; init; }

    public int FixedTokensEstimate { get; init; }

    public int DynamicTokensEstimate { get; init; }

    public int TotalContextTokensEstimate { get; init; }

    public string BudgetPosture { get; init; } = ContextBudgetPostures.WithinTarget;

    public IReadOnlyList<string> BudgetViolationReasonCodes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ContextPackBudgetContributor> LargestContributors { get; init; } = Array.Empty<ContextPackBudgetContributor>();

    public int L3QueryCount { get; init; }

    public int EvidenceExpansionCount { get; init; }

    public int TruncatedItemsCount { get; init; }

    public int DroppedItemsCount { get; init; }

    public int FullDocBlockedCount { get; init; }

    public IReadOnlyList<string> TopSources { get; init; } = Array.Empty<string>();
}

public sealed record ContextPackBudgetContributor
{
    public string Kind { get; init; } = string.Empty;

    public int EstimatedTokens { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed record ContextPackTrimmedItem
{
    public string Key { get; init; } = string.Empty;

    public ContextPackLayer Layer { get; init; } = ContextPackLayer.Relevant;

    public ContextPackPriority Priority { get; init; } = ContextPackPriority.Modules;

    public int EstimatedTokens { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public sealed record ContextPackCompaction
{
    public string Strategy { get; init; } = "bounded_scope_projection";

    public int CandidateFileCount { get; init; }

    public int RelevantFileCount { get; init; }

    public int WindowedReadCount { get; init; }

    public int FullReadCount { get; init; }

    public int OmittedFileCount { get; init; }

    public int TrimmedItemCount { get; init; }
}

public sealed record ContextPack
{
    public string SchemaVersion { get; init; } = "context-pack.v2";

    public string PackId { get; init; } = string.Empty;

    public ContextPackAudience Audience { get; init; } = ContextPackAudience.Worker;

    public string? ArtifactPath { get; init; }

    public string? TaskId { get; init; }

    public string Goal { get; init; } = string.Empty;

    public string Task { get; init; } = string.Empty;

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    public AcceptanceContract? AcceptanceContract { get; init; }

    public TaskGraphLocalProjection LocalTaskGraph { get; init; } = new();

    public IReadOnlyList<ContextPackModuleProjection> RelevantModules { get; init; } = Array.Empty<ContextPackModuleProjection>();

    public ContextPackFacetNarrowing FacetNarrowing { get; init; } = new();

    public IReadOnlyList<ContextPackRecallItem> Recall { get; init; } = Array.Empty<ContextPackRecallItem>();

    public IReadOnlyList<string> CodeHints { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ContextPackWindowedRead> WindowedReads { get; init; } = Array.Empty<ContextPackWindowedRead>();

    public CompactFailureSummary? LastFailureSummary { get; init; }

    public ExecutionHistorySummary? LastRunSummary { get; init; }

    public IReadOnlyList<ContextPackArtifactReference> ExpandableReferences { get; init; } = Array.Empty<ContextPackArtifactReference>();

    public ContextPackBudget Budget { get; init; } = new();

    public IReadOnlyList<ContextPackTrimmedItem> Trimmed { get; init; } = Array.Empty<ContextPackTrimmedItem>();

    public ContextPackCompaction Compaction { get; init; } = new();

    public string PromptInput { get; init; } = string.Empty;

    public IReadOnlyList<RenderedPromptSection> PromptSections { get; init; } = Array.Empty<RenderedPromptSection>();
}
