namespace Carves.Runtime.Application.Guard;

public sealed class GuardDecisionReadService
{
    private readonly GuardDecisionAuditStore store;
    private readonly GuardPolicyService policyService;

    public GuardDecisionReadService()
        : this(new GuardDecisionAuditStore(), new GuardPolicyService())
    {
    }

    public GuardDecisionReadService(GuardDecisionAuditStore store, GuardPolicyService policyService)
    {
        this.store = store;
        this.policyService = policyService;
    }

    public bool RecordCheck(string repositoryRoot, GuardCheckResult result)
    {
        return store.TryAppend(repositoryRoot, FromCheck(result));
    }

    public bool RecordRun(string repositoryRoot, GuardRunResult result)
    {
        return store.TryAppend(repositoryRoot, FromRun(result));
    }

    public GuardAuditSnapshot Audit(string repositoryRoot, int limit = 10)
    {
        var normalizedLimit = NormalizeLimit(limit);
        var result = store.LoadRecent(repositoryRoot, normalizedLimit);
        return new GuardAuditSnapshot(
            GuardDecisionAuditStore.RelativeAuditPath,
            result.Records,
            result.Diagnostics);
    }

    public GuardReportSnapshot Report(string repositoryRoot, string policyPath = ".ai/guard-policy.json", int limit = 10)
    {
        var policyLoad = policyService.Load(repositoryRoot, policyPath);
        var normalizedLimit = NormalizeLimit(limit);
        var result = store.LoadRecent(repositoryRoot, normalizedLimit);
        return new GuardReportSnapshot(
            GuardDecisionAuditStore.RelativeAuditPath,
            policyLoad,
            result.Records,
            BuildPosture(policyLoad, result.Records),
            result.Diagnostics);
    }

    public GuardExplainResult Explain(string repositoryRoot, string runId)
    {
        var result = store.Find(repositoryRoot, runId);
        return new GuardExplainResult(runId, result.Record, result.Diagnostics);
    }

    public static GuardDecisionAuditRecord FromCheck(GuardCheckResult result)
    {
        return new GuardDecisionAuditRecord(
            GuardDecisionAuditStore.CurrentSchemaVersion,
            result.Decision.RunId,
            DateTimeOffset.UtcNow,
            Source: "guard-check",
            Outcome: FormatOutcome(result.Decision.Outcome),
            result.Decision.PolicyId,
            result.Decision.Summary,
            result.Decision.RequiresRuntimeTaskTruth,
            TaskId: null,
            ExecutionOutcome: null,
            ExecutionFailureKind: null,
            ChangedFiles: result.Context?.ChangedFiles.Select(file => file.Path).ToArray() ?? Array.Empty<string>(),
            PatchStats: result.Context is null ? null : FromPatchStats(result.Context.PatchStats),
            Violations: result.Decision.Violations.Select(FromViolation).ToArray(),
            Warnings: result.Decision.Warnings.Select(FromWarning).ToArray(),
            result.Decision.EvidenceRefs);
    }

    public static GuardDecisionAuditRecord FromRun(GuardRunResult result)
    {
        return new GuardDecisionAuditRecord(
            GuardDecisionAuditStore.CurrentSchemaVersion,
            result.Decision.RunId,
            DateTimeOffset.UtcNow,
            Source: "guard-run",
            Outcome: FormatOutcome(result.Decision.Outcome),
            result.Decision.PolicyId,
            result.Decision.Summary,
            result.Decision.RequiresRuntimeTaskTruth,
            result.Execution.TaskId,
            result.Execution.Outcome,
            result.Execution.FailureKind,
            result.Execution.ChangedFiles,
            PatchStats: null,
            Violations: result.Decision.Violations.Select(FromViolation).ToArray(),
            Warnings: result.Decision.Warnings.Select(FromWarning).ToArray(),
            result.Decision.EvidenceRefs);
    }

    private static GuardReportPosture BuildPosture(
        GuardPolicyLoadResult policyLoad,
        IReadOnlyList<GuardDecisionAuditRecord> recent)
    {
        var allowCount = recent.Count(record => string.Equals(record.Outcome, "allow", StringComparison.OrdinalIgnoreCase));
        var reviewCount = recent.Count(record =>
            string.Equals(record.Outcome, "review", StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.Outcome, "needs_review", StringComparison.OrdinalIgnoreCase));
        var blockCount = recent.Count(record => string.Equals(record.Outcome, "block", StringComparison.OrdinalIgnoreCase));

        if (!policyLoad.IsValid || policyLoad.Policy is null)
        {
            return new GuardReportPosture(
                "policy_invalid",
                policyLoad.ErrorMessage ?? "Guard policy could not be loaded.",
                recent.Count,
                allowCount,
                reviewCount,
                blockCount,
                recent.FirstOrDefault()?.RunId);
        }

        if (blockCount > 0)
        {
            return new GuardReportPosture(
                "attention",
                "Recent Guard decisions include blocks.",
                recent.Count,
                allowCount,
                reviewCount,
                blockCount,
                recent.FirstOrDefault()?.RunId);
        }

        if (reviewCount > 0)
        {
            return new GuardReportPosture(
                "review",
                "Recent Guard decisions include review-required patches.",
                recent.Count,
                allowCount,
                reviewCount,
                blockCount,
                recent.FirstOrDefault()?.RunId);
        }

        return new GuardReportPosture(
            "ready",
            recent.Count == 0
                ? "Guard policy is valid; no decisions have been recorded yet."
                : "Guard policy is valid; recent decisions are passing.",
            recent.Count,
            allowCount,
            reviewCount,
            blockCount,
            recent.FirstOrDefault()?.RunId);
    }

    private static GuardDecisionAuditPatchStats FromPatchStats(GuardPatchStats stats)
    {
        return new GuardDecisionAuditPatchStats(
            stats.ChangedFileCount,
            stats.AddedFileCount,
            stats.ModifiedFileCount,
            stats.DeletedFileCount,
            stats.RenamedFileCount,
            stats.BinaryFileCount,
            stats.TotalAdditions,
            stats.TotalDeletions);
    }

    private static GuardDecisionAuditIssue FromViolation(GuardViolation violation)
    {
        return new GuardDecisionAuditIssue(
            violation.RuleId,
            FormatSeverity(violation.Severity),
            violation.Message,
            violation.FilePath,
            violation.Evidence,
            violation.EvidenceRef);
    }

    private static GuardDecisionAuditIssue FromWarning(GuardWarning warning)
    {
        return new GuardDecisionAuditIssue(
            warning.RuleId,
            "review",
            warning.Message,
            warning.FilePath,
            warning.Evidence,
            warning.EvidenceRef);
    }

    private static string FormatOutcome(GuardDecisionOutcome outcome)
    {
        return outcome.ToString().ToLowerInvariant();
    }

    private static string FormatSeverity(GuardSeverity severity)
    {
        return severity.ToString().ToLowerInvariant();
    }

    private static int NormalizeLimit(int limit)
    {
        return Math.Clamp(limit, 1, 100);
    }
}
