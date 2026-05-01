using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Orchestration;

public interface IRuntimeSessionRepository
{
    RuntimeSessionState? Load();

    void Save(RuntimeSessionState session);

    void Delete();
}
