using Carves.Runtime.Application.Guard;
using Carves.Runtime.Application.Processes;

namespace Carves.Runtime.Application.Tests;

public sealed class GuardDiffAdapterTests
{
    [Fact]
    public void PathMatches_HonorsExactPrefixNormalizationAndCasePolicy()
    {
        Assert.True(GuardDiffAdapter.PathMatches(@"src\feature.ts", "src/"));
        Assert.True(GuardDiffAdapter.PathMatches("./src//feature.ts", "src/"));
        Assert.True(GuardDiffAdapter.PathMatches("package.json", "package.json"));
        Assert.False(GuardDiffAdapter.PathMatches("src-old/feature.ts", "src/"));
        Assert.False(GuardDiffAdapter.PathMatches("SRC/feature.ts", "src/"));
        Assert.True(GuardDiffAdapter.PathMatches("SRC/feature.ts", "src/", caseSensitive: false));
    }

    [Fact]
    public void GlobMatches_DistinguishesSingleSegmentStarFromRecursiveDoubleStar()
    {
        Assert.True(GuardDiffAdapter.GlobMatches("src/foo/test.ts", "src/*/test.ts"));
        Assert.False(GuardDiffAdapter.GlobMatches("src/foo/bar/test.ts", "src/*/test.ts"));
        Assert.True(GuardDiffAdapter.GlobMatches("src/foo/bar/test.ts", "src/**/test.ts"));
        Assert.True(GuardDiffAdapter.GlobMatches("src/test.ts", "src/**/test.ts"));
        Assert.True(GuardDiffAdapter.GlobMatches("Service.csproj", "*.csproj"));
        Assert.True(GuardDiffAdapter.GlobMatches("src/App/Service.csproj", "*.csproj"));
        Assert.False(GuardDiffAdapter.GlobMatches("src/App/Service.csproj", "src/*.csproj"));
    }

    [Fact]
    public void IsUnsafeRepoPath_FlagsAbsoluteUriDriveAndRootEscapePaths()
    {
        Assert.True(GuardDiffAdapter.IsUnsafeRepoPath("../outside.md"));
        Assert.True(GuardDiffAdapter.IsUnsafeRepoPath("/etc/passwd"));
        Assert.True(GuardDiffAdapter.IsUnsafeRepoPath("C:/temp/file.txt"));
        Assert.True(GuardDiffAdapter.IsUnsafeRepoPath("https://example.invalid/file.txt"));
        Assert.False(GuardDiffAdapter.IsUnsafeRepoPath("src/../.ai/tasks/generated.json"));
        Assert.False(GuardDiffAdapter.IsUnsafeRepoPath("src/feature.ts"));
    }

    [Fact]
    public void BuildContext_ParsesPorcelainStatusKindsAndRenameOldPath()
    {
        using var workspace = new TemporaryWorkspace();
        var adapter = new GuardDiffAdapter(new ScriptedProcessRunner(
            statusOutput: """
             M src/modified.ts
            A  src/added.ts
            D  src/deleted.ts
            R  src/old.ts -> src/new.ts
            ?? src/untracked.ts
            """,
            numstatOutput: """
            1	0	src/modified.ts
            0	1	src/deleted.ts
            2	0	src/new.ts
            """));

        var context = adapter.BuildContext(Input(workspace, changedFiles: []), Policy());

        Assert.Equal(5, context.ChangedFiles.Count);
        Assert.Contains(context.ChangedFiles, file => file.Path == "src/modified.ts" && file.Status == GuardFileChangeStatus.Modified && file.Additions == 1);
        Assert.Contains(context.ChangedFiles, file => file.Path == "src/added.ts" && file.Status == GuardFileChangeStatus.Added);
        Assert.Contains(context.ChangedFiles, file => file.Path == "src/deleted.ts" && file.Status == GuardFileChangeStatus.Deleted && file.Deletions == 1);
        Assert.Contains(context.ChangedFiles, file => file.Path == "src/new.ts" && file.OldPath == "src/old.ts" && file.Status == GuardFileChangeStatus.Renamed);
        Assert.Contains(context.ChangedFiles, file => file.Path == "src/untracked.ts" && file.WasUntracked);
        Assert.Empty(context.Diagnostics ?? Array.Empty<GuardDiffDiagnostic>());
    }

