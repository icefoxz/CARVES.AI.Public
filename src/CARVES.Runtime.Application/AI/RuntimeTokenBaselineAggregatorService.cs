using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenBaselineAggregatorService
{
    private readonly LlmRequestEnvelopeAttributionService attributionService;

    public RuntimeTokenBaselineAggregatorService(ControlPlanePaths paths)
    {
        attributionService = new LlmRequestEnvelopeAttributionService(paths);
    }

    public RuntimeTokenBaselineAggregation Aggregate(RuntimeTokenBaselineCohortFreeze cohort)
    {
        return Aggregate(cohort, attributionService.ListAll());
    }

    internal static RuntimeTokenBaselineAggregation Aggregate(
        RuntimeTokenBaselineCohortFreeze cohort,
        IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> sourceRecords)
    {
        var records = FilterRecords(cohort, sourceRecords);

        var requestCount = records.Count;
        var uniqueTaskCount = records
            .Select(record => record.TaskId)
            .Where(taskId => !string.IsNullOrWhiteSpace(taskId))
            .Distinct(StringComparer.Ordinal)
            .Count();

        var requestKindBreakdown = records
            .GroupBy(record => record.RequestKind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new RuntimeTokenRequestKindBreakdown
            {
                RequestKind = group.Key,
                RequestCount = group.Count(),
                UniqueTaskCount = group
                    .Select(record => record.TaskId)
                    .Where(taskId => !string.IsNullOrWhiteSpace(taskId))
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
            })
            .ToArray();

        var perRequestSegmentMetrics = records
            .Select(BuildPerRequestSegmentMetrics)
            .ToArray();
        var segmentKinds = perRequestSegmentMetrics
            .SelectMany(item => item.BySegmentKind.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .ToArray();

        var segmentKindShares = segmentKinds
            .Select(kind => BuildSegmentSummary(kind, perRequestSegmentMetrics))
            .ToArray();

        var contextPackVsNon = BuildBucketGroup(
            "context_pack_vs_non_context_pack",
            perRequestSegmentMetrics,
            new BucketSpec(
                "context_pack_explicit",
                static item => item.ContextPackExplicitShareRatio,
                static item => item.ContextPackExplicitContributionTokens,
                static item => item.BillableContextPackExplicitContributionTokens),
            new BucketSpec(
                "non_context_pack_explicit",
                static item => item.NonContextPackExplicitShareRatio,
                static item => item.NonContextPackExplicitContributionTokens,
                static item => item.BillableNonContextPackExplicitContributionTokens),
            new BucketSpec(
                "parent_residual",
                static item => item.ParentResidualShareRatio,
                static item => item.ParentResidualContributionTokens,
                static item => item.BillableParentResidualContributionTokens),
            new BucketSpec(
                "known_provider_overhead",
                static item => item.KnownProviderOverheadShareRatio,
                static item => item.KnownProviderOverheadContributionTokens,
                static item => item.BillableKnownProviderOverheadContributionTokens),
            new BucketSpec(
                "unknown_unattributed",
                static item => item.UnknownUnattributedShareRatio,
                static item => item.UnknownUnattributedContributionTokens,
                static item => item.BillableUnknownUnattributedContributionTokens));

        var stableVsDynamic = BuildBucketGroup(
            "stable_vs_dynamic",
            perRequestSegmentMetrics,
            new BucketSpec(
                "stable_explicit",
                static item => item.StableExplicitShareRatio,
                static item => item.StableExplicitContributionTokens,
                static item => item.BillableStableExplicitContributionTokens),
            new BucketSpec(
                "dynamic_explicit",
                static item => item.DynamicExplicitShareRatio,
                static item => item.DynamicExplicitContributionTokens,
                static item => item.BillableDynamicExplicitContributionTokens),
            new BucketSpec(
                "other_classified_explicit",
                static item => item.OtherClassifiedExplicitShareRatio,
                static item => item.OtherClassifiedExplicitContributionTokens,
                static item => item.BillableOtherClassifiedExplicitContributionTokens),
            new BucketSpec(
                "parent_residual",
                static item => item.ParentResidualShareRatio,
                static item => item.ParentResidualContributionTokens,
                static item => item.BillableParentResidualContributionTokens),
            new BucketSpec(
                "known_provider_overhead",
                static item => item.KnownProviderOverheadShareRatio,
                static item => item.KnownProviderOverheadContributionTokens,
                static item => item.BillableKnownProviderOverheadContributionTokens),
            new BucketSpec(
                "unknown_unattributed",
                static item => item.UnknownUnattributedShareRatio,
                static item => item.UnknownUnattributedContributionTokens,
                static item => item.BillableUnknownUnattributedContributionTokens));

        var topTrimmedContributors = perRequestSegmentMetrics
            .SelectMany(item => item.BySegmentKind.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(kind => BuildTrimmedContributorSummary(kind, perRequestSegmentMetrics))
            .Where(item => item.RequestCountWithTrim > 0 || item.TotalTrimmedTokensEst > 0)
            .OrderByDescending(item => item.TotalTrimmedTokensEst)
            .ThenBy(item => item.SegmentKind, StringComparer.Ordinal)
            .Take(10)
            .ToArray();

        var unattributedTokens = records.Select(record => (double)record.UnattributedTokensEst).ToArray();
        var unattributedShareRatios = records
            .Select(record => record.WholeRequestTokensEst <= 0
                ? 0d
                : (double)record.UnattributedTokensEst / record.WholeRequestTokensEst)
            .ToArray();
        var providerInputDeltas = records
            .Where(record => record.ProviderReportedInputTokens.HasValue)
            .Select(record => Math.Abs(record.WholeRequestTokensEst - record.ProviderReportedInputTokens!.Value))
            .Select(static value => (double)value)
            .ToArray();

        var attributionQuality = new RuntimeTokenAttributionQualitySummary
        {
            RequestCount = requestCount,
            P50UnattributedTokensEst = Percentile(unattributedTokens, 0.50),
            P95UnattributedTokensEst = Percentile(unattributedTokens, 0.95),
            P95UnattributedShareRatio = Percentile(unattributedShareRatios, 0.95),
            TokenAccountingSourceBreakdown = records
                .GroupBy(record => record.TokenAccountingSource, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuntimeTokenCountBreakdown { Key = group.Key, Count = group.Count() })
                .ToArray(),
            KnownProviderOverheadBreakdown = records
                .Select(record => string.IsNullOrWhiteSpace(record.KnownProviderOverheadClass) ? "none" : record.KnownProviderOverheadClass!)
                .GroupBy(item => item, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuntimeTokenCountBreakdown { Key = group.Key, Count = group.Count() })
                .ToArray(),
            P50AbsoluteProviderInputDelta = Percentile(providerInputDeltas, 0.50),
            P95AbsoluteProviderInputDelta = Percentile(providerInputDeltas, 0.95),
        };

        var massLedgerCoverage = new RuntimeTokenMassLedgerCoverageSummary
        {
            RequestCount = requestCount,
            P50ExplicitSegmentCoverageRatio = Percentile(perRequestSegmentMetrics.Select(item => item.ExplicitSegmentCoverageRatio).ToArray(), 0.50),
            P95ExplicitSegmentCoverageRatio = Percentile(perRequestSegmentMetrics.Select(item => item.ExplicitSegmentCoverageRatio).ToArray(), 0.95),
            P50ClassifiedSegmentCoverageRatio = Percentile(perRequestSegmentMetrics.Select(item => item.ClassifiedSegmentCoverageRatio).ToArray(), 0.50),
            P95ClassifiedSegmentCoverageRatio = Percentile(perRequestSegmentMetrics.Select(item => item.ClassifiedSegmentCoverageRatio).ToArray(), 0.95),
            P50ParentResidualShareRatio = Percentile(perRequestSegmentMetrics.Select(item => item.ParentResidualShareRatio).ToArray(), 0.50),
            P95ParentResidualShareRatio = Percentile(perRequestSegmentMetrics.Select(item => item.ParentResidualShareRatio).ToArray(), 0.95),
            P50KnownProviderOverheadShareRatio = Percentile(perRequestSegmentMetrics.Select(item => item.KnownProviderOverheadShareRatio).ToArray(), 0.50),
            P95KnownProviderOverheadShareRatio = Percentile(perRequestSegmentMetrics.Select(item => item.KnownProviderOverheadShareRatio).ToArray(), 0.95),
            P50UnknownUnattributedShareRatio = Percentile(perRequestSegmentMetrics.Select(item => item.UnknownUnattributedShareRatio).ToArray(), 0.50),
            P95UnknownUnattributedShareRatio = Percentile(perRequestSegmentMetrics.Select(item => item.UnknownUnattributedShareRatio).ToArray(), 0.95),
        };
        var capTruth = BuildCapTruthSummary(records);

        var contextWindowTokens = records
            .Select(record => ResolveContextWindowTokens(record))
            .Select(static value => (double)value)
            .ToArray();
        var billableTokens = records
            .Select(record => ResolveBillableTokens(record))
            .Select(static value => (double)value)
            .ToArray();

        return new RuntimeTokenBaselineAggregation
        {
            Cohort = cohort,
            RequestCount = requestCount,
            UniqueTaskCount = uniqueTaskCount,
            RequestKindBreakdown = requestKindBreakdown,
            SegmentKindShares = segmentKindShares,
            ContextPackVersusNonContextPack = contextPackVsNon,
            StableVersusDynamic = stableVsDynamic,
            TopTrimmedContributors = topTrimmedContributors,
            AttributionQuality = attributionQuality,
            MassLedgerCoverage = massLedgerCoverage,
            CapTruth = capTruth,
            ContextWindowView = new RuntimeTokenViewSummary
            {
                ViewId = cohort.ContextWindowView,
                RequestCount = requestCount,
                P50Tokens = Percentile(contextWindowTokens, 0.50),
                P95Tokens = Percentile(contextWindowTokens, 0.95),
                AverageTokens = Average(contextWindowTokens),
            },
            BillableCostView = new RuntimeTokenViewSummary
            {
                ViewId = cohort.BillableCostView,
                RequestCount = requestCount,
                P50Tokens = Percentile(billableTokens, 0.50),
                P95Tokens = Percentile(billableTokens, 0.95),
                AverageTokens = Average(billableTokens),
            },
        };
    }

    private static RuntimeTokenCapTruthSummary BuildCapTruthSummary(IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> records)
    {
        static bool HasDirectTruth(LlmRequestEnvelopeTelemetryRecord record)
        {
            return record.ProviderContextCapHit.HasValue
                   || record.InternalPromptBudgetCapHit.HasValue
                   || record.SectionBudgetCapHit.HasValue
                   || record.TrimLoopCapHit.HasValue
                   || !string.IsNullOrWhiteSpace(record.CapTriggerSegmentKind)
                   || !string.IsNullOrWhiteSpace(record.CapTriggerSource);
        }

        var directRecords = records
            .Where(HasDirectTruth)
            .ToArray();

        return new RuntimeTokenCapTruthSummary
        {
            RequestCount = records.Count,
            RequestsWithDirectCapTruth = directRecords.Length,
            ProviderContextCapHitCount = directRecords.Count(record => record.ProviderContextCapHit == true),
            InternalPromptBudgetCapHitCount = directRecords.Count(record => record.InternalPromptBudgetCapHit == true),
            SectionBudgetCapHitCount = directRecords.Count(record => record.SectionBudgetCapHit == true),
            TrimLoopCapHitCount = directRecords.Count(record => record.TrimLoopCapHit == true),
            CapTriggerSegmentKindBreakdown = directRecords
                .Select(record => record.CapTriggerSegmentKind)
                .Where(kind => !string.IsNullOrWhiteSpace(kind))
                .GroupBy(kind => kind!, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuntimeTokenCountBreakdown
                {
                    Key = group.Key,
                    Count = group.Count(),
                })
                .ToArray(),
            CapTriggerSourceBreakdown = directRecords
                .Select(record => record.CapTriggerSource)
                .Where(source => !string.IsNullOrWhiteSpace(source))
                .GroupBy(source => source!, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuntimeTokenCountBreakdown
                {
                    Key = group.Key,
                    Count = group.Count(),
                })
                .ToArray(),
        };
    }

    private static RuntimeTokenSegmentShareSummary BuildSegmentSummary(
        string segmentKind,
        IReadOnlyList<PerRequestSegmentMetrics> perRequestMetrics)
    {
        var values = perRequestMetrics
            .Select(item => item.BySegmentKind.TryGetValue(segmentKind, out var value) ? value : SegmentKindMetrics.Empty)
            .ToArray();
        return new RuntimeTokenSegmentShareSummary
        {
            SegmentKind = segmentKind,
            RequestCountWithSegment = values.Count(item => item.ShareRatio > 0 || item.ContextWindowContributionTokens > 0 || item.BillableContributionTokens > 0),
            P50ShareRatio = Percentile(values.Select(item => item.ShareRatio).ToArray(), 0.50),
            P95ShareRatio = Percentile(values.Select(item => item.ShareRatio).ToArray(), 0.95),
            P50ContextWindowContributionTokens = Percentile(values.Select(item => item.ContextWindowContributionTokens).ToArray(), 0.50),
            P95ContextWindowContributionTokens = Percentile(values.Select(item => item.ContextWindowContributionTokens).ToArray(), 0.95),
            P50BillableContributionTokens = Percentile(values.Select(item => item.BillableContributionTokens).ToArray(), 0.50),
            P95BillableContributionTokens = Percentile(values.Select(item => item.BillableContributionTokens).ToArray(), 0.95),
        };
    }

    private static RuntimeTokenBucketShareGroup BuildBucketGroup(
        string summaryId,
        IReadOnlyList<PerRequestSegmentMetrics> perRequestMetrics,
        params BucketSpec[] bucketSpecs)
    {
        var buckets = bucketSpecs
            .Select(spec => new RuntimeTokenBucketShareSummary
            {
                BucketId = spec.BucketId,
                P50ShareRatio = Percentile(perRequestMetrics.Select(spec.ShareSelector).ToArray(), 0.50),
                P95ShareRatio = Percentile(perRequestMetrics.Select(spec.ShareSelector).ToArray(), 0.95),
                P50ContributionTokens = Percentile(perRequestMetrics.Select(spec.ContextContributionSelector).ToArray(), 0.50),
                P95ContributionTokens = Percentile(perRequestMetrics.Select(spec.ContextContributionSelector).ToArray(), 0.95),
                P50BillableContributionTokens = Percentile(perRequestMetrics.Select(spec.BillableContributionSelector).ToArray(), 0.50),
                P95BillableContributionTokens = Percentile(perRequestMetrics.Select(spec.BillableContributionSelector).ToArray(), 0.95),
            })
            .ToArray();

        return new RuntimeTokenBucketShareGroup
        {
            SummaryId = summaryId,
            Buckets = buckets,
        };
    }

    private static RuntimeTokenTrimmedContributorSummary BuildTrimmedContributorSummary(
        string segmentKind,
        IReadOnlyList<PerRequestSegmentMetrics> perRequestMetrics)
    {
        var trimmed = perRequestMetrics
            .Select(item => item.BySegmentKind.TryGetValue(segmentKind, out var value) ? value.TrimmedTokensEst : 0d)
            .ToArray();
        return new RuntimeTokenTrimmedContributorSummary
        {
            SegmentKind = segmentKind,
            RequestCountWithTrim = trimmed.Count(value => value > 0),
            TotalTrimmedTokensEst = trimmed.Sum(),
            P95TrimmedTokensEst = Percentile(trimmed, 0.95),
        };
    }

    private static PerRequestSegmentMetrics BuildPerRequestSegmentMetrics(LlmRequestEnvelopeTelemetryRecord record)
    {
        var effectiveSegments = ResolveEffectiveSegments(record.Segments);
        var wholeRequestTokens = Math.Max(1, record.WholeRequestTokensEst);
        var contextWindowTokens = ResolveContextWindowTokens(record);
        var billableTokens = ResolveBillableTokens(record);
        var explicitSegmentTokens = effectiveSegments.Sum(segment => (double)segment.TokensEst);
        var parentResidualTokens = ResolveParentResidualTokens(record.Segments);
        var remainingAfterExplicit = Math.Max(0d, wholeRequestTokens - explicitSegmentTokens - parentResidualTokens);
        var knownProviderOverheadTokens = ResolveKnownProviderOverheadTokens(record, remainingAfterExplicit);
        var unknownUnattributedTokens = Math.Max(0d, remainingAfterExplicit - knownProviderOverheadTokens);
        var bySegmentKind = effectiveSegments
            .GroupBy(segment => segment.SegmentKind, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var estimatedTokens = group.Sum(segment => segment.TokensEst);
                    var shareRatio = estimatedTokens / (double)wholeRequestTokens;
                    return new SegmentKindMetrics
                    {
                        ShareRatio = shareRatio,
                        ContextWindowContributionTokens = shareRatio * contextWindowTokens,
                        BillableContributionTokens = shareRatio * billableTokens,
                        TrimmedTokensEst = group.Sum(ResolveTrimmedTokensEst),
                    };
                },
                StringComparer.Ordinal);

        var contextPackExplicitTokens = 0d;
        var nonContextPackExplicitTokens = 0d;
        var stableExplicitTokens = 0d;
        var dynamicExplicitTokens = 0d;
        var otherClassifiedExplicitTokens = 0d;

        foreach (var segment in effectiveSegments)
        {
            if (IsContextPackSegment(segment))
            {
                contextPackExplicitTokens += segment.TokensEst;
            }
            else
            {
                nonContextPackExplicitTokens += segment.TokensEst;
            }

            if (IsStableSegment(segment))
            {
                stableExplicitTokens += segment.TokensEst;
            }
            else if (IsDynamicSegment(segment))
            {
                dynamicExplicitTokens += segment.TokensEst;
            }
            else
            {
                otherClassifiedExplicitTokens += segment.TokensEst;
            }
        }

        return new PerRequestSegmentMetrics
        {
            BySegmentKind = bySegmentKind,
            ContextPackExplicitShareRatio = contextPackExplicitTokens / wholeRequestTokens,
            ContextPackExplicitContributionTokens = (contextPackExplicitTokens / wholeRequestTokens) * contextWindowTokens,
            BillableContextPackExplicitContributionTokens = (contextPackExplicitTokens / wholeRequestTokens) * billableTokens,
            NonContextPackExplicitShareRatio = nonContextPackExplicitTokens / wholeRequestTokens,
            NonContextPackExplicitContributionTokens = (nonContextPackExplicitTokens / wholeRequestTokens) * contextWindowTokens,
            BillableNonContextPackExplicitContributionTokens = (nonContextPackExplicitTokens / wholeRequestTokens) * billableTokens,
            StableExplicitShareRatio = stableExplicitTokens / wholeRequestTokens,
            StableExplicitContributionTokens = (stableExplicitTokens / wholeRequestTokens) * contextWindowTokens,
            BillableStableExplicitContributionTokens = (stableExplicitTokens / wholeRequestTokens) * billableTokens,
            DynamicExplicitShareRatio = dynamicExplicitTokens / wholeRequestTokens,
            DynamicExplicitContributionTokens = (dynamicExplicitTokens / wholeRequestTokens) * contextWindowTokens,
            BillableDynamicExplicitContributionTokens = (dynamicExplicitTokens / wholeRequestTokens) * billableTokens,
            OtherClassifiedExplicitShareRatio = otherClassifiedExplicitTokens / wholeRequestTokens,
            OtherClassifiedExplicitContributionTokens = (otherClassifiedExplicitTokens / wholeRequestTokens) * contextWindowTokens,
            BillableOtherClassifiedExplicitContributionTokens = (otherClassifiedExplicitTokens / wholeRequestTokens) * billableTokens,
            ParentResidualShareRatio = parentResidualTokens / wholeRequestTokens,
            ParentResidualContributionTokens = (parentResidualTokens / wholeRequestTokens) * contextWindowTokens,
            BillableParentResidualContributionTokens = (parentResidualTokens / wholeRequestTokens) * billableTokens,
            KnownProviderOverheadShareRatio = knownProviderOverheadTokens / wholeRequestTokens,
            KnownProviderOverheadContributionTokens = (knownProviderOverheadTokens / wholeRequestTokens) * contextWindowTokens,
            BillableKnownProviderOverheadContributionTokens = (knownProviderOverheadTokens / wholeRequestTokens) * billableTokens,
            UnknownUnattributedShareRatio = unknownUnattributedTokens / wholeRequestTokens,
            UnknownUnattributedContributionTokens = (unknownUnattributedTokens / wholeRequestTokens) * contextWindowTokens,
            BillableUnknownUnattributedContributionTokens = (unknownUnattributedTokens / wholeRequestTokens) * billableTokens,
            ExplicitSegmentCoverageRatio = (explicitSegmentTokens + parentResidualTokens) / wholeRequestTokens,
            ClassifiedSegmentCoverageRatio = explicitSegmentTokens / wholeRequestTokens,
        };
    }

    private static double ResolveParentResidualTokens(IReadOnlyList<LlmRequestEnvelopeTelemetrySegment> segments)
    {
        var childTokenSumsByParent = segments
            .Where(segment => !string.IsNullOrWhiteSpace(segment.SegmentParentId))
            .GroupBy(segment => segment.SegmentParentId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(segment => (double)segment.TokensEst),
                StringComparer.Ordinal);

        return segments
            .Where(segment => childTokenSumsByParent.ContainsKey(segment.SegmentId))
            .Sum(segment => Math.Max(0d, segment.TokensEst - childTokenSumsByParent[segment.SegmentId]));
    }

    private static double ResolveKnownProviderOverheadTokens(LlmRequestEnvelopeTelemetryRecord record, double remainingAfterExplicit)
    {
        if (string.IsNullOrWhiteSpace(record.KnownProviderOverheadClass))
        {
            return 0d;
        }

        var providerDelta = record.ProviderReportedInputTokens.HasValue
            ? Math.Abs(record.ProviderReportedInputTokens.Value - record.WholeRequestTokensEst)
            : 0;
        return Math.Min(remainingAfterExplicit, providerDelta);
    }

    private static IReadOnlyList<LlmRequestEnvelopeTelemetrySegment> ResolveEffectiveSegments(IReadOnlyList<LlmRequestEnvelopeTelemetrySegment> segments)
    {
        var parentIds = segments
            .Select(segment => segment.SegmentParentId)
            .Where(parentId => !string.IsNullOrWhiteSpace(parentId))
            .ToHashSet(StringComparer.Ordinal);

        return segments
            .Where(segment => !parentIds.Contains(segment.SegmentId))
            .ToArray();
    }

    internal static IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> FilterRecords(
        RuntimeTokenBaselineCohortFreeze cohort,
        IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> sourceRecords)
    {
        Validate(cohort);

        var allowedRequestKinds = new HashSet<string>(cohort.RequestKinds, StringComparer.Ordinal);
        return sourceRecords
            .Where(record => record.RecordedAtUtc >= cohort.WindowStartUtc && record.RecordedAtUtc <= cohort.WindowEndUtc)
            .Where(record => allowedRequestKinds.Contains(record.RequestKind))
            .Where(record => MatchesAccountingPolicy(cohort.TokenAccountingSourcePolicy, record.TokenAccountingSource))
            .OrderBy(record => record.RecordedAtUtc)
            .ThenBy(record => record.AttributionId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool MatchesAccountingPolicy(string policy, string source)
    {
        return policy switch
        {
            "provider_actual_only" => string.Equals(source, "provider_actual", StringComparison.Ordinal),
            "local_estimate_only" => string.Equals(source, "local_estimate", StringComparison.Ordinal),
            "mixed_with_reconciliation" => true,
            "provider_actual_preferred_with_reconciliation" => true,
            _ => throw new InvalidOperationException($"Unsupported token accounting source policy '{policy}'."),
        };
    }

    internal static int ResolveContextWindowTokens(LlmRequestEnvelopeTelemetryRecord record)
    {
        return record.ContextWindowInputTokensTotal
               ?? record.ProviderReportedInputTokens
               ?? record.WholeRequestTokensEst;
    }

    internal static int ResolveBillableTokens(LlmRequestEnvelopeTelemetryRecord record)
    {
        return record.BillableInputTokensUncached
               ?? record.ProviderReportedUncachedInputTokens
               ?? record.ProviderReportedInputTokens
               ?? record.WholeRequestTokensEst;
    }

    private static double ResolveTrimmedTokensEst(LlmRequestEnvelopeTelemetrySegment segment)
    {
        if (segment.TrimBeforeTokensEst.HasValue && segment.TrimAfterTokensEst.HasValue)
        {
            return Math.Max(0, segment.TrimBeforeTokensEst.Value - segment.TrimAfterTokensEst.Value);
        }

        return segment.Trimmed ? segment.TokensEst : 0d;
    }

    private static bool IsContextPackSegment(LlmRequestEnvelopeTelemetrySegment segment)
    {
        return string.Equals(segment.SegmentKind, "context_pack", StringComparison.Ordinal)
               || string.Equals(segment.SegmentParentId, "context_pack", StringComparison.Ordinal)
               || string.Equals(segment.SerializationKind, "context_pack_text", StringComparison.Ordinal);
    }

    private static bool IsStableSegment(LlmRequestEnvelopeTelemetrySegment segment)
    {
        return RuntimeTokenPhase10DecisionPolicy.IsStableRendererSegmentKind(segment.SegmentKind);
    }

    private static bool IsDynamicSegment(LlmRequestEnvelopeTelemetrySegment segment)
    {
        return RuntimeTokenPhase10DecisionPolicy.IsDynamicRendererSegmentKind(segment.SegmentKind);
    }

    internal static void Validate(RuntimeTokenBaselineCohortFreeze cohort)
    {
        if (string.IsNullOrWhiteSpace(cohort.CohortId))
        {
            throw new InvalidOperationException("Baseline cohort freeze requires a cohort id.");
        }

        if (cohort.WindowEndUtc < cohort.WindowStartUtc)
        {
            throw new InvalidOperationException("Baseline cohort freeze requires a valid window range.");
        }

        if (cohort.RequestKinds.Count == 0)
        {
            throw new InvalidOperationException("Baseline cohort freeze requires explicit request kinds.");
        }

        if (string.IsNullOrWhiteSpace(cohort.TokenAccountingSourcePolicy))
        {
            throw new InvalidOperationException("Baseline cohort freeze requires an explicit token accounting source policy.");
        }

        if (!string.Equals(cohort.ContextWindowView, "context_window_input_tokens_total", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported context window view '{cohort.ContextWindowView}'.");
        }

        if (!string.Equals(cohort.BillableCostView, "billable_input_tokens_uncached", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported billable cost view '{cohort.BillableCostView}'.");
        }
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        var position = (ordered.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var weight = position - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * weight);
    }

    private static double Average(IReadOnlyList<double> values)
    {
        return values.Count == 0 ? 0d : values.Average();
    }

    private sealed record PerRequestSegmentMetrics
    {
        public IReadOnlyDictionary<string, SegmentKindMetrics> BySegmentKind { get; init; } = new Dictionary<string, SegmentKindMetrics>(StringComparer.Ordinal);

        public double ContextPackExplicitShareRatio { get; init; }

        public double ContextPackExplicitContributionTokens { get; init; }

        public double BillableContextPackExplicitContributionTokens { get; init; }

        public double NonContextPackExplicitShareRatio { get; init; }

        public double NonContextPackExplicitContributionTokens { get; init; }

        public double BillableNonContextPackExplicitContributionTokens { get; init; }

        public double StableExplicitShareRatio { get; init; }

        public double StableExplicitContributionTokens { get; init; }

        public double BillableStableExplicitContributionTokens { get; init; }

        public double DynamicExplicitShareRatio { get; init; }

        public double DynamicExplicitContributionTokens { get; init; }

        public double BillableDynamicExplicitContributionTokens { get; init; }

        public double OtherClassifiedExplicitShareRatio { get; init; }

        public double OtherClassifiedExplicitContributionTokens { get; init; }

        public double BillableOtherClassifiedExplicitContributionTokens { get; init; }

        public double ParentResidualShareRatio { get; init; }

        public double ParentResidualContributionTokens { get; init; }

        public double BillableParentResidualContributionTokens { get; init; }

        public double KnownProviderOverheadShareRatio { get; init; }

        public double KnownProviderOverheadContributionTokens { get; init; }

        public double BillableKnownProviderOverheadContributionTokens { get; init; }

        public double UnknownUnattributedShareRatio { get; init; }

        public double UnknownUnattributedContributionTokens { get; init; }

        public double BillableUnknownUnattributedContributionTokens { get; init; }

        public double ExplicitSegmentCoverageRatio { get; init; }

        public double ClassifiedSegmentCoverageRatio { get; init; }
    }

    private sealed record BucketSpec(
        string BucketId,
        Func<PerRequestSegmentMetrics, double> ShareSelector,
        Func<PerRequestSegmentMetrics, double> ContextContributionSelector,
        Func<PerRequestSegmentMetrics, double> BillableContributionSelector);

    private sealed record SegmentKindMetrics
    {
        public static readonly SegmentKindMetrics Empty = new();

        public double ShareRatio { get; init; }

        public double ContextWindowContributionTokens { get; init; }

        public double BillableContributionTokens { get; init; }

        public double TrimmedTokensEst { get; init; }
    }
}
