using System.Text.Json.Nodes;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed record LocalHostDescriptor(
    string HostId,
    string MachineId,
    string RepoRoot,
    string BaseUrl,
    int Port,
    int ProcessId,
    string RuntimeDirectory,
    string? DeploymentDirectory,
    string? ExecutablePath,
    DateTimeOffset StartedAt,
    string Version,
    string Stage);

internal sealed record HostHandshakeSummary(
    bool HostRunning,
    string Version,
    string StandardVersion,
    string Stage,
    string HostSessionId,
    long UptimeSeconds,
    string BaseUrl,
    string DashboardUrl,
    string RuntimeDirectory,
    string? DeploymentDirectory,
    string? ExecutablePath,
    int RepoCount,
    int AttachedRepoCount,
    int ActiveSessionCount,
    string PlannerState,
    int WorkerCount,
    int ActiveWorkerCount,
    int ActorSessionCount,
    int OperatorOsEventCount,
    bool Rehydrated,
    int PendingApprovalCount,
    int RecentIncidentCount,
    int StaleMarkerCount,
    int PausedRuntimeCount,
    string RehydrationSummary,
    string HostControlState,
    string HostControlAction,
    string? HostControlReason,
    DateTimeOffset? HostControlAt,
    string ProtocolMode,
    string ConversationPhase,
    string IntentState,
    string PromptKernel,
    string ProjectUnderstandingState,
    IReadOnlyList<string> Capabilities,
    string? CommandSurfaceSchemaVersion = null,
    string? CommandSurfaceFingerprint = null,
    int CommandSurfaceCommandCount = 0);

internal sealed record HostDiscoveryResult(
    bool HostRunning,
    string Message,
    HostHandshakeSummary? Summary,
    LocalHostDescriptor? Descriptor,
    HostRuntimeSnapshot? Snapshot);

internal sealed record HostCommandRequest(
    string Command,
    IReadOnlyList<string> Arguments,
    string? RepoRoot = null,
    bool PreferAcceptedOperationPolling = false,
    string? OperationId = null);

internal sealed record HostCommandResponse(
    int ExitCode,
    IReadOnlyList<string> Lines,
    bool Accepted = false,
    bool Completed = true,
    string? OperationId = null,
    string? OperationState = null,
    DateTimeOffset? UpdatedAt = null,
    string? ProgressMarker = null,
    int? ProgressOrdinal = null,
    DateTimeOffset? ProgressAt = null);

internal sealed record HostAcceptedOperationStatusResponse(
    string OperationId,
    string Command,
    string OperationState,
    bool Completed,
    int? ExitCode,
    IReadOnlyList<string> Lines,
    DateTimeOffset AcceptedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    string ProgressMarker,
    int ProgressOrdinal,
    DateTimeOffset ProgressAt);

internal sealed record HostStopRequest(string Reason, bool Force);

internal sealed record HostControlRequest(string Action, string Reason);

internal sealed record AgentRequestEnvelope(
    string RequestId,
    string OperationClass,
    string Operation,
    string? TargetId,
    string? ActorIdentity,
    string? ActorSessionId,
    JsonObject? Arguments,
    JsonNode? Payload);

internal sealed record AgentResponseEnvelope(
    bool Accepted,
    string Outcome,
    string Message,
    string? ActorSessionId,
    JsonNode? Payload,
    string? RequestId = null,
    string? OperationClass = null,
    string? Operation = null,
    string? TargetId = null,
    DateTimeOffset? ReceivedAt = null,
    DateTimeOffset? RespondedAt = null);
