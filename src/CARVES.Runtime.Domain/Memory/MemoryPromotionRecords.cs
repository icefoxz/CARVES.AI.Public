using System.Text.Json.Serialization;

namespace Carves.Runtime.Domain.Memory;

[JsonConverter(typeof(JsonStringEnumConverter<MemoryKnowledgeTier>))]
public enum MemoryKnowledgeTier
{
    ProvisionalMemory = 0,
    CanonicalMemory = 1,
}

[JsonConverter(typeof(JsonStringEnumConverter<MemoryPromotionAuditDecision>))]
public enum MemoryPromotionAuditDecision
{
    Approved = 0,
    Rejected = 1,
}

[JsonConverter(typeof(JsonStringEnumConverter<TemporalMemoryFactStatus>))]
public enum TemporalMemoryFactStatus
{
    Active = 0,
    Superseded = 1,
    Invalidated = 2,
}

[JsonConverter(typeof(JsonStringEnumConverter<MemoryPromotionAction>))]
public enum MemoryPromotionAction
{
    PromoteToProvisional = 0,
    PromoteToCanonical = 1,
    Invalidate = 2,
}

public sealed record MemoryPromotionCandidateRecord
{
    public string SchemaVersion { get; init; } = "memory-promotion-candidate.v1";

    public string CandidateId { get; init; } = string.Empty;

    public string Category { get; init; } = "project";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Statement { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string? TaskScope { get; init; }

    public string? CommitScope { get; init; }

    public string? TargetMemoryPath { get; init; }

    public IReadOnlyList<string> SourceEvidenceIds { get; init; } = Array.Empty<string>();

    public string Proposer { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record MemoryPromotionAuditRecord
{
    public string SchemaVersion { get; init; } = "memory-promotion-audit.v1";

    public string AuditId { get; init; } = string.Empty;

    public string? CandidateId { get; init; }

    public string? FactId { get; init; }

    public MemoryPromotionAuditDecision Decision { get; init; } = MemoryPromotionAuditDecision.Approved;

    public string Auditor { get; init; } = string.Empty;

    public string Rationale { get; init; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record TemporalMemoryFactRecord
{
    public string SchemaVersion { get; init; } = "temporal-memory-fact.v1";

    public string FactId { get; init; } = string.Empty;

    public MemoryKnowledgeTier Tier { get; init; } = MemoryKnowledgeTier.ProvisionalMemory;

    public TemporalMemoryFactStatus Status { get; init; } = TemporalMemoryFactStatus.Active;

    public string Category { get; init; } = "project";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Statement { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string? TaskScope { get; init; }

    public string? CommitScope { get; init; }

    public string? TargetMemoryPath { get; init; }

    public IReadOnlyList<string> SourceEvidenceIds { get; init; } = Array.Empty<string>();

    public string? SourceCandidateId { get; init; }

    public string? SourceFactId { get; init; }

    public string? PromotionRecordId { get; init; }

    public string ProposedBy { get; init; } = string.Empty;

    public string PromotedBy { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public IReadOnlyList<string> Supersedes { get; init; } = Array.Empty<string>();

    public string? SupersededByFactId { get; init; }

    public string? InvalidatedByAuditId { get; init; }

    public string? InvalidatedReason { get; init; }

    public DateTimeOffset ValidFromUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ValidToUtc { get; init; }

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record MemoryPromotionRecord
{
    public string SchemaVersion { get; init; } = "memory-promotion-record.v1";

    public string PromotionId { get; init; } = string.Empty;

    public MemoryPromotionAction Action { get; init; } = MemoryPromotionAction.PromoteToProvisional;

    public string? CandidateId { get; init; }

    public string? SourceFactId { get; init; }

    public string? AuditId { get; init; }

    public string? ResultFactId { get; init; }

    public MemoryKnowledgeTier? ResultTier { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Actor { get; init; } = string.Empty;

    public IReadOnlyList<string> Supersedes { get; init; } = Array.Empty<string>();

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
