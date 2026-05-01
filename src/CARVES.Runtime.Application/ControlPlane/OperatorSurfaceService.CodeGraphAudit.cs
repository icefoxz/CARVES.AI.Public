using Carves.Runtime.Application.CodeGraph;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult AuditCodeGraph(bool strict)
    {
        var report = new CodeGraphAuditService(repoRoot, paths, systemConfig, codeGraphQueryService).Audit();
        var lines = new List<string>
        {
            "CodeGraph audit:",
            $"Strict passed: {report.StrictPassed}",
            $"Modules: {report.ModuleCount}",
            $"Files: {report.FileCount}",
            $"Callables: {report.CallableCount}",
            $"Dependencies: {report.DependencyCount}",
        };

        if (report.DeltaFromPrevious is not null)
        {
            lines.Add($"Delta modules/files/callables/dependencies: {report.DeltaFromPrevious.ModuleDelta}/{report.DeltaFromPrevious.FileDelta}/{report.DeltaFromPrevious.CallableDelta}/{report.DeltaFromPrevious.DependencyDelta}");
        }
        else
        {
            lines.Add("Delta modules/files/callables/dependencies: (no previous audit)");
        }

        lines.Add("Module purity:");
        if (report.ModulePurity.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var module in report.ModulePurity.Take(10))
            {
                lines.Add($"- {module.Module}: purity={module.PurityRatio:0.00}; source_files={module.SourceFiles}; total_files={module.TotalFiles}");
            }
        }

        lines.Add("Findings:");
        if (report.Findings.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var finding in report.Findings.Take(20))
            {
                lines.Add($"- [{finding.Severity}] {finding.Category}: {finding.Path} -> {finding.Message}");
            }
        }

        var exitCode = strict && !report.StrictPassed ? 1 : 0;
        return new OperatorCommandResult(exitCode, lines);
    }
}
