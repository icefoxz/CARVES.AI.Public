namespace Carves.Runtime.Application.Interaction;

public sealed class PromptProtocolService
{
    private readonly string templateRoot;
    private readonly IReadOnlyDictionary<string, string> templateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["intent-summary"] = "intent-summary.template.md",
        ["card-proposal"] = "card-proposal.template.md",
        ["task-proposal"] = "task-proposal.template.md",
        ["review-explanation"] = "review-explanation.template.md",
    };

    public PromptProtocolService(string repoRoot)
    {
        templateRoot = ResolveTemplateRoot(repoRoot);
    }

    public IReadOnlyList<PromptTemplateDefinition> GetTemplates()
    {
        return templateMap.Keys.Select(GetTemplate).ToArray();
    }

    public PromptTemplateDefinition GetTemplate(string templateId)
    {
        if (!templateMap.TryGetValue(templateId, out var fileName))
        {
            throw new InvalidOperationException($"Prompt template '{templateId}' is not defined.");
        }

        var path = Path.Combine(templateRoot, fileName);
        var body = File.ReadAllText(path);
        var sections = File.ReadLines(path)
            .Where(line => line.StartsWith("## ", StringComparison.Ordinal))
            .Select(line => line[3..].Trim())
            .ToArray();
        var summary = File.ReadLines(path)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            ?.Trim()
            ?? $"Template {templateId}";

        return new PromptTemplateDefinition(templateId, "1.0", ResolveContext(templateId), path, sections, summary, body);
    }

    public PromptTemplateDefinition ResolveForPhase(ConversationPhase phase)
    {
        return phase switch
        {
            ConversationPhase.Intent => GetTemplate("intent-summary"),
            ConversationPhase.Cards => GetTemplate("card-proposal"),
            ConversationPhase.Tasks or ConversationPhase.Execution => GetTemplate("task-proposal"),
            ConversationPhase.Review => GetTemplate("review-explanation"),
            _ => GetTemplate("intent-summary"),
        };
    }

    private static string ResolveContext(string templateId)
    {
        return templateId switch
        {
            "intent-summary" => "intent",
            "card-proposal" => "cards",
            "task-proposal" => "tasks",
            "review-explanation" => "review",
            _ => "interaction",
        };
    }

    private static string ResolveTemplateRoot(string repoRoot)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "templates", "interaction"),
            Path.Combine(repoRoot, "templates", "interaction"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "templates", "interaction")),
        };

        var root = candidates.FirstOrDefault(Directory.Exists);
        return root ?? throw new InvalidOperationException("CARVES prompt template assets were not found.");
    }
}
