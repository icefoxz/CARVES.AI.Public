namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerRequestBudget
{
    public static WorkerRequestBudget None { get; } = new();

    public string PolicyId { get; init; } = "runtime_governed_dynamic_request_budget_v1";

    public int TimeoutSeconds { get; init; }

    public int ProviderBaselineSeconds { get; init; }

    public ExecutionBudgetSize ExecutionBudgetSize { get; init; } = ExecutionBudgetSize.Small;

    public ExecutionConfidenceLevel ConfidenceLevel { get; init; } = ExecutionConfidenceLevel.Medium;

    public int MaxDurationMinutes { get; init; }

    public int ValidationCommandCount { get; init; }

    public bool LongRunningLane { get; init; }

    public bool RepoTruthGuidanceRequired { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Rationale { get; init; } = string.Empty;

    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}
