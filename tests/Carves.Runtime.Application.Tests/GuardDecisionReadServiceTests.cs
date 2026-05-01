using Carves.Runtime.Application.Guard;

namespace Carves.Runtime.Application.Tests;

public sealed class GuardDecisionReadServiceTests
{
    [Fact]
    public void Audit_LoadsRecentDecisionRecords()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var service = new GuardDecisionReadService();
        service.RecordCheck(workspace.RootPath, CheckResult("guard-a-older", GuardDecisionOutcome.Allow));
        service.RecordCheck(workspace.RootPath, CheckResult("guard-z-newer", GuardDecisionOutcome.Block, violations:
        [
            new GuardViolation(
                "path.protected_prefix",
                GuardSeverity.Block,
                "Protected path changed.",
                ".ai/tasks/generated.json",
                "path=.ai/tasks/generated.json",
                "guard-rule:guard-z-newer:path.protected_prefix:0"),
        ]));

        var audit = service.Audit(workspace.RootPath, limit: 1);

        Assert.Equal(".ai/runtime/guard/decisions.jsonl", audit.AuditPath);
        var record = Assert.Single(audit.Decisions);
        Assert.Equal("guard-z-newer", record.RunId);
        Assert.Equal("block", record.Outcome);
        Assert.Contains(record.Violations, violation => violation.RuleId == "path.protected_prefix");
    }

    [Fact]
    public void Report_SummarizesPolicyAndRecentPosture()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var service = new GuardDecisionReadService();
        service.RecordCheck(workspace.RootPath, CheckResult("guard-allow", GuardDecisionOutcome.Allow));

        var report = service.Report(workspace.RootPath, ".ai/guard-policy.json");

        Assert.True(report.PolicyLoad.IsValid);
        Assert.Equal("alpha-read-test", report.PolicyLoad.Policy!.PolicyId);
        Assert.Equal("ready", report.Posture.Status);
        Assert.Equal(1, report.Posture.AllowCount);
        Assert.Equal("guard-allow", report.Posture.LatestRunId);
    }

    [Fact]
    public void Explain_ReturnsExactEvidenceForRunId()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var service = new GuardDecisionReadService();
        service.RecordCheck(workspace.RootPath, CheckResult("guard-explain", GuardDecisionOutcome.Block, violations:
        [
            new GuardViolation(
                "budget.max_changed_files",
                GuardSeverity.Block,
                "Too many files changed.",
                null,
                "changed_files=8; max=5",
                "guard-rule:guard-explain:budget.max_changed_files:0"),
        ]));

        var explain = service.Explain(workspace.RootPath, "guard-explain");

        Assert.True(explain.Found);
        Assert.NotNull(explain.Record);
        Assert.Equal("guard-explain", explain.Record.RunId);
        Assert.Contains(explain.Record.Violations, violation =>
            violation.RuleId == "budget.max_changed_files"
            && violation.Evidence == "changed_files=8; max=5");
    }

    [Fact]
    public void Explain_DiagnosticsReflectFullLookupInsteadOfSingleRecordLimit()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var service = new GuardDecisionReadService();
        service.RecordCheck(workspace.RootPath, CheckResult("guard-first", GuardDecisionOutcome.Allow));
        service.RecordCheck(workspace.RootPath, CheckResult("guard-second", GuardDecisionOutcome.Block));

        var explain = service.Explain(workspace.RootPath, "guard-second");

        Assert.True(explain.Found);
        Assert.Equal("guard-second", explain.Record!.RunId);
        Assert.Equal(GuardDecisionAuditStore.MaxStoredLineCount, explain.Diagnostics.RequestedLimit);
        Assert.Equal(GuardDecisionAuditStore.MaxStoredLineCount, explain.Diagnostics.EffectiveLimit);
        Assert.Equal(2, explain.Diagnostics.TotalLineCount);
        Assert.Equal(2, explain.Diagnostics.LoadedRecordCount);
        Assert.Equal(1, explain.Diagnostics.ReturnedRecordCount);
    }

    [Fact]
    public void Readbacks_TolerateMalformedTruncatedEmptyAndFutureVersionRecords()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var service = new GuardDecisionReadService();
        service.RecordCheck(workspace.RootPath, CheckResult("guard-valid", GuardDecisionOutcome.Allow));
        AppendAuditLines(workspace,
        [
            "",
            "{ malformed",
            "{\"schema_version\":1,",
            "{\"schema_version\":999,\"run_id\":\"guard-future\"}",
        ]);

        var audit = service.Audit(workspace.RootPath, limit: 10);
        var report = service.Report(workspace.RootPath, ".ai/guard-policy.json", limit: 10);
        var explain = service.Explain(workspace.RootPath, "guard-valid");

        var record = Assert.Single(audit.Decisions);
        Assert.Equal("guard-valid", record.RunId);
        AssertDiagnostics(audit.Diagnostics);
        AssertDiagnostics(report.Diagnostics);
        AssertDiagnostics(explain.Diagnostics);
        Assert.True(explain.Found);
        Assert.Equal("guard-valid", explain.Record!.RunId);
    }

    [Fact]
    public void RecordCheck_UsesFileExclusiveLockForConcurrentAppends()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var service = new GuardDecisionReadService();

        Parallel.For(0, 50, index =>
        {
            Assert.True(service.RecordCheck(workspace.RootPath, CheckResult($"guard-concurrent-{index:D2}", GuardDecisionOutcome.Allow)));
        });

        var audit = service.Audit(workspace.RootPath, limit: 100);

        Assert.Equal("file_exclusive_append_lock", GuardDecisionAuditStore.WriteConcurrencyPolicy);
        Assert.Equal(50, audit.Diagnostics.TotalLineCount);
        Assert.Equal(50, audit.Diagnostics.LoadedRecordCount);
        Assert.Equal(50, audit.Diagnostics.ReturnedRecordCount);
        Assert.Equal(50, audit.Decisions.Select(record => record.RunId).Distinct(StringComparer.Ordinal).Count());
        Assert.False(audit.Diagnostics.IsDegraded);
    }

    [Fact]
    public void RecordCheck_FailsClosedWhenDecisionStoreIsExclusivelyLocked()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var service = new GuardDecisionReadService();
        var auditPath = new GuardDecisionAuditStore().ResolveAuditPath(workspace.RootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(auditPath)!);
        using var locked = new FileStream(auditPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var recorded = service.RecordCheck(workspace.RootPath, CheckResult("guard-locked", GuardDecisionOutcome.Allow));

        Assert.False(recorded);
        Assert.Equal(0, locked.Length);
    }

    [Fact]
    public void Readbacks_ReturnEmptySnapshotsWhenNoDecisionRecordsExist()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var service = new GuardDecisionReadService();

        var audit = service.Audit(workspace.RootPath);
        var report = service.Report(workspace.RootPath, ".ai/guard-policy.json");
        var explain = service.Explain(workspace.RootPath, "missing-run");

        Assert.Empty(audit.Decisions);
        Assert.Equal(0, audit.Diagnostics.TotalLineCount);
        Assert.Equal("ready", report.Posture.Status);
        Assert.Equal(0, report.Diagnostics.TotalLineCount);
        Assert.False(explain.Found);
        Assert.Equal(0, explain.Diagnostics.TotalLineCount);
    }

    [Fact]
    public void RecordCheck_TrimsDecisionSidecarToMaxStoredLineCount()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var service = new GuardDecisionReadService();
        for (var index = 0; index < GuardDecisionAuditStore.MaxStoredLineCount + 2; index++)
        {
            service.RecordCheck(workspace.RootPath, CheckResult($"guard-{index:D4}", GuardDecisionOutcome.Allow));
        }

        var auditPath = new GuardDecisionAuditStore().ResolveAuditPath(workspace.RootPath);
        var lineCount = File.ReadLines(auditPath).Count();
        var audit = service.Audit(workspace.RootPath, limit: 200);

        Assert.Equal(GuardDecisionAuditStore.MaxStoredLineCount, lineCount);
        Assert.Equal(GuardDecisionAuditStore.MaxStoredLineCount, audit.Diagnostics.TotalLineCount);
        Assert.Equal(GuardDecisionAuditStore.MaxStoredLineCount, audit.Diagnostics.MaxStoredLineCount);
        Assert.Equal(100, audit.Decisions.Count);
        Assert.Equal(100, audit.Diagnostics.EffectiveLimit);
    }

    private static void AssertDiagnostics(GuardDecisionReadDiagnostics diagnostics)
    {
        Assert.True(diagnostics.IsDegraded);
        Assert.Equal(5, diagnostics.TotalLineCount);
        Assert.Equal(1, diagnostics.EmptyLineCount);
        Assert.Equal(1, diagnostics.LoadedRecordCount);
        Assert.Equal(1, diagnostics.ReturnedRecordCount);
        Assert.Equal(3, diagnostics.SkippedRecordCount);
        Assert.Equal(2, diagnostics.MalformedRecordCount);
        Assert.Equal(1, diagnostics.FutureVersionRecordCount);
    }

    private static void AppendAuditLines(TemporaryWorkspace workspace, IReadOnlyList<string> lines)
    {
        var store = new GuardDecisionAuditStore();
        var path = store.ResolveAuditPath(workspace.RootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllLines(path, lines);
    }

    private static GuardCheckResult CheckResult(
        string runId,
        GuardDecisionOutcome outcome,
        IReadOnlyList<GuardViolation>? violations = null,
        IReadOnlyList<GuardWarning>? warnings = null)
    {
        var decision = new GuardDecision(
            runId,
            outcome,
            "alpha-read-test",
            outcome == GuardDecisionOutcome.Block ? "Patch blocked." : "Patch allowed.",
            violations ?? Array.Empty<GuardViolation>(),
            warnings ?? Array.Empty<GuardWarning>(),
            [$"guard-run:{runId}", "guard-policy:alpha-read-test"],
            RequiresRuntimeTaskTruth: false);
        var context = new GuardDiffContext(
            RepositoryRoot: "repo",
            BaseRef: "HEAD",
            HeadRef: null,
            Policy: PolicySnapshot(),
            ChangedFiles:
            [
                new GuardChangedFile(
                    "src/todo.ts",
                    OldPath: null,
                    GuardFileChangeStatus.Modified,
                    Additions: 1,
                    Deletions: 0,
                    IsBinary: false,
                    WasUntracked: false,
                    MatchesAllowedPath: true,
                    MatchesProtectedPath: false,
                    MatchedAllowedPrefix: "src/",
                    MatchedProtectedPrefix: null),
            ],
            PatchStats: new GuardPatchStats(1, 0, 1, 0, 0, 0, 1, 0),
            ScopeSource: "guard-policy",
            RuntimeTaskId: null);
        return new GuardCheckResult(decision, context);
    }

    private static GuardPolicySnapshot PolicySnapshot()
    {
        return new GuardPolicySnapshot(
            1,
            "alpha-read-test",
            null,
            new GuardPathPolicy(true, ["src/", "tests/"], [".ai/tasks/"], GuardPolicyAction.Review, GuardPolicyAction.Block),
            new GuardChangeBudget(5, 100, 100, 50, 50, 1),
            new GuardDependencyPolicy(["package.json"], ["package-lock.json"], GuardPolicyAction.Review, GuardPolicyAction.Review, GuardPolicyAction.Review),
            new GuardChangeShapePolicy(false, false, ["dist/"], GuardPolicyAction.Review, GuardPolicyAction.Review, false, ["src/"], ["tests/"], GuardPolicyAction.Review),
            new GuardDecisionPolicy(true, GuardPolicyAction.Allow, ReviewIsPassing: false, EmitEvidence: true));
    }

    private static void WritePolicy(TemporaryWorkspace workspace)
    {
        workspace.WriteFile(".ai/guard-policy.json", """
        {
          "schema_version": 1,
          "policy_id": "alpha-read-test",
          "path_policy": {
            "path_case": "case_sensitive",
            "allowed_path_prefixes": [ "src/", "tests/" ],
            "protected_path_prefixes": [ ".ai/tasks/" ],
            "outside_allowed_action": "review",
            "protected_path_action": "block"
          },
          "change_budget": {
            "max_changed_files": 5,
            "max_total_additions": 100,
            "max_total_deletions": 100,
            "max_file_additions": 50,
            "max_file_deletions": 50,
            "max_renames": 1
          },
          "dependency_policy": {
            "manifest_paths": [ "package.json" ],
            "lockfile_paths": [ "package-lock.json" ],
            "manifest_without_lockfile_action": "review",
            "lockfile_without_manifest_action": "review",
            "new_dependency_action": "review"
          },
          "change_shape": {
            "allow_rename_with_content_change": false,
            "allow_delete_without_replacement": false,
            "generated_path_prefixes": [ "dist/" ],
            "generated_path_action": "review",
            "mixed_feature_and_refactor_action": "review",
            "require_tests_for_source_changes": false,
            "source_path_prefixes": [ "src/" ],
            "test_path_prefixes": [ "tests/" ],
            "missing_tests_action": "review"
          },
          "decision": {
            "fail_closed": true,
            "default_outcome": "allow",
            "review_is_passing": false,
            "emit_evidence": true
          }
        }
        """);
    }
}
