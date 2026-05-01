using Carves.Runtime.Application.Guard;
using Carves.Runtime.Application.Processes;

namespace Carves.Runtime.Application.Tests;

public sealed class GuardPolicyEvaluatorTests
{
    [Fact]
    public void Load_ParsesGuardPolicyV1WithoutRuntimeTaskTruth()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);

        var result = new GuardPolicyService().Load(workspace.RootPath, ".ai/guard-policy.json");

        Assert.True(result.IsValid);
        Assert.NotNull(result.Policy);
        Assert.Equal(1, result.Policy.SchemaVersion);
        Assert.Equal("alpha-test", result.Policy.PolicyId);
        Assert.True(result.Policy.ChangeShape.RequireTestsForSourceChanges);
    }

    [Fact]
    public void Load_AllowsMissingOptionalFieldsWithConservativeDefaults()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/guard-policy.json", """
        {
          "schema_version": 1,
          "policy_id": "minimal-beta-policy",
          "path_policy": {
            "allowed_path_prefixes": [ "src/" ],
            "protected_path_prefixes": [],
            "outside_allowed_action": "review",
            "protected_path_action": "block"
          },
          "change_budget": {
            "max_changed_files": 3
          },
          "dependency_policy": {
            "manifest_paths": [],
            "lockfile_paths": [],
            "manifest_without_lockfile_action": "review",
            "lockfile_without_manifest_action": "review",
            "new_dependency_action": "review"
          },
          "change_shape": {
            "generated_path_prefixes": [],
            "generated_path_action": "review",
            "mixed_feature_and_refactor_action": "review"
          },
          "decision": {
            "fail_closed": true,
            "default_outcome": "allow",
            "review_is_passing": false,
            "emit_evidence": true
          }
        }
        """);

        var result = new GuardPolicyService().Load(workspace.RootPath, ".ai/guard-policy.json");

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.True(result.Policy!.PathPolicy.CaseSensitive);
        Assert.Contains(".git/", result.Policy.PathPolicy.ProtectedPathPrefixes);
        Assert.Null(result.Policy.ChangeBudget.MaxTotalAdditions);
        Assert.False(result.Policy.ChangeShape.AllowRenameWithContentChange);
        Assert.False(result.Policy.ChangeShape.AllowDeleteWithoutReplacement);
        Assert.False(result.Policy.ChangeShape.RequireTestsForSourceChanges);
        Assert.Empty(result.Policy.ChangeShape.SourcePathPrefixes);
        Assert.Empty(result.Policy.ChangeShape.TestPathPrefixes);
        Assert.Equal(GuardPolicyAction.Review, result.Policy.ChangeShape.MissingTestsAction);
    }

    [Fact]
    public void Load_RejectsUnknownFieldsFailClosed()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var path = Path.Combine(workspace.RootPath, ".ai", "guard-policy.json");
        var policy = File.ReadAllText(path).Replace(
            "\"decision\": {",
            "\"subjective_style_rules\": [],\n  \"decision\": {",
            StringComparison.Ordinal);
        File.WriteAllText(path, policy);

        var result = new GuardPolicyService().Load(workspace.RootPath, ".ai/guard-policy.json");

        Assert.False(result.IsValid);
        Assert.Equal("policy.unknown_field", result.ErrorCode);
        Assert.Contains("subjective_style_rules", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsUnknownNestedFieldsWithConcretePath()
    {
        using var workspace = new TemporaryWorkspace();
        WritePolicy(workspace);
        var path = Path.Combine(workspace.RootPath, ".ai", "guard-policy.json");
        var policy = File.ReadAllText(path).Replace(
            "\"max_changed_files\": 5,",
            "\"max_changed_files\": 5,\n    \"surprise_budget\": 1,",
            StringComparison.Ordinal);
        File.WriteAllText(path, policy);

        var result = new GuardPolicyService().Load(workspace.RootPath, ".ai/guard-policy.json");

        Assert.False(result.IsValid);
        Assert.Equal("policy.unknown_field", result.ErrorCode);
        Assert.Contains("change_budget.surprise_budget", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FailsClosedForFutureSchemaWithClearViolationCode()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/guard-policy.json", """
        {
          "schema_version": 2,
          "policy_id": "future"
        }
        """);

        var result = new GuardPolicyService().Load(workspace.RootPath, ".ai/guard-policy.json");
        var decision = new GuardCheckService(
            new GuardPolicyService(),
            new GuardDiffAdapter(new NoopProcessRunner()),
            new GuardPolicyEvaluator()).Check(workspace.RootPath);

        Assert.False(result.IsValid);
        Assert.Equal("policy.unsupported_schema_version", result.ErrorCode);
        Assert.Equal(GuardDecisionOutcome.Block, decision.Decision.Outcome);
        Assert.Contains(decision.Decision.Violations, violation => violation.RuleId == "policy.unsupported_schema_version");
    }

    [Fact]
    public void Alpha2PolicyAndDecisionFixtures_RemainReadableUnderBetaPath()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRoot = ResolveRepoRoot();
        var fixtureRoot = Path.Combine(repoRoot, "tests", "Carves.Runtime.Application.Tests", "Fixtures", "alpha-guard", "0.1.0-alpha.2");
        workspace.WriteFile(".ai/guard-policy.json", File.ReadAllText(Path.Combine(fixtureRoot, "guard-policy.json")));
        workspace.WriteFile(GuardDecisionAuditStore.RelativeAuditPath, File.ReadAllText(Path.Combine(fixtureRoot, "decisions.jsonl")));

        var policy = new GuardPolicyService().Load(workspace.RootPath, ".ai/guard-policy.json");
        var audit = new GuardDecisionReadService().Audit(workspace.RootPath);

        Assert.True(policy.IsValid, policy.ErrorMessage);
        Assert.Equal("alpha2-fixture", policy.Policy!.PolicyId);
        var record = Assert.Single(audit.Decisions);
        Assert.Equal("alpha2-fixture-allow", record.RunId);
        Assert.Equal("allow", record.Outcome);
        Assert.False(record.RequiresRuntimeTaskTruth);
        Assert.False(audit.Diagnostics.IsDegraded);
    }

    [Fact]
    public void Evaluate_AllowsSourceAndTestPatch()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/todo.ts", null, GuardFileChangeStatus.Modified, 2, 0, false, false),
            new GuardChangedFileInput("tests/todo.test.ts", null, GuardFileChangeStatus.Added, 4, 0, false, true),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-allow");

        Assert.Equal(GuardDecisionOutcome.Allow, decision.Outcome);
        Assert.False(decision.RequiresRuntimeTaskTruth);
        Assert.Empty(decision.Violations);
        Assert.Empty(decision.Warnings);
    }

    [Fact]
    public void Evaluate_BlocksProtectedPath()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput(".ai/tasks/generated.json", null, GuardFileChangeStatus.Added, 1, 0, false, true),
            new GuardChangedFileInput("src/todo.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
            new GuardChangedFileInput("tests/todo.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-protected");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "path.protected_prefix");
        Assert.False(decision.RequiresRuntimeTaskTruth);
    }

    [Fact]
    public void Evaluate_BlocksChangedFileBudget()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace, maxChangedFiles: 1);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/a.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
            new GuardChangedFileInput("tests/a.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-budget");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "budget.max_changed_files");
    }

    [Fact]
    public void Evaluate_BlocksLineBudgets()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/large.ts", null, GuardFileChangeStatus.Modified, 60, 40, false, false),
            new GuardChangedFileInput("tests/large.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-lines");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "budget.max_total_additions");
        Assert.Contains(decision.Violations, violation => violation.RuleId == "budget.max_file_additions");
        Assert.Contains(decision.Violations, violation => violation.RuleId == "budget.max_file_deletions");
    }

    [Fact]
    public void BuildContext_FiltersGeneratedGuardDecisionReadModel()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput(GuardDecisionAuditStore.RelativeAuditPath, null, GuardFileChangeStatus.Modified, 1, 0, false, false),
            new GuardChangedFileInput("src/todo.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
            new GuardChangedFileInput("tests/todo.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        Assert.DoesNotContain(context.ChangedFiles, file => file.Path == GuardDecisionAuditStore.RelativeAuditPath);
        Assert.Equal(["src/todo.ts", "tests/todo.test.ts"], context.ChangedFiles.Select(file => file.Path));
    }

    [Fact]
    public void BuildContext_NormalizesSeparatorsAndDotSegmentsBeforePathMatching()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput(@".\src\feature.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
            new GuardChangedFileInput("./tests//feature.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-normalized-paths");

        Assert.Equal(GuardDecisionOutcome.Allow, decision.Outcome);
        Assert.Equal(["src/feature.ts", "tests/feature.test.ts"], context.ChangedFiles.Select(file => file.Path));
        Assert.All(context.ChangedFiles, file => Assert.True(file.MatchesAllowedPath));
    }

    [Fact]
    public void Evaluate_BlocksNormalizedProtectedTraversalBeforeAllowedPrefix()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/../.ai/tasks/generated.json", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
            new GuardChangedFileInput("tests/traversal.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-protected-precedence");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(context.ChangedFiles, file =>
            file.Path == ".ai/tasks/generated.json"
            && file.MatchesProtectedPath
            && !file.MatchesAllowedPath);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "path.protected_prefix");
    }

    [Fact]
    public void Evaluate_BlocksUnsafeRepoEscapePath()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("../outside.md", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-unsafe-path");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        var violation = Assert.Single(decision.Violations, violation => violation.RuleId == "path.protected_prefix");
        Assert.Equal("unsafe_repo_path", violation.Evidence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last());
    }

    [Fact]
    public void Evaluate_HonorsCaseInsensitivePathPolicy()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace, pathCase: "case_insensitive");
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("SRC/feature.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
            new GuardChangedFileInput("TESTS/feature.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-case-insensitive");

        Assert.Equal(GuardDecisionOutcome.Allow, decision.Outcome);
        Assert.All(context.ChangedFiles, file => Assert.True(file.MatchesAllowedPath));
    }

    [Fact]
    public void Evaluate_BlocksProtectedOldPathOnRename()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/generated.json", ".ai/tasks/generated.json", GuardFileChangeStatus.Renamed, 0, 0, false, false),
            new GuardChangedFileInput("tests/rename.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-rename-old-path");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(context.ChangedFiles, file =>
            file.Path == "src/generated.json"
            && file.OldPath == ".ai/tasks/generated.json"
            && file.MatchesProtectedPath);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "path.protected_prefix");
    }

    [Fact]
    public void Evaluate_BlocksProtectedDeletePath()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput(".ai/tasks/old.json", null, GuardFileChangeStatus.Deleted, 0, 1, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-delete-protected");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "path.protected_prefix");
    }

    [Fact]
    public void Evaluate_BlocksEmptyDiff()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, []);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-empty");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "diff.empty");
    }

    [Fact]
    public void Evaluate_ReviewsPathOutsideAllowedPrefixes()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("docs/notes.md", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-outside-allowed");

        Assert.Equal(GuardDecisionOutcome.Review, decision.Outcome);
        Assert.Contains(decision.Warnings, warning => warning.RuleId == "path.outside_allowed_prefix");
    }

    [Fact]
    public void Evaluate_BlocksTotalDeletionBudget()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace, maxTotalDeletions: 5, maxFileDeletions: 50);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/trim.ts", null, GuardFileChangeStatus.Modified, 0, 6, false, false),
            new GuardChangedFileInput("tests/trim.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-total-deletions");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "budget.max_total_deletions");
    }

    [Fact]
    public void Evaluate_BlocksRenameBudget()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace, maxRenames: 1);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/new-a.ts", "src/old-a.ts", GuardFileChangeStatus.Renamed, 0, 0, false, false),
            new GuardChangedFileInput("src/new-b.ts", "src/old-b.ts", GuardFileChangeStatus.Renamed, 0, 0, false, false),
            new GuardChangedFileInput("tests/rename.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-rename-budget");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "budget.max_renames");
    }

    [Fact]
    public void Evaluate_ReviewsManifestWithoutLockfile()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("package.json", null, GuardFileChangeStatus.Modified, 3, 1, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-dependency");

        Assert.Equal(GuardDecisionOutcome.Review, decision.Outcome);
        Assert.Contains(decision.Warnings, warning => warning.RuleId == "dependency.manifest_without_lockfile");
        Assert.Contains(decision.Warnings, warning => warning.RuleId == "dependency.new_dependency.unverified");
    }

    [Fact]
    public void Evaluate_BlocksManifestWithoutLockfileWhenConfigured()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(
            workspace,
            manifestWithoutLockfileAction: "block",
            newDependencyAction: "block");
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("package.json", null, GuardFileChangeStatus.Modified, 3, 1, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-dependency-block");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "dependency.manifest_without_lockfile");
        Assert.Contains(decision.Violations, violation => violation.RuleId == "dependency.new_dependency.unverified");
    }

    [Fact]
    public void Evaluate_ReviewsLockfileWithoutManifest()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("package-lock.json", null, GuardFileChangeStatus.Modified, 0, 1, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-lockfile-only");

        Assert.Equal(GuardDecisionOutcome.Review, decision.Outcome);
        Assert.Contains(decision.Warnings, warning => warning.RuleId == "dependency.lockfile_without_manifest");
    }

    [Fact]
    public void Evaluate_BlocksRenameWithContentChange()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/new-name.ts", "src/old-name.ts", GuardFileChangeStatus.Renamed, 2, 1, false, false),
            new GuardChangedFileInput("tests/rename.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-rename-content");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "shape.rename_with_content_change");
    }

    [Fact]
    public void Evaluate_BlocksDeleteWithoutReplacement()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/removed.ts", null, GuardFileChangeStatus.Deleted, 0, 4, false, false),
            new GuardChangedFileInput("tests/removed.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-delete");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "shape.delete_without_replacement");
    }

    [Fact]
    public void Evaluate_BlocksDeleteWhenOnlyUnrelatedFileIsAdded()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/removed.ts", null, GuardFileChangeStatus.Deleted, 0, 4, false, false),
            new GuardChangedFileInput("src/unrelated.ts", null, GuardFileChangeStatus.Added, 6, 0, false, true),
            new GuardChangedFileInput("tests/removed.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-delete-unrelated");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        var violation = Assert.Single(decision.Violations, violation => violation.RuleId == "shape.delete_without_replacement");
        Assert.Equal("src/removed.ts", violation.FilePath);
        Assert.DoesNotContain("added replacement", violation.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("related-stem replacement candidate", violation.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_AllowsDeleteWithSameDirectorySameExtensionRelatedStemReplacement()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/removed.ts", null, GuardFileChangeStatus.Deleted, 0, 4, false, false),
            new GuardChangedFileInput("src/removed-v2.ts", null, GuardFileChangeStatus.Added, 6, 0, false, true),
            new GuardChangedFileInput("tests/removed.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-delete-related");

        Assert.NotEqual(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.DoesNotContain(decision.Violations, violation => violation.RuleId == "shape.delete_without_replacement");
    }

    [Fact]
    public void Evaluate_AllowsDeleteWithExplicitRenameReplacement()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/removed.ts", null, GuardFileChangeStatus.Deleted, 0, 4, false, false),
            new GuardChangedFileInput("src/replacement.ts", "src/removed.ts", GuardFileChangeStatus.Renamed, 0, 0, false, false),
            new GuardChangedFileInput("tests/removed.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-delete-rename-replacement");

        Assert.NotEqual(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.DoesNotContain(decision.Violations, violation => violation.RuleId == "shape.delete_without_replacement");
    }

    [Fact]
    public void Evaluate_ReviewsGeneratedPath()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace, allowedPathPrefixes: [ "src/", "tests/", "dist/" ]);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("dist/app.js", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-generated");

        Assert.Equal(GuardDecisionOutcome.Review, decision.Outcome);
        Assert.Contains(decision.Warnings, warning => warning.RuleId == "shape.generated_path");
    }

    [Fact]
    public void Evaluate_ReviewsSourceChangeWithoutTests()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/todo.ts", null, GuardFileChangeStatus.Modified, 4, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-test-required");

        Assert.Equal(GuardDecisionOutcome.Review, decision.Outcome);
        Assert.Contains(decision.Warnings, warning => warning.RuleId == "shape.missing_tests_for_source_changes");
    }

    [Fact]
    public void Evaluate_ReviewsMixedFeatureAndRefactor()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput("src/new-name.ts", "src/old-name.ts", GuardFileChangeStatus.Renamed, 0, 0, false, false),
            new GuardChangedFileInput("src/feature.ts", null, GuardFileChangeStatus.Modified, 2, 0, false, false),
            new GuardChangedFileInput("tests/feature.test.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-mixed");

        Assert.Equal(GuardDecisionOutcome.Review, decision.Outcome);
        Assert.Contains(decision.Warnings, warning => warning.RuleId == "shape.mixed_feature_and_refactor");
    }

    [Fact]
    public void Evaluate_ReportsCombinedPathBudgetAndShapeFindings()
    {
        using var workspace = new TemporaryWorkspace();
        var policy = LoadPolicy(workspace, maxChangedFiles: 2, maxTotalAdditions: 5);
        var context = BuildContext(workspace, policy, [
            new GuardChangedFileInput(".ai/tasks/generated.json", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
            new GuardChangedFileInput("src/feature.ts", null, GuardFileChangeStatus.Modified, 6, 0, false, false),
            new GuardChangedFileInput("src/other.ts", null, GuardFileChangeStatus.Modified, 1, 0, false, false),
        ]);

        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-test-combined");

        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "path.protected_prefix");
        Assert.Contains(decision.Violations, violation => violation.RuleId == "budget.max_changed_files");
        Assert.Contains(decision.Violations, violation => violation.RuleId == "budget.max_total_additions");
        Assert.Contains(decision.Warnings, warning => warning.RuleId == "shape.missing_tests_for_source_changes");
        Assert.True(decision.EvidenceRefs.Count(reference => reference.Contains("guard-test-combined", StringComparison.Ordinal)) >= 4);
    }

    private static GuardPolicySnapshot LoadPolicy(
        TemporaryWorkspace workspace,
        int maxChangedFiles = 5,
        int maxTotalAdditions = 50,
        int maxTotalDeletions = 50,
        int maxFileAdditions = 30,
        int maxFileDeletions = 30,
        int maxRenames = 1,
        string manifestWithoutLockfileAction = "review",
        string lockfileWithoutManifestAction = "review",
        string newDependencyAction = "review",
        IReadOnlyList<string>? allowedPathPrefixes = null,
        string pathCase = "case_sensitive")
    {
        WritePolicy(
            workspace,
            maxChangedFiles,
            maxTotalAdditions,
            maxTotalDeletions,
            maxFileAdditions,
            maxFileDeletions,
            maxRenames,
            manifestWithoutLockfileAction,
            lockfileWithoutManifestAction,
            newDependencyAction,
            allowedPathPrefixes,
            pathCase);
        var result = new GuardPolicyService().Load(workspace.RootPath, ".ai/guard-policy.json");
        Assert.True(result.IsValid, result.ErrorMessage);
        return result.Policy!;
    }

    private static GuardDiffContext BuildContext(
        TemporaryWorkspace workspace,
        GuardPolicySnapshot policy,
        IReadOnlyList<GuardChangedFileInput> files)
    {
        var adapter = new GuardDiffAdapter(new NoopProcessRunner());
        return adapter.BuildContext(new GuardDiffInput(
            workspace.RootPath,
            "HEAD",
            null,
            ".ai/guard-policy.json",
            null,
            files,
            "guard-test",
            "unit-test"), policy);
    }

    private static void WritePolicy(
        TemporaryWorkspace workspace,
        int maxChangedFiles = 5,
        int maxTotalAdditions = 50,
        int maxTotalDeletions = 50,
        int maxFileAdditions = 30,
        int maxFileDeletions = 30,
        int maxRenames = 1,
        string manifestWithoutLockfileAction = "review",
        string lockfileWithoutManifestAction = "review",
        string newDependencyAction = "review",
        IReadOnlyList<string>? allowedPathPrefixes = null,
        string pathCase = "case_sensitive")
    {
        var allowedPathsJson = string.Join(", ", (allowedPathPrefixes ?? [ "src/", "tests/", "package.json", "package-lock.json" ])
            .Select(path => $"\"{path}\""));
        workspace.WriteFile(".ai/guard-policy.json", $$"""
        {
          "schema_version": 1,
          "policy_id": "alpha-test",
          "path_policy": {
            "path_case": "{{pathCase}}",
            "allowed_path_prefixes": [ {{allowedPathsJson}} ],
            "protected_path_prefixes": [ ".ai/tasks/", ".ai/memory/", ".git/" ],
            "outside_allowed_action": "review",
            "protected_path_action": "block"
          },
          "change_budget": {
            "max_changed_files": {{maxChangedFiles}},
            "max_total_additions": {{maxTotalAdditions}},
            "max_total_deletions": {{maxTotalDeletions}},
            "max_file_additions": {{maxFileAdditions}},
            "max_file_deletions": {{maxFileDeletions}},
            "max_renames": {{maxRenames}}
          },
          "dependency_policy": {
            "manifest_paths": [ "package.json", "*.csproj" ],
            "lockfile_paths": [ "package-lock.json", "packages.lock.json" ],
            "manifest_without_lockfile_action": "{{manifestWithoutLockfileAction}}",
            "lockfile_without_manifest_action": "{{lockfileWithoutManifestAction}}",
            "new_dependency_action": "{{newDependencyAction}}"
          },
          "change_shape": {
            "allow_rename_with_content_change": false,
            "allow_delete_without_replacement": false,
            "generated_path_prefixes": [ "dist/", "build/", "coverage/" ],
            "generated_path_action": "review",
            "mixed_feature_and_refactor_action": "review",
            "require_tests_for_source_changes": true,
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

    private sealed class NoopProcessRunner : IProcessRunner
    {
        public ProcessExecutionResult Run(IReadOnlyList<string> command, string workingDirectory)
        {
            return new ProcessExecutionResult(0, string.Empty, string.Empty);
        }
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CARVES.Runtime.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root.");
    }
}
