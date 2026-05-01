using System.Xml.Linq;

namespace Carves.Runtime.IntegrationTests;

public sealed class GuardPublishableTests
{
    [Fact]
    public void GuardPackageMetadata_IsPreparedForPublicPrereleasePackaging()
    {
        var repoRoot = LocateSourceRepoRoot();
        var project = XDocument.Load(Path.Combine(repoRoot, "src", "CARVES.Guard.Cli", "Carves.Guard.Cli.csproj"));

        Assert.Equal("true", ReadProperty(project, "PackAsTool"));
        Assert.Equal("carves-guard", ReadProperty(project, "ToolCommandName"));
        Assert.Equal("CARVES.Guard.Cli", ReadProperty(project, "PackageId"));
        Assert.Equal("0.2.0-beta.1", ReadProperty(project, "Version"));
        Assert.Equal("README.md", ReadProperty(project, "PackageReadmeFile"));
        Assert.Equal("Apache-2.0", ReadProperty(project, "PackageLicenseExpression"));
        Assert.Equal("https://github.com/CARVES-AI/CARVES.Runtime", ReadProperty(project, "PackageProjectUrl"));
        Assert.Equal("https://github.com/CARVES-AI/CARVES.Runtime", ReadProperty(project, "RepositoryUrl"));
        Assert.Contains("guard", ReadProperty(project, "PackageTags"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ai-safety", ReadProperty(project, "PackageTags"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GuardPublishableSmokeScripts_ProveLocalToolInstallAndActionsTemplateShape()
    {
        var repoRoot = LocateSourceRepoRoot();
        var packageSmoke = File.ReadAllText(Path.Combine(repoRoot, "scripts", "guard", "guard-packaged-install-smoke.ps1"));
        var actionsSmoke = File.ReadAllText(Path.Combine(repoRoot, "scripts", "guard", "guard-github-actions-template-smoke.ps1"));

        Assert.Contains("\"pack\"", packageSmoke, StringComparison.Ordinal);
        Assert.Contains("\"tool\"", packageSmoke, StringComparison.Ordinal);
        Assert.Contains("\"install\"", packageSmoke, StringComparison.Ordinal);
        Assert.Contains("\"--tool-path\"", packageSmoke, StringComparison.Ordinal);
        Assert.Contains("\"CARVES.Guard.Cli\"", packageSmoke, StringComparison.Ordinal);
        Assert.Contains("\"help\"", packageSmoke, StringComparison.Ordinal);
        Assert.Contains("\"init\", \"--json\"", packageSmoke, StringComparison.Ordinal);
        Assert.Contains("\"check\", \"--json\"", packageSmoke, StringComparison.Ordinal);
        Assert.Contains("remote_registry_published = $false", packageSmoke, StringComparison.Ordinal);
        Assert.Contains("nuget_org_push_required = $false", packageSmoke, StringComparison.Ordinal);

        Assert.Contains("guard_github_actions_template_shape", actionsSmoke, StringComparison.Ordinal);
        Assert.Contains("actions/checkout@v4", actionsSmoke, StringComparison.Ordinal);
        Assert.Contains("guard check --json", actionsSmoke, StringComparison.Ordinal);
        Assert.Contains("hosted_secrets_required = $false", actionsSmoke, StringComparison.Ordinal);
        Assert.Contains("remote_registry_publication_required = $false", actionsSmoke, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardQuickstarts_AreBilingualLinkedAndGuardOnly()
    {
        var repoRoot = LocateSourceRepoRoot();
        var readme = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "README.md"));
        var english = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "quickstart.en.md"));
        var chinese = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "quickstart.zh-CN.md"));
        var wikiEnglish = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "github-actions.en.md"));
        var wikiChinese = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "github-actions.zh-CN.md"));

        Assert.Contains("quickstart.en.md", readme, StringComparison.Ordinal);
        Assert.Contains("quickstart.zh-CN.md", readme, StringComparison.Ordinal);
        Assert.Contains("carves guard init", english, StringComparison.Ordinal);
        Assert.Contains("carves guard check --json", english, StringComparison.Ordinal);
        Assert.Contains("decision: allow", english, StringComparison.Ordinal);
        Assert.Contains("decision: block", english, StringComparison.Ordinal);
        Assert.Contains("github-actions-template.yml", english, StringComparison.Ordinal);
        Assert.Contains("carves guard init", chinese, StringComparison.Ordinal);
        Assert.Contains("carves guard check --json", chinese, StringComparison.Ordinal);
        Assert.Contains("decision: allow", chinese, StringComparison.Ordinal);
        Assert.Contains("decision: block", chinese, StringComparison.Ordinal);
        Assert.Contains("github-actions-template.yml", chinese, StringComparison.Ordinal);
        Assert.Contains("../github-actions-template.yml", wikiEnglish, StringComparison.Ordinal);
        Assert.Contains("../github-actions-template.yml", wikiChinese, StringComparison.Ordinal);

        foreach (var doc in new[] { english, chinese })
        {
            Assert.DoesNotContain("TaskGraph", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Planner", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WorkerService", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OS sandbox", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("certification", doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GuardGithubActionsTemplate_IsCopyableAndFailsOnReviewOrBlock()
    {
        var repoRoot = LocateSourceRepoRoot();
        var template = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "github-actions-template.yml"));
        var doc = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "github-actions-template.md"));

        Assert.Contains("pull_request:", template, StringComparison.Ordinal);
        Assert.Contains("actions/checkout@v4", template, StringComparison.Ordinal);
        Assert.Contains("actions/setup-dotnet@v4", template, StringComparison.Ordinal);
        Assert.Contains("repository: CARVES-AI/CARVES.Runtime", template, StringComparison.Ordinal);
        Assert.Contains("guard init", template, StringComparison.Ordinal);
        Assert.Contains("guard check --json", template, StringComparison.Ordinal);
        Assert.Contains("guard-check.json", template, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", template, StringComparison.Ordinal);
        Assert.Contains("carves-guard-decision", template, StringComparison.Ordinal);
        Assert.Contains("exit \"$guard_exit\"", template, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", template, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("review` or `block", doc, StringComparison.Ordinal);
        Assert.Contains("does not require NuGet.org publication", doc, StringComparison.Ordinal);
        Assert.Contains("not an operating-system sandbox", doc, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadProperty(XDocument project, string propertyName)
    {
        return project
            .Descendants(propertyName)
            .Select(element => element.Value.Trim())
            .FirstOrDefault();
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