    [Fact]
    public void BuildContext_GitStatusFailureProducesActionableBlockingDiagnostic()
    {
        using var workspace = new TemporaryWorkspace();
        var adapter = new GuardDiffAdapter(new ScriptedProcessRunner(statusExitCode: 128, statusError: "not a git repository"));

        var context = adapter.BuildContext(Input(workspace, changedFiles: []), Policy());
        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-git-status-failed");

        Assert.Empty(context.ChangedFiles);
        var diagnostic = Assert.Single(context.Diagnostics!);
        Assert.Equal("git.status_failed", diagnostic.RuleId);
        Assert.Contains("not a git repository", diagnostic.Evidence, StringComparison.Ordinal);
        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "git.status_failed");
    }

    [Fact]
    public void BuildContext_GitDiffFailureProducesActionableBlockingDiagnostic()
    {
        using var workspace = new TemporaryWorkspace();
        var adapter = new GuardDiffAdapter(new ScriptedProcessRunner(
            statusOutput: " M src/modified.ts",
            diffExitCode: 128,
            diffError: "bad revision 'missing'"));

        var context = adapter.BuildContext(Input(workspace, changedFiles: []), Policy());
        var decision = new GuardPolicyEvaluator().Evaluate(context, "guard-git-diff-failed");

        Assert.Contains(context.ChangedFiles, file => file.Path == "src/modified.ts");
        var diagnostic = Assert.Single(context.Diagnostics!);
        Assert.Equal("git.diff_failed", diagnostic.RuleId);
        Assert.Contains("bad revision", diagnostic.Evidence, StringComparison.Ordinal);
        Assert.Equal(GuardDecisionOutcome.Block, decision.Outcome);
        Assert.Contains(decision.Violations, violation => violation.RuleId == "git.diff_failed");
    }

    private static GuardDiffInput Input(TemporaryWorkspace workspace, IReadOnlyList<GuardChangedFileInput> changedFiles)
    {
        return new GuardDiffInput(
            workspace.RootPath,
            "HEAD",
            HeadRef: null,
            ".ai/guard-policy.json",
            DiffText: null,
            changedFiles,
            "guard-diff-test",
            "unit-test");
    }

    private static GuardPolicySnapshot Policy()
    {
        return new GuardPolicySnapshot(
            1,
            "guard-diff-test-policy",
            Description: null,
            new GuardPathPolicy(true, ["src/", "tests/", "*.csproj"], [".ai/tasks/", ".git/"], GuardPolicyAction.Review, GuardPolicyAction.Block),
            new GuardChangeBudget(10, 100, 100, 50, 50, 1),
            new GuardDependencyPolicy(["*.csproj", "package.json"], ["packages.lock.json", "package-lock.json"], GuardPolicyAction.Review, GuardPolicyAction.Review, GuardPolicyAction.Review),
            new GuardChangeShapePolicy(false, false, ["dist/"], GuardPolicyAction.Review, GuardPolicyAction.Review, false, ["src/"], ["tests/"], GuardPolicyAction.Review),
            new GuardDecisionPolicy(true, GuardPolicyAction.Allow, ReviewIsPassing: false, EmitEvidence: true));
    }

    private sealed class ScriptedProcessRunner : IProcessRunner
    {
        private readonly string statusOutput;
        private readonly int statusExitCode;
        private readonly string statusError;
        private readonly string numstatOutput;
        private readonly int diffExitCode;
        private readonly string diffError;

        public ScriptedProcessRunner(
            string statusOutput = "",
            int statusExitCode = 0,
            string statusError = "",
            string numstatOutput = "",
            int diffExitCode = 0,
            string diffError = "")
        {
            this.statusOutput = statusOutput;
            this.statusExitCode = statusExitCode;
            this.statusError = statusError;
            this.numstatOutput = numstatOutput;
            this.diffExitCode = diffExitCode;
            this.diffError = diffError;
        }

        public ProcessExecutionResult Run(IReadOnlyList<string> command, string workingDirectory)
        {
            if (command.Count >= 2 && command[0] == "git" && command[1] == "status")
            {
                return new ProcessExecutionResult(statusExitCode, statusOutput, statusError);
            }

            if (command.Count >= 2 && command[0] == "git" && command[1] == "diff")
            {
                return new ProcessExecutionResult(diffExitCode, numstatOutput, diffError);
            }

            return new ProcessExecutionResult(1, string.Empty, $"Unexpected command: {string.Join(' ', command)}");
        }
    }
}
