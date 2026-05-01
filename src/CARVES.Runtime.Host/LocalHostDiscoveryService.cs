using System.Diagnostics;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Host;

internal sealed class LocalHostDiscoveryService
{
    private const int HandshakeRetryCount = 3;
    private static readonly TimeSpan HandshakeRetryDelay = TimeSpan.FromMilliseconds(150);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public HostDiscoveryResult Discover(string repoRoot, bool allowMachineDescriptorFallbackForForeignRepo = false)
    {
        var descriptorPath = ResolveDescriptorPath(repoRoot);
        var usingMachineDescriptor = string.Equals(
            descriptorPath,
            LocalHostPaths.GetMachineDescriptorPath(),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        var snapshotStore = new LocalHostSnapshotStore(repoRoot);
        var snapshot = snapshotStore.Load();
        if (!File.Exists(descriptorPath))
        {
            var recovered = TryRecoverHealthyGeneration(repoRoot, snapshotStore, snapshot, currentDescriptor: null);
            return recovered ?? new HostDiscoveryResult(false, BuildMissingHostMessage(snapshot), null, null, snapshot);
        }

        var descriptor = LocalHostDescriptorStore.TryRead(descriptorPath);

        if (descriptor is null)
        {
            SafeDelete(descriptorPath);
            var recovered = TryRecoverHealthyGeneration(repoRoot, snapshotStore, snapshot, currentDescriptor: null);
            return recovered ?? new HostDiscoveryResult(false, "Host discovery descriptor is unreadable. Start a new host.", null, null, snapshot);
        }

        if (!DescriptorMatchesRepoRoot(repoRoot, descriptor.RepoRoot)
            && !(allowMachineDescriptorFallbackForForeignRepo && usingMachineDescriptor))
        {
            return new HostDiscoveryResult(false, BuildMissingHostMessage(snapshot), null, null, snapshot);
        }

        try
        {
            var handshake = FetchHandshake(descriptor.BaseUrl);

            UpdateHostRegistry(repoRoot, descriptor, HostInstanceStatus.Active);
            var liveSnapshot = BuildLiveSnapshot(repoRoot, descriptor, handshake, snapshot);
            snapshotStore.Save(liveSnapshot);
            return new HostDiscoveryResult(true, "Resident host is running.", handshake, descriptor, liveSnapshot);
        }
        catch
        {
            var processAlive = IsProcessAlive(descriptor.ProcessId);
            if (!processAlive)
            {
                UpdateHostRegistry(repoRoot, descriptor, HostInstanceStatus.Stopped);
                SafeDelete(descriptorPath);
                if (snapshot?.State == HostRuntimeSnapshotState.Stopped)
                {
                    var recoveredFromStoppedSnapshot = TryRecoverHealthyGeneration(repoRoot, snapshotStore, snapshot, currentDescriptor: descriptor);
                    return recoveredFromStoppedSnapshot ?? new HostDiscoveryResult(false, BuildMissingHostMessage(snapshot), null, null, snapshot);
                }

                var stoppedSnapshot = snapshot is null
                    ? new HostRuntimeSnapshot
                    {
                        RepoRoot = repoRoot,
                        State = HostRuntimeSnapshotState.Stopped,
                        Summary = "Resident host is no longer running.",
                        BaseUrl = descriptor.BaseUrl,
                        Port = descriptor.Port,
                        ProcessId = descriptor.ProcessId,
                        RuntimeDirectory = descriptor.RuntimeDirectory,
                        DeploymentDirectory = descriptor.DeploymentDirectory,
                        ExecutablePath = descriptor.ExecutablePath,
                        Version = descriptor.Version,
                        Stage = descriptor.Stage,
                        StartedAt = descriptor.StartedAt,
                        RecordedAt = DateTimeOffset.UtcNow,
                    }
                    : new HostRuntimeSnapshot
                    {
                        RepoRoot = snapshot.RepoRoot,
                        State = HostRuntimeSnapshotState.Stopped,
                        Summary = string.Equals(snapshot.State.ToString(), HostRuntimeSnapshotState.Live.ToString(), StringComparison.OrdinalIgnoreCase)
                            ? "Resident host is no longer running."
                            : snapshot.Summary,
                        BaseUrl = snapshot.BaseUrl,
                        Port = snapshot.Port,
                        ProcessId = snapshot.ProcessId,
                        RuntimeDirectory = snapshot.RuntimeDirectory,
                        DeploymentDirectory = snapshot.DeploymentDirectory,
                        ExecutablePath = snapshot.ExecutablePath,
                        Version = snapshot.Version,
                        Stage = snapshot.Stage,
                        SessionStatus = snapshot.SessionStatus,
                        ActiveWorkerCount = snapshot.ActiveWorkerCount,
                        ActiveTaskIds = snapshot.ActiveTaskIds,
                        PendingApprovalCount = snapshot.PendingApprovalCount,
                        Rehydrated = snapshot.Rehydrated,
                        RehydrationSummary = snapshot.RehydrationSummary,
                        StartedAt = snapshot.StartedAt,
                        RecordedAt = DateTimeOffset.UtcNow,
                        LastRequestAt = snapshot.LastRequestAt,
                        LastLoopAt = snapshot.LastLoopAt,
                        LastLoopReason = snapshot.LastLoopReason,
                        RequestCount = snapshot.RequestCount,
                    };
                snapshotStore.Save(stoppedSnapshot);
                var recoveredFromStoppedState = TryRecoverHealthyGeneration(repoRoot, snapshotStore, stoppedSnapshot, currentDescriptor: descriptor);
                return recoveredFromStoppedState ?? new HostDiscoveryResult(false, BuildMissingHostMessage(stoppedSnapshot), null, null, stoppedSnapshot);
            }

            UpdateHostRegistry(repoRoot, descriptor, HostInstanceStatus.Unknown);
            var staleSnapshot = snapshot is null
                ? new HostRuntimeSnapshot
                {
                    RepoRoot = repoRoot,
                    State = HostRuntimeSnapshotState.Stale,
                    Summary = "Host descriptor existed, but no live host responded.",
                    BaseUrl = descriptor.BaseUrl,
                    Port = descriptor.Port,
                    ProcessId = descriptor.ProcessId,
                    RuntimeDirectory = descriptor.RuntimeDirectory,
                    DeploymentDirectory = descriptor.DeploymentDirectory,
                    ExecutablePath = descriptor.ExecutablePath,
                    Version = descriptor.Version,
                    Stage = descriptor.Stage,
                    StartedAt = descriptor.StartedAt,
                    RecordedAt = DateTimeOffset.UtcNow,
                }
                : new HostRuntimeSnapshot
                {
                    RepoRoot = snapshot.RepoRoot,
                    State = HostRuntimeSnapshotState.Stale,
                    Summary = "Host descriptor existed, but no live host responded.",
                    BaseUrl = snapshot.BaseUrl,
                    Port = snapshot.Port,
                    ProcessId = snapshot.ProcessId,
                    RuntimeDirectory = snapshot.RuntimeDirectory,
                    DeploymentDirectory = snapshot.DeploymentDirectory,
                    ExecutablePath = snapshot.ExecutablePath,
                    Version = snapshot.Version,
                    Stage = snapshot.Stage,
                    SessionStatus = snapshot.SessionStatus,
                    ActiveWorkerCount = snapshot.ActiveWorkerCount,
                    ActiveTaskIds = snapshot.ActiveTaskIds,
                    PendingApprovalCount = snapshot.PendingApprovalCount,
                    Rehydrated = snapshot.Rehydrated,
                    RehydrationSummary = snapshot.RehydrationSummary,
                    StartedAt = snapshot.StartedAt,
                    RecordedAt = DateTimeOffset.UtcNow,
                    LastRequestAt = snapshot.LastRequestAt,
                    LastLoopAt = snapshot.LastLoopAt,
                    LastLoopReason = snapshot.LastLoopReason,
                    RequestCount = snapshot.RequestCount,
                };
            snapshotStore.Save(staleSnapshot);
            var recoveredStale = TryRecoverHealthyGeneration(repoRoot, snapshotStore, staleSnapshot, currentDescriptor: descriptor);
            return recoveredStale ?? new HostDiscoveryResult(false, BuildMissingHostMessage(staleSnapshot), null, descriptor, staleSnapshot);
        }
    }

    public HostDiscoveryResult InspectCached(string repoRoot, bool allowMachineDescriptorFallbackForForeignRepo = false)
    {
        var descriptorPath = ResolveDescriptorPath(repoRoot);
        var usingMachineDescriptor = string.Equals(
            descriptorPath,
            LocalHostPaths.GetMachineDescriptorPath(),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        var snapshot = new LocalHostSnapshotStore(repoRoot).Load();
        if (!File.Exists(descriptorPath))
        {
            return new HostDiscoveryResult(false, BuildMissingHostMessage(snapshot), null, null, snapshot);
        }

        var descriptor = LocalHostDescriptorStore.TryRead(descriptorPath);
        if (descriptor is null)
        {
            return new HostDiscoveryResult(
                false,
                "Host discovery descriptor is unreadable. Start a new host.",
                null,
                null,
                snapshot);
        }

        if (!DescriptorMatchesRepoRoot(repoRoot, descriptor.RepoRoot)
            && !(allowMachineDescriptorFallbackForForeignRepo && usingMachineDescriptor))
        {
            return new HostDiscoveryResult(false, BuildMissingHostMessage(snapshot), null, null, snapshot);
        }

        var processAlive = IsProcessAlive(descriptor.ProcessId);
        return new HostDiscoveryResult(
            processAlive,
            processAlive
                ? "Resident host descriptor process is running. Handshake was not performed."
                : BuildMissingHostMessage(snapshot),
            null,
            descriptor,
            snapshot);
    }

    public HostDiscoveryResult EnsureCompatible(
        string repoRoot,
        IEnumerable<string> requiredCapabilities,
        bool allowMachineDescriptorFallbackForForeignRepo = false)
    {
        var discovery = Discover(repoRoot, allowMachineDescriptorFallbackForForeignRepo);
        if (!discovery.HostRunning || discovery.Summary is null)
        {
            return discovery;
        }

        var missing = requiredCapabilities
            .Where(capability => !discovery.Summary.Capabilities.Contains(capability, StringComparer.Ordinal))
            .ToArray();
        if (missing.Length == 0)
        {
            return discovery;
        }

        return new HostDiscoveryResult(
            false,
            $"Host is running but missing required capabilities: {string.Join(", ", missing)}.",
            discovery.Summary,
            discovery.Descriptor,
            discovery.Snapshot);
    }

    private static HttpClient BuildClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2),
        };
    }

    private static HostHandshakeSummary FetchHandshake(string baseUrl)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < HandshakeRetryCount; attempt++)
        {
            try
            {
                using var client = BuildClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/handshake");
                using var response = client.Send(request);
                response.EnsureSuccessStatusCode();
                var handshake = ReadContentAsJson<HostHandshakeSummary>(response.Content);
                if (handshake is null)
                {
                    throw new InvalidOperationException("Handshake returned no payload.");
                }

                return handshake;
            }
            catch (Exception ex) when (attempt < HandshakeRetryCount - 1)
            {
                lastError = ex;
                BlockingHostWait.Delay(HandshakeRetryDelay);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        throw lastError ?? new InvalidOperationException("Host handshake failed.");
    }

    private static void UpdateHostRegistry(string repoRoot, LocalHostDescriptor descriptor, HostInstanceStatus status)
    {
        var paths = ControlPlanePaths.FromRepoRoot(repoRoot);
        var repository = new JsonHostRegistryRepository(paths);
        var service = new HostRegistryService(repository);
        service.Upsert(descriptor.HostId, descriptor.MachineId, descriptor.BaseUrl, status);
    }

    private static HostDiscoveryResult? TryRecoverHealthyGeneration(
        string repoRoot,
        LocalHostSnapshotStore snapshotStore,
        HostRuntimeSnapshot? snapshot,
        LocalHostDescriptor? currentDescriptor)
    {
        foreach (var candidate in LocalHostDescriptorStore.ReadGenerationDescriptors(repoRoot))
        {
            if (!DescriptorMatchesRepoRoot(repoRoot, candidate.RepoRoot))
            {
                continue;
            }

            if (MatchesDescriptor(currentDescriptor, candidate))
            {
                continue;
            }

            try
            {
                var handshake = FetchHandshake(candidate.BaseUrl);
                LocalHostDescriptorStore.WriteActiveDescriptors(repoRoot, candidate);
                UpdateHostRegistry(repoRoot, candidate, HostInstanceStatus.Active);
                var liveSnapshot = BuildLiveSnapshot(repoRoot, candidate, handshake, snapshot);
                snapshotStore.Save(liveSnapshot);
                return new HostDiscoveryResult(
                    true,
                    "Resident host is running. Repaired active host descriptor to a healthy existing generation.",
                    handshake,
                    candidate,
                    liveSnapshot);
            }
            catch
            {
            }
        }

        return null;
    }

    private static HostRuntimeSnapshot BuildLiveSnapshot(
        string repoRoot,
        LocalHostDescriptor descriptor,
        HostHandshakeSummary handshake,
        HostRuntimeSnapshot? previousSnapshot)
    {
        return new HostRuntimeSnapshot
        {
            RepoRoot = repoRoot,
            State = HostRuntimeSnapshotState.Live,
            Summary = "Resident host is running.",
            BaseUrl = descriptor.BaseUrl,
            Port = descriptor.Port,
            ProcessId = descriptor.ProcessId,
            RuntimeDirectory = descriptor.RuntimeDirectory,
            DeploymentDirectory = descriptor.DeploymentDirectory,
            ExecutablePath = descriptor.ExecutablePath,
            Version = handshake.Version,
            Stage = handshake.Stage,
            SessionStatus = previousSnapshot?.SessionStatus,
            HostControlState = handshake.HostControlState,
            HostControlReason = handshake.HostControlReason,
            ActiveWorkerCount = handshake.ActiveWorkerCount,
            ActiveTaskIds = previousSnapshot?.ActiveTaskIds ?? Array.Empty<string>(),
            PendingApprovalCount = handshake.PendingApprovalCount,
            Rehydrated = handshake.Rehydrated,
            RehydrationSummary = handshake.RehydrationSummary,
            StartedAt = descriptor.StartedAt,
            RecordedAt = DateTimeOffset.UtcNow,
            LastRequestAt = previousSnapshot?.LastRequestAt,
            LastLoopAt = previousSnapshot?.LastLoopAt,
            LastLoopReason = previousSnapshot?.LastLoopReason,
            RequestCount = previousSnapshot?.RequestCount ?? 0,
        };
    }

    private static bool IsProcessAlive(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

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

    private static bool MatchesDescriptor(LocalHostDescriptor? left, LocalHostDescriptor right)
    {
        return left is not null
               && string.Equals(left.HostId, right.HostId, StringComparison.Ordinal)
               && left.ProcessId == right.ProcessId
               && left.StartedAt.Equals(right.StartedAt);
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

    private static string ResolveDescriptorPath(string repoRoot)
    {
        var repoDescriptor = LocalHostPaths.GetDescriptorPath(repoRoot);
        if (File.Exists(repoDescriptor))
        {
            return repoDescriptor;
        }

        var machineDescriptor = LocalHostPaths.GetMachineDescriptorPath();
        return File.Exists(machineDescriptor) ? machineDescriptor : repoDescriptor;
    }

    private static string BuildMissingHostMessage(HostRuntimeSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "No resident host is running. Run `carves host ensure --json` before relying on resident-host execution.";
        }

        return snapshot.State switch
        {
            HostRuntimeSnapshotState.Stopped => $"No resident host is running. Last snapshot: stopped at {snapshot.RecordedAt:O}.",
            HostRuntimeSnapshotState.Stale => $"No resident host is running. Host snapshot is stale as of {snapshot.RecordedAt:O}; no live host responded. Run `carves host ensure --json` before relying on resident-host execution.",
            HostRuntimeSnapshotState.Live => $"No resident host is running. Host snapshot still says live from {snapshot.RecordedAt:O}, but no live host responded. Run `carves host ensure --json` before relying on resident-host execution.",
            _ => "No resident host is running. Run `carves host ensure --json` before relying on resident-host execution.",
        };
    }

    private static bool DescriptorMatchesRepoRoot(string requestedRepoRoot, string descriptorRepoRoot)
    {
        if (string.IsNullOrWhiteSpace(descriptorRepoRoot))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(requestedRepoRoot),
            Path.GetFullPath(descriptorRepoRoot),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static T? ReadContentAsJson<T>(HttpContent content)
    {
        using var stream = content.ReadAsStream();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }
}
