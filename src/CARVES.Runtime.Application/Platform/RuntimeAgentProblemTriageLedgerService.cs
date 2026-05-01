using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentProblemTriageLedgerService
{
    public const string TriageLedgerGuideDocumentPath = "docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md";
    private const string Phase35DocumentPath = "docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md";
    private const string Phase36DocumentPath = "docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory;

    public RuntimeAgentProblemTriageLedgerService(
        string repoRoot,
        Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.problemRecordsFactory = problemRecordsFactory;
    }

    public RuntimeAgentProblemTriageLedgerSurface Build()
    {
        var errors = ValidateBaseDocuments();
        var records = problemRecordsFactory()
            .OrderByDescending(static record => record.RecordedAtUtc)
            .ThenByDescending(static record => record.ProblemId, StringComparer.Ordinal)
            .ToArray();
        var blockingCount = records.Count(IsBlocking);
        var repoCount = records
            .Select(static record => record.RepoId)
            .Where(static repoId => !string.IsNullOrWhiteSpace(repoId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var problemKindLedger = BuildProblemKindLedger(records);
        var severityLedger = BuildSeverityLedger(records);
        var stageLedger = BuildStageLedger(records);
        var reviewQueue = records
            .Take(20)
            .Select(RuntimeAgentProblemTriageQueueItemSurface.FromRecord)
            .ToArray();
        var ready = errors.Count == 0;

        return new RuntimeAgentProblemTriageLedgerSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolveOverallPosture(ready, records.Length),
            TriageLedgerReady = ready,
            RecordedProblemCount = records.Length,
            BlockingProblemCount = blockingCount,
            RepoCount = repoCount,
            DistinctProblemKindCount = problemKindLedger.Length,
            ReviewQueueCount = reviewQueue.Length,
            ProblemKindLedger = problemKindLedger,
            SeverityLedger = severityLedger,
            StageLedger = stageLedger,
            ReviewQueue = reviewQueue,
            TriageRules = BuildTriageRules(),
            RecommendedOperatorActions = BuildRecommendedOperatorActions(records.Length),
            Gaps = errors.Select(static error => $"agent_problem_triage_ledger_resource:{error}").ToArray(),
            Summary = BuildSummary(ready, records.Length, blockingCount),
            RecommendedNextAction = BuildRecommendedNextAction(ready, records.Length),
            IsValid = ready,
            Errors = errors,
            NonClaims = BuildNonClaims(),
        };
    }

    public static string ResolveTriageLane(string problemKind)
    {
        return problemKind.Trim() switch
        {
            "command_failed" => "command_contract_or_runtime_surface_review",
            "blocked_posture" => "command_contract_or_runtime_surface_review",
            "protected_truth_root_requested" => "protected_truth_root_policy_review",
            "missing_acceptance_contract" => "acceptance_contract_ingress_review",
            "workspace_scope_ambiguous" => "managed_workspace_or_path_lease_review",
            "next_command_ambiguous" => "pilot_next_or_stage_status_review",
            "runtime_binding_ambiguous" => "dist_binding_or_attach_review",
            "agent_policy_conflict" => "agent_bootstrap_or_constraint_ladder_review",
            "other" => "operator_triage_required",
            _ => "operator_triage_required",
        };
    }

    private List<string> ValidateBaseDocuments()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(Phase36DocumentPath, "Product closure Phase 36 agent problem follow-up candidates document", errors);
        ValidateRuntimeDocument(Phase35DocumentPath, "Product closure Phase 35 agent problem triage ledger document", errors);
        ValidateRuntimeDocument(TriageLedgerGuideDocumentPath, "Agent problem triage ledger guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemIntakeService.ProblemIntakeGuideDocumentPath, "Agent problem intake guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpCandidatesService.FollowUpCandidatesGuideDocumentPath, "Agent problem follow-up candidates guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionPlanService.DecisionPlanGuideDocumentPath, "Agent problem follow-up decision plan guide document", errors);
        ValidateRuntimeDocument(RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);
        return errors;
    }

    private static RuntimeAgentProblemKindLedgerSurface[] BuildProblemKindLedger(IReadOnlyList<PilotProblemIntakeRecord> records)
    {
        return records
            .GroupBy(static record => record.ProblemKind, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new RuntimeAgentProblemKindLedgerSurface
            {
                ProblemKind = group.Key,
                Count = group.Count(),
                BlockingCount = group.Count(IsBlocking),
                RecommendedTriageLane = ResolveTriageLane(group.Key),
                LatestRecordedAtUtc = group.Max(static record => record.RecordedAtUtc),
            })
            .ToArray();
    }

    private static RuntimeAgentProblemSeverityLedgerSurface[] BuildSeverityLedger(IReadOnlyList<PilotProblemIntakeRecord> records)
    {
        return records
            .GroupBy(static record => RuntimeAgentProblemTriageQueueItemSurface.NormalizeSeverity(record.Severity), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static group => new RuntimeAgentProblemSeverityLedgerSurface
            {
                Severity = group.Key,
                Count = group.Count(),
            })
            .ToArray();
    }

    private static RuntimeAgentProblemStageLedgerSurface[] BuildStageLedger(IReadOnlyList<PilotProblemIntakeRecord> records)
    {
        return records
            .GroupBy(static record => RuntimeAgentProblemTriageQueueItemSurface.NormalizeStage(record.CurrentStageId), StringComparer.Ordinal)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => new RuntimeAgentProblemStageLedgerSurface
            {
                CurrentStageId = group.Key,
                Count = group.Count(),
                LatestRecordedAtUtc = group.Max(static record => record.RecordedAtUtc),
            })
            .ToArray();
    }

    private static bool IsBlocking(PilotProblemIntakeRecord record)
    {
        var severity = RuntimeAgentProblemTriageQueueItemSurface.NormalizeSeverity(record.Severity);
        return string.Equals(severity, "blocking", StringComparison.OrdinalIgnoreCase)
               || string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase)
               || string.Equals(severity, "high", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveOverallPosture(bool ready, int problemCount)
    {
        if (!ready)
        {
            return "agent_problem_triage_ledger_blocked";
        }

        return problemCount == 0
            ? "agent_problem_triage_ledger_empty"
            : "agent_problem_triage_review_required";
    }

    private static string[] BuildTriageRules()
    {
        return
        [
            "Treat problem intake as evidence, not approval authority.",
            "Group repeated problems by problem_kind, current_stage_id, and repo_id before opening follow-up work.",
            "Use the recommended triage lane to decide whether the issue is command contract, protected-root, acceptance-contract, workspace, pilot-next, runtime binding, or bootstrap guidance friction.",
            "Do not mutate planning, task, review, .gitignore, or Runtime dist binding from this read-only ledger.",
            "When a record needs implementation work, create or approve a governed card/task through the existing planning lane.",
        ];
    }

    private static string[] BuildRecommendedOperatorActions(int problemCount)
    {
        if (problemCount == 0)
        {
            return
            [
                "Keep pilot problem-intake available in external target bootstrap instructions.",
                "Ask agents to run pilot triage after they submit problem reports during dogfood.",
            ];
        }

        return
        [
            "Review the queue items from newest to oldest.",
            "Cluster repeated problem kinds before opening follow-up work.",
            "Use inspect-problem for the raw record and inspect-evidence for mirrored pilot evidence.",
            "Convert only operator-accepted patterns into governed cards or taskgraph updates.",
        ];
    }

    private static string BuildSummary(bool ready, int problemCount, int blockingCount)
    {
        if (!ready)
        {
            return "Agent problem triage ledger is blocked until its Runtime-owned docs and Phase 34 intake anchors are restored.";
        }

        if (problemCount == 0)
        {
            return "Agent problem triage ledger is ready and empty: no recorded agent problem intake currently needs operator triage.";
        }

        return $"Agent problem triage ledger is ready: {problemCount} recorded problem intake item(s) are visible, including {blockingCount} blocking/high-severity item(s).";
    }

    private static string BuildRecommendedNextAction(bool ready, int problemCount)
    {
        if (!ready)
        {
            return "Restore the listed triage-ledger gaps, then rerun carves pilot triage --json.";
        }

        return problemCount == 0
            ? "Continue external dogfood; when an agent reports a problem, rerun carves pilot triage --json and carves pilot follow-up --json before deciding follow-up work."
            : "Inspect the highest-impact queue items, run carves pilot follow-up --json to cluster repeated friction, then run carves pilot follow-up-plan --json before opening governed follow-up work.";
    }

    private static string[] BuildNonClaims()
    {
        return
        [
            "This surface is read-only and does not create cards, tasks, acceptance contracts, reviews, writebacks, ignore decisions, commits, or releases.",
            "This surface does not automatically close or resolve problem intake records.",
            "This surface does not authorize an agent to continue past a CARVES blocker.",
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
