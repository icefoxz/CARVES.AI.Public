namespace Carves.Runtime.Domain.Platform;

public enum ProjectionReconciliationOutcome
{
    None,
    RefreshedFromRepoTruth,
    ReconciledDrift,
    MarkedUnavailable,
}
