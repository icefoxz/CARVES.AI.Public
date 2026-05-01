namespace Carves.Runtime.Application.ControlPlane;

public sealed class ControlPlaneLockHandle : IDisposable
{
    private readonly Action release;
    private bool disposed;

    public ControlPlaneLockHandle(string scope, Action release, ControlPlaneLockLeaseSnapshot? lease = null)
    {
        Scope = scope;
        this.release = release;
        Lease = lease;
    }

    public string Scope { get; }

    public string? LeaseId => Lease?.LeaseId;

    public string? TaskId => Lease?.TaskId;

    public string? WorkspacePath => Lease?.WorkspacePath;

    public ControlPlaneLockLeaseSnapshot? Lease { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        release();
    }
}
