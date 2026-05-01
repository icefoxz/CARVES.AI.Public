using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentProblemFollowUpCandidatesService
{
    public const string FollowUpCandidatesGuideDocumentPath = "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md";
    private const string Phase35DocumentPath = "docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md";
    private const string Phase36DocumentPath = "docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory;

    public RuntimeAgentProblemFollowUpCandidatesService(
        string repoRoot,
        Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.problemRecordsFactory = problemRecordsFactory;
    }

    public RuntimeAgentProblemFollowUpCandidatesSurface Build()
    {
        var errors = ValidateBaseDocuments();
        var records = problemRecordsFactory()
            .OrderByDescending(static record => record.RecordedAtUtc)
            .ThenByDescending(static record => record.ProblemId, StringComparer.Ordinal)
            .ToArray();
        var candidates = BuildCandidates(records);
        var governedCandidateCount = candidates.Count(static candidate => candidate.CandidateStatus == "governed_follow_up_candidate");
        var ready = errors.Count == 0;

        return new RuntimeAgentProblemFollowUpCandidatesSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolveOverallPosture(ready, records.Length, governedCandidateCount),
            FollowUpCandidatesReady = ready,
            RecordedProblemCount = records.Length,
            CandidateCount = candidates.Length,
            GovernedCandidateCount = governedCandidateCount,
            WatchlistCandidateCount = candidates.Length - governedCandidateCount,
            RepeatedPatternCount = candidates.Count(static candidate => candidate.ProblemCount >= 2),
            BlockingCandidateCount = candidates.Count(static candidate => candidate.BlockingCount > 0),
            Candidates = candidates,
            CandidateRules = BuildCandidateRules(),
            OperatorReviewQuestions = BuildOperatorReviewQuestions(),
            Gaps = errors.Select(static error => $"agent_problem_follow_up_candidates_resource:{error}").ToArray(),
            Summary = BuildSummary(ready, records.Length, governedCandidateCount),
            RecommendedNextAction = BuildRecommendedNextAction(ready, records.Length, governedCandidateCount),
            IsValid = ready,
            Errors = errors,
            NonClaims = BuildNonClaims(),
        };
    }

    private List<string> ValidateBaseDocuments()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(Phase36DocumentPath, "Product closure Phase 36 agent problem follow-up candidates document", errors);
        ValidateRuntimeDocument(Phase35DocumentPath, "Product closure Phase 35 agent problem triage ledger document", errors);
        ValidateRuntimeDocument(FollowUpCandidatesGuideDocumentPath, "Agent problem follow-up candidates guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionPlanService.DecisionPlanGuideDocumentPath, "Agent problem follow-up decision plan guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemTriageLedgerService.TriageLedgerGuideDocumentPath, "Agent problem triage ledger guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemIntakeService.ProblemIntakeGuideDocumentPath, "Agent problem intake guide document", errors);
        ValidateRuntimeDocument(RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);
        return errors;
    }

    private static RuntimeAgentProblemFollowUpCandidateSurface[] BuildCandidates(IReadOnlyList<PilotProblemIntakeRecord> records)
    {
        return records
            .GroupBy(static record => record.ProblemKind, StringComparer.Ordinal)
            .Select(static group => RuntimeAgentProblemFollowUpCandidateSurface.FromRecords(group.Key, group.ToArray()))
            .OrderByDescending(static candidate => candidate.CandidateStatus == "governed_follow_up_candidate")
            .ThenByDescending(static candidate => candidate.BlockingCount)
            .ThenByDescending(static candidate => candidate.ProblemCount)
            .ThenBy(static candidate => candidate.ProblemKind, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveOverallPosture(bool ready, int problemCount, int governedCandidateCount)
    {
        if (!ready)
        {
            return "agent_problem_follow_up_candidates_blocked";
        }

        if (problemCount == 0)
        {
            return "agent_problem_follow_up_candidates_empty";
        }

        return governedCandidateCount == 0
            ? "agent_problem_follow_up_candidates_watchlist_only"
            : "agent_problem_follow_up_candidates_operator_review_required";
    }

    private static string[] BuildCandidateRules()
    {
        return
        [
            "Treat candidates as operator review prompts, not card or task truth.",
            "A candidate becomes governed work only after the operator accepts the pattern through the existing planning lane.",
            "Repeated reports and blocking/high-severity reports are promoted to governed_follow_up_candidate posture.",
            "Single non-blocking reports stay on the watchlist until more evidence appears or an operator accepts the pattern.",
            "Do not mutate protected truth roots, .gitignore, target runtime binding, cards, tasks, reviews, commits, or releases from this surface.",
        ];
    }

    private static string[] BuildOperatorReviewQuestions()
    {
        return
        [
            "Is this a Runtime command contract issue, target bootstrap guidance issue, or target-project-only misunderstanding?",
            "Does the candidate repeat across repos, stages, or agents?",
            "Can the pattern be fixed by docs/readback guidance, or does it require a governed Runtime implementation task?",
            "What acceptance evidence would prove the candidate is resolved?",
        ];
    }

    private static string BuildSummary(bool ready, int problemCount, int governedCandidateCount)
    {
        if (!ready)
        {
            return "Agent problem follow-up candidates are blocked until Runtime-owned Phase 36 docs and Phase 35 triage anchors are restored.";
        }

        if (problemCount == 0)
        {
            return "Agent problem follow-up candidates are ready and empty: no recorded problem intake currently needs follow-up review.";
        }

        return $"Agent problem follow-up candidates are ready: {governedCandidateCount} governed follow-up candidate(s) require operator review.";
    }

    private static string BuildRecommendedNextAction(bool ready, int problemCount, int governedCandidateCount)
    {
        if (!ready)
        {
            return "Restore the listed follow-up candidate gaps, then rerun carves pilot follow-up --json.";
        }

        if (problemCount == 0)
        {
            return "Continue external dogfood; when reports appear, run carves pilot triage --json and carves pilot follow-up --json before opening governed work.";
        }

        return governedCandidateCount == 0
            ? "Keep watchlist candidates visible and wait for repeated or blocking evidence before opening governed follow-up."
            : "Run carves pilot follow-up-plan --json, review governed follow-up candidates, then open cards/tasks only for operator-accepted patterns with explicit acceptance evidence.";
    }

    private static string[] BuildNonClaims()
    {
        return
        [
            "This surface is read-only and does not create cards, tasks, acceptance contracts, reviews, writebacks, ignore decisions, commits, or releases.",
            "This surface does not resolve problem intake records or close triage ledger items.",
            "This surface does not authorize an agent to continue past a CARVES blocker.",
            "This surface does not replace the existing planning lane or operator review.",
            "This surface does not claim OS sandboxing, full ACP, full MCP, remote worker orchestration, or automatic triage.",
        ];
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
