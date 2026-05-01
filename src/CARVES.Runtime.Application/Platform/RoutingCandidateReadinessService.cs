using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RoutingCandidateReadinessService
{
    private readonly RoutingPromotionDecisionService promotionDecisionService;
    private readonly ValidationCoverageMatrixService coverageMatrixService;

    public RoutingCandidateReadinessService(
        RoutingPromotionDecisionService promotionDecisionService,
        ValidationCoverageMatrixService coverageMatrixService)
    {
        this.promotionDecisionService = promotionDecisionService;
        this.coverageMatrixService = coverageMatrixService;
    }

    public RoutingCandidateReadiness Build(string? candidateId = null, int historyLimit = 10)
    {
        var decision = promotionDecisionService.Evaluate(candidateId, historyLimit);
        var matrix = coverageMatrixService.Build(candidateId, historyLimit);
        var multiBatchCovered = matrix.ValidationBatchCount > 1;
        var families = matrix.Families
            .Select(family =>
            {
                var intentDecision = decision.Intents.FirstOrDefault(intent =>
                    string.Equals(intent.RoutingIntent, family.RoutingIntent, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(intent.ModuleId ?? string.Empty, family.ModuleId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                var blockingReasons = family.MissingEvidence
                    .Select(gap => gap.ReasonCode)
                    .Concat(intentDecision?.ReasonCodes ?? [])
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .ToArray();
                var status = blockingReasons.Length == 0 && multiBatchCovered
                    ? "ready"
                    : family.BaselineCovered || family.RoutingCovered || family.FallbackCovered
                        ? "partially_ready"
                        : "not_ready";
                var nextActions = BuildRecommendedNextActions(family.MissingEvidence, multiBatchCovered);
                return new RoutingCandidateReadinessFamily
                {
                    TaskFamily = family.TaskFamily,
                    RoutingIntent = family.RoutingIntent,
                    ModuleId = family.ModuleId,
                    Status = status,
                    BaselineCovered = family.BaselineCovered,
                    RoutingCovered = family.RoutingCovered,
                    FallbackRequired = family.FallbackRequired,
                    FallbackCovered = family.FallbackCovered,
                    MultiBatchCovered = multiBatchCovered,
                    MissingEvidence = family.MissingEvidence,
                    BlockingReasons = blockingReasons,
                    RecommendedNextActions = nextActions,
                };
            })
            .ToArray();
        var coveredFamilies = families
            .Where(family => string.Equals(family.Status, "ready", StringComparison.Ordinal))
            .Select(family => family.TaskFamily)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(family => family, StringComparer.Ordinal)
            .ToArray();
        var missingEvidence = matrix.MissingEvidence;
        var blockingReasons = decision.ReasonCodes
            .Concat(missingEvidence.Select(gap => gap.ReasonCode))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        var status = decision.Eligible && missingEvidence.Length == 0 && multiBatchCovered
            ? "ready"
            : families.Any(family => string.Equals(family.Status, "partially_ready", StringComparison.Ordinal))
                ? "partially_ready"
                : "not_ready";
        var recommendedNextActions = families
            .SelectMany(family => family.RecommendedNextActions)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(action => action, StringComparer.Ordinal)
            .ToArray();
        var summary = status switch
        {
            "ready" => $"Candidate '{decision.CandidateId}' is ready for promotion.",
            "partially_ready" => $"Candidate '{decision.CandidateId}' is partially ready; fill the remaining coverage gaps before promotion.",
            _ => $"Candidate '{decision.CandidateId}' is not ready; required validation evidence is still missing.",
        };

        return new RoutingCandidateReadiness
        {
            CandidateId = decision.CandidateId,
            ProfileId = decision.ProfileId,
            Status = status,
            PromotionEligible = decision.Eligible,
            ValidationBatchCount = matrix.ValidationBatchCount,
            Summary = summary,
            CoveredTaskFamilies = coveredFamilies,
            MissingEvidence = missingEvidence,
            BlockingReasons = blockingReasons,
            RecommendedNextActions = recommendedNextActions,
            Families = families,
        };
    }

    private static string[] BuildRecommendedNextActions(
        IReadOnlyList<ValidationCoverageGap> missingEvidence,
        bool multiBatchCovered)
    {
        var actions = new List<string>();
        actions.AddRange(missingEvidence.Select(gap =>
            $"run {DescribeMode(gap.RequiredMode)} validation for {gap.TaskFamily}"));
        if (!multiBatchCovered)
        {
            actions.Add("run another validation batch to establish multi-batch evidence");
        }

        return actions
            .Distinct(StringComparer.Ordinal)
            .OrderBy(action => action, StringComparer.Ordinal)
            .ToArray();
    }

    private static string DescribeMode(RoutingValidationMode mode)
    {
        return mode switch
        {
            RoutingValidationMode.ForcedFallback => "forced-fallback",
            RoutingValidationMode.Baseline => "baseline",
            _ => "routing",
        };
    }
}
