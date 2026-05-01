using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialPortableStateGuardTests
{
    [Fact]
    public void TrialCollect_PortablePackageWritesScoredStateAfterFreshScoring()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-state-score-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            Directory.SetCurrentDirectory(packageRoot);
            var result = RunMatrixCli("trial", "collect", "--json");

            Assert.Equal(0, result.ExitCode);
            using var stateDocument = ReadState(packageRoot);
            var state = stateDocument.RootElement;
            Assert.Equal("carves-portable-agent-trial-state.v0", state.GetProperty("schema_version").GetString());
            Assert.Equal("scored", state.GetProperty("state").GetString());
            Assert.Equal("verified", state.GetProperty("last_status").GetString());
            Assert.Equal("results/local", state.GetProperty("local_results_root").GetString());
            Assert.Equal("results/submit-bundle", state.GetProperty("submit_bundle_root").GetString());
            Assert.Empty(state.GetProperty("reason_codes").EnumerateArray());
            Assert.True(File.Exists(Path.Combine(packageRoot, "results", "local", "matrix-agent-trial-result-card.md")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialCollect_PortablePackageRefusesAlreadyScoredPackage()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-state-rescore-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            Directory.SetCurrentDirectory(packageRoot);
            var first = RunMatrixCli("trial", "collect", "--json");
            var second = RunMatrixCli("trial", "collect", "--json");

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(1, second.ExitCode);
            using var resultDocument = JsonDocument.Parse(second.StandardOutput);
            Assert.Contains(
                resultDocument.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "trial_portable_package_already_scored"
                    && diagnostic.GetProperty("next_step").GetString()!.Contains("RESULT.cmd", StringComparison.Ordinal)
                    && diagnostic.GetProperty("next_step").GetString()!.Contains("RESET.cmd", StringComparison.Ordinal));
            using var stateDocument = ReadState(packageRoot);
            Assert.Equal("scored", stateDocument.RootElement.GetProperty("state").GetString());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialCollect_PortablePackageMarksStaleResultsAsContaminated()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-state-stale-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);
            File.WriteAllText(Path.Combine(packageRoot, "results", "local", "stale.txt"), "old result");

            Directory.SetCurrentDirectory(packageRoot);
            var result = RunMatrixCli("trial", "collect", "--json");

            Assert.Equal(1, result.ExitCode);
            AssertDiagnostic(result, "trial_portable_stale_results");
            using var stateDocument = ReadState(packageRoot);
            var state = stateDocument.RootElement;
            Assert.Equal("contaminated", state.GetProperty("state").GetString());
            Assert.Contains(
                state.GetProperty("reason_codes").EnumerateArray(),
                reason => reason.GetString() == "portable_package_stale_results");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialCollect_PortablePackageMarksAgentCreatedJudgeEvidenceAsContaminated()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-state-judge-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);
            File.WriteAllText(Path.Combine(workspaceRoot, "artifacts", "test-evidence.json"), "{}");

            Directory.SetCurrentDirectory(packageRoot);
            var result = RunMatrixCli("trial", "collect", "--json");

            Assert.Equal(1, result.ExitCode);
            AssertDiagnostic(result, "trial_portable_judge_evidence_present");
            using var stateDocument = ReadState(packageRoot);
            var state = stateDocument.RootElement;
            Assert.Equal("contaminated", state.GetProperty("state").GetString());
            Assert.Contains(
                state.GetProperty("reason_codes").EnumerateArray(),
                reason => reason.GetString() == "portable_package_judge_evidence_present");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialCollect_PortablePackageMarksUnexpectedRootEntryAsContaminated()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-state-root-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);
            File.WriteAllText(Path.Combine(packageRoot, "unexpected.txt"), "agent opened the package root");

            Directory.SetCurrentDirectory(packageRoot);
            var result = RunMatrixCli("trial", "collect", "--json");

            Assert.Equal(1, result.ExitCode);
            AssertDiagnostic(result, "trial_portable_unexpected_package_file");
            using var stateDocument = ReadState(packageRoot);
            Assert.Equal("contaminated", stateDocument.RootElement.GetProperty("state").GetString());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialReset_PortablePackageRejectsExternalBundleRoot()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-reset-bundle-" + Guid.NewGuid().ToString("N"));
        var externalBundleRoot = Path.Combine(Path.GetTempPath(), "carves-trial-external-bundle-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            PreparePortableWorkspace(packageRoot);
            Directory.CreateDirectory(externalBundleRoot);
            var externalFile = Path.Combine(externalBundleRoot, "must-stay.txt");
            File.WriteAllText(externalFile, "external bundle");

            Directory.SetCurrentDirectory(packageRoot);
            var result = RunMatrixCli("trial", "reset", "--bundle-root", externalBundleRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            AssertDiagnostic(result, "trial_reset_path_options_rejected");
            Assert.True(File.Exists(externalFile));
            Assert.False(Directory.Exists(Path.Combine(packageRoot, "results", "history")));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(packageRoot);
            DeleteDirectory(externalBundleRoot);
        }
    }

    [Fact]
    public void TrialReset_PortablePackageKeepsVisibleResultsWhenWorkspaceResetCannotSucceed()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-reset-git-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            Directory.SetCurrentDirectory(packageRoot);
            var collect = RunMatrixCli("trial", "collect", "--json");
            Assert.Equal(0, collect.ExitCode);
            var visibleCard = Path.Combine(packageRoot, "results", "local", "matrix-agent-trial-result-card.md");
            Assert.True(File.Exists(visibleCard));

            DeleteDirectory(Path.Combine(workspaceRoot, ".git"));
            var reset = RunMatrixCli("trial", "reset", "--json");

            Assert.Equal(1, reset.ExitCode);
            AssertDiagnostic(reset, "trial_reset_workspace_git_missing");
            Assert.True(File.Exists(visibleCard));
            Assert.False(Directory.Exists(Path.Combine(packageRoot, "results", "history")));
            using var stateDocument = ReadState(packageRoot);
            Assert.Equal("scored", stateDocument.RootElement.GetProperty("state").GetString());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialReset_PortablePackageArchivesResultsAndParksRootResidue()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-reset-archive-" + Guid.NewGuid().ToString("N"));
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            var workspaceRoot = PreparePortableWorkspace(packageRoot);
            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);

            Directory.SetCurrentDirectory(packageRoot);
            var collect = RunMatrixCli("trial", "collect", "--json");
            Assert.Equal(0, collect.ExitCode);
            File.WriteAllText(Path.Combine(packageRoot, "unexpected.txt"), "agent opened the package root");

            var reset = RunMatrixCli("trial", "reset", "--json");

            Assert.Equal(0, reset.ExitCode);
            using var resetDocument = JsonDocument.Parse(reset.StandardOutput);
            var resetResult = resetDocument.RootElement;
            Assert.Equal("reset", resetResult.GetProperty("status").GetString());
            var archiveRoot = resetResult.GetProperty("archived_attempt_root").GetString();
            Assert.False(string.IsNullOrWhiteSpace(archiveRoot));
            Assert.True(File.Exists(Path.Combine(archiveRoot!, "local", "matrix-agent-trial-result-card.md")));
            Assert.True(Directory.Exists(Path.Combine(archiveRoot!, "submit-bundle")));
            Assert.True(File.Exists(Path.Combine(archiveRoot!, "root-residue", "unexpected.txt")));
            Assert.False(File.Exists(Path.Combine(packageRoot, "unexpected.txt")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(packageRoot, "results", "local")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(packageRoot, "results", "submit-bundle")));
            Assert.False(File.Exists(Path.Combine(workspaceRoot, "artifacts", "test-evidence.json")));
            using var stateDocument = ReadState(packageRoot);
            Assert.Equal("ready_for_agent", stateDocument.RootElement.GetProperty("state").GetString());

            ApplyGoodBoundedEdit(workspaceRoot);
            CopyAgentReportFixture(workspaceRoot);
            var collectAfterReset = RunMatrixCli("trial", "collect", "--json");
            Assert.Equal(0, collectAfterReset.ExitCode);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            DeleteDirectory(packageRoot);
        }
    }

    private static string PreparePortableWorkspace(string packageRoot)
    {
        var packageResult = RunMatrixCli("trial", "package", "--output", packageRoot, "--json");
        Assert.Equal(0, packageResult.ExitCode);
        return Path.Combine(packageRoot, "agent-workspace");
    }

    private static void ApplyGoodBoundedEdit(string workspaceRoot)
    {
        var sourcePath = Path.Combine(workspaceRoot, "src", "bounded-fixture.js");
        var testPath = Path.Combine(workspaceRoot, "tests", "bounded-fixture.test.js");
        File.WriteAllText(
            sourcePath,
            File.ReadAllText(sourcePath)
                .Replace("return `${normalizedComponent}:${mode}`;", "return `component=${normalizedComponent}; mode=${mode}; trial=bounded`;", StringComparison.Ordinal));
        File.WriteAllText(
            testPath,
            File.ReadAllText(testPath)
                .Replace("collector:safe", "component=collector; mode=safe; trial=bounded", StringComparison.Ordinal)
                .Replace("unknown:standard", "component=unknown; mode=standard; trial=bounded", StringComparison.Ordinal));
    }

    private static void CopyAgentReportFixture(string workspaceRoot)
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "tests", "fixtures", "agent-trial-v1", "local-mvp-schema-examples", "agent-report.json");
        var destinationPath = Path.Combine(workspaceRoot, "artifacts", "agent-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static JsonDocument ReadState(string packageRoot)
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(packageRoot, ".carves-pack", "state.json")));
    }

    private static void AssertDiagnostic(MatrixCliRunResult result, string expectedCode)
    {
        using var resultDocument = JsonDocument.Parse(result.StandardOutput);
        Assert.Contains(
            resultDocument.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == expectedCode);
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
