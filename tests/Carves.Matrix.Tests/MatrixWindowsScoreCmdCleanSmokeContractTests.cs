namespace Carves.Matrix.Tests;

public sealed class MatrixWindowsScoreCmdCleanSmokeContractTests
{
    [Fact]
    public void WindowsScoreCmdCleanSmokeScriptPinsFreshExtractionAndIsolatedPathGate()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var script = Read(repoRoot, "scripts/matrix/smoke-windows-score-cmd.ps1");
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "docs/matrix/README.md"),
            Read(repoRoot, "docs/matrix/portable-scorer-bundle.md"),
            Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md"),
            Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md"),
            Read(repoRoot, "docs/matrix/known-limitations.md"));

        foreach (var text in new[]
        {
            "scripts/matrix/smoke-windows-score-cmd.ps1",
            "cmd.exe /d /c SCORE.cmd",
            "isolates PATH",
            "global `carves.exe` is not used",
            "task runtime tools such as `node.exe`",
            "missing package-local scorer diagnostic",
            "local-only score output",
            "RESULT.cmd",
            "RESET.cmd"
        })
        {
            Assert.Contains(text, docs, StringComparison.Ordinal);
        }

        foreach (var text in new[]
        {
            "Windows SCORE.cmd clean smoke must run on Windows.",
            "CARVES_AGENT_TEST_NO_PAUSE",
            "cmd.exe",
            "/d /c SCORE.cmd",
            "System.Diagnostics.ProcessStartInfo",
            "New-IsolatedPathExt",
            ".COM;.EXE;.BAT;.CMD",
            "$oldPathExt",
            "$env:PATHEXT",
            "Find-ToolDirectory -ToolName \"node.exe\"",
            "Isolated PATH unexpectedly contains a global carves.exe.",
            "Write-GoodAgentRun",
            "artifacts/agent-report.json",
            "Result card: results\\local\\matrix-agent-trial-result-card.md",
            "Assert-SuccessResult",
            "results/submit-bundle/matrix-artifact-manifest.json",
            "results/submit-bundle/matrix-proof-summary.json",
            "results/submit-bundle/trial/carves-agent-trial-result.json",
            "tools/carves/carves.exe",
            "carves.exe.hidden",
            "CARVES scorer/service was not found",
            "Missing scorer:",
            "no package-local scorer",
            "not a complete Windows playable package",
            "Missing dependency"
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
