using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentProblemFollowUpPlanningGateService
{
    public const string PlanningGateGuideDocumentPath = "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeAgentProblemFollowUpPlanningIntakeSurface> planningIntakeFactory;
    private readonly Func<RuntimeFormalPlanningPostureSurface> formalPlanningFactory;

    public RuntimeAgentProblemFollowUpPlanningGateService(
        string repoRoot,
        Func<RuntimeAgentProblemFollowUpPlanningIntakeSurface> planningIntakeFactory,
        Func<RuntimeFormalPlanningPostureSurface> formalPlanningFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.planningIntakeFactory = planningIntakeFactory;
        this.formalPlanningFactory = formalPlanningFactory;
    }

    public RuntimeAgentProblemFollowUpPlanningGateSurface Build()
    {
        var errors = ValidateBaseDocuments();
        var planningIntake = planningIntakeFactory();
        var formalPlanning = formalPlanningFactory();
        errors.AddRange(planningIntake.Errors.Select(static error => $"runtime-agent-problem-follow-up-planning-intake:{error}"));
        errors.AddRange(formalPlanning.Errors.Select(static error => $"runtime-formal-planning-posture:{error}"));

        var planInitCandidateIds = LoadPlanInitCandidateIds();
        var planningGateItems = BuildGateItems(planningIntake, formalPlanning, errors.Count == 0, planInitCandidateIds).ToArray();
        var posture = ResolvePosture(errors.Count, planningIntake, formalPlanning, planningGateItems);
        var nextCommand = ResolveNextGovernedCommand(posture, planningGateItems, formalPlanning);
        var readyForPlanInitCount = planningGateItems.Count(static item => string.Equals(item.PlanningGateStatus, "ready_for_plan_init", StringComparison.Ordinal));
        var blockedAcceptedPlanningItemCount = planningGateItems.Count(static item => !item.Actionable);

        return new RuntimeAgentProblemFollowUpPlanningGateSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = posture,
            PlanningIntakePosture = planningIntake.OverallPosture,
            PlanningIntakeReady = planningIntake.PlanningIntakeReady,
            PlanningGateReady = errors.Count == 0 && planningIntake.PlanningIntakeReady,
            AcceptedPlanningItemCount = planningIntake.AcceptedPlanningItemCount,
            ReadyForPlanInitCount = readyForPlanInitCount,
            BlockedAcceptedPlanningItemCount = blockedAcceptedPlanningItemCount,
            FormalPlanningPosture = formalPlanning.OverallPosture,
            FormalPlanningState = formalPlanning.FormalPlanningState,
            FormalPlanningEntryCommand = formalPlanning.FormalPlanningEntryCommand,
            FormalPlanningEntryRecommendedNextAction = formalPlanning.FormalPlanningEntryRecommendedNextAction,
            ActivePlanningSlotState = formalPlanning.ActivePlanningSlotState,
            ActivePlanningSlotCanInitialize = formalPlanning.ActivePlanningSlotCanInitialize,
            ActivePlanningSlotConflictReason = formalPlanning.ActivePlanningSlotConflictReason,
            ActivePlanningSlotRemediationAction = formalPlanning.ActivePlanningSlotRemediationAction,
            PlanningSlotId = formalPlanning.PlanningSlotId,
            PlanHandle = formalPlanning.PlanHandle,
            PlanningCardId = formalPlanning.PlanningCardId,
            NextGovernedCommand = nextCommand,
            PlanningGateItems = planningGateItems,
            BoundaryRules =
            [
                "The planning gate is read-only; it projects whether accepted follow-up planning inputs may enter the single active formal planning slot.",
                "Accepted follow-up decision records remain planning inputs only and do not create card, task, acceptance-contract, review, or writeback truth.",
                "When no intent draft exists, agents must run carves intent draft --persist before plan init.",
                "When the active planning slot is occupied, agents must continue or close the existing slot instead of creating a competing planning card.",
                "Agents must not bypass this gate by editing .ai cards, tasks, graph files, reviews, or acceptance contracts directly.",
            ],
            Gaps = BuildGaps(errors, planningIntake, formalPlanning, planningGateItems).ToArray(),
            Summary = BuildSummary(errors.Count, planningIntake, formalPlanning, readyForPlanInitCount),
            RecommendedNextAction = BuildRecommendedNextAction(posture, nextCommand, formalPlanning),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not create, approve, or mutate cards, tasks, taskgraphs, acceptance contracts, reviews, writebacks, commits, packs, problem records, or decision records.",
                "This surface does not replace formal planning posture, planning-card invariant checks, planning-card fill guidance, or plan status.",
                "This surface does not give an external agent authority to self-accept a follow-up pattern or open multiple active planning cards.",
                "This surface does not close problem intake, triage, follow-up candidate, follow-up decision plan, decision record, or planning intake evidence.",
            ],
        };
    }

    private static IEnumerable<RuntimeAgentProblemFollowUpPlanningGateItemSurface> BuildGateItems(
        RuntimeAgentProblemFollowUpPlanningIntakeSurface planningIntake,
        RuntimeFormalPlanningPostureSurface formalPlanning,
        bool baseResourcesValid,
        IReadOnlySet<string> planInitCandidateIds)
    {
        foreach (var item in planningIntake.PlanningItems)
        {
            var gateStatus = ResolveItemGateStatus(item, planningIntake, formalPlanning, baseResourcesValid, planInitCandidateIds);
            yield return new RuntimeAgentProblemFollowUpPlanningGateItemSurface
            {
                CandidateId = item.CandidateId,
                IntakeStatus = item.IntakeStatus,
                PlanningGateStatus = gateStatus,
                Actionable = string.Equals(gateStatus, "ready_for_plan_init", StringComparison.Ordinal),
                NextGovernedCommand = ResolveItemNextCommand(gateStatus, item),
                SuggestedPlanInitCommand = item.SuggestedPlanInitCommand,
                BlockingReason = ResolveItemBlockingReason(gateStatus, planningIntake, formalPlanning),
                RemediationAction = ResolveItemRemediationAction(gateStatus, planningIntake, formalPlanning),
                SuggestedTitle = item.SuggestedTitle,
                SuggestedIntent = item.SuggestedIntent,
                DecisionRecordIds = item.DecisionRecordIds,
                DecisionRecordPaths = item.DecisionRecordPaths,
                RelatedProblemIds = item.RelatedProblemIds,
                RelatedEvidenceIds = item.RelatedEvidenceIds,
                AcceptanceEvidence = item.AcceptanceEvidence,
                ReadbackCommands = item.ReadbackCommands,
            };
        }
    }

    private static string ResolveItemGateStatus(
        RuntimeAgentProblemFollowUpPlanningIntakeItemSurface item,
        RuntimeAgentProblemFollowUpPlanningIntakeSurface planningIntake,
        RuntimeFormalPlanningPostureSurface formalPlanning,
        bool baseResourcesValid,
        IReadOnlySet<string> planInitCandidateIds)
    {
        if (!baseResourcesValid || !planningIntake.PlanningIntakeReady || !item.Actionable)
        {
            return "blocked_by_planning_intake";
        }

        if (formalPlanning.ActivePlanningSlotCanInitialize)
        {
            return planInitCandidateIds.Contains(item.CandidateId)
                ? "ready_for_plan_init"
                : "waiting_for_intent_draft_candidate_projection";
        }

        return string.Equals(formalPlanning.ActivePlanningSlotState, "no_intent_draft", StringComparison.Ordinal)
            ? "waiting_for_intent_draft"
            : "blocked_by_active_planning_slot";
    }

    private static string ResolveItemNextCommand(string gateStatus, RuntimeAgentProblemFollowUpPlanningIntakeItemSurface item)
    {
        return gateStatus switch
        {
            "ready_for_plan_init" => item.SuggestedPlanInitCommand,
            "waiting_for_intent_draft_candidate_projection" => "carves intent draft --persist",
            "waiting_for_intent_draft" => "carves intent draft --persist",
            "blocked_by_active_planning_slot" => "carves plan status",
            _ => "carves pilot follow-up-intake --json",
        };
    }

    private static string ResolveItemBlockingReason(
        string gateStatus,
        RuntimeAgentProblemFollowUpPlanningIntakeSurface planningIntake,
        RuntimeFormalPlanningPostureSurface formalPlanning)
    {
        return gateStatus switch
        {
            "ready_for_plan_init" => string.Empty,
            "waiting_for_intent_draft_candidate_projection" => "Accepted follow-up candidate is not present as a ready or grounded candidate in the active intent draft.",
            "waiting_for_intent_draft" => "No intent draft exists, so Runtime has no formal planning slot to initialize.",
            "blocked_by_active_planning_slot" => string.IsNullOrWhiteSpace(formalPlanning.ActivePlanningSlotConflictReason)
                ? $"Active planning slot state is {formalPlanning.ActivePlanningSlotState}."
                : formalPlanning.ActivePlanningSlotConflictReason,
            _ => planningIntake.PlanningIntakeReady
                ? "Planning intake item is not actionable."
                : "Planning intake is not ready.",
        };
    }

    private static string ResolveItemRemediationAction(
        string gateStatus,
        RuntimeAgentProblemFollowUpPlanningIntakeSurface planningIntake,
        RuntimeFormalPlanningPostureSurface formalPlanning)
    {
        return gateStatus switch
        {
            "ready_for_plan_init" => "Run the suggested plan init command for this candidate and carry decision evidence into the planning card editable fields.",
            "waiting_for_intent_draft_candidate_projection" => "Run carves intent draft --persist so accepted follow-up planning inputs are projected into guided-planning candidates, then resolve guided-planning decisions and rerun carves pilot follow-up-gate --json.",
            "waiting_for_intent_draft" => "Run carves intent draft --persist, then rerun carves pilot follow-up-gate --json.",
            "blocked_by_active_planning_slot" => string.IsNullOrWhiteSpace(formalPlanning.ActivePlanningSlotRemediationAction)
                ? "Run carves plan status and continue or close the existing planning slot before opening another candidate."
                : formalPlanning.ActivePlanningSlotRemediationAction,
            _ => planningIntake.RecommendedNextAction,
        };
    }

    private static string ResolvePosture(
        int errorCount,
        RuntimeAgentProblemFollowUpPlanningIntakeSurface planningIntake,
        RuntimeFormalPlanningPostureSurface formalPlanning,
        IReadOnlyList<RuntimeAgentProblemFollowUpPlanningGateItemSurface> planningGateItems)
    {
        if (errorCount > 0)
        {
            return "agent_problem_follow_up_planning_gate_blocked_by_surface_gaps";
        }

        if (!planningIntake.PlanningIntakeReady)
        {
            return "agent_problem_follow_up_planning_gate_blocked_by_planning_intake";
        }

        if (planningIntake.AcceptedPlanningItemCount == 0)
        {
            return "agent_problem_follow_up_planning_gate_no_accepted_records";
        }

        if (formalPlanning.ActivePlanningSlotCanInitialize)
        {
            return planningGateItems.Any(static item => string.Equals(item.PlanningGateStatus, "ready_for_plan_init", StringComparison.Ordinal))
                ? "agent_problem_follow_up_planning_gate_ready_to_plan_init"
                : "agent_problem_follow_up_planning_gate_waiting_for_intent_draft_candidate_projection";
        }

        return string.Equals(formalPlanning.ActivePlanningSlotState, "no_intent_draft", StringComparison.Ordinal)
            ? "agent_problem_follow_up_planning_gate_waiting_for_intent_draft"
            : "agent_problem_follow_up_planning_gate_blocked_by_active_planning_slot";
    }

    private static string ResolveNextGovernedCommand(
        string posture,
        IReadOnlyList<RuntimeAgentProblemFollowUpPlanningGateItemSurface> items,
        RuntimeFormalPlanningPostureSurface formalPlanning)
    {
        if (string.Equals(posture, "agent_problem_follow_up_planning_gate_ready_to_plan_init", StringComparison.Ordinal))
        {
            return items.FirstOrDefault(static item => item.Actionable)?.NextGovernedCommand ?? formalPlanning.FormalPlanningEntryCommand;
        }

        return posture switch
        {
            "agent_problem_follow_up_planning_gate_waiting_for_intent_draft" => "carves intent draft --persist",
            "agent_problem_follow_up_planning_gate_waiting_for_intent_draft_candidate_projection" => "carves intent draft --persist",
            "agent_problem_follow_up_planning_gate_blocked_by_active_planning_slot" => "carves plan status",
            "agent_problem_follow_up_planning_gate_blocked_by_planning_intake" => "carves pilot follow-up-intake --json",
            _ => "carves pilot follow-up-gate --json",
        };
    }

    private static IEnumerable<string> BuildGaps(
        IReadOnlyList<string> errors,
        RuntimeAgentProblemFollowUpPlanningIntakeSurface planningIntake,
        RuntimeFormalPlanningPostureSurface formalPlanning,
        IReadOnlyList<RuntimeAgentProblemFollowUpPlanningGateItemSurface> planningGateItems)
    {
        foreach (var error in errors)
        {
            yield return $"agent_problem_follow_up_planning_gate_resource:{error}";
        }

        if (!planningIntake.PlanningIntakeReady)
        {
            yield return "agent_problem_follow_up_planning_intake_not_ready";
        }

        foreach (var gap in planningIntake.Gaps)
        {
            yield return $"runtime-agent-problem-follow-up-planning-intake:{gap}";
        }

        foreach (var prerequisite in formalPlanning.MissingPrerequisites)
        {
            yield return $"runtime-formal-planning-posture:{prerequisite}";
        }

        foreach (var item in planningGateItems.Where(static item =>
                     string.Equals(item.PlanningGateStatus, "waiting_for_intent_draft_candidate_projection", StringComparison.Ordinal)))
        {
            yield return $"follow_up_candidate_not_in_intent_draft:{item.CandidateId}";
        }

        if (planningIntake.AcceptedPlanningItemCount > 0
            && !formalPlanning.ActivePlanningSlotCanInitialize
            && !string.Equals(formalPlanning.ActivePlanningSlotState, "no_intent_draft", StringComparison.Ordinal))
        {
            yield return $"active_planning_slot_not_initializable:{formalPlanning.ActivePlanningSlotState}";
        }
    }

    private static string BuildSummary(
        int errorCount,
        RuntimeAgentProblemFollowUpPlanningIntakeSurface planningIntake,
        RuntimeFormalPlanningPostureSurface formalPlanning,
        int readyForPlanInitCount)
    {
        if (errorCount > 0)
        {
            return "Agent problem follow-up planning gate is blocked until required Runtime docs and dependency surfaces are restored.";
        }

        if (!planningIntake.PlanningIntakeReady)
        {
            return "Agent problem follow-up planning gate is waiting for a clean follow-up planning intake readback.";
        }

        if (planningIntake.AcceptedPlanningItemCount == 0)
        {
            return "Agent problem follow-up planning gate is ready and empty: no accepted follow-up planning inputs currently require plan init.";
        }

        if (readyForPlanInitCount > 0)
        {
            return $"Agent problem follow-up planning gate is ready: {readyForPlanInitCount} accepted candidate(s) can enter the single active planning slot through plan init.";
        }

        if (formalPlanning.ActivePlanningSlotCanInitialize)
        {
            return "Agent problem follow-up planning gate found accepted planning inputs, but they must be projected into the active intent draft before plan init.";
        }

        if (string.Equals(formalPlanning.ActivePlanningSlotState, "no_intent_draft", StringComparison.Ordinal))
        {
            return "Agent problem follow-up planning gate found accepted planning inputs, but intent draft must run before plan init.";
        }

        return "Agent problem follow-up planning gate found accepted planning inputs, but the active planning slot is occupied or otherwise blocked.";
    }

    private static string BuildRecommendedNextAction(
        string posture,
        string nextCommand,
        RuntimeFormalPlanningPostureSurface formalPlanning)
    {
        return posture switch
        {
            "agent_problem_follow_up_planning_gate_ready_to_plan_init" => $"Run `{nextCommand}` and carry the accepted decision record ids, related problem ids, acceptance evidence, and readback commands into the planning card.",
            "agent_problem_follow_up_planning_gate_waiting_for_intent_draft_candidate_projection" => "Run `carves intent draft --persist` so accepted follow-up planning inputs are projected as guided-planning candidates, resolve guided-planning decisions, then rerun `carves pilot follow-up-gate --json` before plan init.",
            "agent_problem_follow_up_planning_gate_waiting_for_intent_draft" => "Run `carves intent draft --persist`, then rerun `carves pilot follow-up-gate --json` before plan init.",
            "agent_problem_follow_up_planning_gate_blocked_by_active_planning_slot" => string.IsNullOrWhiteSpace(formalPlanning.ActivePlanningSlotRemediationAction)
                ? "Run `carves plan status` and continue or close the existing active planning slot before opening another candidate."
                : formalPlanning.ActivePlanningSlotRemediationAction,
            "agent_problem_follow_up_planning_gate_blocked_by_planning_intake" => "Run `carves pilot follow-up-intake --json` and resolve its gaps before trying to initialize planning.",
            "agent_problem_follow_up_planning_gate_no_accepted_records" => "agent_problem_follow_up_planning_gate_readback_clean",
            _ => "Restore the listed planning-gate resources, then rerun `carves pilot follow-up-gate --json`.",
        };
    }

    private List<string> ValidateBaseDocuments()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpPlanningIntakeService.Phase39DocumentPath, "Product closure Phase 39 agent problem follow-up planning intake document", errors);
        ValidateRuntimeDocument(PlanningGateGuideDocumentPath, "Agent problem follow-up planning gate guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpPlanningIntakeService.PlanningIntakeGuideDocumentPath, "Agent problem follow-up planning intake guide document", errors);
        ValidateRuntimeDocument(RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);
        return errors;
    }

    private IReadOnlySet<string> LoadPlanInitCandidateIds()
    {
        var draftPath = Path.Combine(repoRoot, ".ai", "runtime", "intent_draft.json");
        if (!File.Exists(draftPath))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(draftPath));
            if (!document.RootElement.TryGetProperty("candidate_cards", out var candidates)
                || candidates.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var ids = candidates.EnumerateArray()
                .Where(static candidate =>
                    candidate.TryGetProperty("candidate_card_id", out var candidateId)
                    && !string.IsNullOrWhiteSpace(candidateId.GetString())
                    && candidate.TryGetProperty("planning_posture", out var posture)
                    && (string.Equals(posture.GetString(), "ready_to_plan", StringComparison.Ordinal)
                        || string.Equals(posture.GetString(), "grounded", StringComparison.Ordinal)))
                .Select(static candidate => candidate.GetProperty("candidate_card_id").GetString()!.Trim())
                .ToHashSet(StringComparer.Ordinal);

            return ids;
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
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
