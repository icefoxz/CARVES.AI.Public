using Carves.Runtime.Application.Guard;

namespace Carves.Guard.Core;

public static partial class GuardCliRunner
{
    private static void WriteGuardText(GuardCheckResult result)
    {
        Console.WriteLine("CARVES Guard check");
        Console.WriteLine($"Decision: {FormatOutcome(result.Decision.Outcome)}");
        Console.WriteLine($"Policy: {result.Decision.PolicyId}");
        Console.WriteLine($"Run: {result.Decision.RunId}");
        Console.WriteLine($"Requires Runtime task truth: {result.Decision.RequiresRuntimeTaskTruth.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Summary: {result.Decision.Summary}");
        Console.WriteLine($"Changed files: {result.Context?.ChangedFiles.Count ?? 0}");
        WriteDecisionIssues(result.Decision);
    }

    private static void WriteGuardAuditText(GuardAuditSnapshot snapshot)
    {
        Console.WriteLine("CARVES Guard audit");
        Console.WriteLine($"Recent decisions: {snapshot.Decisions.Count}");
        Console.WriteLine($"Read model: {snapshot.AuditPath}");
        WriteReadDiagnosticsText(snapshot.Diagnostics);
        if (snapshot.Decisions.Count == 0)
        {
            Console.WriteLine("No Guard decisions recorded yet.");
            return;
        }

        foreach (var record in snapshot.Decisions)
        {
            var issueCount = record.Violations.Count + record.Warnings.Count;
            Console.WriteLine($"- {record.RunId} | {record.Outcome} | policy={record.PolicyId} | files={record.ChangedFiles.Count} | issues={issueCount} | source={record.Source}");
        }
    }

    private static void WriteGuardReportText(GuardReportSnapshot report)
    {
        Console.WriteLine("CARVES Guard report");
        if (report.PolicyLoad.IsValid && report.PolicyLoad.Policy is not null)
        {
            var policy = report.PolicyLoad.Policy;
            Console.WriteLine($"Policy: {policy.PolicyId} (schema v{policy.SchemaVersion})");
            Console.WriteLine($"Allowed paths: {FormatList(policy.PathPolicy.AllowedPathPrefixes)}");
            Console.WriteLine($"Protected paths: {FormatList(policy.PathPolicy.ProtectedPathPrefixes)}");
            Console.WriteLine($"Budget: files <= {policy.ChangeBudget.MaxChangedFiles}; additions <= {FormatNullable(policy.ChangeBudget.MaxTotalAdditions)}; deletions <= {FormatNullable(policy.ChangeBudget.MaxTotalDeletions)}");
        }
        else
        {
            Console.WriteLine("Policy: invalid");
            Console.WriteLine($"Reason: {report.PolicyLoad.ErrorMessage ?? "Guard policy could not be loaded."}");
        }

        Console.WriteLine($"Posture: {report.Posture.Status}");
        Console.WriteLine($"Summary: {report.Posture.Summary}");
        Console.WriteLine($"Recent decisions: {report.Posture.TotalCount} (allow={report.Posture.AllowCount}, review={report.Posture.ReviewCount}, block={report.Posture.BlockCount})");
        Console.WriteLine($"Latest run: {report.Posture.LatestRunId ?? "(none)"}");
        WriteReadDiagnosticsText(report.Diagnostics);
    }

    private static void WriteGuardExplainText(GuardExplainResult result)
    {
        var record = result.Record!;
        Console.WriteLine("CARVES Guard explain");
        Console.WriteLine($"Run: {record.RunId}");
        Console.WriteLine($"Recorded: {record.RecordedAtUtc:O}");
        Console.WriteLine($"Decision: {record.Outcome}");
        Console.WriteLine($"Policy: {record.PolicyId}");
        Console.WriteLine($"Source: {record.Source}");
        Console.WriteLine($"Task: {record.TaskId ?? "(none)"}");
        Console.WriteLine($"Requires Runtime task truth: {record.RequiresRuntimeTaskTruth.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Summary: {record.Summary}");
        Console.WriteLine($"Changed files: {record.ChangedFiles.Count}");
        WriteReadDiagnosticsText(result.Diagnostics);

        if (record.Violations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Violations:");
            foreach (var violation in record.Violations)
            {
                Console.WriteLine($"  - {violation.RuleId}: {violation.FilePath ?? "(patch)"}");
                Console.WriteLine($"    evidence: {violation.Evidence}");
                Console.WriteLine($"    ref: {violation.EvidenceRef}");
            }
        }

        if (record.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Warnings:");
            foreach (var warning in record.Warnings)
            {
                Console.WriteLine($"  - {warning.RuleId}: {warning.FilePath ?? "(patch)"}");
                Console.WriteLine($"    evidence: {warning.Evidence}");
                Console.WriteLine($"    ref: {warning.EvidenceRef}");
            }
        }

        if (record.EvidenceRefs.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Evidence refs:");
            foreach (var evidenceRef in record.EvidenceRefs)
            {
                Console.WriteLine($"  - {evidenceRef}");
            }
        }
    }

    private static void WriteGuardRunText(GuardRunResult result)
    {
        Console.WriteLine("CARVES Guard run");
        Console.WriteLine($"Decision: {FormatOutcome(result.Decision.Outcome)}");
        Console.WriteLine($"Policy: {result.Decision.PolicyId}");
        Console.WriteLine($"Run: {result.Decision.RunId}");
        Console.WriteLine($"Task: {result.Execution.TaskId}");
        Console.WriteLine($"Mode stability: {GuardRunModeStability}");
        Console.WriteLine($"Execution outcome: {result.Execution.Outcome}");
        Console.WriteLine($"Execution accepted: {result.Execution.Accepted.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Requires Runtime task truth: {result.Decision.RequiresRuntimeTaskTruth.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Summary: {result.Decision.Summary}");
        Console.WriteLine($"Changed files: {result.Execution.ChangedFiles.Count}");
        WriteDecisionIssues(result.Decision);
    }

    private static void WriteDecisionIssues(GuardDecision decision)
    {
        if (decision.Violations.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Violations:");
            foreach (var violation in decision.Violations)
            {
                Console.WriteLine($"  - {violation.RuleId}: {violation.FilePath ?? "(patch)"} | {violation.Evidence}");
            }
        }

        if (decision.Warnings.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Warnings:");
            foreach (var warning in decision.Warnings)
            {
                Console.WriteLine($"  - {warning.RuleId}: {warning.FilePath ?? "(patch)"} | {warning.Evidence}");
            }
        }
    }
}
