using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IRuntimeIncidentTimelineRepository
{
    IReadOnlyList<RuntimeIncidentRecord> Load();

    void Save(IReadOnlyList<RuntimeIncidentRecord> records);
}
