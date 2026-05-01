namespace Carves.Runtime.IntegrationTests;

public sealed class ShieldDocsWikiTests
{
    [Fact]
    public void ShieldDocsWiki_PublishesBilingualUserEntry()
    {
        var repoRoot = LocateSourceRepoRoot();
        var shieldReadme = Read(repoRoot, "docs/shield/README.md");
        var wikiReadme = Read(repoRoot, "docs/shield/wiki/README.md");
        var wikiHome = Read(repoRoot, "docs/shield/wiki/Home.md");

        Assert.Contains("CARVES Shield is a local-first AI governance self-check", shieldReadme, StringComparison.Ordinal);
        Assert.Contains("wiki/shield-beginner-guide.zh-CN.md", shieldReadme, StringComparison.Ordinal);
        Assert.Contains("wiki/shield-beginner-guide.en.md", shieldReadme, StringComparison.Ordinal);
        Assert.Contains("wiki/glossary.zh-CN.md", shieldReadme, StringComparison.Ordinal);
        Assert.Contains("wiki/glossary.en.md", shieldReadme, StringComparison.Ordinal);
        Assert.Contains("wiki/github-actions.zh-CN.md", shieldReadme, StringComparison.Ordinal);
        Assert.Contains("wiki/github-actions.en.md", shieldReadme, StringComparison.Ordinal);
        Assert.Contains("local-first AI governance self-check", shieldReadme, StringComparison.Ordinal);
        Assert.Contains("It is not hosted verification", shieldReadme, StringComparison.Ordinal);

        Assert.Contains("中文入口", wikiReadme, StringComparison.Ordinal);
        Assert.Contains("English entry", wikiReadme, StringComparison.Ordinal);
        Assert.Contains("新手教程", wikiHome, StringComparison.Ordinal);
        Assert.Contains("Beginner guide", wikiHome, StringComparison.Ordinal);
        Assert.Contains("carves shield evaluate", wikiReadme, StringComparison.Ordinal);
        Assert.Contains("carves shield badge", wikiReadme, StringComparison.Ordinal);
    }

