using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenBaselineReadinessGateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeTokenBaselineReadinessGateService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenBaselineReadinessGateResult Persist(RuntimeTokenBaselineEvidenceResult evidenceResult)
    {
        return Persist(paths, evidenceResult, evidenceResult.ResultDate);
    }

    internal static RuntimeTokenBaselineReadinessGateResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(evidenceResult);

        var checks = BuildChecks(evidenceResult);
        var readiness = BuildReadiness(evidenceResult);
        var blockingReasons = checks
            .Where(check => check.Blocking && !check.Passed)
            .Select(check => check.CheckId)
            .Concat(readiness.AttributionShareBlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reason => reason, StringComparer.Ordinal)
            .ToArray();
        var unlocksPhase10TargetDecision = readiness.Phase10TargetDecisionAllowed && blockingReasons.Length == 0;

        var gateResult = new RuntimeTokenBaselineReadinessGateResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            Verdict = unlocksPhase10TargetDecision ? "ready_for_phase_1_target_work" : "insufficient_data",
            UnlocksPhase10TargetDecision = unlocksPhase10TargetDecision,
            Readiness = readiness,
            Checks = checks,
            BlockingReasons = blockingReasons,
            Notes = BuildNotes(evidenceResult),
        };

        var markdownPath = GetMarkdownArtifactPathFor(paths, resultDate);
        var jsonPath = GetJsonArtifactPathFor(paths, resultDate);
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(gateResult));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(gateResult, JsonOptions));
        return gateResult;
    }

    internal static string FormatMarkdown(RuntimeTokenBaselineReadinessGateResult gateResult)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 0A Readiness Gate Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{gateResult.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{gateResult.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Verdict: `{gateResult.Verdict}`");
        builder.AppendLine($"- Unlocks Phase 1.0 target decision: `{(gateResult.UnlocksPhase10TargetDecision ? "yes" : "no")}`");
        builder.AppendLine($"- Evidence markdown artifact: `{gateResult.EvidenceMarkdownArtifactPath}`");
        builder.AppendLine($"- Evidence json artifact: `{gateResult.EvidenceJsonArtifactPath}`");
        builder.AppendLine();

        builder.AppendLine("## Readiness");
        builder.AppendLine();
        builder.AppendLine($"- Attribution share ready: `{(gateResult.Readiness.AttributionShareReady ? "yes" : "no")}`");
        builder.AppendLine($"- Task cost ready: `{(gateResult.Readiness.TaskCostReady ? "yes" : "no")}`");
        builder.AppendLine($"- Route reinjection ready: `{(gateResult.Readiness.RouteReinjectionReady ? "yes" : "no")}`");
        builder.AppendLine($"- Cap truth ready: `{(gateResult.Readiness.CapTruthReady ? "yes" : "no")}`");
        builder.AppendLine($"- Phase 1.0 target decision allowed: `{(gateResult.Readiness.Phase10TargetDecisionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Cap-based target decision allowed: `{(gateResult.Readiness.CapBasedTargetDecisionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Total cost claim allowed: `{(gateResult.Readiness.TotalCostClaimAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Active canary allowed: `{(gateResult.Readiness.ActiveCanaryAllowed ? "yes" : "no")}`");
        AppendBlockingReasons(builder, "Attribution share blocking reasons", gateResult.Readiness.AttributionShareBlockingReasons);
        AppendBlockingReasons(builder, "Task cost blocking reasons", gateResult.Readiness.TaskCostBlockingReasons);
        AppendBlockingReasons(builder, "Route reinjection blocking reasons", gateResult.Readiness.RouteReinjectionBlockingReasons);
        AppendBlockingReasons(builder, "Cap truth blocking reasons", gateResult.Readiness.CapTruthBlockingReasons);
        AppendBlockingReasons(builder, "Active canary blocking reasons", gateResult.Readiness.ActiveCanaryBlockingReasons);
        builder.AppendLine();

        builder.AppendLine("## Checks");
        builder.AppendLine();
        builder.AppendLine("| Check | Passed | Blocking | Detail |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var check in gateResult.Checks)
        {
            builder.AppendLine($"| `{check.CheckId}` | {(check.Passed ? "yes" : "no")} | {(check.Blocking ? "yes" : "no")} | {EscapePipe(check.Detail)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Blocking Reasons");
        builder.AppendLine();
        if (gateResult.BlockingReasons.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var reason in gateResult.BlockingReasons)
            {
                builder.AppendLine($"- `{reason}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Notes");
        builder.AppendLine();
        if (gateResult.Notes.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var note in gateResult.Notes)
            {
                builder.AppendLine($"- {note}");
            }
        }

        return builder.ToString();
    }

    private static void AppendBlockingReasons(StringBuilder builder, string title, IReadOnlyList<string> reasons)
    {
        builder.AppendLine($"- {title}: {(reasons.Count == 0 ? "none" : string.Join(", ", reasons))}");
    }

    private static RuntimeTokenBaselineReadinessDimensions BuildReadiness(RuntimeTokenBaselineEvidenceResult evidenceResult)
    {
        var hasNonNoneProviderOverheadClassification = evidenceResult.Aggregation.AttributionQuality.KnownProviderOverheadBreakdown
            .Where(item => !string.Equals(item.Key, "none", StringComparison.Ordinal))
            .Sum(item => item.Count);
        var allRequestsClassifiedAsProviderOverhead = evidenceResult.Aggregation.AttributionQuality.RequestCount > 0
                                                     && hasNonNoneProviderOverheadClassification >= evidenceResult.Aggregation.AttributionQuality.RequestCount;
        var derivedAttributionBlockingReasons = new List<string>(evidenceResult.DecisionInputsReadiness.AttributionShareBlockingReasons);
        if (!(evidenceResult.Aggregation.AttributionQuality.P95UnattributedShareRatio <= RuntimeTokenBaselineReadinessPolicy.MaxAllowedUnattributedShareRatio
              || allRequestsClassifiedAsProviderOverhead))
        {
            derivedAttributionBlockingReasons.Add("unattributed_tokens_within_bound_or_classified");
        }

        if (evidenceResult.Aggregation.MassLedgerCoverage.P95ClassifiedSegmentCoverageRatio < RuntimeTokenBaselineReadinessPolicy.MinClassifiedSegmentCoverageRatio)
        {
            derivedAttributionBlockingReasons.Add("classified_segment_coverage_below_threshold");
        }

        var attributionShareBlockingReasons = derivedAttributionBlockingReasons
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var attributionShareReady = attributionShareBlockingReasons.Length == 0;
        var taskCostBlockingReasons = evidenceResult.DecisionInputsReadiness.TaskCostBlockingReasons
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var routeReinjectionBlockingReasons = evidenceResult.DecisionInputsReadiness.RouteReinjectionBlockingReasons
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var capTruthBlockingReasons = evidenceResult.DecisionInputsReadiness.CapTruthBlockingReasons
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var activeCanaryBlockingReasons = evidenceResult.DecisionInputsReadiness.ActiveCanaryBlockingReasons
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        return new RuntimeTokenBaselineReadinessDimensions
        {
            AttributionShareReady = attributionShareReady,
            TaskCostReady = taskCostBlockingReasons.Length == 0,
            RouteReinjectionReady = routeReinjectionBlockingReasons.Length == 0,
            CapTruthReady = capTruthBlockingReasons.Length == 0,
            Phase10TargetDecisionAllowed = attributionShareReady,
            CapBasedTargetDecisionAllowed = evidenceResult.DecisionInputsReadiness.CapBasedTargetDecisionAllowed,
            TotalCostClaimAllowed = taskCostBlockingReasons.Length == 0,
            ActiveCanaryAllowed = false,
            AttributionShareBlockingReasons = attributionShareBlockingReasons,
            TaskCostBlockingReasons = taskCostBlockingReasons,
            RouteReinjectionBlockingReasons = routeReinjectionBlockingReasons,
            CapTruthBlockingReasons = capTruthBlockingReasons,
            ActiveCanaryBlockingReasons = activeCanaryBlockingReasons,
        };
    }

    private static RuntimeTokenBaselineReadinessCheck[] BuildChecks(RuntimeTokenBaselineEvidenceResult evidenceResult)
    {
        var hasNonNoneProviderOverheadClassification = evidenceResult.Aggregation.AttributionQuality.KnownProviderOverheadBreakdown
            .Where(item => !string.Equals(item.Key, "none", StringComparison.Ordinal))
            .Sum(item => item.Count);
        var allRequestsClassifiedAsProviderOverhead = evidenceResult.Aggregation.AttributionQuality.RequestCount > 0
                                                     && hasNonNoneProviderOverheadClassification >= evidenceResult.Aggregation.AttributionQuality.RequestCount;

        return
        [
            new RuntimeTokenBaselineReadinessCheck
            {
                CheckId = "unattributed_tokens_within_bound_or_classified",
                Passed = evidenceResult.Aggregation.AttributionQuality.P95UnattributedShareRatio <= RuntimeTokenBaselineReadinessPolicy.MaxAllowedUnattributedShareRatio
                         || allRequestsClassifiedAsProviderOverhead,
                Blocking = true,
                Detail = evidenceResult.Aggregation.AttributionQuality.P95UnattributedShareRatio <= RuntimeTokenBaselineReadinessPolicy.MaxAllowedUnattributedShareRatio
                    ? $"p95 unattributed share ratio {evidenceResult.Aggregation.AttributionQuality.P95UnattributedShareRatio:0.000} is within the <= {RuntimeTokenBaselineReadinessPolicy.MaxAllowedUnattributedShareRatio:0.00} bound."
                    : allRequestsClassifiedAsProviderOverhead
                        ? "p95 unattributed share exceeds the bound, but all sampled requests are classified as known provider overhead."
                        : $"p95 unattributed share ratio {evidenceResult.Aggregation.AttributionQuality.P95UnattributedShareRatio:0.000} exceeds the <= {RuntimeTokenBaselineReadinessPolicy.MaxAllowedUnattributedShareRatio:0.00} bound without complete provider-overhead classification.",
            },
            new RuntimeTokenBaselineReadinessCheck
            {
                CheckId = "top_p95_contributors_visible",
                Passed = evidenceResult.Aggregation.SegmentKindShares.Any(item => item.P95ContextWindowContributionTokens > 0 || item.P95BillableContributionTokens > 0),
                Blocking = true,
                Detail = "Top p95 contributors are visible when segment share summaries have non-zero p95 contribution tokens.",
            },
            new RuntimeTokenBaselineReadinessCheck
            {
                CheckId = "trim_pressure_visible",
                Passed = evidenceResult.Aggregation.TopTrimmedContributors.Count > 0,
                Blocking = true,
                Detail = "Trimmed-token pressure must be visible through top trimmed contributors.",
            },
            new RuntimeTokenBaselineReadinessCheck
            {
                CheckId = "cap_based_dominance_truth_ready",
                Passed = evidenceResult.HardCapTriggerAnalysis.CapBasedDominanceAllowed,
                Blocking = false,
                Detail = evidenceResult.HardCapTriggerAnalysis.CapBasedDominanceAllowed
                    ? "Direct cap truth is available and cap-based dominance may be used."
                    : $"Cap-based dominance is unavailable because hard cap trigger analysis status is '{evidenceResult.HardCapTriggerAnalysis.Status}'.",
            },
            new RuntimeTokenBaselineReadinessCheck
            {
                CheckId = "renderer_vs_non_renderer_split_visible",
                Passed = evidenceResult.Aggregation.ContextPackVersusNonContextPack.Buckets.Any(item => string.Equals(item.BucketId, "context_pack_explicit", StringComparison.Ordinal))
                         && evidenceResult.Aggregation.ContextPackVersusNonContextPack.Buckets.Any(item => string.Equals(item.BucketId, "non_context_pack_explicit", StringComparison.Ordinal)),
                Blocking = true,
                Detail = "Renderer vs non-renderer dominance requires ContextPack and non-ContextPack buckets.",
            },
            new RuntimeTokenBaselineReadinessCheck
            {
                CheckId = "stable_vs_dynamic_split_visible",
                Passed = evidenceResult.Aggregation.StableVersusDynamic.Buckets.Any(item => string.Equals(item.BucketId, "stable_explicit", StringComparison.Ordinal))
                         && evidenceResult.Aggregation.StableVersusDynamic.Buckets.Any(item => string.Equals(item.BucketId, "dynamic_explicit", StringComparison.Ordinal)),
                Blocking = true,
                Detail = "Stable vs dynamic section share must include both stable and dynamic buckets.",
            },
            new RuntimeTokenBaselineReadinessCheck
            {
                CheckId = "successful_task_cost_view_visible",
                Passed = evidenceResult.DecisionInputsReadiness.TaskCostReady,
                Blocking = false,
                Detail = evidenceResult.OutcomeBinding.TaskCostViewTrusted
                    ? "Successful-task cost view requires at least one successful task and both context-window and billable per-successful-task values."
                    : $"Successful-task cost view is blocked by: {string.Join(", ", evidenceResult.OutcomeBinding.TaskCostViewBlockingReasons)}",
            },
            new RuntimeTokenBaselineReadinessCheck
            {
                CheckId = "decision_inputs_readiness_passes",
                Passed = evidenceResult.DecisionInputsReadiness.Phase10TargetDecisionAllowed,
                Blocking = true,
                Detail = evidenceResult.DecisionInputsReadiness.Phase10TargetDecisionAllowed
                    ? "Formatter phase-1 target decision readiness passed."
                    : $"Formatter phase-1 target decision readiness is blocked by: {string.Join(", ", evidenceResult.DecisionInputsReadiness.AttributionShareBlockingReasons)}",
            },
        ];
    }

    private static IReadOnlyList<string> BuildNotes(RuntimeTokenBaselineEvidenceResult evidenceResult)
    {
        var notes = new List<string>();
        if (evidenceResult.HardCapTriggerAnalysis.UsesTrimPressureProxy)
        {
            notes.Add("Hard-cap analysis currently has trim-pressure proxy support, but proxy evidence does not unlock cap-based dominance.");
        }

        if (evidenceResult.HardCapTriggerAnalysis.DirectMetricsAvailable && !evidenceResult.HardCapTriggerAnalysis.CapBasedDominanceAllowed)
        {
            notes.Add("Direct cap metrics are available, but no direct cap hit was observed in the frozen cohort.");
        }

        if (evidenceResult.OutcomeBinding.UnboundIncludedRequestCount > 0)
        {
            notes.Add($"There are {evidenceResult.OutcomeBinding.UnboundIncludedRequestCount} included requests that could not be bound to task outcome truth.");
        }

        if (!evidenceResult.OutcomeBinding.TaskCostViewTrusted && evidenceResult.OutcomeBinding.TaskCostViewBlockingReasons.Count > 0)
        {
            notes.Add($"Task cost view is not trusted because: {string.Join(", ", evidenceResult.OutcomeBinding.TaskCostViewBlockingReasons)}.");
        }

        if (!evidenceResult.DecisionInputsReadiness.RouteReinjectionReady && evidenceResult.DecisionInputsReadiness.RouteReinjectionBlockingReasons.Count > 0)
        {
            notes.Add($"Route reinjection truth is not ready because: {string.Join(", ", evidenceResult.DecisionInputsReadiness.RouteReinjectionBlockingReasons)}.");
        }

        if (!evidenceResult.DecisionInputsReadiness.CapTruthReady && evidenceResult.DecisionInputsReadiness.CapTruthBlockingReasons.Count > 0)
        {
            notes.Add($"Cap truth is not ready because: {string.Join(", ", evidenceResult.DecisionInputsReadiness.CapTruthBlockingReasons)}.");
        }

        if (evidenceResult.OutcomeBinding.RunReportCoverage.IncludedRequestsMissingMatchingRunReport > 0)
        {
            notes.Add($"There are {evidenceResult.OutcomeBinding.RunReportCoverage.IncludedRequestsMissingMatchingRunReport} included requests with a run id but no matching run report.");
        }

        return notes;
    }

    private static void ValidateInputs(RuntimeTokenBaselineEvidenceResult evidenceResult)
    {
        if (string.IsNullOrWhiteSpace(evidenceResult.MarkdownArtifactPath) || string.IsNullOrWhiteSpace(evidenceResult.JsonArtifactPath))
        {
            throw new InvalidOperationException("Baseline readiness gate requires evidence result artifact paths.");
        }

        if (evidenceResult.Aggregation.RequestCount <= 0)
        {
            throw new InvalidOperationException("Baseline readiness gate requires a non-empty evidence result.");
        }
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-0a-readiness-gate-result-{resultDate:yyyy-MM-dd}.md");
    }

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-0a",
            $"readiness-gate-result-{resultDate:yyyy-MM-dd}.json");
    }

    private static string EscapePipe(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }
}
