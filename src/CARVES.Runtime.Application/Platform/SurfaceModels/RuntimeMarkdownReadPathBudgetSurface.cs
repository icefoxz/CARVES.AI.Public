namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeMarkdownReadPathBudgetSurface
{
    public string SchemaVersion { get; init; } = "runtime-markdown-read-path-budget.v1";

    public string SurfaceId { get; init; } = "runtime-markdown-read-path-budget";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string RepoRoot { get; init; } = string.Empty;

    public string PolicyId { get; init; } = "markdown-read-path-budget.v1";

    public string EstimatorVersion { get; init; } = "bytes_div_4.v1";

    public string OverallPosture { get; init; } = "markdown_read_path_budgeted";

    public bool WithinBudget { get; init; } = true;

    public string CommandEntry { get; init; } = "carves inspect runtime-markdown-read-path-budget [<task-id>]";

    public string JsonCommandEntry { get; init; } = "carves api runtime-markdown-read-path-budget [<task-id>]";

    public string RequestedTaskId { get; init; } = "N/A";

    public string ResolvedTaskId { get; init; } = "N/A";

    public string ResolvedTaskSource { get; init; } = "none";

    public MarkdownReadPathBudgetSummary ColdInitialization { get; init; } = new();

    public MarkdownReadPathBudgetSummary PostInitializationDefault { get; init; } = new();

    public MarkdownReadPathBudgetSummary GeneratedMarkdownViews { get; init; } = new();

    public MarkdownReadPathBudgetSummary TaskScopedMarkdown { get; init; } = new();

    public IReadOnlyList<MarkdownReadPathBudgetItem> Items { get; init; } = [];

    public IReadOnlyList<string> DefaultMachineReadPath { get; init; } = [];

    public IReadOnlyList<string> DeferredMarkdownSources { get; init; } = [];

    public IReadOnlyList<string> EscalationTriggers { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class MarkdownReadPathBudgetSummary
{
    public string BudgetId { get; init; } = string.Empty;

    public string TierId { get; init; } = string.Empty;

    public int MaxDefaultMarkdownTokens { get; init; }

    public int EstimatedDefaultMarkdownTokens { get; init; }

    public int DeferredMarkdownTokens { get; init; }

    public int MaxDefaultMarkdownSources { get; init; }

    public int DefaultMarkdownSourceCount { get; init; }

    public int LargestItemTokens { get; init; }

    public bool WithinBudget { get; init; } = true;

    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
}

public sealed class MarkdownReadPathBudgetItem
{
    public string Path { get; init; } = string.Empty;

    public string TierId { get; init; } = string.Empty;

    public string SourceKind { get; init; } = string.Empty;

    public string ReadAction { get; init; } = string.Empty;

    public string ReplacementSurface { get; init; } = string.Empty;

    public bool Exists { get; init; }

    public long ByteSize { get; init; }

    public int EstimatedTokens { get; init; }

    public bool OverSingleFileBudget { get; init; }

    public string Summary { get; init; } = string.Empty;
}
