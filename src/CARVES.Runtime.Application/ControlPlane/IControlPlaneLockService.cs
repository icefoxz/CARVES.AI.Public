namespace Carves.Runtime.Application.ControlPlane;

public interface IControlPlaneLockService
{
    ControlPlaneLockHandle Acquire(string scope, TimeSpan? timeout = null, ControlPlaneLockOptions? options = null);

    bool TryAcquire(string scope, TimeSpan timeout, out ControlPlaneLockHandle? handle, ControlPlaneLockOptions? options = null);

    ControlPlaneLockLeaseSnapshot? InspectLease(string scope);
}
