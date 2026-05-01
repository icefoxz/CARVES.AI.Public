namespace Carves.Runtime.Domain.AI;

public sealed record ContextBudgetTelemetryRecord
{
    public string SchemaVersion { get; init; } = "context-budget-telemetry.v1";

    public string TelemetryId { get; init; } = string.Empty;

    public string OperationKind { get; init; } = string.Empty;

    public string ProfileId { get; init; } = string.Empty;

    public string ModelId { get; init; } = string.Empty;

    public string EstimatorVersion { get; init; } = string.Empty;

    public int FixedTokensEst { get; init; }

    public int DynamicTokensEst { get; init; }

    public int TotalContextTokensEst { get; init; }

    public string BudgetPosture { get; init; } = ContextBudgetPostures.WithinTarget;

    public IReadOnlyList<string> BudgetViolationReason { get; init; } = Array.Empty<string>();

    public int L3QueryCount { get; init; }

    public int EvidenceExpansionCount { get; init; }

    public int TruncatedItemsCount { get; init; }

    public int DroppedItemsCount { get; init; }

    public int FullDocBlockedCount { get; init; }

    public IReadOnlyList<string> TopSources { get; init; } = Array.Empty<string>();

    public string Outcome { get; init; } = string.Empty;

    public string? PackId { get; init; }

    public string? Audience { get; init; }

    public string? ArtifactPath { get; init; }

    public string? FacetPhase { get; init; }

    public string? TaskId { get; init; }

    public string? QueryText { get; init; }

    public int? BudgetTokens { get; init; }

    public int ResultCount { get; init; }

    public IReadOnlyList<string> IncludedItemIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<ContextBudgetTelemetryTrimmedItem> TrimmedItems { get; init; } = Array.Empty<ContextBudgetTelemetryTrimmedItem>();

    public IReadOnlyList<ContextBudgetTelemetryExpandableReference> ExpandableReferences { get; init; } = Array.Empty<ContextBudgetTelemetryExpandableReference>();

    public IReadOnlyList<string> SourcePaths { get; init; } = Array.Empty<string>();

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ContextBudgetTelemetryTrimmedItem
{
    public string ItemId { get; init; } = string.Empty;

    public string Layer { get; init; } = string.Empty;

    public string Priority { get; init; } = string.Empty;

    public int EstimatedTokens { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public sealed record ContextBudgetTelemetryExpandableReference
{
    public string Kind { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Layer { get; init; } = string.Empty;
}
