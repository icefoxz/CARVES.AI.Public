using System.Text.Json.Serialization;

namespace Carves.Runtime.Domain.Evidence;

[JsonConverter(typeof(JsonStringEnumConverter<RuntimeEvidenceKind>))]
public enum RuntimeEvidenceKind
{
    ContextPack = 0,
    ExecutionRun = 1,
    Review = 2,
    Planning = 3,
}

public sealed record RuntimeEvidenceRecord
{
    public string SchemaVersion { get; init; } = "runtime-evidence-record.v1";

    public string EvidenceId { get; init; } = string.Empty;

    public RuntimeEvidenceKind Kind { get; init; } = RuntimeEvidenceKind.ContextPack;

    public string Tier { get; init; } = "raw_evidence";

    public string Producer { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string? TaskId { get; init; }

    public string? CardId { get; init; }

    public string? RunId { get; init; }

    public string? SessionId { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Excerpt { get; init; } = string.Empty;

    public bool ExcerptTruncated { get; init; }

    public IReadOnlyList<string> ArtifactPaths { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> SourceEvidenceIds { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Lineage { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RuntimeEvidenceSearchResult
{
    public IReadOnlyList<RuntimeEvidenceRecord> Records { get; init; } = Array.Empty<RuntimeEvidenceRecord>();

    public int BudgetTokens { get; init; }

    public int UsedTokens { get; init; }

    public int DroppedRecords { get; init; }

    public IReadOnlyList<string> TopSources { get; init; } = Array.Empty<string>();
}
