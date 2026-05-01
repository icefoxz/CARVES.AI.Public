using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class LlmRequestEnvelopeAttributionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public LlmRequestEnvelopeAttributionService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public LlmRequestEnvelopeTelemetryRecord Record(LlmRequestEnvelopeDraft draft, LlmRequestEnvelopeUsage usage)
    {
        Directory.CreateDirectory(paths.RuntimeRequestEnvelopeAttributionRoot);

        var sumSegmentTokensEst = 0;
        var segments = draft.Segments
            .Select(segment =>
            {
                var normalized = RuntimeTelemetryHashing.Normalize(segment.Content);
                var chars = normalized.Length;
                var tokensEst = ContextBudgetPolicyResolver.EstimateTokens(normalized);
                sumSegmentTokensEst += tokensEst;
                return new LlmRequestEnvelopeTelemetrySegment
                {
                    MessageIndex = segment.MessageIndex,
                    Role = segment.Role,
                    PayloadPath = segment.PayloadPath,
                    SegmentParentId = segment.SegmentParentId,
                    SegmentOrder = segment.SegmentOrder,
                    SegmentId = segment.SegmentId,
                    SegmentKind = segment.SegmentKind,
                    SerializationKind = segment.SerializationKind,
                    Chars = chars,
                    TokensEst = tokensEst,
                    Included = segment.Included,
                    Trimmed = segment.Trimmed,
                    TrimBeforeTokensEst = segment.TrimBeforeTokensEst,
                    TrimAfterTokensEst = segment.TrimAfterTokensEst,
                    SourceItemId = segment.SourceItemId,
                    ContentHash = RuntimeTelemetryHashing.Compute(normalized, paths),
                    HashMode = RuntimeTelemetryHashing.HashMode,
                    HashSaltScope = RuntimeTelemetryHashing.HashSaltScope,
                    HmacKeyId = RuntimeTelemetryHashing.HmacKeyId,
                    HashAlgorithm = RuntimeTelemetryHashing.HashAlgorithm,
                    NormalizationVersion = RuntimeTelemetryHashing.NormalizationVersion,
                    RendererVersion = segment.RendererVersion,
                };
            })
            .ToArray();
        var wholeRequestText = RuntimeTelemetryHashing.Normalize(draft.WholeRequestText);
        var wholeRequestTokensEst = ContextBudgetPolicyResolver.EstimateTokens(wholeRequestText);
        var unattributedTokensEst = Math.Max(0, wholeRequestTokensEst - sumSegmentTokensEst);

        var contextWindowInputTokensTotal = usage.ProviderReportedInputTokens ?? wholeRequestTokensEst;
        var billableInputTokensUncached = usage.ProviderReportedUncachedInputTokens
                                          ?? usage.ProviderReportedInputTokens
                                          ?? wholeRequestTokensEst;
        var totalContextTokensPerRequest = ComputeTotalTokens(
            contextWindowInputTokensTotal,
            usage.ProviderReportedOutputTokens,
            usage.ProviderReportedReasoningTokens,
            usage.ProviderReportedTotalTokens,
            usage.ReasoningTokensReportedSeparately,
            usage.ReasoningTokensIncludedInOutput,
            usage.ProviderTotalIncludesReasoning);
        var totalBillableTokensPerRequest = ComputeTotalTokens(
            billableInputTokensUncached,
            usage.ProviderReportedOutputTokens,
            usage.ProviderReportedReasoningTokens,
            usage.ProviderReportedTotalTokens,
            usage.ReasoningTokensReportedSeparately,
            usage.ReasoningTokensIncludedInOutput,
            usage.ProviderTotalIncludesReasoning);

        var record = new LlmRequestEnvelopeTelemetryRecord
        {
            AttributionId = CreateSequentialId("REQENV", paths.RuntimeRequestEnvelopeAttributionRoot),
            RequestId = draft.RequestId,
            RequestKind = draft.RequestKind,
            RequestKindEnumVersion = draft.RequestKindEnumVersion,
            Model = draft.Model,
            Provider = draft.Provider,
            ProviderApiVersion = draft.ProviderApiVersion,
            Tokenizer = draft.Tokenizer,
            RequestSerializerVersion = draft.RequestSerializerVersion,
            TokenAccountingSource = usage.TokenAccountingSource,
            RunId = draft.RunId,
            TaskId = draft.TaskId,
            PackId = draft.PackId,
            ParentRequestId = draft.ParentRequestId,
            WholeRequestTokensEst = wholeRequestTokensEst,
            SumSegmentTokensEst = sumSegmentTokensEst,
            UnattributedTokensEst = unattributedTokensEst,
            KnownProviderOverheadClass = usage.KnownProviderOverheadClass,
            ContextWindowInputTokensTotal = contextWindowInputTokensTotal,
            BillableInputTokensUncached = billableInputTokensUncached,
            CachedInputTokens = usage.ProviderReportedCachedInputTokens,
            OutputTokens = usage.ProviderReportedOutputTokens,
            ReasoningTokens = usage.ProviderReportedReasoningTokens,
            TotalContextTokensPerRequest = totalContextTokensPerRequest,
            TotalBillableTokensPerRequest = totalBillableTokensPerRequest,
            ProviderReportedInputTokens = usage.ProviderReportedInputTokens,
            ProviderReportedCachedInputTokens = usage.ProviderReportedCachedInputTokens,
            ProviderReportedUncachedInputTokens = usage.ProviderReportedUncachedInputTokens,
            ProviderReportedOutputTokens = usage.ProviderReportedOutputTokens,
            ProviderReportedReasoningTokens = usage.ProviderReportedReasoningTokens,
            ProviderReportedTotalTokens = usage.ProviderReportedTotalTokens,
            ReasoningTokensReportedSeparately = usage.ReasoningTokensReportedSeparately,
            ReasoningTokensIncludedInOutput = usage.ReasoningTokensIncludedInOutput,
            ProviderTotalIncludesReasoning = usage.ProviderTotalIncludesReasoning,
            EstimatedCostUsd = usage.EstimatedCostUsd,
            ProviderReportedCostUsdIfAvailable = usage.ProviderReportedCostUsdIfAvailable,
            PricingVersion = usage.PricingVersion,
            PricingSource = usage.PricingSource,
            CostEstimationVersion = usage.CostEstimationVersion,
            ProviderContextCapHit = usage.ProviderContextCapHit,
            InternalPromptBudgetCapHit = usage.InternalPromptBudgetCapHit,
            SectionBudgetCapHit = usage.SectionBudgetCapHit,
            TrimLoopCapHit = usage.TrimLoopCapHit,
            CapTriggerSegmentKind = usage.CapTriggerSegmentKind,
            CapTriggerSource = usage.CapTriggerSource,
            Segments = segments,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };

        File.WriteAllText(
            Path.Combine(paths.RuntimeRequestEnvelopeAttributionRoot, $"{record.AttributionId}.json"),
            JsonSerializer.Serialize(record, JsonOptions));
        return record;
    }

    public IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> ListRecent(int take = 50)
    {
        if (!Directory.Exists(paths.RuntimeRequestEnvelopeAttributionRoot))
        {
            return Array.Empty<LlmRequestEnvelopeTelemetryRecord>();
        }

        return Directory.EnumerateFiles(paths.RuntimeRequestEnvelopeAttributionRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<LlmRequestEnvelopeTelemetryRecord>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidOperationException($"LLM request attribution telemetry at '{path}' could not be deserialized."))
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.AttributionId, StringComparer.Ordinal)
            .Take(Math.Max(1, take))
            .ToArray();
    }

    public IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> ListAll()
    {
        if (!Directory.Exists(paths.RuntimeRequestEnvelopeAttributionRoot))
        {
            return Array.Empty<LlmRequestEnvelopeTelemetryRecord>();
        }

        return Directory.EnumerateFiles(paths.RuntimeRequestEnvelopeAttributionRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<LlmRequestEnvelopeTelemetryRecord>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidOperationException($"LLM request attribution telemetry at '{path}' could not be deserialized."))
            .OrderBy(item => item.RecordedAtUtc)
            .ThenBy(item => item.AttributionId, StringComparer.Ordinal)
            .ToArray();
    }

    private static int? ComputeTotalTokens(
        int? inputTokens,
        int? outputTokens,
        int? reasoningTokens,
        int? providerTotalTokens,
        bool reasoningTokensReportedSeparately,
        bool reasoningTokensIncludedInOutput,
        bool providerTotalIncludesReasoning)
    {
        if (providerTotalTokens.HasValue)
        {
            return providerTotalTokens.Value;
        }

        var total = 0;
        var hasValue = false;
        if (inputTokens.HasValue)
        {
            total += inputTokens.Value;
            hasValue = true;
        }

        if (outputTokens.HasValue)
        {
            total += outputTokens.Value;
            hasValue = true;
        }

        if (reasoningTokens.HasValue
            && reasoningTokensReportedSeparately
            && !reasoningTokensIncludedInOutput
            && !providerTotalIncludesReasoning)
        {
            total += reasoningTokens.Value;
            hasValue = true;
        }

        return hasValue ? total : null;
    }
    private static string CreateSequentialId(string prefix, string root)
    {
        var next = Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly).Count() + 1
            : 1;
        return $"{prefix}-{next:000}";
    }
}
