namespace Carves.Runtime.Domain.ExecutionPolicy;

public enum ChangeKind
{
    Schema = 0,
    Domain = 1,
    Application = 2,
    HostOrCli = 3,
    Tests = 4,
    Docs = 5,
    Config = 6,
}
