namespace Carves.Matrix.Tests;

public sealed class MatrixWindowsScoreCmdPathSmokeContractTests
{
    [Fact]
    public void WindowsScoreCmdPathSmokePinsSpaceAndNonAsciiPathGate()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var script = Read(repoRoot, "scripts/matrix/smoke-windows-score-cmd-paths.ps1");
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "docs/matrix/README.md"),
            Read(repoRoot, "docs/matrix/portable-scorer-bundle.md"),
            Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md"),
            Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md"),
            Read(repoRoot, "docs/matrix/known-limitations.md"));

        foreach (var text in new[]
        {
            "scripts/matrix/smoke-windows-score-cmd-paths.ps1",
            "spaces and non-ASCII path text",
            "Windows SCORE.cmd path smoke",
            "local-only score output"
        })
        {
            Assert.Contains(text, docs, StringComparison.Ordinal);
        }

        foreach (var text in new[]
        {
            "Windows SCORE.cmd path smoke must run on Windows.",
            "CARVES Score Cmd Path Smoke",
            "路径",
            "release output 路径",
            "smoke-windows-score-cmd.ps1",
            "Path smoke work root must contain a space",
            "Path smoke work root must contain non-ASCII path text",
            "windows-scorecmd-path-smoke-summary.json",
            "carves-windows-scorecmd-path-smoke-summary.v0",
            "Convert-ToRedactedSmokeMessage",
            "Write-SmokeSummary",
            "<redacted-work-root>",
            "<redacted-repo-root>",
            "work_root_absolute_path_redacted",
            "success_results_root",
            "missing_scorer_results_root",
            "failure = $null",
            "status = $Status",
            "Path smoke failure summary:",
            "not_hosted_verification",
            "Path smoke summary:",
            "Windows SCORE.cmd path smoke passed."
        })
        {
            Assert.Contains(text, script, StringComparison.Ordinal);
        }
    }

    private static string Read(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