    [Fact]
    public void ShieldDocsWiki_CoversWorkflowEvidenceGithubActionsAndBadge()
    {
        var repoRoot = LocateSourceRepoRoot();
        var requiredPages = new[]
        {
            "docs/shield/wiki/shield-beginner-guide.zh-CN.md",
            "docs/shield/wiki/shield-beginner-guide.en.md",
            "docs/shield/wiki/glossary.zh-CN.md",
            "docs/shield/wiki/glossary.en.md",
            "docs/shield/wiki/workflow.zh-CN.md",
            "docs/shield/wiki/workflow.en.md",
            "docs/shield/wiki/evidence-starter.zh-CN.md",
            "docs/shield/wiki/evidence-starter.en.md",
            "docs/shield/wiki/github-actions.zh-CN.md",
            "docs/shield/wiki/github-actions.en.md",
            "docs/shield/wiki/badge.zh-CN.md",
            "docs/shield/wiki/badge.en.md",
        };

        foreach (var page in requiredPages)
        {
            Assert.True(File.Exists(Path.Combine(repoRoot, page)), page);
        }

        var workflowZh = Read(repoRoot, "docs/shield/wiki/workflow.zh-CN.md");
        var workflowEn = Read(repoRoot, "docs/shield/wiki/workflow.en.md");
        Assert.Contains("```mermaid", workflowZh, StringComparison.Ordinal);
        Assert.Contains("carves shield evaluate", workflowZh, StringComparison.Ordinal);
        Assert.Contains("carves shield badge", workflowZh, StringComparison.Ordinal);
        Assert.Contains("```mermaid", workflowEn, StringComparison.Ordinal);
        Assert.Contains("carves shield evaluate", workflowEn, StringComparison.Ordinal);
        Assert.Contains("carves shield badge", workflowEn, StringComparison.Ordinal);

        var evidenceZh = Read(repoRoot, "docs/shield/wiki/evidence-starter.zh-CN.md");
        var evidenceEn = Read(repoRoot, "docs/shield/wiki/evidence-starter.en.md");
        Assert.Contains("\"schema_version\": \"shield-evidence.v0\"", evidenceZh, StringComparison.Ordinal);
        Assert.Contains("\"source_included\": false", evidenceZh, StringComparison.Ordinal);
        Assert.Contains("\"raw_diff_included\": false", evidenceEn, StringComparison.Ordinal);
        Assert.Contains("\"upload_intent\": \"local_only\"", evidenceEn, StringComparison.Ordinal);

        var actionsZh = Read(repoRoot, "docs/shield/wiki/github-actions.zh-CN.md");
        var actionsEn = Read(repoRoot, "docs/shield/wiki/github-actions.en.md");
        Assert.Contains("actions/setup-dotnet@v4", actionsZh, StringComparison.Ordinal);
        Assert.Contains("carves shield evaluate .carves/shield-evidence.json", actionsZh, StringComparison.Ordinal);
        Assert.Contains("carves shield badge .carves/shield-evidence.json", actionsEn, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", actionsEn, StringComparison.Ordinal);
        Assert.Contains("shield-evaluate.json", actionsEn, StringComparison.Ordinal);
        Assert.Contains("shield-badge.svg", actionsZh, StringComparison.Ordinal);

        var badgeZh = Read(repoRoot, "docs/shield/wiki/badge.zh-CN.md");
        var badgeEn = Read(repoRoot, "docs/shield/wiki/badge.en.md");
        Assert.Contains("self-check", badgeZh, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("It is not certification", badgeEn, StringComparison.Ordinal);
        Assert.Contains("G8.H8.A8", badgeZh, StringComparison.Ordinal);
        Assert.Contains("G8.H8.A8", badgeEn, StringComparison.Ordinal);
    }

    [Fact]
    public void ShieldGlossary_DefinesScoreAndMatrixResultTermsWithoutCertificationClaims()
    {
        var repoRoot = LocateSourceRepoRoot();
        var glossaryZh = Read(repoRoot, "docs/shield/wiki/glossary.zh-CN.md");
        var glossaryEn = Read(repoRoot, "docs/shield/wiki/glossary.en.md");

        foreach (var glossary in new[] { glossaryZh, glossaryEn })
        {
            Assert.Contains("## G/H/A", glossary, StringComparison.Ordinal);
            Assert.Contains("## Lite score", glossary, StringComparison.Ordinal);
            Assert.Contains("## PASS", glossary, StringComparison.Ordinal);
            Assert.Contains("## REVIEW", glossary, StringComparison.Ordinal);
            Assert.Contains("## BLOCK", glossary, StringComparison.Ordinal);
            Assert.Contains("## Local self-check", glossary, StringComparison.Ordinal);
            Assert.Contains("## Challenge result", glossary, StringComparison.Ordinal);
            Assert.Contains("## Verification result", glossary, StringComparison.Ordinal);
            Assert.Contains("governance maturity", glossary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("certification", glossary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("model safety", glossary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hosted verification", glossary, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ShieldAndMatrixQuickstarts_LinkScoreGlossary()
    {
        var repoRoot = LocateSourceRepoRoot();
        var shieldBeginnerEn = Read(repoRoot, "docs/shield/wiki/shield-beginner-guide.en.md");
        var shieldBeginnerZh = Read(repoRoot, "docs/shield/wiki/shield-beginner-guide.zh-CN.md");
        var shieldChallenge = Read(repoRoot, "docs/shield/lite-challenge-quickstart.md");
        var matrixQuickstartEn = Read(repoRoot, "docs/matrix/quickstart.en.md");
        var matrixQuickstartZh = Read(repoRoot, "docs/matrix/quickstart.zh-CN.md");

        Assert.Contains("glossary.en.md", shieldBeginnerEn, StringComparison.Ordinal);
        Assert.Contains("glossary.zh-CN.md", shieldBeginnerZh, StringComparison.Ordinal);
        Assert.Contains("wiki/glossary.en.md", shieldChallenge, StringComparison.Ordinal);
        Assert.Contains("../shield/wiki/glossary.en.md", matrixQuickstartEn, StringComparison.Ordinal);
        Assert.Contains("../shield/wiki/glossary.zh-CN.md", matrixQuickstartZh, StringComparison.Ordinal);
    }

    [Fact]
    public void ShieldDocsWiki_PreservesShieldOnlyPublicBoundary()
    {
        var repoRoot = LocateSourceRepoRoot();
        var wikiCorpus = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(repoRoot, "docs", "shield", "wiki"), "*.md", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));

        Assert.Contains("does not require", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source upload", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw diff", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prompt", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("credential", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("self-check", wikiCorpus, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(".ai/tasks", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".ai/runtime", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TaskGraph", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorkerService", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("planning truth", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"certification\": true", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"verified\": true", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source_upload_required\": true", wikiCorpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hosted_api_required\": true", wikiCorpus, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string LocateSourceRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CARVES.Runtime.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root from test output directory.");
    }
}
