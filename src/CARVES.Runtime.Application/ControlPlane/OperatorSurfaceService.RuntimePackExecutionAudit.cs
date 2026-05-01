using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimePackExecutionAudit()
    {
        return FormatRuntimePackExecutionAudit(CreateRuntimePackExecutionAuditService().Build());
    }

    public OperatorCommandResult ApiRuntimePackExecutionAudit()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackExecutionAuditService().Build()));
    }

    private RuntimePackExecutionAuditService CreateRuntimePackExecutionAuditService()
    {
        return new RuntimePackExecutionAuditService(paths, artifactRepository, executionRunService);
    }

    private static OperatorCommandResult FormatRuntimePackExecutionAudit(RuntimePackExecutionAuditSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack execution audit",
            surface.Summary,
            $"Coverage: {surface.Coverage.Summary}"
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
                lines.Add($"Declarative manifest: {surface.CurrentSelection.DeclarativeContribution.ManifestPath}");
            }
        }

        if (surface.RecentRuns.Count > 0)
        {
            lines.Add("Recent attributed runs:");
            foreach (var entry in surface.RecentRuns.Take(5))
            {
                var scope = entry.MatchesCurrentSelection ? "current" : "historical";
                var contributionScope = entry.MatchesCurrentDeclarativeContribution ? "current" : "historical";
                lines.Add($"- {entry.RunId}: task={entry.TaskId}; pack={entry.PackId}@{entry.PackVersion} ({entry.Channel}); status={entry.RunStatus}; scope={scope}; declarative_scope={contributionScope}");
                if (entry.DeclarativeContribution is not null)
                {
                    lines.Add($"  declarative: {entry.DeclarativeContribution.Summary}");
                }
            }
        }
        else
        {
            lines.Add("Recent attributed runs: none");
        }

        if (surface.RecentReports.Count > 0)
        {
            lines.Add("Recent attributed reports:");
            foreach (var entry in surface.RecentReports.Take(5))
            {
                var scope = entry.MatchesCurrentSelection ? "current" : "historical";
                var contributionScope = entry.MatchesCurrentDeclarativeContribution ? "current" : "historical";
                lines.Add($"- {entry.RunId}: task={entry.TaskId}; pack={entry.PackId}@{entry.PackVersion} ({entry.Channel}); files_changed={entry.FilesChanged}; scope={scope}; declarative_scope={contributionScope}");
                if (entry.DeclarativeContribution is not null)
                {
                    lines.Add($"  declarative: {entry.DeclarativeContribution.Summary}");
                }
            }
        }
        else
        {
            lines.Add("Recent attributed reports: none");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
