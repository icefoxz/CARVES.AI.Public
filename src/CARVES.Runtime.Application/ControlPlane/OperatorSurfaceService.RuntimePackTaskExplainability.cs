using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimePackTaskExplainability(string taskId)
    {
        return FormatRuntimePackTaskExplainability(CreateRuntimePackTaskExplainabilityService().Build(taskId));
    }

    public OperatorCommandResult ApiRuntimePackTaskExplainability(string taskId)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackTaskExplainabilityService().Build(taskId)));
    }

    private RuntimePackTaskExplainabilityService CreateRuntimePackTaskExplainabilityService()
    {
        return new RuntimePackTaskExplainabilityService(paths, artifactRepository, executionRunService);
    }

    private static OperatorCommandResult FormatRuntimePackTaskExplainability(RuntimePackTaskExplainabilitySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack task explainability",
            $"Task: {surface.TaskId}",
            surface.Summary,
            $"Coverage: {surface.Coverage.Summary}",
        };

        if (surface.CurrentSelection is null)
        {
            lines.Add("Current selection: none");
        }
        else
        {
            lines.Add($"Current selection: {surface.CurrentSelection.PackId}@{surface.CurrentSelection.PackVersion} ({surface.CurrentSelection.Channel})");
            lines.Add($"Selection mode: {surface.CurrentSelection.SelectionMode}");
            if (!string.IsNullOrWhiteSpace(surface.CurrentSelection.ArtifactRef))
            {
                lines.Add($"Artifact ref: {surface.CurrentSelection.ArtifactRef}");
            }
            if (surface.CurrentSelection.DeclarativeContribution is not null)
            {
                lines.Add($"Declarative contribution: {surface.CurrentSelection.DeclarativeContribution.Summary}");
            }
        }

        if (surface.CurrentReviewRubricProjection is null)
        {
            lines.Add("Review rubric projection: none");
        }
        else
        {
            var projection = surface.CurrentReviewRubricProjection;
            lines.Add($"Review rubric projection: {projection.PackId}@{projection.PackVersion} ({projection.Channel}); rubrics={projection.RubricCount}; checklist_items={projection.ChecklistItemCount}");
            foreach (var rubric in projection.Rubrics.Take(3))
            {
                lines.Add($"- rubric {rubric.RubricId}: {rubric.Description}");
                foreach (var item in rubric.ChecklistItems.Take(3))
                {
                    lines.Add($"  - [{item.Severity}] {item.ChecklistItemId}: {item.Text}");
                }
            }
        }

        if (surface.RecentRuns.Count > 0)
        {
            lines.Add("Task-attributed runs:");
            foreach (var entry in surface.RecentRuns.Take(5))
            {
                var scope = entry.MatchesCurrentSelection ? "current" : "historical";
                var declarativeScope = entry.MatchesCurrentDeclarativeContribution ? "current" : "historical";
                lines.Add($"- {entry.RunId}: pack={entry.PackId}@{entry.PackVersion} ({entry.Channel}); status={entry.RunStatus}; scope={scope}; declarative_scope={declarativeScope}");
            }
        }
        else
        {
            lines.Add("Task-attributed runs: none");
        }

        if (surface.RecentReports.Count > 0)
        {
            lines.Add("Task-attributed reports:");
            foreach (var entry in surface.RecentReports.Take(5))
            {
                var scope = entry.MatchesCurrentSelection ? "current" : "historical";
                var declarativeScope = entry.MatchesCurrentDeclarativeContribution ? "current" : "historical";
                lines.Add($"- {entry.RunId}: pack={entry.PackId}@{entry.PackVersion} ({entry.Channel}); files_changed={entry.FilesChanged}; scope={scope}; declarative_scope={declarativeScope}");
            }
        }
        else
        {
            lines.Add("Task-attributed reports: none");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
