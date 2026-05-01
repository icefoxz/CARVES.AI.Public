using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal static class RuntimeConsistencyHostCommand
{
    public static OperatorCommandResult Execute(RuntimeServices services, bool runningInsideResidentHost)
    {
        var hostSnapshot = runningInsideResidentHost
            ? new RuntimeConsistencyHostSnapshot(
                DescriptorExists: File.Exists(LocalHostPaths.GetDescriptorPath(services.Paths.RepoRoot)),
                LiveHostRunning: true,
                Message: "Verification executed through the resident host.",
                DescriptorPath: LocalHostPaths.GetDescriptorPath(services.Paths.RepoRoot),
                SnapshotPath: LocalHostPaths.GetSnapshotPath(services.Paths.RepoRoot),
                SnapshotState: HostRuntimeSnapshotState.Live.ToString(),
                SnapshotSummary: "Live resident host execution path.")
            : BuildColdHostSnapshot(services.Paths.RepoRoot);
        return services.OperatorSurfaceService.VerifyRuntime(hostSnapshot);
    }

    private static RuntimeConsistencyHostSnapshot BuildColdHostSnapshot(string repoRoot)
    {
        var descriptorPath = LocalHostPaths.GetDescriptorPath(repoRoot);
        var discovery = new LocalHostDiscoveryService().Discover(repoRoot);
        return new RuntimeConsistencyHostSnapshot(
            DescriptorExists: File.Exists(descriptorPath),
            LiveHostRunning: discovery.HostRunning,
            Message: discovery.Message,
            DescriptorPath: descriptorPath,
            SnapshotPath: LocalHostPaths.GetSnapshotPath(repoRoot),
            SnapshotState: discovery.Snapshot?.State.ToString(),
            SnapshotSummary: discovery.Snapshot?.Summary);
    }
}
