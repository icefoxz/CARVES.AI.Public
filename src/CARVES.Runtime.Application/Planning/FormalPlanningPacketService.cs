using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Planning;

public sealed class FormalPlanningPacketService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly IntentDiscoveryService intentDiscoveryService;
    private readonly PlanningDraftService planningDraftService;
    private readonly TaskGraphService taskGraphService;

    public FormalPlanningPacketService(
        IntentDiscoveryService intentDiscoveryService,
        PlanningDraftService planningDraftService,
        TaskGraphService taskGraphService)
    {
        this.intentDiscoveryService = intentDiscoveryService;
        this.planningDraftService = planningDraftService;
        this.taskGraphService = taskGraphService;
    }

    public FormalPlanningPacket? TryBuildCurrentPacket()
    {
        var status = intentDiscoveryService.GetStatus();
        return status.Draft?.ActivePlanningCard is null
            ? null
            : BuildPacket(status);
    }

    public FormalPlanningPacket BuildCurrentPacket()
    {
        var packet = TryBuildCurrentPacket();
        if (packet is null)
        {
            throw new InvalidOperationException("No active planning card exists. Run `plan init` first.");
        }

        return packet;
    }

    public FormalPlanningPacketExportResult ExportCurrentPacket(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Usage: plan export-packet <json-path>");
        }

        var packet = BuildCurrentPacket();
        var exportPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
        File.WriteAllText(exportPath, JsonSerializer.Serialize(packet, JsonOptions));
        return new FormalPlanningPacketExportResult(exportPath, packet.PlanHandle, packet.PlanningCardId);
    }

    public static string BuildPlanHandle(string planningSlotId, string planningCardId)
    {
        return $"plan-{planningSlotId}-{planningCardId}";
    }

    public static string BuildPlanHandle(ActivePlanningCard activePlanningCard)
    {
        return BuildPlanHandle(activePlanningCard.PlanningSlotId, activePlanningCard.PlanningCardId);
    }

    public static string BuildPlanHandle(PlanningLineage lineage)
    {
        return BuildPlanHandle(lineage.PlanningSlotId, lineage.ActivePlanningCardId);
    }

    private FormalPlanningPacket BuildPacket(IntentDiscoveryStatus status)
    {
        var draft = status.Draft ?? throw new InvalidOperationException("No active planning draft exists.");
        var activePlanningCard = draft.ActivePlanningCard ?? throw new InvalidOperationException("No active planning card exists.");
        var linkedCardDrafts = planningDraftService.ListCardDrafts()
            .Where(item => MatchesPlanningLineage(item.PlanningLineage, activePlanningCard))
            .ToArray();
        var linkedTaskGraphDrafts = planningDraftService.ListTaskGraphDrafts()
            .Where(item => MatchesPlanningLineage(item.PlanningLineage, activePlanningCard))
            .ToArray();
        var linkedTasks = taskGraphService.Load().ListTasks()
            .Where(task => MatchesPlanningLineage(PlanningLineageMetadata.TryRead(task.Metadata), activePlanningCard))
            .ToArray();
        var formalPlanningState = ResolveFormalPlanningState(status, linkedCardDrafts, linkedTaskGraphDrafts, linkedTasks);
        var acceptanceBinding = SelectAcceptanceBinding(activePlanningCard, linkedCardDrafts, linkedTaskGraphDrafts, linkedTasks);
        var replanRequired = linkedTasks.Any(static task =>
            string.Equals(task.Metadata.GetValueOrDefault("planner_replan_required"), "true", StringComparison.OrdinalIgnoreCase));
        var blockers = BuildBlockers(formalPlanningState, linkedCardDrafts, linkedTaskGraphDrafts, linkedTasks, replanRequired);
        return new FormalPlanningPacket
        {
            PlanHandle = BuildPlanHandle(activePlanningCard),
            PlanningSlotId = activePlanningCard.PlanningSlotId,
            PlanningCardId = activePlanningCard.PlanningCardId,
            SourceIntentDraftId = activePlanningCard.SourceIntentDraftId,
            SourceCandidateCardId = activePlanningCard.SourceCandidateCardId,
            FormalPlanningState = formalPlanningState,
            Briefing = BuildBriefing(formalPlanningState, status, activePlanningCard, linkedCardDrafts, linkedTaskGraphDrafts, linkedTasks, blockers, replanRequired),
            AcceptanceContractSummary = BuildAcceptanceSummary(activePlanningCard, acceptanceBinding),
            Constraints = BuildConstraints(activePlanningCard, linkedCardDrafts),
            NonGoals = BuildNonGoals(activePlanningCard, linkedCardDrafts),
            DecompositionCandidates = BuildDecompositionCandidates(activePlanningCard, linkedTaskGraphDrafts),
            Blockers = blockers,
            EvidenceExpectations = BuildEvidenceExpectations(activePlanningCard, acceptanceBinding),
            AllowedScopeSummary = BuildAllowedScopeSummary(linkedCardDrafts, linkedTaskGraphDrafts, linkedTasks),
            ReplanRules = BuildReplanRules(),
            LinkedTruth = new FormalPlanningLinkedTruth
            {
                CardDraftIds = linkedCardDrafts.Select(item => item.CardId).Distinct(StringComparer.Ordinal).ToArray(),
                TaskGraphDraftIds = linkedTaskGraphDrafts.Select(item => item.DraftId).Distinct(StringComparer.Ordinal).ToArray(),
                TaskIds = linkedTasks.Select(item => item.TaskId).Distinct(StringComparer.Ordinal).ToArray(),
            },
        };
    }

    private static FormalPlanningState ResolveFormalPlanningState(
        IntentDiscoveryStatus status,
        IReadOnlyList<CardDraftRecord> linkedCardDrafts,
        IReadOnlyList<TaskGraphDraftRecord> linkedTaskGraphDrafts,
        IReadOnlyList<TaskNode> linkedTasks)
    {
        if (status.Draft?.ActivePlanningCard is null)
        {
            return status.Draft?.PlanningPosture is GuidedPlanningPosture.ReadyToPlan or GuidedPlanningPosture.Grounded
                ? FormalPlanningState.PlanInitRequired
                : FormalPlanningState.Discuss;
        }

        if (linkedTasks.Count > 0)
        {
            if (linkedTasks.Any(task => task.Status is DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait))
            {
                return FormalPlanningState.ReviewBound;
            }

            if (linkedTasks.All(task => task.Status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Discarded or DomainTaskStatus.Superseded))
            {
                return FormalPlanningState.Closed;
            }

            return FormalPlanningState.ExecutionBound;
        }

        if (linkedTaskGraphDrafts.Count > 0 || linkedCardDrafts.Count > 0)
        {
            return FormalPlanningState.PlanBound;
        }

        return FormalPlanningState.Planning;
    }

    private static FormalPlanningBriefing BuildBriefing(
        FormalPlanningState state,
        IntentDiscoveryStatus status,
        ActivePlanningCard activePlanningCard,
        IReadOnlyList<CardDraftRecord> linkedCardDrafts,
        IReadOnlyList<TaskGraphDraftRecord> linkedTaskGraphDrafts,
        IReadOnlyList<TaskNode> linkedTasks,
        IReadOnlyList<string> blockers,
        bool replanRequired)
    {
        return new FormalPlanningBriefing
        {
            Summary = state switch
            {
                FormalPlanningState.Planning => $"Formal planning is active on '{activePlanningCard.OperatorIntent.Title}' but no official card draft exists yet.",
                FormalPlanningState.PlanBound => $"Formal planning is bound to {linkedCardDrafts.Count} card draft(s) and {linkedTaskGraphDrafts.Count} taskgraph draft(s) on the same plan handle.",
                FormalPlanningState.ExecutionBound => $"Formal planning is bound to {linkedTasks.Count} task(s) on the same plan handle.",
                FormalPlanningState.ReviewBound => $"Formal planning is waiting on review or approval across {linkedTasks.Count} bound task(s).",
                FormalPlanningState.Closed => "Formal planning lineage is closed; all bound tasks reached terminal completion.",
                FormalPlanningState.PlanInitRequired => "Guided planning is ready for formal planning, but no active planning card exists yet.",
                _ => status.Rationale,
            },
            RecommendedNextAction = ResolveRecommendedNextAction(state, linkedCardDrafts, linkedTaskGraphDrafts, replanRequired),
            Rationale = ResolveBriefingRationale(state, blockers),
            NextActionPosture = ResolveNextActionPosture(state, linkedCardDrafts, linkedTaskGraphDrafts, replanRequired),
            ReplanRequired = replanRequired,
        };
    }

    private static FormalPlanningNextActionPosture ResolveNextActionPosture(
        FormalPlanningState state,
        IReadOnlyList<CardDraftRecord> linkedCardDrafts,
        IReadOnlyList<TaskGraphDraftRecord> linkedTaskGraphDrafts,
        bool replanRequired)
    {
        if (replanRequired)
        {
            return FormalPlanningNextActionPosture.ReplanRefreshRequired;
        }

        return state switch
        {
            FormalPlanningState.Discuss => FormalPlanningNextActionPosture.DiscussionOnly,
            FormalPlanningState.PlanInitRequired => FormalPlanningNextActionPosture.PlanInitRequired,
            FormalPlanningState.Planning => FormalPlanningNextActionPosture.PlanExportRequired,
            FormalPlanningState.PlanBound when linkedCardDrafts.Count == 0 => FormalPlanningNextActionPosture.CardDraftRequired,
            FormalPlanningState.PlanBound when linkedTaskGraphDrafts.Count == 0 => FormalPlanningNextActionPosture.TaskGraphDraftRequired,
            FormalPlanningState.PlanBound => FormalPlanningNextActionPosture.TaskGraphDraftRequired,
            FormalPlanningState.ExecutionBound => FormalPlanningNextActionPosture.ExecutionFollowThrough,
            FormalPlanningState.ReviewBound => FormalPlanningNextActionPosture.ReviewFollowThrough,
            FormalPlanningState.Closed => FormalPlanningNextActionPosture.ClosedObserve,
            _ => FormalPlanningNextActionPosture.DiscussionOnly,
        };
    }

    private static string ResolveRecommendedNextAction(
        FormalPlanningState state,
        IReadOnlyList<CardDraftRecord> linkedCardDrafts,
        IReadOnlyList<TaskGraphDraftRecord> linkedTaskGraphDrafts,
        bool replanRequired)
    {
        if (replanRequired)
        {
            return "Inspect the bound task replan signal, refresh the planning packet on the same plan handle, and reopen planning on the current lineage before new execution.";
        }

        return state switch
        {
            FormalPlanningState.Discuss => "Continue discussion or guided planning before entering formal planning.",
            FormalPlanningState.PlanInitRequired => "Run `plan init [candidate-card-id]` to issue one active planning card.",
            FormalPlanningState.Planning => "Run `plan export-card <json-path>` and create the official card draft on the same planning lineage.",
            FormalPlanningState.PlanBound when linkedCardDrafts.Count == 0 => "Create the official card draft on the current plan handle before moving to taskgraph work.",
            FormalPlanningState.PlanBound when linkedTaskGraphDrafts.Count == 0 => "Create a taskgraph draft that keeps `planning_lineage` on the current plan handle.",
            FormalPlanningState.PlanBound => "Approve the current taskgraph draft through the host-routed planning lane on the same plan handle.",
            FormalPlanningState.ExecutionBound => "Continue execution on the bound task truth or inspect the current task packet on the same plan handle.",
            FormalPlanningState.ReviewBound => "Finish review and approval on the bound tasks or reopen planning if the current evidence invalidates the decomposition.",
            FormalPlanningState.Closed => "Observe closed planning truth or issue a fresh `plan init` if a new bounded slice is required.",
            _ => "Inspect the current planning packet.",
        };
    }

    private static string ResolveBriefingRationale(FormalPlanningState state, IReadOnlyList<string> blockers)
    {
        if (blockers.Count == 0)
        {
            return state switch
            {
                FormalPlanningState.Planning => "one active planning card exists and still needs export into official card truth",
                FormalPlanningState.PlanBound => "planning drafts already point back to the current active planning card",
                FormalPlanningState.ExecutionBound => "execution truth already points back to the active planning card",
                FormalPlanningState.ReviewBound => "bound tasks are waiting in review or approval",
                FormalPlanningState.Closed => "all bound tasks reached a terminal completion state",
                FormalPlanningState.PlanInitRequired => "guided planning is grounded enough for formal planning, but no active planning card exists yet",
                _ => "formal planning remains discussion-only",
            };
        }

        return string.Join(" ", blockers);
    }

    private static IReadOnlyList<string> BuildBlockers(
        FormalPlanningState state,
        IReadOnlyList<CardDraftRecord> linkedCardDrafts,
        IReadOnlyList<TaskGraphDraftRecord> linkedTaskGraphDrafts,
        IReadOnlyList<TaskNode> linkedTasks,
        bool replanRequired)
    {
        var blockers = new List<string>();
        if (replanRequired)
        {
            var taskIds = linkedTasks
                .Where(static task => string.Equals(task.Metadata.GetValueOrDefault("planner_replan_required"), "true", StringComparison.OrdinalIgnoreCase))
                .Select(static task => task.TaskId)
                .ToArray();
            blockers.Add($"Bound task truth now requires replan before continuing on this planning lineage: {string.Join(", ", taskIds)}.");
        }

        switch (state)
        {
            case FormalPlanningState.PlanInitRequired:
                blockers.Add("No active planning card exists yet for formal planning.");
                break;
            case FormalPlanningState.Planning:
                blockers.Add("Official card draft truth has not been created from the active planning card export.");
                break;
            case FormalPlanningState.PlanBound when linkedCardDrafts.Count > 0 && linkedTaskGraphDrafts.Count == 0:
                blockers.Add("Taskgraph draft truth has not been created on the current planning lineage.");
                break;
            case FormalPlanningState.PlanBound when linkedTaskGraphDrafts.Count > 0 && linkedTasks.Count == 0:
                blockers.Add("Approved task truth has not been created from the current taskgraph draft.");
                break;
            case FormalPlanningState.ReviewBound:
                blockers.Add("Bound tasks are waiting in review or approval before the planning lineage can close.");
                break;
        }

        return blockers;
    }

    private static IReadOnlyList<string> BuildConstraints(ActivePlanningCard activePlanningCard, IReadOnlyList<CardDraftRecord> linkedCardDrafts)
    {
        var constraints = linkedCardDrafts
            .SelectMany(item => item.Constraints)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .ToArray();
        if (constraints.Length > 0)
        {
            return constraints.Distinct(StringComparer.Ordinal).ToArray();
        }

        return activePlanningCard.OperatorIntent.Constraints
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildNonGoals(ActivePlanningCard activePlanningCard, IReadOnlyList<CardDraftRecord> linkedCardDrafts)
    {
        var nonGoals = linkedCardDrafts
            .SelectMany(item => item.RealityModel?.NonGoals ?? Array.Empty<string>())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .ToArray();
        if (nonGoals.Length > 0)
        {
            return nonGoals.Distinct(StringComparer.Ordinal).ToArray();
        }

        return activePlanningCard.OperatorIntent.NonGoals
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildDecompositionCandidates(
        ActivePlanningCard activePlanningCard,
        IReadOnlyList<TaskGraphDraftRecord> linkedTaskGraphDrafts)
    {
        var candidates = new List<string>();
        candidates.AddRange(activePlanningCard.AgentProposal.DecompositionCandidates);
        candidates.AddRange(linkedTaskGraphDrafts
            .SelectMany(item => item.Tasks)
            .Select(task => $"{task.TaskId}: {task.Title}"));
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(activePlanningCard.AgentProposal.CandidateSummary))
        {
            candidates.Add(activePlanningCard.AgentProposal.CandidateSummary);
        }

        return candidates
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildEvidenceExpectations(
        ActivePlanningCard activePlanningCard,
        AcceptanceBindingSelection acceptanceBinding)
    {
        if (acceptanceBinding.Contract is not null)
        {
            var evidence = acceptanceBinding.Contract.EvidenceRequired
                .Select(requirement => string.IsNullOrWhiteSpace(requirement.Description)
                    ? requirement.Type
                    : $"{requirement.Type}: {requirement.Description}")
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
            if (evidence.Length > 0)
            {
                return evidence;
            }
        }

        var fallback = new List<string>();
        if (!string.IsNullOrWhiteSpace(activePlanningCard.OperatorIntent.ValidationArtifact))
        {
            fallback.Add(activePlanningCard.OperatorIntent.ValidationArtifact);
        }

        fallback.AddRange(activePlanningCard.OperatorIntent.AcceptanceOutline);
        return fallback
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildAllowedScopeSummary(
        IReadOnlyList<CardDraftRecord> linkedCardDrafts,
        IReadOnlyList<TaskGraphDraftRecord> linkedTaskGraphDrafts,
        IReadOnlyList<TaskNode> linkedTasks)
    {
        var scope = linkedTasks
            .SelectMany(item => item.Scope)
            .Concat(linkedTaskGraphDrafts.SelectMany(item => item.Tasks).SelectMany(task => task.Scope))
            .Concat(linkedCardDrafts.SelectMany(item => item.Scope))
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (scope.Length > 0)
        {
            return scope;
        }

        return
        [
            "No official scope is bound yet; remain on the active planning card, the current candidate lane, and host-routed planning files until card or taskgraph truth is created.",
        ];
    }

    private static IReadOnlyList<FormalPlanningReplanRule> BuildReplanRules()
    {
        return
        [
            new FormalPlanningReplanRule
            {
                RuleId = "scope_escape",
                Trigger = "touches_paths_outside_declared_scope",
                Summary = "If the work needs paths outside the current declared scope, the packet is no longer sufficient.",
                RequiredAction = "Stop freeform continuation, inspect the current planning packet, and reopen planning before widening the slice.",
                ReentryCommand = "plan packet",
            },
            new FormalPlanningReplanRule
            {
                RuleId = "subsystem_growth",
                Trigger = "needs_more_modules_than_current_task_allows",
                Summary = "If more modules or subsystems are needed than the current task allows, the decomposition has drifted.",
                RequiredAction = "Return to planning on the same lineage and create or revise the taskgraph draft before continuing.",
                ReentryCommand = "plan status",
            },
            new FormalPlanningReplanRule
            {
                RuleId = "acceptance_gap",
                Trigger = "acceptance_contract_missing_or_materially_incomplete",
                Summary = "If canonical acceptance semantics are missing or materially incomplete, the packet must be refreshed before execution continues.",
                RequiredAction = "Refresh planning truth and bind the missing acceptance shape through card or taskgraph truth before continuing.",
                ReentryCommand = "plan export-card <json-path>",
            },
            new FormalPlanningReplanRule
            {
                RuleId = "blocker_shape_change",
                Trigger = "blocker_changes_task_shape",
                Summary = "If the current blocker changes the task shape, the old decomposition is no longer authoritative.",
                RequiredAction = "Re-enter planning and replace the old next action with a new bounded decomposition or an explicit stop.",
                ReentryCommand = "plan init [candidate-card-id]",
            },
            new FormalPlanningReplanRule
            {
                RuleId = "evidence_invalidates_decomposition",
                Trigger = "review_or_validation_evidence_invalidates_current_decomposition",
                Summary = "If review or validation evidence disproves the current decomposition, the packet must be refreshed mechanically.",
                RequiredAction = "Inspect the affected truth, refresh the planning packet, and reopen planning on the same lineage before more execution.",
                ReentryCommand = "plan packet",
            },
        ];
    }

    private static FormalPlanningAcceptanceSummary BuildAcceptanceSummary(
        ActivePlanningCard activePlanningCard,
        AcceptanceBindingSelection acceptanceBinding)
    {
        if (acceptanceBinding.Contract is null)
        {
            return new FormalPlanningAcceptanceSummary
            {
                BindingState = acceptanceBinding.BindingState,
                SummaryLines = activePlanningCard.OperatorIntent.AcceptanceOutline,
                GapSummary = acceptanceBinding.GapSummary,
            };
        }

        return new FormalPlanningAcceptanceSummary
        {
            BindingState = acceptanceBinding.BindingState,
            ContractId = acceptanceBinding.Contract.ContractId,
            LifecycleStatus = acceptanceBinding.Contract.Status,
            SummaryLines = AcceptanceContractSummaryFormatter.BuildSummaryLines(acceptanceBinding.Contract),
            GapSummary = acceptanceBinding.GapSummary,
        };
    }

    private static AcceptanceBindingSelection SelectAcceptanceBinding(
        ActivePlanningCard activePlanningCard,
        IReadOnlyList<CardDraftRecord> linkedCardDrafts,
        IReadOnlyList<TaskGraphDraftRecord> linkedTaskGraphDrafts,
        IReadOnlyList<TaskNode> linkedTasks)
    {
        var taskContract = linkedTasks
            .Select(task => task.AcceptanceContract)
            .FirstOrDefault(static contract => contract is not null);
        if (taskContract is not null)
        {
            return new AcceptanceBindingSelection(
                "task_truth_bound",
                taskContract,
                "Canonical acceptance contract is already bound through approved task truth on the current plan handle.");
        }

        var taskGraphDraftContract = linkedTaskGraphDrafts
            .SelectMany(item => item.Tasks)
            .Select(task => task.AcceptanceContract)
            .FirstOrDefault(static contract => contract is not null);
        if (taskGraphDraftContract is not null)
        {
            return new AcceptanceBindingSelection(
                "taskgraph_draft_bound",
                taskGraphDraftContract,
                "Canonical acceptance contract is currently bound through taskgraph draft truth on the current plan handle.");
        }

        var cardDraftContract = linkedCardDrafts
            .Select(item => item.AcceptanceContract)
            .FirstOrDefault(static contract => contract is not null);
        if (cardDraftContract is not null)
        {
            return new AcceptanceBindingSelection(
                "card_draft_bound",
                cardDraftContract,
                "Canonical acceptance contract is currently bound through card draft truth on the current plan handle.");
        }

        return new AcceptanceBindingSelection(
            "not_bound_yet",
            null,
            $"No canonical acceptance contract is bound yet. Use the active planning card acceptance outline and validation artifact '{activePlanningCard.OperatorIntent.ValidationArtifact}' until official card or taskgraph truth exists.");
    }

    private static bool MatchesPlanningLineage(PlanningLineage? lineage, ActivePlanningCard activePlanningCard)
    {
        return lineage is not null
            && string.Equals(lineage.PlanningSlotId, activePlanningCard.PlanningSlotId, StringComparison.Ordinal)
            && string.Equals(lineage.ActivePlanningCardId, activePlanningCard.PlanningCardId, StringComparison.Ordinal);
    }

    private sealed record AcceptanceBindingSelection(
        string BindingState,
        AcceptanceContract? Contract,
        string GapSummary);
}
