using System.Diagnostics;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public static class ActorSessionLivenessRules
{
    public static readonly TimeSpan FreshnessWindow = TimeSpan.FromMinutes(20);

    public static ActorSessionLivenessSnapshot Classify(IEnumerable<ActorSessionRecord> sessions, DateTimeOffset? checkedAt = null)
    {
        var now = checkedAt ?? DateTimeOffset.UtcNow;
        var registered = sessions
            .OrderBy(item => item.ActorSessionId, StringComparer.Ordinal)
            .ToArray();
        var closed = registered
            .Where(IsClosed)
            .ToArray();
        var stale = registered
            .Where(item => !IsClosed(item))
            .Where(item => IsStale(item, now))
            .ToArray();
        var nonLiveSessionIds = closed
            .Concat(stale)
            .Select(item => item.ActorSessionId)
            .ToHashSet(StringComparer.Ordinal);
        var live = registered
            .Where(item => !nonLiveSessionIds.Contains(item.ActorSessionId))
            .ToArray();

        return new ActorSessionLivenessSnapshot(
            now,
            FreshnessWindow,
            registered,
            live,
            stale,
            closed);
    }

    public static bool IsStale(ActorSessionRecord session, DateTimeOffset checkedAt)
    {
        return checkedAt - session.LastSeenAt > FreshnessWindow;
    }

    public static bool IsClosed(ActorSessionRecord session)
    {
        return ResolveNonLiveReason(session, DateTimeOffset.UtcNow) is "state_stopped"
            or "process_missing"
            or "process_mismatch"
            or "health_closed"
            or "health_dead"
            or "health_lost"
            or "health_offline"
            or "health_stopped"
            or "health_terminated"
            or "health_unavailable"
            or "health_unhealthy";
    }

    public static string ResolveStatus(ActorSessionRecord session, DateTimeOffset checkedAt)
    {
        if (IsClosed(session))
        {
            return "closed";
        }

        return IsStale(session, checkedAt)
            ? "stale"
            : "live";
    }

    public static string ResolveNonLiveReason(ActorSessionRecord session, DateTimeOffset checkedAt)
    {
        if (session.State == ActorSessionState.Stopped)
        {
            return "state_stopped";
        }

        var processTrackingStatus = ResolveProcessTrackingStatus(session);
        if (processTrackingStatus == "process_missing")
        {
            return "process_missing";
        }

        if (processTrackingStatus == "process_mismatch")
        {
            return "process_mismatch";
        }

        var health = (session.HealthPosture ?? string.Empty).Trim().ToLowerInvariant();
        if (health is "closed"
            or "dead"
            or "lost"
            or "offline"
            or "stopped"
            or "terminated"
            or "unavailable"
            or "unhealthy")
        {
            return $"health_{health}";
        }

        return IsStale(session, checkedAt)
            ? "heartbeat_stale"
            : "live";
    }

    public static bool IsLocalProcessMissing(ActorSessionRecord session)
    {
        return ResolveProcessTrackingStatus(session) == "process_missing";
    }

    public static string ResolveProcessTrackingStatus(ActorSessionRecord session)
    {
        if (session.ProcessId is not int processId || processId <= 0)
        {
            return "heartbeat_only";
        }

        var currentStartedAt = TryResolveProcessStartedAt(processId);
        if (currentStartedAt is null)
        {
            return "process_missing";
        }

        if (session.ProcessStartedAt is not DateTimeOffset recordedStartedAt)
        {
            return "process_identity_unverified";
        }

        return ProcessStartMatches(recordedStartedAt, currentStartedAt.Value)
            ? "process_alive"
            : "process_mismatch";
    }

    public static string[] BuildStopCommands(IEnumerable<ActorSessionRecord> sessions, string reason)
    {
        return sessions
            .Select(item => item.ActorSessionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => $"api actor-session-stop --actor-session-id {id} --reason {reason}")
            .ToArray();
    }

    private static DateTimeOffset? TryResolveProcessStartedAt(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return null;
            }

            return NormalizeProcessStartTime(process.StartTime);
        }
        catch
        {
            return null;
        }
    }

    private static bool ProcessStartMatches(DateTimeOffset recordedStartedAt, DateTimeOffset currentStartedAt)
    {
        return (recordedStartedAt.ToUniversalTime() - currentStartedAt.ToUniversalTime()).Duration() <= TimeSpan.FromSeconds(2);
    }

    private static DateTimeOffset NormalizeProcessStartTime(DateTime startTime)
    {
        var localStartTime = startTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(startTime, DateTimeKind.Local)
            : startTime;
        return new DateTimeOffset(localStartTime).ToUniversalTime();
    }
}

public sealed record ActorSessionLivenessSnapshot(
    DateTimeOffset CheckedAt,
    TimeSpan FreshnessWindow,
    IReadOnlyList<ActorSessionRecord> RegisteredSessions,
    IReadOnlyList<ActorSessionRecord> LiveSessions,
    IReadOnlyList<ActorSessionRecord> StaleSessions,
    IReadOnlyList<ActorSessionRecord> ClosedSessions);
