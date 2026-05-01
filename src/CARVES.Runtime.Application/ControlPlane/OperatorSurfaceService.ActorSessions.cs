using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private const int ActorSessionRenderLimit = 50;

    public OperatorCommandResult ActorSessions(ActorSessionKind? kind = null, string? repoId = null)
    {
        var sessions = FilterActorSessions(kind, repoId);
        var liveness = ActorSessionLivenessRules.Classify(sessions);
        var liveIds = liveness.LiveSessions.Select(item => item.ActorSessionId).ToHashSet(StringComparer.Ordinal);
        var staleIds = liveness.StaleSessions.Select(item => item.ActorSessionId).ToHashSet(StringComparer.Ordinal);
        var closedIds = liveness.ClosedSessions.Select(item => item.ActorSessionId).ToHashSet(StringComparer.Ordinal);
        var renderedSessions = sessions.Take(ActorSessionRenderLimit).ToArray();
        var renderedIds = renderedSessions.Select(item => item.ActorSessionId).ToHashSet(StringComparer.Ordinal);
        var hiddenCount = Math.Max(0, sessions.Count - renderedSessions.Length);
        var hiddenNonLiveCount = liveness.StaleSessions
            .Concat(liveness.ClosedSessions)
            .Count(item => !renderedIds.Contains(item.ActorSessionId));
        var processAliveCount = sessions.Count(item =>
            ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_alive");
        var processMissingCount = sessions.Count(item =>
            ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_missing");
        var processMismatchCount = sessions.Count(item =>
            ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_mismatch");
        var processIdentityUnverifiedCount = sessions.Count(item =>
            ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "process_identity_unverified");
        var heartbeatOnlyCount = sessions.Count(item =>
            ActorSessionLivenessRules.ResolveProcessTrackingStatus(item) == "heartbeat_only");
        var lines = new List<string>
        {
            $"Actor sessions: total={sessions.Count}; rendered={renderedSessions.Length}; hidden={hiddenCount}",
            $"Actor session filters: kind={FormatActorSessionKindFilter(kind)}; repo={FormatFilter(repoId)}",
            $"Actor session liveness: live={liveness.LiveSessions.Count} stale={liveness.StaleSessions.Count} closed={liveness.ClosedSessions.Count} non_live={liveness.StaleSessions.Count + liveness.ClosedSessions.Count}; freshness_window_seconds={(int)liveness.FreshnessWindow.TotalSeconds}; checked_at={liveness.CheckedAt:O}",
            $"Actor session process tracking: process_alive={processAliveCount} process_missing={processMissingCount} process_mismatch={processMismatchCount} process_identity_unverified={processIdentityUnverifiedCount} heartbeat_only={heartbeatOnlyCount}",
        };
        if (hiddenCount > 0)
        {
            lines.Add($"Actor session output truncated: rendered={renderedSessions.Length}; hidden={hiddenCount}; use --repo-id and --kind to narrow the view.");
        }

        if (hiddenNonLiveCount > 0)
        {
            lines.Add($"Hidden non-live actor sessions: {hiddenNonLiveCount}; narrow filters before relying on this view for cleanup.");
        }

        if (sessions.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var session in renderedSessions)
        {
            var livenessStatus = ResolveActorSessionLivenessStatus(session, liveIds, staleIds, closedIds);
            var ageSeconds = Math.Max(0, (int)(liveness.CheckedAt - session.LastSeenAt).TotalSeconds);
            lines.Add($"- {session.ActorSessionId} [{session.Kind}/{session.State}] identity={session.ActorIdentity} repo={session.RepoId}");
            lines.Add($"  runtime_session: {session.RuntimeSessionId ?? "(none)"}");
            lines.Add($"  process_id: {session.ProcessId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}");
            lines.Add($"  process_started_at: {session.ProcessStartedAt?.ToString("O") ?? "(none)"}");
            lines.Add($"  process_tracking: {ActorSessionLivenessRules.ResolveProcessTrackingStatus(session)}");
            lines.Add($"  registration: mode={session.RegistrationMode}; worker_instance={(session.WorkerInstanceId ?? "(none)")}; supervisor_launch_token={(session.SupervisorLaunchTokenId ?? "(none)")}");
            lines.Add($"  operation: {(session.LastOperationClass ?? "(none)")}/{(session.LastOperation ?? "(none)")}");
            lines.Add($"  binding: schedule={(session.ScheduleBinding ?? "(none)")}; context_receipt={(session.LastContextReceipt ?? "(none)")}; health={(session.HealthPosture ?? "(none)")}");
            lines.Add($"  liveness: {livenessStatus}; reason={ActorSessionLivenessRules.ResolveNonLiveReason(session, liveness.CheckedAt)}; last_seen={session.LastSeenAt:O}; age_seconds={ageSeconds}");
            if (!string.Equals(livenessStatus, "live", StringComparison.Ordinal))
            {
                lines.Add($"  next_action: api actor-session-stop --actor-session-id {session.ActorSessionId} --reason actor-thread-not-live");
            }

            lines.Add($"  task/run: {(session.CurrentTaskId ?? "(none)")}/{(session.CurrentRunId ?? "(none)")}");
            lines.Add($"  ownership: {(session.CurrentOwnershipScope?.ToString() ?? "(none)")}/{(session.CurrentOwnershipTargetId ?? "(none)")}");
            lines.Add($"  last_reason: {session.LastReason}");
        }

        return new OperatorCommandResult(0, lines);
    }

    private static string ResolveActorSessionLivenessStatus(
        ActorSessionRecord session,
        IReadOnlySet<string> liveIds,
        IReadOnlySet<string> staleIds,
        IReadOnlySet<string> closedIds)
    {
        if (closedIds.Contains(session.ActorSessionId))
        {
            return "closed";
        }

        if (staleIds.Contains(session.ActorSessionId))
        {
            return "stale";
        }

        return liveIds.Contains(session.ActorSessionId)
            ? "live"
            : "unknown";
    }

    private IReadOnlyList<ActorSessionRecord> FilterActorSessions(ActorSessionKind? kind, string? repoId)
    {
        var sessions = operatorApiService.GetActorSessions(kind);
        if (string.IsNullOrWhiteSpace(repoId))
        {
            return sessions;
        }

        var normalizedRepoId = repoId.Trim();
        return sessions
            .Where(item => string.Equals(item.RepoId, normalizedRepoId, StringComparison.Ordinal))
            .ToArray();
    }

    private static string FormatActorSessionKindFilter(ActorSessionKind? kind)
    {
        return kind?.ToString() ?? "(any)";
    }

    private static string FormatFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "(any)"
            : value.Trim();
    }

    public OperatorCommandResult ActorOwnership(OwnershipScope? scope = null, string? targetId = null)
    {
        var bindings = operatorApiService.GetOwnershipBindings(scope, targetId);
        var lines = new List<string> { $"Ownership bindings: {bindings.Count}" };
        if (bindings.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var binding in bindings.Take(50))
        {
            lines.Add($"- {binding.BindingId} scope={binding.Scope} target={binding.TargetId}");
            lines.Add($"  owner: {binding.OwnerKind}:{binding.OwnerIdentity} ({binding.OwnerActorSessionId})");
            lines.Add($"  claimed: {binding.ClaimedAt:O}");
            lines.Add($"  updated: {binding.UpdatedAt:O}");
            lines.Add($"  reason: {binding.Reason}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult OperatorOsEvents(string? taskId = null, string? actorSessionId = null, OperatorOsEventKind? eventKind = null)
    {
        var events = operatorApiService.GetOperatorOsEvents(taskId, actorSessionId, eventKind);
        var lines = new List<string> { $"Operator OS events: {events.Count}" };
        if (events.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var item in events.Take(100))
        {
            lines.Add($"- {item.EventKind} [{item.OccurredAt:O}] actor={item.ActorKind?.ToString() ?? "(none)"}:{item.ActorIdentity ?? "(none)"}");
            lines.Add($"  task/run: {(item.TaskId ?? "(none)")}/{(item.RunId ?? "(none)")}");
            lines.Add($"  ownership: {(item.OwnershipScope?.ToString() ?? "(none)")}/{(item.OwnershipTargetId ?? "(none)")}");
            lines.Add($"  reason: {item.ReasonCode}");
            lines.Add($"  summary: {item.Summary}");
            if (!string.IsNullOrWhiteSpace(item.DetailRef))
            {
                lines.Add($"  detail_ref: {item.DetailRef}");
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult AgentGatewayTrace()
    {
        var events = operatorApiService.GetOperatorOsEvents()
            .Where(item => item.EventKind is OperatorOsEventKind.AgentGatewayRequestReceived
                or OperatorOsEventKind.AgentGatewayResponseReturned)
            .Take(100)
            .ToArray();
        var requestCount = events.Count(item => item.EventKind == OperatorOsEventKind.AgentGatewayRequestReceived);
        var responseCount = events.Count(item => item.EventKind == OperatorOsEventKind.AgentGatewayResponseReturned);
        var lines = new List<string>
        {
            $"Agent gateway trace: {events.Length} events",
            $"Requests: {requestCount}",
            $"Responses: {responseCount}",
            "Retention: recent operator OS events",
        };

        if (events.Length == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var item in events)
        {
            var direction = item.EventKind == OperatorOsEventKind.AgentGatewayRequestReceived
                ? "Request"
                : "Response";
            lines.Add($"- {direction} [{item.OccurredAt:O}] request_id={item.ReferenceId ?? "(none)"} actor={item.ActorIdentity ?? "(none)"} session={item.ActorSessionId ?? "(none)"}");
            lines.Add($"  reason: {item.ReasonCode}");
            lines.Add($"  summary: {item.Summary}");
            if (!string.IsNullOrWhiteSpace(item.DetailRef))
            {
                lines.Add($"  detail_ref: {item.DetailRef}");
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult RepairActorSessions(bool dryRun)
    {
        var result = actorSessionService.RepairStaleCurrentWork(
            dryRun,
            dryRun
                ? "Dry-run inspected stale actor-session current work residue."
                : "Repaired stale actor-session current work residue.");
        var lines = new List<string>
        {
            dryRun ? "Actor session repair dry run" : "Actor session repair",
            $"Repaired sessions: {result.RepairedCount}",
        };

        if (result.Repairs.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var repair in result.Repairs)
        {
            lines.Add($"- {repair.ActorSessionId} [{repair.Kind}] repo={repair.RepoId}");
            lines.Add($"  state: {repair.PreviousState} -> {repair.RepairedState}");
            lines.Add($"  current task/run: {repair.PreviousCurrentTaskId ?? "(none)"}/{repair.PreviousCurrentRunId ?? "(none)"} -> (none)/(none)");
            lines.Add($"  reason: {repair.ReasonCode}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult ClearActorSessions(string? actorSessionId, string? repoId, ActorSessionKind? kind, bool dryRun, string? reason)
    {
        if (string.IsNullOrWhiteSpace(actorSessionId) && string.IsNullOrWhiteSpace(repoId) && kind is null)
        {
            return OperatorCommandResult.Failure("Usage: actor clear-sessions [--actor-session-id <id>] [--repo-id <id>] [--kind <operator|agent|planner|worker>] [--reason <text>] [--dry-run]");
        }

        var resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? "Cleared actor session cache through Host."
            : reason.Trim();
        var result = actorSessionService.Clear(actorSessionId, repoId, kind, dryRun, resolvedReason);
        var lines = new List<string>
        {
            dryRun ? "Actor session clear dry run" : "Actor session cache cleared",
            $"Cleared sessions: {result.ClearedCount}",
            $"Reason: {result.Reason}",
        };

        if (result.ClearedSessions.Count == 0)
        {
            lines.Add("(none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var session in result.ClearedSessions)
        {
            lines.Add($"- {session.ActorSessionId} [{session.Kind}/{session.State}] identity={session.ActorIdentity} repo={session.RepoId}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult ActorSessionFallbackPolicy(string? repoId)
    {
        var result = BuildActorSessionFallbackPolicy(repoId);
        var lines = new List<string>
        {
            "Actor session fallback policy",
            $"Repo: {result.RepoId}",
            $"Fallback mode: {result.FallbackMode}",
            $"Fallback allowed: {result.FallbackAllowed}",
            $"Explicit planner registered: {result.ExplicitPlannerRegistered}",
            $"Explicit worker registered: {result.ExplicitWorkerRegistered}",
            $"Missing actor roles: {(result.MissingActorRoles.Count == 0 ? "(none)" : string.Join(", ", result.MissingActorRoles))}",
            $"Registered actor sessions: {result.RegisteredActorCount}",
            $"Live actor sessions: {result.LiveActorSessionCount}",
            $"Non-live actor sessions: {result.NonLiveActorSessionCount}",
            $"Stale actor sessions: {result.StaleActorSessionCount}",
            $"Closed actor sessions: {result.ClosedActorSessionCount}",
            $"Non-live actor session policy: {result.NonLiveActorSessionPolicy}",
            $"Grants execution authority: {result.GrantsExecutionAuthority}",
            $"Grants truth write authority: {result.GrantsTruthWriteAuthority}",
            $"Creates task queue: {result.CreatesTaskQueue}",
            $"Fallback run packet required: {result.FallbackRunPacketRequired}",
            $"Required run packet fields: {(result.RequiredRunPacketFields.Count == 0 ? "(none)" : string.Join(", ", result.RequiredRunPacketFields))}",
            $"Recommended next action: {result.RecommendedNextAction}",
            "Required evidence:",
        };

        lines.AddRange(result.RequiredEvidence.Select(item => $"- {item}"));
        if (result.NonLiveActorStopCommands.Count > 0)
        {
            lines.Add("Non-live actor stop commands:");
            lines.AddRange(result.NonLiveActorStopCommands.Select(item => $"- {item}"));
        }

        lines.Add("Boundaries:");
        lines.AddRange(result.Boundaries.Select(item => $"- {item}"));

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult RegisterActorSession(
        ActorSessionKind? kind,
        string? actorIdentity,
        string? repoId,
        string? actorSessionId,
        string? providerProfile,
        string? capabilityProfile,
        string? sessionScope,
        string? budgetProfile,
        string? scheduleBinding,
        string? lastContextReceipt,
        string? healthPosture,
        int? processId,
        string? registrationMode,
        string? workerInstanceId,
        string? launchToken,
        string? reason,
        bool dryRun)
    {
        if (kind is null || string.IsNullOrWhiteSpace(actorIdentity))
        {
            return OperatorCommandResult.Failure("Usage: actor register --kind <operator|agent|planner|worker> --identity <id> [--repo-id <id>] [--actor-session-id <id>] [--provider-profile <id>] [--capability-profile <id>] [--scope <scope>] [--budget-profile <id>] [--schedule-binding <id>] [--context-receipt <id>] [--health <posture>] [--process-id <pid>] [--registration-mode <manual|supervised>] [--worker-instance-id <id>] [--launch-token <token>] [--reason <text>] [--dry-run]");
        }

        Carves.Runtime.Application.Platform.ActorSessionRegistrationResult result;
        try
        {
            result = RegisterActorSessionCore(
                kind.Value,
                actorIdentity,
                repoId,
                actorSessionId,
                providerProfile,
                capabilityProfile,
                sessionScope,
                budgetProfile,
                scheduleBinding,
                lastContextReceipt,
                healthPosture,
                processId,
                registrationMode,
                workerInstanceId,
                launchToken,
                reason,
                dryRun);
        }
        catch (InvalidOperationException exception)
        {
            return OperatorCommandResult.Failure(exception.Message);
        }

        var lines = new List<string>
        {
            dryRun ? "Actor session registration dry run" : "Actor session registered",
            $"Actor session: {result.ActorSessionId}",
            $"Kind: {result.Kind}",
            $"Identity: {result.ActorIdentity}",
            $"Repo: {result.RepoId}",
            $"Role boundary: {result.RoleBoundary}",
            $"Schedule binding state: {result.ScheduleBindingState}",
            $"Callback wake authority: {result.CallbackWakeAuthority}",
            $"Schedule replacement policy: {result.ScheduleReplacementPolicy}",
            $"Schedule grants execution authority: {result.ScheduleBindingGrantsExecutionAuthority}",
            $"Schedule grants truth write authority: {result.ScheduleBindingGrantsTruthWriteAuthority}",
            $"Process id: {result.ProcessId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}",
            $"Process started at: {result.ProcessStartedAt?.ToString("O") ?? "(none)"}",
            $"Registration mode: {result.RegistrationMode}",
            $"Worker instance: {result.WorkerInstanceId ?? "(none)"}",
            $"Supervisor launch token: {result.SupervisorLaunchTokenId ?? "(none)"}",
            $"Supervisor launch verified: {result.SupervisorLaunchVerified}",
            $"Host dispatch required: {result.HostDispatchRequired}",
            $"Starts run: {result.StartsRun}",
            $"Issues lease: {result.IssuesLease}",
            $"Task truth write allowed: {result.TaskTruthWriteAllowed}",
            $"Reason: {result.Reason}",
        };

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult ApiActorSessions(ActorSessionKind? kind = null, string? repoId = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(FilterActorSessions(kind, repoId)));
    }

    public OperatorCommandResult ApiActorSessionRegister(
        ActorSessionKind? kind,
        string? actorIdentity,
        string? repoId,
        string? actorSessionId,
        string? providerProfile,
        string? capabilityProfile,
        string? sessionScope,
        string? budgetProfile,
        string? scheduleBinding,
        string? lastContextReceipt,
        string? healthPosture,
        int? processId,
        string? registrationMode,
        string? workerInstanceId,
        string? launchToken,
        string? reason,
        bool dryRun)
    {
        if (kind is null || string.IsNullOrWhiteSpace(actorIdentity))
        {
            return OperatorCommandResult.Failure("Usage: api actor-session-register --kind <operator|agent|planner|worker> --identity <id> [--repo-id <id>] [--actor-session-id <id>] [--provider-profile <id>] [--capability-profile <id>] [--scope <scope>] [--budget-profile <id>] [--schedule-binding <id>] [--context-receipt <id>] [--health <posture>] [--process-id <pid>] [--registration-mode <manual|supervised>] [--worker-instance-id <id>] [--launch-token <token>] [--reason <text>] [--dry-run]");
        }

        try
        {
            return OperatorCommandResult.Success(operatorApiService.ToJson(RegisterActorSessionCore(
                kind.Value,
                actorIdentity,
                repoId,
                actorSessionId,
                providerProfile,
                capabilityProfile,
                sessionScope,
                budgetProfile,
                scheduleBinding,
                lastContextReceipt,
                healthPosture,
                processId,
                registrationMode,
                workerInstanceId,
                launchToken,
                reason,
                dryRun)));
        }
        catch (InvalidOperationException exception)
        {
            return OperatorCommandResult.Failure(exception.Message);
        }
    }

    public OperatorCommandResult ApiActorSessionRepair(bool dryRun)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(actorSessionService.RepairStaleCurrentWork(
            dryRun,
            dryRun
                ? "Dry-run inspected stale actor-session current work residue."
                : "Repaired stale actor-session current work residue.")));
    }

    public OperatorCommandResult ApiActorSessionClear(string? actorSessionId, string? repoId, ActorSessionKind? kind, bool dryRun, string? reason)
    {
        if (string.IsNullOrWhiteSpace(actorSessionId) && string.IsNullOrWhiteSpace(repoId) && kind is null)
        {
            return OperatorCommandResult.Failure("Usage: api actor-session-clear [--actor-session-id <id>] [--repo-id <id>] [--kind <operator|agent|planner|worker>] [--reason <text>] [--dry-run]");
        }

        return OperatorCommandResult.Success(operatorApiService.ToJson(actorSessionService.Clear(
            actorSessionId,
            repoId,
            kind,
            dryRun,
            string.IsNullOrWhiteSpace(reason)
                ? "Cleared actor session cache through Host API."
                : reason.Trim())));
    }

    public OperatorCommandResult ApiActorSessionStop(string? actorSessionId, bool dryRun, string? reason)
    {
        if (string.IsNullOrWhiteSpace(actorSessionId))
        {
            return OperatorCommandResult.Failure("Usage: api actor-session-stop --actor-session-id <id> [--reason <text>] [--dry-run]");
        }

        return OperatorCommandResult.Success(operatorApiService.ToJson(actorSessionService.Stop(
            actorSessionId.Trim(),
            dryRun,
            string.IsNullOrWhiteSpace(reason)
                ? "Stopped actor session through Host API."
                : reason.Trim())));
    }

    public OperatorCommandResult ApiActorSessionHeartbeat(string? actorSessionId, string? healthPosture, string? lastContextReceipt, bool dryRun, string? reason)
    {
        if (string.IsNullOrWhiteSpace(actorSessionId))
        {
            return OperatorCommandResult.Failure("Usage: api actor-session-heartbeat --actor-session-id <id> [--health <posture>] [--context-receipt <id>] [--reason <text>] [--dry-run]");
        }

        return OperatorCommandResult.Success(operatorApiService.ToJson(actorSessionService.Heartbeat(
            actorSessionId.Trim(),
            dryRun,
            string.IsNullOrWhiteSpace(reason)
                ? "Actor session heartbeat reported through Host API."
                : reason.Trim(),
            string.IsNullOrWhiteSpace(healthPosture) ? null : healthPosture.Trim(),
            string.IsNullOrWhiteSpace(lastContextReceipt) ? null : lastContextReceipt.Trim())));
    }

    public OperatorCommandResult ActorSessionHeartbeat(string? actorSessionId, string? healthPosture, string? lastContextReceipt, bool dryRun, string? reason)
    {
        if (string.IsNullOrWhiteSpace(actorSessionId))
        {
            return OperatorCommandResult.Failure("Usage: actor heartbeat --actor-session-id <id> [--health <posture>] [--context-receipt <id>] [--reason <text>] [--dry-run]");
        }

        var result = actorSessionService.Heartbeat(
            actorSessionId.Trim(),
            dryRun,
            string.IsNullOrWhiteSpace(reason)
                ? "Actor session heartbeat reported through Host."
                : reason.Trim(),
            string.IsNullOrWhiteSpace(healthPosture) ? null : healthPosture.Trim(),
            string.IsNullOrWhiteSpace(lastContextReceipt) ? null : lastContextReceipt.Trim());
        var lines = new List<string>
        {
            dryRun ? "Actor session heartbeat dry run" : "Actor session heartbeat",
            $"Actor session: {result.ActorSessionId}",
            $"Found: {result.Found}",
            $"Heartbeat accepted: {result.HeartbeatAccepted}",
            $"Updated: {result.Updated}",
            $"Kind: {result.Kind?.ToString() ?? "(none)"}",
            $"Identity: {result.ActorIdentity ?? "(none)"}",
            $"Repo: {result.RepoId ?? "(none)"}",
            $"State: {result.PreviousState?.ToString() ?? "(none)"} -> {result.CurrentState?.ToString() ?? "(none)"}",
            $"Health: {result.PreviousHealthPosture ?? "(none)"} -> {result.CurrentHealthPosture ?? "(none)"}",
            $"Context receipt: {result.PreviousContextReceipt ?? "(none)"} -> {result.CurrentContextReceipt ?? "(none)"}",
            $"Liveness status: {result.LivenessStatus}",
            $"Grants execution authority: {result.GrantsExecutionAuthority}",
            $"Grants truth write authority: {result.GrantsTruthWriteAuthority}",
            $"Creates task queue: {result.CreatesTaskQueue}",
            $"Reason: {result.Reason}",
        };

        return new OperatorCommandResult(result.Found && result.HeartbeatAccepted ? 0 : 1, lines);
    }

    public OperatorCommandResult ApiActorSessionFallbackPolicy(string? repoId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(BuildActorSessionFallbackPolicy(repoId)));
    }

    public OperatorCommandResult ApiActorOwnership(OwnershipScope? scope = null, string? targetId = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetOwnershipBindings(scope, targetId)));
    }

    public OperatorCommandResult ApiOperatorOsEvents(string? taskId = null, string? actorSessionId = null, OperatorOsEventKind? eventKind = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(operatorApiService.GetOperatorOsEvents(taskId, actorSessionId, eventKind)));
    }

    private Carves.Runtime.Application.Platform.ActorSessionRegistrationResult RegisterActorSessionCore(
        ActorSessionKind kind,
        string actorIdentity,
        string? repoId,
        string? actorSessionId,
        string? providerProfile,
        string? capabilityProfile,
        string? sessionScope,
        string? budgetProfile,
        string? scheduleBinding,
        string? lastContextReceipt,
        string? healthPosture,
        int? processId,
        string? registrationMode,
        string? workerInstanceId,
        string? launchToken,
        string? reason,
        bool dryRun)
    {
        var resolvedRepoId = string.IsNullOrWhiteSpace(repoId)
            ? repoRegistryService.List().FirstOrDefault()?.RepoId ?? Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : repoId.Trim();
        var resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? $"Registered {kind} actor session through Host."
            : reason.Trim();
        return actorSessionService.Register(
            kind,
            actorIdentity.Trim(),
            resolvedRepoId,
            resolvedReason,
            dryRun,
            string.IsNullOrWhiteSpace(actorSessionId) ? null : actorSessionId.Trim(),
            devLoopService.GetSession()?.SessionId,
            string.IsNullOrWhiteSpace(providerProfile) ? null : providerProfile.Trim(),
            string.IsNullOrWhiteSpace(capabilityProfile) ? null : capabilityProfile.Trim(),
            string.IsNullOrWhiteSpace(sessionScope) ? null : sessionScope.Trim(),
            string.IsNullOrWhiteSpace(budgetProfile) ? null : budgetProfile.Trim(),
            string.IsNullOrWhiteSpace(scheduleBinding) ? null : scheduleBinding.Trim(),
            string.IsNullOrWhiteSpace(lastContextReceipt) ? null : lastContextReceipt.Trim(),
            string.IsNullOrWhiteSpace(healthPosture) ? null : healthPosture.Trim(),
            processId,
            ParseActorSessionRegistrationMode(registrationMode),
            string.IsNullOrWhiteSpace(workerInstanceId) ? null : workerInstanceId.Trim(),
            string.IsNullOrWhiteSpace(launchToken) ? null : launchToken.Trim());
    }

    private static ActorSessionRegistrationMode ParseActorSessionRegistrationMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ActorSessionRegistrationMode.Manual;
        }

        return Enum.TryParse<ActorSessionRegistrationMode>(value.Trim(), true, out var parsed)
            ? parsed
            : throw new InvalidOperationException("Actor session registration mode must be 'manual' or 'supervised'.");
    }

    private Carves.Runtime.Application.Platform.ActorSessionFallbackPolicyResult BuildActorSessionFallbackPolicy(string? repoId)
    {
        var resolvedRepoId = string.IsNullOrWhiteSpace(repoId)
            ? repoRegistryService.List().FirstOrDefault()?.RepoId ?? Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            : repoId.Trim();
        return actorSessionService.BuildFallbackPolicy(resolvedRepoId);
    }

    private ActorSessionRecord EnsureControlActorSession(
        ActorSessionKind kind,
        string actorIdentity,
        string reason,
        OwnershipScope? scope = null,
        string? targetId = null,
        string? operationClass = null,
        string? operation = null)
    {
        var repoId = repoRegistryService.List().FirstOrDefault()?.RepoId ?? Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var session = actorSessionService.Ensure(
            kind,
            actorIdentity,
            repoId,
            reason,
            runtimeSessionId: devLoopService.GetSession()?.SessionId,
            operationClass: operationClass,
            operation: operation);
        if (scope is not null && !string.IsNullOrWhiteSpace(targetId))
        {
            actorSessionService.MarkState(
                session.ActorSessionId,
                session.State,
                reason,
                runtimeSessionId: devLoopService.GetSession()?.SessionId,
                ownershipScope: scope,
                ownershipTargetId: targetId,
                operationClass: operationClass,
                operation: operation);
        }

        return session;
    }

    private OperatorCommandResult ResolveArbitratedAction(
        ActorSessionRecord actorSession,
        OwnershipScope scope,
        string targetId,
        string reason,
        Func<OperatorCommandResult> action)
    {
        var arbitration = concurrentActorArbitrationService.Resolve(actorSession, scope, targetId, reason);
        if (arbitration.Outcome != ActorArbitrationOutcome.Granted)
        {
            return new OperatorCommandResult(
                1,
                [
                    $"Actor arbitration outcome: {arbitration.Outcome}",
                    $"Scope: {arbitration.Scope}",
                    $"Target: {arbitration.TargetId}",
                    $"Summary: {arbitration.Summary}",
                    $"Reason code: {arbitration.ReasonCode}",
                    $"Current owner: {(arbitration.CurrentOwnerKind?.ToString() ?? "(none)")}:{arbitration.CurrentOwnerIdentity ?? "(none)"}",
                    $"Next action: {BuildOwnershipPollInstruction(scope, targetId)}",
                ]);
        }

        try
        {
            return action();
        }
        finally
        {
            sessionOwnershipService.Release(scope, targetId, $"Ownership released after {scope} action completed.");
        }
    }

    private static string BuildOwnershipPollInstruction(OwnershipScope scope, string targetId)
    {
        return $"Poll `actor ownership --scope {scope} --target-id {targetId}` or `api actor-ownership --scope {scope} --target-id {targetId}` until the current owner releases the scope, then retry.";
    }
}
