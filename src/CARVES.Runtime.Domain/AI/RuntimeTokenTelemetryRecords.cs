namespace Carves.Runtime.Domain.AI;

public sealed record RenderedPromptSection
{
    public string SectionId { get; init; } = string.Empty;

    public string SectionKind { get; init; } = string.Empty;

    public string? SourceItemId { get; init; }

    public string RendererVersion { get; init; } = "prose_v1";

    public int StartChar { get; init; }

    public int EndChar { get; init; }
}

public sealed record LlmRequestEnvelopeSegmentDraft
{
    public string SegmentId { get; init; } = string.Empty;

    public string SegmentKind { get; init; } = string.Empty;

    public string? SegmentParentId { get; init; }

    public int SegmentOrder { get; init; }

    public int? MessageIndex { get; init; }

    public string? Role { get; init; }

    public string PayloadPath { get; init; } = string.Empty;

    public string SerializationKind { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public bool Included { get; init; } = true;

    public bool Trimmed { get; init; }

    public int? TrimBeforeTokensEst { get; init; }

    public int? TrimAfterTokensEst { get; init; }

    public string? SourceItemId { get; init; }

    public string RendererVersion { get; init; } = "runtime_request_serializer.v1";
}

public sealed record LlmRequestEnvelopeDraft
{
    public string RequestId { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public string RequestKindEnumVersion { get; init; } = "runtime_request_kind.v1";

    public string Model { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string ProviderApiVersion { get; init; } = "n/a";

    public string Tokenizer { get; init; } = string.Empty;

    public string RequestSerializerVersion { get; init; } = string.Empty;

    public string? RunId { get; init; }

    public string? TaskId { get; init; }

    public string? PackId { get; init; }

    public string? ParentRequestId { get; init; }

    public string WholeRequestText { get; init; } = string.Empty;

    public IReadOnlyList<LlmRequestEnvelopeSegmentDraft> Segments { get; init; } = Array.Empty<LlmRequestEnvelopeSegmentDraft>();
}

public sealed record LlmRequestEnvelopeUsage
{
    public string TokenAccountingSource { get; init; } = "local_estimate";

    public int? ProviderReportedInputTokens { get; init; }

    public int? ProviderReportedCachedInputTokens { get; init; }

    public int? ProviderReportedUncachedInputTokens { get; init; }

    public int? ProviderReportedOutputTokens { get; init; }

    public int? ProviderReportedReasoningTokens { get; init; }

    public int? ProviderReportedTotalTokens { get; init; }

    public bool ReasoningTokensReportedSeparately { get; init; }

    public bool ReasoningTokensIncludedInOutput { get; init; }

    public bool ProviderTotalIncludesReasoning { get; init; }

    public decimal? EstimatedCostUsd { get; init; }

    public decimal? ProviderReportedCostUsdIfAvailable { get; init; }

    public string PricingVersion { get; init; } = string.Empty;

    public string PricingSource { get; init; } = string.Empty;

    public string CostEstimationVersion { get; init; } = string.Empty;

    public string? KnownProviderOverheadClass { get; init; }

    public bool? ProviderContextCapHit { get; init; }

    public bool? InternalPromptBudgetCapHit { get; init; }

    public bool? SectionBudgetCapHit { get; init; }

    public bool? TrimLoopCapHit { get; init; }

    public string? CapTriggerSegmentKind { get; init; }

    public string? CapTriggerSource { get; init; }
}

public sealed record LlmRequestEnvelopeTelemetrySegment
{
    public int? MessageIndex { get; init; }

    public string? Role { get; init; }

    public string PayloadPath { get; init; } = string.Empty;

    public string? SegmentParentId { get; init; }

    public int SegmentOrder { get; init; }

    public string SegmentId { get; init; } = string.Empty;

    public string SegmentKind { get; init; } = string.Empty;

    public string SerializationKind { get; init; } = string.Empty;

    public int Chars { get; init; }

    public int TokensEst { get; init; }

    public bool Included { get; init; } = true;

    public bool Trimmed { get; init; }

    public int? TrimBeforeTokensEst { get; init; }

    public int? TrimAfterTokensEst { get; init; }

    public string? SourceItemId { get; init; }

    public string ContentHash { get; init; } = string.Empty;

    public string HashMode { get; init; } = string.Empty;

    public string HashSaltScope { get; init; } = string.Empty;

    public string? HmacKeyId { get; init; }

    public string HashAlgorithm { get; init; } = string.Empty;

    public string NormalizationVersion { get; init; } = string.Empty;

    public string RendererVersion { get; init; } = string.Empty;
}

public sealed record LlmRequestEnvelopeTelemetryRecord
{
    public string SchemaVersion { get; init; } = "llm-request-envelope-attribution.v1";

    public string AttributionId { get; init; } = string.Empty;

    public string RequestId { get; init; } = string.Empty;

    public string RequestKind { get; init; } = string.Empty;

    public string RequestKindEnumVersion { get; init; } = "runtime_request_kind.v1";

    public string Model { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string ProviderApiVersion { get; init; } = "n/a";

    public string Tokenizer { get; init; } = string.Empty;

    public string RequestSerializerVersion { get; init; } = string.Empty;

    public string TokenAccountingSource { get; init; } = "local_estimate";

    public string? RunId { get; init; }

    public string? TaskId { get; init; }

    public string? PackId { get; init; }

    public string? ParentRequestId { get; init; }

    public int WholeRequestTokensEst { get; init; }

    public int SumSegmentTokensEst { get; init; }

    public int UnattributedTokensEst { get; init; }

    public string? KnownProviderOverheadClass { get; init; }

    public int? ContextWindowInputTokensTotal { get; init; }

    public int? BillableInputTokensUncached { get; init; }

    public int? CachedInputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public int? ReasoningTokens { get; init; }

    public int? TotalContextTokensPerRequest { get; init; }

    public int? TotalBillableTokensPerRequest { get; init; }

    public int? ProviderReportedInputTokens { get; init; }

    public int? ProviderReportedCachedInputTokens { get; init; }

    public int? ProviderReportedUncachedInputTokens { get; init; }

    public int? ProviderReportedOutputTokens { get; init; }

    public int? ProviderReportedReasoningTokens { get; init; }

    public int? ProviderReportedTotalTokens { get; init; }

    public bool ReasoningTokensReportedSeparately { get; init; }

    public bool ReasoningTokensIncludedInOutput { get; init; }

    public bool ProviderTotalIncludesReasoning { get; init; }

    public decimal? EstimatedCostUsd { get; init; }

    public decimal? ProviderReportedCostUsdIfAvailable { get; init; }

    public string PricingVersion { get; init; } = string.Empty;

    public string PricingSource { get; init; } = string.Empty;

    public string CostEstimationVersion { get; init; } = string.Empty;

    public bool? ProviderContextCapHit { get; init; }

    public bool? InternalPromptBudgetCapHit { get; init; }

    public bool? SectionBudgetCapHit { get; init; }

    public bool? TrimLoopCapHit { get; init; }

    public string? CapTriggerSegmentKind { get; init; }

    public string? CapTriggerSource { get; init; }

    public IReadOnlyList<LlmRequestEnvelopeTelemetrySegment> Segments { get; init; } = Array.Empty<LlmRequestEnvelopeTelemetrySegment>();

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RuntimeConsumerRouteSurfaceRecord
{
    public string SurfaceId { get; init; } = string.Empty;

    public string Producer { get; init; } = string.Empty;

    public string SurfaceKind { get; init; } = string.Empty;

    public string ContentHash { get; init; } = string.Empty;

    public string HashMode { get; init; } = string.Empty;

    public string HashSaltScope { get; init; } = string.Empty;

    public string? HmacKeyId { get; init; }

    public string HashAlgorithm { get; init; } = string.Empty;

    public string NormalizationVersion { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RuntimeConsumerRouteEdgeRecord
{
    public string SurfaceId { get; init; } = string.Empty;

    public string Consumer { get; init; } = string.Empty;

    public string DeclaredRouteKind { get; init; } = string.Empty;

    public string ObservedRouteKind { get; init; } = string.Empty;

    public int ObservedCount { get; init; }

    public string FrequencyWindow { get; init; } = "7d";

    public int SampleCount { get; init; }

    public DateTimeOffset? LastSeen { get; init; }

    public int RetrievalHitCount { get; init; }

    public int LlmReinjectionCount { get; init; }

    public double AverageFanout { get; init; }

    public string EvidenceSource { get; init; } = string.Empty;
}
