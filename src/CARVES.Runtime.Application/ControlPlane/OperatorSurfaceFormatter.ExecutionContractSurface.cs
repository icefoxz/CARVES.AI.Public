using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult ExecutionContractSurface(ExecutionContractSurfaceSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "Execution contract surface",
            $"Surface id: {snapshot.SurfaceId}",
            $"Generated at: {snapshot.GeneratedAt:O}",
            $"Summary: {snapshot.Summary}",
        };

        lines.Add("Contracts:");
        foreach (var contract in snapshot.Contracts.OrderBy(item => item.ContractId, StringComparer.Ordinal))
        {
            lines.Add($"- {contract.ContractId} [{contract.Availability}]");
            lines.Add($"  schema: {contract.SchemaPath}");
            lines.Add($"  summary: {contract.Summary}");
            lines.Add($"  lineage: {(contract.CurrentTruthLineage.Length == 0 ? "(none)" : string.Join(" | ", contract.CurrentTruthLineage))}");
            if (contract.Notes.Length > 0)
            {
                lines.Add($"  notes: {string.Join(" | ", contract.Notes)}");
            }
        }

        lines.Add("Planner verdict contracts:");
        foreach (var verdict in snapshot.PlannerVerdicts.OrderBy(item => item.OutcomeClass).ThenBy(item => item.ContractId, StringComparer.Ordinal))
        {
            lines.Add($"- {verdict.ContractId} [{verdict.OutcomeClass}] legacy={verdict.LegacyVerdict ?? "(none)"} task_status={verdict.ResultingTaskStatus}");
            lines.Add($"  summary: {verdict.Summary}");
            lines.Add($"  flags: planner_only={verdict.PlannerOnly}; review={verdict.RequiresReviewBoundary}; human={verdict.RequiresHumanReview}; replan={verdict.RequestsReplan}; failure={verdict.IndicatesFailure}; quarantine={verdict.IndicatesQuarantine}");
        }

        return new OperatorCommandResult(0, lines);
    }
}
