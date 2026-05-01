using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

public static partial class RuntimeComposition
{
    public static RuntimeServices Create(string repoRoot) => CreateRuntimeServicesCore(repoRoot);
}
