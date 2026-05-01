namespace Carves.Runtime.Domain.Execution;

public enum WorkerPermissionKind
{
    FilesystemWrite = 0,
    FilesystemDelete = 1,
    OutsideWorkspaceAccess = 2,
    NetworkAccess = 3,
    ProcessControl = 4,
    SystemConfiguration = 5,
    SecretAccess = 6,
    ElevatedPrivilege = 7,
    UnknownPermissionRequest = 8,
}
