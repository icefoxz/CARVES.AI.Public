using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeArtifactCatalogService
{
    private RuntimeArtifactFamilyPolicy[] BuildCodeUnderstandingAndArtifactFamilies()
    {
        return
        [
            .. BuildCodeGraphArtifactFamilies(),
            .. BuildPackProjectionFamilies(),
        ];
    }
}
