using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Infrastructure.CodeGraph;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using ApplicationTaskScheduler = Carves.Runtime.Application.TaskGraph.TaskScheduler;

namespace Carves.Runtime.Application.Tests;

public sealed class CodeGraphPlannerTests
{
    [Fact]
    public void Builder_WritesModuleFileCallableAndDependencyIndexes()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/Sample.Module/OrderService.cs", """
using Sample.Dependency;

namespace Sample.Module;

public sealed class OrderService
{
    public void PlanOrder()
    {
    }
}
""");
        workspace.WriteFile("src/Sample.Dependency/OrderRecord.cs", """
namespace Sample.Dependency;

public sealed record OrderRecord(string Id);
""");

        var builder = new FileCodeGraphBuilder(workspace.RootPath, workspace.Paths, TestSystemConfigFactory.Create());
        var result = builder.Build();
        var orderFile = Assert.Single(result.Index.Files, file => string.Equals(file.Path, "src/Sample.Module/OrderService.cs", StringComparison.Ordinal));

        Assert.Equal(2, result.Index.Modules.Count);
        Assert.Contains(result.Index.Callables, callable => callable.QualifiedName.EndsWith(".PlanOrder", StringComparison.Ordinal));
        Assert.Contains("Sample.Dependency", orderFile.DependencyModules);
    }

    [Fact]
    public void Planner_UsesCodeGraphScopeAnalysisWhenPlanningCard()
    {
        using var workspace = new TemporaryWorkspace();
        var cardPath = workspace.WriteFile(".ai/tasks/cards/CARD-TEST.md", """
# CARD-TEST
Title: Planner uses codegraph
Type: feature
Priority: P1

## Goal
Use codegraph data during planning.

## Scope
- `src/Sample.Module/`

## Acceptance
- planner sees scope
""");
        workspace.WriteFile("src/Sample.Module/OrderService.cs", """
namespace Sample.Module;

public sealed class OrderService
{
    public void PlanOrder()
    {
    }
}
""");

        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new ApplicationTaskScheduler());
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, workspace.Paths, systemConfig);
        var queryService = new FileCodeGraphQueryService(workspace.Paths, builder);
        var planner = new PlannerService(new CardParser(), new TaskDecomposer(), new StubGitClient(), taskGraphService, builder, queryService);

        var analysis = planner.AnalyzeCardScope(cardPath);
        var tasks = planner.PlanCard(cardPath, systemConfig);

        Assert.Contains("Sample.Module", analysis.Modules);
        Assert.Contains("src/Sample.Module/OrderService.cs", analysis.Files);
        Assert.Contains(analysis.Callables, callable => callable.EndsWith(".PlanOrder", StringComparison.Ordinal));
        Assert.All(tasks, task => Assert.Equal("Sample.Module", task.Metadata["codegraph_modules"]));
    }
}
