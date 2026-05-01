using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IRuntimeRoutingProfileRepository
{
    RuntimeRoutingProfile? LoadActive();

    void SaveActive(RuntimeRoutingProfile profile);
}
