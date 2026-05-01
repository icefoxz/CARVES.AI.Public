using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Host;

internal sealed class HostedRuntimeLoopService
{
    private readonly RuntimeServices services;
    private readonly int intervalMilliseconds;

    public HostedRuntimeLoopService(RuntimeServices services, int intervalMilliseconds)
    {
        this.services = services;
        this.intervalMilliseconds = Math.Max(100, intervalMilliseconds);
    }

    public async Task RunAsync(LocalHostState hostState, Action<string> persistSnapshot, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var reason = AdvanceRunningInstances();
            hostState.RecordLoop(reason);
            persistSnapshot(reason);
            try
            {
                await Task.Delay(intervalMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public void PauseRunningInstances(string reason)
    {
        foreach (var instance in services.RuntimeInstanceManager.List().Where(instance => instance.Status == RuntimeInstanceStatus.Running))
        {
            try
            {
                services.RuntimeInstanceManager.Pause(instance.RepoId, reason);
            }
            catch
            {
            }
        }
    }

    private string AdvanceRunningInstances()
    {
        var runningInstances = services.RuntimeInstanceManager.List()
            .Where(instance => instance.Status == RuntimeInstanceStatus.Running)
            .ToArray();
        if (runningInstances.Length == 0)
        {
            return "Host loop idle: no running runtime instances.";
        }

        var advanced = 0;
        var paused = new List<string>();
        foreach (var instance in runningInstances)
        {
            try
            {
                var descriptor = services.RepoRegistryService.Inspect(instance.RepoId);
                var repoServices = RuntimeComposition.Create(descriptor.RepoPath);
                var session = repoServices.DevLoopService.GetSession();
                if (session is null)
                {
                    continue;
                }

                if (session.Status is RuntimeSessionStatus.Paused or RuntimeSessionStatus.Stopped or RuntimeSessionStatus.Failed)
                {
                    continue;
                }

                repoServices.DevLoopService.RunContinuousLoop(session.DryRun, 1);
                advanced += 1;
            }
            catch (Exception exception)
            {
                try
                {
                    services.RuntimeInstanceManager.Pause(
                        instance.RepoId,
                        $"Resident host paused runtime after loop failure: {exception.Message}");
                }
                catch
                {
                }

                paused.Add($"{instance.RepoId}: {exception.Message}");
            }
        }

        if (paused.Count == 0)
        {
            return $"Host loop advanced {advanced} running runtime instance(s).";
        }

        return $"Host loop advanced {advanced} runtime instance(s) and paused {paused.Count} after loop failure(s): {string.Join(" | ", paused)}";
    }
}
