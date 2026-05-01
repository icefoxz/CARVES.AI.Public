using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed partial class WorkbenchSurfaceService
{
    public WorkbenchOverviewReadModel BuildOverview()
    {
        var graph = taskGraphService.Load();
        var completed = graph.CompletedTaskIds();
        var tasks = graph.ListTasks().ToArray();
        var session = devLoopService.GetSession();
        var dispatch = dispatchProjectionService.Build(graph, session, maxParallelTasks);
        var platformStatus = operatorApiService.GetPlatformStatus();
        var repoSummary = platformStatus.Repos.FirstOrDefault();
        var operational = operationalSummaryService.Build(refreshProviderHealth: false);
        var hostSession = new HostSessionService(paths).Load();
        var agentWorkingModes = runtimeAgentWorkingModesFactory();
        var formalPlanningPosture = runtimeFormalPlanningPostureFactory();
        var vendorNativeAcceleration = runtimeVendorNativeAccelerationFactory();
        var modeExecutionEntryGateService = new ModeExecutionEntryGateService(formalPlanningExecutionGateService);
        var focusTasks = tasks
            .Where(task => task.Status is DomainTaskStatus.Running or DomainTaskStatus.Review or DomainTaskStatus.ApprovalWait or DomainTaskStatus.Blocked
                || (task.Status == DomainTaskStatus.Pending && modeExecutionEntryGateService.Evaluate(task).BlocksExecution)
                || (!modeExecutionEntryGateService.Evaluate(task).BlocksExecution
                    && task.Status == DomainTaskStatus.Pending
                    && task.CanDispatchToWorkerPool
                    && task.IsReady(completed)))
            .OrderBy(task => RankTask(task, completed, formalPlanningExecutionGateService))
            .ThenBy(task => task.TaskId, StringComparer.Ordinal)
            .Take(8)
            .Select(task => ToTaskListItem(task, completed, formalPlanningExecutionGateService))
            .ToArray();

        var currentTask = ResolveCurrentTask(graph, dispatch, session);
        var currentCardId = currentTask?.CardId;
        var pendingApprovals = operatorApiService.GetPendingWorkerPermissionRequests().Count;

        return new WorkbenchOverviewReadModel
        {
            RepoRoot = repoRoot,
            RepoId = repoSummary?.RepoId ?? Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Summary = $"Current card={currentCardId ?? "(none)"}; next task={dispatch.NextTaskId ?? "(none)"}; mode={agentWorkingModes.CurrentMode}; external_agent_recommended_mode={agentWorkingModes.ExternalAgentRecommendedMode}; external_agent_blockers={agentWorkingModes.ExternalAgentStrongerModeBlockerCount}; mode_e_activation={agentWorkingModes.ModeEOperationalActivationState}; planning_posture={formalPlanningPosture.OverallPosture}; planning_entry={formalPlanningPosture.FormalPlanningEntryTriggerState}; planning_entry_command={formalPlanningPosture.FormalPlanningEntryCommand}; active_planning_slot={formalPlanningPosture.ActivePlanningSlotState}; planning_card_invariant={formalPlanningPosture.PlanningCardInvariantState}; planning_card_fill={formalPlanningPosture.ActivePlanningCardFillState}; plan_handle={formalPlanningPosture.PlanHandle ?? "(none)"}; workspace_posture={formalPlanningPosture.ManagedWorkspacePosture}; vendor_native_acceleration={vendorNativeAcceleration.OverallPosture}; review={tasks.Count(task => task.Status == DomainTaskStatus.Review)}; blocked={tasks.Count(task => task.Status == DomainTaskStatus.Blocked)}; acceptance_contract_gaps={dispatch.AcceptanceContractGapCount}; plan_required={dispatch.PlanRequiredBlockCount}; workspace_required={dispatch.WorkspaceRequiredBlockCount}; mode_entry_first_blocker={dispatch.FirstBlockingCheckId ?? "(none)"}; mode_entry_command={dispatch.RecommendedNextCommand ?? "(none)"}.",
            SessionStatus = session?.Status.ToString().ToLowerInvariant() ?? "none",
            HostControlState = hostSession?.ControlState.ToString().ToLowerInvariant() ?? "running",
            Actionability = operational.Actionability,
            WaitingReason = session?.WaitingReason,
            CurrentCardId = currentCardId,
            CurrentTaskId = currentTask?.TaskId,
            NextTaskId = dispatch.NextTaskId,
            CurrentMode = agentWorkingModes.CurrentMode,
            ExternalAgentRecommendedMode = agentWorkingModes.ExternalAgentRecommendedMode,
            ExternalAgentRecommendationPosture = agentWorkingModes.ExternalAgentRecommendationPosture,
            ExternalAgentRecommendationSummary = agentWorkingModes.ExternalAgentRecommendationSummary,
            ExternalAgentRecommendedAction = agentWorkingModes.ExternalAgentRecommendedAction,
            ExternalAgentConstraintTier = agentWorkingModes.ExternalAgentConstraintTier,
            ExternalAgentConstraintSummary = agentWorkingModes.ExternalAgentConstraintSummary,
            ExternalAgentStrongerModeBlockerCount = agentWorkingModes.ExternalAgentStrongerModeBlockerCount,
            ExternalAgentFirstStrongerModeBlockerId = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerId,
            ExternalAgentFirstStrongerModeBlockerTargetMode = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerTargetMode,
            ExternalAgentFirstStrongerModeBlockerRequiredAction = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerRequiredAction,
            ExternalAgentFirstStrongerModeBlockerConstraintClass = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerConstraintClass,
            ExternalAgentFirstStrongerModeBlockerEnforcementLevel = agentWorkingModes.ExternalAgentFirstStrongerModeBlockerEnforcementLevel,
            ModeEOperationalActivationState = agentWorkingModes.ModeEOperationalActivationState,
            ModeEOperationalActivationSummary = agentWorkingModes.ModeEOperationalActivationSummary,
            ModeEActivationTaskId = agentWorkingModes.ModeEActivationTaskId,
            ModeEActivationResultReturnChannel = agentWorkingModes.ModeEActivationResultReturnChannel,
            ModeEActivationCommands = agentWorkingModes.ModeEActivationCommands,
            ModeEActivationRecommendedNextAction = agentWorkingModes.ModeEActivationRecommendedNextAction,
            ModeEActivationBlockingCheckCount = agentWorkingModes.ModeEActivationBlockingCheckCount,
            ModeEActivationFirstBlockingCheckId = agentWorkingModes.ModeEActivationFirstBlockingCheckId,
            ModeEActivationFirstBlockingCheckSummary = agentWorkingModes.ModeEActivationFirstBlockingCheckSummary,
            ModeEActivationFirstBlockingCheckRequiredAction = agentWorkingModes.ModeEActivationFirstBlockingCheckRequiredAction,
            ModeEActivationPlaybookSummary = agentWorkingModes.ModeEActivationPlaybookSummary,
            ModeEActivationPlaybookStepCount = agentWorkingModes.ModeEActivationPlaybookStepCount,
            ModeEActivationFirstPlaybookStepCommand = agentWorkingModes.ModeEActivationFirstPlaybookStepCommand,
            ModeEActivationFirstPlaybookStepSummary = agentWorkingModes.ModeEActivationFirstPlaybookStepSummary,
            PlanningCouplingPosture = agentWorkingModes.PlanningCouplingPosture,
            FormalPlanningPosture = formalPlanningPosture.OverallPosture,
            FormalPlanningEntryTriggerState = formalPlanningPosture.FormalPlanningEntryTriggerState,
            FormalPlanningEntryCommand = formalPlanningPosture.FormalPlanningEntryCommand,
            FormalPlanningEntryRecommendedNextAction = formalPlanningPosture.FormalPlanningEntryRecommendedNextAction,
            FormalPlanningEntrySummary = formalPlanningPosture.FormalPlanningEntrySummary,
            ActivePlanningSlotState = formalPlanningPosture.ActivePlanningSlotState,
            ActivePlanningSlotCanInitialize = formalPlanningPosture.ActivePlanningSlotCanInitialize,
            ActivePlanningSlotConflictReason = formalPlanningPosture.ActivePlanningSlotConflictReason,
            ActivePlanningSlotRemediationAction = formalPlanningPosture.ActivePlanningSlotRemediationAction,
            PlanningCardInvariantState = formalPlanningPosture.PlanningCardInvariantState,
            PlanningCardInvariantCanExportGovernedTruth = formalPlanningPosture.PlanningCardInvariantCanExportGovernedTruth,
            PlanningCardInvariantSummary = formalPlanningPosture.PlanningCardInvariantSummary,
            PlanningCardInvariantRemediationAction = formalPlanningPosture.PlanningCardInvariantRemediationAction,
            PlanningCardInvariantBlockCount = formalPlanningPosture.PlanningCardInvariantBlockCount,
            PlanningCardInvariantViolationCount = formalPlanningPosture.PlanningCardInvariantViolationCount,
            ActivePlanningCardFillState = formalPlanningPosture.ActivePlanningCardFillState,
            ActivePlanningCardFillCompletionPosture = formalPlanningPosture.ActivePlanningCardFillCompletionPosture,
            ActivePlanningCardFillReadyForRecommendedExport = formalPlanningPosture.ActivePlanningCardFillReadyForRecommendedExport,
            ActivePlanningCardFillSummary = formalPlanningPosture.ActivePlanningCardFillSummary,
            ActivePlanningCardFillRecommendedNextAction = formalPlanningPosture.ActivePlanningCardFillRecommendedNextAction,
            ActivePlanningCardFillNextMissingFieldPath = formalPlanningPosture.ActivePlanningCardFillNextMissingFieldPath,
            ActivePlanningCardFillRequiredFieldCount = formalPlanningPosture.ActivePlanningCardFillRequiredFieldCount,
            ActivePlanningCardFillMissingRequiredFieldCount = formalPlanningPosture.ActivePlanningCardFillMissingRequiredFieldCount,
            ActivePlanningCardFillMissingFieldPaths = formalPlanningPosture.ActivePlanningCardFillMissingFieldPaths,
            PlanHandle = formalPlanningPosture.PlanHandle,
            PlanningCardId = formalPlanningPosture.PlanningCardId,
            ManagedWorkspacePosture = formalPlanningPosture.ManagedWorkspacePosture,
            VendorNativeAccelerationPosture = vendorNativeAcceleration.OverallPosture,
            CodexReinforcementState = vendorNativeAcceleration.CodexReinforcementState,
            ClaudeReinforcementState = vendorNativeAcceleration.ClaudeReinforcementState,
            ReadyTaskCount = dispatch.ReadyTaskCount,
            RunningTaskCount = tasks.Count(task => task.Status == DomainTaskStatus.Running),
            ReviewTaskCount = tasks.Count(task => task.Status == DomainTaskStatus.Review),
            BlockedTaskCount = tasks.Count(task => task.Status == DomainTaskStatus.Blocked),
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
            PendingApprovalCount = pendingApprovals,
            FocusTasks = focusTasks,
            AvailableActions = [BuildSyncAction()],
        };
    }
}
