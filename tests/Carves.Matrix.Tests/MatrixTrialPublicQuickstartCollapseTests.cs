namespace Carves.Matrix.Tests;

public sealed class MatrixTrialPublicQuickstartCollapseTests
{
    [Fact]
    public void PublicDocsLeadWithCarvesTestBeforeAdvancedMatrixPaths()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var readme = Read(repoRoot, "README.md");
        var matrixReadme = Read(repoRoot, "docs/matrix/README.md");
        var quickstart = Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md");
        var smoke = Read(repoRoot, "docs/matrix/agent-trial-v1-local-user-smoke.md");

        AssertFirst(readme, "test demo --json", "carves-matrix trial plan");
        AssertFirst(matrixReadme, "carves test demo", "carves-matrix trial plan");
        AssertFirst(quickstart, "test demo --json", "--workspace");
        AssertFirst(quickstart, "carves test demo", "carves-matrix trial plan");
        AssertFirst(smoke, "test demo --json", "trial plan --workspace");

        Assert.DoesNotContain("--bundle-root", FirstScreen(quickstart), StringComparison.Ordinal);
        Assert.DoesNotContain("--history-root", FirstScreen(quickstart), StringComparison.Ordinal);
        Assert.DoesNotContain("matrix-artifact-manifest", FirstScreen(quickstart), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("matrix-proof-summary", FirstScreen(quickstart), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicDocsKeepScoreMeaningNonClaimsLauncherAndDefaultOutput()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "README.md"),
            Read(repoRoot, "docs/matrix/README.md"),
            Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md"),
            Read(repoRoot, "docs/matrix/agent-trial-v1-local-user-smoke.md"),
            Read(repoRoot, "docs/matrix/known-limitations.md"));

        foreach (var text in new[]
        {
            "reviewability",
            "traceability",
            "explainability",
            "report honesty",
            "constraint adherence",
            "reproducibility evidence",
            "./carves-trials/",
            "Start-CARVES-Agent-Test.cmd",
            "trial_setup_pack_missing",
            "trial_setup_git_unavailable",
            "trial_agent_report_missing",
            "trial_verify_hash_mismatch"
        })
        {
            Assert.Contains(text, docs, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var text in new[]
        {
            "certification",
            "leaderboard eligibility",
            "hosted verification",
            "producer identity",
            "OS sandboxing",
            "semantic correctness",
            "local anti-cheat"
        })
        {
            Assert.Contains(text, docs, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AdvancedTrialCommandsRemainDocumentedAfterTheSimplePath()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var quickstart = Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md");

        var simpleIndex = quickstart.IndexOf("carves test demo", StringComparison.Ordinal);
        Assert.True(simpleIndex >= 0);

        foreach (var command in new[]
        {
            "carves-matrix trial plan",
            "carves-matrix trial prepare",
            "carves-matrix trial local",
            "carves-matrix trial verify",
            "carves-matrix trial record",
            "carves-matrix trial compare"
        })
        {
            var advancedIndex = quickstart.IndexOf(command, StringComparison.Ordinal);
            Assert.True(advancedIndex > simpleIndex, $"{command} should remain documented after the simple carves test path.");
        }
    }

    private static string Read(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string FirstScreen(string content)
    {
        const int maxLength = 1800;
        return content.Length <= maxLength ? content : content[..maxLength];
    }

    private static void AssertFirst(string content, string first, string second)
    {
        var firstIndex = content.IndexOf(first, StringComparison.Ordinal);
        var secondIndex = content.IndexOf(second, StringComparison.Ordinal);

        Assert.True(firstIndex >= 0, $"Expected to find {first}.");
        Assert.True(secondIndex >= 0, $"Expected to find {second}.");
        Assert.True(firstIndex < secondIndex, $"Expected {first} to appear before {second}.");
    }
}
