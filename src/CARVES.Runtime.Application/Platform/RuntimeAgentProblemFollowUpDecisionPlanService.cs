using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentProblemFollowUpDecisionPlanService
{
    public const string DecisionPlanGuideDocumentPath = "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md";
    public const string Phase37DocumentPath = "docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md";

    private const string Phase35DocumentPath = "docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md";
    private const string Phase36DocumentPath = "docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory;

    public RuntimeAgentProblemFollowUpDecisionPlanService(
        string repoRoot,
        Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.problemRecordsFactory = problemRecordsFactory;
    }

    public RuntimeAgentProblemFollowUpDecisionPlanSurface Build()
    {
        var errors = ValidateBaseDocuments();
        var candidateSurface = new RuntimeAgentProblemFollowUpCandidatesService(repoRoot, problemRecordsFactory).Build();
        errors.AddRange(candidateSurface.Errors.Select(static error => $"runtime-agent-problem-follow-up-candidates:{error}"));

        var decisionItems = candidateSurface.Candidates
            .Select(BuildDecisionItem)
            .OrderByDescending(static item => item.RecommendedDecision == "operator_review_required")
            .ThenByDescending(static item => item.BlockingCount)
            .ThenByDescending(static item => item.ProblemCount)
            .ThenBy(static item => item.ProblemKind, StringComparer.Ordinal)
            .ToArray();
        var ready = errors.Count == 0 && candidateSurface.IsValid;
        var operatorReviewCount = decisionItems.Count(static item => item.RecommendedDecision == "operator_review_required");

        return new RuntimeAgentProblemFollowUpDecisionPlanSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolveOverallPosture(ready, candidateSurface.CandidateCount, operatorReviewCount),
            DecisionPlanId = BuildDecisionPlanId(candidateSurface, decisionItems),
            CandidateSurfacePosture = candidateSurface.OverallPosture,
            CandidateSurfaceReady = candidateSurface.FollowUpCandidatesReady,
            DecisionPlanReady = ready,
            DecisionRequired = ready && operatorReviewCount > 0,
            RecordedProblemCount = candidateSurface.RecordedProblemCount,
            CandidateCount = candidateSurface.CandidateCount,
            GovernedCandidateCount = candidateSurface.GovernedCandidateCount,
            WatchlistCandidateCount = candidateSurface.WatchlistCandidateCount,
            DecisionItemCount = decisionItems.Length,
            OperatorReviewItemCount = operatorReviewCount,
            WatchlistItemCount = decisionItems.Length - operatorReviewCount,
            DecisionItems = decisionItems,
            OperatorDecisionChecklist = BuildOperatorDecisionChecklist(),
            BoundaryRules = BuildBoundaryRules(),
            Gaps = errors.Select(static error => $"agent_problem_follow_up_decision_plan_resource:{error}").ToArray(),
            Summary = BuildSummary(ready, candidateSurface.CandidateCount, operatorReviewCount),
            RecommendedNextAction = BuildRecommendedNextAction(ready, candidateSurface.CandidateCount, operatorReviewCount),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims = BuildNonClaims(),
        };
    }

    private List<string> ValidateBaseDocuments()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(Phase37DocumentPath, "Product closure Phase 37 agent problem follow-up decision plan document", errors);
        ValidateRuntimeDocument(Phase36DocumentPath, "Product closure Phase 36 agent problem follow-up candidates document", errors);
        ValidateRuntimeDocument(Phase35DocumentPath, "Product closure Phase 35 agent problem triage ledger document", errors);
        ValidateRuntimeDocument(DecisionPlanGuideDocumentPath, "Agent problem follow-up decision plan guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpCandidatesService.FollowUpCandidatesGuideDocumentPath, "Agent problem follow-up candidates guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemTriageLedgerService.TriageLedgerGuideDocumentPath, "Agent problem triage ledger guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemIntakeService.ProblemIntakeGuideDocumentPath, "Agent problem intake guide document", errors);
        ValidateRuntimeDocument(RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);
        return errors;
    }

    private static RuntimeAgentProblemFollowUpDecisionItemSurface BuildDecisionItem(RuntimeAgentProblemFollowUpCandidateSurface candidate)
    {
        var requiresOperatorReview = string.Equals(candidate.CandidateStatus, "governed_follow_up_candidate", StringComparison.Ordinal);
        return new RuntimeAgentProblemFollowUpDecisionItemSurface
        {
            CandidateId = candidate.CandidateId,
            CandidateStatus = candidate.CandidateStatus,
            ProblemKind = candidate.ProblemKind,
            RecommendedTriageLane = candidate.RecommendedTriageLane,
            ProblemCount = candidate.ProblemCount,
            BlockingCount = candidate.BlockingCount,
            RecommendedDecision = requiresOperatorReview
                ? "operator_review_required"
                : "wait_for_more_evidence",
            DecisionOptions = requiresOperatorReview
                ?
                [
                    "accept_as_governed_planning_input",
                    "reject_as_target_project_only",
                    "wait_for_more_evidence",
                ]
                :
                [
                    "wait_for_more_evidence",
                    "accept_as_governed_planning_input_after_operator_override",
                    "reject_as_noise_or_target_only",
                ],
            PlanningEntryHint = requiresOperatorReview
                ? "If accepted, run carves intent draft --persist and carves plan init [candidate-card-id] with explicit acceptance evidence; do not create task truth directly from this surface."
                : "Do not open governed planning unless the operator overrides watchlist posture or more evidence appears.",
            RequiredAcceptanceEvidence =
            [
                "related problem ids and evidence ids",
                "operator decision reason",
                "acceptance contract proving how the Runtime or target guidance change resolves the friction",
                "readback command that will prove the candidate is resolved",
            ],
            SuggestedTitle = candidate.SuggestedTitle,
            SuggestedIntent = candidate.SuggestedIntent,
            RelatedProblemIds = candidate.RelatedProblemIds,
            RelatedEvidenceIds = candidate.RelatedEvidenceIds,
        };
    }

    private static string ResolveOverallPosture(bool ready, int candidateCount, int operatorReviewCount)
    {
        if (!ready)
        {
            return "agent_problem_follow_up_decision_plan_blocked";
        }

        if (candidateCount == 0)
        {
            return "agent_problem_follow_up_decision_plan_empty";
        }

        return operatorReviewCount == 0
            ? "agent_problem_follow_up_decision_plan_watchlist_only"
            : "agent_problem_follow_up_decision_plan_ready_for_operator_review";
    }

    private static string BuildSummary(bool ready, int candidateCount, int operatorReviewCount)
    {
        if (!ready)
        {
            return "Agent problem follow-up decision plan is blocked until Phase 37 docs, Phase 36 candidates, and Phase 35 triage anchors are valid.";
        }

        if (candidateCount == 0)
        {
            return "Agent problem follow-up decision plan is ready and empty: there are no candidate patterns to decide.";
        }

        return operatorReviewCount == 0
            ? "Agent problem follow-up decision plan is ready with watchlist-only candidates; no governed follow-up decision is currently required."
            : $"Agent problem follow-up decision plan is ready: {operatorReviewCount} candidate(s) require operator review before any governed work is opened.";
    }

    private static string BuildRecommendedNextAction(bool ready, int candidateCount, int operatorReviewCount)
    {
        if (!ready)
        {
            return "Restore the listed decision-plan gaps, then rerun carves pilot follow-up-plan --json.";
        }

        if (candidateCount == 0)
        {
            return "Continue external dogfood; when problem reports appear, run carves pilot triage --json, carves pilot follow-up --json, and carves pilot follow-up-plan --json.";
        }

        return operatorReviewCount == 0
            ? "Keep watchlist candidates visible; do not open governed work until repeated/blocking evidence appears or the operator explicitly overrides watchlist posture."
            : "For each operator_review_required item, run carves pilot follow-up-record --json, then record accept, reject, or wait with carves pilot record-follow-up-decision before opening governed work.";
    }

    private static string[] BuildOperatorDecisionChecklist()
    {
        return
        [
            "Confirm the candidate is a real Runtime/product friction pattern, not a one-off target misunderstanding.",
            "Decide accept, reject, or wait; do not let an external agent self-accept the candidate.",
            "If accepted, convert the candidate through intent draft --persist and plan init rather than editing card/task truth directly.",
            "Attach related problem ids, evidence ids, and a concrete acceptance contract to the governed work.",
            "If rejected or waiting, leave the evidence visible for future triage rather than deleting reports.",
        ];
    }

    private static string[] BuildBoundaryRules()
    {
        return
        [
            "This plan is read-only and projects operator decision choices; it does not record the decision.",
            "Use carves pilot follow-up-record and carves pilot record-follow-up-decision for durable operator decision records.",
            "Accepting a candidate is an operator act and still requires the existing planning lane.",
            "Watchlist candidates should not become governed work unless the operator overrides them with a reason.",
            "Problem evidence remains evidence; it is not card truth, task truth, review truth, or writeback authority.",
            "Do not mutate protected truth roots, .gitignore, target runtime binding, commits, tags, packs, or releases from this surface.",
        ];
    }

    private static string[] BuildNonClaims()
    {
        return
        [
            "This surface does not create or mutate cards, tasks, acceptance contracts, reviews, writebacks, problem records, commits, or releases.",
            "This surface does not resolve or close problem intake records.",
            "This surface does not record durable operator decisions; it only prepares the decision plan.",
            "This surface does not authorize an external agent to continue past a CARVES blocker.",
            "This surface does not claim OS sandboxing, full ACP, full MCP, remote worker orchestration, or automatic triage.",
        ];
    }

    private static string BuildDecisionPlanId(
        RuntimeAgentProblemFollowUpCandidatesSurface candidateSurface,
        IReadOnlyList<RuntimeAgentProblemFollowUpDecisionItemSurface> decisionItems)
    {
        var builder = new StringBuilder();
        builder.AppendLine(candidateSurface.OverallPosture);
        builder.AppendLine(candidateSurface.RecordedProblemCount.ToString());
        foreach (var item in decisionItems.OrderBy(static item => item.CandidateId, StringComparer.Ordinal))
        {
            builder.Append(item.CandidateId)
                .Append('|')
                .Append(item.CandidateStatus)
                .Append('|')
                .Append(item.RecommendedDecision)
                .Append('|')
                .Append(item.ProblemCount)
                .Append('|')
                .AppendLine(item.BlockingCount.ToString());
            foreach (var problemId in item.RelatedProblemIds.OrderBy(static id => id, StringComparer.Ordinal))
            {
                builder.Append("problem:")
                    .AppendLine(problemId);
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return $"agent-problem-follow-up-decision-plan-{Convert.ToHexString(hash)[..12].ToLowerInvariant()}";
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
