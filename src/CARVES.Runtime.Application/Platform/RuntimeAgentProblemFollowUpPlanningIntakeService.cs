using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentProblemFollowUpPlanningIntakeService
{
    public const string Phase39DocumentPath = "docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md";
    public const string PlanningIntakeGuideDocumentPath = "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md";

    private static readonly string[] AcceptedDecisions =
    [
        "accept_as_governed_planning_input",
        "accept_as_governed_planning_input_after_operator_override",
    ];

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory;

    public RuntimeAgentProblemFollowUpPlanningIntakeService(
        string repoRoot,
        Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        paths = ControlPlanePaths.FromRepoRoot(this.repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, paths);
        this.problemRecordsFactory = problemRecordsFactory;
    }

    public RuntimeAgentProblemFollowUpPlanningIntakeSurface Build()
    {
        var errors = ValidateBaseDocuments();
        var decisionRecord = new RuntimeAgentProblemFollowUpDecisionRecordService(repoRoot, problemRecordsFactory).Build();
        var decisionPlan = new RuntimeAgentProblemFollowUpDecisionPlanService(repoRoot, problemRecordsFactory).Build();
        errors.AddRange(decisionRecord.Errors.Select(static error => $"runtime-agent-problem-follow-up-decision-record:{error}"));
        errors.AddRange(decisionPlan.Errors.Select(static error => $"runtime-agent-problem-follow-up-decision-plan:{error}"));

        var acceptedRecords = decisionRecord.Records
            .Where(static record => AcceptedDecisions.Contains(record.Decision, StringComparer.Ordinal))
            .ToArray();
        var rejectedRecords = decisionRecord.Records
            .Where(static record => record.Decision.StartsWith("reject_", StringComparison.Ordinal))
            .ToArray();
        var waitingRecords = decisionRecord.Records
            .Where(static record => string.Equals(record.Decision, "wait_for_more_evidence", StringComparison.Ordinal))
            .ToArray();
        var decisionRecordReady = errors.Count == 0 && decisionRecord.DecisionRecordReady;
        var consumedCandidateIds = LoadConsumedCandidateIds();
        var consumedAcceptedCandidateIds = BuildAcceptedCandidateIds(acceptedRecords)
            .Where(consumedCandidateIds.Contains)
            .OrderBy(static candidateId => candidateId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var planningItems = BuildPlanningItems(acceptedRecords, decisionPlan, decisionRecordReady, consumedCandidateIds).ToArray();
        var nonActionable = BuildNonActionableDecisions(rejectedRecords, waitingRecords).ToArray();
        var planningIntakeReady = errors.Count == 0 && decisionRecordReady;

        return new RuntimeAgentProblemFollowUpPlanningIntakeSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(errors.Count, decisionRecord, acceptedRecords.Length, planningItems.Length, planningIntakeReady),
            DecisionPlanId = decisionRecord.DecisionPlanId,
            DecisionRecordPosture = decisionRecord.OverallPosture,
            DecisionRecordReady = decisionRecord.DecisionRecordReady,
            DecisionRecordCommitReady = decisionRecord.DecisionRecordCommitReady,
            RecordAuditReady = decisionRecord.RecordAuditReady,
            PlanningIntakeReady = planningIntakeReady,
            AcceptedDecisionRecordCount = acceptedRecords.Length,
            RejectedDecisionRecordCount = rejectedRecords.Length,
            WaitingDecisionRecordCount = waitingRecords.Length,
            NonActionableDecisionRecordCount = rejectedRecords.Length + waitingRecords.Length,
            AcceptedPlanningItemCount = planningItems.Length,
            ActionablePlanningItemCount = planningItems.Count(static item => item.Actionable),
            ConsumedPlanningItemCount = consumedAcceptedCandidateIds.Length,
            ConsumedPlanningCandidateIds = consumedAcceptedCandidateIds,
            PlanningItems = planningItems,
            NonActionableDecisions = nonActionable,
            PlanningLaneCommands =
            [
                "carves intent draft --persist",
                "carves plan init [candidate-card-id]",
                "carves plan status",
                "carves plan export-card",
            ],
            BoundaryRules =
            [
                "Accepted follow-up decision records are planning inputs only; they are not card truth, task truth, acceptance-contract truth, review truth, or writeback authority.",
                "Planning intake is actionable only when the follow-up decision record surface is ready and its records are tracked and clean.",
                "Accepted follow-up candidate ids already materialized into completed or merged task truth are treated as consumed and do not reopen formal planning.",
                "Rejected and waiting decisions remain visible evidence, but they do not open governed work.",
                "Formal work must still enter through carves intent draft --persist and exactly one active carves plan init card.",
                "Agents must not convert accepted records by editing .ai cards, tasks, graph files, reviews, or acceptance contracts directly.",
            ],
            Gaps = BuildGaps(errors, decisionRecord, planningIntakeReady).ToArray(),
            Summary = BuildSummary(errors.Count, decisionRecord, acceptedRecords.Length, planningItems.Length, planningIntakeReady),
            RecommendedNextAction = BuildRecommendedNextAction(errors.Count, decisionRecord, acceptedRecords.Length, planningItems.Length, planningIntakeReady),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not create, approve, or mutate cards, tasks, acceptance contracts, reviews, writebacks, commits, releases, packs, problem records, or decision records.",
                "This surface does not bypass the active planning slot or the existing formal planning lane.",
                "This surface does not let an external agent self-accept a follow-up pattern.",
                "This surface does not close problem intake, triage, or decision-record evidence.",
            ],
        };
    }

    private IEnumerable<RuntimeAgentProblemFollowUpPlanningIntakeItemSurface> BuildPlanningItems(
        IReadOnlyList<RuntimeAgentProblemFollowUpDecisionRecordEntrySurface> acceptedRecords,
        RuntimeAgentProblemFollowUpDecisionPlanSurface decisionPlan,
        bool actionable,
        IReadOnlySet<string> consumedCandidateIds)
    {
        var decisionItems = decisionPlan.DecisionItems
            .ToDictionary(static item => item.CandidateId, StringComparer.OrdinalIgnoreCase);
        return acceptedRecords
            .SelectMany(static record => record.CandidateIds.Select(candidateId => new { CandidateId = NormalizeCandidateId(candidateId), Record = record }))
            .Where(item => item.CandidateId.Length > 0 && !consumedCandidateIds.Contains(item.CandidateId))
            .GroupBy(static item => item.CandidateId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var records = group.Select(static item => item.Record).ToArray();
                decisionItems.TryGetValue(group.Key, out var decisionItem);
                var suggestedTitle = decisionItem?.SuggestedTitle ?? $"Govern follow-up candidate {group.Key}";
                var suggestedIntent = decisionItem?.SuggestedIntent ?? "Convert the accepted follow-up decision record into a bounded governed planning card.";
                return new RuntimeAgentProblemFollowUpPlanningIntakeItemSurface
                {
                    CandidateId = group.Key,
                    IntakeStatus = actionable
                        ? "ready_for_formal_planning"
                        : "blocked_by_decision_record_readback",
                    Actionable = actionable,
                    SuggestedTitle = suggestedTitle,
                    SuggestedIntent = suggestedIntent,
                    SuggestedAcceptanceEvidence = BuildSuggestedAcceptanceEvidence(records),
                    SuggestedReadbackCommand = BuildSuggestedReadback(records),
                    SuggestedPlanInitCommand = $"carves plan init {group.Key}",
                    Decision = records.Select(static record => record.Decision).Distinct(StringComparer.Ordinal).OrderBy(static decision => decision, StringComparer.Ordinal).FirstOrDefault() ?? string.Empty,
                    DecisionRecordIds = records.Select(static record => record.DecisionRecordId).Distinct(StringComparer.Ordinal).OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
                    DecisionRecordPaths = records.Select(static record => record.RecordPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
                    RelatedProblemIds = records.SelectMany(static record => record.RelatedProblemIds).Distinct(StringComparer.Ordinal).OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
                    RelatedEvidenceIds = records.SelectMany(static record => record.RelatedEvidenceIds).Distinct(StringComparer.Ordinal).OrderBy(static id => id, StringComparer.Ordinal).ToArray(),
                    OperatorReasons = records.Select(static record => record.Reason).Where(static reason => !string.IsNullOrWhiteSpace(reason)).Distinct(StringComparer.Ordinal).OrderBy(static reason => reason, StringComparer.Ordinal).ToArray(),
                    AcceptanceEvidence = records.Select(static record => record.AcceptanceEvidence).Where(static evidence => !string.IsNullOrWhiteSpace(evidence)).Distinct(StringComparer.Ordinal).OrderBy(static evidence => evidence, StringComparer.Ordinal).ToArray(),
                    ReadbackCommands = records.Select(static record => record.ReadbackCommand).Where(static command => !string.IsNullOrWhiteSpace(command)).Distinct(StringComparer.Ordinal).OrderBy(static command => command, StringComparer.Ordinal).ToArray(),
                    PlanningRequirements =
                    [
                        "Run carves intent draft --persist before durable planning work.",
                        "Run carves plan init with one candidate-card id; do not create a second active planning card.",
                        "Carry the decision record id, related problem ids, related evidence ids, acceptance evidence, and readback command into the planning card editable fields.",
                        "Do not create task truth until the planning card and acceptance contract are explicit.",
                    ],
                };
            });
    }

    private static IEnumerable<RuntimeAgentProblemFollowUpPlanningIntakeDecisionSurface> BuildNonActionableDecisions(
        IReadOnlyList<RuntimeAgentProblemFollowUpDecisionRecordEntrySurface> rejectedRecords,
        IReadOnlyList<RuntimeAgentProblemFollowUpDecisionRecordEntrySurface> waitingRecords)
    {
        return rejectedRecords
            .Select(static record => BuildNonActionableDecision(record, "rejected_not_planning_input"))
            .Concat(waitingRecords.Select(static record => BuildNonActionableDecision(record, "waiting_for_more_evidence")));
    }

    private static RuntimeAgentProblemFollowUpPlanningIntakeDecisionSurface BuildNonActionableDecision(
        RuntimeAgentProblemFollowUpDecisionRecordEntrySurface record,
        string intakeStatus)
    {
        return new RuntimeAgentProblemFollowUpPlanningIntakeDecisionSurface
        {
            DecisionRecordId = record.DecisionRecordId,
            RecordPath = record.RecordPath,
            Decision = record.Decision,
            CandidateIds = record.CandidateIds,
            Reason = record.Reason,
            IntakeStatus = intakeStatus,
        };
    }

    private static IEnumerable<string> BuildAcceptedCandidateIds(
        IReadOnlyList<RuntimeAgentProblemFollowUpDecisionRecordEntrySurface> acceptedRecords)
    {
        return acceptedRecords
            .SelectMany(static record => record.CandidateIds.Select(NormalizeCandidateId))
            .Where(static candidateId => candidateId.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> BuildGaps(
        IReadOnlyList<string> errors,
        RuntimeAgentProblemFollowUpDecisionRecordSurface decisionRecord,
        bool planningIntakeReady)
    {
        if (planningIntakeReady)
        {
            yield break;
        }

        foreach (var error in errors)
        {
            yield return $"agent_problem_follow_up_planning_intake_resource:{error}";
        }

        foreach (var gap in decisionRecord.Gaps)
        {
            yield return $"agent_problem_follow_up_decision_record:{gap}";
        }

        if (!decisionRecord.DecisionRecordReady)
        {
            yield return "agent_problem_follow_up_decision_record_not_ready";
        }

        if (!decisionRecord.DecisionRecordCommitReady)
        {
            yield return "agent_problem_follow_up_decision_record_commit_not_ready";
        }
    }

    private static string ResolvePosture(
        int errorCount,
        RuntimeAgentProblemFollowUpDecisionRecordSurface decisionRecord,
        int acceptedRecordCount,
        int openPlanningItemCount,
        bool planningIntakeReady)
    {
        if (errorCount > 0)
        {
            return "agent_problem_follow_up_planning_intake_blocked_by_surface_gaps";
        }

        if (!decisionRecord.DecisionRecordReady)
        {
            return "agent_problem_follow_up_planning_intake_waiting_for_decision_record";
        }

        if (acceptedRecordCount == 0)
        {
            return "agent_problem_follow_up_planning_intake_no_accepted_records";
        }

        if (openPlanningItemCount == 0)
        {
            return "agent_problem_follow_up_planning_intake_no_open_accepted_records";
        }

        return planningIntakeReady
            ? "agent_problem_follow_up_planning_intake_ready"
            : "agent_problem_follow_up_planning_intake_blocked";
    }

    private static string BuildSummary(
        int errorCount,
        RuntimeAgentProblemFollowUpDecisionRecordSurface decisionRecord,
        int acceptedRecordCount,
        int planningItemCount,
        bool planningIntakeReady)
    {
        if (errorCount > 0)
        {
            return "Agent problem follow-up planning intake is blocked until required Runtime docs and decision-record anchors are restored.";
        }

        if (!decisionRecord.DecisionRecordReady)
        {
            return "Agent problem follow-up planning intake is waiting for durable, clean follow-up decision records.";
        }

        if (acceptedRecordCount == 0)
        {
            return "Agent problem follow-up planning intake is ready and empty: no accepted follow-up decisions currently need formal planning.";
        }

        if (planningItemCount == 0)
        {
            return "Agent problem follow-up planning intake is ready and empty: accepted follow-up decisions have already been consumed by completed or merged task truth.";
        }

        return planningIntakeReady
            ? $"Agent problem follow-up planning intake is ready with {planningItemCount} accepted candidate(s) that must enter through intent draft and plan init."
            : "Agent problem follow-up planning intake is blocked until decision-record readback is clean.";
    }

    private static string BuildRecommendedNextAction(
        int errorCount,
        RuntimeAgentProblemFollowUpDecisionRecordSurface decisionRecord,
        int acceptedRecordCount,
        int planningItemCount,
        bool planningIntakeReady)
    {
        if (errorCount > 0)
        {
            return "Restore the listed planning-intake docs and decision-record anchors, then rerun carves pilot follow-up-intake --json.";
        }

        if (!decisionRecord.DecisionRecordReady)
        {
            return "Run carves pilot follow-up-record --json and resolve missing, invalid, conflicting, or uncommitted decision records before opening governed planning.";
        }

        if (acceptedRecordCount == 0 || planningItemCount == 0)
        {
            return "agent_problem_follow_up_planning_intake_readback_clean";
        }

        return planningIntakeReady
            ? "For each accepted planning item, run carves intent draft --persist, then carves plan init [candidate-card-id], carrying the decision record, acceptance evidence, and readback command into the planning card."
            : "Rerun carves pilot follow-up-intake --json after decision-record readback is clean.";
    }

    private IReadOnlySet<string> LoadConsumedCandidateIds()
    {
        if (!Directory.Exists(paths.TaskNodesRoot))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var candidateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var nodePath in Directory.EnumerateFiles(paths.TaskNodesRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            var candidateId = TryReadConsumedCandidateId(nodePath);
            if (!string.IsNullOrWhiteSpace(candidateId))
            {
                candidateIds.Add(candidateId);
            }
        }

        return candidateIds;
    }

    private static string? TryReadConsumedCandidateId(string nodePath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(nodePath));
            var root = document.RootElement;
            if (!root.TryGetProperty("status", out var status)
                || status.ValueKind != JsonValueKind.String
                || !IsCandidateConsumingTaskStatus(status.GetString()))
            {
                return null;
            }

            if (!root.TryGetProperty("metadata", out var metadata)
                || metadata.ValueKind != JsonValueKind.Object
                || !metadata.TryGetProperty(PlanningLineageMetadata.SourceCandidateCardIdKey, out var candidateId)
                || candidateId.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var normalized = NormalizeCandidateId(candidateId.GetString() ?? string.Empty);
            return normalized.Length == 0 ? null : normalized;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsCandidateConsumingTaskStatus(string? status)
    {
        return string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "merged", StringComparison.OrdinalIgnoreCase);
    }

    private List<string> ValidateBaseDocuments()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(Phase39DocumentPath, "Product closure Phase 39 agent problem follow-up planning intake document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionRecordService.Phase38DocumentPath, "Product closure Phase 38 agent problem follow-up decision record document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionPlanService.Phase37DocumentPath, "Product closure Phase 37 agent problem follow-up decision plan document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", "Product closure Phase 36 agent problem follow-up candidates document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md", "Product closure Phase 35 agent problem triage ledger document", errors);
        ValidateRuntimeDocument(PlanningIntakeGuideDocumentPath, "Agent problem follow-up planning intake guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionRecordService.DecisionRecordGuideDocumentPath, "Agent problem follow-up decision record guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionPlanService.DecisionPlanGuideDocumentPath, "Agent problem follow-up decision plan guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpCandidatesService.FollowUpCandidatesGuideDocumentPath, "Agent problem follow-up candidates guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemTriageLedgerService.TriageLedgerGuideDocumentPath, "Agent problem triage ledger guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemIntakeService.ProblemIntakeGuideDocumentPath, "Agent problem intake guide document", errors);
        ValidateRuntimeDocument(RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);
        return errors;
    }

    private static string BuildSuggestedAcceptanceEvidence(IReadOnlyList<RuntimeAgentProblemFollowUpDecisionRecordEntrySurface> records)
    {
        var evidence = records
            .Select(static record => record.AcceptanceEvidence)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        return evidence.Length == 0
            ? "Use the accepted decision record acceptance evidence."
            : string.Join(" | ", evidence);
    }

    private static string BuildSuggestedReadback(IReadOnlyList<RuntimeAgentProblemFollowUpDecisionRecordEntrySurface> records)
    {
        var commands = records
            .Select(static record => record.ReadbackCommand)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        return commands.Length == 0
            ? "Use the accepted decision record readback command."
            : string.Join(" | ", commands);
    }

    private static string NormalizeCandidateId(string candidateId)
    {
        return candidateId.Trim();
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
