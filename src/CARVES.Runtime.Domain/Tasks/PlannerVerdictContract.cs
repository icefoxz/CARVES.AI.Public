namespace Carves.Runtime.Domain.Tasks;

public enum PlannerVerdictOutcomeClass
{
    Continue,
    Review,
    HumanReview,
    Replan,
    Failure,
    Superseded,
    Quarantine,
    Complete,
}

public sealed class PlannerVerdictContract
{
    public string ContractId { get; init; } = string.Empty;

    public PlannerVerdictOutcomeClass OutcomeClass { get; init; } = PlannerVerdictOutcomeClass.Continue;

    public string? LegacyVerdict { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string ResultingTaskStatus { get; init; } = string.Empty;

    public bool PlannerOnly { get; init; } = true;

    public bool RequiresReviewBoundary { get; init; }

    public bool RequiresHumanReview { get; init; }

    public bool RequestsReplan { get; init; }

    public bool IndicatesFailure { get; init; }

    public bool IndicatesQuarantine { get; init; }
}

public static class PlannerVerdictContractCatalog
{
    private static readonly PlannerVerdictContract[] Contracts =
    [
        new()
        {
            ContractId = "continue",
            OutcomeClass = PlannerVerdictOutcomeClass.Continue,
            LegacyVerdict = PlannerVerdict.Continue.ToString(),
            Summary = "Planner allows execution to continue without lifecycle writeback.",
            ResultingTaskStatus = TaskStatus.Pending.ToString(),
        },
        new()
        {
            ContractId = "review",
            OutcomeClass = PlannerVerdictOutcomeClass.Review,
            LegacyVerdict = PlannerVerdict.PauseForReview.ToString(),
            Summary = "Planner admits the result to review but stops before final writeback.",
            ResultingTaskStatus = TaskStatus.Review.ToString(),
            RequiresReviewBoundary = true,
        },
        new()
        {
            ContractId = "human_review",
            OutcomeClass = PlannerVerdictOutcomeClass.HumanReview,
            LegacyVerdict = PlannerVerdict.HumanDecisionRequired.ToString(),
            Summary = "Planner requires explicit human decision before the task can proceed.",
            ResultingTaskStatus = TaskStatus.Review.ToString(),
            RequiresReviewBoundary = true,
            RequiresHumanReview = true,
        },
        new()
        {
            ContractId = "replan",
            OutcomeClass = PlannerVerdictOutcomeClass.Replan,
            LegacyVerdict = PlannerVerdict.SplitTask.ToString(),
            Summary = "Planner stops normal execution and routes the task into bounded replan or split flow.",
            ResultingTaskStatus = TaskStatus.Review.ToString(),
            RequestsReplan = true,
        },
        new()
        {
            ContractId = "failure",
            OutcomeClass = PlannerVerdictOutcomeClass.Failure,
            LegacyVerdict = PlannerVerdict.Blocked.ToString(),
            Summary = "Planner records a failed or blocked outcome that must not be written back as success.",
            ResultingTaskStatus = TaskStatus.Blocked.ToString(),
            IndicatesFailure = true,
        },
        new()
        {
            ContractId = "superseded",
            OutcomeClass = PlannerVerdictOutcomeClass.Superseded,
            LegacyVerdict = PlannerVerdict.Superseded.ToString(),
            Summary = "Planner records an explicit superseded outcome that finalizes stale lineage without treating it as success.",
            ResultingTaskStatus = TaskStatus.Superseded.ToString(),
        },
        new()
        {
            ContractId = "quarantined",
            OutcomeClass = PlannerVerdictOutcomeClass.Quarantine,
            Summary = "Planner holds the result in quarantine because boundary or safety evidence cannot admit normal writeback.",
            ResultingTaskStatus = TaskStatus.Blocked.ToString(),
            IndicatesFailure = true,
            IndicatesQuarantine = true,
        },
        new()
        {
            ContractId = "complete",
            OutcomeClass = PlannerVerdictOutcomeClass.Complete,
            LegacyVerdict = PlannerVerdict.Complete.ToString(),
            Summary = "Planner records a validated completion that may advance task lifecycle truth.",
            ResultingTaskStatus = TaskStatus.Completed.ToString(),
        },
    ];

    public static IReadOnlyList<PlannerVerdictContract> All => Contracts;

    public static PlannerVerdictContract FromLegacy(PlannerVerdict verdict)
    {
        return Contracts.First(contract => string.Equals(contract.LegacyVerdict, verdict.ToString(), StringComparison.Ordinal));
    }
}
