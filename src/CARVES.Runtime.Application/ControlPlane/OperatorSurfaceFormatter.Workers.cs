using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult WorkerNodes(IReadOnlyList<WorkerNode> nodes)
    {
        return WorkerNodesCore(nodes);
    }

    public static OperatorCommandResult WorkerProviders(IReadOnlyList<WorkerBackendDescriptor> backends)
    {
        return WorkerProvidersCore(backends);
    }

    public static OperatorCommandResult WorkerProfiles(IReadOnlyList<Domain.Execution.WorkerExecutionProfile> profiles, string? repoId)
    {
        return WorkerProfilesCore(profiles, repoId);
    }

    public static OperatorCommandResult WorkerOperationalSummary(OperationalSummary summary)
    {
        return WorkerOperationalSummaryCore(summary);
    }

    public static OperatorCommandResult WorkerSelection(Domain.Execution.WorkerSelectionDecision decision)
    {
        return WorkerSelectionCore(decision);
    }

    public static OperatorCommandResult CodexRoutingEligibility(
        IReadOnlyList<WorkerBackendDescriptor> codexBackends,
        Domain.Execution.WorkerSelectionDecision selection)
    {
        return CodexRoutingEligibilityCore(codexBackends, selection);
    }

    public static OperatorCommandResult WorkerNodeChanged(string action, WorkerNode node)
    {
        return WorkerNodeChangedCore(action, node);
    }

    public static OperatorCommandResult WorkerLeases(IReadOnlyList<WorkerLeaseRecord> leases)
    {
        return WorkerLeasesCore(leases);
    }

    public static OperatorCommandResult WorkerLeaseExpired(WorkerLeaseRecord lease)
    {
        return WorkerLeaseExpiredCore(lease);
    }
}
