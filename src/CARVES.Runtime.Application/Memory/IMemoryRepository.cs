using Carves.Runtime.Domain.Memory;

namespace Carves.Runtime.Application.Memory;

public interface IMemoryRepository
{
    IReadOnlyList<MemoryDocument> LoadCategory(string category);

    IReadOnlyList<MemoryDocument> LoadRelevantModules(IReadOnlyList<string> moduleNames);
}
