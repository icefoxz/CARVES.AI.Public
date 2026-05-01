using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostServer
{
    private void SaveLiveLoopSnapshot(string reason)
    {
        SaveSnapshot(HostRuntimeSnapshotState.Live, reason);
    }

    private void SaveSnapshot(HostRuntimeSnapshotState state, string summary)
    {
        UpdateHostRegistry(state);
        var session = services.DevLoopService.GetSession();
        var hostSession = hostSessionService.Load();
        snapshotStore.Save(hostState.BuildSnapshot(
            state,
            summary,
            RuntimeStageInfo.CurrentStage,
            session?.Status.ToString(),
            hostSession?.ControlState.ToString(),
            hostSession?.LastControlReason,
            session?.ActiveWorkerCount ?? 0,
            session?.ActiveTaskIds ?? Array.Empty<string>(),
            session?.PendingPermissionRequestIds.Count ?? 0));
    }

    private void WriteDescriptor()
    {
        var descriptor = new LocalHostDescriptor(
            hostState.HostId,
            hostState.MachineId,
            services.Paths.RepoRoot,
            hostState.BaseUrl,
            hostState.Port,
            Environment.ProcessId,
            hostState.RuntimeDirectory,
            hostState.DeploymentDirectory,
            hostState.ExecutablePath,
            hostState.StartedAt,
            typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            RuntimeStageInfo.CurrentStage);
        LocalHostDescriptorStore.WriteActiveDescriptors(services.Paths.RepoRoot, descriptor);
        LocalHostDescriptorStore.WriteGenerationDescriptor(services.Paths.RepoRoot, descriptor);
    }

    private void UpdateHostRegistry(HostRuntimeSnapshotState state)
    {
        var status = state switch
        {
            HostRuntimeSnapshotState.Live => HostInstanceStatus.Active,
            HostRuntimeSnapshotState.Stopped => HostInstanceStatus.Stopped,
            _ => HostInstanceStatus.Unknown,
        };
        hostRegistryService.Upsert(hostState.HostId, hostState.MachineId, hostState.BaseUrl, status);
    }

    private void DeleteDescriptor()
    {
        var descriptor = new LocalHostDescriptor(
            hostState.HostId,
            hostState.MachineId,
            services.Paths.RepoRoot,
            hostState.BaseUrl,
            hostState.Port,
            Environment.ProcessId,
            hostState.RuntimeDirectory,
            hostState.DeploymentDirectory,
            hostState.ExecutablePath,
            hostState.StartedAt,
            typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            RuntimeStageInfo.CurrentStage);
        LocalHostDescriptorStore.TryDeleteMatchingActiveDescriptors(services.Paths.RepoRoot, descriptor);
        LocalHostDescriptorStore.TryDeleteMatchingGenerationDescriptors(services.Paths.RepoRoot, descriptor);
    }

    private void RequestShutdown()
    {
        if (shutdown.IsCancellationRequested)
        {
            return;
        }

        shutdown.Cancel();
        try
        {
            listener.Stop();
        }
        catch
        {
        }
    }

    private void TryStopHostSession()
    {
        try
        {
            hostSessionService.Stop("Resident host stopped cleanly.");
        }
        catch
        {
        }
    }
    private static void SafeDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private void TryDeleteMatchingDescriptor(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var descriptor = JsonSerializer.Deserialize<LocalHostDescriptor>(SharedFileAccess.ReadAllText(path), JsonOptions);
            if (descriptor is null)
            {
                return;
            }

            if (string.Equals(descriptor.HostId, hostState.HostId, StringComparison.Ordinal)
                && descriptor.ProcessId == Environment.ProcessId
                && descriptor.StartedAt.Equals(hostState.StartedAt))
            {
                SafeDelete(path);
            }
        }
        catch
        {
        }
    }
}
