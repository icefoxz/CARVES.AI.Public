namespace Carves.Matrix.Tests;

public sealed class MatrixPortableAgentTrialPackContractTests
{
    [Fact]
    public void PortablePackContractPinsLayoutAuthorityBoundaryAndOutputNames()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var contract = Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md");

        foreach (var text in new[]
        {
            "agent-workspace/",
            ".carves-pack/",
            "results/",
            "results/submit-bundle/",
            "README-FIRST.md",
            "COPY_THIS_TO_AGENT_BLIND.txt",
            "COPY_THIS_TO_AGENT_GUIDED.txt",
            "carves test collect",
            "carves test reset",
            "SCORE.cmd",
            "score.sh",
            "RESULT.cmd",
            "result.sh",
            "RESET.cmd",
            "reset.sh"
        })
        {
            Assert.Contains(text, contract, StringComparison.Ordinal);
        }

        Assert.Contains("Users should open only `agent-workspace/` in the tested agent.", contract, StringComparison.Ordinal);
        Assert.Contains("`.carves-pack/` is the local scorer authority area", contract, StringComparison.Ordinal);
        Assert.Contains("MUST NOT be placed inside `agent-workspace/`", contract, StringComparison.Ordinal);
        Assert.Contains("future upload-ready local output location", contract, StringComparison.Ordinal);
        Assert.Contains("V1 portable packages can be reused through the package reset command.", contract, StringComparison.Ordinal);
        Assert.Contains("After a completed score, `SCORE.cmd` and `score.sh` show the previous result", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicDocsReferencePortablePackWithoutClaimingAntiCheatOrHostedEligibility()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md"),
            Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md"),
            Read(repoRoot, "docs/matrix/known-limitations.md"));

        foreach (var text in new[]
        {
            "open only `agent-workspace/`",
            ".carves-pack/` is the local scorer authority",
            "must stay outside the agent writable workspace",
            "results/submit-bundle/",
            "RESULT.cmd",
            "RESET.cmd",
            "fresh extraction remains the cleanest strict-comparison path"
        })
        {
            Assert.Contains(text, docs, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var text in new[]
        {
            "does not provide local anti-cheat",
            "tamper-proof local execution",
            "hosted verification",
            "certification",
            "leaderboard eligibility",
            "producer identity"
        })
        {
            Assert.Contains(text, docs, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PortablePackContractDoesNotMakeLowLevelMatrixPathsTheMainStory()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var contract = Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md");

        var scoreIndex = contract.IndexOf("SCORE.cmd", StringComparison.Ordinal);
        var advancedIndex = contract.IndexOf("carves-matrix trial local --workspace", StringComparison.Ordinal);

        Assert.True(scoreIndex >= 0, "The product-level score script must be documented.");
        Assert.True(advancedIndex > scoreIndex, "Advanced Matrix path arguments should appear only after the package entry.");
        Assert.Contains("not the main portable package story", contract, StringComparison.Ordinal);
    }

    private static string Read(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
