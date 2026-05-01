namespace Carves.Runtime.Host;

internal static class GatewayActivityEventKinds
{
    public const string StoreContractVersion = "carves-gateway-activity-store.v1";
    public const string StoreRecordSchemaVersion = "carves-gateway-activity-store-record.v1";
    public const string StoreContractPath = "docs/runtime/gateway-activity-store-v1-contract.md";
    public const string StoreSchemaPath = "docs/contracts/gateway-activity-store-v1.schema.json";
    public const string StoreManifestSchemaPath = "docs/contracts/gateway-activity-segment-manifest-v1.schema.json";
    public const string StoreCheckpointSchemaPath = "docs/contracts/gateway-activity-manifest-checkpoint-v1.schema.json";
    public const string StoreTruthBoundary = "host_operational_evidence_not_governance_truth";
    public const string StoreAuthorityPosture = "operator_diagnosis_evidence_not_audit_truth";
    public const string StoreCompletenessPosture = "bounded_local_store_with_drop_telemetry_not_guaranteed_complete";
    public const string StoreOperatorUse = "diagnose_gateway_requests_and_feedback_only";
    public const bool StoreAuditTruth = false;
    public const string CatalogVersion = "carves-gateway-activity-events.v3";
    public const string CategoryAll = "all";
    public const string CategoryRequest = "request";
    public const string CategoryFeedback = "feedback";
    public const string CategoryCliInvoke = "cli_invoke";
    public const string CategoryRestRequest = "rest_request";
    public const string CategorySessionMessage = "session_message";
    public const string CategoryOperationFeedback = "operation_feedback";
    public const string CategoryRouteNotFound = "route_not_found";
    public const string CategoryGatewayRequest = "gateway_request";

    public const string GatewayRequest = "gateway-request";
    public const string GatewayRequestFailed = "gateway-request-failed";
    public const string InvokeRequest = "gateway-invoke-request";
    public const string InvokeResponse = "gateway-invoke-response";
    public const string InvokeAccepted = "gateway-invoke-accepted";
    public const string AcceptedOperationRunning = "gateway-accepted-operation-running";
    public const string AcceptedOperationCompleted = "gateway-accepted-operation-completed";
    public const string AcceptedOperationFailed = "gateway-accepted-operation-failed";
    public const string SessionCreateRequest = "gateway-session-create-request";
    public const string SessionCreateResponse = "gateway-session-create-response";
    public const string SessionMessageRequest = "gateway-session-message-request";
    public const string SessionMessageResponse = "gateway-session-message-response";
    public const string SessionEventsRequest = "gateway-session-events-request";
    public const string SessionEventsResponse = "gateway-session-events-response";
    public const string SessionReadRequest = "gateway-session-read-request";
    public const string SessionReadResponse = "gateway-session-read-response";
    public const string SessionOperationApproveRequest = "gateway-session-operation-approve-request";
    public const string SessionOperationApproveResponse = "gateway-session-operation-approve-response";
    public const string SessionOperationRejectRequest = "gateway-session-operation-reject-request";
    public const string SessionOperationRejectResponse = "gateway-session-operation-reject-response";
    public const string SessionOperationReplanRequest = "gateway-session-operation-replan-request";
    public const string SessionOperationReplanResponse = "gateway-session-operation-replan-response";
    public const string SessionOperationReadRequest = "gateway-session-operation-read-request";
    public const string SessionOperationReadResponse = "gateway-session-operation-read-response";
    public const string RouteNotFound = "gateway-route-not-found";

    public static readonly IReadOnlyList<string> All =
    [
        GatewayRequest,
        GatewayRequestFailed,
        InvokeRequest,
        InvokeResponse,
        InvokeAccepted,
        AcceptedOperationRunning,
        AcceptedOperationCompleted,
        AcceptedOperationFailed,
        SessionCreateRequest,
        SessionCreateResponse,
        SessionMessageRequest,
        SessionMessageResponse,
        SessionEventsRequest,
        SessionEventsResponse,
        SessionReadRequest,
        SessionReadResponse,
        SessionOperationApproveRequest,
        SessionOperationApproveResponse,
        SessionOperationRejectRequest,
        SessionOperationRejectResponse,
        SessionOperationReplanRequest,
        SessionOperationReplanResponse,
        SessionOperationReadRequest,
        SessionOperationReadResponse,
        RouteNotFound,
    ];

    public static readonly IReadOnlyList<string> AllCategories =
    [
        CategoryAll,
        CategoryRequest,
        CategoryFeedback,
        CategoryCliInvoke,
        CategoryRestRequest,
        CategorySessionMessage,
        CategoryOperationFeedback,
        CategoryRouteNotFound,
        CategoryGatewayRequest,
    ];

