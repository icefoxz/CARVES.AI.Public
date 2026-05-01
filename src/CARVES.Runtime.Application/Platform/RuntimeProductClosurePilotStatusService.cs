using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeProductClosurePilotStatusService
{
    private const string PhaseDocumentPath = RuntimeProductClosureMetadata.CurrentDocumentPath;
    public const string GuideDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md";
    private const string PreviousPhaseDocumentPath = RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath;

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly TaskGraphService taskGraphService;
    private readonly Func<RuntimeProductClosurePilotGuideSurface> guideFactory;
    private readonly Func<RuntimeFormalPlanningPostureSurface> formalPlanningFactory;
    private readonly Func<RuntimeManagedWorkspaceSurface> managedWorkspaceFactory;
    private readonly Func<RuntimeProductPilotProofSurface> productPilotProofFactory;

    public RuntimeProductClosurePilotStatusService(
        string repoRoot,
        TaskGraphService taskGraphService,
        Func<RuntimeProductClosurePilotGuideSurface> guideFactory,
        Func<RuntimeFormalPlanningPostureSurface> formalPlanningFactory,
        Func<RuntimeManagedWorkspaceSurface> managedWorkspaceFactory,
        Func<RuntimeProductPilotProofSurface>? productPilotProofFactory = null)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.taskGraphService = taskGraphService;
        this.guideFactory = guideFactory;
        this.formalPlanningFactory = formalPlanningFactory;
        this.managedWorkspaceFactory = managedWorkspaceFactory;
        this.productPilotProofFactory = productPilotProofFactory ?? (() => new RuntimeProductPilotProofService(this.repoRoot).Build());
    }

    public RuntimeProductClosurePilotStatusSurface Build()
    {
        var errors = new List<string>();
        ValidatePath(PhaseDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidatePath(GuideDocumentPath, "Productized pilot status guide document", errors);
        ValidatePath(RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "Frozen dist target readback proof guide document", errors);
        ValidatePath(RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "Local dist freshness smoke guide document", errors);
        ValidatePath(RuntimeTargetDistBindingPlanService.GuideDocumentPath, "Target dist binding plan guide document", errors);
        ValidatePath(RuntimeCliActivationPlanService.ActivationGuideDocumentPath, "CLI activation plan guide document", errors);
        ValidatePath(PreviousPhaseDocumentPath, "Product closure Phase 22 local dist freshness smoke document", errors);
        ValidatePath(RuntimeTargetDistBindingPlanService.PhaseDocumentPath, "Product closure Phase 21 target dist binding plan document", errors);
        ValidatePath(RuntimeFrozenDistTargetReadbackProofService.PhaseDocumentPath, "Product closure Phase 23 frozen dist target readback proof document", errors);
        ValidatePath("docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md", "Product closure Phase 24 wrapper runtime root binding document", errors);
        ValidatePath(RuntimeCliActivationPlanService.PhaseDocumentPath, "Product closure Phase 20 CLI activation plan document", errors);
        ValidatePath(RuntimeCliInvocationContractService.PhaseDocumentPath, "Product closure Phase 19 CLI invocation contract document", errors);
        ValidatePath(RuntimeExternalConsumerResourcePackService.PhaseDocumentPath, "Product closure Phase 18 external consumer resource pack document", errors);
        ValidatePath(RuntimeExternalConsumerResourcePackService.PreviousPhaseDocumentPath, "Product closure Phase 17 product pilot proof document", errors);
        ValidatePath(RuntimeLocalDistHandoffService.PhaseDocumentPath, "Product closure Phase 16 local dist handoff document", errors);

        var guide = guideFactory();
        var formalPlanning = formalPlanningFactory();
        var managedWorkspace = managedWorkspaceFactory();
        var targetAgentBootstrap = new RuntimeTargetAgentBootstrapPackService(repoRoot).Build(writeRequested: false);
        var cliInvocationContract = new RuntimeCliInvocationContractService(repoRoot).Build();
        var cliActivationPlan = new RuntimeCliActivationPlanService(repoRoot).Build();
        var localDistFreshnessSmoke = new RuntimeLocalDistFreshnessSmokeService(repoRoot).Build();
        var targetDistBindingPlan = new RuntimeTargetDistBindingPlanService(repoRoot).Build();
        var localDistHandoff = new RuntimeLocalDistHandoffService(repoRoot).Build();
        var frozenDistTargetReadbackProof = new RuntimeFrozenDistTargetReadbackProofService(repoRoot).Build();
        errors.AddRange(guide.Errors.Select(static error => $"runtime-product-closure-pilot-guide: {error}"));
        errors.AddRange(formalPlanning.Errors.Select(static error => $"runtime-formal-planning-posture: {error}"));
        errors.AddRange(managedWorkspace.Errors.Select(static error => $"runtime-managed-workspace: {error}"));
        errors.AddRange(targetAgentBootstrap.Errors.Select(static error => $"runtime-target-agent-bootstrap-pack: {error}"));
        errors.AddRange(cliInvocationContract.Errors.Select(static error => $"runtime-cli-invocation-contract: {error}"));
        errors.AddRange(cliActivationPlan.Errors.Select(static error => $"runtime-cli-activation-plan: {error}"));
        errors.AddRange(localDistFreshnessSmoke.Errors.Select(static error => $"runtime-local-dist-freshness-smoke: {error}"));
        errors.AddRange(targetDistBindingPlan.Errors.Select(static error => $"runtime-target-dist-binding-plan: {error}"));
        errors.AddRange(localDistHandoff.Errors.Select(static error => $"runtime-local-dist-handoff: {error}"));
        errors.AddRange(frozenDistTargetReadbackProof.Errors.Select(static error => $"runtime-frozen-dist-target-readback-proof: {error}"));

        var graph = taskGraphService.Load();
        var tasks = graph.Tasks.Values.ToArray();
        var runtimeInitialized = File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json"));
        var reviewTaskCount = tasks.Count(static task => task.Status == DomainTaskStatus.Review);
        var completedTaskCount = tasks.Count(static task => IsTerminal(task.Status));
        var targetCommitClosureRequired = runtimeInitialized
                                          && completedTaskCount > 0
                                          && string.Equals(managedWorkspace.OverallPosture, "planning_lineage_closed_no_active_workspace", StringComparison.Ordinal);
        var targetCommitClosure = targetCommitClosureRequired
            ? new RuntimeTargetCommitClosureService(repoRoot).Build()
            : BuildSkippedTargetCommitClosure(runtimeInitialized);
        var targetResiduePolicy = targetCommitClosureRequired
            ? new RuntimeTargetResiduePolicyService(repoRoot).Build()
            : BuildSkippedTargetResiduePolicy(runtimeInitialized);
        var targetIgnoreDecisionPlan = targetCommitClosureRequired
            ? new RuntimeTargetIgnoreDecisionPlanService(repoRoot).Build()
            : BuildSkippedTargetIgnoreDecisionPlan(runtimeInitialized);
        var targetIgnoreDecisionRecord = targetCommitClosureRequired
            ? new RuntimeTargetIgnoreDecisionRecordService(repoRoot).Build()
            : BuildSkippedTargetIgnoreDecisionRecord(runtimeInitialized);
        errors.AddRange(targetCommitClosure.Errors.Select(static error => $"runtime-target-commit-closure: {error}"));
        errors.AddRange(targetResiduePolicy.Errors.Select(static error => $"runtime-target-residue-policy: {error}"));
        errors.AddRange(targetIgnoreDecisionPlan.Errors.Select(static error => $"runtime-target-ignore-decision-plan: {error}"));
        errors.AddRange(targetIgnoreDecisionRecord.Errors.Select(static error => $"runtime-target-ignore-decision-record: {error}"));
        var productPilotProofRequired = targetCommitClosureRequired
                                        && targetCommitClosure.CommitClosureComplete
                                        && targetResiduePolicy.ResiduePolicyReady
                                        && targetIgnoreDecisionPlan.IgnoreDecisionPlanReady
                                        && targetIgnoreDecisionRecord.DecisionRecordReady
                                        && localDistFreshnessSmoke.LocalDistFreshnessSmokeReady
                                        && localDistHandoff.StableExternalConsumptionReady
                                        && frozenDistTargetReadbackProof.FrozenDistTargetReadbackProofComplete;
        var productPilotProof = productPilotProofRequired
            ? productPilotProofFactory()
            : BuildSkippedProductPilotProof(runtimeInitialized);
        if (productPilotProofRequired)
        {
            errors.AddRange(productPilotProof.Errors.Select(static error => $"runtime-product-pilot-proof: {error}"));
        }

        var assessment = ResolveAssessment(runtimeInitialized, targetAgentBootstrap, localDistFreshnessSmoke, targetDistBindingPlan, localDistHandoff, frozenDistTargetReadbackProof, targetCommitClosure, targetResiduePolicy, targetIgnoreDecisionPlan, targetIgnoreDecisionRecord, productPilotProof, formalPlanning, managedWorkspace, tasks, reviewTaskCount, completedTaskCount, errors);
        var discussionFirstSurface = RuntimeDiscussionFirstSurfacePolicy.IsDiscussionFirstStage(assessment.StageId)
                                     || RuntimeDiscussionFirstSurfacePolicy.IsDiscussionFirstCommand(assessment.NextCommand);
        var recoverableCleanupRequired = managedWorkspace.RecoverableResidueCount > 0;
        var availableActions = BuildAvailableActions(discussionFirstSurface, managedWorkspace);
        var gaps = recoverableCleanupRequired
            ? [.. assessment.Gaps, "recoverable_runtime_residue_present"]
            : assessment.Gaps;
        var summary = recoverableCleanupRequired
            ? $"{assessment.Summary} Recoverable runtime residue remains; run cleanup before treating the repo as clean."
            : assessment.Summary;

        return new RuntimeProductClosurePilotStatusSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            PreviousPhaseDocumentPath = PreviousPhaseDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            OverallPosture = assessment.OverallPosture,
            OperationalState = recoverableCleanupRequired
                ? ControlPlaneResidueContract.RecoverableResidueHealthState
                : ControlPlaneResidueContract.CleanHealthState,
            SafeToStartNewExecution = !managedWorkspace.RecoverableResidueBlocksAutoRun,
            SafeToDiscuss = true,
            SafeToCleanup = recoverableCleanupRequired,
            CurrentStageId = assessment.StageId,
            CurrentStageOrder = assessment.StageOrder,
            CurrentStageStatus = assessment.StageStatus,
            NextCommand = assessment.NextCommand,
            DiscussionFirstSurface = discussionFirstSurface,
            AutoRunAllowed = false,
            RecommendedActionId = null,
            AvailableActions = availableActions,
            ForbiddenAutoActions = discussionFirstSurface
                ? RuntimeDiscussionFirstSurfacePolicy.BuildForbiddenAutoActions()
                : [],
            Summary = summary,
            RuntimeInitialized = runtimeInitialized,
            TargetAgentBootstrapPosture = targetAgentBootstrap.OverallPosture,
            TargetAgentBootstrapRecommendedNextAction = targetAgentBootstrap.RecommendedNextAction,
            TargetAgentBootstrapMissingFiles = targetAgentBootstrap.MissingFiles,
            FormalPlanningState = formalPlanning.FormalPlanningState,
            FormalPlanningPosture = formalPlanning.OverallPosture,
            ManagedWorkspacePosture = managedWorkspace.OverallPosture,
            ActiveLeaseCount = managedWorkspace.ActiveLeases.Count,
            RecoverableCleanupRequired = recoverableCleanupRequired,
            RecoverableResidueCount = managedWorkspace.RecoverableResidueCount,
            HighestRecoverableResidueSeverity = managedWorkspace.HighestRecoverableResidueSeverity,
            RecoverableResidueBlocksAutoRun = managedWorkspace.RecoverableResidueBlocksAutoRun,
            RecoverableCleanupActionId = managedWorkspace.RecoverableCleanupActionId,
            RecoverableCleanupActionMode = managedWorkspace.RecoverableCleanupActionMode,
            RecoverableCleanupSummary = managedWorkspace.RecoverableResidueSummary,
            RecoverableCleanupRecommendedNextAction = managedWorkspace.RecoverableResidueRecommendedNextAction,
            TargetCommitClosurePosture = targetCommitClosure.OverallPosture,
            TargetCommitClosureRecommendedNextAction = targetCommitClosure.RecommendedNextAction,
            TargetGitWorktreeClean = targetCommitClosure.TargetGitWorktreeClean,
            TargetCommitClosureComplete = targetCommitClosure.CommitClosureComplete,
            TargetResiduePolicyPosture = targetResiduePolicy.OverallPosture,
            TargetResiduePolicyReady = targetResiduePolicy.ResiduePolicyReady,
            TargetIgnoreDecisionPlanPosture = targetIgnoreDecisionPlan.OverallPosture,
            TargetIgnoreDecisionPlanReady = targetIgnoreDecisionPlan.IgnoreDecisionPlanReady,
            IgnoreDecisionRequired = targetIgnoreDecisionPlan.IgnoreDecisionRequired,
            TargetIgnoreDecisionRecordPosture = targetIgnoreDecisionRecord.OverallPosture,
            TargetIgnoreDecisionRecordReady = targetIgnoreDecisionRecord.DecisionRecordReady,
            TargetIgnoreDecisionRecordAuditReady = targetIgnoreDecisionRecord.RecordAuditReady,
            TargetIgnoreDecisionRecordCommitReady = targetIgnoreDecisionRecord.DecisionRecordCommitReady,
            MissingIgnoreDecisionEntryCount = targetIgnoreDecisionRecord.MissingDecisionEntryCount,
            InvalidIgnoreDecisionRecordCount = targetIgnoreDecisionRecord.InvalidRecordCount,
            MalformedIgnoreDecisionRecordCount = targetIgnoreDecisionRecord.MalformedRecordCount,
            ConflictingIgnoreDecisionEntryCount = targetIgnoreDecisionRecord.ConflictingDecisionEntryCount,
            UncommittedIgnoreDecisionRecordCount = targetIgnoreDecisionRecord.UncommittedDecisionRecordCount,
            LocalDistFreshnessSmokePosture = localDistFreshnessSmoke.OverallPosture,
            LocalDistFreshnessSmokeRecommendedNextAction = localDistFreshnessSmoke.RecommendedNextAction,
            LocalDistFreshnessSmokeReady = localDistFreshnessSmoke.LocalDistFreshnessSmokeReady,
            TargetDistBindingPlanPosture = targetDistBindingPlan.OverallPosture,
            TargetDistBindingPlanRecommendedNextAction = targetDistBindingPlan.RecommendedNextAction,
            LocalDistHandoffPosture = localDistHandoff.OverallPosture,
            LocalDistHandoffRecommendedNextAction = localDistHandoff.RecommendedNextAction,
            StableExternalConsumptionReady = localDistHandoff.StableExternalConsumptionReady,
            RuntimeRootKind = localDistHandoff.RuntimeRootKind,
            FrozenDistTargetReadbackProofPosture = frozenDistTargetReadbackProof.OverallPosture,
            FrozenDistTargetReadbackProofRecommendedNextAction = frozenDistTargetReadbackProof.RecommendedNextAction,
            FrozenDistTargetReadbackProofComplete = frozenDistTargetReadbackProof.FrozenDistTargetReadbackProofComplete,
            TaskCount = tasks.Length,
            ReviewTaskCount = reviewTaskCount,
            CompletedTaskCount = completedTaskCount,
            StageStatuses = BuildStageStatuses(guide.Steps, assessment),
            Gaps = gaps,
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This status surface does not initialize, plan, issue a workspace, approve review, write back files, or commit git changes.",
                "This status surface does not replace the productized pilot guide; it selects the current stage and next governed command.",
                "This status surface does not claim OS sandboxing, full ACP, full MCP, remote worker orchestration, or automatic packaging.",
            ],
        };
    }

    private static IReadOnlyList<RuntimeInteractionActionSurface> BuildAvailableActions(
        bool discussionFirstSurface,
        RuntimeManagedWorkspaceSurface managedWorkspace)
    {
        var actions = discussionFirstSurface
            ? RuntimeDiscussionFirstSurfacePolicy.BuildSafeMenu().ToList()
            : [];

        if (managedWorkspace.AvailableActions.Count == 0)
        {
            return actions;
        }

        foreach (var action in managedWorkspace.AvailableActions)
        {
            if (actions.Any(existing => string.Equals(existing.ActionId, action.ActionId, StringComparison.Ordinal)))
            {
                continue;
            }

            actions.Add(action);
        }

        return actions;
    }

    private static PilotStatusAssessment ResolveAssessment(
        bool runtimeInitialized,
        RuntimeTargetAgentBootstrapPackSurface targetAgentBootstrap,
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeTargetDistBindingPlanSurface targetDistBindingPlan,
        RuntimeLocalDistHandoffSurface localDistHandoff,
        RuntimeFrozenDistTargetReadbackProofSurface frozenDistTargetReadbackProof,
        RuntimeTargetCommitClosureSurface targetCommitClosure,
        RuntimeTargetResiduePolicySurface targetResiduePolicy,
        RuntimeTargetIgnoreDecisionPlanSurface targetIgnoreDecisionPlan,
        RuntimeTargetIgnoreDecisionRecordSurface targetIgnoreDecisionRecord,
        RuntimeProductPilotProofSurface productPilotProof,
        RuntimeFormalPlanningPostureSurface formalPlanning,
        RuntimeManagedWorkspaceSurface managedWorkspace,
        IReadOnlyList<Carves.Runtime.Domain.Tasks.TaskNode> tasks,
        int reviewTaskCount,
        int completedTaskCount,
        IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            var productPilotProofBlocked = errors.Any(static error => error.StartsWith("runtime-product-pilot-proof:", StringComparison.Ordinal));
            return new PilotStatusAssessment(
                "pilot_status_blocked_by_surface_gaps",
                productPilotProofBlocked ? "product_pilot_proof" : "local_dist_freshness_smoke",
                productPilotProofBlocked ? 25 : 21,
                "blocked",
                "carves inspect runtime-product-closure-pilot-status",
                "Pilot status cannot be trusted until required Runtime documents and dependent surfaces are valid.",
                errors.ToArray());
        }

        if (!runtimeInitialized)
        {
            return new PilotStatusAssessment(
                "pilot_status_blocked_by_runtime_init",
                "attach_target",
                1,
                "blocked",
                "carves init [target-path] --json",
                "The target repo is not yet initialized or attached to CARVES Runtime.",
                ["runtime_not_initialized"]);
        }

        if (targetAgentBootstrap.MissingFiles.Count > 0)
        {
            return new PilotStatusAssessment(
                "pilot_status_target_agent_bootstrap_required",
                "target_agent_bootstrap",
                3,
                "ready",
                "carves agent bootstrap --write",
                "The target repo is initialized but missing agent bootstrap files; materialize them before asking external agents to plan or edit.",
                ["target_agent_bootstrap_missing", .. targetAgentBootstrap.MissingFiles]);
        }

        if (reviewTaskCount > 0)
        {
            var taskId = tasks.First(task => task.Status == DomainTaskStatus.Review).TaskId;
            return new PilotStatusAssessment(
                "pilot_status_review_preflight_required",
                "pre_writeback_audit",
                13,
                "ready",
                $"carves inspect runtime-workspace-mutation-audit {taskId}",
                "A task is in review; inspect mutation audit before approving writeback.",
                []);
        }

        if (completedTaskCount > 0
            && string.Equals(managedWorkspace.OverallPosture, "planning_lineage_closed_no_active_workspace", StringComparison.Ordinal))
        {
            if (targetCommitClosure.CommitClosureComplete)
            {
                if (!targetResiduePolicy.ResiduePolicyReady)
                {
                    return new PilotStatusAssessment(
                        "pilot_status_target_residue_policy_required",
                        "target_residue_policy",
                        18,
                        "ready",
                        "carves pilot residue --json",
                        targetResiduePolicy.Summary,
                        targetResiduePolicy.Gaps.ToArray());
                }

                if (!targetIgnoreDecisionPlan.IgnoreDecisionPlanReady)
                {
                    return new PilotStatusAssessment(
                        "pilot_status_target_ignore_decision_plan_required",
                        "target_ignore_decision_plan",
                        19,
                        "ready",
                        "carves pilot ignore-plan --json",
                        targetIgnoreDecisionPlan.Summary,
                        targetIgnoreDecisionPlan.Gaps.ToArray());
                }

                if (!targetIgnoreDecisionRecord.DecisionRecordReady)
                {
                    return new PilotStatusAssessment(
                        "pilot_status_target_ignore_decision_record_required",
                        "target_ignore_decision_record",
                        20,
                        "ready",
                        "carves pilot ignore-record --json",
                        targetIgnoreDecisionRecord.Summary,
                        targetIgnoreDecisionRecord.Gaps.ToArray());
                }

                if (!localDistFreshnessSmoke.LocalDistFreshnessSmokeReady)
                {
                    return new PilotStatusAssessment(
                        "pilot_status_local_dist_freshness_smoke_required",
                        "local_dist_freshness_smoke",
                        21,
                        "ready",
                        "carves pilot dist-smoke --json",
                        localDistFreshnessSmoke.Summary,
                        localDistFreshnessSmoke.Gaps.ToArray());
                }

                if (!localDistHandoff.StableExternalConsumptionReady)
                {
                    return new PilotStatusAssessment(
                        "pilot_status_target_dist_binding_required",
                        "target_dist_binding_plan",
                        22,
                        "ready",
                        "carves pilot dist-binding --json",
                        targetDistBindingPlan.Summary,
                        targetDistBindingPlan.Gaps.ToArray());
                }

                if (!frozenDistTargetReadbackProof.FrozenDistTargetReadbackProofComplete)
                {
                    return new PilotStatusAssessment(
                        "pilot_status_frozen_dist_target_readback_proof_required",
                        "frozen_dist_target_readback_proof",
                        24,
                        "ready",
                        "carves pilot target-proof --json",
                        frozenDistTargetReadbackProof.Summary,
                        frozenDistTargetReadbackProof.Gaps.ToArray());
                }

                if (productPilotProof.ProductPilotProofComplete)
                {
                    return new PilotStatusAssessment(
                        "pilot_status_product_pilot_proof_complete",
                        "ready_for_new_intent",
                        26,
                        "ready",
                        "carves discuss context",
                        "Product pilot proof is complete. Stay in normal discussion, clarify the next project purpose and whether new engineering work is actually requested, and run intent draft --persist only after the operator/user gives a bounded scope.",
                        []);
                }

                return new PilotStatusAssessment(
                    "pilot_status_product_pilot_proof_required",
                    "product_pilot_proof",
                    25,
                    "ready",
                    "carves pilot proof --json",
                    string.IsNullOrWhiteSpace(productPilotProof.Summary)
                        ? "Runtime writeback, target commit closure, local dist handoff, and frozen dist target readback proof are complete; final product pilot proof readback is ready."
                        : productPilotProof.Summary,
                    productPilotProof.Gaps.ToArray());
            }

            return new PilotStatusAssessment(
                "pilot_status_writeback_complete_commit_plan_required",
                "target_commit_plan",
                16,
                "ready",
                "carves pilot commit-plan",
                targetCommitClosure.Summary,
                targetCommitClosure.Gaps.ToArray());
        }

        if (managedWorkspace.ActiveLeases.Count > 0)
        {
            var taskId = managedWorkspace.ActiveLeases[0].TaskId;
            return new PilotStatusAssessment(
                "pilot_status_workspace_output_required",
                "workspace_submit",
                12,
                "ready",
                $"carves plan submit-workspace {taskId} \"submitted managed workspace result\"",
                "An active managed workspace lease exists; keep changes inside the lease and submit through Runtime review.",
                []);
        }

        if (tasks.Count > 0
            && string.Equals(managedWorkspace.OverallPosture, "task_bound_workspace_ready_to_issue", StringComparison.Ordinal))
        {
            var taskId = managedWorkspace.BoundTaskIds.FirstOrDefault()
                         ?? tasks.FirstOrDefault(task => !IsTerminal(task.Status))?.TaskId
                         ?? tasks[0].TaskId;
            return new PilotStatusAssessment(
                "pilot_status_workspace_issue_required",
                "workspace_issue",
                11,
                "ready",
                $"carves plan issue-workspace {taskId}",
                "Task truth is available and a managed workspace can be issued for Mode D execution.",
                []);
        }

        if (tasks.Count == 0
            && (string.Equals(formalPlanning.FormalPlanningState, "planning", StringComparison.Ordinal)
                || string.Equals(formalPlanning.FormalPlanningState, "plan_bound", StringComparison.Ordinal)
                || string.Equals(formalPlanning.ActivePlanningCardFillState, "ready_to_export", StringComparison.Ordinal)))
        {
            return new PilotStatusAssessment(
                "pilot_status_card_taskgraph_writeback_required",
                "card_taskgraph_writeback",
                10,
                "ready",
                "carves plan export-card <json-path>",
                "Formal planning is active or bound, but no task truth exists yet.",
                []);
        }

        if (formalPlanning.ActivePlanningSlotCanInitialize
            || string.Equals(formalPlanning.FormalPlanningState, "plan_init_required", StringComparison.Ordinal))
        {
            return new PilotStatusAssessment(
                "pilot_status_formal_plan_init_required",
                "formal_plan_init",
                9,
                "ready",
                formalPlanning.FormalPlanningEntryCommand,
                "Intent capture is ready to become one active formal planning card.",
                formalPlanning.MissingPrerequisites.ToArray());
        }

        if (string.Equals(formalPlanning.FormalPlanningState, "discuss", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(formalPlanning.FormalPlanningState))
        {
            return new PilotStatusAssessment(
                "pilot_status_intent_capture_required",
                "intent_capture",
                8,
                "ready",
                "carves discuss context",
                "Runtime is attached but no bounded project purpose has been confirmed yet. Stay in normal discussion, ask what the project is for and whether engineering work is actually requested, and run intent draft --persist only after the operator/user gives a bounded scope.",
                []);
        }

        var fallbackCommand = string.IsNullOrWhiteSpace(formalPlanning.ModeExecutionEntryRecommendedNextCommand)
            ? "carves pilot guide"
            : formalPlanning.ModeExecutionEntryRecommendedNextCommand;
        return new PilotStatusAssessment(
            "pilot_status_needs_operator_review",
            "handoff_contract",
            7,
            "ready",
            fallbackCommand,
            "Pilot status could not map the current repo state to a later deterministic productized pilot stage.",
            formalPlanning.MissingPrerequisites.ToArray());
    }

    private static RuntimeProductClosurePilotStatusStageSurface[] BuildStageStatuses(
        IReadOnlyList<RuntimeProductClosurePilotGuideStepSurface> steps,
        PilotStatusAssessment assessment)
    {
        return steps
            .OrderBy(step => step.Order)
            .Select(step => new RuntimeProductClosurePilotStatusStageSurface
            {
                Order = step.Order,
                StageId = step.StageId,
                Command = step.Command,
                State = step.Order < assessment.StageOrder
                    ? "satisfied_or_not_required"
                    : step.Order == assessment.StageOrder
                        ? assessment.StageStatus
                        : "pending",
                Summary = step.Order == assessment.StageOrder
                    ? assessment.Summary
                    : step.Purpose,
            })
            .ToArray();
    }

    private static bool IsTerminal(DomainTaskStatus status)
    {
        return status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Discarded or DomainTaskStatus.Superseded;
    }

    private static RuntimeTargetCommitClosureSurface BuildSkippedTargetCommitClosure(bool runtimeInitialized)
    {
        return new RuntimeTargetCommitClosureSurface
        {
            OverallPosture = "target_commit_closure_not_required",
            RuntimeInitialized = runtimeInitialized,
            GitRepositoryDetected = false,
            TargetGitWorktreeClean = false,
            CommitClosureComplete = false,
            Summary = "Target commit closure is not evaluated until Runtime writeback is closed with at least one completed task and no active managed workspace.",
            RecommendedNextAction = "carves pilot status --json",
        };
    }

    private static RuntimeTargetResiduePolicySurface BuildSkippedTargetResiduePolicy(bool runtimeInitialized)
    {
        return new RuntimeTargetResiduePolicySurface
        {
            OverallPosture = "target_residue_policy_not_required",
            RuntimeInitialized = runtimeInitialized,
            GitRepositoryDetected = false,
            TargetGitWorktreeClean = false,
            CommitClosureComplete = false,
            ResiduePolicyReady = false,
            ProductProofCanRemainComplete = false,
            Summary = "Target residue policy is not evaluated until Runtime writeback is closed with at least one completed task and no active managed workspace.",
            RecommendedNextAction = "carves pilot status --json",
        };
    }

    private static RuntimeTargetIgnoreDecisionPlanSurface BuildSkippedTargetIgnoreDecisionPlan(bool runtimeInitialized)
    {
        return new RuntimeTargetIgnoreDecisionPlanSurface
        {
            OverallPosture = "target_ignore_decision_plan_not_required",
            CommitClosureComplete = false,
            ResiduePolicyReady = false,
            ProductProofCanRemainComplete = false,
            IgnoreDecisionPlanReady = false,
            IgnoreDecisionRequired = false,
            Summary = runtimeInitialized
                ? "Target ignore decision plan is not evaluated until target residue policy is required."
                : "Target ignore decision plan waits for target runtime initialization.",
            RecommendedNextAction = "carves pilot status --json",
        };
    }

    private static RuntimeTargetIgnoreDecisionRecordSurface BuildSkippedTargetIgnoreDecisionRecord(bool runtimeInitialized)
    {
        return new RuntimeTargetIgnoreDecisionRecordSurface
        {
            OverallPosture = "target_ignore_decision_record_not_required",
            IgnoreDecisionPlanReady = false,
            IgnoreDecisionRequired = false,
            DecisionRecordReady = false,
            DecisionRecordCommitReady = true,
            ProductProofCanRemainComplete = false,
            Summary = runtimeInitialized
                ? "Target ignore decision record is not evaluated until target ignore decision plan is required."
                : "Target ignore decision record waits for target runtime initialization.",
            RecommendedNextAction = "carves pilot status --json",
        };
    }

    private static RuntimeProductPilotProofSurface BuildSkippedProductPilotProof(bool runtimeInitialized)
    {
        return new RuntimeProductPilotProofSurface
        {
            OverallPosture = "product_pilot_proof_not_required",
            RuntimeInitialized = runtimeInitialized,
            ProductPilotProofComplete = false,
            Summary = "Product pilot proof is not evaluated until writeback closure, target commit closure, dist handoff, and frozen target proof are complete.",
            RecommendedNextAction = "carves pilot status --json",
        };
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private sealed record PilotStatusAssessment(
        string OverallPosture,
        string StageId,
        int StageOrder,
        string StageStatus,
        string NextCommand,
        string Summary,
        IReadOnlyList<string> Gaps);
}
