using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IOperatorOsEventRepository
{
    OperatorOsEventSnapshot Load();

    void Save(OperatorOsEventSnapshot snapshot);
}