    public static readonly IReadOnlyList<string> StoreEnvelopeRequiredFields =
    [
        "schema_version",
        "event_id",
        "event",
        "category",
        "timestamp",
        "outcome",
        "fields",
    ];

    public static readonly IReadOnlyList<string> StoreStableQueryFields =
    [
        "since_minutes",
        "category",
        "event",
        "command",
        "path",
        "operation_id",
        "session_id",
        "message_id",
        "request_id",
    ];

    public static readonly IReadOnlyList<string> StoreForbiddenFieldFamilies =
    [
        "raw_prompt_text",
        "raw_message_text",
        "raw_model_output",
        "request_body",
        "response_body",
        "source_diff",
        "credential",
        "authorization_header",
        "cookie",
        "api_key",
        "secret_value",
    ];

    public static readonly IReadOnlyList<string> StoreNotFor =
    [
        "governance_truth",
        "approval_truth",
        "worker_dispatch_authority",
        "memory_writeback_truth",
        "compliance_audit_truth",
    ];

    public static readonly IReadOnlyList<string> StoreRouteCoverageFamilies =
    [
        "/handshake",
        "/invoke",
        "/sessions create",
        "/sessions/{session_id}/messages",
        "/sessions/{session_id}/events",
        "/sessions/{session_id} read",
        "/sessions/{session_id}/operations/{operation_id}/approve",
        "/sessions/{session_id}/operations/{operation_id}/reject",
        "/sessions/{session_id}/operations/{operation_id}/replan",
        "/sessions/{session_id}/operations/{operation_id} read",
        "unknown route",
        "unhandled exception",
    ];

    private static readonly HashSet<string> AllSet = new(All, StringComparer.Ordinal);
    private static readonly HashSet<string> AllCategorySet = new(AllCategories, StringComparer.Ordinal);
    private static readonly HashSet<string> RequestSet = new(
        [
            InvokeRequest,
            SessionCreateRequest,
            SessionMessageRequest,
            SessionEventsRequest,
            SessionReadRequest,
            SessionOperationApproveRequest,
            SessionOperationRejectRequest,
            SessionOperationReplanRequest,
            SessionOperationReadRequest,
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> CliInvokeSet = new(
        [
            InvokeRequest,
            InvokeResponse,
            InvokeAccepted,
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> SessionMessageSet = new(
        [
            SessionMessageRequest,
            SessionMessageResponse,
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> OperationFeedbackSet = new(
        [
            AcceptedOperationRunning,
            AcceptedOperationCompleted,
            AcceptedOperationFailed,
            SessionOperationApproveResponse,
            SessionOperationRejectResponse,
            SessionOperationReplanResponse,
            SessionOperationReadResponse,
        ],
        StringComparer.Ordinal);

    public static bool IsKnown(string eventKind)
    {
        return AllSet.Contains(eventKind);
    }

    public static bool IsRequest(string eventKind)
    {
        return RequestSet.Contains(eventKind);
    }

    public static bool IsFeedback(string eventKind)
    {
        return IsKnown(eventKind)
               && !IsRequest(eventKind)
               && !string.Equals(eventKind, GatewayRequest, StringComparison.Ordinal)
               && !string.Equals(eventKind, GatewayRequestFailed, StringComparison.Ordinal);
    }

    public static bool IsCliInvoke(string eventKind)
    {
        return CliInvokeSet.Contains(eventKind);
    }

    public static bool IsRestRequest(string eventKind)
    {
        return RequestSet.Contains(eventKind) && !string.Equals(eventKind, InvokeRequest, StringComparison.Ordinal);
    }

    public static bool IsSessionMessage(string eventKind)
    {
        return SessionMessageSet.Contains(eventKind);
    }

    public static bool IsOperationFeedback(string eventKind)
    {
        return OperationFeedbackSet.Contains(eventKind);
    }

    public static bool IsKnownCategory(string category)
    {
        return AllCategorySet.Contains(category);
    }

    public static bool IsCategoryMatch(string eventKind, string category)
    {
        return category switch
        {
            CategoryAll => true,
            CategoryRequest => IsRequest(eventKind),
            CategoryFeedback => IsFeedback(eventKind),
            CategoryCliInvoke => IsCliInvoke(eventKind),
            CategoryRestRequest => IsRestRequest(eventKind),
            CategorySessionMessage => IsSessionMessage(eventKind),
            CategoryOperationFeedback => IsOperationFeedback(eventKind),
            CategoryRouteNotFound => string.Equals(eventKind, RouteNotFound, StringComparison.Ordinal),
            CategoryGatewayRequest => string.Equals(eventKind, GatewayRequest, StringComparison.Ordinal)
                                      || string.Equals(eventKind, GatewayRequestFailed, StringComparison.Ordinal),
            _ => false,
        };
    }
}
