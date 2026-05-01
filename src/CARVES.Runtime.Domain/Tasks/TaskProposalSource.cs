namespace Carves.Runtime.Domain.Tasks;

public enum TaskProposalSource
{
    None,
    CardDecomposition,
    RefactoringBacklog,
    SuggestedTask,
    FailureRecovery,
    CodeGraphOpportunity,
    MemoryAudit,
    TestCoverageOpportunity,
    PlannerGapDetection,
}
