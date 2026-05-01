namespace Carves.Runtime.Domain.Execution;

public enum WorkerSandboxMode
{
    ReadOnly = 0,
    WorkspaceWrite = 1,
    DangerFullAccess = 2,
}
