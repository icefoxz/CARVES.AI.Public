namespace Carves.Runtime.Application.Tests;

public sealed class CodexGovernanceAssetTests
{
    [Fact]
    public void RepoLevelCodexGovernanceAssets_Exist()
    {
        var repoRoot = ResolveRepoRoot();

        Assert.True(File.Exists(Path.Combine(repoRoot, ".codex", "config.toml")));
        Assert.True(File.Exists(Path.Combine(repoRoot, ".codex", "rules", "carves-control-plane.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, ".codex", "rules", "carves-execution-boundary.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, ".codex", "skills", "carves-runtime", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "codex-native-governance-layer.md")));
    }

    [Fact]
    public void RepoLevelCodexGovernanceAssets_ExposeMustCallAndBoundaryGuidance()
    {
        var repoRoot = ResolveRepoRoot();
        var agents = File.ReadAllText(Path.Combine(repoRoot, "AGENTS.md"));
        var config = File.ReadAllText(Path.Combine(repoRoot, ".codex", "config.toml"));
        var controlPlaneRules = File.ReadAllText(Path.Combine(repoRoot, ".codex", "rules", "carves-control-plane.md"));
        var skill = File.ReadAllText(Path.Combine(repoRoot, ".codex", "skills", "carves-runtime", "SKILL.md"));
        var doc = File.ReadAllText(Path.Combine(repoRoot, "docs", "runtime", "codex-native-governance-layer.md"));

        Assert.Contains("Stateful actions", agents, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("task run", agents, StringComparison.Ordinal);
        Assert.Contains("sandbox = \"elevated\"", config, StringComparison.Ordinal);
        Assert.Contains("sync-state", controlPlaneRules, StringComparison.Ordinal);
        Assert.Contains("review-task", skill, StringComparison.Ordinal);
        Assert.Contains("bootstrap governance", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CARD-299", doc, StringComparison.Ordinal);
    }

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
