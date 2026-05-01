using System.Diagnostics;
using System.Text.Json;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Host;

internal sealed class HostRehydrationService
{
    private readonly RuntimeServices services;

    public HostRehydrationService(RuntimeServices services)
    {
        this.services = services;
    }

    public HostRehydrationSummary Rehydrate()
    {
        var cleanupActions = new List<string>();
        var staleMarkerCount = 0;

        staleMarkerCount += CleanupStaleDescriptor(cleanupActions);
        staleMarkerCount += CleanupStagingDirectories(cleanupActions);
        var delegatedRunReport = services.DelegatedWorkerLifecycleReconciliationService.RehydrateAfterHostRestart(
            "Resident host restart rehydration reconciled delegated worker lifecycle truth.");
        cleanupActions.AddRange(delegatedRunReport.Actions);
        var resourceCleanupReport = services.WorktreeResourceCleanupService.Cleanup("host_startup", includeRuntimeResidue: true);
        cleanupActions.AddRange(resourceCleanupReport.Actions);

        var pausedRuntimeCount = PauseRunningInstances(cleanupActions);
        var pendingApprovals = services.OperatorApiService.GetPendingWorkerPermissionRequests();
        var actorSessions = services.OperatorApiService.GetActorSessions();
        var incidents = services.OperatorApiService.GetRuntimeIncidents();
        var operatorOsEvents = services.OperatorApiService.GetOperatorOsEvents();

        var summaryParts = new List<string>
        {
            pausedRuntimeCount == 0
                ? "No running runtime instances required rehydration pause."
                : $"Paused {pausedRuntimeCount} runtime instance(s) pending explicit resume after host restart.",
            pendingApprovals.Count == 0
                ? "No pending approval requests were recovered."
                : $"Recovered {pendingApprovals.Count} pending approval request(s).",
            actorSessions.Count == 0
                ? "No persisted actor sessions were recovered."
                : $"Recovered {actorSessions.Count} actor session(s).",
            delegatedRunReport.InvalidatedLeaseCount == 0 && delegatedRunReport.ReconciledTaskCount == 0
                ? "No delegated worker lifecycle required restart reconciliation."
                : $"Invalidated {delegatedRunReport.InvalidatedLeaseCount} delegated lease(s) and reconciled {delegatedRunReport.ReconciledTaskCount} task(s).",
            resourceCleanupReport.RemovedWorktreeCount == 0
                ? "No stale worktrees required startup cleanup."
                : $"Removed {resourceCleanupReport.RemovedWorktreeCount} stale worktree(s) during startup cleanup.",
        };
        if (staleMarkerCount > 0)
        {
            summaryParts.Add($"Cleaned {staleMarkerCount} stale host marker(s).");
        }

        return new HostRehydrationSummary(
            Rehydrated: true,
            PendingApprovalCount: pendingApprovals.Count,
            ActorSessionCount: actorSessions.Count,
            RecentIncidentCount: incidents.Count,
            RecentOperatorEventCount: operatorOsEvents.Count,
            StaleMarkerCount: staleMarkerCount,
            PausedRuntimeCount: pausedRuntimeCount,
            CleanupActions: cleanupActions,
            Summary: string.Join(' ', summaryParts));
    }

    private int CleanupStaleDescriptor(ICollection<string> cleanupActions)
    {
        var descriptorPath = LocalHostPaths.GetDescriptorPath(services.Paths.RepoRoot);
        if (!File.Exists(descriptorPath))
        {
            return 0;
        }

        try
        {
            var descriptor = JsonSerializer.Deserialize<LocalHostDescriptor>(
                File.ReadAllText(descriptorPath),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                });
            if (descriptor is not null && IsProcessAlive(descriptor.ProcessId))
            {
                return 0;
            }
        }
        catch
        {
        }

        try
        {
            File.Delete(descriptorPath);
            cleanupActions.Add("Deleted stale host discovery descriptor.");
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private int CleanupStagingDirectories(ICollection<string> cleanupActions)
    {
        var deploymentsDirectory = LocalHostPaths.GetDeploymentsDirectory(services.Paths.RepoRoot);
        if (!Directory.Exists(deploymentsDirectory))
        {
            return 0;
        }

        var cleaned = 0;
        foreach (var directory in Directory.EnumerateDirectories(deploymentsDirectory, "*.staging*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                cleanupActions.Add($"Deleted stale host staging directory '{Path.GetFileName(directory)}'.");
                cleaned += 1;
            }
            catch
            {
            }
        }

        return cleaned;
    }

    private int PauseRunningInstances(ICollection<string> cleanupActions)
    {
        var paused = 0;
        foreach (var instance in services.RuntimeInstanceManager.List().Where(item => item.Status == RuntimeInstanceStatus.Running))
        {
            var descriptor = services.RepoRegistryService.Inspect(instance.RepoId);
            var repoServices = string.Equals(Path.GetFullPath(descriptor.RepoPath), services.Paths.RepoRoot, StringComparison.OrdinalIgnoreCase)
                ? services
                : RuntimeComposition.Create(descriptor.RepoPath);
            var session = repoServices.DevLoopService.GetSession();
            if (session is null)
            {
                continue;
            }

            if (session.Status is RuntimeSessionStatus.Stopped or RuntimeSessionStatus.Paused or RuntimeSessionStatus.Failed)
            {
                continue;
            }

            var pausedSession = repoServices.DevLoopService.PauseSession("Host restart rehydration paused the runtime; explicit resume is required.");
            instance.Status = RuntimeInstanceStatus.Paused;
            instance.ActiveSessionId = pausedSession.SessionId;
            instance.LastSchedulingReason = "host_restart_rehydration_pause";
            instance.GatewayHealth = RepoRuntimeGatewayHealthState.Healthy;
            services.RuntimeInstanceManager.Update(instance);
            cleanupActions.Add($"Paused runtime instance '{instance.RepoId}' during host rehydration.");
            paused += 1;
        }

        return paused;
    }
}
