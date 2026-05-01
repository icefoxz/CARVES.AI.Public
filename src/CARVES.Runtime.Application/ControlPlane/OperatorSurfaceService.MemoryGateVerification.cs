using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult MemoryVerify(bool strict, bool asJson)
    {
        if (!strict)
        {
            return OperatorCommandResult.Failure("Usage: memory verify --strict [--json]");
        }

        var report = new MemoryVerifyStrictService(paths.RepoRoot).RunStrict();
        if (asJson)
        {
            return new OperatorCommandResult(report.ExitCode, [operatorApiService.ToJson(report)]);
        }

        var lines = new List<string>
        {
            $"Memory verify ({report.Mode}): {(report.Passed ? "pass" : "fail")}",
            $"Command: {report.VerifyCommand}",
            $"Critical failures: {report.Metrics.CriticalFailureCount}",
            $"Warnings: {report.Metrics.WarningCount}",
            string.Empty,
            "Checks:",
        };

        foreach (var check in report.Checks)
        {
            lines.Add($"- [{check.Severity}] {check.CheckId}: {check.Status}");
            lines.Add($"  {check.Summary}");
        }

        return new OperatorCommandResult(report.ExitCode, lines);
    }
}
