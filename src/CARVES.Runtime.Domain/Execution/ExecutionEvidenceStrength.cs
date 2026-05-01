namespace Carves.Runtime.Domain.Execution;

public enum ExecutionEvidenceStrength
{
    Missing = 0,
    Observed = 1,
    Verifiable = 2,
    Replayable = 3,
}
