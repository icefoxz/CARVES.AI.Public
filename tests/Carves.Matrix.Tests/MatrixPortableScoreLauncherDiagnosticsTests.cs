using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixPortableScoreLauncherDiagnosticsTests
{
    [Fact]
    public void TrialPackage_GeneratedScoreLaunchersExplainFirstRunFailuresWithoutMatrixInternals()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-launcher-diagnostics-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli("trial", "package", "--output", packageRoot, "--json");
            Assert.Equal(0, result.ExitCode);

            var scoreCmd = File.ReadAllText(Path.Combine(packageRoot, "SCORE.cmd"));
            var scoreSh = File.ReadAllText(Path.Combine(packageRoot, "score.sh"));
            var resultCmd = File.ReadAllText(Path.Combine(packageRoot, "RESULT.cmd"));
            var resultSh = File.ReadAllText(Path.Combine(packageRoot, "result.sh"));
            var resetCmd = File.ReadAllText(Path.Combine(packageRoot, "RESET.cmd"));
            var resetSh = File.ReadAllText(Path.Combine(packageRoot, "reset.sh"));
            var launchers = string.Join(
                Environment.NewLine,
                scoreCmd,
                scoreSh,
                resultCmd,
                resultSh,
                resetCmd,
                resetSh);

            Assert.Contains("\r\n:run_carves\r\n", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("\r\n:done\r\n", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("\r\n", resultCmd, StringComparison.Ordinal);
            Assert.Contains("\r\n", resetCmd, StringComparison.Ordinal);

            foreach (var text in new[]
            {
                "Mode: local only (this computer only; no upload, no certification, no leaderboard).",
                "Meaning: checks this folder and writes a local score.",
                "Missing dependency: Git is required for baseline and diff evidence.",
                "Missing dependency: Node.js is required for the official starter-pack task command.",
                "Install Node.js or put node",
                "Broken package layout: agent-workspace",
                "Broken package layout: .carves-pack",
                "Broken package layout: .carves-pack/state.json",
                "Package already scored. Showing the previous local result.",
                "Package already failed during scoring.",
                "Package is marked contaminated.",
                "Missing agent report: agent-workspace",
                "First open only agent-workspace",
                "COPY_THIS_TO_AGENT_BLIND.txt",
                "COPY_THIS_TO_AGENT_GUIDED.txt",
                "CARVES scorer/service was not found.",
                "not a complete",
                "scorer bundle",
                "Run RESULT.cmd to view this result again.",
                "Run RESET.cmd before testing another agent",
                "Run ./result.sh to view this result again.",
                "Run ./reset.sh before testing another agent",
                "CARVES Agent Trial local result",
                "No previous local result card was found.",
                "CARVES Agent Trial reset",
                "What reset clears",
                "What reset keeps",
                "Reset is local cleanup only"
            })
            {
                Assert.Contains(text, launchers, StringComparison.OrdinalIgnoreCase);
            }

            Assert.DoesNotContain("--workspace", launchers, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", launchers, StringComparison.Ordinal);
            Assert.DoesNotContain("carves-matrix", launchers, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialPackage_GeneratedScoreLaunchersStillDelegateToCarvesTestCollect()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-launcher-delegate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli("trial", "package", "--output", packageRoot, "--json");
            Assert.Equal(0, result.ExitCode);

            var scoreCmd = File.ReadAllText(Path.Combine(packageRoot, "SCORE.cmd"));
            var scoreSh = File.ReadAllText(Path.Combine(packageRoot, "score.sh"));
            var resetCmd = File.ReadAllText(Path.Combine(packageRoot, "RESET.cmd"));
            var resetSh = File.ReadAllText(Path.Combine(packageRoot, "reset.sh"));

            Assert.Contains("%CARVES% test collect %*", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("\"$CARVES\" test collect \"$@\"", scoreSh, StringComparison.Ordinal);
            Assert.Contains("%CARVES% test reset %*", resetCmd, StringComparison.Ordinal);
            Assert.Contains("\"$CARVES\" test reset \"$@\"", resetSh, StringComparison.Ordinal);
            Assert.Contains("PATHEXT=.COM;.EXE;.BAT;.CMD", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("PATHEXT=.COM;.EXE;.BAT;.CMD", resetCmd, StringComparison.Ordinal);
            Assert.Contains("where git.exe", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("where node.exe", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("where carves.exe", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("where carves.exe", resetCmd, StringComparison.Ordinal);
            Assert.Contains("if not \"%CARVES_AGENT_TEST_NO_PAUSE%\"==\"1\" pause", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("if not \"%CARVES_AGENT_TEST_NO_PAUSE%\"==\"1\" pause", resetCmd, StringComparison.Ordinal);
            Assert.Contains("tools\\carves\\carves.exe", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("tools\\carves\\carves.exe", resetCmd, StringComparison.Ordinal);
            Assert.Contains("./tools/carves/carves", scoreSh, StringComparison.Ordinal);
            Assert.Contains("./tools/carves/carves", resetSh, StringComparison.Ordinal);
            Assert.Contains("command -v node", scoreSh, StringComparison.Ordinal);
            Assert.Contains("if not errorlevel 1 goto already_scored", scoreCmd, StringComparison.Ordinal);
            Assert.Contains(":already_scored", scoreCmd, StringComparison.Ordinal);
            Assert.Contains(":previous_result_missing", scoreCmd, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
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
