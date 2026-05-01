using System.Text;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Memory;

namespace Carves.Runtime.Application.Memory;

public sealed class ActiveMemoryReadService
{
    private readonly ControlPlanePaths paths;
    private readonly RuntimeMemoryPromotionService promotionService;

    public ActiveMemoryReadService(ControlPlanePaths paths)
    {
        this.paths = paths;
        promotionService = new RuntimeMemoryPromotionService(paths);
    }

    public IReadOnlyList<MemoryDocument> LoadCompatibleDocuments(string category)
    {
        var root = Path.Combine(paths.AiRoot, "memory", category);
        if (!Directory.Exists(root))
        {
            return Array.Empty<MemoryDocument>();
        }

        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new MemoryDocument(
                Path.GetRelativePath(paths.RepoRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                category,
                Path.GetFileNameWithoutExtension(path),
                File.ReadAllText(path)))
            .ToArray();
    }

    public IReadOnlyList<MemoryDocument> LoadProjectDocumentsWithProjectedFacts(IReadOnlyList<string> scopeHints, int take = 200)
    {
        var documents = LoadCompatibleDocuments("project");
        var projectedFacts = ListFacts(scopeHints, category: null, includeInactiveFacts: false, take)
            .Select(ProjectFactToDocument);

        return documents
            .Concat(projectedFacts)
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<TemporalMemoryFactRecord> ListFacts(
        IReadOnlyList<string>? scopeHints = null,
        string? category = null,
        bool includeInactiveFacts = false,
        int take = 200)
    {
        var bufferedTake = Math.Max(take * 4, 200);
        var facts = includeInactiveFacts
            ? promotionService.ListFacts(scope: null, tier: null, status: null, take: bufferedTake)
            : promotionService.ListActiveFacts(scope: null, take: bufferedTake);

        return facts
            .Where(item => MatchesCategory(item, category))
            .Where(item => MatchesScopeHints(item, scopeHints))
            .OrderByDescending(item => item.ValidFromUtc)
            .ThenByDescending(item => item.FactId, StringComparer.Ordinal)
            .Take(Math.Max(1, take))
            .ToArray();
    }

    private MemoryDocument ProjectFactToDocument(TemporalMemoryFactRecord fact)
    {
        var path = fact.TargetMemoryPath ?? $".ai/evidence/facts/{fact.FactId}.json";
        var builder = new StringBuilder();
        builder.AppendLine($"# {fact.Title}");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(fact.Summary))
        {
            builder.AppendLine(fact.Summary);
            builder.AppendLine();
        }

        builder.AppendLine(fact.Statement);
        builder.AppendLine();
        builder.AppendLine($"Scope: {fact.Scope}");
        builder.AppendLine($"Tier: {fact.Tier}");
        builder.AppendLine($"Status: {fact.Status}");
        builder.AppendLine($"FactId: {fact.FactId}");

        return new MemoryDocument(path, NormalizeCategory(fact.Category), fact.Title, builder.ToString().Trim());
    }

    private static bool MatchesCategory(TemporalMemoryFactRecord fact, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        return string.Equals(NormalizeCategory(fact.Category), category, StringComparison.OrdinalIgnoreCase)
               || string.Equals(fact.Category, category, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesScopeHints(TemporalMemoryFactRecord fact, IReadOnlyList<string>? scopeHints)
    {
        if (scopeHints is null || scopeHints.Count == 0)
        {
            return true;
        }

        return scopeHints.Any(hint =>
            !string.IsNullOrWhiteSpace(hint)
            && (string.Equals(fact.Scope, hint, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fact.TaskScope, hint, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fact.CommitScope, hint, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizeCategory(string category)
    {
        return string.Equals(category, "module", StringComparison.OrdinalIgnoreCase)
            ? "modules"
            : category;
    }
}
