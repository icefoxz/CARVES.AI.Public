using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Planning;

public sealed class PlannerRunRequest
{
    public string ProposalId { get; init; } = string.Empty;

    public string RepoRoot { get; init; } = string.Empty;

    public RuntimeSessionState Session { get; init; } = new();

    public PlannerWakeReason WakeReason { get; init; } = PlannerWakeReason.None;

    public PlannerIntent PlannerIntent { get; init; } = PlannerIntent.Planning;

    public string WakeDetail { get; init; } = string.Empty;

    public int NextPlannerRound { get; init; }

    public string GoalSummary { get; init; } = string.Empty;

    public string CurrentStage { get; init; } = string.Empty;

    public string TaskGraphSummary { get; init; } = string.Empty;

    public string BlockedTaskSummary { get; init; } = string.Empty;

    public string OpportunitySummary { get; init; } = string.Empty;

    public string MemorySummary { get; init; } = string.Empty;

    public string CodeGraphSummary { get; init; } = string.Empty;

    public string GovernanceSummary { get; init; } = string.Empty;

    public string NamingSummary { get; init; } = string.Empty;

    public string DependencySummary { get; init; } = string.Empty;

    public string FailureSummary { get; init; } = string.Empty;

    public ContextPack? ContextPack { get; init; }

    public IReadOnlyList<Opportunity> SelectedOpportunities { get; init; } = Array.Empty<Opportunity>();

    public IReadOnlyList<PlannerProposedTask> PreviewTasks { get; init; } = Array.Empty<PlannerProposedTask>();

    public IReadOnlyList<PlannerProposedDependency> PreviewDependencies { get; init; } = Array.Empty<PlannerProposedDependency>();

    public LlmRequestEnvelopeDraft? RequestEnvelopeDraft { get; init; }
}
