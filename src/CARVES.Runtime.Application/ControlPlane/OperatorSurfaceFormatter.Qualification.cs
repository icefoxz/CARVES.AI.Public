using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RoutingProfile(RuntimeRoutingProfile? profile)
    {
        if (profile is null)
        {
            return OperatorCommandResult.Success(
                "Active routing profile: (none)",
                "Runtime is using existing registry/policy-based worker selection only.");
        }

        var lines = new List<string>
        {
            $"Active routing profile: {profile.ProfileId}",
            $"Version: {profile.Version}",
            $"Source qualification: {profile.SourceQualificationId ?? "(none)"}",
            $"Summary: {profile.Summary ?? "(none)"}",
            $"Activated at: {profile.ActivatedAt?.ToString("O") ?? "(none)"}",
            $"Rules: {profile.Rules.Length}",
        };
        lines.AddRange(profile.Rules.Select(rule =>
            $"- {rule.RuleId}: intent={rule.RoutingIntent ?? "(any)"}; module={rule.ModuleId ?? "(any)"}; preferred={rule.PreferredRoute.ProviderId}/{rule.PreferredRoute.BackendId ?? "(none)"}/{rule.PreferredRoute.RoutingProfileId ?? "(none)"}/{rule.PreferredRoute.RequestFamily ?? "(none)"}/{rule.PreferredRoute.Model ?? "(none)"}@{rule.PreferredRoute.BaseUrl ?? "(none)"}; fallbacks={(rule.FallbackRoutes.Length == 0 ? "(none)" : string.Join(", ", rule.FallbackRoutes.Select(route => $"{route.ProviderId}/{route.BackendId ?? "(none)"}/{route.RoutingProfileId ?? "(none)"}/{route.RequestFamily ?? "(none)"}/{route.Model ?? "(none)"}@{route.BaseUrl ?? "(none)"}")))}"));
        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult Qualification(ModelQualificationMatrix matrix, ModelQualificationRunLedger? latestRun)
    {
        var lines = new List<string>
        {
            $"Qualification matrix: {matrix.MatrixId}",
            $"Version: {matrix.Version}",
            $"Summary: {matrix.Summary ?? "(none)"}",
            $"Default attempts: {matrix.DefaultAttempts}",
            $"Lanes: {matrix.Lanes.Length}",
        };
        lines.AddRange(matrix.Lanes.Length == 0
            ? ["(none)"]
            : matrix.Lanes.Select(lane => $"- {lane.LaneId}: {lane.ProviderId}/{lane.BackendId}/{lane.RequestFamily}/{lane.Model}; env={lane.ApiKeyEnvironmentVariable}; route_group={lane.RouteGroup ?? "(none)"}; variance={lane.ObservedVariance ?? "(none)"}"));
        lines.Add($"Cases: {matrix.Cases.Length}");
        lines.AddRange(matrix.Cases.Select(item => $"- {item.CaseId}: intent={item.RoutingIntent}; module={item.ModuleId ?? "(none)"}; format={item.ExpectedFormat}; attempts={(item.Attempts?.ToString() ?? matrix.DefaultAttempts.ToString())}"));
        if (latestRun is null)
        {
            lines.Add("Latest run: (none)");
        }
        else
        {
            lines.Add($"Latest run: {latestRun.RunId} ({latestRun.Results.Length} records at {latestRun.GeneratedAt:O})");
            lines.AddRange(latestRun.Results
                .GroupBy(item => item.LaneId, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group =>
                {
                    var results = group.ToArray();
                    var successes = results.Count(item => item.Success);
                    var valid = results.Count(item => item.FormatValid);
                    var avgLatency = results.Where(item => item.LatencyMs.HasValue).Select(item => item.LatencyMs!.Value).DefaultIfEmpty(0).Average();
                    var avgQuality = results.DefaultIfEmpty().Average(item => item?.QualityScore ?? 0);
                    return $"- lane={group.Key}: success={successes}/{results.Length}; format_valid={valid}/{results.Length}; avg_quality={avgQuality:F1}; avg_latency_ms={avgLatency:F0}";
                }));
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult QualificationCandidate(ModelQualificationCandidateProfile? candidate)
    {
        if (candidate is null)
        {
            return OperatorCommandResult.Success(
                "Candidate routing profile: (none)",
                "Run qualification and materialize a candidate before promotion.");
        }

        var lines = new List<string>
        {
            $"Candidate routing profile: {candidate.CandidateId}",
            $"Matrix: {candidate.MatrixId}",
            $"Source run: {candidate.SourceRunId}",
            $"Summary: {candidate.Summary ?? "(none)"}",
            $"Profile id: {candidate.Profile.ProfileId}",
            $"Rules: {candidate.Profile.Rules.Length}",
        };
        lines.AddRange(candidate.Intents.Select(intent =>
            $"- intent={intent.RoutingIntent}; module={intent.ModuleId ?? "(none)"}; preferred={intent.PreferredLaneId}; fallbacks={(intent.FallbackLaneIds.Length == 0 ? "(none)" : string.Join(", ", intent.FallbackLaneIds))}; rejects={(intent.RejectLaneIds.Length == 0 ? "(none)" : string.Join(", ", intent.RejectLaneIds))}"));
        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult QualificationPromotionDecision(RoutingPromotionDecision decision)
    {
        var lines = new List<string>
        {
            $"Qualification promotion decision: {decision.DecisionId}",
            $"Candidate: {decision.CandidateId}",
            $"Profile id: {decision.ProfileId}",
            $"Source run: {decision.SourceRunId}",
            $"Eligible: {decision.Eligible}",
            $"Evidence batches: {decision.EvidenceBatchCount}",
            $"Multi-batch evidence: {decision.MultiBatchEvidence}",
            $"Baseline comparisons: {decision.BaselineComparisonCount}",
            $"Routing evidence count: {decision.RoutingEvidenceCount}",
            $"Fallback evidence count: {decision.FallbackEvidenceCount}",
            $"Summary: {decision.Summary}",
        };
        lines.Add("Reason codes:");
        lines.AddRange(decision.ReasonCodes.Length == 0
            ? ["(none)"]
            : decision.ReasonCodes.Select(reason => $"- {reason}"));
        lines.Add("Intent decisions:");
        lines.AddRange(decision.Intents.Length == 0
            ? ["(none)"]
            : decision.Intents.Select(intent =>
                $"- intent={intent.RoutingIntent}; module={intent.ModuleId ?? "(none)"}; eligible={intent.Eligible}; baseline={intent.BaselineTraceId ?? "(none)"}; routing={intent.RoutingTraceId ?? "(none)"}; fallback={intent.FallbackTraceId ?? "(none)"}; reasons={(intent.ReasonCodes.Length == 0 ? "(none)" : string.Join(", ", intent.ReasonCodes))}; {intent.Summary}"));
        return new OperatorCommandResult(decision.Eligible ? 0 : 1, lines);
    }

    public static OperatorCommandResult RoutingCandidateReadiness(RoutingCandidateReadiness readiness)
    {
        var lines = new List<string>
        {
            $"Routing candidate readiness: {readiness.ReadinessId}",
            $"Candidate: {readiness.CandidateId}",
            $"Profile id: {readiness.ProfileId}",
            $"Generated at: {readiness.GeneratedAt:O}",
            $"Status: {readiness.Status}",
            $"Promotion eligible: {readiness.PromotionEligible}",
            $"Validation batches: {readiness.ValidationBatchCount}",
            $"Summary: {readiness.Summary}",
        };
        lines.Add("Covered task families:");
        lines.AddRange(readiness.CoveredTaskFamilies.Length == 0
            ? ["(none)"]
            : readiness.CoveredTaskFamilies.Select(family => $"- {family}"));
        lines.Add("Blocking reasons:");
        lines.AddRange(readiness.BlockingReasons.Length == 0
            ? ["(none)"]
            : readiness.BlockingReasons.Select(reason => $"- {reason}"));
        lines.Add("Missing evidence:");
        lines.AddRange(readiness.MissingEvidence.Length == 0
            ? ["(none)"]
            : readiness.MissingEvidence.Select(gap =>
                $"- {gap.RequiredMode}: family={gap.TaskFamily}; intent={gap.RoutingIntent}; module={gap.ModuleId ?? "(none)"}; {gap.ReasonCode}"));
        lines.Add("Recommended next actions:");
        lines.AddRange(readiness.RecommendedNextActions.Length == 0
            ? ["(none)"]
            : readiness.RecommendedNextActions.Select(action => $"- {action}"));
        lines.Add("Families:");
        lines.AddRange(readiness.Families.Length == 0
            ? ["(none)"]
            : readiness.Families.Select(family =>
                $"- {family.TaskFamily}: status={family.Status}; intent={family.RoutingIntent}; module={family.ModuleId ?? "(none)"}; baseline={family.BaselineCovered}; routing={family.RoutingCovered}; fallback_required={family.FallbackRequired}; fallback={family.FallbackCovered}; multi_batch={family.MultiBatchCovered}; blocking={(family.BlockingReasons.Length == 0 ? "(none)" : string.Join(", ", family.BlockingReasons))}; next={(family.RecommendedNextActions.Length == 0 ? "(none)" : string.Join(", ", family.RecommendedNextActions))}"));
        return OperatorCommandResult.Success(lines.ToArray());
    }
}
