namespace Carves.Matrix.Tests;

public sealed class MatrixWindowsPlayableCiSmokeContractTests
{
    [Fact]
    public void MatrixProofWorkflowPinsWindowsPlayableScoreCmdSmoke()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var workflow = Read(repoRoot, ".github/workflows/matrix-proof.yml");
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "docs/matrix/README.md"),
            Read(repoRoot, "docs/matrix/portable-scorer-bundle.md"),
            Read(repoRoot, "docs/matrix/known-limitations.md"));

        foreach (var text in new[]
        {
            "Windows playable SCORE.cmd CI smoke",
            ".github/workflows/matrix-proof.yml",
            "scripts/matrix/smoke-windows-score-cmd-paths.ps1",
            "failure summary",
            "local-only score output"
        })
        {
            Assert.Contains(text, docs, StringComparison.Ordinal);
        }

        foreach (var text in new[]
        {
            "windows-playable-scorecmd-smoke:",
            "Windows playable SCORE.cmd smoke",
            "runs-on: windows-latest",
            "timeout-minutes: 30",
            "dotnet-version: 10.0.x",
            "CARVES Score Cmd Path Smoke \u8def\u5f84",
            "smoke-windows-score-cmd-paths.ps1",
            "-Configuration Release",
            "-BuildLabel \"github-actions-${{ github.run_id }}\"",
            "carves-windows-playable-scorecmd-smoke",
            "windows-scorecmd-path-smoke-summary.json",
            "release output \u8def\u5f84/carves-agent-trial-pack-win-x64.zip",
            "fresh extracted success/results/**",
            "fresh extracted missing scorer/results/**"
        })
        {
            Assert.Contains(text, workflow, StringComparison.Ordinal);
        }
    }

    private static string Read(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
