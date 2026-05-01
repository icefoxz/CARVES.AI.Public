namespace Carves.Runtime.Application.Configuration;

public sealed record PlannerAutonomyPolicy(
    int MaxPlannerRounds,
    int MaxGeneratedTasks,
    int MaxRefactorScopeFiles,
    int MaxOpportunitiesPerRound)
{
    public static PlannerAutonomyPolicy CreateDefault()
    {
        return new PlannerAutonomyPolicy(
            MaxPlannerRounds: 3,
            MaxGeneratedTasks: 8,
            MaxRefactorScopeFiles: 5,
            MaxOpportunitiesPerRound: 3);
    }
}
