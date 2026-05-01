using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RoutingPromotionDecisionService
{
    private readonly CurrentModelQualificationService qualificationService;
    private readonly RoutingValidationService routingValidationService;

    public RoutingPromotionDecisionService(
        CurrentModelQualificationService qualificationService,
        RoutingValidationService routingValidationService)
    {
        this.qualificationService = qualificationService;
        this.routingValidationService = routingValidationService;
    }

    public RoutingPromotionDecision Evaluate(string? candidateId = null, int historyLimit = 10)
    {
        var candidate = qualificationService.LoadCandidate() ?? throw new InvalidOperationException("No candidate routing profile exists.");
        if (!string.IsNullOrWhiteSpace(candidateId) && !string.Equals(candidate.CandidateId, candidateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Candidate '{candidateId}' does not match the current candidate '{candidate.CandidateId}'.");
        }

        var traces = routingValidationService.LoadTraces();
        var history = routingValidationService.LoadHistory(historyLimit);
        var intentDecisions = candidate.Intents
            .Select(intent => EvaluateIntent(intent, traces))
            .ToArray();
        var reasonCodes = intentDecisions
            .SelectMany(intent => intent.ReasonCodes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();
        var baselineComparisons = intentDecisions.Count(intent => intent.BaselineTraceId is not null && intent.RoutingTraceId is not null);
        var routingEvidenceCount = intentDecisions.Count(intent => intent.RoutingTraceId is not null);
        var fallbackEvidenceCount = intentDecisions.Count(intent => intent.FallbackTraceId is not null);
        var eligible = intentDecisions.Length > 0 && intentDecisions.All(intent => intent.Eligible);
        var summary = eligible
            ? $"Candidate '{candidate.CandidateId}' is eligible for promotion with {baselineComparisons} baseline comparisons across {intentDecisions.Length} intents."
            : $"Candidate '{candidate.CandidateId}' is ineligible for promotion: {string.Join(", ", reasonCodes.DefaultIfEmpty("insufficient_validation_evidence"))}.";

        return new RoutingPromotionDecision
        {
            CandidateId = candidate.CandidateId,
            ProfileId = candidate.Profile.ProfileId,
            SourceRunId = candidate.SourceRunId,
            Eligible = eligible,
            MultiBatchEvidence = history.BatchCount > 1,
            EvidenceBatchCount = history.BatchCount,
            BaselineComparisonCount = baselineComparisons,
            RoutingEvidenceCount = routingEvidenceCount,
            FallbackEvidenceCount = fallbackEvidenceCount,
            Summary = summary,
            ReasonCodes = reasonCodes.ToArray(),
            Intents = intentDecisions,
        };
    }

    private static RoutingPromotionIntentDecision EvaluateIntent(
        ModelQualificationIntentSummary intent,
        IReadOnlyList<RoutingValidationTrace> traces)
    {
        var matchingTraces = traces
            .Where(trace => string.Equals(trace.RoutingIntent, intent.RoutingIntent, StringComparison.OrdinalIgnoreCase)
                && string.Equals(trace.ModuleId ?? string.Empty, intent.ModuleId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(trace => trace.EndedAt)
            .ToArray();

        var baseline = matchingTraces.FirstOrDefault(trace => trace.ExecutionMode == RoutingValidationMode.Baseline);
        var routing = matchingTraces.FirstOrDefault(trace =>
            trace.ExecutionMode == RoutingValidationMode.Routing
            && string.Equals(trace.SelectedLane, intent.PreferredLaneId, StringComparison.Ordinal));
        var fallback = matchingTraces.FirstOrDefault(trace =>
            trace.ExecutionMode == RoutingValidationMode.ForcedFallback
            && intent.FallbackLaneIds.Contains(trace.SelectedLane ?? string.Empty, StringComparer.Ordinal));

        var reasonCodes = new List<string>();
        if (baseline is null)
        {
            reasonCodes.Add("missing_baseline_comparison");
        }

        if (routing is null)
        {
            reasonCodes.Add("missing_routing_evidence");
        }

        if (intent.FallbackLaneIds.Length > 0 && fallback is null)
        {
            reasonCodes.Add("missing_fallback_evidence");
        }

        if (baseline is not null && routing is not null)
        {
            if (baseline.TaskSucceeded && !routing.TaskSucceeded)
            {
                reasonCodes.Add("routing_success_regression");
            }

            if (baseline.SchemaValid && !routing.SchemaValid)
            {
                reasonCodes.Add("routing_schema_regression");
            }

            if (baseline.BuildOutcome == RoutingValidationExecutionOutcome.Passed && routing.BuildOutcome == RoutingValidationExecutionOutcome.Failed)
            {
                reasonCodes.Add("routing_build_regression");
            }

            if (baseline.TestOutcome == RoutingValidationExecutionOutcome.Passed && routing.TestOutcome == RoutingValidationExecutionOutcome.Failed)
            {
                reasonCodes.Add("routing_test_regression");
            }

            if (baseline.SafetyOutcome == RoutingValidationExecutionOutcome.Passed && routing.SafetyOutcome is RoutingValidationExecutionOutcome.Failed or RoutingValidationExecutionOutcome.Rejected)
            {
                reasonCodes.Add("routing_safety_regression");
            }
        }

        var eligible = reasonCodes.Count == 0;
        var summary = eligible
            ? $"Intent '{intent.RoutingIntent}' has baseline, preferred route, and fallback evidence."
            : $"Intent '{intent.RoutingIntent}' is blocked by {string.Join(", ", reasonCodes)}.";

        return new RoutingPromotionIntentDecision
        {
            RoutingIntent = intent.RoutingIntent,
            ModuleId = intent.ModuleId,
            PreferredLaneId = intent.PreferredLaneId,
            FallbackLaneIds = intent.FallbackLaneIds,
            BaselineTraceId = baseline?.TraceId,
            RoutingTraceId = routing?.TraceId,
            FallbackTraceId = fallback?.TraceId,
            Eligible = eligible,
            Summary = summary,
            ReasonCodes = reasonCodes.ToArray(),
        };
    }
}
