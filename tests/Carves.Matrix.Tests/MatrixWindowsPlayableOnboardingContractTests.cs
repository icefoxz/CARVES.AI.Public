namespace Carves.Matrix.Tests;

public sealed class MatrixWindowsPlayableOnboardingContractTests
{
    [Fact]
    public void NodeWindowsPlayableQuickstartPinsPhase7PublicOnboardingBoundary()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var quickstart = Read(repoRoot, "docs/matrix/agent-trial-node-windows-playable-quickstart.md");
        var phaseRecord = Read(repoRoot, "docs/matrix/agent-trial-node-phase-7-public-onboarding.md");
        var matrixReadme = Read(repoRoot, "docs/matrix/README.md");
        var localQuickstart = Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md");
        var rootReadme = Read(repoRoot, "README.md");

        foreach (var text in new[]
        {
            "parallel official local trial entry",
            "not yet the default public",
            "local trial entry",
            "Windows on x64 hardware",
            "Git on `PATH`",
            "Node.js on `PATH`",
            "open only `agent-workspace/`",
            "COPY_THIS_TO_AGENT_BLIND.txt",
            "COPY_THIS_TO_AGENT_GUIDED.txt",
            "SCORE.cmd",
            "RESULT.cmd",
            "RESET.cmd",
            "agent-workspace/artifacts/agent-report.json",
            "certification",
            "leaderboard eligibility",
            "hosted verification",
            "local anti-cheat",
            "tamper-proof local execution",
            "Agent Trial local quickstart"
        })
        {
            Assert.Contains(text, quickstart, StringComparison.Ordinal);
        }

        foreach (var text in new[]
        {
            "Status: accepted for public onboarding copy.",
            "Phase 7 is accepted.",
            "GO for Phase 8 small user trial planning.",
            "NO-GO for default public local trial promotion until Phase 8 evidence exists.",
            "agent-trial-node-windows-playable-quickstart.md"
        })
        {
            Assert.Contains(text, phaseRecord, StringComparison.Ordinal);
        }

        foreach (var surface in new[] { matrixReadme, localQuickstart, rootReadme })
        {
            Assert.Contains("agent-trial-node-windows-playable-quickstart.md", surface, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NodeWindowsPlayablePhase8PinsSmallUserTrialGate()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var phaseRecord = Read(repoRoot, "docs/matrix/agent-trial-node-phase-8-small-user-trial.md");
        var matrixReadme = Read(repoRoot, "docs/matrix/README.md");

        foreach (var text in new[]
        {
            "Status: accepted for small user trial execution packet.",
            "does not claim that external users have already completed the trial",
            "at least one Node/Web developer",
            "at least one AI coding agent user who has not read CARVES internals",
            "Open only `agent-workspace/`",
            "SCORE.cmd",
            "RESULT.cmd",
            "RESET.cmd",
            "Understood local-only result boundary",
            "at least 2 participant rows are filled",
            "NO-GO for default public local trial promotion",
            "docs/matrix/agent-trial-node-phase-8-user-trial-results.md",
            "private source, raw diff, prompt, model response, secret, credential",
            "parallel official local trial entry, not default public local trial entry"
        })
        {
            Assert.Contains(text, phaseRecord, StringComparison.Ordinal);
        }

        Assert.Contains(
            "agent-trial-node-phase-8-small-user-trial.md",
            matrixReadme,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NodeWindowsPlayablePhase9PinsDefaultEntryDecisionGate()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var phaseRecord = Read(repoRoot, "docs/matrix/agent-trial-node-phase-9-default-entry-decision.md");
        var matrixReadme = Read(repoRoot, "docs/matrix/README.md");

        foreach (var text in new[]
        {
            "Status: accepted as default-entry decision gate.",
            "KEEP Node Windows playable as a parallel official local trial entry.",
            "NO-GO for default public local trial promotion.",
            "No filled Phase 8 user-trial results exist yet.",
            "docs/matrix/agent-trial-node-phase-8-user-trial-results.md",
            "at least 2 participant rows",
            "at least one Node/Web developer",
            "at least one AI coding agent user who has not read CARVES internals",
            "successful participants open only `agent-workspace/`",
            "SCORE.cmd",
            "RESULT.cmd",
            "RESET.cmd",
            "Default local trial entry:",
            "Parallel official local trial entry:",
            "blocked until filled Phase 8 user-trial results exist and pass the gate",
            "small user trial has already run",
            "replacement of the existing source-checkout default quickstart"
        })
        {
            Assert.Contains(text, phaseRecord, StringComparison.Ordinal);
        }

        Assert.Contains(
            "agent-trial-node-phase-9-default-entry-decision.md",
            matrixReadme,
            StringComparison.Ordinal);
    }

    private static string Read(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
