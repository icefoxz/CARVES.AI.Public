using System.Diagnostics;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal static class LocalHostTerminator
{
    public static bool ForceStop(string repoRoot, LocalHostDescriptor? descriptor, string reason)
    {
        var terminated = descriptor is null;
        if (descriptor is not null)
        {
            try
            {
                var process = Process.GetProcessById(descriptor.ProcessId);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
            }

            terminated = !IsProcessAlive(descriptor.ProcessId);
        }

        if (terminated)
        {
            if (descriptor is not null)
            {
                LocalHostDescriptorStore.TryDeleteMatchingActiveDescriptors(repoRoot, descriptor);
                LocalHostDescriptorStore.TryDeleteMatchingGenerationDescriptors(repoRoot, descriptor);
            }
        }

        var snapshotState = terminated ? HostRuntimeSnapshotState.Stopped : HostRuntimeSnapshotState.Stale;
        var summary = terminated
            ? reason
            : $"{reason} Resident host process could not be terminated and may still be holding runtime files.";
        new LocalHostSnapshotStore(repoRoot).Save(new HostRuntimeSnapshot
        {
            RepoRoot = repoRoot,
            State = snapshotState,
            Summary = summary,
            BaseUrl = descriptor?.BaseUrl,
            Port = descriptor?.Port,
            ProcessId = descriptor?.ProcessId,
            RuntimeDirectory = descriptor?.RuntimeDirectory,
            DeploymentDirectory = descriptor?.DeploymentDirectory,
            ExecutablePath = descriptor?.ExecutablePath,
            Version = descriptor?.Version ?? "0.0.0",
            Stage = descriptor?.Stage ?? RuntimeStageInfo.CurrentStage,
            StartedAt = descriptor?.StartedAt ?? DateTimeOffset.UtcNow,
            RecordedAt = DateTimeOffset.UtcNow,
        });
        return terminated;
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
}
