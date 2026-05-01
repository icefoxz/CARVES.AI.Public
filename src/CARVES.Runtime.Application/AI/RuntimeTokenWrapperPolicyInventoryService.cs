using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenWrapperPolicyInventoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly LlmRequestEnvelopeAttributionService attributionService;

    public RuntimeTokenWrapperPolicyInventoryService(ControlPlanePaths paths)
    {
        this.paths = paths;
        attributionService = new LlmRequestEnvelopeAttributionService(paths);
    }

    public RuntimeTokenWrapperPolicyInventoryResult Persist(
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        RuntimeTokenPhase10TargetDecisionResult phase10Result)
    {
        return Persist(paths, evidenceResult, trustLineResult, phase10Result, attributionService.ListAll(), evidenceResult.ResultDate);
    }

    internal static RuntimeTokenWrapperPolicyInventoryResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        RuntimeTokenPhase10TargetDecisionResult phase10Result,
        IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> sourceRecords,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(evidenceResult, trustLineResult, phase10Result, resultDate);

        var cohort = evidenceResult.Aggregation.Cohort;
        var filteredRecords = RuntimeTokenBaselineAggregatorService.FilterRecords(cohort, sourceRecords);
        var totalRequestCount = filteredRecords.Count;
        var requestCountsByKind = filteredRecords
            .GroupBy(record => record.RequestKind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var wrapperObservations = filteredRecords
            .SelectMany(record => record.Segments
                .Where(IsWrapperSegment)
                .Select(segment => new WrapperObservation(
                    record.AttributionId,
                    record.RequestKind,
                    record.RequestId,
                    segment.SegmentKind,
                    segment.PayloadPath,
                    segment.Role ?? string.Empty,
                    segment.SerializationKind,
                    segment.RendererVersion,
                    segment.ContentHash,
                    segment.TokensEst,
                    ResolveShareRatio(record, segment))))
            .ToArray();

        var requestKindsCovered = filteredRecords
            .Select(record => record.RequestKind)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var contentHashRequestKinds = wrapperObservations
            .Where(observation => !string.IsNullOrWhiteSpace(observation.ContentHash))
            .GroupBy(observation => observation.ContentHash, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.RequestKind).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        var wrapperSurfaces = wrapperObservations
            .GroupBy(
                observation => new WrapperGroupKey(
                    observation.RequestKind,
                    observation.SegmentKind,
                    observation.PayloadPath,
                    observation.Role,
                    observation.SerializationKind,
                    observation.Producer),
                WrapperGroupKey.Comparer)
            .Select(group => BuildSurfaceSummary(group, totalRequestCount, requestCountsByKind, contentHashRequestKinds))
            .OrderByDescending(item => item.ShareP95)
            .ThenByDescending(item => item.TokensP95)
            .ThenBy(item => item.InventoryId, StringComparer.Ordinal)
            .ToArray();

        var requestKindSummaries = requestCountsByKind
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => BuildRequestKindSummary(item.Key, item.Value, wrapperObservations))
            .ToArray();

        var topWrapperSurfaces = wrapperSurfaces
            .OrderByDescending(item => item.ShareP95)
            .ThenByDescending(item => item.TokensP95)
            .ThenBy(item => item.InventoryId, StringComparer.Ordinal)
            .Take(10)
            .ToArray();

        var repeatedBoilerplateSurfaces = wrapperSurfaces
            .Where(item => item.RepeatedAcrossRequests)
            .OrderByDescending(item => item.RequestCountWithSurface)
            .ThenByDescending(item => item.ShareP95)
            .ThenBy(item => item.InventoryId, StringComparer.Ordinal)
            .Take(10)
            .ToArray();

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenWrapperPolicyInventoryResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = cohort.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            TrustMarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineTrustLineService.GetMarkdownArtifactPathFor(paths, resultDate)),
            TrustJsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineTrustLineService.GetJsonArtifactPathFor(paths, resultDate)),
            Phase10MarkdownArtifactPath = phase10Result.MarkdownArtifactPath,
            Phase10JsonArtifactPath = phase10Result.JsonArtifactPath,
            TrustLineClassification = trustLineResult.TrustLineClassification,
            Phase11WrapperInventoryMayReferenceThisLine = true,
            Phase10Decision = phase10Result.Decision,
            Phase10NextTrack = phase10Result.NextTrack,
            CohortRequestCount = totalRequestCount,
            RequestKindsCovered = requestKindsCovered,
            CoverageLimitations = BuildCoverageLimitations(requestKindsCovered),
            RequestKindSummaries = requestKindSummaries,
            WrapperSurfaces = wrapperSurfaces,
            TopWrapperSurfaces = topWrapperSurfaces,
            RepeatedBoilerplateSurfaces = repeatedBoilerplateSurfaces,
            WhatMustNotHappenNext =
            [
                "do not enable runtime shadow execution",
                "do not start active canary",
                "do not replace the main renderer",
                "do not start Phase 1.2 wrapper candidate work before invariant manifest and offline validator are ready"
            ],
            Notes = BuildNotes(requestKindsCovered, wrapperSurfaces),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    private static RuntimeTokenWrapperPolicyRequestKindSummary BuildRequestKindSummary(
        string requestKind,
        int requestCount,
        IReadOnlyList<WrapperObservation> wrapperObservations)
    {
        var observations = wrapperObservations
            .Where(item => string.Equals(item.RequestKind, requestKind, StringComparison.Ordinal))
            .ToArray();
        var wrapperTokensByRequest = observations
            .GroupBy(item => item.RequestId, StringComparer.Ordinal)
            .Select(group => (double)group.Sum(item => item.TokensEst))
            .ToArray();
        var wrapperShareByRequest = observations
            .GroupBy(item => item.RequestId, StringComparer.Ordinal)
            .Select(group => group.Sum(item => item.ShareRatio))
            .ToArray();

        return new RuntimeTokenWrapperPolicyRequestKindSummary
        {
            RequestKind = requestKind,
            RequestCount = requestCount,
            WrapperSurfaceCount = observations
                .Select(item => $"{item.RequestKind}|{item.SegmentKind}|{item.PayloadPath}|{item.Role}|{item.SerializationKind}|{item.Producer}")
                .Distinct(StringComparer.Ordinal)
                .Count(),
            WrapperTokensP50 = Percentile(wrapperTokensByRequest, 0.50),
            WrapperTokensP95 = Percentile(wrapperTokensByRequest, 0.95),
            WrapperShareP50 = Percentile(wrapperShareByRequest, 0.50),
            WrapperShareP95 = Percentile(wrapperShareByRequest, 0.95),
        };
    }

    private static RuntimeTokenWrapperPolicySurfaceSummary BuildSurfaceSummary(
        IGrouping<WrapperGroupKey, WrapperObservation> group,
        int totalRequestCount,
        IReadOnlyDictionary<string, int> requestCountsByKind,
        IReadOnlyDictionary<string, HashSet<string>> contentHashRequestKinds)
    {
        var observations = group.ToArray();
        var requestCountWithSurface = observations
            .Select(item => item.RequestId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var distinctContentHashes = observations
            .Select(item => item.ContentHash)
            .Where(hash => !string.IsNullOrWhiteSpace(hash))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var repeatedAcrossRequests = requestCountWithSurface > 1 && distinctContentHashes.Length == 1;
        var repeatedAcrossRequestKinds = distinctContentHashes.Any(hash =>
            contentHashRequestKinds.TryGetValue(hash, out var requestKinds)
            && requestKinds.Count > 1);
        var policyCritical = IsPolicyCriticalWrapperSegment(group.Key.SegmentKind, group.Key.SerializationKind, group.Key.PayloadPath);
        var boilerplateClass = repeatedAcrossRequests
            ? repeatedAcrossRequestKinds
                ? "shared_cross_request_kind_boilerplate"
                : "request_kind_repeated_boilerplate"
            : distinctContentHashes.Length < requestCountWithSurface
                ? "partially_repeated_policy"
                : "request_specific_policy";
        var compressionAllowed = policyCritical
            ? "structural_only"
            : repeatedAcrossRequests
                ? "light"
                : "none";
        var recommendedAction = policyCritical
            ? repeatedAcrossRequests
                ? "dedupe_and_request_kind_slice_review"
                : "invariant_first"
            : repeatedAcrossRequests
                ? "candidate_for_boilerplate_thinning"
                : "hold_for_manual_review";

        return new RuntimeTokenWrapperPolicySurfaceSummary
        {
            InventoryId = $"{group.Key.RequestKind}:{group.Key.SegmentKind}:{group.Key.PayloadPath}",
            RequestKind = group.Key.RequestKind,
            SegmentKind = group.Key.SegmentKind,
            PayloadPath = group.Key.PayloadPath,
            Role = group.Key.Role,
            SerializationKind = group.Key.SerializationKind,
            Producer = group.Key.Producer,
            RequestCountWithSurface = requestCountWithSurface,
            CohortFrequencyRatio = totalRequestCount == 0 ? 0d : (double)requestCountWithSurface / totalRequestCount,
            RequestKindFrequencyRatio = requestCountsByKind.TryGetValue(group.Key.RequestKind, out var requestKindCount) && requestKindCount > 0
                ? (double)requestCountWithSurface / requestKindCount
                : 0d,
            TokensP50 = Percentile(observations.Select(item => (double)item.TokensEst).ToArray(), 0.50),
            TokensP95 = Percentile(observations.Select(item => (double)item.TokensEst).ToArray(), 0.95),
            ShareP50 = Percentile(observations.Select(item => item.ShareRatio).ToArray(), 0.50),
            ShareP95 = Percentile(observations.Select(item => item.ShareRatio).ToArray(), 0.95),
            DistinctContentHashCount = distinctContentHashes.Length,
            RepeatedAcrossRequests = repeatedAcrossRequests,
            RepeatedAcrossRequestKinds = repeatedAcrossRequestKinds,
            PolicyCritical = policyCritical,
            ManualReviewRequired = policyCritical,
            BoilerplateClass = boilerplateClass,
            CompressionAllowed = compressionAllowed,
            RecommendedInventoryAction = recommendedAction,
            SampleAttributionIds = observations
                .Select(item => item.AttributionId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .Take(5)
                .ToArray(),
        };
    }

    private static bool IsWrapperSegment(LlmRequestEnvelopeTelemetrySegment segment)
    {
        if (string.Equals(RuntimeTokenPhase10DecisionPolicy.ClassifyOptimizationSurface(segment.SegmentKind), "wrapper", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(segment.SerializationKind, "developer_policy_text", StringComparison.Ordinal)
               || segment.PayloadPath.Contains("output_contract", StringComparison.Ordinal)
               || string.Equals(segment.PayloadPath, "$.instructions", StringComparison.Ordinal);
    }

    private static bool IsPolicyCriticalWrapperSegment(string segmentKind, string serializationKind, string payloadPath)
    {
        return string.Equals(RuntimeTokenPhase10DecisionPolicy.ClassifyOptimizationSurface(segmentKind), "wrapper", StringComparison.Ordinal)
               || string.Equals(serializationKind, "developer_policy_text", StringComparison.Ordinal)
               || payloadPath.Contains("output_contract", StringComparison.Ordinal)
               || string.Equals(payloadPath, "$.instructions", StringComparison.Ordinal);
    }

    private static double ResolveShareRatio(LlmRequestEnvelopeTelemetryRecord record, LlmRequestEnvelopeTelemetrySegment segment)
    {
        var wholeRequestTokens = Math.Max(1, record.WholeRequestTokensEst);
        return (double)segment.TokensEst / wholeRequestTokens;
    }

    private static IReadOnlyList<string> BuildCoverageLimitations(IReadOnlyList<string> requestKindsCovered)
    {
        var expected = new[] { "worker", "planner", "reviewer", "repair", "retry" };
        return expected
            .Where(item => !requestKindsCovered.Contains(item, StringComparer.Ordinal))
            .Select(item => $"request_kind_not_covered:{item}")
            .ToArray();
    }

    private static IReadOnlyList<string> BuildNotes(
        IReadOnlyList<string> requestKindsCovered,
        IReadOnlyList<RuntimeTokenWrapperPolicySurfaceSummary> wrapperSurfaces)
    {
        var notes = new List<string>();
        if (requestKindsCovered.Count == 1 && string.Equals(requestKindsCovered[0], "worker", StringComparison.Ordinal))
        {
            notes.Add("Current wrapper inventory is worker-only; planner/reviewer/retry wrapper surfaces remain out of cohort scope.");
        }

        var topSurface = wrapperSurfaces
            .OrderByDescending(item => item.ShareP95)
            .ThenByDescending(item => item.TokensP95)
            .FirstOrDefault();
        if (topSurface is not null)
        {
            notes.Add($"Top wrapper surface is {topSurface.SegmentKind} for {topSurface.RequestKind} at share_p95={topSurface.ShareP95:F3}.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        RuntimeTokenPhase10TargetDecisionResult phase10Result,
        DateOnly resultDate)
    {
        if (evidenceResult.ResultDate != resultDate
            || trustLineResult.ResultDate != resultDate
            || phase10Result.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Wrapper policy inventory requires evidence, trust line, and Phase 1.0 decision dates to match the requested result date.");
        }

        if (!phase10Result.Phase10TargetDecisionMayReferenceThisLine
            || !string.Equals(trustLineResult.TrustLineClassification, "recomputed_trusted_for_phase_1_target_decision", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper policy inventory requires a trusted baseline line that Phase 1.0 may reference.");
        }

        if (!string.Equals(phase10Result.NextTrack, "wrapper_policy_shadow_offline", StringComparison.Ordinal)
            || !string.Equals(phase10Result.Decision, "reprioritize_to_wrapper", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper policy inventory requires Phase 1.0 to select wrapper_policy_shadow_offline.");
        }
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenWrapperPolicyInventoryResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 1.1-W Wrapper Policy Inventory Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Trust line classification: `{result.TrustLineClassification}`");
        builder.AppendLine($"- Wrapper inventory may reference this line: `{(result.Phase11WrapperInventoryMayReferenceThisLine ? "yes" : "no")}`");
        builder.AppendLine($"- Phase 1.0 decision: `{result.Phase10Decision}`");
        builder.AppendLine($"- Phase 1.0 next track: `{result.Phase10NextTrack}`");
        builder.AppendLine($"- Cohort request count: `{result.CohortRequestCount}`");
        builder.AppendLine($"- Request kinds covered: `{(result.RequestKindsCovered.Count == 0 ? "none" : string.Join(", ", result.RequestKindsCovered.Select(item => $"`{item}`")))}`");
        builder.AppendLine();

        builder.AppendLine("## Request Kind Summary");
        builder.AppendLine();
        builder.AppendLine("| Request Kind | Requests | Wrapper Surfaces | Wrapper Tokens P50 | Wrapper Tokens P95 | Wrapper Share P50 | Wrapper Share P95 |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var summary in result.RequestKindSummaries)
        {
            builder.AppendLine($"| `{summary.RequestKind}` | {summary.RequestCount} | {summary.WrapperSurfaceCount} | {FormatNumber(summary.WrapperTokensP50)} | {FormatNumber(summary.WrapperTokensP95)} | {FormatRatio(summary.WrapperShareP50)} | {FormatRatio(summary.WrapperShareP95)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Top Wrapper Surfaces");
        builder.AppendLine();
        builder.AppendLine("| Inventory Id | Request Kind | Segment | Role | Share P95 | Tokens P95 | Req Frequency | Repeated | Policy Critical | Compression | Action |");
        builder.AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: | --- | --- | --- | --- |");
        foreach (var surface in result.TopWrapperSurfaces)
        {
            builder.AppendLine($"| `{surface.InventoryId}` | `{surface.RequestKind}` | `{surface.SegmentKind}` | `{surface.Role}` | {FormatRatio(surface.ShareP95)} | {FormatNumber(surface.TokensP95)} | {FormatRatio(surface.RequestKindFrequencyRatio)} | `{(surface.RepeatedAcrossRequests ? "yes" : "no")}` | `{(surface.PolicyCritical ? "yes" : "no")}` | `{surface.CompressionAllowed}` | `{surface.RecommendedInventoryAction}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Repeated Boilerplate Surfaces");
        builder.AppendLine();
        if (result.RepeatedBoilerplateSurfaces.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            builder.AppendLine("| Inventory Id | Boilerplate Class | Request Kind | Segment | Distinct Hashes | Compression |");
            builder.AppendLine("| --- | --- | --- | --- | ---: | --- |");
            foreach (var surface in result.RepeatedBoilerplateSurfaces)
            {
                builder.AppendLine($"| `{surface.InventoryId}` | `{surface.BoilerplateClass}` | `{surface.RequestKind}` | `{surface.SegmentKind}` | {surface.DistinctContentHashCount} | `{surface.CompressionAllowed}` |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Coverage Limitations");
        builder.AppendLine();
        if (result.CoverageLimitations.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var limitation in result.CoverageLimitations)
            {
                builder.AppendLine($"- `{limitation}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## What Must Not Happen Next");
        builder.AppendLine();
        foreach (var item in result.WhatMustNotHappenNext)
        {
            builder.AppendLine($"- {item}");
        }

        if (result.Notes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Notes");
            builder.AppendLine();
            foreach (var note in result.Notes)
            {
                builder.AppendLine($"- {note}");
            }
        }

        return builder.ToString();
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-1-wrapper-policy-inventory-result-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-1",
            $"wrapper-policy-inventory-result-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        var ordered = values.OrderBy(item => item).ToArray();
        var position = (ordered.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var fraction = position - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * fraction);
    }

    private static string FormatRatio(double value) => value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatNumber(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record WrapperObservation(
        string AttributionId,
        string RequestKind,
        string RequestId,
        string SegmentKind,
        string PayloadPath,
        string Role,
        string SerializationKind,
        string Producer,
        string ContentHash,
        int TokensEst,
        double ShareRatio);

    private sealed record WrapperGroupKey(
        string RequestKind,
        string SegmentKind,
        string PayloadPath,
        string Role,
        string SerializationKind,
        string Producer)
    {
        public static IEqualityComparer<WrapperGroupKey> Comparer { get; } = new WrapperGroupKeyComparer();

        private sealed class WrapperGroupKeyComparer : IEqualityComparer<WrapperGroupKey>
        {
            public bool Equals(WrapperGroupKey? x, WrapperGroupKey? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return string.Equals(x.RequestKind, y.RequestKind, StringComparison.Ordinal)
                       && string.Equals(x.SegmentKind, y.SegmentKind, StringComparison.Ordinal)
                       && string.Equals(x.PayloadPath, y.PayloadPath, StringComparison.Ordinal)
                       && string.Equals(x.Role, y.Role, StringComparison.Ordinal)
                       && string.Equals(x.SerializationKind, y.SerializationKind, StringComparison.Ordinal)
                       && string.Equals(x.Producer, y.Producer, StringComparison.Ordinal);
            }

            public int GetHashCode(WrapperGroupKey obj)
            {
                return HashCode.Combine(
                    obj.RequestKind,
                    obj.SegmentKind,
                    obj.PayloadPath,
                    obj.Role,
                    obj.SerializationKind,
                    obj.Producer);
            }
        }
    }
}
