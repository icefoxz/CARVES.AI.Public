namespace Carves.Runtime.Application.ControlPlane;

public sealed class NoOpControlPlaneLockService : IControlPlaneLockService
{
    public static readonly NoOpControlPlaneLockService Instance = new();

    private NoOpControlPlaneLockService()
    {
    }

    public ControlPlaneLockHandle Acquire(string scope, TimeSpan? timeout = null, ControlPlaneLockOptions? options = null)
    {
        return new ControlPlaneLockHandle(scope, static () => { });
    }

    public bool TryAcquire(string scope, TimeSpan timeout, out ControlPlaneLockHandle? handle, ControlPlaneLockOptions? options = null)
    {
        handle = Acquire(scope, timeout, options);
        return true;
    }

    public ControlPlaneLockLeaseSnapshot? InspectLease(string scope)
    {
        return null;
    }
}
