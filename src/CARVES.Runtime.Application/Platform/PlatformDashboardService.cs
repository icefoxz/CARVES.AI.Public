using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class PlatformDashboardService
{
    private readonly ControlPlanePaths paths;
    private readonly OperatorApiService operatorApiService;
    private readonly OperationalSummaryService operationalSummaryService;
    private readonly GovernanceReportingService governanceReportingService;
    private readonly Carves.Runtime.Application.Planning.PlanningDraftService planningDraftService;
    private readonly Carves.Runtime.Application.Planning.DispatchProjectionService dispatchProjectionService;
    private readonly Carves.Runtime.Application.TaskGraph.TaskGraphService taskGraphService;
    private readonly Carves.Runtime.Application.Orchestration.DevLoopService devLoopService;
    private readonly ExecutionRunService executionRunService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly int maxParallelTasks;
    private readonly Func<RuntimeSessionGatewayGovernanceAssistSurface> runtimeSessionGatewayGovernanceAssistFactory;
    private readonly Func<RuntimeAcceptanceContractIngressPolicySurface> runtimeAcceptanceContractIngressPolicyFactory;
    private readonly Func<RuntimeAgentWorkingModesSurface> runtimeAgentWorkingModesFactory;
    private readonly Func<RuntimeFormalPlanningPostureSurface> runtimeFormalPlanningPostureFactory;
    private readonly Func<RuntimeVendorNativeAccelerationSurface> runtimeVendorNativeAccelerationFactory;

    public PlatformDashboardService(
        ControlPlanePaths paths,
        OperatorApiService operatorApiService,
        OperationalSummaryService operationalSummaryService,
        GovernanceReportingService governanceReportingService,
        Carves.Runtime.Application.Planning.PlanningDraftService planningDraftService,
        Carves.Runtime.Application.Planning.DispatchProjectionService dispatchProjectionService,
        Carves.Runtime.Application.TaskGraph.TaskGraphService taskGraphService,
        Carves.Runtime.Application.Orchestration.DevLoopService devLoopService,
        ExecutionRunService executionRunService,
        IRuntimeArtifactRepository artifactRepository,
        int maxParallelTasks,
        Func<RuntimeSessionGatewayGovernanceAssistSurface> runtimeSessionGatewayGovernanceAssistFactory,
        Func<RuntimeAcceptanceContractIngressPolicySurface> runtimeAcceptanceContractIngressPolicyFactory,
        Func<RuntimeAgentWorkingModesSurface> runtimeAgentWorkingModesFactory,
        Func<RuntimeFormalPlanningPostureSurface> runtimeFormalPlanningPostureFactory,
        Func<RuntimeVendorNativeAccelerationSurface> runtimeVendorNativeAccelerationFactory)
    {
        this.paths = paths;
        this.operatorApiService = operatorApiService;
        this.operationalSummaryService = operationalSummaryService;
        this.governanceReportingService = governanceReportingService;
        this.planningDraftService = planningDraftService;
        this.dispatchProjectionService = dispatchProjectionService;
        this.taskGraphService = taskGraphService;
        this.devLoopService = devLoopService;
        this.executionRunService = executionRunService;
        this.artifactRepository = artifactRepository;
        this.maxParallelTasks = Math.Max(1, maxParallelTasks);
        this.runtimeSessionGatewayGovernanceAssistFactory = runtimeSessionGatewayGovernanceAssistFactory;
        this.runtimeAcceptanceContractIngressPolicyFactory = runtimeAcceptanceContractIngressPolicyFactory;
        this.runtimeAgentWorkingModesFactory = runtimeAgentWorkingModesFactory;
        this.runtimeFormalPlanningPostureFactory = runtimeFormalPlanningPostureFactory;
        this.runtimeVendorNativeAccelerationFactory = runtimeVendorNativeAccelerationFactory;
    }

    public RuntimeSessionGatewayGovernanceAssistSurface BuildSessionGatewayGovernanceAssistSurface()
    {
        return runtimeSessionGatewayGovernanceAssistFactory();
    }

    public RuntimeAcceptanceContractIngressPolicySurface BuildAcceptanceContractIngressPolicySurface()
    {
        return runtimeAcceptanceContractIngressPolicyFactory();
    }

    public RuntimeAgentWorkingModesSurface BuildAgentWorkingModesSurface()
    {
        return runtimeAgentWorkingModesFactory();
    }

    public RuntimeFormalPlanningPostureSurface BuildFormalPlanningPostureSurface()
    {
        return runtimeFormalPlanningPostureFactory();
    }

    public RuntimeVendorNativeAccelerationSurface BuildVendorNativeAccelerationSurface()
    {
        return runtimeVendorNativeAccelerationFactory();
    }

    public IReadOnlyList<string> Render(string? repoId = null)
    {
        var status = operatorApiService.GetPlatformStatus();
        var pendingPermissions = operatorApiService.GetPendingWorkerPermissionRequests();
        var incidents = operatorApiService.GetRuntimeIncidents();
        var actorSessions = operatorApiService.GetActorSessions();
        var operatorOsEvents = operatorApiService.GetOperatorOsEvents();
        var operational = operationalSummaryService.Build(refreshProviderHealth: false);
        var governanceReport = governanceReportingService.Build();
        var graph = taskGraphService.Load();
        var dispatch = dispatchProjectionService.Build(graph, devLoopService.GetSession(), maxParallelTasks);
        var cardDrafts = planningDraftService.ListCardDrafts();
        var plannerEmergence = new PlannerEmergenceService(paths, taskGraphService, executionRunService).BuildProjection();
        var sessionGatewayGovernanceAssist = BuildSessionGatewayGovernanceAssistSurface();
        var acceptanceContractIngressPolicy = BuildAcceptanceContractIngressPolicySurface();
        var agentWorkingModes = BuildAgentWorkingModesSurface();
        var formalPlanningPosture = BuildFormalPlanningPostureSurface();
        var vendorNativeAcceleration = BuildVendorNativeAccelerationSurface();
        var sessionGatewayTopPressure = sessionGatewayGovernanceAssist.ChangePressures.FirstOrDefault();
        var sessionGatewayTopCandidate = sessionGatewayGovernanceAssist.DecompositionCandidates.FirstOrDefault();
        var hostSession = new HostSessionService(paths).Load();
        var runtimeManifest = new RuntimeManifestService(paths).Load();
        var runtimeHealth = new RuntimeHealthCheckService(paths, taskGraphService).Evaluate();
        var projectionHealth = new MarkdownProjectionHealthService(paths).Load();
        var realityCounts = graph.ListTasks()
            .GroupBy(ResolveRealityStatus, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var nextReality = string.IsNullOrWhiteSpace(dispatch.NextTaskId) || !graph.Tasks.TryGetValue(dispatch.NextTaskId, out var nextTask)
            ? "(none)"
            : BuildRealitySummary(nextTask);
        var runDrilldown = taskGraphService.Load().ListTasks()
            .SelectMany(task => executionRunService.ListRuns(task.TaskId))
            .OrderByDescending(run => run.EndedAtUtc ?? run.StartedAtUtc ?? run.CreatedAtUtc)
            .Take(5)
            .ToArray();
        var lines = new List<string>
        {
            "Platform Overview",
            $"Repos: {status.RegisteredRepoCount}",
            $"Active sessions: {status.ActiveSessionCount}",
            $"Running instances: {status.RunningInstanceCount}",
            $"Open opportunities: {status.OpenOpportunityCount}",
            $"Worker nodes: {status.WorkerNodeCount}",
            $"Active leases: {status.ActiveLeaseCount}",
            $"Providers: {status.ProviderCount}",
            $"Stale projections: {status.StaleProjectionCount}",
            $"Actor sessions: {actorSessions.Count}",
            $"Pending permission requests: {pendingPermissions.Count}",
            $"Recent incidents: {incidents.Count}",
            $"Operator OS events: {operatorOsEvents.Count}",
            $"Operational actionability: {operational.Actionability} ({operational.ActionabilityReason})",
            $"Operational summary: {operational.ActionabilitySummary}",
            $"Operational next action: {operational.RecommendedNextAction}",
            $"Agent working mode: {agentWorkingModes.CurrentMode}",
            $"External agent recommended mode: {agentWorkingModes.ExternalAgentRecommendedMode}",
            $"External agent recommendation posture: {agentWorkingModes.ExternalAgentRecommendationPosture}",
            $"External agent constraint tier: {agentWorkingModes.ExternalAgentConstraintTier}",
            $"External agent stronger-mode blockers: {agentWorkingModes.ExternalAgentStrongerModeBlockerCount}",
            $"External agent first stronger-mode blocker: {agentWorkingModes.ExternalAgentFirstStrongerModeBlockerId ?? "(none)"}",
            $"External agent first stronger-mode blocker action: {agentWorkingModes.ExternalAgentFirstStrongerModeBlockerRequiredAction ?? "(none)"}",
            $"Mode E activation: {agentWorkingModes.ModeEOperationalActivationState}",
            $"Mode E activation task: {agentWorkingModes.ModeEActivationTaskId ?? "(none)"}",
            $"Mode E activation command: {agentWorkingModes.ModeEActivationCommands.FirstOrDefault() ?? "(none)"}",
            $"Mode E result return channel: {agentWorkingModes.ModeEActivationResultReturnChannel ?? "(none)"}",
            $"Mode E activation next action: {agentWorkingModes.ModeEActivationRecommendedNextAction}",
            $"Mode E activation blocking checks: {agentWorkingModes.ModeEActivationBlockingCheckCount}",
            $"Mode E activation first blocker: {agentWorkingModes.ModeEActivationFirstBlockingCheckId ?? "(none)"}",
            $"Mode E activation first blocker action: {agentWorkingModes.ModeEActivationFirstBlockingCheckRequiredAction ?? "(none)"}",
            $"Mode E activation playbook: {agentWorkingModes.ModeEActivationPlaybookSummary}",
            $"Mode E activation playbook steps: {agentWorkingModes.ModeEActivationPlaybookStepCount}",
            $"Mode E activation first playbook command: {agentWorkingModes.ModeEActivationFirstPlaybookStepCommand ?? "(none)"}",
            $"Planning coupling: {agentWorkingModes.PlanningCouplingPosture}",
            $"Formal planning posture: {formalPlanningPosture.OverallPosture}",
            $"Formal planning entry trigger: {formalPlanningPosture.FormalPlanningEntryTriggerState}",
            $"Formal planning entry command: {formalPlanningPosture.FormalPlanningEntryCommand}",
            $"Formal planning entry next action: {formalPlanningPosture.FormalPlanningEntryRecommendedNextAction}",
            $"Active planning slot state: {formalPlanningPosture.ActivePlanningSlotState}",
            $"Active planning slot conflict reason: {(string.IsNullOrWhiteSpace(formalPlanningPosture.ActivePlanningSlotConflictReason) ? "(none)" : formalPlanningPosture.ActivePlanningSlotConflictReason)}",
            $"Active planning slot remediation: {formalPlanningPosture.ActivePlanningSlotRemediationAction}",
            $"Planning card invariant state: {formalPlanningPosture.PlanningCardInvariantState}",
            $"Planning card invariant violations: {formalPlanningPosture.PlanningCardInvariantViolationCount}",
            $"Planning card invariant remediation: {formalPlanningPosture.PlanningCardInvariantRemediationAction}",
            $"Active planning card fill state: {formalPlanningPosture.ActivePlanningCardFillState}",
            $"Active planning card fill missing required fields: {formalPlanningPosture.ActivePlanningCardFillMissingRequiredFieldCount}",
            $"Active planning card fill next action: {formalPlanningPosture.ActivePlanningCardFillRecommendedNextAction}",
            $"Active plan handle: {formalPlanningPosture.PlanHandle ?? "(none)"}",
            $"Active planning card: {formalPlanningPosture.PlanningCardId ?? "(none)"}",
            $"Managed workspace posture: {formalPlanningPosture.ManagedWorkspacePosture}",
            $"Vendor-native acceleration: {vendorNativeAcceleration.OverallPosture}",
            $"Codex reinforcement: {vendorNativeAcceleration.CodexReinforcementState}",
            $"Claude reinforcement: {vendorNativeAcceleration.ClaudeReinforcementState}",
            $"Session Gateway assist: {sessionGatewayGovernanceAssist.OverallPosture}",
            $"Session Gateway assist next action: {sessionGatewayGovernanceAssist.RecommendedNextAction}",
            $"Acceptance contract ingress policy: {acceptanceContractIngressPolicy.PolicySummary}",
            $"Projection writeback: {projectionHealth.State} ({projectionHealth.Summary})",
            $"Dispatch state: {dispatch.State} ({dispatchProjectionService.DescribeIdleReason(dispatch.IdleReason)})",
            $"Acceptance contract gaps: {dispatch.AcceptanceContractGapCount}",
            $"Plan-required execution gaps: {dispatch.PlanRequiredBlockCount}",
            $"Managed workspace gaps: {dispatch.WorkspaceRequiredBlockCount}",
            $"Mode C/D entry first blocker: {dispatch.FirstBlockingCheckId ?? "(none)"}",
            $"Mode C/D entry first blocker command: {dispatch.FirstBlockingCheckRequiredCommand ?? "(none)"}",
            $"Mode C/D entry next command: {dispatch.RecommendedNextCommand ?? "(none)"}",
            $"Dispatch next task: {dispatch.NextTaskId ?? "(none)"}",
            $"Host session: {hostSession?.SessionId ?? "(none)"} [{hostSession?.Status.ToString() ?? "none"}]",
            $"Host control: {hostSession?.ControlState.ToString().ToLowerInvariant() ?? "running"} ({hostSession?.LastControlReason ?? "Resident host session started."})",
            $"Attached repos in host session: {hostSession?.AttachedRepos.Count ?? 0}",
            $"Runtime manifest: {(runtimeManifest is null ? "(missing)" : $"{runtimeManifest.RepoId} / {runtimeManifest.State.ToString().ToLowerInvariant()} / {runtimeManifest.RuntimeStatus}")}",
            $"Runtime health: {runtimeHealth.State.ToString().ToLowerInvariant()} ({runtimeHealth.SuggestedAction})",
            $"Replan-required tasks: {plannerEmergence.ReplanRequiredTaskCount}",
            $"Draft suggested tasks: {plannerEmergence.DraftSuggestedTaskCount}",
            $"Planning signals: {plannerEmergence.PlanningSignalCount}",
            $"Execution memory records: {plannerEmergence.ExecutionMemoryRecordCount}",
            string.Empty,
            "Repo Runtime View",
        };

        var repo = string.IsNullOrWhiteSpace(repoId)
            ? status.Repos.FirstOrDefault()
            : status.Repos.FirstOrDefault(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal));

        if (repo is null)
        {
            lines.Add("(no registered repo)");
            return lines;
        }

        lines.Add($"Repo: {repo.RepoId}");
        lines.Add($"Stage: {repo.Stage}");
        lines.Add($"Runtime status: {repo.RuntimeStatus}");
        lines.Add($"Runtime actionability: {repo.Actionability}");
        lines.Add($"Operator actionability: {operational.Actionability} ({operational.ActionabilityReason})");
        lines.Add($"Open tasks: {repo.OpenTasks}");
        lines.Add($"Review tasks: {repo.ReviewTasks}");
        lines.Add($"Open opportunities: {repo.OpenOpportunities}");
        lines.Add($"Truth: {repo.TruthSource} / {repo.ProjectionFreshness} / {repo.ProjectionOutcome}");
        lines.Add($"Gateway: {repo.GatewayMode} / {repo.GatewayHealth}");
        lines.Add($"Last scheduling reason: {repo.LastSchedulingReason ?? "(none)"}");
        WorkerSelectionDecision? workerSelection = null;
        string? workerSelectionError = null;
        try
        {
            workerSelection = operatorApiService.GetWorkerSelection(repo.RepoId, null);
        }
        catch (Exception exception)
        {
            // Keep the dashboard readable even when routing state is temporarily incomplete.
            workerSelectionError = exception.Message;
        }

        lines.Add(workerSelection is null
            ? "Worker selection: (unavailable)/(none)"
            : $"Worker selection: {(workerSelection.Allowed ? workerSelection.SelectedBackendId : "(none)")}/{workerSelection.RequestedTrustProfileId}");
        lines.Add(workerSelection is null
            ? $"Worker selection reason: unavailable ({workerSelectionError ?? "worker selection decision unavailable"})"
            : $"Worker selection reason: {workerSelection.Summary}");
        lines.Add(workerSelection is null
            ? "Routing profile: (unavailable) -> (unavailable)"
            : $"Routing profile: {workerSelection.ActiveRoutingProfileId ?? "(none)"} -> {workerSelection.SelectedRoutingProfileId ?? "(none)"}");
        lines.Add(workerSelection is null
            ? "Routing route: unavailable (worker selection decision unavailable)"
            : $"Routing route: {workerSelection.RouteSource} ({workerSelection.RouteReason})");
        lines.Add($"Pending permission requests (global): {pendingPermissions.Count}");
        lines.Add($"Provider health issues: actionable={operational.ProviderHealthIssueCount}; optional={operational.OptionalProviderHealthIssueCount}; disabled={operational.DisabledProviderCount}");
        lines.Add($"Projection writeback: {projectionHealth.State} ({projectionHealth.Summary})");
        lines.Add($"Host control actions: pause=`host pause <reason...>` resume=`host resume [reason...]` stop=`host stop <reason...>`");
        lines.Add($"Actor sessions (global): {actorSessions.Count}");
        lines.Add($"Card drafts: {cardDrafts.Count(item => item.Status == CardLifecycleState.Draft)}");
        lines.Add($"Agent working mode: {agentWorkingModes.CurrentMode}");
        lines.Add($"External agent recommended mode: {agentWorkingModes.ExternalAgentRecommendedMode}");
        lines.Add($"External agent recommendation posture: {agentWorkingModes.ExternalAgentRecommendationPosture}");
        lines.Add($"External agent constraint tier: {agentWorkingModes.ExternalAgentConstraintTier}");
        lines.Add($"External agent stronger-mode blockers: {agentWorkingModes.ExternalAgentStrongerModeBlockerCount}");
        lines.Add($"External agent first stronger-mode blocker: {agentWorkingModes.ExternalAgentFirstStrongerModeBlockerId ?? "(none)"}");
        lines.Add($"External agent first stronger-mode blocker action: {agentWorkingModes.ExternalAgentFirstStrongerModeBlockerRequiredAction ?? "(none)"}");
        lines.Add($"Mode E activation: {agentWorkingModes.ModeEOperationalActivationState}");
        lines.Add($"Mode E activation task: {agentWorkingModes.ModeEActivationTaskId ?? "(none)"}");
        lines.Add($"Mode E activation command: {agentWorkingModes.ModeEActivationCommands.FirstOrDefault() ?? "(none)"}");
        lines.Add($"Mode E result return channel: {agentWorkingModes.ModeEActivationResultReturnChannel ?? "(none)"}");
        lines.Add($"Mode E activation next action: {agentWorkingModes.ModeEActivationRecommendedNextAction}");
        lines.Add($"Mode E activation blocking checks: {agentWorkingModes.ModeEActivationBlockingCheckCount}");
        lines.Add($"Mode E activation first blocker: {agentWorkingModes.ModeEActivationFirstBlockingCheckId ?? "(none)"}");
        lines.Add($"Mode E activation first blocker action: {agentWorkingModes.ModeEActivationFirstBlockingCheckRequiredAction ?? "(none)"}");
        lines.Add($"Mode E activation playbook: {agentWorkingModes.ModeEActivationPlaybookSummary}");
        lines.Add($"Mode E activation playbook steps: {agentWorkingModes.ModeEActivationPlaybookStepCount}");
        lines.Add($"Mode E activation first playbook command: {agentWorkingModes.ModeEActivationFirstPlaybookStepCommand ?? "(none)"}");
        lines.Add($"Planning coupling: {agentWorkingModes.PlanningCouplingPosture}");
        lines.Add($"Formal planning posture: {formalPlanningPosture.OverallPosture}");
        lines.Add($"Formal planning entry trigger: {formalPlanningPosture.FormalPlanningEntryTriggerState}");
        lines.Add($"Formal planning entry command: {formalPlanningPosture.FormalPlanningEntryCommand}");
        lines.Add($"Formal planning entry next action: {formalPlanningPosture.FormalPlanningEntryRecommendedNextAction}");
        lines.Add($"Active planning slot state: {formalPlanningPosture.ActivePlanningSlotState}");
        lines.Add($"Active planning slot conflict reason: {(string.IsNullOrWhiteSpace(formalPlanningPosture.ActivePlanningSlotConflictReason) ? "(none)" : formalPlanningPosture.ActivePlanningSlotConflictReason)}");
        lines.Add($"Active planning slot remediation: {formalPlanningPosture.ActivePlanningSlotRemediationAction}");
        lines.Add($"Planning card invariant state: {formalPlanningPosture.PlanningCardInvariantState}");
        lines.Add($"Planning card invariant violations: {formalPlanningPosture.PlanningCardInvariantViolationCount}");
        lines.Add($"Planning card invariant remediation: {formalPlanningPosture.PlanningCardInvariantRemediationAction}");
        lines.Add($"Active planning card fill state: {formalPlanningPosture.ActivePlanningCardFillState}");
        lines.Add($"Active planning card fill missing required fields: {formalPlanningPosture.ActivePlanningCardFillMissingRequiredFieldCount}");
        lines.Add($"Active planning card fill next action: {formalPlanningPosture.ActivePlanningCardFillRecommendedNextAction}");
        lines.Add($"Active plan handle: {formalPlanningPosture.PlanHandle ?? "(none)"}");
        lines.Add($"Active planning card: {formalPlanningPosture.PlanningCardId ?? "(none)"}");
        lines.Add($"Managed workspace posture: {formalPlanningPosture.ManagedWorkspacePosture}");
        lines.Add($"Vendor-native acceleration: {vendorNativeAcceleration.OverallPosture}");
        lines.Add($"Codex reinforcement: {vendorNativeAcceleration.CodexReinforcementState}");
        lines.Add($"Claude reinforcement: {vendorNativeAcceleration.ClaudeReinforcementState}");
        lines.Add($"Approved cards: {graph.Cards.Count}");
        lines.Add($"Dispatchable: {dispatch.State}");
        lines.Add($"Acceptance contract gaps: {dispatch.AcceptanceContractGapCount}");
        lines.Add($"Plan-required execution gaps: {dispatch.PlanRequiredBlockCount}");
        lines.Add($"Managed workspace gaps: {dispatch.WorkspaceRequiredBlockCount}");
        lines.Add($"Mode C/D entry first blocker: {dispatch.FirstBlockingCheckId ?? "(none)"}");
        lines.Add($"Mode C/D entry first blocker command: {dispatch.FirstBlockingCheckRequiredCommand ?? "(none)"}");
        lines.Add($"Mode C/D entry next command: {dispatch.RecommendedNextCommand ?? "(none)"}");
        lines.Add($"Idle reason: {dispatchProjectionService.DescribeIdleReason(dispatch.IdleReason)}");
        lines.Add($"Runtime manifest health: {runtimeManifest?.State.ToString().ToLowerInvariant() ?? "missing"}");
        lines.Add($"Runtime health check: {runtimeHealth.State.ToString().ToLowerInvariant()} ({runtimeHealth.Summary})");
        lines.Add($"Reality gradient: solid={GetRealityCount(realityCounts, "solid")}; proto={GetRealityCount(realityCounts, "proto")}; ghost={GetRealityCount(realityCounts, "ghost")}");
        lines.Add($"Next task reality: {nextReality}");
        lines.Add(string.Empty);
        lines.Add("Attached Repos");
        foreach (var attached in status.Repos.OrderBy(item => item.RepoId, StringComparer.Ordinal).Take(8))
        {
            var manifest = new RuntimeManifestService(ControlPlanePaths.FromRepoRoot(attached.RepoPath)).Load();
            lines.Add($"- {attached.RepoId}: runtime={attached.RuntimeStatus}; actionability={attached.Actionability}; branch={manifest?.ActiveBranch ?? "(unknown)"}; health={manifest?.State.ToString().ToLowerInvariant() ?? "unknown"}");
        }
        lines.Add(string.Empty);
        lines.Add("Operational Summary");
        lines.Add($"- Pending approvals: {operational.PendingApprovalCount}");
        lines.Add($"- Blocked tasks: {operational.BlockedTaskCount}");
        lines.Add($"- Dispatch state: {dispatch.State}");
        lines.Add($"- Acceptance contract gaps: {dispatch.AcceptanceContractGapCount}");
        lines.Add($"- Plan-required execution gaps: {dispatch.PlanRequiredBlockCount}");
        lines.Add($"- Managed workspace gaps: {dispatch.WorkspaceRequiredBlockCount}");
        lines.Add($"- Mode C/D entry first blocker: {dispatch.FirstBlockingCheckId ?? "(none)"}");
        lines.Add($"- Mode C/D entry first blocker command: {dispatch.FirstBlockingCheckRequiredCommand ?? "(none)"}");
        lines.Add($"- Mode C/D entry next command: {dispatch.RecommendedNextCommand ?? "(none)"}");
        lines.Add($"- Idle reason: {dispatchProjectionService.DescribeIdleReason(dispatch.IdleReason)}");
        lines.Add($"- Auto continue on approve: {dispatch.AutoContinueOnApprove}");
        lines.Add($"- Unhealthy providers: actionable={operational.ProviderHealthIssueCount}; optional={operational.OptionalProviderHealthIssueCount}; disabled={operational.DisabledProviderCount}");
        lines.Add($"- Recent incidents: {operational.RecentIncidentCount}");
        lines.Add($"- Replan-required tasks: {plannerEmergence.ReplanRequiredTaskCount}");
        lines.Add($"- Draft suggested tasks: {plannerEmergence.DraftSuggestedTaskCount}");
        lines.Add($"- Actionability summary: {operational.ActionabilitySummary}");
        lines.Add($"- Agent working mode: {agentWorkingModes.CurrentMode}");
        lines.Add($"- External agent recommended mode: {agentWorkingModes.ExternalAgentRecommendedMode}");
        lines.Add($"- External agent recommendation posture: {agentWorkingModes.ExternalAgentRecommendationPosture}");
        lines.Add($"- External agent constraint tier: {agentWorkingModes.ExternalAgentConstraintTier}");
        lines.Add($"- External agent stronger-mode blockers: {agentWorkingModes.ExternalAgentStrongerModeBlockerCount}");
        lines.Add($"- External agent first stronger-mode blocker: {agentWorkingModes.ExternalAgentFirstStrongerModeBlockerId ?? "(none)"}");
        lines.Add($"- External agent first stronger-mode blocker action: {agentWorkingModes.ExternalAgentFirstStrongerModeBlockerRequiredAction ?? "(none)"}");
        lines.Add($"- Mode E activation: {agentWorkingModes.ModeEOperationalActivationState}");
        lines.Add($"- Mode E activation task: {agentWorkingModes.ModeEActivationTaskId ?? "(none)"}");
        lines.Add($"- Mode E activation command: {agentWorkingModes.ModeEActivationCommands.FirstOrDefault() ?? "(none)"}");
        lines.Add($"- Mode E result return channel: {agentWorkingModes.ModeEActivationResultReturnChannel ?? "(none)"}");
        lines.Add($"- Mode E activation next action: {agentWorkingModes.ModeEActivationRecommendedNextAction}");
        lines.Add($"- Mode E activation blocking checks: {agentWorkingModes.ModeEActivationBlockingCheckCount}");
        lines.Add($"- Mode E activation first blocker: {agentWorkingModes.ModeEActivationFirstBlockingCheckId ?? "(none)"}");
        lines.Add($"- Mode E activation first blocker action: {agentWorkingModes.ModeEActivationFirstBlockingCheckRequiredAction ?? "(none)"}");
        lines.Add($"- Mode E activation playbook: {agentWorkingModes.ModeEActivationPlaybookSummary}");
        lines.Add($"- Mode E activation playbook steps: {agentWorkingModes.ModeEActivationPlaybookStepCount}");
        lines.Add($"- Mode E activation first playbook command: {agentWorkingModes.ModeEActivationFirstPlaybookStepCommand ?? "(none)"}");
        lines.Add($"- Planning coupling: {agentWorkingModes.PlanningCouplingPosture}");
        lines.Add($"- Formal planning posture: {formalPlanningPosture.OverallPosture}");
        lines.Add($"- Formal planning entry trigger: {formalPlanningPosture.FormalPlanningEntryTriggerState}");
        lines.Add($"- Formal planning entry command: {formalPlanningPosture.FormalPlanningEntryCommand}");
        lines.Add($"- Formal planning entry next action: {formalPlanningPosture.FormalPlanningEntryRecommendedNextAction}");
        lines.Add($"- Active planning slot state: {formalPlanningPosture.ActivePlanningSlotState}");
        lines.Add($"- Active planning slot conflict reason: {(string.IsNullOrWhiteSpace(formalPlanningPosture.ActivePlanningSlotConflictReason) ? "(none)" : formalPlanningPosture.ActivePlanningSlotConflictReason)}");
        lines.Add($"- Active planning slot remediation: {formalPlanningPosture.ActivePlanningSlotRemediationAction}");
        lines.Add($"- Planning card invariant state: {formalPlanningPosture.PlanningCardInvariantState}");
        lines.Add($"- Planning card invariant violations: {formalPlanningPosture.PlanningCardInvariantViolationCount}");
        lines.Add($"- Planning card invariant remediation: {formalPlanningPosture.PlanningCardInvariantRemediationAction}");
        lines.Add($"- Active planning card fill state: {formalPlanningPosture.ActivePlanningCardFillState}");
        lines.Add($"- Active planning card fill missing required fields: {formalPlanningPosture.ActivePlanningCardFillMissingRequiredFieldCount}");
        lines.Add($"- Active planning card fill next action: {formalPlanningPosture.ActivePlanningCardFillRecommendedNextAction}");
        lines.Add($"- Active plan handle: {formalPlanningPosture.PlanHandle ?? "(none)"}");
        lines.Add($"- Active planning card: {formalPlanningPosture.PlanningCardId ?? "(none)"}");
        lines.Add($"- Managed workspace posture: {formalPlanningPosture.ManagedWorkspacePosture}");
        lines.Add($"- Vendor-native acceleration: {vendorNativeAcceleration.OverallPosture}");
        lines.Add($"- Codex reinforcement: {vendorNativeAcceleration.CodexReinforcementState}");
        lines.Add($"- Claude reinforcement: {vendorNativeAcceleration.ClaudeReinforcementState}");
        lines.Add($"- Session Gateway assist posture: {sessionGatewayGovernanceAssist.OverallPosture}");
        lines.Add($"- Session Gateway highest-priority pressure: {(sessionGatewayTopPressure is null ? "(none)" : $"{sessionGatewayTopPressure.PressureKind} [{sessionGatewayTopPressure.Level}]")}");
        lines.Add($"- Session Gateway highest-priority candidate: {sessionGatewayTopCandidate?.CandidateId ?? "(none)"}");
        lines.Add($"- Session Gateway assist next action: {sessionGatewayGovernanceAssist.RecommendedNextAction}");
        lines.Add($"- Acceptance contract ingress policy: {acceptanceContractIngressPolicy.PolicySummary}");
        lines.Add($"- Recovery outcomes: {(operational.RecoveryOutcomes.Count == 0 ? "(none)" : string.Join(", ", operational.RecoveryOutcomes.Select(item => $"{item.RecoveryAction}:{item.Outcome}")))}");
        lines.Add($"- Recommended next action: {operational.RecommendedNextAction}");
        lines.Add($"- Projection writeback: {projectionHealth.State} ({projectionHealth.Summary})");
        lines.Add($"- Host control: {hostSession?.ControlState.ToString().ToLowerInvariant() ?? "running"}");
        lines.Add($"- Reality gradient: solid={GetRealityCount(realityCounts, "solid")}; proto={GetRealityCount(realityCounts, "proto")}; ghost={GetRealityCount(realityCounts, "ghost")}");
        lines.Add(string.Empty);
        lines.Add("Permission Approvals");
        foreach (var request in pendingPermissions.Take(5))
        {
            lines.Add($"- {request.PermissionRequestId}: task={request.TaskId}; kind={request.Kind}; risk={request.RiskLevel}; recommended={request.RecommendedDecision}");
        }

        if (lines[^1] == "Permission Approvals")
        {
            lines.Add("(none)");
        }

        lines.Add(string.Empty);
        lines.Add("Opportunity Explorer");
        foreach (var opportunity in operatorApiService.GetRepoOpportunities(repo.RepoId).Take(5))
        {
            lines.Add($"- {opportunity.OpportunityId}: {opportunity.Source} [{opportunity.Status}] confidence={opportunity.Confidence:0.00}");
        }

        if (lines[^1] == "Opportunity Explorer")
        {
            lines.Add("(none)");
        }

        lines.Add(string.Empty);
        lines.Add("TaskGraph Explorer");
        foreach (var task in operatorApiService.GetRepoTasks(repo.RepoId).Tasks.Take(5))
        {
            lines.Add($"- {task.TaskId}: {task.Status} / {task.TaskType} / {task.ProposalSource}");
        }

        if (lines[^1] == "TaskGraph Explorer")
        {
            lines.Add("(none)");
        }

        lines.Add(string.Empty);
        lines.Add("Incident Timeline");
        foreach (var incident in incidents.Take(5))
        {
            lines.Add($"- {incident.IncidentType}: task={incident.TaskId ?? "(none)"} backend={incident.BackendId ?? "(none)"} action={incident.RecoveryAction} summary={incident.Summary}");
        }

        if (lines[^1] == "Incident Timeline")
        {
            lines.Add("(none)");
        }

        lines.Add(string.Empty);
        lines.Add("Run Drilldown");
        foreach (var run in runDrilldown)
        {
            var currentStep = run.Steps.Count == 0
                ? "(none)"
                : run.Steps[Math.Clamp(run.CurrentStepIndex, 0, run.Steps.Count - 1)].Title;
            lines.Add($"- {run.RunId}: task={run.TaskId} status={run.Status} step={currentStep}");
        }

        if (lines[^1] == "Run Drilldown")
        {
            lines.Add("(none)");
        }

        lines.Add(string.Empty);
        lines.Add("Governance Report");
        lines.Add($"- Window: last {governanceReport.WindowHours}h");
        lines.Add($"- Approval decisions: {(governanceReport.ApprovalDecisions.Count == 0 ? "(none)" : string.Join(", ", governanceReport.ApprovalDecisions.Select(item => $"{item.Decision}={item.Count}")))}");
        lines.Add($"- Recovery success: {governanceReport.RecoverySuccessfulCount}/{governanceReport.RecoverySampleCount} ({governanceReport.RecoverySuccessRate:P0})");
        lines.Add($"- Unstable providers: {governanceReport.UnstableProviders.Count}");
        lines.Add($"- Repo incident density: {(governanceReport.RepoIncidentDensity.Count == 0 ? "(none)" : string.Join(", ", governanceReport.RepoIncidentDensity.Select(item => $"{item.RepoId}={item.IncidentCount}")))}");
        lines.Add(string.Empty);
        lines.Add("Actor Sessions");
        foreach (var actorSession in actorSessions.Take(5))
        {
            lines.Add($"- {actorSession.ActorSessionId}: {actorSession.Kind}/{actorSession.State} identity={actorSession.ActorIdentity} ownership={actorSession.CurrentOwnershipScope?.ToString() ?? "(none)"}");
        }

        if (lines[^1] == "Actor Sessions")
        {
            lines.Add("(none)");
        }

        lines.Add(string.Empty);
        lines.Add("Operator OS Event Stream");
        foreach (var item in operatorOsEvents.Take(5))
        {
            lines.Add($"- {item.EventKind}: actor={item.ActorKind?.ToString() ?? "(none)"} task={item.TaskId ?? "(none)"} summary={item.Summary}");
        }

        if (lines[^1] == "Operator OS Event Stream")
        {
            lines.Add("(none)");
        }

        return lines;
    }

    private string ResolveRealityStatus(TaskNode task)
    {
        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId);
        if (reviewArtifact is not null)
        {
            return ToSnakeCase(reviewArtifact.RealityProjection.SolidityClass);
        }

        var draft = string.IsNullOrWhiteSpace(task.CardId) ? null : planningDraftService.TryGetCardDraft(task.CardId!);
        return draft?.RealityModel is null ? "ghost" : ToSnakeCase(draft.RealityModel.SolidityClass);
    }

    private string BuildRealitySummary(TaskNode task)
    {
        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId);
        if (reviewArtifact is not null)
        {
            return $"{task.TaskId} [{ToSnakeCase(reviewArtifact.RealityProjection.SolidityClass)}] planned={reviewArtifact.RealityProjection.PlannedScope}; verified={reviewArtifact.RealityProjection.VerifiedOutcome}; promotion={reviewArtifact.RealityProjection.PromotionResult}";
        }

        var draft = string.IsNullOrWhiteSpace(task.CardId) ? null : planningDraftService.TryGetCardDraft(task.CardId!);
        if (draft?.RealityModel is not null)
        {
            return $"{task.TaskId} [{ToSnakeCase(draft.RealityModel.SolidityClass)}] planned={draft.RealityModel.CurrentSolidScope}; next={draft.RealityModel.NextRealSlice}";
        }

        return $"{task.TaskId} [ghost] no reality projection recorded";
    }

    private static int GetRealityCount(IReadOnlyDictionary<string, int> counts, string key)
    {
        return counts.TryGetValue(key, out var count) ? count : 0;
    }

    private static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
