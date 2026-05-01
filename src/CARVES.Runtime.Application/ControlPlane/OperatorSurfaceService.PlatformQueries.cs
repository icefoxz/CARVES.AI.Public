namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult Status()
    {
        var carvesCodeStandard = configRepository.LoadCarvesCodeStandard();
        var plannerAutonomyPolicy = configRepository.LoadPlannerAutonomyPolicy();
        var safetyRules = configRepository.LoadSafetyRules();
        var moduleMap = configRepository.LoadModuleDependencyMap();
        var opportunities = opportunityDetectorService.LoadSnapshot();
        var graph = taskGraphService.Load();
        var session = devLoopService.GetSession();
        var dispatch = dispatchProjectionService.Build(graph, session, systemConfig.MaxParallelTasks);
        var platformStatus = operatorApiService.GetPlatformStatus();
        var interaction = interactionLayerService.GetSnapshot(session);
        var operationalSummary = operationalSummaryService.Build();
        var agentWorkingModes = platformDashboardService.BuildAgentWorkingModesSurface();
        var formalPlanningPosture = platformDashboardService.BuildFormalPlanningPostureSurface();
        var vendorNativeAcceleration = platformDashboardService.BuildVendorNativeAccelerationSurface();
        var sessionGatewayGovernanceAssist = platformDashboardService.BuildSessionGatewayGovernanceAssistSurface();
        var acceptanceContractIngressPolicy = platformDashboardService.BuildAcceptanceContractIngressPolicySurface();

        return OperatorSurfaceFormatter.Status(repoRoot, paths, systemConfig, aiProviderConfig, carvesCodeStandard, plannerAutonomyPolicy, aiClient.IsConfigured, safetyRules, opportunities, moduleMap.Entries.Count, graph, dispatch, session, platformStatus, interaction, operationalSummary, agentWorkingModes, formalPlanningPosture, vendorNativeAcceleration, sessionGatewayGovernanceAssist, acceptanceContractIngressPolicy);
    }

    public OperatorCommandResult GovernanceShow()
    {
        return OperatorSurfaceFormatter.Governance(platformGovernanceService.GetSnapshot(), platformGovernanceService.LoadEvents());
    }

    public OperatorCommandResult GovernanceReport(int? hours = null)
    {
        return OperatorSurfaceFormatter.GovernanceReport(governanceReportingService.Build(hours));
    }

    public OperatorCommandResult GovernanceInspect(string repoId)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        var repoPolicy = platformGovernanceService.ResolveRepoPolicy(descriptor.PolicyProfile);
        var providerPolicy = platformGovernanceService.ResolveProviderPolicy(repoPolicy.ProviderPolicyProfile);
        var workerPolicy = platformGovernanceService.ResolveWorkerPolicy(repoPolicy.WorkerPolicyProfile);
        var reviewPolicy = platformGovernanceService.ResolveReviewPolicy(repoPolicy.ReviewPolicyProfile);
        return OperatorSurfaceFormatter.GovernanceInspect(descriptor, repoPolicy, providerPolicy, workerPolicy, reviewPolicy);
    }

    public OperatorCommandResult WorkerList() => WorkerListCore();

    public OperatorCommandResult WorkerProviders() => WorkerProvidersCore();

    public OperatorCommandResult WorkerProfiles(string? repoId = null) => WorkerProfilesCore(repoId);

    public OperatorCommandResult WorkerSummary() => WorkerSummaryCore();

    public OperatorCommandResult WorkerSelection(string? repoId, string? taskId) => WorkerSelectionCore(repoId, taskId);

    public OperatorCommandResult WorkerActivateExternalCodex(bool dryRun, string? reason) => WorkerActivateExternalCodexCore(dryRun, reason);

    public OperatorCommandResult WorkerActivateExternalAppCli(bool dryRun, string? reason) => WorkerActivateExternalAppCliCore(dryRun, reason);

    public OperatorCommandResult WorkerLeases() => WorkerLeasesCore();

    public OperatorCommandResult WorkerHeartbeat(string nodeId) => WorkerHeartbeatCore(nodeId);

    public OperatorCommandResult WorkerQuarantine(string nodeId, string reason) => WorkerQuarantineCore(nodeId, reason);

    public OperatorCommandResult WorkerExpireLease(string leaseId, string reason) => WorkerExpireLeaseCore(leaseId, reason);

    public OperatorCommandResult ApiPlatformStatus() => ApiPlatformStatusCore();

    public OperatorCommandResult ApiRepos() => ApiReposCore();

    public OperatorCommandResult ApiRepoRuntime(string repoId) => ApiRepoRuntimeCore(repoId);

    public OperatorCommandResult ApiRepoTasks(string repoId) => ApiRepoTasksCore(repoId);

    public OperatorCommandResult ApiRepoOpportunities(string repoId) => ApiRepoOpportunitiesCore(repoId);

    public OperatorCommandResult ApiRepoSession(string repoId) => ApiRepoSessionCore(repoId);

    public OperatorCommandResult ApiProviderQuota() => ApiProviderQuotaCore();

    public OperatorCommandResult ApiProviderRoute(string repoId, string role, bool allowFallback) => ApiProviderRouteCore(repoId, role, allowFallback);

    public OperatorCommandResult ApiGovernanceReport(int? hours = null) => ApiGovernanceReportCore(hours);

    public OperatorCommandResult ApiPlatformSchedule(int requestedSlots) => ApiPlatformScheduleCore(requestedSlots);

    public OperatorCommandResult ApiWorkerLeases() => ApiWorkerLeasesCore();

    public OperatorCommandResult ApiWorkerProviders() => ApiWorkerProvidersCore();

    public OperatorCommandResult ApiWorkerProfiles(string? repoId) => ApiWorkerProfilesCore(repoId);

    public OperatorCommandResult ApiWorkerSelection(string repoId, string? taskId) => ApiWorkerSelectionCore(repoId, taskId);

    public OperatorCommandResult ApiWorkerExternalCodexActivation(bool dryRun, string? reason) => ApiWorkerExternalCodexActivationCore(dryRun, reason);

    public OperatorCommandResult ApiWorkerExternalAppCliActivation(bool dryRun, string? reason) => ApiWorkerExternalAppCliActivationCore(dryRun, reason);

    public OperatorCommandResult InspectRoutingProfile() => InspectRoutingProfileCore();

    public OperatorCommandResult ApiRoutingProfile() => ApiRoutingProfileCore();

    public OperatorCommandResult InspectQualification() => InspectQualificationCore();

    public OperatorCommandResult ApiQualification() => ApiQualificationCore();

    public OperatorCommandResult InspectQualificationCandidate() => InspectQualificationCandidateCore();

    public OperatorCommandResult ApiQualificationCandidate() => ApiQualificationCandidateCore();

    public OperatorCommandResult InspectQualificationPromotionDecision(string? candidateId = null) => InspectQualificationPromotionDecisionCore(candidateId);

    public OperatorCommandResult ApiQualificationPromotionDecision(string? candidateId = null) => ApiQualificationPromotionDecisionCore(candidateId);

    public OperatorCommandResult RunQualification(int? attempts = null) => RunQualificationCore(attempts);

    public OperatorCommandResult MaterializeQualificationCandidate() => MaterializeQualificationCandidateCore();

    public OperatorCommandResult PromoteQualificationCandidate(string? candidateId = null) => PromoteQualificationCandidateCore(candidateId);

    public OperatorCommandResult ApiOperationalSummary() => ApiOperationalSummaryCore();

    public OperatorCommandResult ApiRepoGateway(string repoId) => ApiRepoGatewayCore(repoId);

    public OperatorCommandResult Dashboard(string? repoId = null) => DashboardCore(repoId);

    public OperatorCommandResult LintPlatform() => LintPlatformCore();

    public OperatorCommandResult StartSession(bool dryRun)
    {
        return OperatorSurfaceFormatter.SessionChanged("Started", devLoopService.StartSession(dryRun));
    }

    public OperatorCommandResult SessionStatus()
    {
        return OperatorSurfaceFormatter.SessionStatus(devLoopService.GetSession());
    }

    public OperatorCommandResult TickSession(bool dryRun)
    {
        return OperatorSurfaceFormatter.RunNext(devLoopService.Tick(dryRun));
    }

    public OperatorCommandResult RunSessionLoop(bool dryRun, int maxIterations)
    {
        return OperatorSurfaceFormatter.ContinuousLoop(devLoopService.RunContinuousLoop(dryRun, maxIterations));
    }

    public OperatorCommandResult PauseSession(string reason)
    {
        return OperatorSurfaceFormatter.SessionChanged("Paused", devLoopService.PauseSession(reason));
    }

    public OperatorCommandResult ResumeSession(string reason)
    {
        return OperatorSurfaceFormatter.SessionChanged("Resumed", devLoopService.ResumeSession(reason));
    }

    public OperatorCommandResult StopSession(string reason)
    {
        return OperatorSurfaceFormatter.SessionChanged("Stopped", devLoopService.StopSession(reason));
    }
}
