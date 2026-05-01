using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

internal static class PlannerVerdictParser
{
    public static PlannerVerdict Parse(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "continue" => PlannerVerdict.Continue,
            "split_task" => PlannerVerdict.SplitTask,
            "pause_for_review" => PlannerVerdict.PauseForReview,
            "blocked" => PlannerVerdict.Blocked,
            "block" => PlannerVerdict.Blocked,
            "fail" => PlannerVerdict.Blocked,
            "failed" => PlannerVerdict.Blocked,
            "supersede" => PlannerVerdict.Superseded,
            "superseded" => PlannerVerdict.Superseded,
            "human_decision_required" => PlannerVerdict.HumanDecisionRequired,
            "complete" => PlannerVerdict.Complete,
            "done" => PlannerVerdict.Complete,
            _ => throw new InvalidOperationException($"Unsupported review verdict '{value}'."),
        };
    }
}
