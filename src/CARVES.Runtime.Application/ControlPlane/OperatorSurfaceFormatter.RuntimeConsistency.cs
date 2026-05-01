using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeConsistency(RuntimeConsistencyReport report)
    {
        var lines = new List<string>
        {
            "Runtime consistency check",
            $"Repo root: {report.RepoRoot}",
            $"Checked at: {report.CheckedAt:O}",
            $"Session status: {report.SessionStatus}",
            $"Active leases: {report.ActiveLeaseCount}",
            $"Running tasks: {report.RunningTaskCount}",
            $"Pending approvals: {report.PendingApprovalCount}",
        };

        if (report.HostSnapshot is not null)
        {
            lines.Add($"Host descriptor exists: {report.HostSnapshot.DescriptorExists}");
            lines.Add($"Live host running: {report.HostSnapshot.LiveHostRunning}");
            lines.Add($"Host observation: {report.HostSnapshot.Message}");
            lines.Add($"Host snapshot state: {report.HostSnapshot.SnapshotState ?? "(none)"}");
            lines.Add($"Host snapshot summary: {report.HostSnapshot.SnapshotSummary ?? "(none)"}");
        }

        if (report.Findings.Count == 0)
        {
            lines.Add("Findings: 0");
            lines.Add("No runtime consistency drift was detected.");
            return new OperatorCommandResult(0, lines);
        }

        lines.Add($"Findings: {report.Findings.Count}");
        lines.AddRange(report.Findings.SelectMany(FormatRuntimeConsistencyFinding));
        var exitCode = report.Findings.Any(item => item.Severity != RuntimeConsistencySeverity.Info) ? 1 : 0;
        return new OperatorCommandResult(exitCode, lines);
    }

    private static IReadOnlyList<string> FormatRuntimeConsistencyFinding(RuntimeConsistencyFinding finding)
    {
        var lines = new List<string>
        {
            $"- [{finding.Severity}] {finding.Category}: {finding.Summary}",
            $"  likely cause: {finding.LikelyCause}",
            $"  recommended action: {finding.RecommendedAction}",
        };

        if (!string.IsNullOrWhiteSpace(finding.TaskId))
        {
            lines.Add($"  task: {finding.TaskId}");
        }

        if (!string.IsNullOrWhiteSpace(finding.LeaseId))
        {
            lines.Add($"  lease: {finding.LeaseId}");
        }

        if (!string.IsNullOrWhiteSpace(finding.RunId))
        {
            lines.Add($"  run: {finding.RunId}");
        }

        if (!string.IsNullOrWhiteSpace(finding.PermissionRequestId))
        {
            lines.Add($"  permission request: {finding.PermissionRequestId}");
        }

        if (!string.IsNullOrWhiteSpace(finding.RepoTruthAnchor))
        {
            lines.Add($"  repo truth: {finding.RepoTruthAnchor}");
        }

        if (!string.IsNullOrWhiteSpace(finding.PlatformTruthAnchor))
        {
            lines.Add($"  platform truth: {finding.PlatformTruthAnchor}");
        }

        return lines;
    }
}
