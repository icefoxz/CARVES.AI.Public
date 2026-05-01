using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult FleetStatus(bool full)
    {
        var snapshot = new FleetSnapshotService(
            hostRegistryService,
            repoRuntimeService,
            new RepoHostMappingService(repoRegistryService, repoRuntimeService, hostRegistryService))
            .Build();
        return OperatorSurfaceFormatter.FleetStatus(snapshot, full);
    }
}
