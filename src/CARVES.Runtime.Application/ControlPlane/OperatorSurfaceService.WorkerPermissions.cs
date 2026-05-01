using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult WorkerApprovals()
    {
        var requests = workerPermissionOrchestrationService.ListPendingRequests();
        var lines = new List<string> { $"Pending permission requests: {requests.Count}" };
        if (requests.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var request in requests)
        {
            lines.Add($"- {request.PermissionRequestId}");
            lines.Add($"  task: {request.TaskId}");
            lines.Add($"  backend/provider: {request.BackendId}/{request.ProviderId}");
            lines.Add($"  kind/risk: {request.Kind}/{request.RiskLevel}");
            lines.Add($"  scope: {request.ScopeSummary}");
            lines.Add($"  resource: {request.ResourcePath ?? "(none)"}");
            lines.Add($"  summary: {request.Summary}");
            lines.Add($"  recommended: {request.RecommendedDecision} ({request.RecommendedReasonCode})");
            lines.Add($"  reason: {request.RecommendedReason}");
            lines.Add($"  consequence: {request.RecommendedConsequenceSummary}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult WorkerApprovalPolicy(string? repoId = null)
    {
        var requests = workerPermissionOrchestrationService.ListPendingRequests();
        var lines = approvalPolicyEngine.DescribePolicySummary(repoId).ToList();
        lines.Insert(0, "Worker approval policy:");
        lines.Add($"Pending approval requests: {requests.Count}");
        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult WorkerApprove(string permissionRequestId, string actorIdentity)
    {
        var actorKind = string.Equals(actorIdentity, "operator", StringComparison.OrdinalIgnoreCase) ? ActorSessionKind.Operator : ActorSessionKind.Agent;
        var actorSession = EnsureControlActorSession(
            actorKind,
            actorIdentity,
            $"Approval requested for permission '{permissionRequestId}'.",
            OwnershipScope.ApprovalDecision,
            permissionRequestId,
            operationClass: "worker",
            operation: "approve");
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.ApprovalDecision,
            permissionRequestId,
            $"Approval requested for permission '{permissionRequestId}'.",
            () =>
            {
                var request = workerPermissionOrchestrationService.ResolveApprove(permissionRequestId, actorIdentity);
                return OperatorCommandResult.Success(
                    $"Approved permission request {request.PermissionRequestId}.",
                    $"Task: {request.TaskId}",
                    $"Provider: {request.ProviderId}",
                    $"Kind: {request.Kind}",
                    $"Consequence: {request.ConsequenceSummary ?? "(none)"}");
            });
    }

    public OperatorCommandResult WorkerDeny(string permissionRequestId, string actorIdentity)
    {
        var actorKind = string.Equals(actorIdentity, "operator", StringComparison.OrdinalIgnoreCase) ? ActorSessionKind.Operator : ActorSessionKind.Agent;
        var actorSession = EnsureControlActorSession(
            actorKind,
            actorIdentity,
            $"Denial requested for permission '{permissionRequestId}'.",
            OwnershipScope.ApprovalDecision,
            permissionRequestId,
            operationClass: "worker",
            operation: "deny");
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.ApprovalDecision,
            permissionRequestId,
            $"Denial requested for permission '{permissionRequestId}'.",
            () =>
            {
                var request = workerPermissionOrchestrationService.ResolveDeny(permissionRequestId, actorIdentity);
                return OperatorCommandResult.Success(
                    $"Denied permission request {request.PermissionRequestId}.",
                    $"Task: {request.TaskId}",
                    $"Provider: {request.ProviderId}",
                    $"Kind: {request.Kind}",
                    $"Consequence: {request.ConsequenceSummary ?? "(none)"}");
            });
    }

    public OperatorCommandResult WorkerTimeout(string permissionRequestId, string actorIdentity)
    {
        var actorKind = string.Equals(actorIdentity, "operator", StringComparison.OrdinalIgnoreCase) ? ActorSessionKind.Operator : ActorSessionKind.Agent;
        var actorSession = EnsureControlActorSession(
            actorKind,
            actorIdentity,
            $"Timeout requested for permission '{permissionRequestId}'.",
            OwnershipScope.ApprovalDecision,
            permissionRequestId,
            operationClass: "worker",
            operation: "timeout");
        return ResolveArbitratedAction(
            actorSession,
            OwnershipScope.ApprovalDecision,
            permissionRequestId,
            $"Timeout requested for permission '{permissionRequestId}'.",
            () =>
            {
                var request = workerPermissionOrchestrationService.ResolveTimeout(permissionRequestId, actorIdentity);
                return OperatorCommandResult.Success(
                    $"Timed out permission request {request.PermissionRequestId}.",
                    $"Task: {request.TaskId}",
                    $"Provider: {request.ProviderId}",
                    $"Kind: {request.Kind}",
                    $"Consequence: {request.ConsequenceSummary ?? "(none)"}");
            });
    }

    public OperatorCommandResult WorkerPermissionAudit(string? taskId, string? permissionRequestId)
    {
        var records = workerPermissionOrchestrationService.LoadAudit(taskId, permissionRequestId);
        var lines = new List<string> { $"Permission audit records: {records.Count}" };
        if (records.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var record in records.Take(50))
        {
            lines.Add($"- {record.EventKind} [{record.OccurredAt:O}] request={record.PermissionRequestId} task={record.TaskId} provider={record.ProviderId} decision={record.Decision?.ToString() ?? "(none)"} actor={record.ActorKind}:{record.ActorIdentity} reason={record.ReasonCode}");
            lines.Add($"  consequence: {record.ConsequenceSummary}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult ApiWorkerPermissionRequests()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetPendingWorkerPermissionRequests()));
    }

    public OperatorCommandResult ApiWorkerPermissionAudit(string? taskId, string? permissionRequestId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetWorkerPermissionAudit(taskId, permissionRequestId)));
    }
}
