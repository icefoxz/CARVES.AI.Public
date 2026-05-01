using System.Text.Json;
using System.Xml.Linq;
using Carves.Handoff.Core;

namespace Carves.Handoff.Tests;

public sealed class HandoffStandaloneReadinessTests
{
    [Fact]
    public void Draft_AllowsMultipleExplicitPacketsUnderHandoffDirectory()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new HandoffDraftService();

        var first = service.Draft(workspace.RootPath, ".ai/handoff/first.json");
        var second = service.Draft(workspace.RootPath, ".ai/handoff/second.json");

        Assert.True(first.Written);
        Assert.True(second.Written);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai", "handoff", "first.json")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai", "handoff", "second.json")));
    }

    [Fact]
    public void Inspect_EmptyPacketReturnsIncompleteInvalid()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/handoff/handoff.json", "{}");

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, HandoffDefaults.DefaultPacketPath);

        Assert.Equal("incomplete", result.InspectionStatus);
        Assert.Equal("invalid", result.Readiness.Decision);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "packet.incomplete");
    }

    [Fact]
    public void Project_UnicodePacketPreservesObjective()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffTestPackets.ValidPacket(objective: "继续完成中文交接，不要重复扫描。"));

        var result = new HandoffProjectionService().Project(workspace.RootPath, HandoffDefaults.DefaultPacketPath);

        Assert.Equal("continue", result.Action);
        Assert.Equal("继续完成中文交接，不要重复扫描。", result.CurrentObjective);
    }

    [Fact]
    public void Inspect_LargePacketWithManyContextRefsRemainsReady()
    {
        using var workspace = new TemporaryWorkspace();
        var refs = string.Join(",\n", Enumerable.Range(1, 60).Select(index =>
            $$"""{ "ref": "docs/context-{{index}}.md", "reason": "Context {{index}}.", "priority": {{index}} }"""));
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffTestPackets.ValidPacket().Replace("""
          "context_refs": [
            {
              "ref": "docs/example.md",
              "reason": "Provides bounded context.",
              "priority": 1
            }
          ],
        """, $$"""
          "context_refs": [
            {{refs}}
          ],
        """, StringComparison.Ordinal));

        var result = new HandoffInspectionService().Inspect(workspace.RootPath, HandoffDefaults.DefaultPacketPath);

        Assert.Equal("ready", result.Readiness.Decision);
        Assert.Equal(60, result.ContextRefs.Count);
    }

    [Fact]
    public void InspectionService_IsDecomposedIntoValidationResultsAndReaderComponents()
    {
        var repoRoot = LocateSourceRepoRoot();
        var coreRoot = Path.Combine(repoRoot, "src", "CARVES.Handoff.Core");
        var expectedFiles = new[]
        {
            "HandoffInspectionService.cs",
            "HandoffInspectionService.Validation.cs",
            "HandoffInspectionService.Results.cs",
            "HandoffInspectionService.Readers.cs",
        };

        foreach (var file in expectedFiles)
        {
            var path = Path.Combine(coreRoot, file);
            Assert.True(File.Exists(path), $"Missing Handoff inspection component: {file}");
            Assert.Contains("partial class HandoffInspectionService", File.ReadAllText(path), StringComparison.Ordinal);
        }

        Assert.True(File.ReadAllLines(Path.Combine(coreRoot, "HandoffInspectionService.cs")).Length < 420);
        Assert.Contains("ValidatePacketFreshness", File.ReadAllText(Path.Combine(coreRoot, "HandoffInspectionService.Validation.cs")), StringComparison.Ordinal);
        Assert.Contains("BuildResult", File.ReadAllText(Path.Combine(coreRoot, "HandoffInspectionService.Results.cs")), StringComparison.Ordinal);
        Assert.Contains("ReadReferences", File.ReadAllText(Path.Combine(coreRoot, "HandoffInspectionService.Readers.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void PackageMetadataDocsAndSmoke_AreStandalonePublishable()
    {
        var repoRoot = LocateSourceRepoRoot();
        var cliProject = XDocument.Load(Path.Combine(repoRoot, "src", "CARVES.Handoff.Cli", "Carves.Handoff.Cli.csproj"));
        var coreProject = XDocument.Load(Path.Combine(repoRoot, "src", "CARVES.Handoff.Core", "Carves.Handoff.Core.csproj"));
        var distribution = File.ReadAllText(Path.Combine(repoRoot, "docs", "guides", "CARVES_HANDOFF_DISTRIBUTION.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "docs", "handoff", "README.md"));
        var quickstartEn = File.ReadAllText(Path.Combine(repoRoot, "docs", "handoff", "quickstart.en.md"));
        var quickstartZh = File.ReadAllText(Path.Combine(repoRoot, "docs", "handoff", "quickstart.zh-CN.md"));
        var checkpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "handoff-publish-checkpoint.md"));
        var smoke = File.ReadAllText(Path.Combine(repoRoot, "scripts", "handoff", "handoff-packaged-install-smoke.ps1"));

        Assert.Equal("CARVES.Handoff.Cli", ReadProperty(cliProject, "PackageId"));
        Assert.Equal("carves-handoff", ReadProperty(cliProject, "ToolCommandName"));
        Assert.Equal("Apache-2.0", ReadProperty(cliProject, "PackageLicenseExpression"));
        Assert.Equal("https://github.com/CARVES-AI/CARVES.Runtime", ReadProperty(cliProject, "RepositoryUrl"));
        Assert.Contains("ai-continuity", ReadProperty(cliProject, "PackageTags"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Apache-2.0", ReadProperty(coreProject, "PackageLicenseExpression"));

        foreach (var doc in new[] { distribution, readme, quickstartEn, quickstartZh, checkpoint })
        {
            Assert.Contains(".ai/handoff/handoff.json", doc, StringComparison.Ordinal);
            Assert.DoesNotContain("TaskGraph", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Runtime internal planning", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("\"tool\"", smoke, StringComparison.Ordinal);
        Assert.Contains("\"install\"", smoke, StringComparison.Ordinal);
        Assert.Contains("CARVES.Handoff.Cli", smoke, StringComparison.Ordinal);
        Assert.Contains("\"help\"", smoke, StringComparison.Ordinal);
        Assert.Contains("\"draft\", \"--json\"", smoke, StringComparison.Ordinal);
        Assert.Contains("\"inspect\", \"--json\"", smoke, StringComparison.Ordinal);
        Assert.Contains("\"next\", \"--json\"", smoke, StringComparison.Ordinal);
        Assert.Contains("remote_registry_published = $false", smoke, StringComparison.Ordinal);
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
