namespace Carves.Runtime.Domain.Execution;

public enum WorkerApprovalMode
{
    Never = 0,
    OnRequest = 1,
    OnFailure = 2,
    Untrusted = 3,
}
