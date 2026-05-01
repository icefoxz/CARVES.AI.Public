using System.Net.Http;
using System.Text.Json;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostServer
{
    private static readonly TimeSpan StartupReadinessTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartupReadinessPollInterval = TimeSpan.FromMilliseconds(50);

    private void PromoteActiveHost()
    {
        WaitForLocalReadiness();
        EnsureNoOtherHealthyActiveGeneration();
        WriteDescriptor();
        SaveSnapshot(HostRuntimeSnapshotState.Live, "Resident host started.");
    }

    private void WaitForLocalReadiness()
    {
        Exception? lastError = null;
        var ready = BlockingHostWait.WaitUntil(
            StartupReadinessTimeout,
            StartupReadinessPollInterval,
            () =>
            {
                try
                {
                    ProbeHandshake(hostState.BaseUrl);
                    return true;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    return false;
                }
            });

        if (!ready)
        {
            throw new InvalidOperationException(
                lastError is null
                    ? "Resident host startup failed before readiness."
                    : $"Resident host startup failed before readiness: {lastError.Message}");
        }
    }

    private void EnsureNoOtherHealthyActiveGeneration()
    {
        var descriptor = TryReadDescriptor(LocalHostPaths.GetDescriptorPath(services.Paths.RepoRoot));
        if (descriptor is null)
        {
            return;
        }

        if (string.Equals(descriptor.HostId, hostState.HostId, StringComparison.Ordinal)
            && descriptor.ProcessId == Environment.ProcessId
            && descriptor.StartedAt.Equals(hostState.StartedAt))
        {
            return;
        }

        try
        {
            ProbeHandshake(descriptor.BaseUrl);
        }
        catch
        {
            return;
        }

        throw new InvalidOperationException(
            $"Another healthy resident host generation is already active for this repo at {descriptor.BaseUrl} (pid {descriptor.ProcessId}).");
    }

    private static LocalHostDescriptor? TryReadDescriptor(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<LocalHostDescriptor>(SharedFileAccess.ReadAllText(path), DescriptorJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void ProbeHandshake(string baseUrl)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(1),
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/handshake");
        using var response = client.Send(request);
        response.EnsureSuccessStatusCode();
    }
}
