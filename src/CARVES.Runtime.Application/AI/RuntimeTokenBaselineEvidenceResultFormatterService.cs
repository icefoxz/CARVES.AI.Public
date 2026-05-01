using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenBaselineEvidenceResultFormatterService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly RuntimeTokenBaselineAggregatorService aggregatorService;
    private readonly RuntimeTokenOutcomeBinderService outcomeBinderService;

    public RuntimeTokenBaselineEvidenceResultFormatterService(
        ControlPlanePaths paths,
        RuntimeTokenBaselineAggregatorService aggregatorService,
        RuntimeTokenOutcomeBinderService outcomeBinderService)
    {
        this.paths = paths;
        this.aggregatorService = aggregatorService;
        this.outcomeBinderService = outcomeBinderService;
    }

    public RuntimeTokenBaselineEvidenceResult Persist(RuntimeTokenBaselineCohortFreeze cohort, DateOnly resultDate)
    {
        var aggregation = aggregatorService.Aggregate(cohort);
        var outcomeBinding = outcomeBinderService.Bind(cohort);
        return Persist(paths, aggregation, outcomeBinding, resultDate);
    }

    internal static RuntimeTokenBaselineEvidenceResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenBaselineAggregation aggregation,
        RuntimeTokenOutcomeBinding outcomeBinding,
        DateOnly resultDate,
        DateTimeOffset? generatedAtUtc = null)
    {
        ValidateInputs(aggregation, outcomeBinding);

        var markdownArtifactPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonArtifactPath = GetJsonArtifactPath(paths, resultDate);
        var hardCapTriggerAnalysis = BuildHardCapTriggerAnalysis(aggregation);
        var decisionInputsReadiness = BuildDecisionInputsReadiness(aggregation, outcomeBinding, hardCapTriggerAnalysis);
        var decisionInputs = BuildDecisionInputs(aggregation, hardCapTriggerAnalysis);
        var recommendation = BuildRecommendation(decisionInputsReadiness, decisionInputs, hardCapTriggerAnalysis);
        decisionInputs = decisionInputs with { DominanceCriteriaSatisfied = recommendation.DominanceBasis };
        var result = new RuntimeTokenBaselineEvidenceResult
        {
            ResultDate = resultDate,
            GeneratedAtUtc = generatedAtUtc ?? DateTimeOffset.UtcNow,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownArtifactPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonArtifactPath),
            Aggregation = aggregation,
            OutcomeBinding = outcomeBinding,
            HardCapTriggerAnalysis = hardCapTriggerAnalysis,
            DecisionInputsReadiness = decisionInputsReadiness,
            DecisionInputs = decisionInputs,
            Recommendation = recommendation,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownArtifactPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonArtifactPath)!);
        File.WriteAllText(markdownArtifactPath, FormatMarkdown(result));
        File.WriteAllText(jsonArtifactPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string FormatMarkdown(RuntimeTokenBaselineEvidenceResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 0A Attribution Baseline Evidence Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Generated at: `{result.GeneratedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.Aggregation.Cohort.CohortId}`");
        builder.AppendLine($"- Context window view: `{result.Aggregation.ContextWindowView.ViewId}`");
        builder.AppendLine($"- Billable cost view: `{result.Aggregation.BillableCostView.ViewId}`");
        builder.AppendLine();

        builder.AppendLine("## Request Kind Breakdown");
        builder.AppendLine();
        builder.AppendLine("| Request Kind | Request Count | Unique Task Count |");
        builder.AppendLine("| --- | ---: | ---: |");
        foreach (var item in result.Aggregation.RequestKindBreakdown)
        {
            builder.AppendLine($"| `{item.RequestKind}` | {item.RequestCount} | {item.UniqueTaskCount} |");
        }

        builder.AppendLine();
        builder.AppendLine("## P50/P95 Token Share By Segment Kind");
        builder.AppendLine();
        builder.AppendLine("| Segment Kind | Request Count | P50 Share | P95 Share | P50 Context Tokens | P95 Context Tokens | P50 Billable Tokens | P95 Billable Tokens |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var item in result.Aggregation.SegmentKindShares.OrderByDescending(item => item.P95ContextWindowContributionTokens).ThenBy(item => item.SegmentKind, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"| `{item.SegmentKind}` | {item.RequestCountWithSegment} | {FormatRatio(item.P50ShareRatio)} | {FormatRatio(item.P95ShareRatio)} | {FormatNumber(item.P50ContextWindowContributionTokens)} | {FormatNumber(item.P95ContextWindowContributionTokens)} | {FormatNumber(item.P50BillableContributionTokens)} | {FormatNumber(item.P95BillableContributionTokens)} |");
        }

        builder.AppendLine();
        AppendBucketGroup(builder, "## ContextPack Versus Non-ContextPack Share", result.Aggregation.ContextPackVersusNonContextPack);
        builder.AppendLine();
        AppendBucketGroup(builder, "## Stable Versus Dynamic Section Share", result.Aggregation.StableVersusDynamic);
        builder.AppendLine();

        builder.AppendLine("## Top Trimmed Contributors");
        builder.AppendLine();
        builder.AppendLine("| Segment Kind | Requests With Trim | Total Trimmed Tokens | P95 Trimmed Tokens |");
        builder.AppendLine("| --- | ---: | ---: | ---: |");
        foreach (var item in result.Aggregation.TopTrimmedContributors)
        {
            builder.AppendLine($"| `{item.SegmentKind}` | {item.RequestCountWithTrim} | {FormatNumber(item.TotalTrimmedTokensEst)} | {FormatNumber(item.P95TrimmedTokensEst)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Hard Cap Trigger Analysis");
        builder.AppendLine();
        builder.AppendLine($"- Status: `{result.HardCapTriggerAnalysis.Status}`");
        builder.AppendLine($"- Direct metrics available: `{ToYesNo(result.HardCapTriggerAnalysis.DirectMetricsAvailable)}`");
        builder.AppendLine($"- Proxy metrics available: `{ToYesNo(result.HardCapTriggerAnalysis.ProxyMetricsAvailable)}`");
        builder.AppendLine($"- Cap-based dominance allowed: `{ToYesNo(result.HardCapTriggerAnalysis.CapBasedDominanceAllowed)}`");
        builder.AppendLine($"- Requests with direct cap truth: `{result.HardCapTriggerAnalysis.RequestsWithDirectCapTruth}`");
        builder.AppendLine($"- Primary direct cap trigger segment: `{result.HardCapTriggerAnalysis.PrimaryCapTriggerSegmentKind ?? "none"}`");
        builder.AppendLine($"- Primary direct cap trigger source: `{result.HardCapTriggerAnalysis.PrimaryCapTriggerSource ?? "none"}`");
        builder.AppendLine($"- Provider context cap hit count: `{result.HardCapTriggerAnalysis.ProviderContextCapHitCount}`");
        builder.AppendLine($"- Internal prompt budget cap hit count: `{result.HardCapTriggerAnalysis.InternalPromptBudgetCapHitCount}`");
        builder.AppendLine($"- Section budget cap hit count: `{result.HardCapTriggerAnalysis.SectionBudgetCapHitCount}`");
        builder.AppendLine($"- Trim loop cap hit count: `{result.HardCapTriggerAnalysis.TrimLoopCapHitCount}`");
        builder.AppendLine($"- Primary trim pressure segment: `{result.HardCapTriggerAnalysis.PrimaryTrimPressureSegmentKind ?? "none"}`");
        builder.AppendLine($"- Primary p95 trimmed tokens: `{FormatNullableNumber(result.HardCapTriggerAnalysis.PrimaryTrimmedTokensP95)}`");
        builder.AppendLine($"- Uses trim pressure proxy: `{(result.HardCapTriggerAnalysis.UsesTrimPressureProxy ? "yes" : "no")}`");
        foreach (var note in result.HardCapTriggerAnalysis.Notes)
        {
            builder.AppendLine($"- Note: {note}");
        }

        builder.AppendLine();
        builder.AppendLine("## Attribution Quality");
        builder.AppendLine();
        builder.AppendLine($"- Request count: `{result.Aggregation.AttributionQuality.RequestCount}`");
        builder.AppendLine($"- P50 unattributed tokens est: `{FormatNumber(result.Aggregation.AttributionQuality.P50UnattributedTokensEst)}`");
        builder.AppendLine($"- P95 unattributed tokens est: `{FormatNumber(result.Aggregation.AttributionQuality.P95UnattributedTokensEst)}`");
        builder.AppendLine($"- P95 unattributed share ratio: `{FormatRatio(result.Aggregation.AttributionQuality.P95UnattributedShareRatio)}`");
        builder.AppendLine($"- P50 absolute provider input delta: `{FormatNumber(result.Aggregation.AttributionQuality.P50AbsoluteProviderInputDelta)}`");
        builder.AppendLine($"- P95 absolute provider input delta: `{FormatNumber(result.Aggregation.AttributionQuality.P95AbsoluteProviderInputDelta)}`");
        builder.AppendLine($"- Token accounting source breakdown: {FormatBreakdown(result.Aggregation.AttributionQuality.TokenAccountingSourceBreakdown)}");
        builder.AppendLine($"- Known provider overhead breakdown: {FormatBreakdown(result.Aggregation.AttributionQuality.KnownProviderOverheadBreakdown)}");

        builder.AppendLine();
        builder.AppendLine("## Mass Ledger Coverage");
        builder.AppendLine();
        builder.AppendLine($"- Request count: `{result.Aggregation.MassLedgerCoverage.RequestCount}`");
        builder.AppendLine($"- P50 explicit segment coverage ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P50ExplicitSegmentCoverageRatio)}`");
        builder.AppendLine($"- P95 explicit segment coverage ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P95ExplicitSegmentCoverageRatio)}`");
        builder.AppendLine($"- P50 classified segment coverage ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P50ClassifiedSegmentCoverageRatio)}`");
        builder.AppendLine($"- P95 classified segment coverage ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P95ClassifiedSegmentCoverageRatio)}`");
        builder.AppendLine($"- P50 parent residual share ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P50ParentResidualShareRatio)}`");
        builder.AppendLine($"- P95 parent residual share ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P95ParentResidualShareRatio)}`");
        builder.AppendLine($"- P50 known provider overhead share ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P50KnownProviderOverheadShareRatio)}`");
        builder.AppendLine($"- P95 known provider overhead share ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P95KnownProviderOverheadShareRatio)}`");
        builder.AppendLine($"- P50 unknown unattributed share ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P50UnknownUnattributedShareRatio)}`");
        builder.AppendLine($"- P95 unknown unattributed share ratio: `{FormatRatio(result.Aggregation.MassLedgerCoverage.P95UnknownUnattributedShareRatio)}`");

        builder.AppendLine();
        AppendViewSummary(builder, "## Context Window View", result.Aggregation.ContextWindowView);
        builder.AppendLine();
        AppendViewSummary(builder, "## Billable Cost View", result.Aggregation.BillableCostView);
        builder.AppendLine();

        builder.AppendLine("## Outcome Binding");
        builder.AppendLine();
        builder.AppendLine($"- Included request count: `{result.OutcomeBinding.IncludedRequestCount}`");
        builder.AppendLine($"- Excluded request count: `{result.OutcomeBinding.ExcludedRequestCount}`");
        builder.AppendLine($"- Unbound included request count: `{result.OutcomeBinding.UnboundIncludedRequestCount}`");
        builder.AppendLine($"- Unbound included mandatory request count: `{result.OutcomeBinding.UnboundIncludedMandatoryRequestCount}`");
        builder.AppendLine($"- Unbound included optional request count: `{result.OutcomeBinding.UnboundIncludedOptionalRequestCount}`");
        builder.AppendLine($"- Unbound included context tokens: `{FormatNumber(result.OutcomeBinding.UnboundIncludedContextTokens)}`");
        builder.AppendLine($"- Unbound included billable tokens: `{FormatNumber(result.OutcomeBinding.UnboundIncludedBillableTokens)}`");
        builder.AppendLine($"- Attempted task count: `{result.OutcomeBinding.AttemptedTaskCount}`");
        builder.AppendLine($"- Successful task count: `{result.OutcomeBinding.SuccessfulTaskCount}`");
        builder.AppendLine($"- Task cost view trusted: `{ToYesNo(result.OutcomeBinding.TaskCostViewTrusted)}`");
        builder.AppendLine($"- Context window total tokens per successful task: `{FormatNullableNumber(result.OutcomeBinding.ContextWindowView.TokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Billable total tokens per successful task: `{FormatNullableNumber(result.OutcomeBinding.BillableCostView.TokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Run report coverage: `{result.OutcomeBinding.RunReportCoverage.IncludedRequestsWithMatchingRunReport}` matched / `{result.OutcomeBinding.RunReportCoverage.IncludedRequestsWithRunId}` with run id");
        if (result.OutcomeBinding.TaskCostViewBlockingReasons.Count > 0)
        {
            builder.AppendLine($"- Task cost view blocking reasons: {string.Join(", ", result.OutcomeBinding.TaskCostViewBlockingReasons)}");
        }
        if (result.OutcomeBinding.BindingGaps.Count > 0)
        {
            builder.AppendLine($"- Binding gaps: {FormatBreakdown(result.OutcomeBinding.BindingGaps.Select(item => new RuntimeTokenCountBreakdown { Key = item.Reason, Count = item.RequestCount }).ToArray())}");
        }

        builder.AppendLine();
        builder.AppendLine("## Decision Inputs Ready For Phase 1.0");
        builder.AppendLine();
        builder.AppendLine($"- Ready for Phase 1.0 target decision: `{(result.DecisionInputsReadiness.ReadyForPhase10TargetDecision ? "yes" : "no")}`");
        builder.AppendLine($"- Has request kind breakdown: `{ToYesNo(result.DecisionInputsReadiness.HasRequestKindBreakdown)}`");
        builder.AppendLine($"- Has p95 segment shares: `{ToYesNo(result.DecisionInputsReadiness.HasP95SegmentShares)}`");
        builder.AppendLine($"- Has renderer vs non-renderer split: `{ToYesNo(result.DecisionInputsReadiness.HasRendererVsNonRendererSplit)}`");
        builder.AppendLine($"- Has stable vs dynamic split: `{ToYesNo(result.DecisionInputsReadiness.HasStableVsDynamicSplit)}`");
        builder.AppendLine($"- Has trim pressure visibility: `{ToYesNo(result.DecisionInputsReadiness.HasTrimPressureVisibility)}`");
        builder.AppendLine($"- Has attribution quality: `{ToYesNo(result.DecisionInputsReadiness.HasAttributionQuality)}`");
        builder.AppendLine($"- Has context window view: `{ToYesNo(result.DecisionInputsReadiness.HasContextWindowView)}`");
        builder.AppendLine($"- Has billable cost view: `{ToYesNo(result.DecisionInputsReadiness.HasBillableCostView)}`");
        builder.AppendLine($"- Has successful task cost view: `{ToYesNo(result.DecisionInputsReadiness.HasSuccessfulTaskCostView)}`");
        builder.AppendLine($"- Has explicit hard cap trigger analysis: `{ToYesNo(result.DecisionInputsReadiness.HasExplicitHardCapTriggerAnalysis)}`");
        builder.AppendLine($"- Has direct hard cap truth: `{ToYesNo(result.DecisionInputsReadiness.HasDirectHardCapTruth)}`");
        builder.AppendLine($"- Cap-based target decision allowed: `{ToYesNo(result.DecisionInputsReadiness.CapBasedTargetDecisionAllowed)}`");
        builder.AppendLine($"- Attribution share ready: `{ToYesNo(result.DecisionInputsReadiness.AttributionShareReady)}`");
        builder.AppendLine($"- Task cost ready: `{ToYesNo(result.DecisionInputsReadiness.TaskCostReady)}`");
        builder.AppendLine($"- Route reinjection ready: `{ToYesNo(result.DecisionInputsReadiness.RouteReinjectionReady)}`");
        builder.AppendLine($"- Cap truth ready: `{ToYesNo(result.DecisionInputsReadiness.CapTruthReady)}`");
        builder.AppendLine($"- Phase 1.0 target decision allowed: `{ToYesNo(result.DecisionInputsReadiness.Phase10TargetDecisionAllowed)}`");
        builder.AppendLine($"- Total cost claim allowed: `{ToYesNo(result.DecisionInputsReadiness.TotalCostClaimAllowed)}`");
        builder.AppendLine($"- Active canary allowed: `{ToYesNo(result.DecisionInputsReadiness.ActiveCanaryAllowed)}`");
        if (result.DecisionInputsReadiness.BlockingReasons.Count > 0)
        {
            builder.AppendLine("- Blocking reasons:");
            foreach (var reason in result.DecisionInputsReadiness.BlockingReasons)
            {
                builder.AppendLine($"  - {reason}");
            }
        }

        AppendBlockingReasons(builder, "Attribution share blocking reasons", result.DecisionInputsReadiness.AttributionShareBlockingReasons);
        AppendBlockingReasons(builder, "Task cost blocking reasons", result.DecisionInputsReadiness.TaskCostBlockingReasons);
        AppendBlockingReasons(builder, "Route reinjection blocking reasons", result.DecisionInputsReadiness.RouteReinjectionBlockingReasons);
        AppendBlockingReasons(builder, "Cap truth blocking reasons", result.DecisionInputsReadiness.CapTruthBlockingReasons);
        AppendBlockingReasons(builder, "Active canary blocking reasons", result.DecisionInputsReadiness.ActiveCanaryBlockingReasons);

        builder.AppendLine();
        builder.AppendLine("## Phase 1.0 Decision Inputs");
        builder.AppendLine();
        builder.AppendLine($"- ContextPack explicit share p95: `{FormatRatio(result.DecisionInputs.ContextPackExplicitShareP95)}`");
        builder.AppendLine($"- Non-ContextPack explicit share p95: `{FormatRatio(result.DecisionInputs.NonContextPackExplicitShareP95)}`");
        builder.AppendLine($"- Stable explicit share p95: `{FormatRatio(result.DecisionInputs.StableExplicitShareP95)}`");
        builder.AppendLine($"- Dynamic explicit share p95: `{FormatRatio(result.DecisionInputs.DynamicExplicitShareP95)}`");
        builder.AppendLine($"- Renderer share p95 proxy: `{FormatRatio(result.DecisionInputs.RendererShareP95Proxy)}`");
        builder.AppendLine($"- Tool schema share p95 proxy: `{FormatRatio(result.DecisionInputs.ToolSchemaShareP95Proxy)}`");
        builder.AppendLine($"- Wrapper policy share p95 proxy: `{FormatRatio(result.DecisionInputs.WrapperPolicyShareP95Proxy)}`");
        builder.AppendLine($"- Other segment share p95 proxy: `{FormatRatio(result.DecisionInputs.OtherSegmentShareP95Proxy)}`");
        builder.AppendLine($"- Parent residual share p95: `{FormatRatio(result.DecisionInputs.ParentResidualShareP95)}`");
        builder.AppendLine($"- Known provider overhead share p95: `{FormatRatio(result.DecisionInputs.KnownProviderOverheadShareP95)}`");
        builder.AppendLine($"- Unknown unattributed share p95: `{FormatRatio(result.DecisionInputs.UnknownUnattributedShareP95)}`");
        builder.AppendLine($"- Hard cap trigger segments: {(result.DecisionInputs.HardCapTriggerSegments.Count == 0 ? "none" : string.Join(", ", result.DecisionInputs.HardCapTriggerSegments.Select(item => $"`{item}`")))}");
        builder.AppendLine($"- Dominance criteria satisfied: {(result.DecisionInputs.DominanceCriteriaSatisfied.Count == 0 ? "none" : string.Join(", ", result.DecisionInputs.DominanceCriteriaSatisfied.Select(item => $"`{item}`")))}");

        builder.AppendLine();
        builder.AppendLine("| Top P95 Contributor | Class | Share P95 | Context Tokens P95 | Billable Tokens P95 |");
        builder.AppendLine("| --- | --- | ---: | ---: | ---: |");
        foreach (var contributor in result.DecisionInputs.TopP95Contributors)
        {
            builder.AppendLine($"| `{contributor.SegmentKind}` | `{contributor.TargetSegmentClass}` | {FormatRatio(contributor.ShareP95)} | {FormatNumber(contributor.ContextTokensP95)} | {FormatNumber(contributor.BillableTokensP95)} |");
        }

        builder.AppendLine();
        builder.AppendLine("| Top Trimmed Contributor | Class | Trimmed Tokens P95 | Trimmed Share Proxy P95 |");
        builder.AppendLine("| --- | --- | ---: | ---: |");
        foreach (var contributor in result.DecisionInputs.TopTrimmedContributors)
        {
            builder.AppendLine($"| `{contributor.SegmentKind}` | `{contributor.TargetSegmentClass}` | {FormatNumber(contributor.TrimmedTokensP95)} | {FormatRatio(contributor.TrimmedShareProxyP95)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Phase 1.0 Recommendation");
        builder.AppendLine();
        builder.AppendLine($"- Decision: `{result.Recommendation.Decision}`");
        builder.AppendLine($"- Target segment: `{result.Recommendation.TargetSegment ?? "none"}`");
        builder.AppendLine($"- Target segment class: `{result.Recommendation.TargetSegmentClass ?? "none"}`");
        builder.AppendLine($"- Target share p95: `{FormatNullableNumber(result.Recommendation.TargetShareP95)}`");
        builder.AppendLine($"- Trimmed share proxy p95: `{FormatNullableNumber(result.Recommendation.TrimmedShareProxyP95)}`");
        builder.AppendLine($"- Hard cap trigger segment: `{result.Recommendation.HardCapTriggerSegment ?? "none"}`");
        builder.AppendLine($"- Dominance basis: {(result.Recommendation.DominanceBasis.Count == 0 ? "none" : string.Join(", ", result.Recommendation.DominanceBasis.Select(item => $"`{item}`")))}");
        builder.AppendLine($"- Next track: `{result.Recommendation.NextTrack}`");
        builder.AppendLine($"- Confidence: `{result.Recommendation.Confidence}`");
        builder.AppendLine($"- Blocked criteria: {(result.Recommendation.BlockedCriteria.Count == 0 ? "none" : string.Join(", ", result.Recommendation.BlockedCriteria.Select(item => $"`{item}`")))}");

        return builder.ToString();
    }

    private static void AppendBucketGroup(StringBuilder builder, string title, RuntimeTokenBucketShareGroup group)
    {
        builder.AppendLine(title);
        builder.AppendLine();
        builder.AppendLine("| Bucket | P50 Share | P95 Share | P50 Context Tokens | P95 Context Tokens | P50 Billable Tokens | P95 Billable Tokens |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var bucket in group.Buckets)
        {
            builder.AppendLine($"| `{bucket.BucketId}` | {FormatRatio(bucket.P50ShareRatio)} | {FormatRatio(bucket.P95ShareRatio)} | {FormatNumber(bucket.P50ContributionTokens)} | {FormatNumber(bucket.P95ContributionTokens)} | {FormatNumber(bucket.P50BillableContributionTokens)} | {FormatNumber(bucket.P95BillableContributionTokens)} |");
        }
    }

    private static void AppendViewSummary(StringBuilder builder, string title, RuntimeTokenViewSummary view)
    {
        builder.AppendLine(title);
        builder.AppendLine();
        builder.AppendLine($"- View id: `{view.ViewId}`");
        builder.AppendLine($"- Request count: `{view.RequestCount}`");
        builder.AppendLine($"- P50 tokens: `{FormatNumber(view.P50Tokens)}`");
        builder.AppendLine($"- P95 tokens: `{FormatNumber(view.P95Tokens)}`");
        builder.AppendLine($"- Average tokens: `{FormatNumber(view.AverageTokens)}`");
    }

    private static void AppendBlockingReasons(StringBuilder builder, string title, IReadOnlyList<string> reasons)
    {
        builder.AppendLine();
        builder.AppendLine($"- {title}: {(reasons.Count == 0 ? "none" : string.Join(", ", reasons))}");
    }

    private static RuntimeTokenHardCapTriggerAnalysis BuildHardCapTriggerAnalysis(RuntimeTokenBaselineAggregation aggregation)
    {
        var primaryTrimPressure = aggregation.TopTrimmedContributors
            .OrderByDescending(item => item.P95TrimmedTokensEst)
            .ThenByDescending(item => item.TotalTrimmedTokensEst)
            .ThenBy(item => item.SegmentKind, StringComparer.Ordinal)
            .FirstOrDefault();
        var primaryDirectCapTrigger = aggregation.CapTruth.CapTriggerSegmentKindBreakdown
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        var primaryDirectCapSource = aggregation.CapTruth.CapTriggerSourceBreakdown
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        var directMetricsAvailable = aggregation.CapTruth.RequestsWithDirectCapTruth > 0;
        var capHitObserved = aggregation.CapTruth.ProviderContextCapHitCount > 0
                             || aggregation.CapTruth.InternalPromptBudgetCapHitCount > 0
                             || aggregation.CapTruth.SectionBudgetCapHitCount > 0
                             || aggregation.CapTruth.TrimLoopCapHitCount > 0;
        var proxyMetricsAvailable = primaryTrimPressure is not null;

        if (directMetricsAvailable)
        {
            var notes = new List<string>
            {
                "Direct cap truth was observed in request-level telemetry.",
            };
            if (!capHitObserved)
            {
                notes.Add("Direct cap metrics are available, but the frozen cohort did not observe a direct cap hit.");
            }

            if (proxyMetricsAvailable)
            {
                notes.Add("Trim-pressure proxy remains available as supporting evidence only.");
            }

            return new RuntimeTokenHardCapTriggerAnalysis
            {
                Status = "direct",
                DirectMetricsAvailable = true,
                ProxyMetricsAvailable = proxyMetricsAvailable,
                CapBasedDominanceAllowed = capHitObserved,
                PrimaryCapTriggerSegmentKind = primaryDirectCapTrigger?.Key,
                PrimaryCapTriggerSource = primaryDirectCapSource?.Key,
                RequestsWithDirectCapTruth = aggregation.CapTruth.RequestsWithDirectCapTruth,
                ProviderContextCapHitCount = aggregation.CapTruth.ProviderContextCapHitCount,
                InternalPromptBudgetCapHitCount = aggregation.CapTruth.InternalPromptBudgetCapHitCount,
                SectionBudgetCapHitCount = aggregation.CapTruth.SectionBudgetCapHitCount,
                TrimLoopCapHitCount = aggregation.CapTruth.TrimLoopCapHitCount,
                PrimaryTrimPressureSegmentKind = primaryTrimPressure?.SegmentKind,
                PrimaryTrimmedTokensP95 = primaryTrimPressure?.P95TrimmedTokensEst,
                UsesTrimPressureProxy = proxyMetricsAvailable,
                Notes = notes,
            };
        }

        if (primaryTrimPressure is null)
        {
            return new RuntimeTokenHardCapTriggerAnalysis
            {
                Status = "unavailable",
                DirectMetricsAvailable = false,
                ProxyMetricsAvailable = false,
                CapBasedDominanceAllowed = false,
                RequestsWithDirectCapTruth = aggregation.CapTruth.RequestsWithDirectCapTruth,
                ProviderContextCapHitCount = aggregation.CapTruth.ProviderContextCapHitCount,
                InternalPromptBudgetCapHitCount = aggregation.CapTruth.InternalPromptBudgetCapHitCount,
                SectionBudgetCapHitCount = aggregation.CapTruth.SectionBudgetCapHitCount,
                TrimLoopCapHitCount = aggregation.CapTruth.TrimLoopCapHitCount,
                UsesTrimPressureProxy = false,
                Notes =
                [
                    "No direct cap truth and no trim-pressure proxy were observed in the frozen cohort.",
                    "Cap-based dominance is unavailable, but share-based target decisions may still proceed if other readiness inputs pass."
                ],
            };
        }

        return new RuntimeTokenHardCapTriggerAnalysis
        {
            Status = "proxy_only",
            DirectMetricsAvailable = false,
            ProxyMetricsAvailable = true,
            CapBasedDominanceAllowed = false,
            RequestsWithDirectCapTruth = aggregation.CapTruth.RequestsWithDirectCapTruth,
            ProviderContextCapHitCount = aggregation.CapTruth.ProviderContextCapHitCount,
            InternalPromptBudgetCapHitCount = aggregation.CapTruth.InternalPromptBudgetCapHitCount,
            SectionBudgetCapHitCount = aggregation.CapTruth.SectionBudgetCapHitCount,
            TrimLoopCapHitCount = aggregation.CapTruth.TrimLoopCapHitCount,
            PrimaryTrimPressureSegmentKind = primaryTrimPressure.SegmentKind,
            PrimaryTrimmedTokensP95 = primaryTrimPressure.P95TrimmedTokensEst,
            UsesTrimPressureProxy = true,
            Notes =
            [
                "Trim-pressure proxy is available, but direct cap truth is unavailable.",
                "Cap-based dominance remains blocked until direct cap metrics are collected."
            ],
        };
    }

    private static RuntimeTokenDecisionInputsReadiness BuildDecisionInputsReadiness(
        RuntimeTokenBaselineAggregation aggregation,
        RuntimeTokenOutcomeBinding outcomeBinding,
        RuntimeTokenHardCapTriggerAnalysis hardCapTriggerAnalysis)
    {
        var hasRequestKindBreakdown = aggregation.RequestKindBreakdown.Count > 0;
        var hasP95SegmentShares = aggregation.SegmentKindShares.Any(item => item.P95ShareRatio > 0 || item.P95ContextWindowContributionTokens > 0 || item.P95BillableContributionTokens > 0);
        var hasRendererVsNonRendererSplit = aggregation.ContextPackVersusNonContextPack.Buckets.Any(item => string.Equals(item.BucketId, "context_pack_explicit", StringComparison.Ordinal))
                                           && aggregation.ContextPackVersusNonContextPack.Buckets.Any(item => string.Equals(item.BucketId, "non_context_pack_explicit", StringComparison.Ordinal));
        var hasStableVsDynamicSplit = aggregation.StableVersusDynamic.Buckets.Any(item => string.Equals(item.BucketId, "stable_explicit", StringComparison.Ordinal))
                                      && aggregation.StableVersusDynamic.Buckets.Any(item => string.Equals(item.BucketId, "dynamic_explicit", StringComparison.Ordinal));
        var hasTrimPressureVisibility = aggregation.TopTrimmedContributors.Count > 0;
        var hasAttributionQuality = aggregation.AttributionQuality.RequestCount > 0;
        var hasContextWindowView = aggregation.ContextWindowView.RequestCount > 0;
        var hasBillableCostView = aggregation.BillableCostView.RequestCount > 0;
        var hasSuccessfulTaskCostView = outcomeBinding.TaskCostViewTrusted
                                        && outcomeBinding.SuccessfulTaskCount > 0
                                        && outcomeBinding.ContextWindowView.TokensPerSuccessfulTask.HasValue
                                        && outcomeBinding.BillableCostView.TokensPerSuccessfulTask.HasValue;
        var hasExplicitHardCapTriggerAnalysis = !string.Equals(hardCapTriggerAnalysis.Status, "unavailable", StringComparison.Ordinal);
        var hasDirectHardCapTruth = hardCapTriggerAnalysis.DirectMetricsAvailable;
        var capBasedTargetDecisionAllowed = hardCapTriggerAnalysis.CapBasedDominanceAllowed;
        var unattributedWithinBound = aggregation.AttributionQuality.P95UnattributedShareRatio <= RuntimeTokenBaselineReadinessPolicy.MaxAllowedUnattributedShareRatio;
        var classifiedCoverageReady = aggregation.MassLedgerCoverage.P95ClassifiedSegmentCoverageRatio >= RuntimeTokenBaselineReadinessPolicy.MinClassifiedSegmentCoverageRatio;
        var attributionShareBlockingReasons = new List<string>();
        var taskCostBlockingReasons = new List<string>();
        var routeReinjectionBlockingReasons = new List<string>();
        var capTruthBlockingReasons = new List<string>();
        var activeCanaryBlockingReasons = new List<string>();

        if (!hasRequestKindBreakdown)
        {
            attributionShareBlockingReasons.Add("request_kind_breakdown_missing");
        }

        if (!hasP95SegmentShares)
        {
            attributionShareBlockingReasons.Add("p95_segment_shares_missing");
        }

        if (!hasRendererVsNonRendererSplit)
        {
            attributionShareBlockingReasons.Add("context_pack_vs_non_context_pack_missing");
        }

        if (!hasStableVsDynamicSplit)
        {
            attributionShareBlockingReasons.Add("stable_vs_dynamic_missing");
        }

        if (!hasTrimPressureVisibility)
        {
            attributionShareBlockingReasons.Add("trim_pressure_visibility_missing");
        }

        if (!hasAttributionQuality)
        {
            attributionShareBlockingReasons.Add("attribution_quality_missing");
        }

        if (!hasContextWindowView)
        {
            attributionShareBlockingReasons.Add("context_window_view_missing");
        }

        if (!hasBillableCostView)
        {
            attributionShareBlockingReasons.Add("billable_cost_view_missing");
        }

        if (!hasSuccessfulTaskCostView)
        {
            taskCostBlockingReasons.Add(
                outcomeBinding.TaskCostViewTrusted
                    ? "successful_task_cost_view_missing"
                    : "successful_task_cost_view_untrusted");
        }

        if (!unattributedWithinBound)
        {
            attributionShareBlockingReasons.Add("unattributed_share_exceeds_bound");
        }

        if (!classifiedCoverageReady)
        {
            attributionShareBlockingReasons.Add("classified_segment_coverage_below_threshold");
        }

        if (outcomeBinding.OperatorReadbackInclusions.Any(item => !item.Included))
        {
            routeReinjectionBlockingReasons.AddRange(
                outcomeBinding.OperatorReadbackInclusions
                    .Where(item => !item.Included && !string.IsNullOrWhiteSpace(item.ExclusionReason))
                    .Select(item => item.ExclusionReason!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal));
        }

        if (!hasDirectHardCapTruth)
        {
            capTruthBlockingReasons.Add("direct_cap_truth_missing");
        }
        else if (!capBasedTargetDecisionAllowed)
        {
            capTruthBlockingReasons.Add("cap_based_dominance_not_observed");
        }

        activeCanaryBlockingReasons.Add("phase_0a_cannot_unlock_active_canary");
        if (taskCostBlockingReasons.Count > 0)
        {
            activeCanaryBlockingReasons.AddRange(taskCostBlockingReasons);
        }
        if (routeReinjectionBlockingReasons.Count > 0)
        {
            activeCanaryBlockingReasons.AddRange(routeReinjectionBlockingReasons);
        }
        if (capTruthBlockingReasons.Count > 0)
        {
            activeCanaryBlockingReasons.AddRange(capTruthBlockingReasons);
        }

        var attributionShareReady = attributionShareBlockingReasons.Count == 0;
        var taskCostReady = taskCostBlockingReasons.Count == 0;
        var routeReinjectionReady = routeReinjectionBlockingReasons.Count == 0;
        var capTruthReady = capTruthBlockingReasons.Count == 0;
        var phase10TargetDecisionAllowed = attributionShareReady;
        var totalCostClaimAllowed = taskCostReady;
        var activeCanaryAllowed = false;
        var blockingReasons = attributionShareBlockingReasons.ToArray();

        return new RuntimeTokenDecisionInputsReadiness
        {
            HasRequestKindBreakdown = hasRequestKindBreakdown,
            HasP95SegmentShares = hasP95SegmentShares,
            HasRendererVsNonRendererSplit = hasRendererVsNonRendererSplit,
            HasStableVsDynamicSplit = hasStableVsDynamicSplit,
            HasTrimPressureVisibility = hasTrimPressureVisibility,
            HasAttributionQuality = hasAttributionQuality,
            HasContextWindowView = hasContextWindowView,
            HasBillableCostView = hasBillableCostView,
            HasSuccessfulTaskCostView = hasSuccessfulTaskCostView,
            HasExplicitHardCapTriggerAnalysis = hasExplicitHardCapTriggerAnalysis,
            HasDirectHardCapTruth = hasDirectHardCapTruth,
            CapBasedTargetDecisionAllowed = capBasedTargetDecisionAllowed,
            AttributionShareReady = attributionShareReady,
            TaskCostReady = taskCostReady,
            RouteReinjectionReady = routeReinjectionReady,
            CapTruthReady = capTruthReady,
            Phase10TargetDecisionAllowed = phase10TargetDecisionAllowed,
            TotalCostClaimAllowed = totalCostClaimAllowed,
            ActiveCanaryAllowed = activeCanaryAllowed,
            ReadyForPhase10TargetDecision = phase10TargetDecisionAllowed,
            AttributionShareBlockingReasons = attributionShareBlockingReasons.ToArray(),
            TaskCostBlockingReasons = taskCostBlockingReasons.ToArray(),
            RouteReinjectionBlockingReasons = routeReinjectionBlockingReasons.ToArray(),
            CapTruthBlockingReasons = capTruthBlockingReasons.ToArray(),
            ActiveCanaryBlockingReasons = activeCanaryBlockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            BlockingReasons = blockingReasons,
        };
    }

    private static RuntimeTokenPhase10DecisionInputs BuildDecisionInputs(
        RuntimeTokenBaselineAggregation aggregation,
        RuntimeTokenHardCapTriggerAnalysis hardCapTriggerAnalysis)
    {
        var topContributors = aggregation.SegmentKindShares
            .Where(item => !string.IsNullOrWhiteSpace(item.SegmentKind)
                           && !string.Equals(item.SegmentKind, "context_pack", StringComparison.Ordinal))
            .OrderByDescending(item => item.P95ContextWindowContributionTokens)
            .ThenByDescending(item => item.P95ShareRatio)
            .ThenBy(item => item.SegmentKind, StringComparer.Ordinal)
            .Take(5)
            .Select(item => new RuntimeTokenPhase10ContributorSummary
            {
                SegmentKind = item.SegmentKind,
                TargetSegmentClass = RuntimeTokenPhase10DecisionPolicy.ClassifyOptimizationSurface(item.SegmentKind),
                ShareP95 = item.P95ShareRatio,
                ContextTokensP95 = item.P95ContextWindowContributionTokens,
                BillableTokensP95 = item.P95BillableContributionTokens,
            })
            .ToArray();

        var totalTrimmedP95 = aggregation.TopTrimmedContributors.Sum(item => item.P95TrimmedTokensEst);
        var topTrimmedContributors = aggregation.TopTrimmedContributors
            .OrderByDescending(item => item.P95TrimmedTokensEst)
            .ThenByDescending(item => item.TotalTrimmedTokensEst)
            .ThenBy(item => item.SegmentKind, StringComparer.Ordinal)
            .Take(5)
            .Select(item => new RuntimeTokenPhase10TrimmedContributorSummary
            {
                SegmentKind = item.SegmentKind,
                TargetSegmentClass = RuntimeTokenPhase10DecisionPolicy.ClassifyOptimizationSurface(item.SegmentKind),
                TrimmedTokensP95 = item.P95TrimmedTokensEst,
                TrimmedShareProxyP95 = totalTrimmedP95 <= 0d ? 0d : item.P95TrimmedTokensEst / totalTrimmedP95,
            })
            .ToArray();

        return new RuntimeTokenPhase10DecisionInputs
        {
            ContextPackExplicitShareP95 = ResolveBucketP95Share(aggregation.ContextPackVersusNonContextPack, "context_pack_explicit"),
            NonContextPackExplicitShareP95 = ResolveBucketP95Share(aggregation.ContextPackVersusNonContextPack, "non_context_pack_explicit"),
            StableExplicitShareP95 = ResolveBucketP95Share(aggregation.StableVersusDynamic, "stable_explicit"),
            DynamicExplicitShareP95 = ResolveBucketP95Share(aggregation.StableVersusDynamic, "dynamic_explicit"),
            RendererShareP95Proxy = ResolveBucketP95Share(aggregation.StableVersusDynamic, "stable_explicit")
                                     + ResolveBucketP95Share(aggregation.StableVersusDynamic, "dynamic_explicit"),
            ToolSchemaShareP95Proxy = ResolveClassShareProxy(aggregation.SegmentKindShares, "tool_schema"),
            WrapperPolicyShareP95Proxy = ResolveClassShareProxy(aggregation.SegmentKindShares, "wrapper"),
            OtherSegmentShareP95Proxy = ResolveClassShareProxy(aggregation.SegmentKindShares, "other"),
            ParentResidualShareP95 = ResolveBucketP95Share(aggregation.ContextPackVersusNonContextPack, "parent_residual"),
            KnownProviderOverheadShareP95 = ResolveBucketP95Share(aggregation.ContextPackVersusNonContextPack, "known_provider_overhead"),
            UnknownUnattributedShareP95 = ResolveBucketP95Share(aggregation.ContextPackVersusNonContextPack, "unknown_unattributed"),
            TopP95Contributors = topContributors,
            TopTrimmedContributors = topTrimmedContributors,
            HardCapTriggerSegments = BuildHardCapTriggerSegments(hardCapTriggerAnalysis),
        };
    }

    private static RuntimeTokenPhase10TargetRecommendation BuildRecommendation(
        RuntimeTokenDecisionInputsReadiness readiness,
        RuntimeTokenPhase10DecisionInputs decisionInputs,
        RuntimeTokenHardCapTriggerAnalysis hardCapTriggerAnalysis)
    {
        if (!readiness.Phase10TargetDecisionAllowed)
        {
            return new RuntimeTokenPhase10TargetRecommendation
            {
                Decision = "insufficient_data",
                NextTrack = "insufficient_data",
                Confidence = "low",
                BlockedCriteria = readiness.AttributionShareBlockingReasons,
            };
        }

        var secondContributor = decisionInputs.TopP95Contributors.Skip(1).FirstOrDefault();
        var rendererTarget = BuildRendererCandidate(decisionInputs, hardCapTriggerAnalysis);
        var nonRendererTarget = BuildNonRendererCandidate(decisionInputs, hardCapTriggerAnalysis);

        var classCandidates = new[]
            {
                new ClassShareCandidate("renderer", decisionInputs.RendererShareP95Proxy),
                new ClassShareCandidate("tool_schema", decisionInputs.ToolSchemaShareP95Proxy),
                new ClassShareCandidate("wrapper", decisionInputs.WrapperPolicyShareP95Proxy),
                new ClassShareCandidate("other", decisionInputs.OtherSegmentShareP95Proxy),
            }
            .OrderByDescending(item => item.ShareP95)
            .ThenBy(item => item.TargetSegmentClass, StringComparer.Ordinal)
            .ToArray();

        var leadingClass = classCandidates.FirstOrDefault();
        var runnerUpClass = classCandidates.Skip(1).FirstOrDefault();
        var ambiguousClassShares = leadingClass is not null
                                   && runnerUpClass is not null
                                   && Math.Abs(leadingClass.ShareP95 - runnerUpClass.ShareP95) < RuntimeTokenPhase10DecisionPolicy.AmbiguousClassShareGapThreshold;

        if (leadingClass is null || leadingClass.ShareP95 <= 0d)
        {
            return new RuntimeTokenPhase10TargetRecommendation
            {
                Decision = "insufficient_data",
                NextTrack = "insufficient_data",
                Confidence = "low",
                BlockedCriteria = ["no_material_target_candidate"],
            };
        }

        if (ambiguousClassShares && rendererTarget.DominanceBasis.Count == 0 && nonRendererTarget.DominanceBasis.Count == 0)
        {
            return new RuntimeTokenPhase10TargetRecommendation
            {
                Decision = "insufficient_data",
                NextTrack = "insufficient_data",
                Confidence = "low",
                BlockedCriteria = ["class_share_gap_below_threshold"],
            };
        }

        if (string.Equals(leadingClass.TargetSegmentClass, "renderer", StringComparison.Ordinal))
        {
            if (rendererTarget.DominanceBasis.Count == 0)
            {
                return new RuntimeTokenPhase10TargetRecommendation
                {
                    Decision = "insufficient_data",
                    TargetSegment = rendererTarget.TargetSegment,
                    TargetSegmentClass = rendererTarget.TargetSegmentClass,
                    TargetShareP95 = rendererTarget.TargetShareP95,
                    TrimmedShareProxyP95 = rendererTarget.TrimmedShareProxyP95,
                    HardCapTriggerSegment = rendererTarget.HardCapTriggerSegment,
                    NextTrack = "insufficient_data",
                    Confidence = "low",
                    BlockedCriteria = ["renderer_dominance_criteria_not_satisfied"],
                };
            }

            return rendererTarget with
            {
                Decision = "proceed_renderer_shadow",
                NextTrack = "renderer_shadow_offline",
                Confidence = ResolveConfidence(rendererTarget.DominanceBasis, leadingClass.ShareP95, secondContributor?.ShareP95),
            };
        }

        if (nonRendererTarget.DominanceBasis.Count == 0)
        {
            return new RuntimeTokenPhase10TargetRecommendation
            {
                Decision = "insufficient_data",
                TargetSegment = nonRendererTarget.TargetSegment,
                TargetSegmentClass = nonRendererTarget.TargetSegmentClass,
                TargetShareP95 = nonRendererTarget.TargetShareP95,
                TrimmedShareProxyP95 = nonRendererTarget.TrimmedShareProxyP95,
                HardCapTriggerSegment = nonRendererTarget.HardCapTriggerSegment,
                NextTrack = "insufficient_data",
                Confidence = "low",
                BlockedCriteria = ["non_renderer_dominance_criteria_not_satisfied"],
            };
        }

        var decision = nonRendererTarget.TargetSegmentClass switch
        {
            "tool_schema" => "reprioritize_to_tool_schema",
            "wrapper" => "reprioritize_to_wrapper",
            _ => "reprioritize_to_other_segment",
        };
        var nextTrack = nonRendererTarget.TargetSegmentClass switch
        {
            "tool_schema" => "tool_schema_shadow_offline",
            "wrapper" => "wrapper_policy_shadow_offline",
            _ => "other_segment_analysis",
        };

        return nonRendererTarget with
        {
            Decision = decision,
            NextTrack = nextTrack,
            Confidence = ResolveConfidence(nonRendererTarget.DominanceBasis, leadingClass.ShareP95, secondContributor?.ShareP95),
        };
    }

    private static RuntimeTokenPhase10TargetRecommendation BuildRendererCandidate(
        RuntimeTokenPhase10DecisionInputs decisionInputs,
        RuntimeTokenHardCapTriggerAnalysis hardCapTriggerAnalysis)
    {
        var targetSegment = decisionInputs.StableExplicitShareP95 >= decisionInputs.DynamicExplicitShareP95
            ? "stable_explicit"
            : "dynamic_explicit";
        var targetShareP95 = Math.Max(decisionInputs.StableExplicitShareP95, decisionInputs.DynamicExplicitShareP95);
        var trimmedCandidate = decisionInputs.TopTrimmedContributors
            .Where(item => string.Equals(item.TargetSegmentClass, "renderer", StringComparison.Ordinal))
            .OrderByDescending(item => item.TrimmedShareProxyP95)
            .ThenBy(item => item.SegmentKind, StringComparer.Ordinal)
            .FirstOrDefault();
        var dominanceBasis = new List<string>();

        if (IsLargestContributorInClass(decisionInputs.TopP95Contributors, "renderer"))
        {
            dominanceBasis.Add("largest_context_share");
        }

        if (targetShareP95 >= RuntimeTokenPhase10DecisionPolicy.TopTwoTargetShareThreshold
            && IsTopTwoContributorInClass(decisionInputs.TopP95Contributors, "renderer"))
        {
            dominanceBasis.Add("top2_context_share_ge_20pct");
        }

        if (trimmedCandidate is not null && trimmedCandidate.TrimmedShareProxyP95 >= RuntimeTokenPhase10DecisionPolicy.TrimmedShareProxyThreshold)
        {
            dominanceBasis.Add("trimmed_share_proxy_ge_40pct");
        }

        var hardCapTriggerSegment = decisionInputs.HardCapTriggerSegments.FirstOrDefault(item =>
            string.Equals(RuntimeTokenPhase10DecisionPolicy.ClassifyOptimizationSurface(item), "renderer", StringComparison.Ordinal));
        if (hardCapTriggerAnalysis.CapBasedDominanceAllowed && !string.IsNullOrWhiteSpace(hardCapTriggerSegment))
        {
            dominanceBasis.Add("direct_cap_trigger");
        }

        return new RuntimeTokenPhase10TargetRecommendation
        {
            TargetSegment = targetSegment,
            TargetSegmentClass = "renderer",
            TargetShareP95 = targetShareP95,
            TrimmedShareProxyP95 = trimmedCandidate?.TrimmedShareProxyP95,
            HardCapTriggerSegment = hardCapTriggerSegment,
            DominanceBasis = dominanceBasis,
        };
    }

    private static RuntimeTokenPhase10TargetRecommendation BuildNonRendererCandidate(
        RuntimeTokenPhase10DecisionInputs decisionInputs,
        RuntimeTokenHardCapTriggerAnalysis hardCapTriggerAnalysis)
    {
        var topNonRendererContributor = decisionInputs.TopP95Contributors
            .Where(item => !string.Equals(item.TargetSegmentClass, "renderer", StringComparison.Ordinal))
            .OrderByDescending(item => item.ShareP95)
            .ThenBy(item => item.SegmentKind, StringComparer.Ordinal)
            .FirstOrDefault();
        if (topNonRendererContributor is null)
        {
            return new RuntimeTokenPhase10TargetRecommendation
            {
                TargetSegmentClass = "other",
                DominanceBasis = Array.Empty<string>(),
            };
        }

        var trimmedCandidate = decisionInputs.TopTrimmedContributors
            .Where(item => string.Equals(item.TargetSegmentClass, topNonRendererContributor.TargetSegmentClass, StringComparison.Ordinal))
            .OrderByDescending(item => item.TrimmedShareProxyP95)
            .ThenBy(item => item.SegmentKind, StringComparer.Ordinal)
            .FirstOrDefault();
        var dominanceBasis = new List<string>();

        if (string.Equals(decisionInputs.TopP95Contributors.FirstOrDefault()?.TargetSegmentClass, topNonRendererContributor.TargetSegmentClass, StringComparison.Ordinal))
        {
            dominanceBasis.Add("largest_context_share");
        }

        if (topNonRendererContributor.ShareP95 >= RuntimeTokenPhase10DecisionPolicy.TopTwoTargetShareThreshold
            && IsTopTwoContributorInClass(decisionInputs.TopP95Contributors, topNonRendererContributor.TargetSegmentClass))
        {
            dominanceBasis.Add("top2_context_share_ge_20pct");
        }

        if (trimmedCandidate is not null && trimmedCandidate.TrimmedShareProxyP95 >= RuntimeTokenPhase10DecisionPolicy.TrimmedShareProxyThreshold)
        {
            dominanceBasis.Add("trimmed_share_proxy_ge_40pct");
        }

        var hardCapTriggerSegment = decisionInputs.HardCapTriggerSegments.FirstOrDefault(item =>
            string.Equals(RuntimeTokenPhase10DecisionPolicy.ClassifyOptimizationSurface(item), topNonRendererContributor.TargetSegmentClass, StringComparison.Ordinal));
        if (hardCapTriggerAnalysis.CapBasedDominanceAllowed && !string.IsNullOrWhiteSpace(hardCapTriggerSegment))
        {
            dominanceBasis.Add("direct_cap_trigger");
        }

        return new RuntimeTokenPhase10TargetRecommendation
        {
            TargetSegment = topNonRendererContributor.SegmentKind,
            TargetSegmentClass = topNonRendererContributor.TargetSegmentClass,
            TargetShareP95 = topNonRendererContributor.ShareP95,
            TrimmedShareProxyP95 = trimmedCandidate?.TrimmedShareProxyP95,
            HardCapTriggerSegment = hardCapTriggerSegment,
            DominanceBasis = dominanceBasis,
        };
    }

    private static IReadOnlyList<string> BuildHardCapTriggerSegments(RuntimeTokenHardCapTriggerAnalysis analysis)
    {
        return new[]
            {
                analysis.PrimaryCapTriggerSegmentKind,
                analysis.CapBasedDominanceAllowed ? analysis.PrimaryTrimPressureSegmentKind : null,
            }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray()!;
    }

    private static double ResolveBucketP95Share(RuntimeTokenBucketShareGroup group, string bucketId)
    {
        return group.Buckets
            .Where(item => string.Equals(item.BucketId, bucketId, StringComparison.Ordinal))
            .Select(item => item.P95ShareRatio)
            .DefaultIfEmpty(0d)
            .First();
    }

    private static double ResolveClassShareProxy(
        IReadOnlyList<RuntimeTokenSegmentShareSummary> segments,
        string targetSegmentClass)
    {
        return segments
            .Where(item => !string.Equals(item.SegmentKind, "context_pack", StringComparison.Ordinal)
                           && string.Equals(RuntimeTokenPhase10DecisionPolicy.ClassifyOptimizationSurface(item.SegmentKind), targetSegmentClass, StringComparison.Ordinal))
            .Sum(item => item.P95ShareRatio);
    }

    private static bool IsLargestContributorInClass(
        IReadOnlyList<RuntimeTokenPhase10ContributorSummary> contributors,
        string targetSegmentClass)
    {
        return string.Equals(contributors.FirstOrDefault()?.TargetSegmentClass, targetSegmentClass, StringComparison.Ordinal);
    }

    private static bool IsTopTwoContributorInClass(
        IReadOnlyList<RuntimeTokenPhase10ContributorSummary> contributors,
        string targetSegmentClass)
    {
        return contributors
            .Take(2)
            .Any(item => string.Equals(item.TargetSegmentClass, targetSegmentClass, StringComparison.Ordinal));
    }

    private static string ResolveConfidence(
        IReadOnlyList<string> dominanceBasis,
        double leadingShare,
        double? runnerUpShare)
    {
        if (dominanceBasis.Contains("direct_cap_trigger", StringComparer.Ordinal)
            || (dominanceBasis.Contains("largest_context_share", StringComparer.Ordinal)
                && dominanceBasis.Contains("trimmed_share_proxy_ge_40pct", StringComparer.Ordinal)))
        {
            return "high";
        }

        if (dominanceBasis.Count > 0
            && (!runnerUpShare.HasValue || (leadingShare - runnerUpShare.Value) >= RuntimeTokenPhase10DecisionPolicy.AmbiguousClassShareGapThreshold))
        {
            return "medium";
        }

        return "low";
    }

    private sealed record ClassShareCandidate(string TargetSegmentClass, double ShareP95);

    private static void ValidateInputs(RuntimeTokenBaselineAggregation aggregation, RuntimeTokenOutcomeBinding outcomeBinding)
    {
        if (!string.Equals(aggregation.Cohort.CohortId, outcomeBinding.Cohort.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Baseline evidence result formatter requires matching cohort ids.");
        }

        if (aggregation.Cohort.WindowStartUtc != outcomeBinding.Cohort.WindowStartUtc
            || aggregation.Cohort.WindowEndUtc != outcomeBinding.Cohort.WindowEndUtc)
        {
            throw new InvalidOperationException("Baseline evidence result formatter requires matching cohort windows.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-0a-attribution-baseline-evidence-result-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-0a",
            $"attribution-baseline-evidence-result-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string absolutePath)
    {
        return Path.GetRelativePath(repoRoot, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string FormatBreakdown(IReadOnlyList<RuntimeTokenCountBreakdown> items)
    {
        return items.Count == 0
            ? "none"
            : string.Join(", ", items.Select(item => $"{item.Key}={item.Count}"));
    }

    private static string FormatRatio(double value)
    {
        return value.ToString("0.000");
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.##");
    }

    private static string FormatNullableNumber(double? value)
    {
        return value.HasValue ? FormatNumber(value.Value) : "n/a";
    }

    private static string ToYesNo(bool value)
    {
        return value ? "yes" : "no";
    }
}
