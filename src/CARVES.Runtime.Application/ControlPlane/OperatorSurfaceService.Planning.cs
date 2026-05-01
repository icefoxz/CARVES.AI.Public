using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult PlanCard(string cardPath, bool persist)
    {
        var card = plannerService.ParseCard(cardPath);
        var methodology = new RuntimeMethodologyComplianceService(paths).AssessCard(card, cardPath);
        if (persist)
        {
            new RuntimeMethodologyComplianceService(paths).EnsureSatisfied(methodology, $"Card '{card.CardId}'");
            planningDraftService.EnsureCardApprovedForPlanning(card.CardId);
            var tasks = plannerService.PlanCard(cardPath, systemConfig);
            var lines = new List<string>
            {
                $"Planned {tasks.Count} tasks for {Path.GetFileName(cardPath)}.",
                $"Methodology gate: {(methodology.Applies ? (methodology.Acknowledged ? "satisfied" : "missing") : "not_required")}",
                $"Methodology coverage: {RuntimeMethodologyComplianceService.DescribeCoverage(methodology.CoverageStatus)}",
                $"Methodology summary: {methodology.Summary}",
            };
            lines.AddRange(tasks.Select(task => $"- {task.TaskId}: {task.Title}"));
            return new OperatorCommandResult(0, lines);
        }

        var scopeAnalysis = plannerService.AnalyzeCardScope(card);
        var impactAnalysis = plannerService.AnalyzeCardImpact(card);
        var preview = OperatorSurfaceFormatter.PlanPreview(card, scopeAnalysis, impactAnalysis);
        return new OperatorCommandResult(
            preview.ExitCode,
            [
                .. preview.Lines,
                $"Methodology gate: {(methodology.Applies ? (methodology.Acknowledged ? "satisfied" : "missing") : "not_required")}",
                $"Methodology coverage: {RuntimeMethodologyComplianceService.DescribeCoverage(methodology.CoverageStatus)}",
                $"Methodology summary: {methodology.Summary}",
                $"Methodology next action: {methodology.RecommendedAction}",
            ]);
    }
}
