using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public HostHandshakeSummary BuildHandshake(LocalHostState hostState)
    {
        new RepoHostMappingService(services.RepoRegistryService, services.RepoRuntimeService, services.HostRegistryService)
            .RefreshManagedMappings();
        var platformStatus = services.OperatorApiService.GetPlatformStatus();
        var hostSession = new HostSessionService(services.Paths).Load();
        var session = services.DevLoopService.GetSession();
        var standard = services.ConfigRepository.LoadCarvesCodeStandard();
        var interaction = services.InteractionLayerService.GetSnapshot(session);
        var actorSessions = services.OperatorApiService.GetActorSessions();
        var operatorOsEvents = services.OperatorApiService.GetOperatorOsEvents();
        var rehydration = hostState.RehydrationSummary;
        return new HostHandshakeSummary(
            HostRunning: true,
            Version: typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            StandardVersion: standard.Version,
            Stage: RuntimeStageInfo.CurrentStage,
            HostSessionId: hostSession?.SessionId ?? "(none)",
            UptimeSeconds: Math.Max(0, (long)(DateTimeOffset.UtcNow - hostState.StartedAt).TotalSeconds),
            BaseUrl: hostState.BaseUrl,
            DashboardUrl: hostState.DashboardUrl,
            RuntimeDirectory: hostState.RuntimeDirectory,
            DeploymentDirectory: hostState.DeploymentDirectory,
            ExecutablePath: hostState.ExecutablePath,
            RepoCount: platformStatus.RegisteredRepoCount,
            AttachedRepoCount: hostSession?.AttachedRepos.Count ?? 0,
            ActiveSessionCount: platformStatus.ActiveSessionCount,
            PlannerState: session is null ? "none" : PlannerLifecycleSemantics.DescribeState(session.PlannerLifecycleState),
            WorkerCount: platformStatus.WorkerNodeCount,
            ActiveWorkerCount: session?.ActiveWorkerCount ?? 0,
            ActorSessionCount: actorSessions.Count,
            OperatorOsEventCount: operatorOsEvents.Count,
            Rehydrated: rehydration.Rehydrated,
            PendingApprovalCount: rehydration.PendingApprovalCount,
            RecentIncidentCount: rehydration.RecentIncidentCount,
            StaleMarkerCount: rehydration.StaleMarkerCount,
            PausedRuntimeCount: rehydration.PausedRuntimeCount,
            RehydrationSummary: rehydration.Summary,
            HostControlState: hostSession?.ControlState.ToString().ToLowerInvariant() ?? "running",
            HostControlAction: hostSession?.LastControlAction.ToString().ToLowerInvariant() ?? "started",
            HostControlReason: hostSession?.LastControlReason,
            HostControlAt: hostSession?.LastControlAt,
            ProtocolMode: interaction.ProtocolMode,
            ConversationPhase: interaction.Protocol.CurrentPhase.ToString().ToLowerInvariant(),
            IntentState: interaction.Intent.State.ToString().ToLowerInvariant(),
            PromptKernel: $"{interaction.PromptKernel.KernelId}@{interaction.PromptKernel.Version}",
            ProjectUnderstandingState: interaction.ProjectUnderstanding.State.ToString().ToLowerInvariant(),
            Capabilities:
            [
                "runtime-status",
                "control-plane-mutation",
                "dashboard",
                "workbench",
                "planner-inspect",
                "worker-inspect",
                "agent-gateway",
                "session-gateway-v1",
                "delegated-execution",
                "card-task-inspect",
                "discussion-surface",
                "attach-flow",
                "interaction-surface",
            ],
            CommandSurfaceSchemaVersion: HostCommandSurfaceCatalog.SchemaVersion,
            CommandSurfaceFingerprint: HostCommandSurfaceCatalog.Fingerprint,
            CommandSurfaceCommandCount: HostCommandSurfaceCatalog.CommandEntries.Count);
    }

    private IReadOnlyList<HostAcceptedOperationStatusResponse> ListRecentAcceptedOperations(int maxCount = 5)
    {
        return acceptedOperationStore?.ListRecent(maxCount) ?? Array.Empty<HostAcceptedOperationStatusResponse>();
    }

    private JsonArray BuildAcceptedOperationsNode(int maxCount = 5)
    {
        return new JsonArray(ListRecentAcceptedOperations(maxCount).Select(operation => (JsonNode)new JsonObject
        {
            ["operation_id"] = operation.OperationId,
            ["command"] = operation.Command,
            ["operation_state"] = operation.OperationState,
            ["progress_marker"] = operation.ProgressMarker,
            ["progress_ordinal"] = operation.ProgressOrdinal,
            ["completed"] = operation.Completed,
            ["accepted_at"] = operation.AcceptedAt,
            ["updated_at"] = operation.UpdatedAt,
            ["progress_at"] = operation.ProgressAt,
            ["completed_at"] = operation.CompletedAt,
        }).ToArray());
    }
}
