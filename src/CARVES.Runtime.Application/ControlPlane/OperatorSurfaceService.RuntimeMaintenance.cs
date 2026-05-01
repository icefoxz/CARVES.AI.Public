using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RuntimeRepair()
    {
        var result = CreateRuntimeMaintenanceService().Repair();
        return FormatRuntimeMaintenanceResult(result);
    }

    public OperatorCommandResult RuntimeRebuild()
    {
        var result = CreateRuntimeMaintenanceService().Rebuild();
        return FormatRuntimeMaintenanceResult(result);
    }

    public OperatorCommandResult RuntimeResetDerived()
    {
        var result = CreateRuntimeMaintenanceService().ResetDerived();
        return FormatRuntimeMaintenanceResult(result);
    }

    public OperatorCommandResult CompactHistory()
    {
        var result = CreateRuntimeMaintenanceService().CompactHistory();
        return FormatRuntimeMaintenanceResult(result);
    }

    public OperatorCommandResult RuntimeHealth()
    {
        var health = CreateRuntimeHealthCheckService().Evaluate();
        return new OperatorCommandResult(0,
        [
            $"Runtime health: {health.State.ToString().ToLowerInvariant()}",
            $"Summary: {health.Summary}",
            $"Suggested action: {health.SuggestedAction}",
            $"Issues: {health.Issues.Count}",
            .. health.Issues.Select(issue => $"- {issue.Code}: {issue.Summary}")
        ]);
    }

    private RuntimeHealthCheckService CreateRuntimeHealthCheckService()
    {
        return new RuntimeHealthCheckService(paths, taskGraphService);
    }

    private RuntimeMaintenanceService CreateRuntimeMaintenanceService()
    {
        return new RuntimeMaintenanceService(
            repoRoot,
            paths,
            systemConfig,
            taskGraphService,
            new RuntimeManifestService(paths),
            CreateRuntimeHealthCheckService(),
            codeGraphBuilder,
            codeGraphQueryService);
    }

    private static OperatorCommandResult FormatRuntimeMaintenanceResult(RuntimeMaintenanceResult result)
    {
        var lines = new List<string>
        {
            $"Runtime maintenance: {result.Operation}",
            $"Health: {result.Health.State.ToString().ToLowerInvariant()}",
            $"Summary: {result.Health.Summary}",
            $"Suggested action: {result.Health.SuggestedAction}",
            $"Repaired tasks: {(result.RepairedTaskIds.Count == 0 ? "(none)" : string.Join(", ", result.RepairedTaskIds))}",
            "Commit hygiene: feature commits carry scoped source/docs/tests and stable definition truth; truth-only checkpoints carry task/writeback and bounded checkpoint mirrors; live state/history stays local.",
        };
        if (!string.IsNullOrWhiteSpace(result.CodeGraphOutputPath) && !string.IsNullOrWhiteSpace(result.CodeGraphIndexPath))
        {
            lines.Add($"CodeGraph rebuilt: true");
            lines.Add($"CodeGraph manifest path: {result.CodeGraphOutputPath}");
            lines.Add($"CodeGraph index path: {result.CodeGraphIndexPath}");
        }

        if (result.CodeGraphAudit is not null)
        {
            lines.Add($"CodeGraph audit strict passed: {result.CodeGraphAudit.StrictPassed}");
            lines.Add($"CodeGraph audit findings: {result.CodeGraphAudit.Findings.Count}");
        }

        if (result.SustainabilityAudit is not null)
        {
            lines.Add($"Sustainability audit strict passed: {result.SustainabilityAudit.StrictPassed}");
            lines.Add($"Sustainability findings: {result.SustainabilityAudit.Findings.Length}");
            var recommended = result.SustainabilityAudit.Families
                .Where(item => item.RecommendedAction != RuntimeMaintenanceActionKind.None)
                .Select(item => $"{item.FamilyId}:{item.RecommendedAction}")
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToArray();
            lines.Add($"Sustainability actions: {(recommended.Length == 0 ? "(none)" : string.Join(", ", recommended))}");
        }

        if (result.HistoryCompaction is not null)
        {
            lines.Add($"History archive root: {result.HistoryCompaction.ArchiveRoot}");
            lines.Add($"History archived files: {result.HistoryCompaction.ArchivedFileCount}");
            lines.Add($"History preserved hot files: {result.HistoryCompaction.PreservedHotFileCount}");
            lines.Add("Commit hygiene next: compact-history reduces local operational history pressure but does not turn archived history into a commit target.");
        }

        lines.AddRange(result.Health.Issues.Select(issue => $"- {issue.Code}: {issue.Summary}"));
        var exitCode =
            (result.CodeGraphAudit is not null && !result.CodeGraphAudit.StrictPassed)
            || (result.SustainabilityAudit is not null && !result.SustainabilityAudit.StrictPassed)
                ? 1
                : 0;
        return new OperatorCommandResult(exitCode, lines);
    }
}
