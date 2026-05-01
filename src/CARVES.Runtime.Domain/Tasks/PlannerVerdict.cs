namespace Carves.Runtime.Domain.Tasks;

public enum PlannerVerdict
{
    Continue,
    SplitTask,
    PauseForReview,
    Blocked,
    Superseded,
    HumanDecisionRequired,
    Complete,
}
