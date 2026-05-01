using Carves.Runtime.Application.Guard;

namespace Carves.Guard.Core;

public static partial class GuardCliRunner
{
    private static object ToJsonContract(GuardCheckResult result)
    {
        return new
        {
            schema_version = "guard-check.v1",
            run_id = result.Decision.RunId,
            decision = FormatOutcome(result.Decision.Outcome),
            policy_id = result.Decision.PolicyId,
            summary = result.Decision.Summary,
            requires_runtime_task_truth = result.Decision.RequiresRuntimeTaskTruth,
            changed_files = result.Context?.ChangedFiles.Select((file, index) => new
            {
                index,
                path = file.Path,
                old_path = file.OldPath,
                status = FormatStatus(file.Status),
                additions = file.Additions,
                deletions = file.Deletions,
                is_binary = file.IsBinary,
                was_untracked = file.WasUntracked,
                matches_allowed_path = file.MatchesAllowedPath,
                matches_protected_path = file.MatchesProtectedPath,
                matched_allowed_prefix = file.MatchedAllowedPrefix,
                matched_protected_prefix = file.MatchedProtectedPrefix,
            }).ToArray() ?? [],
            patch_stats = result.Context is null
                ? null
                : new
                {
                    result.Context.PatchStats.ChangedFileCount,
                    result.Context.PatchStats.AddedFileCount,
                    result.Context.PatchStats.ModifiedFileCount,
                    result.Context.PatchStats.DeletedFileCount,
                    result.Context.PatchStats.RenamedFileCount,
                    result.Context.PatchStats.BinaryFileCount,
                    result.Context.PatchStats.TotalAdditions,
                    result.Context.PatchStats.TotalDeletions,
                },
            violations = result.Decision.Violations.Select(violation => new
            {
                violation.RuleId,
                severity = FormatSeverity(violation.Severity),
                violation.Message,
                violation.FilePath,
                violation.Evidence,
                violation.EvidenceRef,
            }).ToArray(),
            warnings = result.Decision.Warnings.Select(warning => new
            {
                warning.RuleId,
                warning.Message,
                warning.FilePath,
                warning.Evidence,
                warning.EvidenceRef,
            }).ToArray(),
            evidence_refs = result.Decision.EvidenceRefs,
        };
    }

    private static object ToJsonContract(GuardRunResult result)
    {
        return new
        {
            schema_version = "guard-run.v1",
            mode_stability = GuardRunModeStability,
            stable_external_entry = false,
            run_id = result.Decision.RunId,
            task_id = result.Execution.TaskId,
            decision = FormatOutcome(result.Decision.Outcome),
            policy_id = result.Decision.PolicyId,
            summary = result.Decision.Summary,
            requires_runtime_task_truth = result.Decision.RequiresRuntimeTaskTruth,
            execution = new
            {
                task_id = result.Execution.TaskId,
                accepted = result.Execution.Accepted,
                outcome = result.Execution.Outcome,
                task_status = result.Execution.TaskStatus,
                summary = result.Execution.Summary,
                failure_kind = result.Execution.FailureKind,
                run_id = result.Execution.RunId,
                execution_run_id = result.Execution.ExecutionRunId,
                changed_files = result.Execution.ChangedFiles,
                next_action = result.Execution.NextAction,
            },
            discipline_decision = new
            {
                run_id = result.DisciplineDecision.RunId,
                decision = FormatOutcome(result.DisciplineDecision.Outcome),
                summary = result.DisciplineDecision.Summary,
            },
            violations = result.Decision.Violations.Select(violation => new
            {
                violation.RuleId,
                severity = FormatSeverity(violation.Severity),
                violation.Message,
                violation.FilePath,
                violation.Evidence,
                violation.EvidenceRef,
            }).ToArray(),
            warnings = result.Decision.Warnings.Select(warning => new
            {
                warning.RuleId,
                warning.Message,
                warning.FilePath,
                warning.Evidence,
                warning.EvidenceRef,
            }).ToArray(),
            evidence_refs = result.Decision.EvidenceRefs,
        };
    }

