using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IDelegatedRunRecoveryLedgerRepository
{
    DelegatedRunRecoveryLedgerSnapshot Load();

    void Save(DelegatedRunRecoveryLedgerSnapshot snapshot);
}
