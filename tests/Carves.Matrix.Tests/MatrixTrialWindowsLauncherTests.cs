namespace Carves.Matrix.Tests;

public sealed class MatrixTrialWindowsLauncherTests
{
    [Fact]
    public void WindowsLauncherScripts_RouteToCarvesTestAndPauseBeforeExit()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var cmdPath = Path.Combine(repoRoot, "Start-CARVES-Agent-Test.cmd");
        var ps1Path = Path.Combine(repoRoot, "Start-CARVES-Agent-Test.ps1");

        Assert.True(File.Exists(cmdPath));
        Assert.True(File.Exists(ps1Path));

        var cmd = File.ReadAllText(cmdPath);
        Assert.Contains("Start-CARVES-Agent-Test.ps1", cmd, StringComparison.Ordinal);
        Assert.Contains("powershell.exe -NoProfile -ExecutionPolicy Bypass", cmd, StringComparison.Ordinal);
        Assert.Contains("CARVES_AGENT_TEST_NO_PAUSE", cmd, StringComparison.Ordinal);
        Assert.Contains("pause", cmd, StringComparison.OrdinalIgnoreCase);

        var ps1 = File.ReadAllText(ps1Path);
        Assert.Contains("carves test", ps1, StringComparison.Ordinal);
        Assert.Contains("@(\"test\")", ps1, StringComparison.Ordinal);
        Assert.Contains("Read-Host \"Selection [1]\"", ps1, StringComparison.Ordinal);
        Assert.Contains("Read-Host \"Press Enter to close this CARVES Agent Trial window\"", ps1, StringComparison.Ordinal);
        Assert.Contains("Get-Command \"carves\"", ps1, StringComparison.Ordinal);
        Assert.Contains("runtime-cli", ps1, StringComparison.Ordinal);
        Assert.Contains("carves.cmd", ps1, StringComparison.Ordinal);
        Assert.Contains("dotnet", ps1, StringComparison.Ordinal);
        Assert.Contains("score summary and result card path", ps1, StringComparison.Ordinal);
        Assert.Contains("The CLI diagnostic above is preserved as the source of truth.", ps1, StringComparison.Ordinal);
        Assert.Contains("not a sandbox, anti-cheat system, hosted verifier, or benchmark", ps1, StringComparison.Ordinal);
        Assert.DoesNotContain("carves-matrix", ps1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MatrixCliRunner", ps1, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsLauncherDocs_PointCommandLineUsersToCarvesTestDirectly()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var docs = string.Join(
            Environment.NewLine,
            File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "README.md")),
            File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "agent-trial-v1-local-quickstart.md")),
            File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "agent-trial-v1-local-user-smoke.md")));

        Assert.Contains("Start-CARVES-Agent-Test.cmd", docs, StringComparison.Ordinal);
        Assert.Contains("Start-CARVES-Agent-Test.ps1", docs, StringComparison.Ordinal);
        Assert.Contains("carves test demo", docs, StringComparison.Ordinal);
        Assert.Contains("carves test agent", docs, StringComparison.Ordinal);
        Assert.Contains("Command-line users can", docs, StringComparison.Ordinal);
        Assert.Contains("pauses before closing", docs, StringComparison.Ordinal);
        Assert.Contains("not a sandbox", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("leaderboard", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("certification", docs, StringComparison.OrdinalIgnoreCase);
    }
}
