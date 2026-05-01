using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IPlatformGovernanceRepository
{
    PlatformGovernanceSnapshot Load();

    IReadOnlyList<GovernanceEvent> LoadEvents();

    void Save(PlatformGovernanceSnapshot snapshot);

    void SaveEvents(IReadOnlyList<GovernanceEvent> events);
}
