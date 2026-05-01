using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Infrastructure.CodeGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class CodeGraphAuditServiceTests
{
    [Fact]
    public void Audit_PassesForSourceOnlyCodeGraph()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/Sample.Module/OrderService.cs", """
namespace Sample.Module;

public sealed class OrderService
{
    public void PlanOrder()
    {
    }
}
""");
        var systemConfig = TestSystemConfigFactory.Create(["src"]);
        var builder = new FileCodeGraphBuilder(workspace.RootPath, workspace.Paths, systemConfig);
        var query = new FileCodeGraphQueryService(workspace.Paths, builder);
        builder.Build();

        var report = new CodeGraphAuditService(workspace.RootPath, workspace.Paths, systemConfig, query).Audit();

        Assert.True(report.StrictPassed);
        Assert.Empty(report.Findings);
        Assert.All(report.ModulePurity, module => Assert.Equal(1d, module.PurityRatio));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai", "codegraph", "audit.json")));
    }

    [Fact]
    public void Audit_FailsWhenForbiddenArtifactLeaksIntoIndex()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/Sample.Module/OrderService.cs", """
namespace Sample.Module;

public sealed class OrderService
{
    public void PlanOrder()
    {
    }
}
""");
        var systemConfig = TestSystemConfigFactory.Create(["src"]);
        var builder = new FileCodeGraphBuilder(workspace.RootPath, workspace.Paths, systemConfig);
        var query = new FileCodeGraphQueryService(workspace.Paths, builder);
        builder.Build();

        var moduleShardPath = Path.Combine(workspace.RootPath, ".ai", "codegraph", "modules", "Sample.Module.json");
        var indexNode = JsonNode.Parse(File.ReadAllText(moduleShardPath))!.AsObject();
        var files = indexNode["files"]!.AsArray();
        files.Add(new JsonObject
        {
            ["node_id"] = "file:src/Sample.Module/obj/project.assets.json",
            ["path"] = "src/Sample.Module/obj/project.assets.json",
            ["module"] = "Sample.Module",
            ["language"] = "json",
            ["summary"] = "generated artifact",
            ["type_ids"] = new JsonArray(),
            ["callable_ids"] = new JsonArray(),
            ["dependency_modules"] = new JsonArray(),
            ["tokens"] = new JsonArray("project", "assets"),
        });
        File.WriteAllText(moduleShardPath, indexNode.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true }));

        var report = new CodeGraphAuditService(workspace.RootPath, workspace.Paths, systemConfig, query).Audit();

        Assert.False(report.StrictPassed);
        Assert.Contains(report.Findings, finding => string.Equals(finding.Category, "forbidden_path_leakage", StringComparison.Ordinal));
        Assert.Contains(report.Findings, finding => string.Equals(finding.Category, "non_source_language", StringComparison.Ordinal));
    }
}
