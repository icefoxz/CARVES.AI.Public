using Carves.Runtime.Domain.Cards;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.CodeGraph;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerService
{
    private readonly CardParser cardParser;
    private readonly TaskDecomposer taskDecomposer;
    private readonly IGitClient gitClient;
    private readonly TaskGraphService taskGraphService;
    private readonly ICodeGraphBuilder? codeGraphBuilder;
    private readonly ICodeGraphQueryService? codeGraphQueryService;

    public PlannerService(
        CardParser cardParser,
        TaskDecomposer taskDecomposer,
        IGitClient gitClient,
        TaskGraphService taskGraphService,
        ICodeGraphBuilder? codeGraphBuilder = null,
        ICodeGraphQueryService? codeGraphQueryService = null)
    {
        this.cardParser = cardParser;
        this.taskDecomposer = taskDecomposer;
        this.gitClient = gitClient;
        this.taskGraphService = taskGraphService;
        this.codeGraphBuilder = codeGraphBuilder;
        this.codeGraphQueryService = codeGraphQueryService;
    }

    public CardDefinition ParseCard(string path)
    {
        return cardParser.Parse(path);
    }

    public IReadOnlyList<TaskNode> PlanCard(string path, SystemConfig systemConfig)
    {
        var card = ParseCard(path);
        var baseCommit = gitClient.TryGetCurrentCommit(Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory());
        var scopeAnalysis = AnalyzeCardScope(card);
        var impactAnalysis = AnalyzeCardImpact(card);
        var tasks = taskDecomposer.Decompose(card, baseCommit, [systemConfig.DefaultTestCommand], scopeAnalysis, impactAnalysis);
        taskGraphService.AddTasks(tasks);
        return tasks;
    }

    public CodeGraphScopeAnalysis AnalyzeCardScope(string path)
    {
        return AnalyzeCardScope(ParseCard(path));
    }

    public CodeGraphScopeAnalysis AnalyzeCardScope(CardDefinition card)
    {
        if (codeGraphQueryService is null)
        {
            return CodeGraphScopeAnalysis.Empty;
        }

        return codeGraphQueryService.AnalyzeScope(card.Scope);
    }

    public CodeGraphImpactAnalysis AnalyzeCardImpact(string path)
    {
        return AnalyzeCardImpact(ParseCard(path));
    }

    public CodeGraphImpactAnalysis AnalyzeCardImpact(CardDefinition card)
    {
        return codeGraphQueryService?.AnalyzeImpact(card.Scope) ?? CodeGraphImpactAnalysis.Empty;
    }
}
