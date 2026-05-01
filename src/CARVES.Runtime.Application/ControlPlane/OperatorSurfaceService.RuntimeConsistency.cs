using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public RuntimeConsistencyReport VerifyRuntimeReport(RuntimeConsistencyHostSnapshot? hostSnapshot = null)
    {
        return runtimeConsistencyCheckService.Run(hostSnapshot);
    }

    public OperatorCommandResult VerifyRuntime(RuntimeConsistencyHostSnapshot? hostSnapshot = null)
    {
        return OperatorSurfaceFormatter.RuntimeConsistency(VerifyRuntimeReport(hostSnapshot));
    }

    public OperatorCommandResult ReconcileRuntime()
    {
        var snapshot = delegatedWorkerLifecycleReconciliationService.ReconcileKnownDrift();
        var ghostBlockerTaskIds = new HistoricalGhostBlockedTaskReconciliationService(taskGraphService, artifactRepository).Reconcile();
        if (ghostBlockerTaskIds.Count == 0)
        {
            return OperatorSurfaceFormatter.RuntimeReconciliation(snapshot);
        }

        var result = OperatorSurfaceFormatter.RuntimeReconciliation(snapshot);
        var lines = result.Lines.ToList();
        lines.Add($"Historical ghost blockers superseded: {string.Join(", ", ghostBlockerTaskIds)}");
        return new OperatorCommandResult(result.ExitCode, lines);
    }
}
