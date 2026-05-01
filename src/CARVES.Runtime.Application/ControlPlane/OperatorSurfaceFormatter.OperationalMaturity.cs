using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult ResourceCleanup(ResourceCleanupReport report)
    {
        var lines = new List<string>
        {
            "Runtime resource cleanup",
            $"Trigger: {report.Trigger}",
            $"Executed at: {report.ExecutedAt:O}",
            $"Removed worktrees: {report.RemovedWorktreeCount}",
            $"Pruned records: {report.RemovedRecordCount}",
            $"Removed runtime residue: {report.RemovedRuntimeResidueCount}",
            $"Removed ephemeral residue: {report.RemovedEphemeralResidueCount}",
            $"Preserved active worktrees: {report.PreservedActiveWorktreeCount}",
            $"Summary: {report.Summary}",
            "Commit hygiene:",
            "- cleanup prunes EphemeralResidue only; it does not authorize committing live state or operational history.",
            "- feature or truth-only checkpoint commits should be reviewed after cleanup, not replaced by it.",
            "Actions:",
        };

        lines.AddRange(report.Actions.Count == 0 ? ["(none)"] : report.Actions.Select(action => $"- {action}"));
        if (report.SustainabilityAudit is not null)
        {
            lines.Add($"Sustainability audit strict passed: {report.SustainabilityAudit.StrictPassed}");
            lines.Add($"Sustainability findings: {report.SustainabilityAudit.Findings.Length}");
            var recommendations = report.SustainabilityAudit.Families
                .Where(item => item.RecommendedAction != RuntimeMaintenanceActionKind.None)
                .Select(item => $"{item.FamilyId}:{item.RecommendedAction}")
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToArray();
            lines.Add($"Sustainability actions: {(recommendations.Length == 0 ? "(none)" : string.Join(", ", recommendations))}");
        }
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult RuntimeArtifactCatalog(RuntimeArtifactCatalog catalog)
    {
        var lines = new List<string>
        {
            "Runtime artifact catalog",
            $"Catalog id: {catalog.CatalogId}",
            $"Generated at: {catalog.GeneratedAt:O}",
            $"Families: {catalog.Families.Length}",
            "Class guidance:",
            "- CanonicalTruth: versioned source of truth that should stay small and reviewable.",
            "- GovernedMirror: repo-visible projection or mirror that remains inspectable but regenerable from stronger truth.",
            "- DerivedTruth: rebuildable projection or summary that should not accumulate raw runtime residue.",
            "- OperationalHistory: bounded historical detail that stays online only behind review and compaction discipline.",
            "- LiveState: inspectable current-session or machine drift that should not be treated as a normal feature-commit target.",
            "- EphemeralResidue: temporary spill that should be pruned rather than archived.",
            "- AuditArchive: archived or incident detail kept for traceability outside the default read path.",
            "Commit classes:",
        };

        lines.AddRange(RuntimeCommitHygieneService.DescribeCommitClasses().Select(line => $"- {line}"));
        lines.Add("Maintenance mapping:");
        lines.AddRange(RuntimeCommitHygieneService.DescribeMaintenanceMapping().Select(line => $"- {line}"));
        lines.Add("Closure disciplines:");
        lines.Add("- cleanup_only: cleanup-only residue is pruned locally and must not be reclassified as compactable history or truth.");
        lines.Add("- compact_history: bounded local history remains local and archive-ready after its online window drifts.");
        lines.Add("- truth_checkpoint / feature_commit / rebuild_then_checkpoint: truth-bearing and rebuildable families still require governed writeback or rebuild, not cleanup.");
        lines.Add("Families:");
        lines.AddRange(catalog.Families.Select(family =>
        {
            var hygiene = RuntimeCommitHygieneService.Resolve(family);
            var hotWindow = family.Budget.HotWindowCount?.ToString() ?? "n/a";
            var maxAgeDays = family.Budget.MaxAgeDays?.ToString() ?? "n/a";
            return $"- {family.FamilyId} [{family.ArtifactClass}/{family.RetentionMode}/{family.DefaultReadVisibility}] commit={RuntimeCommitHygieneService.FormatCommitClass(hygiene.CommitClass)} retention={RuntimeCommitHygieneService.DescribeRetentionDiscipline(family)} hot_window={hotWindow} max_age_days={maxAgeDays} closure={RuntimeCommitHygieneService.DescribeClosureDiscipline(family)} archive={RuntimeCommitHygieneService.DescribeArchiveReadinessState(family)} cleanup={family.CleanupEligible} compact={family.CompactEligible} rebuild={family.RebuildEligible}";
        }));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult SustainabilityAudit(SustainabilityAuditReport report)
    {
        var lines = new List<string>
        {
            "Runtime sustainability audit",
            $"Catalog id: {report.CatalogId}",
            $"Generated at: {report.GeneratedAt:O}",
            $"Strict passed: {report.StrictPassed}",
            $"Findings: {report.Findings.Length}",
            "Class guidance: governed mirrors stay versioned summary views; live state stays inspectable but outside normal commit expectations.",
            "Commit classes:",
        };

        lines.AddRange(RuntimeCommitHygieneService.DescribeCommitClasses().Select(line => $"- {line}"));
        lines.Add("Maintenance mapping:");
        lines.AddRange(RuntimeCommitHygieneService.DescribeMaintenanceMapping().Select(line => $"- {line}"));
        lines.AddRange(
        [
            "Families:",
        ]);

        if (report.Families.Length == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var family in report.Families)
            {
                var hygiene = RuntimeCommitHygieneService.Resolve(family);
                var hotWindow = family.HotWindowCount?.ToString() ?? "n/a";
                var maxAgeDays = family.MaxAgeDays?.ToString() ?? "n/a";
                lines.Add($"- {family.FamilyId}: commit={RuntimeCommitHygieneService.FormatCommitClass(hygiene.CommitClass)}; retention={family.RetentionDiscipline}; hot_window={hotWindow}; max_age_days={maxAgeDays}; closure={family.ClosureDiscipline}; archive={family.ArchiveReadinessState}; files={family.FileCount}; bytes={family.TotalBytes}; overdue={family.RetentionOverdueCount}; read_pressure={family.ReadPathPressureCount}; action={family.RecommendedAction}; within_budget={family.WithinBudget}");
            }
        }

        lines.Add("Findings detail:");
        lines.AddRange(report.Findings.Length == 0
            ? ["- (none)"]
            : report.Findings.Select(finding => $"- [{finding.Severity}] {finding.Category}: {finding.FamilyId} -> {finding.Path} ({finding.RecommendedAction}) {finding.Message}"));
        return new OperatorCommandResult(report.StrictPassed ? 0 : 1, lines);
    }

    public static OperatorCommandResult OperationalHistoryCompaction(OperationalHistoryCompactionReport report)
    {
        var lines = new List<string>
        {
            "Operational history compaction",
            $"Generated at: {report.GeneratedAt:O}",
            $"Archive root: {report.ArchiveRoot}",
            $"Archived files: {report.ArchivedFileCount}",
            $"Preserved hot files: {report.PreservedHotFileCount}",
            $"Summary: {report.Summary}",
            "Commit hygiene:",
            "- compact-history reduces OperationalHistory and bounded mirror/projection pressure so those families can remain local residue.",
            "- compacted history still does not become a normal feature or truth-only checkpoint commit target.",
            "Families:",
        };

        lines.AddRange(report.Families.Length == 0
            ? ["- (none)"]
            : report.Families.Select(family => $"- {family.FamilyId}: archived={family.ArchivedFileCount}; preserved_hot={family.PreservedHotFileCount}; entries={family.ArchiveEntryCount}"));
        lines.Add("Actions:");
        lines.AddRange(report.Actions.Length == 0 ? ["- (none)"] : report.Actions.Select(action => $"- {action}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult OperationalHistoryArchiveReadiness(OperationalHistoryArchiveReadinessReport report)
    {
        var lines = new List<string>
        {
            "Operational history archive readiness",
            $"Generated at: {report.GeneratedAt:O}",
            $"Archive root: {report.ArchiveRoot}",
            $"Source compaction report: {report.SourceCompactionReportId ?? "(none)"}",
            $"Summary: {report.Summary}",
            "Families:",
        };

        lines.AddRange(report.Families.Length == 0
            ? ["- (none)"]
            : report.Families.Select(family =>
            {
                var hotWindow = family.HotWindowCount?.ToString() ?? "n/a";
                var maxAgeDays = family.MaxAgeDays?.ToString() ?? "n/a";
                return $"- {family.FamilyId}: retention={family.RetentionDiscipline}; hot_window={hotWindow}; max_age_days={maxAgeDays}; closure={family.ClosureDiscipline}; archive={family.ArchiveReadinessState}; archived={family.ArchivedFileCount}; preserved_hot={family.PreservedHotFileCount}; promotion_relevant={family.PromotionRelevantCount}; why={family.ArchiveReason}";
            }));
        lines.Add("Promotion-relevant archived entries:");
        lines.AddRange(report.PromotionRelevantEntries.Length == 0
            ? ["- (none)"]
            : report.PromotionRelevantEntries.Select(entry =>
                $"- {entry.FamilyId}: {entry.OriginalPath} [{entry.ArchivedAt:O}] archive={entry.ArchiveReadinessState} why={entry.WhyArchived} promotion_reason={entry.PromotionReason}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult OperationalHistoryArchiveFollowUp(OperationalHistoryArchiveFollowUpQueue queue)
    {
        var lines = new List<string>
        {
            "Operational history archive follow-up",
            $"Generated at: {queue.GeneratedAt:O}",
            $"Source archive readiness report: {queue.SourceArchiveReadinessReportId ?? "(none)"}",
            $"Summary: {queue.Summary}",
            "Groups:",
        };

        lines.AddRange(queue.Groups.Length == 0
            ? ["- (none)"]
            : queue.Groups.Select(group =>
                $"- {group.GroupId}: count={group.ItemCount}; action={group.RecommendedAction}; {group.Summary}"));
        lines.Add("Entries:");
        lines.AddRange(queue.Entries.Length == 0
            ? ["- (none)"]
            : queue.Entries.Select(entry =>
                $"- {entry.GroupId}: task={entry.TaskId}; path={entry.OriginalPath}; action={entry.RecommendedAction}; promotion_reason={entry.PromotionReason}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult ExecutionRunHistoricalExceptions(ExecutionRunHistoricalExceptionReport report)
    {
        var lines = new List<string>
        {
            "Execution run historical exceptions",
            $"Generated at: {report.GeneratedAt:O}",
            $"Summary: {report.Summary}",
            $"Entries: {report.Entries.Length}",
            "Entries detail:",
        };

        lines.AddRange(report.Entries.Length == 0
            ? ["- (none)"]
            : report.Entries.Select(entry =>
                $"- {entry.TaskId}: task_status={entry.TaskStatus}; latest_run={entry.LatestRunId}/{entry.LatestRunStatus}; review={entry.ReviewDecisionStatus}->{entry.ReviewResultingStatus}; validation_passed={entry.ValidationPassed}; safety={entry.SafetyOutcome}; auto_reconcile_eligible={entry.AutoReconcileEligible}; categories={string.Join(",", entry.Categories.Select(static category => category.ToString()))}; action={entry.RecommendedAction}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult DelegationReport(DelegationReport report)
    {
        var lines = new List<string>
        {
            $"Delegation report window: last {report.WindowHours}h",
            $"Generated at: {report.GeneratedAt:O}",
            $"Delegation requested: {report.DelegationRequestedCount}",
            $"Delegation completed: {report.DelegationCompletedCount}",
            $"Delegation failed: {report.DelegationFailedCount}",
            $"Manual fallbacks: {report.DelegationFallbackCount}",
            $"Bypass detections: {report.DelegationBypassCount}",
            $"Actionable events: {report.ActionableEventCount}",
            $"Downgraded noise: projection={report.ProjectionNoiseCount}, legacy={report.LegacyDebtCount}",
            "Actors:",
        };

        lines.AddRange(report.Actors.Count == 0 ? ["(none)"] : report.Actors.Select(item => $"- {item.Actor}: {item.Count}"));
        lines.Add("Outcomes:");
        lines.AddRange(report.Outcomes.Count == 0 ? ["(none)"] : report.Outcomes.Select(item => $"- {item.Outcome}: {item.Count}"));
        lines.Add("Recent actionable events:");
        lines.AddRange(report.RecentEvents.Count == 0
            ? ["(none)", "- Non-blocking delegation residue was suppressed; use `audit runtime-noise` for cleanup planning."]
            : report.RecentEvents.Select(item =>
                $"- {item.EventKind}/{item.Outcome}: actor={item.Actor} task={item.TaskId ?? "(none)"} run={item.RunId ?? "(none)"} classification={item.Classification.ToString().ToLowerInvariant()} [{item.OccurredAt:O}] {item.Summary}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult ApprovalReport(ApprovalReport report)
    {
        var lines = new List<string>
        {
            $"Approval report window: last {report.WindowHours}h",
            $"Generated at: {report.GeneratedAt:O}",
            $"Pending requests: {report.PendingRequestCount}",
            "Decisions:",
        };

        lines.AddRange(report.Decisions.Count == 0 ? ["(none)"] : report.Decisions.Select(item => $"- {item.Decision}: {item.Count}"));
        lines.Add("Actors:");
        lines.AddRange(report.Actors.Count == 0 ? ["(none)"] : report.Actors.Select(item => $"- {item.Actor}: {item.Count}"));
        lines.Add("Recent approval events:");
        lines.AddRange(report.RecentEvents.Count == 0
            ? ["(none)"]
            : report.RecentEvents.Select(item =>
                $"- {item.Decision}: request={item.PermissionRequestId} task={item.TaskId} actor={item.Actor} provider={item.ProviderId} reason={item.ReasonCode} [{item.OccurredAt:O}]"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult OperationalSummary(OperationalSummary summary)
    {
        var lines = new List<string>
        {
            $"Workers: {summary.ActiveWorkerCount} running ({summary.WorkerCount} total)",
            $"Tasks: {summary.RunningTaskCount} running / {summary.BlockedTaskCount} blocked / {summary.ReviewTaskCount} review",
            $"Approvals: {summary.PendingApprovalCount} pending",
            $"Incidents: {summary.ActiveIncidentCount} active / {summary.RecentIncidentCount} recent",
            $"Last delegation: {summary.LastDelegationTaskId ?? "(none)"}",
            $"Actionability: {summary.Actionability} (session={summary.SessionActionability}; reason={summary.ActionabilityReason})",
            $"Actionability summary: {summary.ActionabilitySummary}",
            $"Recommended next action: {summary.RecommendedNextAction}",
            $"Projection writeback: {summary.ProjectionWritebackState} ({summary.ProjectionWritebackSummary})",
        };

        if (summary.ProjectionNoiseCount > 0 || summary.LegacyDebtCount > 0)
        {
            lines.Add($"Noise: {summary.ProjectionNoiseCount} projection / {summary.LegacyDebtCount} legacy (non-blocking)");
        }

        return new OperatorCommandResult(0, lines);
    }
}
