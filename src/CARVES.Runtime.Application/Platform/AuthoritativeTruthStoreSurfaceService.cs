using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class AuthoritativeTruthStoreSurfaceService
{
    private readonly AuthoritativeTruthStoreService authoritativeTruthStoreService;

    public AuthoritativeTruthStoreSurfaceService(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        authoritativeTruthStoreService = new AuthoritativeTruthStoreService(paths, lockService);
    }

    public AuthoritativeTruthStoreSurface Build()
    {
        return authoritativeTruthStoreService.BuildSurface();
    }
}
