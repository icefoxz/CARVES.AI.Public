using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

internal static partial class LocalHostCommandDispatcher
{
    private static OperatorCommandResult RunApiCommandCore(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var apiUsage = BuildApiUsage();
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure(apiUsage);
        }

        if (string.Equals(arguments[0], "all-surfaces", StringComparison.OrdinalIgnoreCase))
        {
            return OperatorCommandResult.Success(BuildApiAllSurfacesUsage());
        }

        var surface = new LocalHostSurfaceService(services);
        if (RuntimeSurfaceCommandRegistry.TryDispatchApi(services.OperatorSurfaceService, arguments, out var registryResult))
        {
            return string.Equals(arguments[0], "runtime-product-closure-pilot-status", StringComparison.OrdinalIgnoreCase)
                ? DecoratePilotStatusJson(services, registryResult)
                : registryResult;
        }

        return arguments[0] switch
        {
            "platform-status" => services.OperatorSurfaceService.ApiPlatformStatus(),
            "repos" => services.OperatorSurfaceService.ApiRepos(),
            "repo-runtime" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: api repo-runtime <repo-id>")
                : services.OperatorSurfaceService.ApiRepoRuntime(arguments[1]),
            "repo-tasks" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: api repo-tasks <repo-id>")
                : services.OperatorSurfaceService.ApiRepoTasks(arguments[1]),
            "repo-opportunities" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: api repo-opportunities <repo-id>")
                : services.OperatorSurfaceService.ApiRepoOpportunities(arguments[1]),
            "repo-session" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: api repo-session <repo-id>")
                : services.OperatorSurfaceService.ApiRepoSession(arguments[1]),
            "provider-quota" => services.OperatorSurfaceService.ApiProviderQuota(),
            "provider-route" => arguments.Count < 3
                ? OperatorCommandResult.Failure("Usage: api provider-route <repo-id> <role> [--no-fallback]")
                : services.OperatorSurfaceService.ApiProviderRoute(arguments[1], arguments[2], !arguments.Any(argument => string.Equals(argument, "--no-fallback", StringComparison.OrdinalIgnoreCase))),
            "routing-profile" => services.OperatorSurfaceService.ApiRoutingProfile(),
            "qualification" => services.OperatorSurfaceService.ApiQualification(),
            "qualification-candidate" => services.OperatorSurfaceService.ApiQualificationCandidate(),
            "qualification-promotion" => services.OperatorSurfaceService.ApiQualificationPromotionDecision(arguments.Count >= 2 ? arguments[1] : null),
            "promotion-readiness" => services.OperatorSurfaceService.ApiPromotionReadiness(arguments.Count >= 2 ? arguments[1] : null),
            "validation-suite" => services.OperatorSurfaceService.ApiValidationSuite(),
            "validation-trace" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: api validation-trace <trace-id>")
                : services.OperatorSurfaceService.ApiValidationTrace(arguments[1]),
            "validation-summary" => services.OperatorSurfaceService.ApiValidationSummary(ResolveOption(arguments, "--run-id")),
            "validation-history" => services.OperatorSurfaceService.ApiValidationHistory(ResolveOptionalInt(arguments, "--limit")),
            "validation-coverage" => services.OperatorSurfaceService.ApiValidationCoverage(arguments.Count >= 2 ? arguments[1] : null),
            "platform-schedule" => services.OperatorSurfaceService.ApiPlatformSchedule(ResolveOptionalPositiveInt(arguments, "--slots", 1)),
            "worker-providers" => services.OperatorSurfaceService.ApiWorkerProviders(),
            "worker-profiles" => services.OperatorSurfaceService.ApiWorkerProfiles(arguments.Count >= 2 ? arguments[1] : null),
            "worker-selection" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: api worker-selection <repo-id> [--task-id <task-id>]")
                : services.OperatorSurfaceService.ApiWorkerSelection(arguments[1], ResolveOption(arguments, "--task-id")),
            "worker-health" => services.OperatorSurfaceService.ApiWorkerHealth(ResolveOption(arguments, "--backend"), !arguments.Any(argument => string.Equals(argument, "--no-refresh", StringComparison.OrdinalIgnoreCase))),
            "worker-automation-readiness" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildWorkerAutomationReadiness(
                ResolveOption(arguments, "--repo-id"),
                arguments.Any(argument => string.Equals(argument, "--refresh-worker-health", StringComparison.OrdinalIgnoreCase))))),
            "worker-automation-schedule-tick" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildWorkerAutomationScheduleTick(
                ResolveOption(arguments, "--repo-id"),
                arguments.Any(argument => string.Equals(argument, "--refresh-worker-health", StringComparison.OrdinalIgnoreCase)),
                arguments.Any(argument => string.Equals(argument, "--dispatch", StringComparison.OrdinalIgnoreCase)),
                !(arguments.Any(argument => string.Equals(argument, "--dispatch", StringComparison.OrdinalIgnoreCase))
                  && arguments.Any(argument => string.Equals(argument, "--execute", StringComparison.OrdinalIgnoreCase)))))),
            "worker-supervisor-launch" => services.OperatorSurfaceService.ApiWorkerSupervisorLaunch(
                ResolveOption(arguments, "--repo-id"),
                ResolveOption(arguments, "--identity"),
                ResolveOption(arguments, "--worker-instance-id"),
                ResolveOption(arguments, "--actor-session-id"),
                ResolveOption(arguments, "--provider-profile"),
                ResolveOption(arguments, "--capability-profile"),
                ResolveOption(arguments, "--schedule-binding"),
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "worker-supervisor-instances" => services.OperatorSurfaceService.ApiWorkerSupervisorInstances(ResolveOption(arguments, "--repo-id")),
            "worker-supervisor-events" => services.OperatorSurfaceService.ApiWorkerSupervisorEvents(
                ResolveOption(arguments, "--repo-id"),
                ResolveOption(arguments, "--worker-instance-id"),
                ResolveOption(arguments, "--actor-session-id")),
            "worker-supervisor-archive" => services.OperatorSurfaceService.ApiWorkerSupervisorArchive(
                ResolveOption(arguments, "--worker-instance-id"),
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "worker-dispatch-pilot-evidence" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: api worker-dispatch-pilot-evidence <task-id>")
                : OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildWorkerDispatchPilotEvidence(arguments[1]))),
            "worker-external-app-cli-activation" => services.OperatorSurfaceService.ApiWorkerExternalAppCliActivation(
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "worker-external-codex-activation" => services.OperatorSurfaceService.ApiWorkerExternalCodexActivation(
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "worker-permissions" => services.OperatorSurfaceService.ApiWorkerPermissionRequests(),
            "worker-permission-audit" => services.OperatorSurfaceService.ApiWorkerPermissionAudit(ResolveOption(arguments, "--task-id"), ResolveOption(arguments, "--request-id")),
            "worker-incidents" => services.OperatorSurfaceService.ApiWorkerIncidents(ResolveOption(arguments, "--task-id"), ResolveOption(arguments, "--run-id")),
            "worker-leases" => services.OperatorSurfaceService.ApiWorkerLeases(),
            "operational-summary" => services.OperatorSurfaceService.ApiOperationalSummary(),
            "governance-report" => services.OperatorSurfaceService.ApiGovernanceReport(ResolveOptionalPositiveInt(arguments, "--hours", defaultValue: 24)),
            "actor-sessions" => services.OperatorSurfaceService.ApiActorSessions(
                ParseActorSessionKind(ResolveOption(arguments, "--kind")),
                ResolveOption(arguments, "--repo-id")),
            "actor-session-register" => arguments.Count == 1
                ? OperatorCommandResult.Failure(ApiActorSessionRegisterUsage)
                : services.OperatorSurfaceService.ApiActorSessionRegister(
                    ParseActorSessionKind(ResolveOption(arguments, "--kind")),
                    ResolveOption(arguments, "--identity"),
                    ResolveOption(arguments, "--repo-id"),
                    ResolveOption(arguments, "--actor-session-id"),
                    ResolveOption(arguments, "--provider-profile"),
                    ResolveOption(arguments, "--capability-profile"),
                    ResolveOption(arguments, "--scope"),
                    ResolveOption(arguments, "--budget-profile"),
                    ResolveOption(arguments, "--schedule-binding"),
                    ResolveOption(arguments, "--context-receipt"),
                    ResolveOption(arguments, "--health"),
                    ResolveOptionalInt(arguments, "--process-id"),
                    ResolveOption(arguments, "--registration-mode"),
                    ResolveOption(arguments, "--worker-instance-id"),
                    ResolveOption(arguments, "--launch-token"),
                    ResolveOption(arguments, "--reason"),
                    arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))),
            "actor-session-repair" => services.OperatorSurfaceService.ApiActorSessionRepair(arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))),
            "actor-session-clear" => services.OperatorSurfaceService.ApiActorSessionClear(
                ResolveOption(arguments, "--actor-session-id"),
                ResolveOption(arguments, "--repo-id"),
                ParseActorSessionKind(ResolveOption(arguments, "--kind")),
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "actor-session-stop" => services.OperatorSurfaceService.ApiActorSessionStop(
                ResolveOption(arguments, "--actor-session-id"),
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "actor-session-heartbeat" => services.OperatorSurfaceService.ApiActorSessionHeartbeat(
                ResolveOption(arguments, "--actor-session-id"),
                ResolveOption(arguments, "--health"),
                ResolveOption(arguments, "--context-receipt"),
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "actor-session-fallback-policy" => services.OperatorSurfaceService.ApiActorSessionFallbackPolicy(ResolveOption(arguments, "--repo-id")),
            "actor-ownership" => services.OperatorSurfaceService.ApiActorOwnership(ParseOwnershipScope(ResolveOption(arguments, "--scope")), ResolveOption(arguments, "--target-id")),
            "os-events" => services.OperatorSurfaceService.ApiOperatorOsEvents(ResolveOption(arguments, "--task-id"), ResolveOption(arguments, "--actor-session-id"), ParseOperatorOsEventKind(ResolveOption(arguments, "--kind"))),
            "agent-gateway-trace" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildAgentGatewayTrace())),
            "repo-gateway" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: api repo-gateway <repo-id>")
                : services.OperatorSurfaceService.ApiRepoGateway(arguments[1]),
            _ => OperatorCommandResult.Failure(apiUsage),
        };
    }
}
