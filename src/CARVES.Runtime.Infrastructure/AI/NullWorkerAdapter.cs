using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class NullWorkerAdapter : DisabledWorkerAdapter
{
    public NullWorkerAdapter(string selectionReason)
        : base(selectionReason)
    {
    }

    public override string AdapterId => nameof(NullWorkerAdapter);

    public override string BackendId => "null_worker";

    public override string ProviderId => "null";

    protected override WorkerProviderCapabilities Capabilities => new()
    {
        SupportsExecution = true,
        SupportsEventStream = false,
        SupportsHealthProbe = true,
        SupportsCancellation = false,
        SupportsTrustedProfiles = false,
        SupportsNetworkAccess = false,
        SupportsDotNetBuild = true,
        SupportsLongRunningTasks = true,
    };

    public override WorkerBackendHealthSummary CheckHealth()
    {
        return new WorkerBackendHealthSummary
        {
            State = WorkerBackendHealthState.Healthy,
            Summary = "Null worker adapter is available as the governed local fallback backend.",
        };
    }
}
