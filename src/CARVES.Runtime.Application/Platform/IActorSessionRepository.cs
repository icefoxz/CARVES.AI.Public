using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IActorSessionRepository
{
    ActorSessionSnapshot Load();

    void Save(ActorSessionSnapshot snapshot);
}
