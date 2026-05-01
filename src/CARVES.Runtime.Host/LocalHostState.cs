using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed class LocalHostState
{
    public LocalHostState(string repoRoot, string baseUrl, string runtimeDirectory, string deploymentDirectory, string executablePath, int port)
    {
        RepoRoot = repoRoot;
        MachineId = LocalHostPaths.ResolveMachineId();
        HostId = LocalHostPaths.GetHostId(repoRoot, MachineId);
        BaseUrl = baseUrl.TrimEnd('/');
        RuntimeDirectory = runtimeDirectory;
        DeploymentDirectory = deploymentDirectory;
        ExecutablePath = executablePath;
        Port = port;
    }

    public string RepoRoot { get; }

    public string HostId { get; }

    public string MachineId { get; }

    public string BaseUrl { get; }

    public string DashboardUrl => $"{BaseUrl}/dashboard";

    public string WorkbenchUrl => $"{BaseUrl}/workbench";

    public string RuntimeDirectory { get; }

    public string DeploymentDirectory { get; }

    public string ExecutablePath { get; }

    public int Port { get; }

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastRequestAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoopAt { get; private set; }

    public string LastLoopReason { get; private set; } = "Host loop has not advanced a runtime yet.";

    public int RequestCount { get; private set; }

    public HostRehydrationSummary RehydrationSummary { get; private set; } = new(
        Rehydrated: false,
        PendingApprovalCount: 0,
        ActorSessionCount: 0,
        RecentIncidentCount: 0,
        RecentOperatorEventCount: 0,
        StaleMarkerCount: 0,
        PausedRuntimeCount: 0,
        CleanupActions: Array.Empty<string>(),
        Summary: "Host has not completed rehydration.");

    public void RecordRequest()
    {
        RequestCount += 1;
        LastRequestAt = DateTimeOffset.UtcNow;
    }

    public void RecordLoop(string reason)
    {
        LastLoopAt = DateTimeOffset.UtcNow;
        LastLoopReason = reason;
    }

    public void RecordRehydration(HostRehydrationSummary summary)
    {
        RehydrationSummary = summary;
        LastRequestAt = DateTimeOffset.UtcNow;
    }

    public HostRuntimeSnapshot BuildSnapshot(
        HostRuntimeSnapshotState state,
        string summary,
        string stage,
        string? sessionStatus,
        string? hostControlState,
        string? hostControlReason,
        int activeWorkerCount,
        IReadOnlyList<string> activeTaskIds,
        int pendingApprovalCount)
    {
        return new HostRuntimeSnapshot
        {
            RepoRoot = RepoRoot,
            State = state,
            Summary = summary,
            BaseUrl = BaseUrl,
            Port = Port,
            ProcessId = Environment.ProcessId,
            RuntimeDirectory = RuntimeDirectory,
            DeploymentDirectory = DeploymentDirectory,
            ExecutablePath = ExecutablePath,
            Version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            Stage = stage,
            SessionStatus = sessionStatus,
            HostControlState = hostControlState,
            HostControlReason = hostControlReason,
            ActiveWorkerCount = activeWorkerCount,
            ActiveTaskIds = activeTaskIds,
            PendingApprovalCount = pendingApprovalCount,
            Rehydrated = RehydrationSummary.Rehydrated,
            RehydrationSummary = RehydrationSummary.Summary,
            StartedAt = StartedAt,
            RecordedAt = DateTimeOffset.UtcNow,
            LastRequestAt = LastRequestAt,
            LastLoopAt = LastLoopAt,
            LastLoopReason = LastLoopReason,
            RequestCount = RequestCount,
        };
    }
}
