namespace Carves.Runtime.Domain.Execution;

public enum WorkerFailureLayer
{
    None = 0,
    Transport = 1,
    Protocol = 2,
    Provider = 3,
    WorkerSemantic = 4,
    Environment = 5,
}
