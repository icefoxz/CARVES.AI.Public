using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private OperatorCommandResult WorkerListCore()
    {
        return OperatorSurfaceFormatter.WorkerNodes(workerBroker.ListNodes());
    }

    private OperatorCommandResult WorkerProvidersCore()
    {
        return OperatorSurfaceFormatter.WorkerProviders(providerRegistryService.ListWorkerBackends());
    }

    private OperatorCommandResult WorkerProfilesCore(string? repoId = null)
    {
        return OperatorSurfaceFormatter.WorkerProfiles(workerExecutionBoundaryService.ListProfiles(repoId), repoId);
    }

    private OperatorCommandResult WorkerSummaryCore()
    {
        return OperatorSurfaceFormatter.WorkerOperationalSummary(operationalSummaryService.Build());
    }

    private OperatorCommandResult WorkerSelectionCore(string? repoId, string? taskId)
    {
        TaskNode? task = null;
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            task = taskGraphService.GetTask(taskId);
        }

        var decision = workerSelectionPolicyService.Evaluate(task, repoId);
        return OperatorSurfaceFormatter.WorkerSelection(decision);
    }

    private OperatorCommandResult WorkerActivateExternalCodexCore(bool dryRun, string? reason)
    {
        return WorkerActivateExternalAppCliCore(
            dryRun,
            string.IsNullOrWhiteSpace(reason)
                ? "Activated external Codex worker policy through Host."
                : reason.Trim(),
            "activate_external_codex",
            dryRun ? "External Codex worker activation dry run" : "External Codex worker activation",
            useCompatibilityCodexSurface: true);
    }

    private OperatorCommandResult WorkerActivateExternalAppCliCore(bool dryRun, string? reason)
    {
        return WorkerActivateExternalAppCliCore(
            dryRun,
            string.IsNullOrWhiteSpace(reason)
                ? "Activated provider-neutral external app/CLI worker policy through Host."
                : reason.Trim(),
            "activate_external_app_cli",
            dryRun ? "External app/CLI worker activation dry run" : "External app/CLI worker activation",
            useCompatibilityCodexSurface: false);
    }

    private OperatorCommandResult WorkerActivateExternalAppCliCore(
        bool dryRun,
        string resolvedReason,
        string operation,
        string title,
        bool useCompatibilityCodexSurface)
    {
        var actorSession = EnsureControlActorSession(
            ActorSessionKind.Operator,
            "operator",
            resolvedReason,
            OwnershipScope.RuntimeControl,
            "worker-selection-policy",
            operationClass: "worker_policy",
            operation: operation);
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.RuntimeControl,
            "worker-selection-policy",
            resolvedReason,
            () =>
            {
                var result = useCompatibilityCodexSurface
                    ? runtimePolicyBundleService.ActivateExternalCodexWorkerOnly(dryRun, resolvedReason)
                    : runtimePolicyBundleService.ActivateExternalAppCliWorkers(dryRun, resolvedReason);
                var lines = new List<string>
                {
                    title,
                    $"Outcome: {result.Outcome}",
                    $"Applied: {result.Applied}",
                    $"Activation mode: {result.ActivationMode}",
                    $"Current activation mode: {result.CurrentActivationMode}",
                    $"Policy file: {result.PolicyFile}",
                    $"Allowed worker paths: {string.Join(", ", result.AllowedWorkerPaths)}",
                    $"Allowed backend ids: {string.Join(", ", result.AllowedBackendIds)}",
                    $"Current materialized worker backend: {result.CurrentMaterializedWorkerBackendId}",
                    $"Provider-neutral external app/CLI policy: {result.ProviderNeutralExternalAppCliPolicy}",
                    $"Future external app/CLI adapters require governed onboarding: {result.FutureExternalAppCliAdaptersRequireGovernedOnboarding}",
                    $"SDK/API backends closed: {result.SdkApiBackendsClosed}",
                    $"SDK/API worker boundary: {result.SdkApiWorkerBoundary}",
                    $"Codex CLI registered: {result.CodexCliRegistered}",
                    $"Codex CLI allowed: {result.CodexCliAllowed}",
                    $"Codex SDK allowed: {result.CodexSdkAllowed}",
                    $"Starts run: {result.StartsRun}",
                    $"Issues lease: {result.IssuesLease}",
                    $"Ingests result: {result.IngestsResult}",
                    $"Writes task truth: {result.WritesTaskTruth}",
                    $"Reason: {result.Reason}",
                };

                if (!string.IsNullOrWhiteSpace(result.BlockedReason))
                {
                    lines.Add($"Blocked reason: {result.BlockedReason}");
                }

                return new OperatorCommandResult(result.Allowed ? 0 : 1, lines);
            });
    }

    private OperatorCommandResult WorkerLeasesCore()
    {
        return OperatorSurfaceFormatter.WorkerLeases(workerBroker.ListLeases());
    }

    private OperatorCommandResult WorkerHeartbeatCore(string nodeId)
    {
        return OperatorSurfaceFormatter.WorkerNodeChanged("Heartbeat", workerBroker.Heartbeat(nodeId));
    }

    private OperatorCommandResult WorkerQuarantineCore(string nodeId, string reason)
    {
        var actorSession = EnsureControlActorSession(
            ActorSessionKind.Operator,
            "operator",
            reason,
            OwnershipScope.WorkerInterruption,
            nodeId,
            operationClass: "worker",
            operation: "quarantine");
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.WorkerInterruption,
            nodeId,
            reason,
            () =>
            {
                var node = workerBroker.Quarantine(nodeId, reason);
                platformGovernanceService.RecordEvent(GovernanceEventType.WorkerQuarantined, string.Empty, $"Worker {nodeId} quarantined: {reason}");
                return OperatorSurfaceFormatter.WorkerNodeChanged("Quarantined", node);
            });
    }

    private OperatorCommandResult WorkerExpireLeaseCore(string leaseId, string reason)
    {
        var actorSession = EnsureControlActorSession(
            ActorSessionKind.Operator,
            "operator",
            reason,
            OwnershipScope.WorkerInterruption,
            leaseId,
            operationClass: "worker",
            operation: "expire");
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.WorkerInterruption,
            leaseId,
            reason,
            () =>
            {
                var lease = workerBroker.ExpireLease(leaseId, reason);
                platformGovernanceService.RecordEvent(GovernanceEventType.WorkerLeaseExpired, lease.RepoId ?? string.Empty, $"Worker lease {leaseId} expired: {reason}");
                return OperatorSurfaceFormatter.WorkerLeaseExpired(lease);
            });
    }

    private OperatorCommandResult ApiPlatformStatusCore() => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetPlatformStatus()));

    private OperatorCommandResult ApiReposCore() => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetRepos()));

    private OperatorCommandResult ApiRepoRuntimeCore(string repoId) => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetRepoRuntime(repoId)));

    private OperatorCommandResult ApiRepoTasksCore(string repoId) => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetRepoTasks(repoId)));

    private OperatorCommandResult ApiRepoOpportunitiesCore(string repoId) => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetRepoOpportunities(repoId)));

    private OperatorCommandResult ApiRepoSessionCore(string repoId) => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetRepoSession(repoId)));

    private OperatorCommandResult ApiProviderQuotaCore() => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetProviderQuotas()));

    private OperatorCommandResult ApiProviderRouteCore(string repoId, string role, bool allowFallback) => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetProviderRoute(repoId, role, allowFallback)));

    private OperatorCommandResult ApiGovernanceReportCore(int? hours = null) => OperatorCommandResult.Success(operatorApiService.ToJson(governanceReportingService.Build(hours)));

    private OperatorCommandResult ApiPlatformScheduleCore(int requestedSlots) => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetPlatformSchedule(requestedSlots)));

    private OperatorCommandResult ApiWorkerLeasesCore() => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetWorkerLeases()));

    private OperatorCommandResult ApiWorkerProvidersCore() => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetWorkerProviders()));

    private OperatorCommandResult ApiWorkerProfilesCore(string? repoId) => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetWorkerProfiles(repoId)));

    private OperatorCommandResult ApiWorkerSelectionCore(string repoId, string? taskId) => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetWorkerSelection(repoId, taskId)));

    private OperatorCommandResult ApiWorkerExternalCodexActivationCore(bool dryRun, string? reason)
    {
        return ApiWorkerExternalAppCliActivationCore(
            dryRun,
            string.IsNullOrWhiteSpace(reason)
                ? "Activated external Codex worker policy through Host API."
                : reason.Trim(),
            "activate_external_codex_api",
            useCompatibilityCodexSurface: true);
    }

    private OperatorCommandResult ApiWorkerExternalAppCliActivationCore(bool dryRun, string? reason)
    {
        return ApiWorkerExternalAppCliActivationCore(
            dryRun,
            string.IsNullOrWhiteSpace(reason)
                ? "Activated provider-neutral external app/CLI worker policy through Host API."
                : reason.Trim(),
            "activate_external_app_cli_api",
            useCompatibilityCodexSurface: false);
    }

    private OperatorCommandResult ApiWorkerExternalAppCliActivationCore(
        bool dryRun,
        string resolvedReason,
        string operation,
        bool useCompatibilityCodexSurface)
    {
        var actorSession = EnsureControlActorSession(
            ActorSessionKind.Operator,
            "operator",
            resolvedReason,
            OwnershipScope.RuntimeControl,
            "worker-selection-policy",
            operationClass: "worker_policy",
            operation: operation);
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.RuntimeControl,
            "worker-selection-policy",
            resolvedReason,
            () =>
            {
                var result = useCompatibilityCodexSurface
                    ? runtimePolicyBundleService.ActivateExternalCodexWorkerOnly(dryRun, resolvedReason)
                    : runtimePolicyBundleService.ActivateExternalAppCliWorkers(dryRun, resolvedReason);
                return new OperatorCommandResult(
                    result.Allowed ? 0 : 1,
                    [operatorApiService.ToJson(result)]);
            });
    }

    private OperatorCommandResult InspectRoutingProfileCore() => OperatorSurfaceFormatter.RoutingProfile(operatorApiService.GetActiveRoutingProfile());

    private OperatorCommandResult ApiRoutingProfileCore() => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetActiveRoutingProfile()));

    private OperatorCommandResult InspectQualificationCore() => OperatorSurfaceFormatter.Qualification(currentModelQualificationService.LoadOrCreateMatrix(), currentModelQualificationService.LoadLatestRun());

    private OperatorCommandResult ApiQualificationCore()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(new
        {
            matrix = currentModelQualificationService.LoadOrCreateMatrix(),
            latest_run = currentModelQualificationService.LoadLatestRun(),
        }));
    }

    private OperatorCommandResult InspectQualificationCandidateCore() => OperatorSurfaceFormatter.QualificationCandidate(currentModelQualificationService.LoadCandidate());

    private OperatorCommandResult ApiQualificationCandidateCore() => OperatorCommandResult.Success(operatorApiService.ToJson(currentModelQualificationService.LoadCandidate()));

    private OperatorCommandResult InspectQualificationPromotionDecisionCore(string? candidateId = null) => OperatorSurfaceFormatter.QualificationPromotionDecision(routingPromotionDecisionService.Evaluate(candidateId));

    private OperatorCommandResult ApiQualificationPromotionDecisionCore(string? candidateId = null) => OperatorCommandResult.Success(operatorApiService.ToJson(routingPromotionDecisionService.Evaluate(candidateId)));

    private OperatorCommandResult RunQualificationCore(int? attempts = null)
    {
        var run = currentModelQualificationService.Run(attempts);
        return OperatorCommandResult.Success(
            $"Qualification run: {run.RunId}",
            $"Matrix: {run.MatrixId}",
            $"Records: {run.Results.Length}",
            $"Generated at: {run.GeneratedAt:O}");
    }

    private OperatorCommandResult MaterializeQualificationCandidateCore()
    {
        var candidate = currentModelQualificationService.MaterializeCandidate();
        return OperatorCommandResult.Success(
            $"Candidate routing profile: {candidate.CandidateId}",
            $"Source run: {candidate.SourceRunId}",
            $"Profile id: {candidate.Profile.ProfileId}",
            $"Rules: {candidate.Profile.Rules.Length}");
    }

    private OperatorCommandResult PromoteQualificationCandidateCore(string? candidateId = null)
    {
        var decision = routingPromotionDecisionService.Evaluate(candidateId);
        if (!decision.Eligible)
        {
            return OperatorSurfaceFormatter.QualificationPromotionDecision(decision);
        }

        var profile = currentModelQualificationService.PromoteCandidate(candidateId);
        return OperatorCommandResult.Success(
            "Promoted candidate routing profile to active profile.",
            $"Profile id: {profile.ProfileId}",
            $"Source qualification: {profile.SourceQualificationId ?? "(none)"}",
            $"Activated at: {profile.ActivatedAt?.ToString("O") ?? "(none)"}",
            $"Promotion gate: {decision.Summary}");
    }

    private OperatorCommandResult ApiOperationalSummaryCore() => OperatorCommandResult.Success(operatorApiService.ToJson(operationalSummaryService.Build()));

    private OperatorCommandResult ApiRepoGatewayCore(string repoId) => OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetRepoGateway(repoId)));

    private OperatorCommandResult DashboardCore(string? repoId = null) => new(0, platformDashboardService.Render(repoId));

    private OperatorCommandResult LintPlatformCore() => OperatorSurfaceFormatter.PlatformLint(platformVocabularyLintService.Run());
}
