using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenBaselineAggregatorServiceTests
{
    [Fact]
    public void Aggregate_ComputesSegmentSharesAndExcludesContainerContextPackSegment()
    {
        var cohort = new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = "phase_0a_baseline",
            WindowStartUtc = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero),
            RequestKinds = ["worker", "planner"],
            TokenAccountingSourcePolicy = "mixed_with_reconciliation",
        };

        var records = new[]
        {
            CreateRecord(
                attributionId: "REQENV-001",
                requestKind: "worker",
                taskId: "T-001",
                wholeRequestTokensEst: 100,
                contextWindowInputTokensTotal: 100,
                billableInputTokensUncached: 80,
                providerReportedInputTokens: 100,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 1, 0, 0, TimeSpan.Zero),
                segments:
                [
                    Segment("context_pack", "context_pack", null, 60),
                    Segment("context_pack:goal", "goal", "context_pack", 20),
                    Segment("context_pack:recall", "recall", "context_pack", 30, trimmed: true, trimBefore: 30, trimAfter: 10),
                    Segment("context_pack:windowed_reads", "windowed_reads", "context_pack", 10),
                    Segment("system", "system", null, 15),
                    Segment("validation_contract", "validation_contract", null, 5),
                ]),
            CreateRecord(
                attributionId: "REQENV-002",
                requestKind: "worker",
                taskId: "T-001",
                wholeRequestTokensEst: 120,
                contextWindowInputTokensTotal: 120,
                billableInputTokensUncached: 90,
                providerReportedInputTokens: 120,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 2, 0, 0, TimeSpan.Zero),
                segments:
                [
                    Segment("context_pack", "context_pack", null, 70),
                    Segment("context_pack:goal", "goal", "context_pack", 25),
                    Segment("context_pack:constraints", "constraints", "context_pack", 25),
                    Segment("context_pack:recall", "recall", "context_pack", 20, trimmed: true, trimBefore: 20, trimAfter: 5),
                    Segment("system", "system", null, 20),
                    Segment("patch_budget_contract", "patch_budget_contract", null, 10),
                ]),
            CreateRecord(
                attributionId: "REQENV-003",
                requestKind: "planner",
                taskId: "T-002",
                wholeRequestTokensEst: 80,
                contextWindowInputTokensTotal: 90,
                billableInputTokensUncached: 70,
                providerReportedInputTokens: 90,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 3, 0, 0, TimeSpan.Zero),
                segments:
                [
                    Segment("system", "system", null, 10),
                    Segment("goal_summary", "goal_summary", null, 15),
                    Segment("context_pack_json", "context_pack", null, 35),
                    Segment("planner_output_contract", "output_contract", null, 20),
                ]),
        };

        var aggregate = RuntimeTokenBaselineAggregatorService.Aggregate(cohort, records);

        Assert.Equal(3, aggregate.RequestCount);
        Assert.Equal(2, aggregate.UniqueTaskCount);

        var requestKinds = aggregate.RequestKindBreakdown.ToDictionary(item => item.RequestKind, StringComparer.Ordinal);
        Assert.Equal(2, requestKinds["worker"].RequestCount);
        Assert.Equal(1, requestKinds["planner"].RequestCount);

        var contextPackBucket = Assert.Single(
            aggregate.ContextPackVersusNonContextPack.Buckets,
            item => item.BucketId == "context_pack_explicit");
        Assert.True(contextPackBucket.P95ShareRatio > 0.40d);

        var recall = Assert.Single(aggregate.SegmentKindShares, item => item.SegmentKind == "recall");
        Assert.Equal(2, recall.RequestCountWithSegment);
        Assert.True(recall.P95ShareRatio > 0.15d);

        var containerContextPack = Assert.Single(aggregate.SegmentKindShares, item => item.SegmentKind == "context_pack");
        Assert.Equal(1, containerContextPack.RequestCountWithSegment);

        var topTrimmed = Assert.Single(aggregate.TopTrimmedContributors, item => item.SegmentKind == "recall");
        Assert.Equal(2, topTrimmed.RequestCountWithTrim);
        Assert.Equal(35d, topTrimmed.TotalTrimmedTokensEst, 6);

        var massLedgerCoverage = aggregate.MassLedgerCoverage;
        Assert.Equal(3, massLedgerCoverage.RequestCount);
        Assert.True(massLedgerCoverage.P95ExplicitSegmentCoverageRatio > 0.80d);

        Assert.Equal("context_window_input_tokens_total", aggregate.ContextWindowView.ViewId);
        Assert.Equal("billable_input_tokens_uncached", aggregate.BillableCostView.ViewId);
    }

    [Fact]
    public void Aggregate_KeepsUnknownUnattributedSeparateFromNonContextPackExplicit()
    {
        var cohort = new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = "phase_0a_baseline",
            WindowStartUtc = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero),
            RequestKinds = ["worker"],
            TokenAccountingSourcePolicy = "mixed_with_reconciliation",
        };

        var record = CreateRecord(
            attributionId: "REQENV-020",
            requestKind: "worker",
            taskId: "T-020",
            wholeRequestTokensEst: 100,
            contextWindowInputTokensTotal: 100,
            billableInputTokensUncached: 80,
            providerReportedInputTokens: 100,
            recordedAtUtc: new DateTimeOffset(2026, 4, 21, 1, 0, 0, TimeSpan.Zero),
            segments:
            [
                Segment("context_pack:goal", "goal", "context_pack", 20),
                Segment("system", "system", null, 20),
            ]);

        var aggregate = RuntimeTokenBaselineAggregatorService.Aggregate(cohort, [record]);

        var contextPack = Assert.Single(aggregate.ContextPackVersusNonContextPack.Buckets, item => item.BucketId == "context_pack_explicit");
        var nonContext = Assert.Single(aggregate.ContextPackVersusNonContextPack.Buckets, item => item.BucketId == "non_context_pack_explicit");
        var unknown = Assert.Single(aggregate.ContextPackVersusNonContextPack.Buckets, item => item.BucketId == "unknown_unattributed");

        Assert.Equal(0.20d, contextPack.P95ShareRatio, 6);
        Assert.Equal(0.20d, nonContext.P95ShareRatio, 6);
        Assert.Equal(0.60d, unknown.P95ShareRatio, 6);
    }

    [Fact]
    public void Aggregate_PreservesParentResidualInsteadOfDroppingParentMass()
    {
        var cohort = new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = "phase_0a_baseline",
            WindowStartUtc = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero),
            RequestKinds = ["worker"],
            TokenAccountingSourcePolicy = "mixed_with_reconciliation",
        };

        var record = CreateRecord(
            attributionId: "REQENV-021",
            requestKind: "worker",
            taskId: "T-021",
            wholeRequestTokensEst: 100,
            contextWindowInputTokensTotal: 100,
            billableInputTokensUncached: 100,
            providerReportedInputTokens: 100,
            recordedAtUtc: new DateTimeOffset(2026, 4, 21, 1, 0, 0, TimeSpan.Zero),
            segments:
            [
                Segment("context_pack", "context_pack", null, 50),
                Segment("context_pack:goal", "goal", "context_pack", 30),
                Segment("system", "system", null, 20),
            ]);

        var aggregate = RuntimeTokenBaselineAggregatorService.Aggregate(cohort, [record]);

        var parentResidual = Assert.Single(aggregate.ContextPackVersusNonContextPack.Buckets, item => item.BucketId == "parent_residual");
        var contextPack = Assert.Single(aggregate.ContextPackVersusNonContextPack.Buckets, item => item.BucketId == "context_pack_explicit");

        Assert.Equal(0.20d, parentResidual.P95ShareRatio, 6);
        Assert.Equal(0.30d, contextPack.P95ShareRatio, 6);
        Assert.Equal(0.70d, aggregate.MassLedgerCoverage.P95ExplicitSegmentCoverageRatio, 6);
        Assert.Equal(0.50d, aggregate.MassLedgerCoverage.P95ClassifiedSegmentCoverageRatio, 6);
    }

    [Fact]
    public void Aggregate_FiltersByWindowRequestKindAndAccountingPolicy()
    {
        var cohort = new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = "provider_actual_workers",
            WindowStartUtc = new DateTimeOffset(2026, 4, 21, 0, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 21, 23, 59, 59, TimeSpan.Zero),
            RequestKinds = ["worker"],
            TokenAccountingSourcePolicy = "provider_actual_only",
        };

        var records = new[]
        {
            CreateRecord(
                attributionId: "REQENV-010",
                requestKind: "worker",
                taskId: "T-010",
                wholeRequestTokensEst: 50,
                contextWindowInputTokensTotal: 55,
                billableInputTokensUncached: 40,
                providerReportedInputTokens: 55,
                tokenAccountingSource: "provider_actual",
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 10, 0, 0, TimeSpan.Zero),
                segments: [Segment("system", "system", null, 10), Segment("context_pack_json", "context_pack", null, 20)]),
            CreateRecord(
                attributionId: "REQENV-011",
                requestKind: "worker",
                taskId: "T-011",
                wholeRequestTokensEst: 60,
                contextWindowInputTokensTotal: 60,
                billableInputTokensUncached: 60,
                providerReportedInputTokens: null,
                tokenAccountingSource: "local_estimate",
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 11, 0, 0, TimeSpan.Zero),
                segments: [Segment("system", "system", null, 10)]),
            CreateRecord(
                attributionId: "REQENV-012",
                requestKind: "planner",
                taskId: "T-012",
                wholeRequestTokensEst: 70,
                contextWindowInputTokensTotal: 70,
                billableInputTokensUncached: 70,
                providerReportedInputTokens: 70,
                tokenAccountingSource: "provider_actual",
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero),
                segments: [Segment("system", "system", null, 10)]),
            CreateRecord(
                attributionId: "REQENV-013",
                requestKind: "worker",
                taskId: "T-013",
                wholeRequestTokensEst: 80,
                contextWindowInputTokensTotal: 80,
                billableInputTokensUncached: 80,
                providerReportedInputTokens: 80,
                tokenAccountingSource: "provider_actual",
                recordedAtUtc: new DateTimeOffset(2026, 4, 22, 0, 0, 0, TimeSpan.Zero),
                segments: [Segment("system", "system", null, 10)]),
        };

        var aggregate = RuntimeTokenBaselineAggregatorService.Aggregate(cohort, records);

        Assert.Equal(1, aggregate.RequestCount);
        Assert.Equal(1, aggregate.UniqueTaskCount);
        var sourceBreakdown = Assert.Single(aggregate.AttributionQuality.TokenAccountingSourceBreakdown);
        Assert.Equal("provider_actual", sourceBreakdown.Key);
        Assert.Equal(1, sourceBreakdown.Count);
    }

    [Fact]
    public void Aggregate_ThrowsWhenCohortFreezeIsNotExplicit()
    {
        var cohort = new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = "invalid",
            WindowStartUtc = new DateTimeOffset(2026, 4, 21, 1, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 20, 1, 0, 0, TimeSpan.Zero),
            RequestKinds = ["worker"],
            TokenAccountingSourcePolicy = "mixed_with_reconciliation",
        };

        var error = Assert.Throws<InvalidOperationException>(() => RuntimeTokenBaselineAggregatorService.Aggregate(cohort, Array.Empty<LlmRequestEnvelopeTelemetryRecord>()));
        Assert.Contains("valid window range", error.Message, StringComparison.Ordinal);
    }

    private static LlmRequestEnvelopeTelemetryRecord CreateRecord(
        string attributionId,
        string requestKind,
        string? taskId,
        int wholeRequestTokensEst,
        int contextWindowInputTokensTotal,
        int billableInputTokensUncached,
        int? providerReportedInputTokens,
        DateTimeOffset recordedAtUtc,
        IReadOnlyList<LlmRequestEnvelopeTelemetrySegment> segments,
        string tokenAccountingSource = "provider_actual")
    {
        return new LlmRequestEnvelopeTelemetryRecord
        {
            AttributionId = attributionId,
            RequestId = attributionId.ToLowerInvariant(),
            RequestKind = requestKind,
            RequestKindEnumVersion = "runtime_request_kind.v1",
            Model = "gpt-5-mini",
            Provider = "openai",
            ProviderApiVersion = "responses_v1",
            Tokenizer = "local_estimator_v1",
            RequestSerializerVersion = "runtime_request_serializer.v1",
            TokenAccountingSource = tokenAccountingSource,
            TaskId = taskId,
            WholeRequestTokensEst = wholeRequestTokensEst,
            SumSegmentTokensEst = segments.Sum(item => item.TokensEst),
            UnattributedTokensEst = Math.Max(0, wholeRequestTokensEst - segments.Sum(item => item.TokensEst)),
            KnownProviderOverheadClass = providerReportedInputTokens.HasValue ? "provider_serialization_delta" : null,
            ContextWindowInputTokensTotal = contextWindowInputTokensTotal,
            BillableInputTokensUncached = billableInputTokensUncached,
            CachedInputTokens = providerReportedInputTokens.HasValue ? Math.Max(0, contextWindowInputTokensTotal - billableInputTokensUncached) : null,
            OutputTokens = 10,
            TotalContextTokensPerRequest = contextWindowInputTokensTotal + 10,
            TotalBillableTokensPerRequest = billableInputTokensUncached + 10,
            ProviderReportedInputTokens = providerReportedInputTokens,
            ProviderReportedUncachedInputTokens = providerReportedInputTokens.HasValue ? billableInputTokensUncached : null,
            ProviderReportedOutputTokens = 10,
            ProviderReportedTotalTokens = providerReportedInputTokens.HasValue ? contextWindowInputTokensTotal + 10 : null,
            EstimatedCostUsd = 0.01m,
            PricingVersion = "test",
            PricingSource = "test",
            CostEstimationVersion = "test",
            Segments = segments,
            RecordedAtUtc = recordedAtUtc,
        };
    }

    private static LlmRequestEnvelopeTelemetrySegment Segment(
        string segmentId,
        string segmentKind,
        string? parentId,
        int tokensEst,
        bool trimmed = false,
        int? trimBefore = null,
        int? trimAfter = null)
    {
        return new LlmRequestEnvelopeTelemetrySegment
        {
            SegmentId = segmentId,
            SegmentKind = segmentKind,
            SegmentParentId = parentId,
            SegmentOrder = 0,
            PayloadPath = $"$.segments.{segmentId}",
            SerializationKind = segmentKind == "context_pack" || string.Equals(parentId, "context_pack", StringComparison.Ordinal)
                ? "context_pack_text"
                : "chat_message_text",
            Chars = tokensEst * 4,
            TokensEst = tokensEst,
            Included = true,
            Trimmed = trimmed,
            TrimBeforeTokensEst = trimBefore,
            TrimAfterTokensEst = trimAfter,
            ContentHash = $"hash-{segmentId}",
            HashMode = "hmac_sha256_env_scoped",
            HashSaltScope = "runtime_live_state",
            HmacKeyId = "token_telemetry_hmac.key",
            HashAlgorithm = "hmac_sha256",
            NormalizationVersion = "runtime_telemetry_norm.v1",
            RendererVersion = "prose_v1",
        };
    }
}