    private static object ToJsonContract(GuardAuditSnapshot snapshot)
    {
        return new
        {
            schema_version = "guard-audit.v1",
            audit_path = snapshot.AuditPath,
            count = snapshot.Decisions.Count,
            diagnostics = ToJsonContract(snapshot.Diagnostics),
            decisions = snapshot.Decisions.Select(ToJsonContract).ToArray(),
        };
    }

    private static object ToJsonContract(GuardReportSnapshot report)
    {
        return new
        {
            schema_version = "guard-report.v1",
            audit_path = report.AuditPath,
            diagnostics = ToJsonContract(report.Diagnostics),
            policy = report.PolicyLoad.IsValid && report.PolicyLoad.Policy is not null
                ? new
                {
                    status = "valid",
                    policy_id = (string?)report.PolicyLoad.Policy.PolicyId,
                    schema_version = (int?)report.PolicyLoad.Policy.SchemaVersion,
                    allowed_path_prefixes = report.PolicyLoad.Policy.PathPolicy.AllowedPathPrefixes,
                    protected_path_prefixes = report.PolicyLoad.Policy.PathPolicy.ProtectedPathPrefixes,
                    max_changed_files = (int?)report.PolicyLoad.Policy.ChangeBudget.MaxChangedFiles,
                    max_total_additions = report.PolicyLoad.Policy.ChangeBudget.MaxTotalAdditions,
                    max_total_deletions = report.PolicyLoad.Policy.ChangeBudget.MaxTotalDeletions,
                }
                : new
                {
                    status = "invalid",
                    policy_id = (string?)null,
                    schema_version = (int?)null,
                    allowed_path_prefixes = Array.Empty<string>() as IReadOnlyList<string>,
                    protected_path_prefixes = Array.Empty<string>() as IReadOnlyList<string>,
                    max_changed_files = (int?)null,
                    max_total_additions = (int?)null,
                    max_total_deletions = (int?)null,
                },
            posture = new
            {
                report.Posture.Status,
                report.Posture.Summary,
                report.Posture.TotalCount,
                report.Posture.AllowCount,
                report.Posture.ReviewCount,
                report.Posture.BlockCount,
                report.Posture.LatestRunId,
            },
            recent_decisions = report.RecentDecisions.Select(ToJsonContract).ToArray(),
        };
    }

    private static object ToJsonContract(GuardExplainResult result)
    {
        return new
        {
            schema_version = "guard-explain.v1",
            run_id = result.RunId,
            found = result.Found,
            diagnostics = ToJsonContract(result.Diagnostics),
            decision = result.Record is null ? null : ToJsonContract(result.Record),
        };
    }

    private static object ToJsonContract(GuardDecisionAuditRecord record)
    {
        return new
        {
            record.SchemaVersion,
            record.RunId,
            record.RecordedAtUtc,
            record.Source,
            record.Outcome,
            record.PolicyId,
            record.Summary,
            record.RequiresRuntimeTaskTruth,
            record.TaskId,
            record.ExecutionOutcome,
            record.ExecutionFailureKind,
            record.ChangedFiles,
            record.PatchStats,
            record.Violations,
            record.Warnings,
            record.EvidenceRefs,
        };
    }

    private static object ToJsonContract(GuardDecisionReadDiagnostics diagnostics)
    {
        return new
        {
            diagnostics.RequestedLimit,
            diagnostics.EffectiveLimit,
            diagnostics.TotalLineCount,
            diagnostics.EmptyLineCount,
            diagnostics.LoadedRecordCount,
            diagnostics.ReturnedRecordCount,
            diagnostics.SkippedRecordCount,
            diagnostics.MalformedRecordCount,
            diagnostics.FutureVersionRecordCount,
            diagnostics.MaxStoredLineCount,
            diagnostics.IsDegraded,
        };
    }
}
