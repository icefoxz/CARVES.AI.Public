using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeFormalPlanningPostureService
{
    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly IntentDiscoveryService intentDiscoveryService;
    private readonly FormalPlanningPacketService formalPlanningPacketService;
    private readonly ManagedWorkspaceLeaseService managedWorkspaceLeaseService;
    private readonly TaskGraphService taskGraphService;
    private readonly DispatchProjectionService dispatchProjectionService;
    private readonly Func<RuntimeSessionState?> sessionResolver;
    private readonly int maxParallelTasks;

    public RuntimeFormalPlanningPostureService(
        string repoRoot,
        IntentDiscoveryService intentDiscoveryService,
        FormalPlanningPacketService formalPlanningPacketService,
        ManagedWorkspaceLeaseService managedWorkspaceLeaseService,
        TaskGraphService taskGraphService,
        DispatchProjectionService dispatchProjectionService,
        Func<RuntimeSessionState?>? sessionResolver,
        int maxParallelTasks)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.intentDiscoveryService = intentDiscoveryService;
        this.formalPlanningPacketService = formalPlanningPacketService;
        this.managedWorkspaceLeaseService = managedWorkspaceLeaseService;
        this.taskGraphService = taskGraphService;
        this.dispatchProjectionService = dispatchProjectionService;
        this.sessionResolver = sessionResolver ?? (() => null);
        this.maxParallelTasks = Math.Max(1, maxParallelTasks);
    }

    public RuntimeFormalPlanningPostureSurface Build()
    {
        var errors = new List<string>();
        const string planModeDocumentPath = "docs/runtime/runtime-plan-mode-and-active-planning-card.md";
        const string planningPacketDocumentPath = "docs/runtime/runtime-planning-packet-and-replan-rules.md";
        const string planningGateDocumentPath = "docs/runtime/runtime-plan-required-and-workspace-required-gates.md";
        const string managedWorkspaceDocumentPath = "docs/runtime/runtime-managed-workspace-lease.md";
        const string collaborationProjectionDocumentPath = "docs/runtime/runtime-collaboration-and-surface-projection.md";

        ValidatePath(planModeDocumentPath, "Plan-mode document", errors);
        ValidatePath(planningPacketDocumentPath, "Planning packet document", errors);
        ValidatePath(planningGateDocumentPath, "Planning gate document", errors);
        ValidatePath(managedWorkspaceDocumentPath, "Managed workspace lease document", errors);
        ValidatePath(collaborationProjectionDocumentPath, "Collaboration and surface projection document", errors);

        var status = intentDiscoveryService.GetStatus();
        var packet = formalPlanningPacketService.TryBuildCurrentPacket();
        var workspaceSurface = managedWorkspaceLeaseService.BuildSurface();
        var graph = taskGraphService.Load();
        var dispatch = dispatchProjectionService.Build(graph, sessionResolver(), maxParallelTasks);
        var planningCouplingPosture = RuntimeWorkingModesSemantics.ResolvePlanningCouplingPosture(status, packet, workspaceSurface);
        var formalPlanningEntry = RuntimeFormalPlanningEntryProjectionResolver.Resolve(status, packet);
        var activePlanningSlot = ActivePlanningSlotProjectionResolver.Resolve(status, packet);
        var planningCardInvariant = PlanningCardInvariantService.Evaluate(status.Draft, status.Draft?.ActivePlanningCard);
        var planningCardFillGuidance = PlanningCardFillGuidanceService.Evaluate(status.Draft?.ActivePlanningCard, planningCardInvariant);

        return new RuntimeFormalPlanningPostureSurface
        {
            PlanModeDocumentPath = planModeDocumentPath,
            PlanningPacketDocumentPath = planningPacketDocumentPath,
            PlanningGateDocumentPath = planningGateDocumentPath,
            ManagedWorkspaceDocumentPath = managedWorkspaceDocumentPath,
            OverallPosture = ResolveOverallPosture(status, packet, workspaceSurface, dispatch, planningCardInvariant, errors),
            IntentState = status.State.ToString().ToLowerInvariant(),
            GuidedPlanningPosture = status.Draft is null
                ? null
                : RuntimeWorkingModesSemantics.ToSnakeCase(status.Draft.PlanningPosture),
            FormalPlanningState = packet is not null
                ? RuntimeWorkingModesSemantics.ToSnakeCase(packet.FormalPlanningState)
                : status.Draft is null
                    ? "discuss"
                    : RuntimeWorkingModesSemantics.ToSnakeCase(status.Draft.FormalPlanningState),
            FormalPlanningEntryTriggerState = formalPlanningEntry.TriggerState,
            FormalPlanningEntryCommand = formalPlanningEntry.Command,
            FormalPlanningEntryRecommendedNextAction = formalPlanningEntry.RecommendedNextAction,
            FormalPlanningEntrySummary = formalPlanningEntry.Summary,
            ActivePlanningSlotState = activePlanningSlot.State,
            ActivePlanningSlotCanInitialize = activePlanningSlot.CanInitializeFormalPlanning,
            ActivePlanningSlotConflictReason = activePlanningSlot.ConflictReason,
            ActivePlanningSlotRemediationAction = activePlanningSlot.RemediationAction,
            PlanningCardInvariantState = planningCardInvariant.State,
            PlanningCardInvariantCanExportGovernedTruth = planningCardInvariant.CanExportGovernedTruth,
            PlanningCardInvariantSummary = planningCardInvariant.Summary,
            PlanningCardInvariantRemediationAction = planningCardInvariant.RemediationAction,
            PlanningCardInvariantBlockCount = planningCardInvariant.Blocks.Count,
            PlanningCardInvariantViolationCount = planningCardInvariant.Violations.Count,
            ActivePlanningCardFillState = planningCardFillGuidance.State,
            ActivePlanningCardFillCompletionPosture = planningCardFillGuidance.CompletionPosture,
            ActivePlanningCardFillReadyForRecommendedExport = planningCardFillGuidance.ReadyForRecommendedExport,
            ActivePlanningCardFillSummary = planningCardFillGuidance.Summary,
            ActivePlanningCardFillRecommendedNextAction = planningCardFillGuidance.RecommendedNextFillAction,
            ActivePlanningCardFillNextMissingFieldPath = planningCardFillGuidance.NextMissingFieldPath,
            ActivePlanningCardFillRequiredFieldCount = planningCardFillGuidance.RequiredFieldCount,
            ActivePlanningCardFillMissingRequiredFieldCount = planningCardFillGuidance.MissingRequiredFieldCount,
            ActivePlanningCardFillMissingFieldPaths = planningCardFillGuidance.MissingRequiredFields.Select(field => field.FieldPath).ToArray(),
            CurrentMode = RuntimeWorkingModesSemantics.ResolveCurrentMode(workspaceSurface),
            PlanningCouplingPosture = planningCouplingPosture,
            PlanningCouplingSummary = RuntimeWorkingModesSemantics.ResolvePlanningCouplingSummary(planningCouplingPosture, status, packet, workspaceSurface),
            PlanningSlotId = packet?.PlanningSlotId ?? status.Draft?.ActivePlanningCard?.PlanningSlotId,
            PlanHandle = packet?.PlanHandle,
            PlanningCardId = packet?.PlanningCardId ?? status.Draft?.ActivePlanningCard?.PlanningCardId,
            PacketAvailable = packet is not null,
            PacketSummary = packet?.Briefing.Summary,
            RecommendedNextAction = packet?.Briefing.RecommendedNextAction ?? status.RecommendedNextAction,
            Rationale = packet?.Briefing.Rationale ?? status.Rationale,
            NextActionPosture = packet is null ? null : RuntimeWorkingModesSemantics.ToSnakeCase(packet.Briefing.NextActionPosture),
            ReplanRequired = packet?.Briefing.ReplanRequired ?? false,
            ManagedWorkspacePosture = workspaceSurface.OverallPosture,
            PathPolicyEnforcementState = workspaceSurface.PathPolicyEnforcementState,
            ActiveLeaseCount = workspaceSurface.ActiveLeases.Count,
            ActiveLeaseTaskIds = workspaceSurface.ActiveLeases.Select(lease => lease.TaskId).Distinct(StringComparer.Ordinal).ToArray(),
            DispatchState = dispatch.State,
            AcceptanceContractGapCount = dispatch.AcceptanceContractGapCount,
            PlanRequiredBlockCount = dispatch.PlanRequiredBlockCount,
            WorkspaceRequiredBlockCount = dispatch.WorkspaceRequiredBlockCount,
            ModeExecutionEntryFirstBlockedTaskId = dispatch.FirstBlockedTaskId,
            ModeExecutionEntryFirstBlockingCheckId = dispatch.FirstBlockingCheckId,
            ModeExecutionEntryFirstBlockingCheckSummary = dispatch.FirstBlockingCheckSummary,
            ModeExecutionEntryFirstBlockingCheckRequiredAction = dispatch.FirstBlockingCheckRequiredAction,
            ModeExecutionEntryFirstBlockingCheckRequiredCommand = dispatch.FirstBlockingCheckRequiredCommand,
            ModeExecutionEntryRecommendedNextAction = dispatch.RecommendedNextAction,
            ModeExecutionEntryRecommendedNextCommand = dispatch.RecommendedNextCommand,
            MissingPrerequisites = BuildMissingPrerequisites(status, packet, workspaceSurface, dispatch, planningCardInvariant, planningCardFillGuidance),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not create a second planner or second planning slot.",
                "This surface does not bypass acceptance-contract execution gating while reporting plan or workspace posture.",
                "This surface does not mutate task truth while projecting active planning-card fill guidance.",
                "This surface does not claim that vendor-native acceleration is required for formal planning posture to stay queryable.",
            ],
        };
    }

    private static string ResolveOverallPosture(
        IntentDiscoveryStatus status,
        FormalPlanningPacket? packet,
        RuntimeManagedWorkspaceSurface workspaceSurface,
        DispatchProjection dispatch,
        PlanningCardInvariantReport planningCardInvariant,
        IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            return "blocked_by_formal_planning_posture_doctrine_gaps";
        }

        if (string.Equals(planningCardInvariant.State, PlanningCardInvariantService.DriftedState, StringComparison.Ordinal))
        {
            return "blocked_by_planning_card_invariant_drift";
        }

        if (packet is null)
        {
            return status.Draft?.FormalPlanningState is FormalPlanningState.PlanInitRequired
                ? "plan_init_required"
                : "discussion_only";
        }

        return packet.FormalPlanningState switch
        {
            FormalPlanningState.Planning => "planning_active",
            FormalPlanningState.PlanBound => "plan_bound",
            FormalPlanningState.ExecutionBound when dispatch.PlanRequiredBlockCount > 0 => "execution_blocked_by_plan_gap",
            FormalPlanningState.ExecutionBound when dispatch.WorkspaceRequiredBlockCount > 0 => "execution_blocked_by_workspace_gap",
            FormalPlanningState.ExecutionBound when dispatch.AcceptanceContractGapCount > 0 => "execution_blocked_by_acceptance_gap",
            FormalPlanningState.ExecutionBound when string.Equals(workspaceSurface.OverallPosture, "task_bound_workspace_active", StringComparison.Ordinal) => "execution_bound_with_active_workspace",
            FormalPlanningState.ExecutionBound => "execution_bound",
            FormalPlanningState.ReviewBound => "review_bound",
            FormalPlanningState.Closed => "closed",
            _ => RuntimeWorkingModesSemantics.ToSnakeCase(packet.FormalPlanningState),
        };
    }

    private static IReadOnlyList<string> BuildMissingPrerequisites(
        IntentDiscoveryStatus status,
        FormalPlanningPacket? packet,
        RuntimeManagedWorkspaceSurface workspaceSurface,
        DispatchProjection dispatch,
        PlanningCardInvariantReport planningCardInvariant,
        PlanningCardFillGuidanceReport planningCardFillGuidance)
    {
        var missing = new List<string>();

        if (status.Draft is null)
        {
            missing.Add("No intent draft is active, so Runtime remains in discussion-only posture.");
            return missing;
        }

        if (status.Draft.ActivePlanningCard is null)
        {
            if (status.Draft.FormalPlanningState == FormalPlanningState.PlanInitRequired)
            {
                missing.Add("Formal planning is ready but no active planning card exists yet. Run `plan init [candidate-card-id]`.");
            }

            return missing;
        }

        if (string.Equals(planningCardInvariant.State, PlanningCardInvariantService.DriftedState, StringComparison.Ordinal))
        {
            missing.Add($"Active planning card invariant drift detected. {planningCardInvariant.RemediationAction}");
        }

        if (string.Equals(planningCardFillGuidance.State, PlanningCardFillGuidanceService.NeedsFillState, StringComparison.Ordinal))
        {
            missing.Add($"Active planning card has {planningCardFillGuidance.MissingRequiredFieldCount} missing required editable field(s). {planningCardFillGuidance.RecommendedNextFillAction}");
        }

        if (packet is null)
        {
            missing.Add("An active planning card exists, but the canonical planning packet could not be built.");
            return missing;
        }

        if (packet.LinkedTruth.CardDraftIds.Count == 0)
        {
            missing.Add("Official card draft truth has not been created from the active planning card export.");
        }

        if (packet.LinkedTruth.CardDraftIds.Count > 0
            && packet.LinkedTruth.TaskGraphDraftIds.Count == 0
            && packet.LinkedTruth.TaskIds.Count == 0)
        {
            missing.Add("Taskgraph draft truth has not been created on the current planning lineage.");
        }

        if (packet.LinkedTruth.TaskGraphDraftIds.Count > 0 && packet.LinkedTruth.TaskIds.Count == 0)
        {
            missing.Add("Approved task truth has not been created from the current taskgraph draft.");
        }

        if (dispatch.AcceptanceContractGapCount > 0)
        {
            missing.Add(FormatDispatchGap(
                $"Dispatch is blocked by {dispatch.AcceptanceContractGapCount} acceptance-contract gap(s).",
                dispatch));
        }

        if (dispatch.PlanRequiredBlockCount > 0)
        {
            missing.Add(FormatDispatchGap(
                $"Dispatch is blocked by {dispatch.PlanRequiredBlockCount} formal-planning gap(s).",
                dispatch));
        }

        if (dispatch.WorkspaceRequiredBlockCount > 0)
        {
            missing.Add(FormatDispatchGap(
                $"Dispatch is blocked by {dispatch.WorkspaceRequiredBlockCount} managed-workspace gap(s).",
                dispatch));
        }

        if (packet.LinkedTruth.TaskIds.Count > 0
            && workspaceSurface.ActiveLeases.Count == 0
            && string.Equals(workspaceSurface.OverallPosture, "task_bound_workspace_ready_to_issue", StringComparison.Ordinal))
        {
            missing.Add($"Task-bound workspace issuance is ready but no active lease exists. {workspaceSurface.RecommendedNextAction}");
        }

        return missing;
    }

    private static string FormatDispatchGap(string prefix, DispatchProjection dispatch)
    {
        if (string.IsNullOrWhiteSpace(dispatch.FirstBlockingCheckId))
        {
            return prefix;
        }

        var command = string.IsNullOrWhiteSpace(dispatch.FirstBlockingCheckRequiredCommand)
            ? "(none)"
            : dispatch.FirstBlockingCheckRequiredCommand;
        return $"{prefix} First Mode C/D entry blocker: {dispatch.FirstBlockingCheckId}; next command: {command}.";
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

}
