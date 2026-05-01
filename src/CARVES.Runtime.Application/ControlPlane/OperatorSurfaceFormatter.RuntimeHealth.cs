using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeReconciliation(DelegatedRunLifecycleSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "Runtime reconciliation",
            $"Generated at: {snapshot.GeneratedAt:O}",
            $"Tracked delegated runs: {snapshot.Records.Count}",
        };

        if (snapshot.Records.Count == 0)
        {
            lines.Add("No delegated worker lifecycle records were found.");
            return new OperatorCommandResult(0, lines);
        }

        lines.AddRange(snapshot.Records.Select(record =>
            $"- {record.TaskId}: state={record.State}; task={record.TaskStatus}; lease={record.LeaseStatus?.ToString() ?? "(none)"}; recovery={record.RecoveryAction}; next={record.RecommendedNextAction}; ledger={record.LatestRecoveryEntryId ?? "(none)"}"));
        var exitCode = snapshot.Records.Any(record => record.RequiresOperatorAction) ? 1 : 0;
        return new OperatorCommandResult(exitCode, lines);
    }

    public static OperatorCommandResult PlatformLint(PlatformVocabularyLintReport report)
    {
        var lines = new List<string>
        {
            $"Platform lint findings: {report.Findings.Count}",
            $"Warnings: {report.WarningCount}",
            $"Suggestions: {report.SuggestionCount}",
        };
        lines.AddRange(report.Findings.Take(20).Select(finding =>
            $"- {finding.RuleId} [{finding.Severity}] {finding.RelativePath} :: {finding.SymbolName} :: {finding.Message}"));
        return new OperatorCommandResult(report.HasBlockingViolations ? 1 : 0, lines);
    }
}
