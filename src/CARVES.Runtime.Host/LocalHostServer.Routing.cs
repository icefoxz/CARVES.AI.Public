using System.Net;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostServer
{
    private void HandleContext(HttpListenerContext context)
    {
        var previousRequestId = CurrentGatewayRequestId.Value;
        CurrentGatewayRequestId.Value = CreateGatewayRequestId();
        try
        {
            hostState.RecordRequest();
            SaveSnapshot(HostRuntimeSnapshotState.Live, "Resident host processed a request.");
            var path = context.Request.Url?.AbsolutePath ?? "/";
            WriteGatewayEvent(
                GatewayActivityEventKinds.GatewayRequest,
                $"method={context.Request.HttpMethod}",
                $"path={path}",
                $"remote={context.Request.RemoteEndPoint}");
            try
            {
                if (string.Equals(path, "/", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    var rootStatus = BuildRootStatus();
                    if (RequestPrefersHtml(context.Request))
                    {
                        WriteHtml(context.Response, RenderRootStatusHtml(rootStatus));
                    }
                    else
                    {
                        WriteJson(context.Response, rootStatus);
                    }

                    return;
                }

                if (string.Equals(path, "/handshake", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(context.Response, hostSurfaceService.BuildHandshake(hostState));
                    return;
                }

            if (string.Equals(path, "/invoke", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var request = ReadJson<HostCommandRequest>(context.Request);
                WriteGatewayEvent(
                    GatewayActivityEventKinds.InvokeRequest,
                    GatewayField("command", request.Command),
                    GatewayField("arguments", string.Join(' ', request.Arguments)),
                    GatewayField("repo_root", string.IsNullOrWhiteSpace(request.RepoRoot) ? services.Paths.RepoRoot : request.RepoRoot),
                    GatewayField("accepted_polling", request.PreferAcceptedOperationPolling));
                var scoped = ResolveScopedServices(request);
                if (!scoped.Allowed)
                {
                    var denied = new HostCommandResponse(1, [scoped.Message ?? "Host rejected the request."]);
                    WriteGatewayEvent(
                        GatewayActivityEventKinds.InvokeResponse,
                        GatewayField("command", request.Command),
                        GatewayField("exit_code", denied.ExitCode),
                        GatewayField("line_count", denied.Lines.Count),
                        GatewayField("rejected", true));
                    WriteJson(context.Response, denied);
                    return;
                }

                var commandArguments = NormalizeCommandArguments(request, scoped.Services!);
                var capability = HostCommandRoutingCatalog.ResolveCapability(request.Command, commandArguments);
                if (request.PreferAcceptedOperationPolling
                    && string.Equals(capability, "control-plane-mutation", StringComparison.OrdinalIgnoreCase))
                {
                    var accepted = acceptedOperationStore.Accept(request.Command, request.OperationId);
                    WriteGatewayEvent(
                        GatewayActivityEventKinds.InvokeAccepted,
                        GatewayField("command", request.Command),
                        GatewayField("capability", capability),
                        GatewayField("operation_id", accepted.OperationId),
                        GatewayField("progress", accepted.ProgressMarker));
                    WriteJson(
                        context.Response,
                        new HostCommandResponse(
                            ExitCode: 0,
                            Lines: [$"Accepted host operation {accepted.OperationId} for {request.Command}."],
                            Accepted: true,
                            Completed: false,
                            OperationId: accepted.OperationId,
                            OperationState: accepted.OperationState,
                            UpdatedAt: accepted.UpdatedAt,
                            ProgressMarker: accepted.ProgressMarker,
                            ProgressOrdinal: accepted.ProgressOrdinal,
                            ProgressAt: accepted.ProgressAt));
                    var acceptedRequestId = CurrentGatewayRequestId.Value ?? string.Empty;
                    QueueBackgroundWork(() => ExecuteAcceptedOperation(accepted.OperationId, scoped.Services!, request.Command, commandArguments, acceptedRequestId));
                    return;
                }

                var result = LocalHostCommandDispatcher.Dispatch(scoped.Services!, request.Command, commandArguments);
                WriteGatewayEvent(
                    GatewayActivityEventKinds.InvokeResponse,
                    GatewayField("command", request.Command),
                    GatewayField("capability", capability),
                    GatewayField("exit_code", result.ExitCode),
                    GatewayField("line_count", result.Lines.Count));
                WriteJson(context.Response, new HostCommandResponse(result.ExitCode, result.Lines));
                return;
            }

            if (path.StartsWith("/operations/", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var operationId = Uri.UnescapeDataString(path["/operations/".Length..]);
                WriteAcceptedOperation(context.Response, operationId);
                return;
            }

            if (string.Equals(path, "/api/session-gateway/v1/sessions", StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var request = ReadJson<SessionGatewaySessionCreateRequest>(context.Request);
                WriteGatewaySessionActivity(
                    GatewayActivityEventKinds.SessionCreateRequest,
                    GatewayField("actor_identity", request.ActorIdentity),
                    GatewayField("requested_mode", request.RequestedMode),
                    GatewayField("client_repo_root", ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot)));
                if (!TryResolveSessionGatewaySurface(
                        context,
                        ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot),
                        out var sessionGatewaySurface))
                {
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionCreateResponse,
                        GatewayField("status", context.Response.StatusCode),
                        GatewayField("rejected", true));
                    return;
                }

                var session = sessionGatewaySurface.CreateSessionGatewaySession(request);
                WriteGatewaySessionActivity(
                    GatewayActivityEventKinds.SessionCreateResponse,
                    GatewayField("status", 200),
                    GatewayField("session_id", session.SessionId),
                    GatewayField("actor_identity", session.ActorIdentity),
                    GatewayField("event_count", session.EventCount));
                WriteJson(context.Response, session);
                return;
            }

            if (string.Equals(path, "/session-gateway/v1/shell", StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                WriteHtml(context.Response, hostSurfaceService.RenderSessionGatewayShellHtml());
                return;
            }

            if (path.StartsWith("/api/session-gateway/v1/sessions/", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = path["/api/session-gateway/v1/sessions/".Length..];
                if (relativePath.EndsWith("/messages", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    var sessionId = Uri.UnescapeDataString(relativePath[..^"/messages".Length]);
                    var request = ReadJson<SessionGatewayMessageRequest>(context.Request);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionMessageRequest,
                        GatewayField("session_id", sessionId),
                        GatewayField("message_id", request.MessageId),
                        GatewayField("requested_mode", request.RequestedMode),
                        GatewayField("target_task_id", request.TargetTaskId),
                        GatewayField("target_card_id", request.TargetCardId),
                        GatewayField("user_text_length", request.UserText.Length),
                        GatewayField("client_repo_root", ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot)));
                    if (!TryResolveSessionGatewaySurface(
                            context,
                            ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot),
                            out var sessionGatewaySurface))
                    {
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionMessageResponse,
                            GatewayField("status", context.Response.StatusCode),
                            GatewayField("session_id", sessionId),
                            GatewayField("message_id", request.MessageId),
                            GatewayField("rejected", true));
                        return;
                    }

                    if (sessionGatewaySurface.TryGetSessionGatewaySession(sessionId) is null)
                    {
                        context.Response.StatusCode = 404;
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionMessageResponse,
                            GatewayField("status", 404),
                            GatewayField("session_id", sessionId),
                            GatewayField("message_id", request.MessageId),
                            GatewayField("not_found", true));
                        WriteJson(context.Response, new JsonObject { ["error"] = $"Session Gateway session '{sessionId}' was not found." });
                        return;
                    }

                    var turn = sessionGatewaySurface.SubmitSessionGatewayMessage(sessionId, request);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionMessageResponse,
                        GatewayField("status", 200),
                        GatewayField("session_id", turn.SessionId),
                        GatewayField("message_id", turn.MessageId),
                        GatewayField("turn_id", turn.TurnId),
                        GatewayField("classified_intent", turn.ClassifiedIntent),
                        GatewayField("operation_id", turn.OperationId),
                        GatewayField("accepted", turn.Accepted));
                    WriteJson(context.Response, turn);
                    return;
                }

                if (relativePath.EndsWith("/events", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    var sessionId = Uri.UnescapeDataString(relativePath[..^"/events".Length]);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionEventsRequest,
                        GatewayField("session_id", sessionId),
                        GatewayField("client_repo_root", ResolveSessionGatewayClientRepoRoot(context.Request)));
                    if (!TryResolveSessionGatewaySurface(
                            context,
                            ResolveSessionGatewayClientRepoRoot(context.Request),
                            out var sessionGatewaySurface))
                    {
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionEventsResponse,
                            GatewayField("status", context.Response.StatusCode),
                            GatewayField("session_id", sessionId),
                            GatewayField("rejected", true));
                        return;
                    }

                    if (sessionGatewaySurface.TryGetSessionGatewaySession(sessionId) is null)
                    {
                        context.Response.StatusCode = 404;
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionEventsResponse,
                            GatewayField("status", 404),
                            GatewayField("session_id", sessionId),
                            GatewayField("not_found", true));
                        WriteJson(context.Response, new JsonObject { ["error"] = $"Session Gateway session '{sessionId}' was not found." });
                        return;
                    }

                    var events = sessionGatewaySurface.GetSessionGatewayEvents(sessionId);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionEventsResponse,
                        GatewayField("status", 200),
                        GatewayField("session_id", events.SessionId),
                        GatewayField("event_count", events.Events.Count));
                    WriteJson(context.Response, events);
                    return;
                }

                if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    var sessionId = Uri.UnescapeDataString(relativePath);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionReadRequest,
                        GatewayField("session_id", sessionId),
                        GatewayField("client_repo_root", ResolveSessionGatewayClientRepoRoot(context.Request)));
                    if (!TryResolveSessionGatewaySurface(
                            context,
                            ResolveSessionGatewayClientRepoRoot(context.Request),
                            out var sessionGatewaySurface))
                    {
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionReadResponse,
                            GatewayField("status", context.Response.StatusCode),
                            GatewayField("session_id", sessionId),
                            GatewayField("rejected", true));
                        return;
                    }

                    var session = sessionGatewaySurface.TryGetSessionGatewaySession(sessionId);
                    if (session is null)
                    {
                        context.Response.StatusCode = 404;
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionReadResponse,
                            GatewayField("status", 404),
                            GatewayField("session_id", sessionId),
                            GatewayField("not_found", true));
                        WriteJson(context.Response, new JsonObject { ["error"] = $"Session Gateway session '{sessionId}' was not found." });
                        return;
                    }

                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionReadResponse,
                        GatewayField("status", 200),
                        GatewayField("session_id", session.SessionId),
                        GatewayField("event_count", session.EventCount),
                        GatewayField("latest_intent", session.LatestClassifiedIntent));
                    WriteJson(context.Response, session);
                    return;
                }
            }

            if (path.StartsWith("/api/session-gateway/v1/operations/", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = path["/api/session-gateway/v1/operations/".Length..];
                if (relativePath.EndsWith("/approve", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    var operationId = Uri.UnescapeDataString(relativePath[..^"/approve".Length]);
                    var request = ReadJson<SessionGatewayOperationMutationRequest>(context.Request);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionOperationApproveRequest,
                        GatewayField("operation_id", operationId),
                        GatewayField("reason_length", request.Reason.Length),
                        GatewayField("client_repo_root", ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot)));
                    if (!TryResolveSessionGatewaySurface(
                            context,
                            ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot),
                            out var sessionGatewaySurface))
                    {
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionOperationApproveResponse,
                            GatewayField("status", context.Response.StatusCode),
                            GatewayField("operation_id", operationId),
                            GatewayField("rejected", true));
                        return;
                    }

                    var operation = sessionGatewaySurface.ApproveSessionGatewayOperation(operationId, request);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionOperationApproveResponse,
                        GatewayField("status", 200),
                        GatewayField("operation_id", operation.OperationId),
                        GatewayField("session_id", operation.SessionId),
                        GatewayField("requested_action", operation.RequestedAction),
                        GatewayField("operation_state", operation.OperationState),
                        GatewayField("completed", operation.Completed));
                    WriteJson(context.Response, operation);
                    return;
                }

                if (relativePath.EndsWith("/reject", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    var operationId = Uri.UnescapeDataString(relativePath[..^"/reject".Length]);
                    var request = ReadJson<SessionGatewayOperationMutationRequest>(context.Request);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionOperationRejectRequest,
                        GatewayField("operation_id", operationId),
                        GatewayField("reason_length", request.Reason.Length),
                        GatewayField("client_repo_root", ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot)));
                    if (!TryResolveSessionGatewaySurface(
                            context,
                            ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot),
                            out var sessionGatewaySurface))
                    {
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionOperationRejectResponse,
                            GatewayField("status", context.Response.StatusCode),
                            GatewayField("operation_id", operationId),
                            GatewayField("rejected", true));
                        return;
                    }

                    var operation = sessionGatewaySurface.RejectSessionGatewayOperation(operationId, request);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionOperationRejectResponse,
                        GatewayField("status", 200),
                        GatewayField("operation_id", operation.OperationId),
                        GatewayField("session_id", operation.SessionId),
                        GatewayField("requested_action", operation.RequestedAction),
                        GatewayField("operation_state", operation.OperationState),
                        GatewayField("completed", operation.Completed));
                    WriteJson(context.Response, operation);
                    return;
                }

                if (relativePath.EndsWith("/replan", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                {
                    var operationId = Uri.UnescapeDataString(relativePath[..^"/replan".Length]);
                    var request = ReadJson<SessionGatewayOperationMutationRequest>(context.Request);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionOperationReplanRequest,
                        GatewayField("operation_id", operationId),
                        GatewayField("reason_length", request.Reason.Length),
                        GatewayField("client_repo_root", ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot)));
                    if (!TryResolveSessionGatewaySurface(
                            context,
                            ResolveSessionGatewayClientRepoRoot(context.Request, request.ClientRepoRoot),
                            out var sessionGatewaySurface))
                    {
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionOperationReplanResponse,
                            GatewayField("status", context.Response.StatusCode),
                            GatewayField("operation_id", operationId),
                            GatewayField("rejected", true));
                        return;
                    }

                    var operation = sessionGatewaySurface.ReplanSessionGatewayOperation(operationId, request);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionOperationReplanResponse,
                        GatewayField("status", 200),
                        GatewayField("operation_id", operation.OperationId),
                        GatewayField("session_id", operation.SessionId),
                        GatewayField("requested_action", operation.RequestedAction),
                        GatewayField("operation_state", operation.OperationState),
                        GatewayField("completed", operation.Completed));
                    WriteJson(context.Response, operation);
                    return;
                }

                if (string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    var operationId = Uri.UnescapeDataString(relativePath);
                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionOperationReadRequest,
                        GatewayField("operation_id", operationId),
                        GatewayField("client_repo_root", ResolveSessionGatewayClientRepoRoot(context.Request)));
                    if (!TryResolveSessionGatewaySurface(
                            context,
                            ResolveSessionGatewayClientRepoRoot(context.Request),
                            out var sessionGatewaySurface))
                    {
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionOperationReadResponse,
                            GatewayField("status", context.Response.StatusCode),
                            GatewayField("operation_id", operationId),
                            GatewayField("rejected", true));
                        return;
                    }

                    var operation = sessionGatewaySurface.TryGetSessionGatewayOperation(operationId);
                    if (operation is null)
                    {
                        context.Response.StatusCode = 404;
                        WriteGatewaySessionActivity(
                            GatewayActivityEventKinds.SessionOperationReadResponse,
                            GatewayField("status", 404),
                            GatewayField("operation_id", operationId),
                            GatewayField("not_found", true));
                        WriteJson(context.Response, new JsonObject { ["error"] = $"Session Gateway operation '{operationId}' was not found." });
                        return;
                    }

                    WriteGatewaySessionActivity(
                        GatewayActivityEventKinds.SessionOperationReadResponse,
                        GatewayField("status", 200),
                        GatewayField("operation_id", operation.OperationId),
                        GatewayField("session_id", operation.SessionId),
                        GatewayField("requested_action", operation.RequestedAction),
                        GatewayField("operation_state", operation.OperationState),
                        GatewayField("completed", operation.Completed),
                        GatewayField("exit_code", operation.ExitCode));
                    WriteJson(context.Response, operation);
                    return;
                }
            }

            if (string.Equals(path, "/agent", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var request = ReadJson<AgentRequestEnvelope>(context.Request);
                WriteJson(context.Response, hostSurfaceService.HandleAgent(request));
                return;
            }

            if (string.Equals(path, "/dashboard", StringComparison.OrdinalIgnoreCase))
            {
                WriteHtml(context.Response, hostSurfaceService.RenderDashboardHtml(hostState));
                return;
            }

            if (string.Equals(path, "/dashboard/summary", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, hostSurfaceService.BuildDashboardSummary(hostState));
                return;
            }

            if (string.Equals(path, "/workbench", StringComparison.OrdinalIgnoreCase))
            {
                WriteHtml(context.Response, hostSurfaceService.RenderWorkbenchOverviewHtml(hostState));
                return;
            }

            if (path.StartsWith("/workbench/card/", StringComparison.OrdinalIgnoreCase))
            {
                WriteHtml(context.Response, hostSurfaceService.RenderWorkbenchCardHtml(hostState, Uri.UnescapeDataString(path["/workbench/card/".Length..])));
                return;
            }

            if (path.StartsWith("/workbench/task/", StringComparison.OrdinalIgnoreCase))
            {
                WriteHtml(context.Response, hostSurfaceService.RenderWorkbenchTaskHtml(hostState, Uri.UnescapeDataString(path["/workbench/task/".Length..])));
                return;
            }

            if (string.Equals(path, "/workbench/review", StringComparison.OrdinalIgnoreCase))
            {
                WriteHtml(context.Response, hostSurfaceService.RenderWorkbenchReviewHtml(hostState));
                return;
            }

            if (string.Equals(path, "/workbench/action", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var form = ReadForm(context.Request);
                var actionId = form.TryGetValue("action", out var actionValue) ? actionValue : string.Empty;
                var targetId = form.TryGetValue("target_id", out var targetValue) ? targetValue : null;
                var reason = form.TryGetValue("reason", out var reasonValue) ? reasonValue : null;
                var returnPath = form.TryGetValue("return_path", out var returnValue) && !string.IsNullOrWhiteSpace(returnValue)
                    ? returnValue
                    : "/workbench/review";
                var result = hostSurfaceService.ExecuteWorkbenchAction(actionId, targetId, reason);
                WriteHtml(context.Response, hostSurfaceService.RenderWorkbenchActionResultHtml(hostState, actionId, targetId, reason, result, returnPath));
                return;
            }

            if (path.StartsWith("/inspect/card/", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, hostSurfaceService.BuildCardInspect(Uri.UnescapeDataString(path["/inspect/card/".Length..])));
                return;
            }

            if (path.StartsWith("/inspect/task/", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, hostSurfaceService.BuildTaskInspect(Uri.UnescapeDataString(path["/inspect/task/".Length..])));
                return;
            }

            if (string.Equals(path, "/discuss/context", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, hostSurfaceService.BuildDiscussionContext());
                return;
            }

            if (string.Equals(path, "/discuss/brief-preview", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, hostSurfaceService.BuildDiscussionBriefPreview());
                return;
            }

            if (string.Equals(path, "/discuss/planner", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, hostSurfaceService.BuildDiscussionPlanner());
                return;
            }

            if (string.Equals(path, "/discuss/blocked", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, hostSurfaceService.BuildDiscussionBlocked());
                return;
            }

            if (path.StartsWith("/discuss/card/", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, hostSurfaceService.BuildDiscussionCard(Uri.UnescapeDataString(path["/discuss/card/".Length..])));
                return;
            }

            if (path.StartsWith("/discuss/task/", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, hostSurfaceService.BuildDiscussionTask(Uri.UnescapeDataString(path["/discuss/task/".Length..])));
                return;
            }

            if (string.Equals(path, "/host/stop", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var request = ReadJson<HostStopRequest>(context.Request);
                hostSessionService.Stop(request.Reason);
                if (services.DevLoopService.GetSession() is not null)
                {
                    services.DevLoopService.StopSession(request.Reason);
                }
                WriteJson(context.Response, new JsonObject
                {
                    ["accepted"] = true,
                    ["message"] = "Host stop requested.",
                });
                QueueBackgroundWork(() =>
                {
                    SaveSnapshot(HostRuntimeSnapshotState.Live, $"Host stop requested: {request.Reason}");
                    BlockingHostWait.Delay(TimeSpan.FromMilliseconds(150));
                    RequestShutdown();
                });
                return;
            }

            if (string.Equals(path, "/host/control", StringComparison.OrdinalIgnoreCase) && string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var request = ReadJson<HostControlRequest>(context.Request);
                var result = request.Action.ToLowerInvariant() switch
                {
                    "pause" => services.OperatorSurfaceService.PauseHost(request.Reason),
                    "resume" => services.OperatorSurfaceService.ResumeHost(request.Reason),
                    _ => OperatorCommandResult.Failure("Usage: host <pause|resume> <reason...>"),
                };
                SaveSnapshot(HostRuntimeSnapshotState.Live, $"Host control {request.Action}: {request.Reason}");
                WriteJson(context.Response, new HostCommandResponse(result.ExitCode, result.Lines));
                return;
            }

                context.Response.StatusCode = 404;
                WriteGatewayEvent(
                    GatewayActivityEventKinds.RouteNotFound,
                    GatewayField("method", context.Request.HttpMethod),
                    GatewayField("path", path),
                    GatewayField("status", 404));
                WriteJson(context.Response, new JsonObject { ["error"] = $"Unknown host route '{path}'." });
            }
            catch (Exception exception)
            {
                context.Response.StatusCode = 500;
                WriteGatewayEvent(
                    GatewayActivityEventKinds.GatewayRequestFailed,
                    GatewayField("method", context.Request.HttpMethod),
                    GatewayField("path", path),
                    GatewayField("status", 500),
                    GatewayField("error_code", "unhandled_exception"),
                    GatewayField("error", exception.Message));
                WriteJson(context.Response, new JsonObject
                {
                    ["error"] = exception.Message,
                });
            }
        }
        finally
        {
            CurrentGatewayRequestId.Value = previousRequestId;
        }
    }

    private JsonObject BuildRootStatus()
    {
        return new JsonObject
        {
            ["schema_version"] = "carves-host-root.v1",
            ["service"] = "CARVES Host",
            ["status"] = "running",
            ["host_running"] = true,
            ["message"] = "CARVES Host is running. This root route is a status pointer, not the dashboard.",
            ["base_url"] = hostState.BaseUrl,
            ["dashboard_url"] = hostState.DashboardUrl,
            ["workbench_url"] = hostState.WorkbenchUrl,
            ["repo_root"] = hostState.RepoRoot,
            ["host_id"] = hostState.HostId,
            ["started_at_utc"] = hostState.StartedAt,
            ["uptime_seconds"] = Math.Max(0, (long)(DateTimeOffset.UtcNow - hostState.StartedAt).TotalSeconds),
            ["product_start_command"] = "carves up <target-project>",
            ["human_start_prompt"] = "start CARVES",
            ["human_next_action"] = "open the attached target project in your coding agent and say: start CARVES",
            ["host_readiness_command"] = "carves host ensure --json",
            ["runtime_agent_start_command"] = "carves agent start --json",
            ["target_project_agent_command"] = ".carves/carves agent start --json",
            ["agent_instruction"] = "read .carves/AGENT_START.md, then run .carves/carves agent start --json from the attached target project",
            ["target_project_agent_entry"] = "read .carves/AGENT_START.md, then run .carves/carves agent start --json from the attached target project",
            ["root_route_role"] = "host_running_status_pointer_not_dashboard",
            ["gateway_role"] = "connection_routing_observability",
            ["gateway_automation_boundary"] = "no_worker_automation_dispatch",
            ["worker_execution_boundary"] = "null_worker_current_version_no_api_sdk_worker_execution",
            ["routes"] = new JsonArray
            {
                "/handshake",
                "/dashboard/summary",
                "/workbench",
                "/session-gateway/v1/shell",
            },
            ["non_authority"] = new JsonArray
            {
                "not_dashboard_product_entry",
                "not_worker_execution_authority",
                "not_task_completion_truth",
                "not_review_approval",
                "not_state_sync",
            },
        };
    }

    private static bool RequestPrefersHtml(HttpListenerRequest request)
    {
        if (string.Equals(request.QueryString["format"], "json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(request.QueryString["format"], "html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var acceptTypes = request.AcceptTypes ?? Array.Empty<string>();
        if (acceptTypes.Length == 0)
        {
            return true;
        }

        if (acceptTypes.Any(type => type.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (acceptTypes.Any(type => type.Contains("*/*", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return !acceptTypes.Any(type => type.Contains("application/json", StringComparison.OrdinalIgnoreCase));
    }

    private static string RenderRootStatusHtml(JsonObject rootStatus)
    {
        static string Text(JsonObject root, string key)
        {
            return WebUtility.HtmlEncode(root[key]?.GetValue<string>() ?? string.Empty);
        }

        return string.Join(
            Environment.NewLine,
            [
                "<!doctype html>",
                "<html lang=\"en\">",
                "<head>",
                "  <meta charset=\"utf-8\">",
                "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">",
                "  <title>CARVES Host is running</title>",
                "  <style>",
                "    body{margin:0;font-family:ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#f5f7f4;color:#1f2326;}",
                "    main{max-width:760px;margin:9vh auto;padding:32px;border:1px solid #cfd8d3;border-radius:8px;background:#ffffff;box-shadow:0 18px 60px rgba(31,35,38,.1);}",
                "    .eyebrow{letter-spacing:0;text-transform:uppercase;color:#4d6259;font-size:12px;font-weight:700;}",
                "    h1{font-size:42px;line-height:1.05;margin:12px 0 10px;}",
                "    p{font-size:18px;line-height:1.6;}",
                "    code{background:#edf2ef;border:1px solid #cfd8d3;border-radius:6px;padding:2px 6px;}",
                "    .panel{margin-top:22px;padding:18px;border-radius:8px;background:#25312c;color:#f8fbf9;}",
                "    .panel p{margin:8px 0;font-size:16px;}",
                "    .muted{color:#52605b;font-size:15px;}",
                "  </style>",
                "</head>",
                "<body>",
                "  <main>",
                "    <div class=\"eyebrow\">CARVES Host</div>",
                "    <h1>CARVES Host is running.</h1>",
                $"    <p>This page only confirms the local Host is alive at <code>{Text(rootStatus, "base_url")}</code>. It is not the product dashboard and it does not dispatch worker automation.</p>",
                "    <div class=\"panel\">",
                $"      <p><strong>Human next step:</strong> {Text(rootStatus, "human_next_action")}</p>",
                $"      <p><strong>Agent next step:</strong> {Text(rootStatus, "agent_instruction")}</p>",
                "    </div>",
                $"    <p class=\"muted\">First-use setup still starts with <code>{Text(rootStatus, "product_start_command")}</code>. JSON status is available with <code>/?format=json</code>.</p>",
                "  </main>",
                "</body>",
                "</html>",
            ]);
    }

    private void WriteGatewaySessionActivity(string eventName, params string[] fields)
    {
        var allFields = new string[fields.Length + 1];
        allFields[0] = "surface=session_gateway_v1";
        Array.Copy(fields, 0, allFields, 1, fields.Length);
        WriteGatewayEvent(eventName, allFields);
    }

    private bool TryResolveSessionGatewaySurface(HttpListenerContext context, string? clientRepoRoot, out LocalHostSurfaceService surface)
    {
        var scoped = ResolveScopedServices(clientRepoRoot);
        if (!scoped.Allowed)
        {
            context.Response.StatusCode = 400;
            WriteJson(context.Response, new JsonObject
            {
                ["error"] = scoped.Message ?? "Session Gateway repo scope was rejected by host policy.",
            });
            surface = hostSurfaceService;
            return false;
        }

        surface = CreateScopedSurface(scoped.Services!);
        return true;
    }

    private LocalHostSurfaceService CreateScopedSurface(RuntimeServices scopedServices)
    {
        return ReferenceEquals(scopedServices, services)
            ? hostSurfaceService
            : new LocalHostSurfaceService(scopedServices, acceptedOperationStore);
    }

    private static string? ResolveSessionGatewayClientRepoRoot(HttpListenerRequest request, string? requestClientRepoRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(requestClientRepoRoot))
        {
            return requestClientRepoRoot.Trim();
        }

        var queryRepoRoot = request.QueryString["client_repo_root"];
        return string.IsNullOrWhiteSpace(queryRepoRoot) ? null : queryRepoRoot.Trim();
    }

    private ScopedRequestServices ResolveScopedServices(HostCommandRequest request)
    {
        return ResolveScopedServices(request.RepoRoot, request.Command);
    }

    private ScopedRequestServices ResolveScopedServices(string? repoRoot, string? command = null)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            return new ScopedRequestServices(true, services, null);
        }

        var requestedRepoRoot = Path.GetFullPath(repoRoot);
        if (string.Equals(requestedRepoRoot, services.Paths.RepoRoot, StringComparison.OrdinalIgnoreCase))
        {
            return new ScopedRequestServices(true, services, null);
        }

        if (string.Equals(command, "attach", StringComparison.OrdinalIgnoreCase))
        {
            return new ScopedRequestServices(true, services, null);
        }

        var descriptor = services.RepoRegistryService.List().FirstOrDefault(item =>
            string.Equals(Path.GetFullPath(item.RepoPath), requestedRepoRoot, StringComparison.OrdinalIgnoreCase));
        if (descriptor is null)
        {
            return new ScopedRequestServices(
                false,
                null,
                $"Repo '{requestedRepoRoot}' is not attached to this machine host. Use `attach` first.");
        }

        return new ScopedRequestServices(true, RuntimeComposition.Create(descriptor.RepoPath), null);
    }

    private static IReadOnlyList<string> NormalizeCommandArguments(HostCommandRequest request, RuntimeServices scopedServices)
    {
        if (!string.Equals(request.Command, "attach", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = request.Arguments.ToList();
            if (!string.IsNullOrWhiteSpace(request.RepoRoot)
                && !normalized.Contains("--client-repo-root", StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add("--client-repo-root");
                normalized.Add(request.RepoRoot);
            }

            return normalized;
        }

        var normalizedAttach = request.Arguments.ToList();
        var primaryArgument = ResolveAttachPrimaryArgument(normalizedAttach);
        if (string.IsNullOrWhiteSpace(primaryArgument)
            && !string.IsNullOrWhiteSpace(request.RepoRoot)
            && !string.Equals(Path.GetFullPath(request.RepoRoot), scopedServices.Paths.RepoRoot, StringComparison.OrdinalIgnoreCase))
        {
            normalizedAttach.Insert(0, request.RepoRoot);
        }

        if (!string.IsNullOrWhiteSpace(request.RepoRoot)
            && !normalizedAttach.Contains("--client-repo-root", StringComparer.OrdinalIgnoreCase))
        {
            normalizedAttach.Add("--client-repo-root");
            normalizedAttach.Add(request.RepoRoot);
        }

        return normalizedAttach;
    }

    private static string? ResolveAttachPrimaryArgument(IReadOnlyList<string> arguments)
    {
        var consumedIndexes = new HashSet<int>();
        var optionsWithValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--repo-id",
            "--provider-profile",
            "--policy-profile",
            "--client-repo-root",
        };

        var flagOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--dry-run",
            "--start-runtime",
            "--force",
        };

        for (var index = 0; index < arguments.Count; index++)
        {
            if (optionsWithValues.Contains(arguments[index]))
            {
                consumedIndexes.Add(index);
                if (index + 1 < arguments.Count)
                {
                    consumedIndexes.Add(index + 1);
                }

                continue;
            }

            if (flagOptions.Contains(arguments[index]))
            {
                consumedIndexes.Add(index);
            }
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            if (!consumedIndexes.Contains(index))
            {
                return arguments[index];
            }
        }

        return null;
    }
}
