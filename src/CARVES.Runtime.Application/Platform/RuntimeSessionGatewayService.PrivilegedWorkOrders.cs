using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeSessionGatewayService
{
    private const string PrivilegedCandidateLevel = "L5_PRIVILEGED";
    private const string PrivilegedTerminalState = "privileged_certificate_required";

    private SessionGatewayWorkOrderDryRunSurface BuildPrivilegedWorkOrderDryRun(
        ActorSessionRecord session,
        SessionGatewayMessageRequest request,
        SessionGatewayIntentEnvelopeSurface intentEnvelope,
        SessionGatewayExternalModuleReceiptEvaluationSurface externalModuleReceiptEvaluation)
    {
        var workOrderId = ResolvePrivilegedWorkOrderId(request);
        var operationId = ResolvePrivilegedOperationId(request) ?? "unknown_privileged_operation";
        var target = ResolvePrivilegedTarget(request, intentEnvelope.ObjectFocus);
        var actorRoles = ResolveTrustedPrivilegedActorRoles(session, operationId, target);
        var confirmation = ReadPrivilegedConfirmation(request.ClientCapabilities);
        var result = privilegedWorkOrderService.TryIssueCertificate(new PrivilegedWorkOrderRequest
        {
            WorkOrderId = workOrderId,
            ActorId = session.ActorSessionId,
            ActorRoles = actorRoles,
            OperationId = operationId,
            TargetKind = target.Kind,
            TargetId = target.Id,
            TargetHash = target.Hash,
            Confirmation = confirmation,
        });

        return new SessionGatewayWorkOrderDryRunSurface
        {
            WorkOrderId = workOrderId,
            ReceiptKind = result.CertificateIssued ? "privileged_receipt" : "blocked_receipt",
            AdmissionState = result.AdmissionState,
            UserVisibleLevel = PrivilegedCandidateLevel,
            TerminalState = result.CertificateIssued ? "privileged_certificate_issued" : PrivilegedTerminalState,
            RequiresWorkOrder = true,
            MayExecute = false,
            LeaseIssued = false,
            BoundObjects = intentEnvelope.ObjectFocus,
            BoundArtifacts = ResolveBoundArtifacts(request, requirePlanHash: false, requireAcceptanceContractHash: false),
            SubmitSemantics = BuildSubmitSemantics(),
            OperationRegistry = BuildOperationRegistry(),
            CapabilityLease = BuildNotRequiredCapabilityLease(),
            ResourceLease = BuildNotRequiredResourceLease(),
            ExternalModuleAdapters = BuildExternalModuleAdapterRegistry(),
            ExternalModuleReceiptEvaluation = externalModuleReceiptEvaluation,
            PrivilegedWorkOrder = ProjectPrivilegedWorkOrder(result, actorRoles, target),
            TransactionDryRun = BuildNotRequiredTransactionDryRun(),
            StopReasons = result.StopReasons,
            NextRequiredAction = ResolvePrivilegedNextRequiredAction(result),
            Summary = result.Summary,
        };
    }

    private static SessionGatewayPrivilegedWorkOrderSurface BuildNotRequiredPrivilegedWorkOrder()
    {
        return new SessionGatewayPrivilegedWorkOrderSurface
        {
            Mode = "not_required",
            ConfirmationState = "not_required",
            NaturalLanguageConfirmationAccepted = false,
            SecondConfirmationRequired = false,
        };
    }

    private static SessionGatewayPrivilegedWorkOrderSurface ProjectPrivilegedWorkOrder(
        PrivilegedWorkOrderResult result,
        IReadOnlyList<string> actorRoles,
        PrivilegedTarget target)
    {
        return new SessionGatewayPrivilegedWorkOrderSurface
        {
            Mode = "privileged",
            ConfirmationState = result.CertificateIssued ? "certificate_issued" : "blocked",
            OperationId = result.OperationId,
            OperationHash = result.Confirmation?.OperationHash,
            TargetKind = result.Certificate?.TargetKind ?? target.Kind,
            TargetId = result.Certificate?.TargetId ?? target.Id,
            TargetHash = result.Certificate?.TargetHash ?? target.Hash,
            RequiredRole = result.RequiredRole,
            ActorRoles = actorRoles,
            SecondConfirmationRequired = true,
            NaturalLanguageConfirmationAccepted = false,
            IrreversibilityAcknowledged = result.Confirmation?.IrreversibilityAcknowledged ?? false,
            IrreversibilityNotice = result.IrreversibilityNotice,
            ExpectedCertificateId = result.Confirmation?.ExpectedCertificateId,
            CertificateId = result.Certificate?.CertificateId,
            CertificatePath = result.CertificatePath,
            CertificateHash = result.Certificate?.CertificateHash,
            ExpiresAt = result.Confirmation?.ExpiresAtUtc,
            StopReasons = result.StopReasons,
        };
    }

    private static string ResolvePrivilegedNextRequiredAction(PrivilegedWorkOrderResult result)
    {
        if (result.CertificateIssued)
        {
            return "route_to_privileged_control_plane";
        }

        return result.StopReasons.Contains(PrivilegedWorkOrderService.RoleMissingStopReason, StringComparer.Ordinal)
            ? "obtain_trusted_privileged_actor"
            : "provide_structured_privileged_confirmation";
    }

    private IReadOnlyList<string> ResolveTrustedPrivilegedActorRoles(
        ActorSessionRecord session,
        string operationId,
        PrivilegedTarget target)
    {
        return privilegedActorRoleResolver.ResolveRoles(
            session,
            repoId,
            operationId,
            target.Kind,
            target.Id,
            target.Hash);
    }

    private static bool ContainsPrivilegedOperationText(string text)
    {
        return ResolvePrivilegedOperationIdFromText(text) is not null;
    }

    private static string? ResolvePrivilegedOperationId(SessionGatewayMessageRequest request)
    {
        return ReadPrivilegedString(request.ClientCapabilities, "privileged_operation_id", "operation_id")
            ?? ResolvePrivilegedOperationIdFromText(request.UserText);
    }

    private static string? ResolvePrivilegedOperationIdFromText(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (ContainsAny(normalized, "memory truth", "promote memory", "promote_memory", "记忆真相", "晋升 memory", "晋升记忆"))
        {
            return "promote_memory_truth";
        }

        if (ContainsAny(normalized, "authoritative codegraph", "codegraph truth", "refresh codegraph", "权威 codegraph", "代码图谱真相"))
        {
            return "refresh_authoritative_codegraph";
        }

        if (ContainsAny(normalized, "release", "publish", "tag release", "发布", "发行"))
        {
            return "release_channel";
        }

        if (ContainsAny(normalized, "delete", "remove permanently", "删除"))
        {
            return "delete_resource";
        }

        if (ContainsAny(normalized, "cleanup", "clean up", "prune", "清理"))
        {
            return "cleanup_resource";
        }

        return null;
    }

    private PrivilegedTarget ResolvePrivilegedTarget(
        SessionGatewayMessageRequest request,
        IReadOnlyList<SessionGatewayObjectFocusSurface> objectFocus)
    {
        var explicitTarget = ResolveExplicitPrivilegedTarget(request);
        if (explicitTarget is not null)
        {
            return explicitTarget;
        }

        var topFocus = objectFocus
            .Where(static item => item.Confidence > 0)
            .OrderByDescending(static item => item.Confidence)
            .FirstOrDefault(static item =>
                string.Equals(item.Kind, "card", StringComparison.Ordinal)
                || string.Equals(item.Kind, "taskgraph", StringComparison.Ordinal)
                || string.Equals(item.Kind, "task", StringComparison.Ordinal));
        if (topFocus is not null)
        {
            return BindPrivilegedTarget(topFocus.Kind, topFocus.Id);
        }

        return new PrivilegedTarget(string.Empty, string.Empty, string.Empty);
    }

    private PrivilegedTarget? ResolveExplicitPrivilegedTarget(SessionGatewayMessageRequest request)
    {
        if (!string.IsNullOrWhiteSpace(NormalizeOptional(request.TargetTaskId)))
        {
            return BindPrivilegedTarget("task", NormalizeOptional(request.TargetTaskId) ?? string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(NormalizeOptional(request.TargetTaskGraphId)))
        {
            return BindPrivilegedTarget("taskgraph", NormalizeOptional(request.TargetTaskGraphId) ?? string.Empty);
        }

        if (!string.IsNullOrWhiteSpace(NormalizeOptional(request.TargetCardId)))
        {
            return BindPrivilegedTarget("card", NormalizeOptional(request.TargetCardId) ?? string.Empty);
        }

        return null;
    }

    private PrivilegedTarget BindPrivilegedTarget(string kind, string id)
    {
        return new PrivilegedTarget(
            kind,
            id,
            TryResolvePrivilegedTargetHash(kind, id) ?? string.Empty);
    }

    private string? TryResolvePrivilegedTargetHash(string kind, string id)
    {
        var path = TryResolvePrivilegedTargetPath(kind, id);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return effectLedgerService.HashFile(path);
    }

    private string? TryResolvePrivilegedTargetPath(string kind, string id)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        if (string.Equals(kind, "card", StringComparison.Ordinal))
        {
            var planningCardPath = Path.Combine(paths.PlanningCardDraftsRoot, $"{id}.json");
            if (File.Exists(planningCardPath))
            {
                return planningCardPath;
            }

            var markdownCardPath = Path.Combine(paths.CardsRoot, $"{id}.md");
            return File.Exists(markdownCardPath) ? markdownCardPath : null;
        }

        if (string.Equals(kind, "taskgraph", StringComparison.Ordinal))
        {
            var taskGraphDraftPath = Path.Combine(paths.PlanningTaskGraphDraftsRoot, $"{id}.json");
            return File.Exists(taskGraphDraftPath) ? taskGraphDraftPath : null;
        }

        if (string.Equals(kind, "task", StringComparison.Ordinal))
        {
            var taskNodePath = Path.Combine(paths.TaskNodesRoot, $"{id}.json");
            return File.Exists(taskNodePath) ? taskNodePath : null;
        }

        return null;
    }

    private static PrivilegedSecondConfirmation? ReadPrivilegedConfirmation(JsonObject? capabilities)
    {
        if (capabilities?["privileged_confirmation"] is not JsonObject confirmation)
        {
            return null;
        }

        var expiresAtText = ReadPrivilegedString(confirmation, "expires_at", "expires_at_utc");
        return new PrivilegedSecondConfirmation
        {
            TargetKind = ReadPrivilegedString(confirmation, "target_kind") ?? string.Empty,
            TargetId = ReadPrivilegedString(confirmation, "target_id") ?? string.Empty,
            TargetHash = ReadPrivilegedString(confirmation, "target_hash") ?? string.Empty,
            OperationId = ReadPrivilegedString(confirmation, "operation_id") ?? string.Empty,
            OperationHash = ReadPrivilegedString(confirmation, "operation_hash") ?? string.Empty,
            ActorRole = ReadPrivilegedString(confirmation, "actor_role") ?? string.Empty,
            ExpiresAtUtc = DateTimeOffset.TryParse(expiresAtText, out var expiresAt)
                ? expiresAt
                : DateTimeOffset.MinValue,
            ExpectedCertificateId = ReadPrivilegedString(confirmation, "expected_certificate_id") ?? string.Empty,
            IrreversibilityAcknowledged = ReadPrivilegedBool(confirmation, "irreversibility_acknowledged"),
        };
    }

    private static string? ReadPrivilegedString(JsonObject? source, params string[] keys)
    {
        if (source is null)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (source.TryGetPropertyValue(key, out var node)
                && node is JsonValue value
                && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static bool ReadPrivilegedBool(JsonObject source, string key)
    {
        return source.TryGetPropertyValue(key, out var node)
            && node is JsonValue value
            && value.TryGetValue<bool>(out var result)
            && result;
    }

    private static string ResolvePrivilegedWorkOrderId(SessionGatewayMessageRequest request)
    {
        var messageId = NormalizeOptional(request.MessageId);
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return $"wopriv-{Guid.NewGuid():N}";
        }

        return $"wopriv-{SanitizePrivilegedId(messageId)}";
    }

    private static string SanitizePrivilegedId(string value)
    {
        return new string(value
            .Select(static item => char.IsLetterOrDigit(item) || item is '-' or '_' or '.' ? item : '-')
            .ToArray());
    }

    private sealed record PrivilegedTarget(string Kind, string Id, string Hash);
}
